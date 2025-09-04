using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using DisplayLogic.SharedInterfaces;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using static System.Net.Mime.MediaTypeNames;



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
