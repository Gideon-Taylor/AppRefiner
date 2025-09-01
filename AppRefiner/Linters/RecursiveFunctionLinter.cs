using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that identifies potentially unsafe recursive functions
    /// </summary>
    public class RecursiveFunctionLinter : BaseLintRule
    {
        public override string LINTER_ID => "RECURSIVE_FUNC";
        private Dictionary<string, FunctionNode> functions = new();
        private string currentFunction = "";

        public RecursiveFunctionLinter()
        {
            Description = "Detects potentially unsafe recursive functions";
            Type = ReportType.Warning;
            Active = false;
        }

        public override void VisitFunction(FunctionNode node)
        {
            // Store the function name and context
            var functionName = node.Name.ToLower();
            functions[functionName] = node;
            currentFunction = functionName;

            // Visit the function body
            base.VisitFunction(node);

            currentFunction = "";
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            // Skip if we're not in a function
            if (string.IsNullOrEmpty(currentFunction))
                return;

            // Check if the function being called is an identifier
            if (!(node.Function is IdentifierNode functionId))
                return;

            var calledFunctionName = functionId.Name.ToLower();

            // Check if the function calls itself
            if (calledFunctionName == currentFunction)
            {
                // Check for termination conditions in the function
                var functionNode = functions[currentFunction];
                if (!HasSafeTerminationCondition(functionNode))
                {
                    AddReport(
                        1,
                        "Potentially unsafe recursive function call. Ensure there is a proper termination condition.",
                        Type,
                        node.SourceSpan.Start.Line,
                        node.SourceSpan
                    );
                }
            }

            base.VisitFunctionCall(node);
        }

        private bool HasSafeTerminationCondition(FunctionNode node)
        {
            // Look for IF statements in the function body
            if (node.Body == null)
                return false;

            // Simple check: does it have at least one if statement?
            foreach (var stmt in node.Body.Statements)
            {
                if (stmt is IfStatementNode)
                    return true;  // It has at least one IF, assume it's for termination
            }

            return false;  // No IF statements found, might be unsafe
        }

        public override void Reset()
        {
            functions.Clear();
            currentFunction = "";
        }
    }
}
