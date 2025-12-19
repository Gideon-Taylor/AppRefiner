using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to navigate to the previous bookmark
    /// </summary>
    public class GoToPreviousBookmarkCommand : BaseCommand
    {
        public override string CommandName => "Editor: Go to Previous Bookmark";

        public override string CommandDescription => "Navigate to the previous bookmark and remove it from the stack";

        public override bool RequiresActiveEditor => true;

        public override Func<bool>? DynamicEnabledCheck => () => true; // Will check in Execute

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control,
                Keys.OemMinus, // -
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control, Keys.OemMinus));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null && context.ActiveEditor.BookmarkStack.Count > 0)
            {
                ScintillaManager.GoToPreviousBookmark(context.ActiveEditor);
            }
        }
    }
}
