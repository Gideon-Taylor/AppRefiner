using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to open the Better Find dialog in replace mode
    /// </summary>
    public class BetterFindReplaceCommand : BaseCommand
    {
        public override string CommandName => "Editor: Better Find Replace";

        public override string CommandDescription => "Open the Better Find dialog in replace mode";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            // Note: Ctrl+H is registered in MainForm, this is Ctrl+K as alternative
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control,
                Keys.K,
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control, Keys.K));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null)
            {
                ScintillaManager.ShowBetterFindDialog(context.ActiveEditor, enableReplaceMode: true);
            }
        }
    }
}
