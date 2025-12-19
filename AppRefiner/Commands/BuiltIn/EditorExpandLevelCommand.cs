using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to expand the current fold level
    /// </summary>
    public class EditorExpandLevelCommand : BaseCommand
    {
        public override string CommandName => "Editor: Expand Level";

        public override string CommandDescription => "Expand the current fold level";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Shift,
                Keys.OemCloseBrackets, // ]
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Shift, Keys.OemCloseBrackets));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null)
            {
                ScintillaManager.SetCurrentLineFoldStatus(context.ActiveEditor, false);
            }
        }
    }
}
