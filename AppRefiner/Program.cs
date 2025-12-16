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

            Application.Run(new MainForm());
        }
    }
}