using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that identifies functions with too many parameters
    /// </summary>
    public class FunctionParameterCountLinter : BaseLintRule
    {
        public override string LINTER_ID => "FUNC_PARAM_COUNT";
        private const int MaxMethodParameters = 5;
        private const int MaxFunctionParameters = 5;
        
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
                Reports?.Add(AddReport(
                    1,
                    $"Method has {paramCount} parameters, which exceeds recommended maximum of {MaxMethodParameters}. Consider refactoring.",
                    Type,
                    context.Start.Line - 1,
                    (context.Start.StartIndex, context.Stop.StopIndex)
                ));
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
                Reports?.Add(AddReport(
                    2,
                    $"Function has {paramCount} parameters, which exceeds recommended maximum of {MaxFunctionParameters}. Consider using a compound parameter object.",
                    Type,
                    context.Start.Line - 1,
                    (context.Start.StartIndex, context.Stop.StopIndex)
                ));
            }
        }

        public override void Reset()
        {
        }
    }
}
