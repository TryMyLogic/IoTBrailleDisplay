using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace DisplayApp.Services
{
    public interface ILoadingService
    {
        Task ShowAsync(string message = "Loading...");
        Task HideAsync();
    }

    public class LoadingService : ILoadingService
    {
        private bool _isShowing;
        private ContentPage? _overlayPage;

        public async Task ShowAsync(string message = "Loading...")
        {
            if (_isShowing)
            {
                return;
            }

            _isShowing = true;

            _overlayPage = new ContentPage
            {
                BackgroundColor = Color.FromArgb("#80000000"), // semi-transparent black
                Content = new StackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new ActivityIndicator
                        {
                            IsRunning = true,
                            WidthRequest = 50,
                            HeightRequest = 50,
                            Color = Colors.White,

                        },
                        new Label
                        {
                            Text = message,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center,
                        }
                    }
                }
            };

            Page? mainPage = Application.Current.MainPage;

            if (mainPage is not null)
            {
                await mainPage.Navigation.PushModalAsync(_overlayPage, false);
            }
        }

        public async Task HideAsync()
        {
            if (!_isShowing)
            {
                return;
            }

            _isShowing = false;

            if (_overlayPage != null)
            {
                _ = await Application.Current.MainPage.Navigation.PopModalAsync(false);
                _overlayPage = null;
            }
        }
    }
}
