namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to navigate to the previous location in navigation history
    /// </summary>
    public class NavigateBackwardCommand : BaseCommand
    {
        public override string CommandName => "Navigate Backward";

        public override string CommandDescription => "Navigate to the previous location in navigation history";

        public override bool RequiresActiveEditor => false;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Alt,
                Keys.Left,
                () => Execute(new CommandContext())))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Alt, Keys.Left));
            }
        }

        public override void Execute(CommandContext context)
        {
            var mainForm = context.MainForm as MainForm;
            mainForm?.NavigateBackwardCommand();
        }
    }
}
