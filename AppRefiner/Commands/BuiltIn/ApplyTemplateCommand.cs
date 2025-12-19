namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to apply a template to the current editor
    /// </summary>
    public class ApplyTemplateCommand : BaseCommand
    {
        public override string CommandName => "Template: Apply Template";

        public override string CommandDescription => "Apply a template to the current editor";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Alt,
                Keys.T,
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Alt, Keys.T));
            }
        }

        public override void Execute(CommandContext context)
        {
            var mainForm = context.MainForm as MainForm;
            mainForm?.ApplyTemplateCommand();
        }
    }
}
