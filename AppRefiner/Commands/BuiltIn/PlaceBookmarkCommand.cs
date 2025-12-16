using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to place a bookmark at the current cursor position
    /// </summary>
    public class PlaceBookmarkCommand : BaseCommand
    {
        public override string CommandName => "Editor: Place Bookmark";

        public override string CommandDescription => "Place a bookmark at the current cursor position";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control,
                Keys.B,
                () => Execute(new CommandContext())))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control, Keys.B));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null)
            {
                ScintillaManager.PlaceBookmark(context.ActiveEditor);
            }
        }
    }
}
