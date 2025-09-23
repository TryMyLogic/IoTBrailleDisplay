namespace DisplayLogic.SharedInterfaces
{
    /// <summary>
    /// Defines a platform-agnostic service for notifying users of messages or events.
    /// Implementations may vary depending on platform (e.g., pop-up alerts on mobile, 
    /// toast notifications on Android, dialogs on Windows, etc.).
    /// </summary>
    public interface IUserNotifier
    {
        /// <summary>
        /// Displays a notification to the user with a given title and message.
        /// </summary>
        /// <param name="title">
        /// The title of the notification. Can be null or empty if only the message is required.
        /// </param>
        /// <param name="message">
        /// The main content of the notification to display to the user.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// The task completes once the notification has been displayed (or queued for display).
        /// </returns>
        /// <remarks>
        /// This method allows decoupling of business logic from UI-specific implementations.
        /// For example, in a MAUI app, an implementation might use <c>CommunityToolkit.Maui.Alerts.Toast</c> 
        /// on mobile or a simple <c>MessageBox</c> on Windows.
        /// </remarks>
        Task NotifyAsync(string title, string message);
    }
}
