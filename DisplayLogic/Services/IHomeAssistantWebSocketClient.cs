using DisplayLogic.Models;

namespace DisplayLogic.Services
{
    /// <summary>
    /// Defines a WebSocket client for interacting with a Home Assistant server, 
    /// managing authentication, device and area registry retrieval, and device area updates.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface communicate with Home Assistant's WebSocket API to fetch and update 
    /// device and area registries, specifically for MQTT-based devices. 
    /// The interface provides access to device and area data, connection status, and methods for connecting 
    /// and updating device area assignments. 
    /// </remarks>
    /// <seealso href="https://developers.home-assistant.io/docs/api/websocket">
    /// Home Assistant WebSocket API
    /// </seealso>
    public interface IHomeAssistantWebSocketClient
    {
        /// <summary>
        /// Gets the list of MQTT devices retrieved from the Home Assistant device registry.
        /// </summary>
        /// <value>
        /// A list of <see cref="MqttDevice"/> objects representing MQTT-based devices with metadata 
        /// such as identifiers and manufacturer. Empty until <see cref="ConnectAsync"/> completes successfully.
        /// </value>
        /// <remarks>
        /// Contains only MQTT-based devices, filtered by their "mqtt" identifier prefix from the Home Assistant 
        /// device registry. Populated during <see cref="ConnectAsync"/> and may be updated after 
        /// <see cref="UpdateDeviceAreaAsync"/> operations.
        /// </remarks>
        List<MqttDevice> Devices { get; }

        /// <summary>
        /// Gets the list of areas retrieved from the Home Assistant area registry.
        /// </summary>
        /// <value>
        /// A list of <see cref="Area"/> objects representing areas with metadata like ID, name, 
        /// and optional floor or icon. Populated during <see cref="ConnectAsync"/>.
        /// </value>
        /// <remarks>
        /// Represents the Home Assistant area registry, which organizes devices into logical or physical spaces 
        /// (e.g., "Kitchen"). Populated during initialization and typically remains unchanged.
        /// </remarks>
        List<Area> Areas { get; }

        /// <summary>
        /// Indicates whether the WebSocket connection to the Home Assistant server is active and authenticated.
        /// </summary>
        /// <value>
        /// <c>true</c> if the connection is active and authenticated; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// Set to <c>true</c> after successful authentication in <see cref="ConnectAsync"/> and reset to 
        /// <c>false</c> in <see cref="StopAsync"/>. Reflects the operational state of the WebSocket connection.
        /// </remarks>
        bool IsConnected { get; }

        /// <summary>
        /// Connects to the Home Assistant WebSocket server, authenticates, and retrieves device and area registries.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="TimeoutException">Thrown if authentication and registry retrieval exceed 30 seconds.</exception>
        /// <exception cref="AuthenticationException">Thrown if the access token is rejected by the server.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during connection or initialization.</exception>
        /// <remarks>
        /// Establishes a WebSocket connection, authenticates using a provided access token, and populates 
        /// <see cref="Devices"/> and <see cref="Areas"/> with MQTT device and area data. 
        /// Sets <see cref="IsConnected"/> to <c>true</c> on success. 
        /// Only MQTT devices are included in the device registry.
        /// </remarks>
        Task ConnectAsync();

        /// <summary>
        /// Updates the area assignment for a device in the Home Assistant device registry.
        /// </summary>
        /// <param name="uniqueId">The unique ID of the device. Must exist in <see cref="Devices"/>.</param>
        /// <param name="areaId">The ID of the area to assign. Must exist in <see cref="Areas"/> and not be null or empty.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="areaId"/> is null, empty, or not present in <see cref="Areas"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if no device with <paramref name="uniqueId"/> exists in <see cref="Devices"/>.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during the update operation.</exception>
        /// <remarks>
        /// Sends a command to update the device's area assignment in the Home Assistant server and updates 
        /// <see cref="Devices"/> upon confirmation. Notifies users of the operation's progress or errors.
        /// </remarks>
        Task UpdateDeviceAreaAsync(string uniqueId, string areaId);
    }
}