using System.Text.Json;
using DisplayLogic.Models;
using DisplayLogic.SharedInterfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Services
{
    public class HomeAssistantWebSocketClient
    {
        private readonly IWebSocketClient _webSocketClient;
        private readonly string _accessToken;
        private int _lastCommandId = 0;
        private readonly IUserNotifier _notifier;
        private readonly ILogger<HomeAssistantWebSocketClient> _logger;

        public event Action<List<MqttDevice>>? DeviceRegistryReceivedAndProcessed;

        public HomeAssistantWebSocketClient(
            IWebSocketClient webSocketClient,
            IUserNotifier notifier,
            string accessToken,
            ILogger<HomeAssistantWebSocketClient>? logger = null)
        {
            _webSocketClient = webSocketClient ?? throw new ArgumentNullException(nameof(webSocketClient));
            _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _logger = logger ?? NullLogger<HomeAssistantWebSocketClient>.Instance;

            _webSocketClient.PayloadReceived += async payload =>
            {
                await OnPayloadReceived(payload);
            };
        }


        private TaskCompletionSource<bool>? _authSuccessTcs;
        private TaskCompletionSource<Exception>? _authFailureTcs;

        public Task WaitForAuthCompletionAsync()
        {
            _authSuccessTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _authFailureTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            return Task.WhenAny(_authSuccessTcs.Task, _authFailureTcs.Task).ContinueWith(t =>
            {
                if (t.Result == _authFailureTcs.Task)
                {
                    throw _authFailureTcs.Task.Result;
                }
            });
        }

        private static List<MqttDevice> ParseMqttDevices(JsonElement resultProp)
        {
            List<JsonElement>? allDevices = JsonSerializer.Deserialize<List<JsonElement>>(resultProp);

            if (allDevices == null)
            {
                return [];
            }

            return allDevices
          ?.Where(device =>
          {
              return device.TryGetProperty("identifiers", out JsonElement identifiersProp) &&
                        identifiersProp.ValueKind == JsonValueKind.Array &&
                        identifiersProp[0].ValueKind == JsonValueKind.Array &&
                        identifiersProp[0][0].GetString() == "mqtt";
          })
          .Select(device =>
          {
              string defaultManufacturer = device.TryGetProperty("default_manufacturer", out JsonElement default_manufacturerProp) && default_manufacturerProp.ValueKind != JsonValueKind.Null ? default_manufacturerProp.GetString()! : "Unknown Manufacturer";
              string defaultModel = device.TryGetProperty("default_model", out JsonElement defaultModelProp) && defaultModelProp.ValueKind != JsonValueKind.Null ? defaultModelProp.GetString()! : "Unknown Model";
              string defaultName = device.TryGetProperty("default_name", out JsonElement defaultNameProp) && defaultNameProp.ValueKind != JsonValueKind.Null ? defaultNameProp.GetString()! : "Unknown Device";

              string manufacturer = device.TryGetProperty("manufacturer", out JsonElement manufacturerProp) && manufacturerProp.ValueKind != JsonValueKind.Null ? manufacturerProp.GetString()! : defaultManufacturer;
              string model = device.TryGetProperty("model", out JsonElement modelProp) && modelProp.ValueKind != JsonValueKind.Null ? modelProp.GetString()! : defaultModel;
              string name = device.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind != JsonValueKind.Null ? nameProp.GetString()! : defaultName;
              string id = device.GetProperty("id").GetString()!;

              string[][] identifiersArray = [.. device.GetProperty("identifiers").EnumerateArray()
              .Select(identifierArray =>
              {
                  return identifierArray.EnumerateArray()
                                    .Select(identifierElement =>
                                    {
                                        return identifierElement.GetString()!;
                                    })
                                    .ToArray();
              })];

              return new MqttDevice
              {
                  area_id = device.TryGetProperty("area_id", out JsonElement areaIdProp) && areaIdProp.ValueKind != JsonValueKind.Null ? areaIdProp.GetString() : null,
                  default_manufacturer = defaultManufacturer,
                  default_model = defaultModel,
                  default_name = defaultName,
                  hw_version = device.TryGetProperty("hw_version", out JsonElement hwVersionProp) && hwVersionProp.ValueKind != JsonValueKind.Null ? hwVersionProp.GetString() : null,
                  id = id,
                  identifiers = identifiersArray,
                  manufacturer = manufacturer,
                  model = model,
                  model_id = device.TryGetProperty("model_id", out JsonElement modelIdProp) && modelIdProp.ValueKind != JsonValueKind.Null ? modelIdProp.GetString() : null,
                  name = name,
                  name_by_user = device.TryGetProperty("name_by_user", out JsonElement nameByUserProp) && nameByUserProp.ValueKind != JsonValueKind.Null ? nameByUserProp.GetString() : null,
                  serial_number = device.TryGetProperty("serial_number", out JsonElement serialNumberProp) && serialNumberProp.ValueKind != JsonValueKind.Null ? serialNumberProp.GetString() : null,
                  sw_version = device.TryGetProperty("sw_version", out JsonElement swVersionProp) && swVersionProp.ValueKind != JsonValueKind.Null ? swVersionProp.GetString() : null,
                  via_device_id = device.TryGetProperty("via_device_id", out JsonElement viaDeviceIdProp) && viaDeviceIdProp.ValueKind != JsonValueKind.Null ? viaDeviceIdProp.GetString() : null
              };
          })
   .ToList() ?? [];
        }

        public async Task StartAsync()
        {
            await _webSocketClient.StartAsync();
        }

        public async Task StopAsync()
        {
            await _webSocketClient.StopAsync();
        }

        private async Task OnPayloadReceived(string payload)
        {
            try
            {
                JsonDocument? jsonDocument = JsonDocument.Parse(payload);
                JsonElement jsonRoot = jsonDocument.RootElement;

                if (jsonRoot.TryGetProperty("type", out JsonElement typeProp))
                {
                    string? type = typeProp.GetString();

                    if (type == "auth_required")
                    {
                        await SendAuthAsync();
                    }
                    else if (type == "auth_ok")
                    {
                        _ = _authSuccessTcs?.TrySetResult(true);
                        await SendDeviceRegistryCommandAsync();
                    }
                    else if (type == "auth_invalid")
                    {
                        InvalidOperationException ex = new("WebSocket auth failed: Invalid token.");
                        _ = _authFailureTcs?.TrySetResult(ex);
                        _logger.LogError(ex, "WebSocket auth failed: Invalid token.");
                        await _notifier.NotifyAsync("Error", ex.Message);
                    }
                    else if (type == "result")
                    {
                        if (jsonRoot.TryGetProperty("id", out JsonElement idProp) && idProp.GetInt32() == _lastCommandId)
                        {
                            if (jsonRoot.TryGetProperty("result", out JsonElement resultProp))
                            {
                                List<MqttDevice> mqttDevices = ParseMqttDevices(resultProp);
                                DeviceRegistryReceivedAndProcessed?.Invoke(mqttDevices);
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                await _notifier.NotifyAsync("Error", $"JSON parse error in WebSocket payload: {ex.Message}");
            }
            catch (Exception ex)
            {
                await _notifier.NotifyAsync("Error", $"Unexpected error processing WebSocket payload: {ex.Message}");
            }
        }

        private Task SendAuthAsync()
        {
            object authCommand = new
            {
                type = "auth",
                access_token = _accessToken
            };
            string jsonString = JsonSerializer.Serialize(authCommand);
            _logger.LogInformation("Sending auth command: {Command}", jsonString);
            return _webSocketClient.SendAsync(jsonString);
        }

        private Task SendDeviceRegistryCommandAsync()
        {
            _lastCommandId = Interlocked.Increment(ref _lastCommandId);
            object deviceRegistryCommand = new
            {
                id = _lastCommandId,
                type = "config/device_registry/list"
            };
            string jsonString = JsonSerializer.Serialize(deviceRegistryCommand);
            _logger.LogInformation("Sending device registry command: {Command}", jsonString);
            return _webSocketClient.SendAsync(jsonString);
        }
    }

}
