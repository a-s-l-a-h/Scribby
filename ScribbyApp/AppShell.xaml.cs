using ScribbyApp.Views;

namespace ScribbyApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register the routes for ALL our pages
            Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
            Routing.RegisterRoute(nameof(ConnectPage), typeof(ConnectPage));
            Routing.RegisterRoute(nameof(ControlPage), typeof(ControlPage)); // <-- ADD THIS
            Routing.RegisterRoute(nameof(ScriptPage), typeof(ScriptPage));   // <-- ADD THIS
        }
    }
}