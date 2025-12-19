namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to generate a type error report
    /// </summary>
    public class GenerateTypeErrorReportCommand : BaseCommand
    {
        public override string CommandName => "Generate Type Error Report";

        public override string CommandDescription => "Generate a type error report at cursor for GitHub submission (editable)";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Alt,
                Keys.E,
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Alt, Keys.E));
            }
        }

        public override void Execute(CommandContext context)
        {
            var mainForm = context.MainForm as MainForm;
            mainForm?.GenerateTypeErrorReportCommand();
        }
    }
}
