using DisplayApp.Views;

namespace DisplayApp
{
    public partial class AppShell : Shell
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppShell"/> class.
        /// Sets up routing for navigable pages within the application.
        /// </summary>
        public AppShell()
        {
            InitializeComponent();

            // Register navigable routes
            Routing.RegisterRoute(nameof(DevicesPage), typeof(DevicesPage));
            Routing.RegisterRoute(nameof(DeviceControlPage), typeof(DeviceControlPage));
        }
    }
}
