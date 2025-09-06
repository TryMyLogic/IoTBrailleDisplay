using System.Security.Authentication;
using System.Text.Json;
using DisplayLogic.Models;
using DisplayLogic.SharedInterfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Services
{
    /// <summary>
    /// Implements a WebSocket client for interacting with a Home Assistant server,
    /// managing authentication, device and area registry retrieval, and device area updates via WebSocket communication.
    /// </summary>
    /// <remarks>
    /// This class communicates with Home Assistant's WebSocket API to fetch and update device and area registries,
    /// specifically for MQTT-based devices.
    /// This class maintains internal state (e.g., <see cref="Devices"/>, <see cref="Areas"/>, <see cref="IsConnected"/>)
    /// and relies on an injected <see cref="IWebSocketClient"/> for WebSocket communication and <see cref="IUserNotifier"/>
    /// to provide user feedback. All operations are optionally logged via the injected
    /// <see cref="ILogger{T}"/>.
    /// </remarks>
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

        /// <summary>
        /// Gets the list of MQTT devices retrieved from the Home Assistant device registry.
        /// </summary>
        /// <value>
        /// A list of <see cref="MqttDevice"/> objects, each representing a device with metadata
        /// like identifiers and the manufacturer. The list is empty until
        /// <see cref="ConnectAsync"/> completes successfully.
        /// </value>
        /// <remarks>
        /// The device registry is a Home Assistant concept that catalogs devices, including their unique
        /// identifiers and attributes. For this list, only MQTT-based devices are included,
        /// filtered by their "mqtt" identifier prefix.
        /// The list is updated during initialization (<see cref="ConnectAsync"/>)
        /// and may be modified when processing area update results from <see cref="UpdateDeviceAreaAsync"/>.
        /// </remarks>
        public List<MqttDevice> Devices { get; private set; } = [];
        /// <summary>
        /// Gets the list of areas retrieved from the Home Assistant area registry.
        /// </summary>
        /// <value>
        /// A list of <see cref="Area"/> objects, each representing an area with metadata like ID, name, 
        /// and optional floor & icon. The list is updated during initialization (<see cref="ConnectAsync"/>)
        /// </value>
        /// <remarks>
        /// The area registry in Home Assistant organizes devices into logical or physical spaces (e.g., "Kitchen").
        /// The list is populated during initialization (<see cref="ConnectAsync"/>) and remains unchanged.
        /// </remarks>
        public List<Area> Areas { get; private set; } = [];

        private TaskCompletionSource<bool>? _authSuccessTcs;
        private TaskCompletionSource<Exception>? _authFailureTcs;
        private TaskCompletionSource<bool>? _registryReceivedTcs;
        private bool _deviceRegistryReceived = false;
        private bool _areaRegistryReceived = false;

        /// <summary>
        /// Indicates whether the WebSocket connection to the Home Assistant server is active and authenticated.
        /// </summary>
        /// <value>
        /// <c>true</c> if connected and authenticated; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// Set to <c>true</c> after successful authentication in <see cref="ConnectAsync"/> 
        /// and reset to <c>false</c> in <see cref="StopAsync"/>. 
        /// Reflects the current operational state of the WebSocket connection.
        /// </remarks>
        public bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Primary constructor for creating and configuring a <see cref="HomeAssistantWebSocketClient"/>.
        /// </summary>
        /// <param name="webSocketClient">
        /// The WebSocket client for communication with the Home Assistant server. Must not be null.
        /// </param>
        /// <param name="notifier">
        /// The notifier for sending user feedback (e.g., errors, status updates). Must not be null.
        /// </param>
        /// <param name="accessToken">
        /// The Home Assistant <b>long-lived access token</b> used for WebSocket API authentication. 
        /// Must not be null or empty. See 
        /// <see href="https://community.home-assistant.io/t/how-to-get-long-lived-access-token/162159/5">
        /// for how to create a long-lived access token
        /// </see>.
        /// </param>
        /// <param name="logger">
        /// The logger for diagnostics, using <see cref="ILogger{T}"/> with Serilog for flexibility.
        /// Optional and defaults to <see cref="NullLogger{T}"/> with no logging.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="webSocketClient"/>, <paramref name="notifier"/>, or 
        /// <paramref name="accessToken"/> is null.
        /// </exception>
        /// <remarks>
        /// Subscribes to the <see cref="IWebSocketClient.PayloadReceived"/> event to handle incoming 
        /// WebSocket messages. Initializes empty <see cref="Devices"/> and <see cref="Areas"/> lists 
        /// and logs creation details.
        /// </remarks>
        /// <seealso href="https://developers.home-assistant.io/docs/api/websocket#authentication-phase">
        /// Home Assistant WebSocket Authentication Phase
        /// </seealso>
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

        /// <summary>
        /// Connects to the Home Assistant WebSocket server, authenticates, and retrieves device and area registries.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="TimeoutException">Thrown if authentication and registry retrieval exceed 30 seconds.</exception>
        /// <exception cref="AuthenticationException">Thrown if the access token is rejected by the server.</exception>
        /// <exception cref="Exception">Thrown if <see cref="IWebSocketClient.StartAsync"/> fails or other unexpected errors occur.</exception>
        /// <remarks>
        /// Waits for authentication and registry data before completing. On success, sets <see cref="IsConnected"/> to <c>true</c> and populates <see cref="Devices"/> and <see cref="Areas"/>. 
        /// Uses <see cref="ILogger{T}"/> for logging and <see cref="IUserNotifier"/> for progress or error notifications. 
        /// <b>Only MQTT devices are included in the device registry.</b> 
        /// </remarks>
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

        /// <summary>
        /// Waits for authentication and registry data during WebSocket initialization.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationException">Thrown if authentication fails.</exception>
        /// <remarks>
        /// Coordinates authentication and registry retrieval for <see cref="ConnectAsync"/>, 
        /// sets <see cref="IsConnected"/>, and notifies via <see cref="IUserNotifier"/>. 
        /// Logs events via <see cref="ILogger{T}"/>.
        /// </remarks>
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

        /// <summary>
        /// Stops the WebSocket connection to the Home Assistant server and updates the connection state.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if <see cref="IWebSocketClient.StopAsync"/> fails.</exception>
        /// <remarks>
        /// Closes the WebSocket connection and sets <see cref="IsConnected"/> to <c>false</c>. 
        /// Logs the stopping process via <see cref="ILogger{T}"/>.
        /// Called after <see cref="ConnectAsync"/> when client is no longer required, to release resources.
        /// </remarks>
        public async Task StopAsync()
        {
            _logger.LogInformation("Stopping WebSocket connection...");
            await _webSocketClient.StopAsync();
            IsConnected = false;
            _logger.LogInformation("WebSocket connection stopped.");
        }

        /// <summary>
        /// Processes incoming WebSocket payloads from the Home Assistant server.
        /// </summary>
        /// <param name="payload">The raw JSON payload received from the server. Must not be null or empty.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="JsonException">Thrown if the payload cannot be parsed as valid JSON.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during payload processing.</exception>
        /// <remarks>
        /// Handles messages such as authentication responses, device and area registry results and device update confirmations. 
        /// Updates <see cref="Devices"/>, <see cref="Areas"/>, and internal state flags, sends notifications via <see cref="IUserNotifier"/>, 
        /// and logs events via <see cref="ILogger{T}"/>. Requires a valid, active WebSocket connection and well-formed JSON payloads.
        /// </remarks>
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

        /// <summary>
        /// Sends an authentication command to the Home Assistant WebSocket server using the access token.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if <see cref="IWebSocketClient.SendAsync"/> fails.</exception>
        /// <remarks>
        /// Serializes the authentication command and sends it via the WebSocket client when the server requests authentication.
        /// Logs the command via <see cref="ILogger{T}"/>.
        /// </remarks>
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

        /// <summary>
        /// Completes initialization when both device and area registries are received.
        /// </summary>
        /// <remarks>
        /// Checks <see cref="_deviceRegistryReceived"/> and <see cref="_areaRegistryReceived"/>, and signals completion via <see cref="_registryReceivedTcs"/> if both are true. 
        /// Called after processing registry results and logs completion via <see cref="ILogger{T}"/>.
        /// </remarks>
        private void TryCompleteRegistryInit()
        {
            if (_deviceRegistryReceived && _areaRegistryReceived)
            {
                _ = (_registryReceivedTcs?.TrySetResult(true));
                _logger.LogInformation("Device and Area registries received, initialization complete.");
            }
        }

        /// <summary>
        /// Sends a command to retrieve the Home Assistant device registry.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if <see cref="IWebSocketClient.SendAsync"/> fails.</exception>
        /// <remarks>
        /// Serializes a device registry list command, assigns a unique ID (<see cref="_deviceRegistryCommandId"/>), and sends it via the WebSocket client. 
        /// Logs the command via <see cref="ILogger{T}"/>.
        /// Similar to <see cref="SendAreaRegistryCommandAsync"/> but targets devices.
        /// </remarks>
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

        /// <summary>
        /// Parses a JSON element into a list of MQTT devices from the Home Assistant device registry.
        /// </summary>
        /// <param name="resultProp">The JSON element containing device registry data. Must be a valid JSON array.</param>
        /// <returns>A list of <see cref="MqttDevice"/> objects filtered for MQTT-based devices, or an empty list if input is null or invalid.</returns>
        /// <exception cref="JsonException">Thrown if the JSON structure is invalid (e.g., missing required fields like "id").</exception>
        /// <remarks>
        /// Extracts metadata corresponding to the <see cref="MqttDevice"/> properties from the JSON result, 
        /// filters for devices with "mqtt" identifiers, and handles optional fields with fallback defaults.
        /// </remarks>
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

        /// <summary>
        /// Sends a command to retrieve the Home Assistant area registry.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if <see cref="IWebSocketClient.SendAsync"/> fails.</exception>
        /// <remarks>
        /// Serializes an area registry list command, assigns a unique ID (<see cref="_areaRegistryCommandId"/>), and sends it via the WebSocket client. 
        /// Logs the command via <see cref="ILogger{T}"/>. Similar to <see cref="SendDeviceRegistryCommandAsync"/> but targets areas.
        /// </remarks>
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

        /// <summary>
        /// Parses a JSON element into a list of areas from the Home Assistant area registry.
        /// </summary>
        /// <param name="resultProp">The JSON element containing area registry data. Must be a valid JSON array.</param>
        /// <returns>A list of <see cref="Area"/> objects, or an empty list if input is null or invalid.</returns>
        /// <exception cref="JsonException">Thrown if the JSON structure is invalid (e.g., missing required fields like "area_id" or "name").</exception>
        /// <remarks>
        /// Extracts area metadata corresponding to <see cref="Area"/> properties from the JSON result. Handles optional fields with null values and provides defaults for required fields.
        /// </remarks>
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

        /// <summary>
        /// Updates the area assignment for a device in the Home Assistant device registry.
        /// </summary>
        /// <param name="uniqueId">The unique ID of the device. Must exist in <see cref="Devices"/>.</param>
        /// <param name="areaId">The ID of the area to assign. Must exist in <see cref="Areas"/> and not be null or empty.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="areaId"/> is null, empty, or not present in <see cref="Areas"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if no device with <paramref name="uniqueId"/> exists in <see cref="Devices"/>.</exception>
        /// <exception cref="Exception">Thrown if <see cref="IWebSocketClient.SendAsync"/> fails.</exception>
        /// <remarks>
        /// Sends a command to update the area of the device and updates <see cref="Devices"/> upon server confirmation. 
        /// Updates command IDs (<see cref="_lastCommandId"/> and <see cref="_updateAreaCommandId"/>), logs events via <see cref="ILogger{T}"/>, and notifies users via <see cref="IUserNotifier"/>.
        /// </remarks>
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
