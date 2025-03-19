using AppRefiner.Linters.Models;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class UnusedLocalVariableStyler : ScopedStyler<object>
    {
        public UnusedLocalVariableStyler()
        {
            Description = "Grays out unused local variables.";
            Active = true;
        }

        protected override void OnExitScope(Dictionary<string, object> scope, Dictionary<string, VariableInfo> variableScope)
        {
            // Check for unused variables in the current scope
            foreach (var variable in variableScope.Values)
            {
                if (!variable.Used)
                {
                    // Add both highlight and color for unused variables
                    Highlights?.Add(new CodeHighlight()
                    {
                        Color = HighlightColor.Gray,
                        Start = variable.Span.Start,
                        Length = variable.Span.Stop - variable.Span.Start + 1
                    });

                }
            }
        }

        public override void ExitProgram(ProgramContext context)
        {
            // Handle any unused variables in the global scope
            foreach (var variable in GetVariablesInCurrentScope())
            {
                if (!variable.Used)
                {
                    Highlights?.Add(new CodeHighlight()
                    {
                        Color = HighlightColor.Gray,
                        Start = variable.Span.Start,
                        Length = variable.Span.Stop - variable.Span.Start + 1
                    });
                }
            }
        }
    }
}
