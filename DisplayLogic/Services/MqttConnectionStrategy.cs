using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;

namespace DisplayLogic.Services
{
    public class MqttConnectionStrategy : IConnectionStrategy
    {
        private readonly IMqttClient _mqttClient;
        private string _receivedText = string.Empty;
        private readonly ILogger<MqttConnectionStrategy> _logger;
        private const string TopicSend = "braille/send";
        private const string TopicReceive = "braille/receive";

        public bool IsConnected => _mqttClient.IsConnected;

        public MqttConnectionStrategy(IMqttClient mqttClient, ILogger<MqttConnectionStrategy> logger)
        {
            _mqttClient = mqttClient;
            _logger = logger ?? NullLogger<MqttConnectionStrategy>.Instance;
            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                try
                {
                    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    _receivedText = payload;
                    _logger.LogInformation($"MQTT: Received text: {_receivedText}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MQTT: Error handling incoming message");
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
                    await _mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
                        .WithTcpServer("localhost")
                        .Build());
                }
                await _mqttClient.SubscribeAsync(TopicReceive);
                _logger.LogInformation("MQTT: Connected and subscribed to topic");
                return _mqttClient.IsConnected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT: Failed to connect or subscribe");
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

                await _mqttClient.PublishAsync(message);
                _logger.LogInformation("MQTT: Sent text to topic '{Topic}' : {Text}", TopicSend, text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT: Failed to send text to topic '{Topic}", TopicSend);
            }
        }
        public Task<string> ReceiveTextAsync()
        {
            try
            {
                if (_receivedText == null)
                {
                    _logger.LogWarning("MQTT: No text received yet");
                    return Task.FromResult(string.Empty);
                }
                return Task.FromResult(_receivedText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT: Error retrieving received text");
                return Task.FromResult(string.Empty);
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            try
            {
                await _mqttClient.DisconnectAsync();
                _logger.LogInformation("MQTT: Disconnected");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT: Disconnection failed");
                return false;
            }
        }
    }
}
