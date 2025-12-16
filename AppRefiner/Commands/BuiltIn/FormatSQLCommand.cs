using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to format SQL code in the current editor
    /// </summary>
    public class FormatSQLCommand : BaseCommand
    {
        public override string CommandName => "SQL: Format SQL";

        public override string CommandDescription => "Format SQL code in the current editor";

        public override bool RequiresActiveEditor => true;

        public override Func<bool>? DynamicEnabledCheck => () => true; // Will check SQL type in Execute

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor != null && context.ActiveEditor.Type == EditorType.SQL)
            {
                ScintillaManager.ForceSQLFormat(context.ActiveEditor);
            }
        }
    }
}
