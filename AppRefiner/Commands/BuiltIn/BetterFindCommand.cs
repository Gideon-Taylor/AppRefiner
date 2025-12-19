using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to open the Better Find dialog for advanced search
    /// </summary>
    public class BetterFindCommand : BaseCommand
    {
        public override string CommandName => "Editor: Better Find";

        public override string CommandDescription => "Open the Better Find dialog for advanced search and replace";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            // Note: Ctrl+F is registered in MainForm, this is Ctrl+J as alternative
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control,
                Keys.J,
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control, Keys.J));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null)
            {
                ScintillaManager.ShowBetterFindDialog(context.ActiveEditor);
            }
        }
    }
}
