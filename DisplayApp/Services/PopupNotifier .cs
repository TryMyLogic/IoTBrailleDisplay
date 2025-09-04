using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DisplayLogic.SharedInterfaces;

namespace DisplayApp.Services
{
    public class PopupNotifier : IUserNotifier
    {
        public async Task NotifyAsync(string message, string title)
        {
            if (Application.Current.MainPage == null)
            {
                return;
            }

            string fullMessage = string.IsNullOrEmpty(title) ? message : $"{title}: {message}";

            var overlay = new ContentPage
            {
                BackgroundColor = Color.FromArgb("#80000000"), // semi-transparent
                Content = new Frame
                {
                    Padding = 20,
                    CornerRadius = 10,
                    BackgroundColor = Colors.Black,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Content = new Label
                    {
                        Text = fullMessage,
                        TextColor = Colors.White,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            };

            // Show overlay
            await Application.Current.MainPage.Navigation.PushModalAsync(overlay, false);

            // Auto-hide
            await Task.Delay(3000);
            _ = await Application.Current.MainPage.Navigation.PopModalAsync(false);
        }
    }
}
