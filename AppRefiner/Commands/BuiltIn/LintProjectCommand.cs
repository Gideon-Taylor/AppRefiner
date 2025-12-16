using AppRefiner.Dialogs;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to run all linters on the entire project and generate a report
    /// </summary>
    public class LintProjectCommand : BaseCommand
    {
        public override string CommandName => "Project: Lint Project";

        public override string CommandDescription => "Run all linters on the entire project and generate a report";

        public override bool RequiresActiveEditor => true;

        public override Func<bool>? DynamicEnabledCheck => () => true; // Will check database in Execute

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null && context.LinterManager != null && context.ActiveEditor.DataManager != null)
            {
                var mainHandle = context.ActiveEditor.AppDesignerProcess.MainWindowHandle;

                context.MainForm?.Invoke(() =>
                {
                    using var lintDialog = new LintProjectProgressDialog(context.LinterManager, context.ActiveEditor, mainHandle);
                    lintDialog.ShowDialog(new WindowWrapper(mainHandle));
                });
            }
        }
    }
}
