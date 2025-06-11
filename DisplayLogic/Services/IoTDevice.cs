namespace DisplayLogic.Services
{
    internal class IoTDevice : IIoTDevice
    {
        //  private readonly IMqttClient _mqttClient;
        private readonly HttpClient _httpClient;
        private readonly string _mqttBroker;
        private readonly string _restEndpoint;
        private readonly bool _isMqttConnected;

        public IoTDevice(string mqttBroker, string restEndpoint)
        {
            // _mqttClient = new MqttClient();
            _httpClient = new HttpClient();
            _mqttBroker = mqttBroker ?? throw new ArgumentNullException(nameof(mqttBroker));
            _restEndpoint = restEndpoint ?? throw new ArgumentNullException(nameof(restEndpoint));
            _isMqttConnected = true; // Only false when rest is connected
        }

        public bool IsConnected => _isMqttConnected;

        public Task ConnectAsync()
        {
            throw new NotImplementedException();
        }

        public Task DisconnectAsync()
        {
            throw new NotImplementedException();
        }

        public Task PublishAsync(string topic, string payload)
        {
            throw new NotImplementedException();
        }

        public Task<string> SubscribeAsync(string topic)
        {
            throw new NotImplementedException();
        }

        private string MapTopicToEndpoint(string topic)
        {
            // Map MQTT request to REST as fallback
            // e,g "iot/devices/request" => $"{_restEndpoint}/devices"
            return "";
        }
    }
}
