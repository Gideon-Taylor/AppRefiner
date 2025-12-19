namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to smart search and open PeopleSoft objects
    /// </summary>
    public class SmartOpenCommand : BaseCommand
    {
        public override string CommandName => "Open: Smart Open";

        public override string CommandDescription => "Smart search and open PeopleSoft objects across all types";

        public override bool RequiresActiveEditor => false;

        public override Func<bool>? DynamicEnabledCheck => () => true; // Will check database connection in Execute

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control,
                Keys.O,
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control, Keys.O));
            }
        }

        public override void Execute(CommandContext context)
        {
            var mainForm = context.MainForm as MainForm;
            mainForm?.ShowSmartOpenDialog();
        }
    }
}
