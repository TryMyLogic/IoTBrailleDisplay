using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using DisplayLogic.SharedInterfaces;



namespace DisplayApp.Services
{
    public class PopupNotifier : IUserNotifier
    {
        public async Task NotifyAsync(string title, string message)
        {
            CancellationTokenSource cancellationTokenSource = new();
            string fullMessage = string.IsNullOrEmpty(title) ? message : $"{title}: {message}";

            IToast toast = Toast.Make(fullMessage, ToastDuration.Short, 14);
            await toast.Show(cancellationTokenSource.Token);
        }
    }
}
