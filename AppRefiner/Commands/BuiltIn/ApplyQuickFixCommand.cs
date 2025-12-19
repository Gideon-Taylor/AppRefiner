namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to apply the suggested quick fix
    /// </summary>
    public class ApplyQuickFixCommand : BaseCommand
    {
        public override string CommandName => "Editor: Apply Quick Fix";

        public override string CommandDescription => "Applies the suggested quick fix for the annotation under the cursor";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control,
                Keys.OemPeriod, // .
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control, Keys.OemPeriod));
            }
        }

        public override void Execute(CommandContext context)
        {
            var mainForm = context.MainForm as MainForm;
            mainForm?.ApplyQuickFixCommand();
        }
    }
}
