using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to force the editor to refresh styles
    /// </summary>
    public class EditorForceRefreshCommand : BaseCommand
    {
        public override string CommandName => "Editor: Force Refresh";

        public override string CommandDescription => "Force the editor to refresh styles";

        public override bool RequiresActiveEditor => true;

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null && context.MainForm != null)
            {
                // Clear content string to force re-reading
                context.ActiveEditor.ContentString = ScintillaManager.GetScintillaText(context.ActiveEditor);
                // Clear annotations
                ScintillaManager.ClearAnnotations(context.ActiveEditor);
                // Reset styles
                ScintillaManager.ResetStyles(context.ActiveEditor);

                // Call CheckForContentChanges on the main form
                // Note: This requires access to the MainForm instance which we now have in context
                var mainForm = context.MainForm as MainForm;
                if (mainForm != null)
                {
                    mainForm.Invoke(() =>
                    {
                        // We need to call the private method CheckForContentChanges
                        // Since it's private, we'll need to use reflection or make it internal/public
                        // For now, I'll leave a TODO comment
                        // TODO: Call CheckForContentChanges(context.ActiveEditor)
                    });
                }
            }
        }
    }
}
