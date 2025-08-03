using System.Text;
using MQTTnet;
using Serilog;

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
                try
                {
                    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    _receivedText = payload;
                    Log.Information($"MQTT: Received text: {_receivedText}");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "MQTT: Error handling incoming message");
                }
                return Task.CompletedTask;
            };
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    _ = await _mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
                        .WithTcpServer("localhost")
                        .Build());
                }
                _ = await _mqttClient.SubscribeAsync(TopicReceive);
                Log.Information("MQTT: Connected and subscribed to topic");
                return _mqttClient.IsConnected;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MQTT: Failed to connect or subscribe");
                return false;
            }
        }

        public async Task SendTextAsync(string text)
        {
            try
            {
                MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                    .WithTopic(TopicSend)
                    .WithPayload(text)
                    .Build();

                _ = await _mqttClient.PublishAsync(message);
                Log.Information("MQTT: Sent text to topic '{Topic}' : {Text}", TopicSend, text);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MQTT: Failed to send text to topic '{Topic}", TopicSend);
            }
        }
        public Task<string> ReceiveTextAsync()
        {
            try
            {
                if (_receivedText == null)
                {
                    Log.Warning("MQTT: No text received yet");
                    return Task.FromResult(string.Empty);
                }
                return Task.FromResult(_receivedText);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MQTT: Error retrieving received text");
                return Task.FromResult(string.Empty);
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            try
            {
                await _mqttClient.DisconnectAsync();
                Log.Information("MQTT: Disconnected");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MQTT: Disconnection failed");
                return false;
            }
        }
    }
}
