using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to collapse all foldable sections
    /// </summary>
    public class EditorCollapseAllCommand : BaseCommand
    {
        public override string CommandName => "Editor: Collapse All";

        public override string CommandDescription => "Collapse all foldable sections";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt,
                Keys.OemOpenBrackets, // [
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, Keys.OemOpenBrackets));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null)
            {
                ScintillaManager.CollapseTopLevel(context.ActiveEditor);
            }
        }
    }
}
