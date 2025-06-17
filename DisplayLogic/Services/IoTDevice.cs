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
        private readonly string _mqttBroker;
        private readonly string _restEndpoint;

        public IoTDevice(string mqttBroker, string restEndpoint)
        {
            // Initialize MQTT client and HTTP client
            _mqttFactory = new MqttClientFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();
            _httpClient = new HttpClient();
            _mqttBroker = mqttBroker ?? throw new ArgumentNullException(nameof(mqttBroker));
            _restEndpoint = restEndpoint ?? throw new ArgumentNullException(nameof(restEndpoint));
            IsConnected = true; // Only false when rest is connected
        }

        // Public read-only property indicating connection status
        public bool IsConnected { get; private set; }

        public async Task ConnectAsync()
        {
            MqttClientOptions MqttClientOptions = new MqttClientOptionsBuilder()
                 .WithTcpServer(_mqttBroker)
                 .Build();

            MqttClientConnectResult response = await _mqttClient.ConnectAsync(MqttClientOptions, CancellationToken.None);
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
                throw new InvalidOperationException("MQTT client is not connected.");

            MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message, CancellationToken.None);
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
                        bytes = Array.Empty<byte>();
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
                    tcs.TrySetResult(msg);
                }
                return Task.CompletedTask;
            };

            await _mqttClient.SubscribeAsync(topic);
            return await tcs.Task;
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
