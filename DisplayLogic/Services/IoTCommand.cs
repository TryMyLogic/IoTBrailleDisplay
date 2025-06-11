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

    // Command to retrieve a list of IoT devices
    public class GetDevicesCommand : IoTCommand
    {
        // Gets the request topic: iot/devices/request
        public override string RequestTopic => "iot/devices/request";

        //Gets the response topic: iot/devices/response
        public override string ResponseTopic => "iot/devices/response";

        // Gets an empty payload for the get devices request
        public override string RequestPayload => "{}";

        // Parses the response into a list of device IDs and its relevant information
        public override Task<object> ProcessResponseAsync(string response)
        {
            throw new NotImplementedException();
        }
    }
}
