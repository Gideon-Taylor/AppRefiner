namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to disconnect from the current database
    /// </summary>
    public class DatabaseDisconnectCommand : BaseCommand
    {
        public override string CommandName => "Database: Disconnect DB";

        public override string CommandDescription => "Disconnect from current database";

        public override bool RequiresActiveEditor => true;

        public override Func<bool>? DynamicEnabledCheck => () =>
        {
            // Only enabled when there's an active editor with a database connection
            // Note: We can't access context here, so this is a simplified check
            return true; // Real check happens in Execute
        };

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null && context.ActiveEditor.AppDesignerProcess.DataManager != null)
            {
                context.ActiveEditor.AppDesignerProcess.DataManager.Disconnect();
                context.ActiveEditor.AppDesignerProcess.DataManager = null;
                foreach (var editor in context.ActiveEditor.AppDesignerProcess.Editors.Values)
                {
                    editor.DataManager = null;
                }
            }
        }
    }
}
