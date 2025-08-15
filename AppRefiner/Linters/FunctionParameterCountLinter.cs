using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that identifies functions with too many parameters
    /// </summary>
    public class FunctionParameterCountLinter : BaseLintRule
    {
        public override string LINTER_ID => "FUNC_PARAM_COUNT";
        
        /// <summary>
        /// Maximum recommended number of parameters for a method
        /// </summary>
        public int MaxMethodParameters { get; set; } = 5;
        
        /// <summary>
        /// Maximum recommended number of parameters for a function
        /// </summary>
        public int MaxFunctionParameters { get; set; } = 5;

        public FunctionParameterCountLinter()
        {
            Description = "Detects functions with too many parameters";
            Type = ReportType.Warning;
            Active = false;
        }

        public override void EnterMethodHeader(MethodHeaderContext context)
        {
            var args = context.methodArguments();
            if (args == null)
                return;

            var paramCount = args.methodArgument().Length;

            if (paramCount > MaxMethodParameters)
            {
                AddReport(
                    1,
                    $"Method has {paramCount} parameters, which exceeds recommended maximum of {MaxMethodParameters}. Consider refactoring.",
                    Type,
                    context.Start.Line - 1,
                    context
                );
            }
        }

        public override void EnterFunctionDefinition(FunctionDefinitionContext context)
        {
            var args = context.functionArguments();
            if (args == null)
                return;

            var paramCount = args.functionArgument().Length;

            if (paramCount > MaxFunctionParameters)
            {
                AddReport(
                    2,
                    $"Function has {paramCount} parameters, which exceeds recommended maximum of {MaxFunctionParameters}. Consider using a compound parameter object.",
                    Type,
                    context.Start.Line - 1,
                    context
                );
            }
        }

        public override void Reset()
        {
        }
    }
}
