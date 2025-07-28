using DisplayApp.Views;

namespace DisplayApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(DevicesPage), typeof(DevicesPage));
        }
    }
}
