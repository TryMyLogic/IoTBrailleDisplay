using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.Net.Sockets;

namespace DisplayLogic.Tests.Services.IoTDeviceTests
{
    public class MqttTestFixture
    {
        public bool IsMqttAvailable { get; }

        public MqttTestFixture()
        {
            try
            {
                using TcpClient client = new();
                client.Connect("localhost", 1883);
                IsMqttAvailable = true;
            }
            catch
            {
                IsMqttAvailable = false;
            }
        }

    }
}
