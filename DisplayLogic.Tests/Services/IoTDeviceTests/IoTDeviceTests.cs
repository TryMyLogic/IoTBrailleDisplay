using DisplayLogic.Services;

namespace DisplayLogic.Tests.Services.IoTDeviceTests
{
    public class IoTDeviceTests
    {
        private const string TestBroker = "localhost";
        private const string TestRestEndpoint = "http://localhost/api";

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
