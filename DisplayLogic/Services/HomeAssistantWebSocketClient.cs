using System.Security.Authentication;
using System.Text.Json;
using DisplayLogic.Models;
using DisplayLogic.SharedInterfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Services
{
    public class HomeAssistantWebSocketClient : IHomeAssistantWebSocketClient
    {
        private readonly IWebSocketClient _webSocketClient;
        private readonly string _accessToken;
        private readonly IUserNotifier _notifier;
        private readonly ILogger<HomeAssistantWebSocketClient> _logger;

        private int _lastCommandId = 0;
        private int _deviceRegistryCommandId;
        private int _areaRegistryCommandId;
        private int _updateAreaCommandId;

        public List<MqttDevice> Devices { get; private set; } = [];
        public List<Area> Areas { get; private set; } = [];

        private TaskCompletionSource<bool>? _authSuccessTcs;
        private TaskCompletionSource<Exception>? _authFailureTcs;
        private TaskCompletionSource<bool>? _registryReceivedTcs;
        private bool _deviceRegistryReceived = false;
        private bool _areaRegistryReceived = false;

        public bool IsConnected { get; private set; } = false;

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
            _logger.LogDebug("HomeAssistantWebSocketClient created with WebSocketClient: {accessToken}", accessToken);
        }

        public async Task ConnectAsync()
        {
            _logger.LogInformation("Starting WebSocket connection...");
            await _webSocketClient.StartAsync();

            using (CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(30)))
            {
                Task initializationProcedure = WaitForInitializationConditionAsync();

                Task completed = await Task.WhenAny(initializationProcedure, Task.Delay(Timeout.Infinite, timeoutCts.Token));
                if (completed != initializationProcedure)
                {
                    _logger.LogError("Initialization timed out");
                    throw new TimeoutException("Initialization timed out");
                }

                await initializationProcedure;
                _logger.LogInformation("WebSocket connection established.");
            }
        }

        private async Task WaitForInitializationConditionAsync()
        {
            _authSuccessTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _authFailureTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            _registryReceivedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            await _notifier.NotifyAsync("WebSocket", "Waiting for authentication...");
            Task authResult = await Task.WhenAny(_authSuccessTcs.Task, _authFailureTcs.Task);

            if (authResult == _authFailureTcs.Task)
            {
                _logger.LogWarning("Authentication failure.");
                throw _authFailureTcs.Task.Result;
            }

            IsConnected = true;
            _logger.LogInformation("Authentication successful, waiting for registry data...");
            await _notifier.NotifyAsync("WebSocket", "Authentication successful, waiting for registry data...");
            _ = await _registryReceivedTcs.Task;
        }

        public async Task StopAsync()
        {
            _logger.LogInformation("Stopping WebSocket connection...");
            await _webSocketClient.StopAsync();
            IsConnected = false;
            _logger.LogInformation("WebSocket connection stopped.");
        }

        private async Task OnPayloadReceived(string payload)
        {
            try
            {
                _logger.LogDebug("Payload received: {payload}", payload);
                JsonDocument? jsonDocument = JsonDocument.Parse(payload);
                JsonElement jsonRoot = jsonDocument.RootElement;

                if (jsonRoot.TryGetProperty("type", out JsonElement typeProp))
                {
                    string? type = typeProp.GetString();

                    if (type == "auth_required")
                    {
                        await SendAuthAsync();
                        _logger.LogDebug("WebSocket server requested authentication.");
                    }
                    else if (type == "auth_ok")
                    {
                        _ = _authSuccessTcs?.TrySetResult(true);
                        await SendDeviceRegistryCommandAsync();
                        await SendAreaRegistryCommandAsync();
                        _logger.LogInformation("Authentication OK received from WebSocket.");
                    }
                    else if (type == "auth_invalid")
                    {
                        AuthenticationException ex = new("WebSocket auth failed: Invalid token");
                        _ = _authFailureTcs?.TrySetResult(ex);
                        _logger.LogError(ex, "WebSocket auth failed: Invalid token");
                        await _notifier.NotifyAsync("Error", ex.Message);
                    }
                    else if (type == "result")
                    {
                        if (jsonRoot.TryGetProperty("id", out JsonElement idProp) && jsonRoot.TryGetProperty("result", out JsonElement resultProp))
                        {
                            int id = idProp.GetInt32();

                            if (id == _deviceRegistryCommandId)
                            {
                                Devices = ParseMqttDevices(resultProp);
                                _deviceRegistryReceived = true;
                                TryCompleteRegistryInit();
                            }
                            else if (id == _areaRegistryCommandId)
                            {
                                Areas = ParseAreas(resultProp);
                                _areaRegistryReceived = true;
                                TryCompleteRegistryInit();
                            }
                            else if (id == _updateAreaCommandId)
                            {
                                string? deviceId = resultProp.TryGetProperty("id", out JsonElement deviceIdProp) ? deviceIdProp.GetString() : null;
                                string? areaId = resultProp.TryGetProperty("area_id", out JsonElement areaIdProp) ? areaIdProp.GetString() : null;
                                await _notifier.NotifyAsync("WebSocket", $"Device update result for ID {id}: Device {deviceId} updated to area {areaId}.");

                                MqttDevice? device = Devices.FirstOrDefault(matchedDevice =>
                                {
                                    return matchedDevice.id == deviceId;
                                });
                                if (device != null)
                                {
                                    device.area_id = areaId;
                                }
                            }
                            else
                            {
                                await _notifier.NotifyAsync("Error", $"Received result for unknown command ID: {id}");
                                _logger.LogWarning("Received result for unknown command ID: {id}", id);
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                await _notifier.NotifyAsync("Error", $"JSON parse error in WebSocket payload: {ex.Message}");
                _logger.LogError(ex, "Error in JSON parse: {errMessage}", ex.Message);
            }
            catch (Exception ex)
            {
                await _notifier.NotifyAsync("Error", $"Unexpected error processing WebSocket payload: {ex.Message}");
                _logger.LogError(ex, "Error in processing WebSocket payload: {errMessage}", ex.Message);
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

        private void TryCompleteRegistryInit()
        {
            if (_deviceRegistryReceived && _areaRegistryReceived)
            {
                _ = (_registryReceivedTcs?.TrySetResult(true));
                _logger.LogInformation("Device and Area registries received, initialization complete.");
            }
        }

        private Task SendDeviceRegistryCommandAsync()
        {
            _deviceRegistryCommandId = Interlocked.Increment(ref _lastCommandId);
            object deviceRegistryCommand = new
            {
                id = _deviceRegistryCommandId,
                type = "config/device_registry/list"
            };
            string jsonString = JsonSerializer.Serialize(deviceRegistryCommand);
            _logger.LogInformation("Sending device registry command: {Command}", jsonString);
            return _webSocketClient.SendAsync(jsonString);
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

        private Task SendAreaRegistryCommandAsync()
        {
            _areaRegistryCommandId = Interlocked.Increment(ref _lastCommandId);
            object deviceRegistryCommand = new
            {
                id = _areaRegistryCommandId,
                type = "config/area_registry/list"
            };
            string jsonString = JsonSerializer.Serialize(deviceRegistryCommand);
            _logger.LogInformation("Sending area registry command: {Command}", jsonString);
            return _webSocketClient.SendAsync(jsonString);
        }

        private static List<Area> ParseAreas(JsonElement resultProp)
        {
            List<JsonElement>? areaElements = JsonSerializer.Deserialize<List<JsonElement>>(resultProp);
            if (areaElements == null)
            {
                return [];
            }

            return [.. areaElements.Select(area =>
            {
                return new Area
                {
                    area_id = area.GetProperty("area_id").GetString() ?? "unknown",
                    floor_id = area.TryGetProperty("floor_id", out JsonElement floorIdProp) && floorIdProp.ValueKind != JsonValueKind.Null ? floorIdProp.GetString() : null,
                    icon = area.TryGetProperty("icon", out JsonElement iconProp) && iconProp.ValueKind != JsonValueKind.Null ? iconProp.GetString() : null,
                    name = area.GetProperty("name").GetString() ?? "Unnamed",
                };
            })];
        }

        public async Task UpdateDeviceAreaAsync(string uniqueId, string areaId)
        {
            _logger.LogInformation("UpdateDeviceAsync called with uniqueId: {uniqueId}, areaId: {areaId}.", uniqueId, areaId);
            if (string.IsNullOrWhiteSpace(areaId))
            {
                _logger.LogError("Area ID cannot be null or empty.");
                throw new ArgumentException("Area ID cannot be null or empty", nameof(areaId));
            }

            MqttDevice? device = Devices?.FirstOrDefault(device =>
            {
                return device.id == uniqueId;
            });

            if (device == null)
            {
                _logger.LogError("Device with id '{uniqueId}' not found in registry.", uniqueId);
                throw new InvalidOperationException($"Device with id '{uniqueId}' not found in registry.");
            }

            _updateAreaCommandId = Interlocked.Increment(ref _lastCommandId);
            object updateCommand = new
            {
                id = _updateAreaCommandId,
                type = "config/device_registry/update",
                device_id = device.id,
                area_id = areaId
            };

            string json = JsonSerializer.Serialize(updateCommand);
            _logger.LogInformation("Sending device area update: {Command}", json);
            await _webSocketClient.SendAsync(json);
        }
    }

}
