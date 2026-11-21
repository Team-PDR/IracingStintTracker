using System.Windows;

namespace StintTracker.Gui
{
    public partial class App : Application
    {
        private static Mutex? mutex = null;

        [STAThread]
        public static void Main()
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "StintTracker";
            bool createdNew;

            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                Application.Current.Shutdown();
            }

            base.OnStartup(e);
        }
    }
}
