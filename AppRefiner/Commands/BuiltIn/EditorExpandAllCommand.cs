using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to expand all foldable sections
    /// </summary>
    public class EditorExpandAllCommand : BaseCommand
    {
        public override string CommandName => "Editor: Expand All";

        public override string CommandDescription => "Expand all foldable sections";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt,
                Keys.OemCloseBrackets, // ]
                () => Execute(new CommandContext())))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, Keys.OemCloseBrackets));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null)
            {
                ScintillaManager.ExpandTopLevel(context.ActiveEditor);
            }
        }
    }
}
