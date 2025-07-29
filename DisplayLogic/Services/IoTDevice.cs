using System.Buffers;
using System.Text;
using MQTTnet;


namespace DisplayLogic.Services
{
    public class IoTDevice : IIoTDevice
    {
        private readonly IMqttClient _mqttClient;
        private readonly MqttClientFactory _mqttFactory;
        private readonly HttpClient _httpClient;
        private readonly int _mqttPort;
        private readonly string _mqttBroker;
        private readonly string _restEndpoint;
        private readonly MqttClientOptions _mqttOptions;

        public IoTDevice(string mqttBroker, string restEndpoint, int port = 1883, HttpClient? httpClient = null)
        {
            // Initialize MQTT client and HTTP client
            _mqttFactory = new MqttClientFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();
            _httpClient = new HttpClient();
            _mqttBroker = mqttBroker ?? throw new ArgumentNullException(nameof(mqttBroker));
            _mqttPort = port;
            _restEndpoint = restEndpoint ?? throw new ArgumentNullException(nameof(restEndpoint));
            IsConnected = true; // Only false when rest is connected

            _mqttOptions = new MqttClientOptionsBuilder()
           .WithTcpServer(_mqttBroker, _mqttPort)
           .WithClientId("test-client")
           .WithCleanSession()
           .Build();

            _mqttClient.DisconnectedAsync += async err =>
            {
                Console.WriteLine($"Disconnected from MQTT broker. Reason: {err.Reason}");
                await Task.CompletedTask;
            };
        }

        // Public read-only property indicating connection status
        public bool IsConnected { get; private set; }

        public async Task ConnectAsync()
        {
            MqttClientConnectResult response = await _mqttClient.ConnectAsync(_mqttOptions, CancellationToken.None);
            // Update internal connection status based on broker's response
            IsConnected = response.ResultCode == MqttClientConnectResultCode.Success;

            if (!IsConnected)
            {
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
                await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
                IsConnected = false;
            }
        }

        // Publish a message to the specified MQTT topic        
        public async Task PublishAsync(string topic, string payload)
        {
            // Ensure client is connected before attempting to publish
            if (!_mqttClient.IsConnected)
            {
                throw new InvalidOperationException("MQTT client is not connected.");
            }

            MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            _ = await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        public async Task PublishWithFallbackAsync(string topic, string payload)
        {
            if (_mqttClient.IsConnected)
            {
                await PublishAsync(topic, payload);
            }
            else
            {
                string restUrl = MapTopicToEndpoint(topic);
                StringContent content = new(payload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(restUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"REST publish failed. Status: {response.StatusCode}");
                }
            }
        }

        // Subscribe to an MQTT topic and return the first message received
        public async Task<string> SubscribeAsync(string topic)
        {
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
                    _ = tcs.TrySetResult(msg);
                }
                return Task.CompletedTask;
            };

            _ = await _mqttClient.SubscribeAsync(topic);
            return await tcs.Task;
        }

        public async Task<string> SubscribeWithFallbackAsync(string topic)
        {
            if (_mqttClient.IsConnected)
            {
                return await SubscribeAsync(topic);
            }
            else
            {
                string restUrl = MapTopicToEndpoint(topic);
                HttpResponseMessage response = await _httpClient.GetAsync(restUrl);
                if (!response.IsSuccessStatusCode)
                {
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
