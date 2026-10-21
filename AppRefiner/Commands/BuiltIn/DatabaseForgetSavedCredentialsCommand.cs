using AppRefiner.Dialogs;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Forgets the saved password (or remembered App Designer Proxy choice) for the active
    /// connection's database, so the connect dialog stops auto-connecting to it. Only that
    /// database's entry is touched.
    /// </summary>
    public class DatabaseForgetSavedCredentialsCommand : BaseCommand
    {
        public override string CommandName => "Database: Forget Saved Credentials";

        public override string CommandDescription => "Stop auto-connecting to the current database by forgetting its saved password";

        public override bool RequiresActiveEditor => false;

        public override bool IsEnabled(CommandContext context)
        {
            var process = context.ActiveAppDesigner;
            return process?.DataManager != null && !string.IsNullOrEmpty(process.ConnectionSettingsKey);
        }

        public override void Execute(CommandContext context)
        {
            var process = context.ActiveAppDesigner;
            var settingsKey = process?.ConnectionSettingsKey;
            if (process == null || string.IsNullOrEmpty(settingsKey))
                return;

            string message = DBConnectDialog.ForgetSavedCredentials(settingsKey)
                ? $"Saved credentials for {settingsKey} were forgotten. The next connection will not connect automatically."
                : $"There are no saved credentials for {settingsKey}.";

            Task.Delay(100).ContinueWith(_ =>
            {
                var mainHandle = process.MainWindowHandle;
                var handleWrapper = new WindowWrapper(mainHandle);
                new MessageBoxDialog(message, "Forget Saved Credentials", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
            });
        }
    }
}
