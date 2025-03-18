using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using System.Collections.Generic;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that identifies potentially unsafe recursive functions
    /// </summary>
    public class RecursiveFunctionLinter : BaseLintRule
    {
        public override string LINTER_ID => "RECURSIVE_FUNC";
        private Dictionary<string, FunctionDefinitionContext> functions = new Dictionary<string, FunctionDefinitionContext>();
        private string currentFunction = "";
        
        public RecursiveFunctionLinter()
        {
            Description = "Detects potentially unsafe recursive functions";
            Type = ReportType.Warning;
            Active = false;
        }
        
        public override void EnterFunctionDefinition(FunctionDefinitionContext context)
        {
            // Store the function name and context
            var functionName = context.allowableFunctionName().GetText().ToLower();
            functions[functionName] = context;
            currentFunction = functionName;
        }
        
        public override void ExitFunctionDefinition(FunctionDefinitionContext context)
        {
            currentFunction = "";
        }
        
        public override void EnterSimpleFunctionCall(SimpleFunctionCallContext context)
        {
            // Skip if we're not in a function
            if (string.IsNullOrEmpty(currentFunction))
                return;
                
            var calledFunctionName = context.genericID().GetText().ToLower();
            
            // Check if the function calls itself
            if (calledFunctionName == currentFunction)
            {
                // Check for termination conditions in the function
                var functionContext = functions[currentFunction];
                if (!HasSafeTerminationCondition(functionContext))
                {
                    Reports?.Add(AddReport(
                        1,
                        "Potentially unsafe recursive function call. Ensure there is a proper termination condition.",
                        Type,
                        context.Start.Line - 1,
                        (context.Start.StartIndex, context.Stop.StopIndex)
                    ));
                }
            }
        }
        
        private bool HasSafeTerminationCondition(FunctionDefinitionContext context)
        {
            // Look for IF statements with RETURN in the function
            var statements = context.statements();
            if (statements == null)
                return false;
                
            // Simple check: does it have at least one if statement?
            foreach (var stmt in statements.statement())
            {
                if (stmt is IfStmtContext)
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
