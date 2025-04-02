using AppRefiner.Linters.Models;
using System;
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
                    if (varNode == null) continue;
                    
                    string varName = varNode.GetText();
                    if (string.IsNullOrEmpty(varName)) continue;
                    
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
            
            if (context == null) return;
            
            string varName = context.GetText();
            if (string.IsNullOrEmpty(varName)) return;
            
            // Check if this is an instance variable and mark it as used
            if (instanceVariables.TryGetValue(varName, out var instanceVar) && instanceVar != null)
            {
                instanceVar.Used = true;
            }
        }

        // Track %This dot access to instance variables (without the & prefix)
        public override void EnterDotAccessExpr(DotAccessExprContext context)
        {
            base.EnterDotAccessExpr(context);
            
            if (context == null) return;
            
            // Check if the expression is %THIS
            var expr = context.expression();
            if (expr != null && expr.GetText().Equals("%THIS", StringComparison.OrdinalIgnoreCase))
            {
                // Get the member name from the dot access
                var dotAccesses = context.dotAccess();
                if (dotAccesses != null && dotAccesses.Length > 0)
                {
                    var firstDotAccess = dotAccesses[0];
                    if (firstDotAccess == null) return;
                    
                    var genericId = firstDotAccess.genericID();
                    if (genericId == null) return;
                    
                    var memberName = genericId.GetText();
                    if (string.IsNullOrEmpty(memberName)) return;
                    
                    // In dot access after %This, the variable name will be without & prefix
                    // We need to find the matching instance variable with &
                    string varNameWithPrefix = $"&{memberName}";
                    
                    // Check if this variable exists as an instance variable
                    if (instanceVariables.TryGetValue(varNameWithPrefix, out var instanceVar) && instanceVar != null)
                    {
                        // Mark as used
                        instanceVar.Used = true;
                    }
                }
            }
        }

        // Handle top-level constants in non-class programs
        public override void EnterConstantDeclaration(ConstantDeclarationContext context)
        {
            base.EnterConstantDeclaration(context);
            
            if (context == null) return;
            
            var varNode = context.USER_VARIABLE();
            if (varNode == null) return;
            
            string varName = varNode.GetText();
            if (string.IsNullOrEmpty(varName)) return;
            
            // Add to the current scope
            AddLocalVariable(
                varName,
                "Constant",
                varNode.Symbol.Line,
                varNode.Symbol.StartIndex,
                varNode.Symbol.StopIndex
            );
        }

        protected override void OnExitScope(Dictionary<string, object> scope, Dictionary<string, VariableInfo> variableScope)
        {
            if (variableScope == null) return;
            
            // Check for unused variables in the current scope
            foreach (var variable in variableScope.Values)
            {
                if (variable == null) continue;
                
                if (!variable.Used)
                {
                    // Add indicator for unused variables
                    Indicators?.Add(new Indicator
                    {
                        Color = HIGHLIGHT_COLOR,
                        Start = variable.Span.Start,
                        Length = variable.Span.Stop - variable.Span.Start + 1,
                        Tooltip = "Unused variable",
                        Type = IndicatorType.HIGHLIGHTER
                    });
                }
            }
        }

        public override void ExitProgram(ProgramContext context)
        {
            // Handle any unused variables in the global scope
            foreach (var variable in GetVariablesInCurrentScope())
            {
                if (variable == null) continue;
                
                if (!variable.Used)
                {
                    Indicators?.Add(new Indicator
                    {
                        Color = HIGHLIGHT_COLOR,
                        Start = variable.Span.Start,
                        Length = variable.Span.Stop - variable.Span.Start + 1,
                        Tooltip = "Unused variable",
                        Type = IndicatorType.HIGHLIGHTER
                    });
                }
            }
            
            // Handle any unused instance variables
            foreach (var variable in instanceVariables.Values)
            {
                if (variable == null) continue;
                
                if (!variable.Used)
                {
                    Indicators?.Add(new Indicator
                    {
                        Color = HIGHLIGHT_COLOR,
                        Start = variable.Span.Start,
                        Length = variable.Span.Stop - variable.Span.Start + 1,
                        Tooltip = "Unused instance variable",
                        Type = IndicatorType.HIGHLIGHTER
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
