namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to navigate to the next location in navigation history
    /// </summary>
    public class NavigateForwardCommand : BaseCommand
    {
        public override string CommandName => "Navigate Forward";

        public override string CommandDescription => "Navigate to the next location in navigation history";

        public override bool RequiresActiveEditor => false;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Alt,
                Keys.Right,
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Alt, Keys.Right));
            }
        }

        public override void Execute(CommandContext context)
        {
            var mainForm = context.MainForm as MainForm;
            mainForm?.NavigateForwardCommand();
        }
    }
}
