namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to open the debug console
    /// </summary>
    public class DebugOpenConsoleCommand : BaseCommand
    {
        public override string CommandName => "Debug: Open Debug Console";

        public override string CommandDescription => "Open the debug console to view application logs";

        public override bool RequiresActiveEditor => false;

        public override void Execute(CommandContext context)
        {
            context.MainForm?.Invoke(() =>
            {
                Debug.ShowDebugDialog(context.MainForm.Handle);
            });
        }
    }
}
