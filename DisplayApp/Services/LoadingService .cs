namespace DisplayApp.Services
{
    /// <summary>
    /// Defines a service responsible for displaying a loading overlay on the UI.
    /// </summary>
    /// <remarks>
    /// The <see cref="ILoadingService"/> abstraction allows view models or other services
    /// to indicate loading operations without directly interacting with the view layer.
    /// This improves testability and separation of concerns, adhering to MVVM principles.
    /// </remarks>
    public interface ILoadingService
    {
        /// <summary>
        /// Displays a loading overlay with an optional message.
        /// </summary>
        /// <param name="message">
        /// The message to display under the loading indicator. Defaults to "Loading...".
        /// </param>
        /// <returns>A task that completes when the overlay has been displayed.</returns>
        Task ShowAsync(string message = "Loading...");

        /// <summary>
        /// Hides the loading overlay if it is currently displayed.
        /// </summary>
        /// <returns>A task that completes when the overlay has been removed from the UI.</returns>
        Task HideAsync();
    }

    /// <summary>
    /// A concrete implementation of <see cref="ILoadingService"/> that displays
    /// a semi-transparent modal overlay with an activity indicator and message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Design considerations:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// A modal <see cref="ContentPage"/> is used for the overlay to block interaction
    /// with underlying UI elements while loading is in progress.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The service prevents multiple overlays from stacking using the <c>_isShowing</c> flag.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The <c>PushModalAsync</c> method is called without animation (<c>false</c>) to prevent
    /// unnecessary delays or flickering.
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// The service relies on <see cref="Application.Current.MainPage"/> to access navigation.
    /// </para>
    /// </remarks>
    public class LoadingService : ILoadingService
    {
        private bool _isShowing;
        private ContentPage? _overlayPage;

        /// <summary>
        /// Displays a loading overlay with the specified message.
        /// </summary>
        /// <param name="message">
        /// The message to show beneath the activity indicator. Defaults to "Loading...".
        /// </param>
        /// <returns>A task that completes when the overlay is displayed.</returns>
        /// <remarks>
        /// If a loading overlay is already being shown, this method does nothing.
        /// This prevents multiple overlays from stacking and ensures consistent UX.
        /// </remarks>
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

        /// <summary>
        /// Hides the currently displayed loading overlay.
        /// </summary>
        /// <returns>A task that completes when the overlay is removed.</returns>
        /// <remarks>
        /// If no overlay is currently displayed, this method does nothing.
        /// This ensures that hiding an overlay does not throw exceptions if
        /// the overlay is already hidden or was never shown.
        /// </remarks>
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
