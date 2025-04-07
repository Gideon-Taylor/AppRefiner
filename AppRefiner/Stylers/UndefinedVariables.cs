using AppRefiner.Linters.Models;
using System;
using System.Collections.Generic;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class UndefinedVariableStyler : ScopedStyler<object>
    {
        private const uint HIGHLIGHT_COLOR = 0xFF0000A0; // Harsh red color with high alpha
        private readonly HashSet<string> instanceVariables = new();
        private readonly HashSet<string> markedVariables = new(); // Track already marked variables to avoid duplicates

        public UndefinedVariableStyler()
        {
            Description = "Highlights variables that are referenced but not defined in any accessible scope.";
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
                    
                    // Add to instance variables set
                    instanceVariables.Add(varName);
                }
            }
        }

        // Override to check if a used variable is defined
        public override void EnterIdentUserVariable(IdentUserVariableContext context)
        {
            base.EnterIdentUserVariable(context);
            
            if (context == null) return;
            
            string varName = context.GetText();
            if (string.IsNullOrEmpty(varName)) return;
            
            // Don't check special variables or already marked variables
            if (IsSpecialVariable(varName) || markedVariables.Contains(varName))
                return;
                
            // Check if this variable exists in any scope or as an instance variable
            if (!IsVariableDefined(varName) && !instanceVariables.Contains(varName))
            {
                // Variable is undefined, mark it
                Indicators?.Add(new Indicator
                {
                    Color = HIGHLIGHT_COLOR,
                    Start = context.Start.StartIndex,
                    Length = context.Stop.StopIndex - context.Start.StartIndex + 1,
                    Tooltip = "Undefined variable",
                    Type = IndicatorType.HIGHLIGHTER
                });
                
                // Remember that we've marked this variable to avoid duplicate highlights
                markedVariables.Add(varName);
            }
        }

        // Handle dot access checking for member access
        public override void EnterDotAccessExpr(DotAccessExprContext context)
        {
            base.EnterDotAccessExpr(context);
            
            // We don't need to validate dot access members here since we're only checking for undefined variables
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

        private bool IsSpecialVariable(string varName)
        {
            // Check for PeopleCode special variables
            return varName.StartsWith("%");
        }

        private bool IsVariableDefined(string varName)
        {
            // Check if variable is in current or any parent scope
            return TryGetVariableInfo(varName, out _);
        }
        
        // Required override from base ScopedStyler
        protected override void OnExitScope(Dictionary<string, object> scope, Dictionary<string, VariableInfo> variableScope)
        {
            // Nothing to do here, we're only checking for undefined variables
        }
        
        public override void Reset()
        {
            base.Reset();
            instanceVariables.Clear();
            markedVariables.Clear();
        }
    }
} 