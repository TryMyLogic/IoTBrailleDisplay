namespace DisplayLogic.Services
{
    // Base class for IoT operation commands, defining communication and response processing
    public abstract class IoTCommand
    {
        // Gets the MQTT topic or REST endpoint for the request 
        public abstract string RequestTopic { get; }

        // Gets the MQTT topic or REST endpoint for the response
        public abstract string ResponseTopic { get; }

        // Gets the payload to send for the operation
        public abstract string RequestPayload { get; }

        // summary>Processes the operation response, returning structured data
        public abstract Task<object> ProcessResponseAsync(string response);
    }

    public class ShellySwitchRelayCommand(string uniqueId, bool turnOn) : IoTCommand
    {
        public override string RequestTopic => $"shellies/{uniqueId}/relay/0/command";
        public override string ResponseTopic => $"shellies/{uniqueId}/relay/0";
        public override string RequestPayload => turnOn ? "on" : "off";

        public override Task<object> ProcessResponseAsync(string response)
        {
            return Task.FromResult<object>($"The device is: {response}"); // Will likely change this when ui is setup. 
        }
    }


}
