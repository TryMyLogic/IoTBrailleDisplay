using System.Text.Json;
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

        public event Action<string>? DeviceRegistryReceived;

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
                                string deviceRegistryRawJson = resultProp.GetRawText();
                                DeviceRegistryReceived?.Invoke(deviceRegistryRawJson);
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
