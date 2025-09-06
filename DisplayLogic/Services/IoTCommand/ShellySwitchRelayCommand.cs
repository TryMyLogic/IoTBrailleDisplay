namespace DisplayLogic.Services.IoTCommand
{
    /// <summary>
    /// Represents a command to toggle a Shelly switch relay on or off via MQTT.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="IoTService"/> to send MQTT commands to a Shelly switch device, 
    /// specifying the relay state ("on" or "off") and processing the response for output. 
    /// Inherits from <see cref="IoTCommand"/> to provide Shelly-specific MQTT topics and payload.
    /// </remarks>
    public class ShellySwitchRelayCommand(string uniqueId, bool turnOn) : IIoTCommand
    {
        /// <summary>
        /// Gets the MQTT topic for sending the Shelly switch relay command.
        /// </summary>
        /// <value>
        /// A string in the format <c>shellies/{uniqueId}/relay/0/command</c>, 
        /// where <c>uniqueId</c> identifies the Shelly switch device.
        /// </value>
        /// <remarks>
        /// Used by <see cref="IoTService"/> to publish the command payload via <see cref="IIoTDevice.PublishAsync"/>.
        /// </remarks>
        public string RequestTopic => $"shellies/{uniqueId}/relay/0/command";

        /// <summary>
        /// Gets the MQTT topic for receiving the Shelly switch relay response.
        /// </summary>
        /// <value>
        /// A string in the format <c>shellies/{uniqueId}/relay/0</c>, 
        /// where <c>uniqueId</c> identifies the Shelly switch device.
        /// </value>
        /// <remarks>
        /// Used by <see cref="IoTService"/> to subscribe to the response via <see cref="IIoTDevice.SubscribeAsync"/>.
        /// </remarks>
        public string ResponseTopic => $"shellies/{uniqueId}/relay/0";

        /// <summary>
        /// Gets the payload for the Shelly switch relay command.
        /// </summary>
        /// <value>
        /// Returns <c>"on"</c> if <c>turnOn</c> is <c>true</c>, otherwise <c>"off"</c>.
        /// </value>
        /// <remarks>
        /// The payload is sent to the <see cref="RequestTopic"/> by <see cref="IoTService"/> 
        /// to toggle the Shelly switch relay state.
        /// </remarks>
        public string RequestPayload => turnOn ? "on" : "off";

        /// <summary>
        /// Processes the response from the Shelly switch relay command.
        /// </summary>
        /// <param name="response">The raw response received from the <see cref="ResponseTopic"/>. Must not be null.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> containing a string describing the device state, 
        /// formatted as <c>"The device is: {response}"</c>, suitable for output to a Braille display or console.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="response"/> is null.</exception>
        /// <remarks>
        /// Returns a formatted string for use by <see cref="IoTService"/>, 
        /// intended for logging or display on an <see cref="IBrailleDisplay"/>. 
        /// The implementation may be updated when integrating with a UI.
        /// </remarks>
        public Task<object> ProcessResponseAsync(string response)
        {
            ArgumentNullException.ThrowIfNull(response);
            return Task.FromResult<object>($"The device is: {response}");
        }
    }
}
