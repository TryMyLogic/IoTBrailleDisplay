using DisplayApp.Tests.Mocks;
using DisplayLogic.Services;

namespace DisplayApp.Tests
{
    public class BrailleDisplayTests
    {
        [Fact]
        public async Task ConnectAsync_ShouldSetIsConnected_WhenMockSucceeds()
        {
            var mock = new MockBluetoothConnection(connectSucceed: true);
            var braille = new BrailleDisplay(mock);

            await braille.ConnectAsync();

            Assert.True(braille.IsConnected);
        }

        [Fact]
        public async Task ConnectAsync_ShouldNotSetIsConnected_WhenMockFails()
        {
            var mock = new MockBluetoothConnection(connectSucceed: false);
            var braille = new BrailleDisplay(mock);

            await braille.ConnectAsync();

            Assert.False(braille.IsConnected);
        }

        [Fact]
        public async Task DisconnectAsync_ShouldSetIsConnectedFalse()
        {
            var mock = new MockBluetoothConnection(connectSucceed: true);
            var braille = new BrailleDisplay(mock);

            await braille.ConnectAsync();
            await braille.DisconnectAsync();

            Assert.False(braille.IsConnected);
        }
    }
}