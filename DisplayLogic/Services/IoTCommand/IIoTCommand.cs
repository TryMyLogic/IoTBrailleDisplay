// REST was an optional feature considered for IoTCommand but was not implemented due to time constraints.

namespace DisplayLogic.Services.IoTCommand
{
    /// <summary>
    /// Contract for IoT operation commands, defining MQTT communication and response processing.
    /// </summary>
    /// <remarks>
    /// Serves as a contract for specific IoT commands used by <see cref="IoTService"/> to interact 
    /// with IoT devices via MQTT. Implementations must provide MQTT topics, payloads, and response processing 
    /// logic. Supports operations like toggling Shelly switches.
    /// </remarks>
    public interface IIoTCommand
    {
        /// <summary>
        /// Gets the MQTT topic for sending the command request.
        /// </summary>
        /// <value>
        /// A string representing the MQTT topic to which the command payload is published.
        /// Must not be null or empty.
        /// </value>
        /// <remarks>
        /// Implemented by concrete commands to specify the target topic for the command, 
        /// used by <see cref="IoTService"/> to publish via <see cref="IIoTDevice.PublishAsync"/>.
        /// </remarks>
        string RequestTopic { get; }

        /// <summary>
        /// Gets the MQTT topic for receiving the command response.
        /// </summary>
        /// <value>
        /// A string representing the MQTT topic from which the response is received.
        /// Must not be null or empty.
        /// </value>
        /// <remarks>
        /// Implemented by concrete commands to specify the topic for subscribing to responses, 
        /// used by <see cref="IoTService"/> via <see cref="IIoTDevice.SubscribeAsync"/>.
        /// </remarks>
        string ResponseTopic { get; }

        /// <summary>
        /// Gets the payload to send for the IoT operation.
        /// </summary>
        /// <value>
        /// A string representing the payload to be sent to the <see cref="RequestTopic"/>.
        /// Must not be null.
        /// </value>
        /// <remarks>
        /// Implemented by concrete commands to provide the command payload, such as "on" or "off" for a Shelly switch, 
        /// sent via <see cref="IIoTDevice.PublishAsync"/> by <see cref="IoTService"/>.
        /// </remarks>
        string RequestPayload { get; }

        /// <summary>
        /// Processes the response from the IoT operation, returning structured data.
        /// </summary>
        /// <param name="response">The raw response received from the <see cref="ResponseTopic"/>. Must not be null.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> containing an object representing the processed response, 
        /// typically a string for output to a Braille display or console.
        /// </returns>
        /// <exception cref="InvalidDataException">Thrown if the response cannot be processed into the expected format.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during response processing.</exception>
        /// <remarks>
        /// Implemented by concrete commands to interpret the response from <see cref="ResponseTopic"/> 
        /// and produce output for <see cref="IoTService"/>, suitable for logging or display on an <see cref="IBrailleDisplay"/>.
        /// </remarks>
        Task<object> ProcessResponseAsync(string response);
    }
}
