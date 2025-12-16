using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to collapse the current fold level
    /// </summary>
    public class EditorCollapseLevelCommand : BaseCommand
    {
        public override string CommandName => "Editor: Collapse Level";

        public override string CommandDescription => "Collapse the current fold level";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Shift,
                Keys.OemOpenBrackets, // [
                () => Execute(new CommandContext())))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Shift, Keys.OemOpenBrackets));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null)
            {
                ScintillaManager.SetCurrentLineFoldStatus(context.ActiveEditor, true);
            }
        }
    }
}
