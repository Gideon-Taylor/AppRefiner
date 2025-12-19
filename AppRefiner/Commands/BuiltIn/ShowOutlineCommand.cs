namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to navigate to methods, properties, functions, getters, and setters within the current file
    /// </summary>
    public class ShowOutlineCommand : BaseCommand
    {
        public override string CommandName => "Navigation: Outline";

        public override string CommandDescription => "Navigate to methods, properties, functions, getters, and setters within the current file";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Shift,
                Keys.O,
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Shift, Keys.O));
            }
        }

        public override void Execute(CommandContext context)
        {
            var mainForm = context.MainForm as MainForm;
            mainForm?.ShowOutlineCommand();
        }
    }
}
