namespace AppRefiner
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();

            if (!Properties.Settings.Default.SettingsUpgraded)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.SettingsUpgraded = true;
                Properties.Settings.Default.Save();
            }

            // Check if this is first run of new version AND user wants to see What's New
            bool shouldShowWhatsNew = false;
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var currentVersion = assembly.GetName().Version?.ToString();

            // Only proceed if we have a valid version and user hasn't disabled the dialog
             if (currentVersion != null && Properties.Settings.Default.ShowWhatsNewDialog)
            {
                var lastSeenVersion = Properties.Settings.Default.LastSeenVersion;

                if (string.IsNullOrEmpty(lastSeenVersion) || lastSeenVersion != currentVersion)
                {
                    shouldShowWhatsNew = true;
                    Properties.Settings.Default.LastSeenVersion = currentVersion;
                    Properties.Settings.Default.Save();
                }
            }

            Application.Run(new MainForm(shouldShowWhatsNew));
        }
    }
}