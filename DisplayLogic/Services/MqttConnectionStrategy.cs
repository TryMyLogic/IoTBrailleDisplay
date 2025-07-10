using System.Text;
using MQTTnet;

namespace DisplayLogic.Services
{
    public class MqttConnectionStrategy : IConnectionStrategy
    {
        private readonly IMqttClient _mqttClient;
        private string _receivedText = string.Empty;
        private const string TopicSend = "braille/send";
        private const string TopicReceive = "braille/receive";

        public bool IsConnected => _mqttClient.IsConnected;

        public MqttConnectionStrategy(IMqttClient mqttClient)
        {
            _mqttClient = mqttClient;
            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                _receivedText = payload;
                return Task.CompletedTask;
            };
        }

        public async Task<bool> ConnectAsync()
        {
            if (!_mqttClient.IsConnected)
            {
                await _mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
                    .WithTcpServer("localhost")
                    .Build());
            }
            await _mqttClient.SubscribeAsync(TopicReceive);
            return _mqttClient.IsConnected;
        }

        public Task SendTextAsync(string text)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(TopicSend)
                .WithPayload(text)
                .Build();
            return _mqttClient.PublishAsync(message);
        }
        public Task<string> ReceiveTextAsync()
        {
            return Task.FromResult(_receivedText);
        }

        public Task<bool> DisconnectAsync()
        {
            return _mqttClient.DisconnectAsync().ContinueWith(x => true);
        }
    }
}
