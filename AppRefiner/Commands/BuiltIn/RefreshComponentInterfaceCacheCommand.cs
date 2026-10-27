using AppRefiner.Dialogs;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Re-reads every cached component interface definition from the database and drops the
    /// type metadata built from it, then re-runs stylers on the active editor so CI members
    /// are checked against the fresh definitions.
    /// </summary>
    public class RefreshComponentInterfaceCacheCommand : BaseCommand
    {
        public override string CommandName => "Refresh Component Interface Cache";
        public override string CommandDescription => "Re-read cached component interface definitions from the database";
        public override bool RequiresActiveEditor => true;

        public override void Execute(CommandContext context)
        {
            var editor = context.ActiveEditor;
            if (editor == null) return;

            var dataManager = editor.DataManager;
            var mainHandle = editor.AppDesignerProcess.MainWindowHandle;

            if (dataManager == null || !dataManager.IsConnected)
            {
                ShowMessage("Connect to a database to refresh component interfaces.", mainHandle);
                return;
            }

            var resolver = editor.AppDesignerProcess.TypeResolver;
            var stylerManager = context.StylerManager;
            var mainForm = context.MainForm;

            Task.Run(() =>
            {
                try
                {
                    int refreshed = 0;
                    foreach (string name in dataManager.CachedComponentInterfaces)
                    {
                        try
                        {
                            dataManager.InvalidateComponentInterface(name);
                            // Null means the CI no longer exists: nothing was refreshed
                            if (dataManager.GetComponentInterface(name) != null)
                                refreshed++;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex, $"{CommandName}: failed to re-fetch '{name}'");
                        }
                    }

                    // Drop every resolver entry, including Missing and failed lookups the data
                    // manager never held, so the next lookups go back to the database instead
                    // of serving stale answers.
                    resolver?.InvalidateComponentInterface(null);

                    Debug.Log($"{CommandName}: refreshed {refreshed} component interface(s)");

                    // Type inference runs on every styler pass, so this re-types CI references
                    // against the refreshed definitions.
                    mainForm?.Invoke(() => stylerManager?.ProcessStylersForEditor(editor));

                    ShowMessage($"Refreshed {refreshed} component interface(s).", mainHandle);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"{CommandName}: refresh failed");
                }
            });
        }

        private static void ShowMessage(string message, IntPtr mainHandle)
        {
            Task.Delay(100).ContinueWith(_ =>
            {
                var handleWrapper = new WindowWrapper(mainHandle);
                new MessageBoxDialog(message, "Component Interface Cache", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
            });
        }
    }
}
