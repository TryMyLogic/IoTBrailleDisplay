using DisplayApp.ViewModels;

namespace DisplayApp
{
    /// <summary>
    /// Represents the main page of the application.
    /// This page displays a list of areas in the smart home system and allows
    /// users to navigate to devices within each area.
    /// </summary>
    /// <remarks>
    /// The <see cref="MainPage"/> is bound to <see cref="MainPageViewModel"/>,
    /// which handles loading data from the Home Assistant WebSocket client,
    /// manages device and area collections, and implements navigation logic.
    /// </remarks>
    public partial class MainPage : ContentPage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainPage"/> class.
        /// Sets up the UI components and binds the page to its ViewModel.
        /// </summary>
        /// <param name="viewModel">
        /// The <see cref="MainPageViewModel"/> instance to bind to this page.
        /// Handles data loading, commands, and interactions for this page.
        /// </param>
        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

    }
}
