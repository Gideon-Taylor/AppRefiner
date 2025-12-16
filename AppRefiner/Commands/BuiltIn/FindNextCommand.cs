using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to find the next occurrence of the search term
    /// </summary>
    public class FindNextCommand : BaseCommand
    {
        public override string CommandName => "Editor: Find Next";

        public override string CommandDescription => "Find the next occurrence of the search term";

        public override bool RequiresActiveEditor => true;

        public override Func<bool>? DynamicEnabledCheck => () => true; // Will check in Execute

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.None,
                Keys.F3,
                () => Execute(new CommandContext())))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.None, Keys.F3));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null && context.ActiveEditor.SearchState.HasValidSearch)
            {
                ScintillaManager.FindNext(context.ActiveEditor);
            }
        }
    }
}
