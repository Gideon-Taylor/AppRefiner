namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to navigate to the definition of the symbol under the cursor
    /// </summary>
    public class GoToDefinitionCommand : BaseCommand
    {
        public override string CommandName => "Go To Definition";

        public override string CommandDescription => "Navigate to the definition of the symbol under the cursor";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.None,
                Keys.F12,
                () => Execute(new CommandContext())))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.None, Keys.F12));
            }
        }

        public override void Execute(CommandContext context)
        {
            var mainForm = context.MainForm as MainForm;
            mainForm?.GoToDefinitionCommand();
        }
    }
}
