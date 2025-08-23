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
    public class IoTDeviceTests
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

        public IoTDeviceTests(ITestOutputHelper output)
        {
            (_testLogger, _loggerFactory) = SharedFunctions.CreateTestLogger<IoTDevice>(output);

            _mqttClient = Substitute.For<IMqttClient>();
            _mqttFactory = new MqttClientFactory(); // Is a concrete class, cannot substitute.
            _httpClient = new HttpClient();  // Is a concrete class, cannot substitute.
        }

        private void InitializeMemorySinkLogger()
        {
            (_memoryLogger, _memorySink, _memoryLoggerFactory) = SharedFunctions.CreateMemorySinkLogger<IoTDevice>();
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Arrange
            InitializeMemorySinkLogger();

            // Act
            IoTDevice device = new(TestBroker, TestRestEndpoint, TestPort, _httpClient, _mqttFactory, _mqttClient, _memoryLogger);

            // Assert
            Assert.True(device.IsConnected); // Initially true until REST is used. May potentially remove as REST fallback is not entirely feasible anymore
           // SharedFunctions.AssertSingleLogEvent(_memorySink, LogEventLevel.Debug, $"Device instance created. Broker: {TestBroker}. REST endpoint: {TestRestEndpoint}. Port: {TestPort}");
            Assert.NotNull(device);
        }

        [Fact]
        public void Constructor_WithNullBroker_ThrowsArgumentNullException()
        {
            // Arrange
            InitializeMemorySinkLogger();

            // Act & Assert
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            {
                return new IoTDevice(null!, TestRestEndpoint, TestPort, _httpClient, _mqttFactory, _mqttClient, _memoryLogger);
            });
            Assert.Equal("mqttBroker", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithNullRestEndpoint_ThrowsArgumentNullException()
        {
            // Arrange
            InitializeMemorySinkLogger();

            // Act & Assert
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            {
                return new IoTDevice(TestBroker, null!, TestPort, _httpClient, _mqttFactory, _mqttClient, _memoryLogger);
            });
            Assert.Equal("restEndpoint", exception.ParamName);
        }

        // This test fails because _mqttClient.IsConnected is false but IoTDevice itself IsConnect = true as default. Mismatch found
        [Fact]
        public async Task DisconnectAsync_WhenNotConnected_DoesNotCallDisconnect()
        {
            // Arrange
            InitializeMemorySinkLogger();
            _ = _mqttClient.IsConnected.Returns(false);
            IoTDevice device = new(TestBroker, TestRestEndpoint, TestPort, _httpClient, _mqttFactory, _mqttClient, _memoryLogger);

            // Act
            await device.DisconnectAsync();

            // Assert
            await _mqttClient.DidNotReceive().DisconnectAsync(Arg.Any<MqttClientDisconnectOptions>());
            Assert.False(device.IsConnected);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Disconnected from MQTT broker.", expectedMatchCount: 0);
        }

        // Due to difficulties with mocking, as classes such as MqttClient are concrete and cannot be handled by NSubstitute,
        // majority of the tests are integration

        [Fact]
        public async Task PublishAsync_WhenNotConnected_ThrowsInvalidOperationException()
        {
            // Arrange
            InitializeMemorySinkLogger();
            _ = _mqttClient.IsConnected.Returns(false);
            IoTDevice device = new(TestBroker, TestRestEndpoint, TestPort, _httpClient, _mqttFactory, _mqttClient, _memoryLogger);

            // Act & Assert
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return device.PublishAsync("iot/test", "payload");
            });
            Assert.Equal("MQTT client is not connected.", exception.Message);
            SharedFunctions.AssertSingleLogEvent(_memorySink, LogEventLevel.Warning, "MQTT client is not connected.");
        }

        [SkippableFact]
        public async Task Publish_Should_Throw_If_Not_Connected()
        {
            IoTDevice device = new(TestBroker, TestRestEndpoint);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return device.PublishAsync("iot/test", "payload");
            });
        }
    }
}
