using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to clear all annotations from the current editor
    /// </summary>
    public class EditorClearAnnotationsCommand : BaseCommand
    {
        public override string CommandName => "Editor: Clear Annotations";

        public override string CommandDescription => "Clear all annotations from the current editor";

        public override bool RequiresActiveEditor => true;

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null)
            {
                ScintillaManager.ClearAnnotations(context.ActiveEditor);
            }
        }
    }
}
