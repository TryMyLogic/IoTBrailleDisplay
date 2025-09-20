using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using DisplayLogic.SharedInterfaces;



namespace DisplayApp.Services
{
    /// <summary>
    /// Provides a cross-platform implementation of the <see cref="IUserNotifier"/> interface
    /// using toast notifications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service leverages the <c>CommunityToolkit.Maui</c> library to display short,
    /// non-intrusive toast messages to the user. Toasts are well-suited for communicating
    /// transient information such as confirmations, warnings, or status updates without
    /// interrupting the user workflow.
    /// </para>
    /// <para>
    /// Design considerations:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    /// By using <see cref="IUserNotifier"/>, the UI layer depends on an abstraction rather than
    /// a platform-specific implementation. This promotes testability and allows different
    /// notification strategies (e.g., dialogs, system notifications) to be swapped in later.
    /// </description>
    ///   </item>
    ///   <item>
    ///     <description>
    /// A <see cref="CancellationTokenSource"/> is used for potential future extensibility,
    /// allowing cancellation of the toast if required (e.g., app shutdown or user navigation).
    /// </description>
    ///   </item>
    ///   <item>
    ///     <description>
    /// The font size parameter (<c>14</c>) ensures legibility across most devices while maintaining
    /// unobtrusiveness. The toast duration is set to <see cref="ToastDuration.Short"/> to avoid 
    /// overloading the user with long-lived messages.
    /// </description>
    ///   </item>
    /// </list>
    /// </para>
    /// </remarks>
    public class PopupNotifier : IUserNotifier
    {
        /// <summary>
        /// Displays a toast notification to the user with the specified title and message.
        /// </summary>
        /// <param name="title">
        /// The notification title. If null or empty, only the <paramref name="message"/> will be displayed.
        /// </param>
        /// <param name="message">
        /// The notification message body. This should be concise to fit within a toast notification.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation of displaying the toast.
        /// </returns>
        /// <remarks>
        /// The title and message are combined into a single string when both are provided, formatted as
        /// "<c>Title: Message</c>". This ensures that the toast remains compact and avoids overwhelming 
        /// the user with separate popups.
        /// </remarks>
        public async Task NotifyAsync(string title, string message)
        {
            CancellationTokenSource cancellationTokenSource = new();
            string fullMessage = string.IsNullOrEmpty(title) ? message : $"{title}: {message}";

            IToast toast = Toast.Make(fullMessage, ToastDuration.Short, 14);
            await toast.Show(cancellationTokenSource.Token);
        }
    }
}
