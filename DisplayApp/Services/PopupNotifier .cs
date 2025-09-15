using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using DisplayLogic.SharedInterfaces;



namespace DisplayApp.Services
{
    public class PopupNotifier : IUserNotifier
    {
        public async Task NotifyAsync(string title, string message)
        {
            string fullMessage = string.IsNullOrEmpty(title) ? message : $"{title}: {message}";

            // Ensure Toast runs on the main thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                CancellationTokenSource cancellationTokenSource = new();
                IToast toast = Toast.Make(fullMessage, ToastDuration.Short, 14);
                await toast.Show(cancellationTokenSource.Token);
            });
        }
    }
}
