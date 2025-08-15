using Antlr4.Runtime.Misc;
using AppRefiner.Linters.Models;
using AppRefiner.PeopleCode;
using AppRefiner.QuickFixes;
using System;
using System.Reflection;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class UnusedLocalVariableStyler : ScopedStyler<object>
    {
        private const uint HIGHLIGHT_COLOR = 0x73737380; // Light gray text (no alpha)
        private readonly Dictionary<string, VariableInfo> instanceVariables = new(StringComparer.InvariantCultureIgnoreCase);
        // Track method parameters for later association with method scopes
        private readonly Dictionary<string, List<VariableInfo>> pendingMethodParameters = new(StringComparer.InvariantCultureIgnoreCase);
        private string? currentMethodName;

        public UnusedLocalVariableStyler()
        {
            Description = "Grays out unused local variables and private instance variables.";
            Active = true;
        }

        

        // handle non private properties (class properties) 
        public override void EnterNonPrivateProperty([NotNull] NonPrivatePropertyContext context)
        {
            base.EnterNonPrivateProperty(context);

            var propDecl = context.propertyDeclaration();
            
            if (propDecl is PropertyGetSetContext propGetSet)
            {

                var propID = propGetSet.genericID();
                if (propID == null) return;
                var propName = propID.GetText();

                var propAsVar = $"&{propName}";
                // Add to instance variables dictionary
                if (!instanceVariables.ContainsKey(propAsVar))
                {
                    instanceVariables[propAsVar] = new VariableInfo(
                        propAsVar,
                        "Property", // Type is just "Instance" for now
                        propID.Start.Line,
                        (propID.Start.ByteStartIndex(), propID.Stop.ByteStopIndex())
                    );
                }
            } else if (propDecl is PropertyDirectContext propDirect)
            {
                var propID = propDirect.genericID();
                if (propID == null) return;
                var propName = propID.GetText();
                var propAsVar = $"&{propName}";
                // Add to instance variables dictionary
                if (!instanceVariables.ContainsKey(propAsVar))
                {
                    instanceVariables[propAsVar] = new VariableInfo(
                        propAsVar,
                        "Property", // Type is just "Instance" for now
                        propID.Start.Line,
                        (propID.Start.ByteStartIndex(), propID.Stop.ByteStopIndex())
                    );
                }
            }

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
                            (varNode.Symbol.ByteStartIndex(), varNode.Symbol.ByteStopIndex())
                        );
                    }
                }
            }
        }

        // Track method header declarations to associate parameters later
        public override void EnterMethodHeader(MethodHeaderContext context)
        {
            base.EnterMethodHeader(context);

            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                var methodName = genericIdNode.GetText();
                currentMethodName = methodName;

                if (!pendingMethodParameters.ContainsKey(methodName))
                {
                    pendingMethodParameters[methodName] = new List<VariableInfo>();
                }
            }
        }

        // Handle method parameters - store them for later association with method scope
        public override void EnterMethodArgument(MethodArgumentContext context)
        {
            base.EnterMethodArgument(context);

            if (currentMethodName == null)
            {
                return; // Safety check
            }

            var varNode = context.USER_VARIABLE();
            if (varNode != null)
            {
                string varName = varNode.GetText();
                var line = varNode.Symbol.Line;

                // Store the parameter for later association with method scope
                pendingMethodParameters[currentMethodName].Add(
                    new VariableInfo(varName, "Parameter", line, (varNode.Symbol.ByteStartIndex(), varNode.Symbol.ByteStopIndex()))
                );
            }
        }

        // When entering a method implementation, associate any pending parameters with this scope
        public override void EnterMethod(MethodContext context)
        {
            base.EnterMethod(context);

            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                var methodName = genericIdNode.GetText();

                // Check if we have pending parameters for this method
                if (pendingMethodParameters.TryGetValue(methodName, out var parameters))
                {
                    // Add each parameter to the current method scope
                    foreach (var paramInfo in parameters)
                    {
                        // Use the existing AddLocalVariable method from ScopedStyler with dual indexing
                        AddLocalVariable(paramInfo.Name, paramInfo.Type, paramInfo.Line, paramInfo.Span);
                    }

                    // Remove the entry as parameters are now associated with the scope
                    pendingMethodParameters.Remove(methodName);
                }
            }
            // Clear currentMethodName after processing the method entry
            currentMethodName = null;
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

        public override void EnterForStatement([NotNull] ForStatementContext context)
        {
            base.EnterForStatement(context);
            if (context == null) return;
            var forVar = context.USER_VARIABLE();
            if (forVar == null) return;
            string forVarName = forVar.GetText();
            // Check if this is an instance variable and mark it as used
            if (instanceVariables.TryGetValue(forVarName, out var instanceVar) && instanceVar != null)
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
            
            AddLocalVariable(varName, "Constant", varNode.Symbol.Line, varNode.Symbol);
        }


        // Handle function parameters directly in the function scope
        public override void EnterFunctionArgument(FunctionArgumentContext context)
        {
            base.EnterFunctionArgument(context);

            var varNode = context.USER_VARIABLE();
            if (varNode != null)
            {
                string varName = varNode.GetText();
                var line = varNode.Symbol.Line;

                AddLocalVariable(varName, "Parameter", line, varNode.Symbol);
            }
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
                    AddScopedIndicator(variable, IndicatorType.TEXTCOLOR, HIGHLIGHT_COLOR,
                        variable.Type == "Parameter" ? "Unused parameter" : "Unused variable",
                        [(typeof(DeleteUnusedVariableDeclaration), variable.Type == "Parameter" ? "Delete unused parameter" : "Delete unused variable declaration")]);
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
                    AddScopedIndicator(variable, IndicatorType.TEXTCOLOR, HIGHLIGHT_COLOR,
                        variable.Type == "Parameter" ? "Unused parameter" : "Unused variable",
                        [(typeof(DeleteUnusedVariableDeclaration),variable.Type == "Parameter" ? "Delete unused parameter" : "Delete unused variable declaration")]);
                }
            }
            
            // Handle any unused instance variables
            foreach (var variable in instanceVariables.Values)
            {
                if (variable == null) continue;
                
                if (!variable.Used)
                {
                    AddScopedIndicator(variable, IndicatorType.TEXTCOLOR, HIGHLIGHT_COLOR,
                        "Unused instance variable",
                        [(typeof(DeleteUnusedVariableDeclaration),"Delete unused instance variable")]);
                }
            }
        }
        
        public override void Reset()
        {
            base.Reset();
            instanceVariables.Clear();
            pendingMethodParameters.Clear();
            currentMethodName = null;
        }
    }
}
