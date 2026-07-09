namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to run linting rules against the current editor
    /// </summary>
    public class EditorLintCurrentCodeCommand : BaseCommand
    {
        public override string CommandName => "Editor: Lint Current Code";

        public override string CommandDescription => "Run linting rules against the current editor";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            // Ctrl+Alt+L collides with App Designer / editor behavior that deletes lines.
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift,
                Keys.L,
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Keys.L));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor == null) return;
            context.LinterManager?.ProcessLintersForActiveEditor(context.ActiveEditor, context.ActiveEditor.DataManager);
        }
    }
}
