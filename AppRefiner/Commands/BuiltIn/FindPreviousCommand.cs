using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to find the previous occurrence of the search term
    /// </summary>
    public class FindPreviousCommand : BaseCommand
    {
        public override string CommandName => "Editor: Find Previous";

        public override string CommandDescription => "Find the previous occurrence of the search term";

        public override bool RequiresActiveEditor => true;

        public override Func<bool>? DynamicEnabledCheck => () => true; // Will check in Execute

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Shift,
                Keys.F3,
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Shift, Keys.F3));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null && context.ActiveEditor.SearchState.HasValidSearch)
            {
                ScintillaManager.FindPrevious(context.ActiveEditor);
            }
        }
    }
}
