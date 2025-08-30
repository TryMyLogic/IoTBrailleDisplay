using DisplayLogic.Services;
using DisplayLogic.Tests.SharedTestItems;
using Microsoft.Extensions.Logging;
using MQTTnet;
using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.InMemory;
using Xunit.Abstractions;

namespace DisplayLogic.Tests.Services.IoTDeviceTests
{
    public class IoTDeviceIntegrationTests : IClassFixture<ServiceFixture>, IDisposable
    {
        private const string TestBroker = "localhost";
        private const string TestRestEndpoint = "http://localhost/api";
        private const int TestPort = 1883;

        private readonly ILogger<IoTDevice> _testLogger;
        private readonly ILoggerFactory _loggerFactory;

        private InMemorySink? _memorySink;
        private ILogger<IoTDevice>? _memoryLogger;
        private ILoggerFactory? _memoryLoggerFactory;

        private readonly IMqttClient _mqttClient;
        private readonly MqttClientFactory _mqttFactory;
        private readonly HttpClient _httpClient;
        private readonly bool _shouldSkipMQTTests;
        private IoTDevice? _device;

        public IoTDeviceIntegrationTests(ServiceFixture fixture, ITestOutputHelper output)
        {
            (_testLogger, _loggerFactory) = SharedFunctions.CreateTestLogger<IoTDevice>(output);
            _shouldSkipMQTTests = !fixture.CanConnectToMQTTBroker;

            _mqttClient = Substitute.For<IMqttClient>();
            _mqttFactory = new MqttClientFactory(); // Is a concrete class, cannot substitute.
            _httpClient = new HttpClient();  // Is a concrete class, cannot substitute.
        }

        private void InitializeMemorySinkLogger()
        {
            (_memoryLogger, _memorySink, _memoryLoggerFactory) = SharedFunctions.CreateMemorySinkLogger<IoTDevice>();
        }

        // Previously known as Should_Connect_To_Mqtt_Broker
        [SkippableFact]
        public async Task ConnectAsync_WithValidBroker_ConnectsSuccessfully()
        {
            Skip.If(_shouldSkipMQTTests, "MQTT broker is not available. Skipping this test");

            // Arrange
            InitializeMemorySinkLogger();
            _device = new(TestBroker, TestRestEndpoint, TestPort, _httpClient, null, null, _memoryLogger);

            // Act
            try
            {
                Task connectTask = _device.ConnectAsync();
                Task completed = await Task.WhenAny(connectTask, Task.Delay(30000));
                if (completed != connectTask)
                {
                    Assert.Fail("Test timed out waiting for ConnectAsync to complete after 30 seconds.");
                }
                await connectTask;
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error connecting to MQTT broker: {ex.Message}");
            }

            // Assert
            Assert.True(_device.IsConnected);
            SharedFunctions.AssertSingleLogEvent(_memorySink, LogEventLevel.Information, "Connected to MQTT broker.");

            // Cleanup
            await CleanupAsync();
        }

        // Previously known as Should_Disconnect_From_Mqtt
        [SkippableFact]
        public async Task DisconnectAsync_AfterConnection_DisconnectsSuccessfully()
        {
            Skip.If(_shouldSkipMQTTests, "MQTT broker is not available. Skipping this test");

            // Arrange
            InitializeMemorySinkLogger();
            _device = new(TestBroker, TestRestEndpoint, TestPort, _httpClient, null, null, _memoryLogger);
            await _device.ConnectAsync();
            Assert.True(_device.IsConnected, "Should be connected before disconnecting.");

            // Act
            try
            {
                await _device.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error disconnecting from MQTT broker: {ex.Message}");
            }

            // Assert
            Assert.False(_device.IsConnected);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Disconnecting from MQTT broker.", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Disconnected from MQTT broker.", expectedMatchCount: 1);

            // Cleanup
            await CleanupAsync();
        }

        [SkippableFact]
        public async Task PublishAsync_WhenConnected_PublishesAndLogsSuccessfully()
        {
            Skip.If(_shouldSkipMQTTests, "MQTT broker is not available. Skipping this test");

            // Arrange
            InitializeMemorySinkLogger();
            _device = new(TestBroker, TestRestEndpoint, TestPort, _httpClient, _mqttFactory, null, _memoryLogger);
            await _device.ConnectAsync();
            Assert.True(_device.IsConnected, "Should be connected before publishing.");
            string topic = "iot/test/publish";
            string payload = "test payload";

            // Act
            try
            {
                await _device.PublishAsync(topic, payload);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error publishing to MQTT broker: {ex.Message}");
            }

            // Assert
            //SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, $"Sending payload: {topic}, {payload}", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Payload sent to MQTT.", expectedMatchCount: 1);

            // Cleanup
            await CleanupAsync();
        }

        [SkippableFact]
        public async Task SubscribeAsync_ReceivesPublishedMessage()
        {
            Skip.If(_shouldSkipMQTTests, "MQTT broker is not available. Skipping this test");

            // Arrange
            InitializeMemorySinkLogger();
            _device = new(TestBroker, TestRestEndpoint, TestPort, _httpClient, _mqttFactory, null, _memoryLogger);
            await _device.ConnectAsync();
            Assert.True(_device.IsConnected, "Should be connected before subscribing.");
            string topic = "iot/test/subscribe";
            string expectedPayload = "expected test payload";

            // Act
            try
            {
                Task<string> subscribeTask = _device.SubscribeAsync(topic);
                await Task.Delay(1000); // Allow subscription with home assistant to establish successfully
                await _device.PublishAsync(topic, expectedPayload);
                Task completed = await Task.WhenAny(subscribeTask, Task.Delay(10000));
                if (completed != subscribeTask)
                {
                    Assert.Fail("Test timed out waiting for SubscribeAsync to complete after 10 seconds.");
                }
                string received = await subscribeTask;

                // Assert
                Assert.Equal(expectedPayload, received);
                // SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, $"Attempting Subscription to MQTT topic: {topic}", expectedMatchCount: 1);
                // SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Debug, $"Received message on topic {topic}: {expectedPayload}", expectedMatchCount: 1);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error during subscribe/publish test: {ex.Message}");
            }

            // Cleanup
            await CleanupAsync();
        }



        [SkippableFact]
        public async Task Should_Publish_And_Receive_Message()
        {
            Skip.If(_shouldSkipMQTTests, "MQTT broker not available");

            string topic = "iot/test/message";
            string expectedPayload = "Hello World";

            IoTDevice device = new(TestBroker, TestRestEndpoint);
            await device.ConnectAsync();

            Task<string> subscribeTask = device.SubscribeAsync(topic);
            await Task.Delay(2000);
            await device.PublishAsync(topic, expectedPayload);

            string received = await subscribeTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(expectedPayload, received);
        }

        private async Task CleanupAsync()
        {
            try
            {
                if (_device != null && _device.IsConnected)
                {
                    await _device.DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error during cleanup: {ex.Message}");
            }
            _memoryLoggerFactory?.Dispose();
        }

        public void Dispose()
        {
            _loggerFactory?.Dispose();
            _memoryLoggerFactory?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
