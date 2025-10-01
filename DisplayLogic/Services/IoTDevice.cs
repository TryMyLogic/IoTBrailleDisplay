using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;


namespace DisplayLogic.Services
{
    /// <summary>
    /// Represents an IoT device communication service that abstracts
    /// MQTT messaging with REST fallback capabilities.
    /// 
    /// This class is responsible for:
    /// - Connecting and disconnecting from an MQTT broker.
    /// - Publishing messages to MQTT topics, with automatic REST fallback if MQTT is unavailable.
    /// - Subscribing to MQTT topics and processing responses.
    /// - Managing persistent subscriptions via callbacks for asynchronous message handling.
    /// 
    /// Thread safety: 
    /// A <see cref="SemaphoreSlim"/> is used to ensure that only one connect/disconnect 
    /// operation is performed at a time.
    /// </summary>
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
        private readonly Dictionary<string, Action<string>> _subscriptionCallbacks = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="IoTDevice"/> class.
        /// </summary>
        /// <param name="mqttBroker">Hostname or IP address of the MQTT broker.</param>
        /// <param name="restEndpoint">Base URL of the REST API endpoint used as a fallback when MQTT is unavailable.</param>
        /// <param name="port">Port number of the MQTT broker. Defaults to 1883 (standard MQTT).</param>
        /// <param name="httpClient">Optional HTTP client for REST fallback communication. If null, a new instance is created.</param>
        /// <param name="mqttClientFactory">Optional MQTT client factory. If null, a new factory is instantiated.</param>
        /// <param name="mqttClient">Optional preconfigured MQTT client. If null, a new client is created.</param>
        /// <param name="logger">Optional logger for structured logging. If null, a <see cref="NullLogger"/> is used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mqttBroker"/> or <paramref name="restEndpoint"/> is null.</exception>
        public IoTDevice(
            string mqttBroker,
            string restEndpoint,
            int port = 1883,
            HttpClient? httpClient = null,
            MqttClientFactory? mqttClientFactory = null,
            IMqttClient? mqttClient = null,
            ILogger<IoTDevice>? logger = null
            )
        {
            // Initialize MQTT client and HTTP client
            _logger = logger ?? NullLogger<IoTDevice>.Instance;
            _mqttFactory = mqttClientFactory ?? new MqttClientFactory();
            _mqttClient = mqttClient ?? _mqttFactory.CreateMqttClient();
            _httpClient = httpClient ?? new HttpClient();
            _mqttBroker = mqttBroker ?? throw new ArgumentNullException(nameof(mqttBroker));
            _mqttPort = port;
            _restEndpoint = restEndpoint ?? throw new ArgumentNullException(nameof(restEndpoint));
            IsConnected = false; //Set to false to prevent assumption that device is connected until ConnectAsync is called

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
            _mqttClient.ApplicationMessageReceivedAsync += HandleMessage;
        }

        /// <summary>
        /// Gets a value indicating whether the client is currently connected to the MQTT broker.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Establishes a connection to the MQTT broker.
        /// 
        /// If already connected, the method first disconnects and then reconnects.
        /// Sets <see cref="IsConnected"/> to <c>true</c> upon success.
        /// </summary>
        /// <exception cref="Exception">Thrown if the connection attempt fails.</exception>
        public async Task ConnectAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (_mqttClient.IsConnected)
                {
                    await _mqttClient.DisconnectAsync();
                }

                if (!IsConnected)
                {
                    MqttClientConnectResult response = await _mqttClient.ConnectAsync(_mqttOptions, CancellationToken.None);
                    IsConnected = response.ResultCode == MqttClientConnectResultCode.Success;
                    _logger.LogInformation("Connected to MQTT broker.");

                    if (!IsConnected)
                    {
                        throw new Exception($"Could not connect to MQTT broker. Result: {response.ResultCode}");
                    }

                    System.Diagnostics.Debug.WriteLine($"Connected to MQTT broker at {_mqttBroker}:{_mqttPort}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConnectAsync error: {ex.Message}");
                IsConnected = false;
                throw;
            }
            finally
            {
                _ = _connectionLock.Release();
            }
        }

        /// <summary>
        /// Gracefully disconnects from the MQTT broker by sending a DISCONNECT packet.
        /// Ensures <see cref="IsConnected"/> is set to <c>false</c> after disconnection.
        /// </summary>
        public async Task DisconnectAsync()
        {
            await _connectionLock.WaitAsync();
            try
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
            finally
            {
                _ = _connectionLock.Release();
            }
        }

        /// <summary>
        /// Publishes a message to the specified MQTT topic.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="payload">The message payload, encoded as a string.</param>
        /// <exception cref="InvalidOperationException">Thrown if the MQTT client is not connected.</exception>
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
            _logger.LogInformation("Sending payload: {topic}, {payload}", topic, payload);
            _ = await _mqttClient.PublishAsync(message, CancellationToken.None);
            _logger.LogInformation("Payload sent to MQTT.");

        }

        /// <summary>
        /// Attempts to publish a message using MQTT. 
        /// If MQTT is unavailable, the method is intended to fall back to REST.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="payload">The message payload.</param>
        /// <remarks>
        /// ⚠️ REST fallback is not currently implemented.  
        /// Calling this method when the MQTT client is disconnected will fail.
        /// </remarks>
        /// <exception cref="NotImplementedException">
        /// Thrown if fallback is attempted while MQTT is disconnected.
        /// </exception>
        public async Task PublishWithFallbackAsync(string topic, string payload)
        {
            if (_mqttClient.IsConnected)
            {
                await PublishAsync(topic, payload);
                _logger.LogInformation("Subscribing via MQTT. Topic: {topic}, Payload: {payload}", topic, payload);
            }
            else
            {
                string restUrl = MapTopicToEndpoint(topic);
                StringContent content = new(payload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(restUrl, content);
                _logger.LogInformation("MQTT disconnected, subscribing via REST fallback. RESTUrl: {restUrl}, Content: {content}.", restUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("REST publish failed.  Status: {StatusCode}", response.StatusCode);
                    throw new Exception($"REST publish failed. Status: {response.StatusCode}");
                }
            }
        }

        /// <summary>
        /// Subscribes to an MQTT topic and asynchronously returns the first received message.
        /// </summary>
        /// <param name="topic">The topic to subscribe to.</param>
        /// <returns>The first received message as a string.</returns>
        /// <exception cref="Exception">Thrown if subscription fails.</exception>
        public async Task<string> SubscribeAsync(string topic)
        {
            _logger.LogInformation("Attempting Subscription to MQTT topic: {topic}", topic);
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
                        _logger.LogDebug("Received empty payload on topic: {topic}", topic);
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
                    _logger.LogDebug("Received message on topic {topic}: {msg}", topic, msg);
                    _ = tcs.TrySetResult(msg);
                }
                return Task.CompletedTask;
            };
            // Added try catch block for logging purposes
            try
            {
                _ = await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to topic {topic}.", topic);
                throw;
            }
            return await tcs.Task;
        }

        /// <summary>
        /// Subscribes to a topic, using MQTT if available, otherwise falls back to REST.
        /// </summary>
        /// <param name="topic">The topic to subscribe to.</param>
        /// <returns>The first message received.</returns>
        /// <exception cref="Exception">Thrown if subscription fails on both MQTT and REST.</exception>
        public async Task<string> SubscribeWithFallbackAsync(string topic)
        {
            if (_mqttClient.IsConnected)
            {
                return await Task.Run(async () =>
                {
                    TaskCompletionSource<string> tcs = new();
                    _subscriptionCallbacks[topic] = msg =>
                    {
                        _ = tcs.TrySetResult(msg);
                    };
                    _logger.LogInformation($"Subscribing to topic {topic} via MQTT.");
                    _ = await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
                    return await tcs.Task;
                });
            }
            else
            {
                string restUrl = MapTopicToEndpoint(topic);
                HttpResponseMessage response = await _httpClient.GetAsync(restUrl);
                _logger.LogInformation("Subscribing to topic {restUrl} via REST fallback.", restUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("REST subscribe failed. Status: {StatusCode}", response.StatusCode);
                    throw new Exception($"REST subscribe failed. Status: {response.StatusCode}");
                }
                return await response.Content.ReadAsStringAsync();
            }
        }

        /// <summary>
        /// Subscribes persistently to an MQTT topic and registers a callback that is invoked whenever a new message is received.
        /// </summary>
        /// <param name="topic">The MQTT topic to subscribe to.</param>
        /// <param name="callback">Callback action that is executed on every received message.</param>
        public async Task SubscribePersistentAsync(string topic, Action<string> callback)
        {

            _subscriptionCallbacks[topic] = callback;
            _ = await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
            System.Diagnostics.Debug.WriteLine($"Subscribed to MQTT topic: {topic}");
        }

        /// <summary>
        /// Internal handler that processes MQTT messages and dispatches them to registered subscription callbacks.
        /// </summary>
        private Task HandleMessage(MqttApplicationMessageReceivedEventArgs e)
        {
            string topic = e.ApplicationMessage.Topic;
            ReadOnlySequence<byte> payload = e.ApplicationMessage.Payload;
            byte[] bytes = payload.IsEmpty ? [] : (payload.IsSingleSegment ? payload.First.Span.ToArray() : payload.ToArray());
            string msg = Encoding.UTF8.GetString(bytes);

            if (_subscriptionCallbacks.TryGetValue(topic, out Action<string>? callback))
            {
                callback(msg); // Caller handles threading
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Converts an MQTT topic into a corresponding REST API endpoint.
        /// Example: "iot/devices/request" → "http://restEndpoint/devices-request"
        /// </summary>
        /// <param name="topic">MQTT topic string.</param>
        /// <returns>A REST endpoint path derived from the topic.</returns>
        private string MapTopicToEndpoint(string topic)
        {
            // Map MQTT request to REST as fallback
            // e,g "iot/devices/request" => $"{_restEndpoint}/devices"
            return $"{_restEndpoint}/{topic.Replace("iot/", "").Replace("/", "-")}";
        }
    }
}