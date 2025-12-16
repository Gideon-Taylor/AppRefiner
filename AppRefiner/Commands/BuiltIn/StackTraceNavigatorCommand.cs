namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to open the Stack Trace Navigator
    /// </summary>
    public class StackTraceNavigatorCommand : BaseCommand
    {
        public override string CommandName => "Stack Trace Navigator";

        public override string CommandDescription => "Open the Stack Trace Navigator to parse and navigate PeopleCode stack traces";

        public override bool RequiresActiveEditor => false;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Alt,
                Keys.S,
                () => Execute(new CommandContext())))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Alt, Keys.S));
            }
        }

        public override void Execute(CommandContext context)
        {
            // Delay showing the dialog to allow Command Palette to close first
            Task.Delay(100).ContinueWith(_ =>
            {
                context.MainForm?.BeginInvoke(new Action(() =>
                {
                    var mainForm = context.MainForm as MainForm;
                    mainForm?.showStackTraceNavigatorHandler();
                }));
            });
        }
    }
}
