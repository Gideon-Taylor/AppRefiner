namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to open the indicator debug panel
    /// </summary>
    public class DebugOpenIndicatorPanelCommand : BaseCommand
    {
        public override string CommandName => "Debug: Open Indicator Panel";

        public override string CommandDescription => "Open the indicator debug panel to view applied styler indicators";

        public override bool RequiresActiveEditor => true;

        public override void Execute(CommandContext context)
        {
            var mainForm = context.MainForm as MainForm;
            if (mainForm != null)
            {
                mainForm.Invoke(() =>
                {
                    Debug.ShowIndicatorPanel(mainForm.Handle, mainForm);
                });
            }
        }
    }
}
