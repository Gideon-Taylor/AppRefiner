using AppRefiner.Linters.Models;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class UnusedLocalVariableStyler : ScopedStyler<object>
    {
        private const uint HIGHLIGHT_COLOR = 0x80808060;
        private readonly Dictionary<string, VariableInfo> instanceVariables = new();

        public UnusedLocalVariableStyler()
        {
            Description = "Grays out unused local variables and private instance variables.";
            Active = true;
        }

        // Handle private instance variables
        public override void EnterPrivateProperty(PrivatePropertyContext context)
        {
            base.EnterPrivateProperty(context);
            
            var instanceDeclContext = context.instanceDeclaration();
            if (instanceDeclContext is InstanceDeclContext instanceDecl)
            {
                // Process each variable in the instance declaration
                foreach (var varNode in instanceDecl.USER_VARIABLE())
                {
                    string varName = varNode.GetText();
                    
                    // Add to instance variables dictionary
                    if (!instanceVariables.ContainsKey(varName))
                    {
                        instanceVariables[varName] = new VariableInfo(
                            varName,
                            "Instance", // Type is just "Instance" for now
                            varNode.Symbol.Line,
                            (varNode.Symbol.StartIndex, varNode.Symbol.StopIndex)
                        );
                    }
                }
            }
        }

        // Override to track usage of instance variables
        public override void EnterIdentUserVariable(IdentUserVariableContext context)
        {
            base.EnterIdentUserVariable(context);
            
            string varName = context.GetText();
            
            // Check if this is an instance variable and mark it as used
            if (instanceVariables.TryGetValue(varName, out var instanceVar))
            {
                instanceVar.Used = true;
            }
        }

        // Handle top-level constants in non-class programs
        public override void EnterConstantDeclaration(ConstantDeclarationContext context)
        {
            base.EnterConstantDeclaration(context);
            
            var varNode = context.USER_VARIABLE();
            if (varNode != null)
            {
                string varName = varNode.GetText();
                
                // Add to the current scope
                AddLocalVariable(
                    varName,
                    "Constant",
                    varNode.Symbol.Line,
                    varNode.Symbol.StartIndex,
                    varNode.Symbol.StopIndex
                );
            }
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
                        Color = HIGHLIGHT_COLOR,
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
                        Color = HIGHLIGHT_COLOR,
                        Start = variable.Span.Start,
                        Length = variable.Span.Stop - variable.Span.Start + 1
                    });
                }
            }
            
            // Handle any unused instance variables
            foreach (var variable in instanceVariables.Values)
            {
                if (!variable.Used)
                {
                    Highlights?.Add(new CodeHighlight()
                    {
                        Color = HIGHLIGHT_COLOR,
                        Start = variable.Span.Start,
                        Length = variable.Span.Stop - variable.Span.Start + 1
                    });
                }
            }
        }
        
        public override void Reset()
        {
            base.Reset();
            instanceVariables.Clear();
        }
    }
}
