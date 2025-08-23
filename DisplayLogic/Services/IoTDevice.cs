using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;


namespace DisplayLogic.Services
{
    public class IoTDevice : IIoTDevice
    {
        private readonly IMqttClient _mqttClient;
        private readonly ILogger<IoTDevice> _logger;
        private readonly MqttClientFactory _mqttFactory;
        private readonly HttpClient _httpClient;
        private readonly int _mqttPort;
        private readonly string _mqttBroker;
        private readonly string _restEndpoint;
        private readonly MqttClientOptions _mqttOptions;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        public IoTDevice(string mqttBroker, string restEndpoint, int port = 1883, HttpClient? httpClient = null, ILogger<IoTDevice>? logger = null)
        {
            // Initialize MQTT client and HTTP client
            _logger = logger ?? NullLogger<IoTDevice>.Instance;
            _mqttFactory = new MqttClientFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();
            _httpClient = new HttpClient();
            _mqttBroker = mqttBroker ?? throw new ArgumentNullException(nameof(mqttBroker));
            _mqttPort = port;
            _restEndpoint = restEndpoint ?? throw new ArgumentNullException(nameof(restEndpoint));
            IsConnected = false; // Only false when rest is connected

            _mqttOptions = new MqttClientOptionsBuilder()
           .WithTcpServer(_mqttBroker, _mqttPort)
           .WithClientId("test-client")
           .WithCleanSession()
           .Build();

            _mqttClient.DisconnectedAsync += async err =>
            {
                Console.WriteLine($"Disconnected from MQTT broker. Reason: {err.Reason}");
                IsConnected = false;
                await Task.CompletedTask;
            };
            _logger.LogDebug($"Device instance created. Broker: {mqttBroker}. REST endpoint: {restEndpoint}. Port: {port} ");
        }

        // Public read-only property indicating connection status
        public bool IsConnected { get; private set; }

        public async Task ConnectAsync()
        {
            MqttClientConnectResult response = await _mqttClient.ConnectAsync(_mqttOptions, CancellationToken.None);
            // Update internal connection status based on broker's response
            IsConnected = response.ResultCode == MqttClientConnectResultCode.Success;
            _logger.LogInformation("Connected to MQTT broker.");

            if (!IsConnected)
            {
                _logger.LogError("Could not connect to MQTT broker.");
                throw new Exception("Could not connect to MQTT broker.");
            }
        }

        // Gracefully disconnect from the MQTT broker        
        // This method sends a DISCONNECT packet to the broker, ensuring a clean disconnection
        public async Task DisconnectAsync()
        {
            if (_mqttClient.IsConnected)
            {
                // This will send the DISCONNECT packet. Calling _Dispose_ without DisconnectAsync the
                // connection is closed in a "not clean" way. See MQTT specification for more details.
                _logger.LogInformation("Disconnecting from MQTT broker...");
                await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
                IsConnected = false;
                _logger.LogInformation("Disconnected from MQTT broker.");
            }
        }

        // Publish a message to the specified MQTT topic        
        public async Task PublishAsync(string topic, string payload)
        {
            // Ensure client is connected before attempting to publish
            if (!_mqttClient.IsConnected)
            {
                _logger.LogWarning("MQTT client is not connected.");
                throw new InvalidOperationException("MQTT client is not connected.");
            }

            MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            _logger.LogInformation($"Sending payload: {topic}, {payload}");
            _ = await _mqttClient.PublishAsync(message, CancellationToken.None);
            _logger.LogInformation("Payload sent to MQTT.");
        }

        public async Task PublishWithFallbackAsync(string topic, string payload)
        {
            if (_mqttClient.IsConnected)
            {
                await PublishAsync(topic, payload);
                _logger.LogInformation($"Subscribing via MQTT. Topic: {topic}, Payload: {payload}");
            }
            else
            {
                string restUrl = MapTopicToEndpoint(topic);
                StringContent content = new(payload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(restUrl, content);
                _logger.LogInformation($"MQTT disconnected, subscribing via REST fallback. RESTUrl: {restUrl}, Content: {content}.");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"REST publish failed.  Status: {response.StatusCode}");
                    throw new Exception($"REST publish failed. Status: {response.StatusCode}");
                }
            }
        }

        // Subscribe to an MQTT topic and return the first message received
        public async Task<string> SubscribeAsync(string topic)
        {
            _logger.LogInformation($"Attempting Subscription to MQTT topic: {topic}");
            TaskCompletionSource<string> tcs = new();

            // Register a handler to process received messages
            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {

                // Check if the received message matches the subscribed topic
                if (e.ApplicationMessage.Topic == topic)
                {
                    ReadOnlySequence<byte> payload = e.ApplicationMessage.Payload;
                    byte[] bytes;

                    // Convert the payload to a byte array
                    if (payload.IsEmpty)
                    {
                        _logger.LogDebug($"Received empty payload on topic: {topic}");
                        bytes = [];
                    }
                    else if (payload.IsSingleSegment)
                    {
                        bytes = payload.First.Span.ToArray();
                    }
                    else
                    {
                        bytes = payload.ToArray();
                    }

                    // Decode the byte array to a UTF-8 string
                    string msg = Encoding.UTF8.GetString(bytes);
                    _logger.LogDebug($"Received message on topic {topic}: {msg}");
                    _ = tcs.TrySetResult(msg);
                }
                return Task.CompletedTask;
            };
            // Added try catch block for logging purposes
            try
            {
                await _mqttClient.SubscribeAsync(topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to subscribe to topic {topic}.");
                throw;
            }
            return await tcs.Task;
        }

        public async Task<string> SubscribeWithFallbackAsync(string topic)
        {
            if (_mqttClient.IsConnected)
            {
                _logger.LogInformation($"Subscribing to topic {topic} via MQTT.");
                return await SubscribeAsync(topic);
            }
            else
            {
                string restUrl = MapTopicToEndpoint(topic);
                HttpResponseMessage response = await _httpClient.GetAsync(restUrl);
                _logger.LogInformation($"Subscribing to topic {restUrl} via REST fallback.");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"REST subscribe failed. Status: {response.StatusCode}");
                    throw new Exception($"REST subscribe failed. Status: {response.StatusCode}");
                }
                return await response.Content.ReadAsStringAsync();
            }
        }

        // Utility method to map an MQTT topic to a REST endpoint path
        private string MapTopicToEndpoint(string topic)
        {
            // Map MQTT request to REST as fallback
            // e,g "iot/devices/request" => $"{_restEndpoint}/devices"
            return $"{_restEndpoint}/{topic.Replace("iot/", "").Replace("/", "-")}";
        }
    }
}
