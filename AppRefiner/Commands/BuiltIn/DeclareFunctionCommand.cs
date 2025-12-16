namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to declare an external function
    /// </summary>
    public class DeclareFunctionCommand : BaseCommand
    {
        public override string CommandName => "Declare Function";

        public override string CommandDescription => "Declare an external function";

        public override bool RequiresActiveEditor => false;

        public override void Execute(CommandContext context)
        {
            var mainForm = context.MainForm as MainForm;
            mainForm?.ShowDeclareFunctionDialog();
        }
    }
}
