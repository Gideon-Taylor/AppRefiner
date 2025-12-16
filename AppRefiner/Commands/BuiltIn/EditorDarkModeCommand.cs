using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to apply dark mode to the current editor
    /// </summary>
    public class EditorDarkModeCommand : BaseCommand
    {
        public override string CommandName => "Editor: Dark Mode";

        public override string CommandDescription => "Apply dark mode to the current editor";

        public override bool RequiresActiveEditor => true;

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null)
            {
                ScintillaManager.SetDarkMode(context.ActiveEditor);
            }
        }
    }
}
