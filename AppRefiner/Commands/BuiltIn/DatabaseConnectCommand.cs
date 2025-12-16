using AppRefiner.Database;
using AppRefiner.Dialogs;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to connect to database for advanced functionality
    /// </summary>
    public class DatabaseConnectCommand : BaseCommand
    {
        public override string CommandName => "Database: Connect to DB";

        public override string CommandDescription => "Connect to database for advanced functionality";

        public override bool RequiresActiveEditor => false;

        public override Func<bool>? DynamicEnabledCheck => () => true; // Will check in Execute

        public override void Execute(CommandContext context)
        {
            if (context.ActiveAppDesigner != null)
            {
                var mainHandle = context.ActiveAppDesigner.MainWindowHandle;
                var handleWrapper = new WindowWrapper(mainHandle);
                DBConnectDialog dialog = new(mainHandle, context.ActiveAppDesigner.DBName);
                dialog.StartPosition = FormStartPosition.CenterParent;

                if (dialog.ShowDialog(handleWrapper) == DialogResult.OK)
                {
                    IDataManager? manager = dialog.DataManager;
                    if (manager != null)
                    {
                        context.ActiveAppDesigner.DataManager = manager;
                        foreach (var editor in context.ActiveAppDesigner.Editors.Values)
                        {
                            editor.DataManager = manager;
                        }

                        // Force refresh all editors to allow DB-dependent stylers to run
                        var mainForm = context.MainForm as MainForm;
                        mainForm?.RefreshAllEditorsAfterDatabaseConnection();
                    }
                }
            }
        }
    }
}
