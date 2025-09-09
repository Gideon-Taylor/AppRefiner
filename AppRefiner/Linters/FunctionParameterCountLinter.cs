using PeopleCodeParser.SelfHosted.Nodes;

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

        public override void VisitMethod(MethodNode node)
        {
            var paramCount = node.Parameters.Count;

            if (paramCount > MaxMethodParameters)
            {
                AddReport(
                    1,
                    $"Method has {paramCount} parameters, which exceeds recommended maximum of {MaxMethodParameters}. Consider refactoring.",
                    Type,
                    node.SourceSpan.Start.Line,
                    node.SourceSpan
                );
            }

            base.VisitMethod(node);
        }

        public override void VisitFunction(FunctionNode node)
        {
            var paramCount = node.Parameters.Count;

            if (paramCount > MaxFunctionParameters)
            {
                AddReport(
                    2,
                    $"Function has {paramCount} parameters, which exceeds recommended maximum of {MaxFunctionParameters}. Consider using a compound parameter object.",
                    Type,
                    node.SourceSpan.Start.Line,
                    node.SourceSpan
                );
            }

            base.VisitFunction(node);
        }
    }
}
