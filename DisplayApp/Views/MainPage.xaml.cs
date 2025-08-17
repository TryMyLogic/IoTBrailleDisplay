using DisplayApp.ViewModels;
using DisplayApp.Views;

namespace DisplayApp
{
    public partial class MainPage : ContentPage
    {

        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

    }
}
