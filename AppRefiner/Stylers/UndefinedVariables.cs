using AppRefiner.Linters.Models;
using AppRefiner.Refactors;
using System;
using System.Collections.Generic;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class UndefinedVariableStyler : ScopedStyler<bool>
    {
        private const uint HIGHLIGHT_COLOR = 0x0000FFA0; // Harsh red color with high alpha
        private readonly HashSet<string> instanceVariables = new(StringComparer.InvariantCultureIgnoreCase);
        private readonly HashSet<string> classProperties = new(StringComparer.InvariantCultureIgnoreCase); // Track declared class properties

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
        
        // Handle non-private property declarations (PropertyGetSet)
        public override void EnterPropertyGetSet(PropertyGetSetContext context)
        {
            base.EnterPropertyGetSet(context);
            
            if (context == null || context.genericID() == null) return;
            
            string propertyName = context.genericID().GetText();
            if (string.IsNullOrEmpty(propertyName)) return;
            
            // Register property as a defined class property
            classProperties.Add($"&{propertyName}");
        }
        
        // Handle non-private property declarations (PropertyDirect)
        public override void EnterPropertyDirect(PropertyDirectContext context)
        {
            base.EnterPropertyDirect(context);
            
            if (context == null || context.genericID() == null) return;
            
            string propertyName = context.genericID().GetText();
            if (string.IsNullOrEmpty(propertyName)) return;

            // Register property as a defined class property
            classProperties.Add($"&{propertyName}");
        }
        
        // Handle parameters from method headers 
        public override void EnterMethodHeader(MethodHeaderContext context)
        {
            base.EnterMethodHeader(context);
            
            // Process method arguments if available
            var methodArgs = context.methodArguments();
            if (methodArgs == null) return;
            
            // Process each parameter and add it to the current scope
            foreach (var arg in methodArgs.methodArgument())
            {
                if (arg.USER_VARIABLE() == null) continue;
                
                string paramName = arg.USER_VARIABLE().GetText();
                if (string.IsNullOrEmpty(paramName)) continue;
                
                string paramType = arg.typeT() != null ? arg.typeT().GetText() : "Any";
                
                // Register method parameter as a defined variable
                AddLocalVariable(
                    paramName,
                    paramType,
                    arg.USER_VARIABLE().Symbol.Line,
                    arg.USER_VARIABLE().Symbol.StartIndex,
                    arg.USER_VARIABLE().Symbol.StopIndex
                );
            }
        }
        
        // Handle setter parameters (properties can have a single parameter in their setter)
        public override void EnterSetter(SetterContext context)
        {
            // Call base implementation first to create a new scope
            base.EnterSetter(context);
            
            // Get method name
            if (context.genericID() == null) return;
            
            // Process setter parameter if available in method annotations
            var paramAnnotation = context.methodParameterAnnotation();
            if (paramAnnotation == null) return;
            
            // Get the argument
            var arg = paramAnnotation.methodAnnotationArgument();
            if (arg == null || arg.USER_VARIABLE() == null) return;
            
            string paramName = arg.USER_VARIABLE().GetText();
            if (string.IsNullOrEmpty(paramName)) return;
            
            string paramType = arg.annotationType() != null ? arg.annotationType().GetText() : "Any";
            
            // Register setter parameter as a defined variable in the current scope
            AddLocalVariable(
                paramName,
                paramType,
                arg.USER_VARIABLE().Symbol.Line,
                arg.USER_VARIABLE().Symbol.StartIndex,
                arg.USER_VARIABLE().Symbol.StopIndex
            );
        }
        
        // Handle function parameters to register them as defined variables
        public override void EnterFunctionDefinition(FunctionDefinitionContext context)
        {
            // Call base implementation first to create a new scope
            base.EnterFunctionDefinition(context);
            
            // Process function parameters if available
            var funcArgs = context.functionArguments();
            if (funcArgs == null) return;
            
            // Process each parameter and add it to the current scope
            foreach (var arg in funcArgs.functionArgument())
            {
                if (arg.USER_VARIABLE() == null) continue;
                
                string paramName = arg.USER_VARIABLE().GetText();
                if (string.IsNullOrEmpty(paramName)) continue;
                
                string paramType = arg.typeT() != null ? arg.typeT().GetText() : "Any";
                
                // Register function parameter as a defined variable in the current scope
                AddLocalVariable(
                    paramName,
                    paramType,
                    arg.USER_VARIABLE().Symbol.Line,
                    arg.USER_VARIABLE().Symbol.StartIndex,
                    arg.USER_VARIABLE().Symbol.StopIndex
                );
            }
        }
        
        // Handle catch clause variables
        public override void EnterCatchClause(CatchClauseContext context)
        {
            base.EnterCatchClause(context);
            
            if (context == null || context.USER_VARIABLE() == null) return;
            
            string varName = context.USER_VARIABLE().GetText();
            if (string.IsNullOrEmpty(varName)) return;
            
            // Determine the type (Exception or app class)
            string varType = (context.EXCEPTION() != null) ? "Exception" : 
                              (context.appClassPath() != null) ? context.appClassPath().GetText() : "Exception";
            
            // Register the catch variable
            AddLocalVariable(
                varName,
                varType,
                context.USER_VARIABLE().Symbol.Line,
                context.USER_VARIABLE().Symbol.StartIndex,
                context.USER_VARIABLE().Symbol.StopIndex
            );
        }

        // Override to check if a used variable is defined
        public override void EnterIdentUserVariable(IdentUserVariableContext context)
        {
            base.EnterIdentUserVariable(context);
            
            if (context == null) return;
            
            string varName = context.GetText();
            if (string.IsNullOrEmpty(varName)) return;
            
            // Don't check special variables
            if (IsSpecialVariable(varName))
                return;
                
            // Get the current scope to check if we've already marked this variable in this scope
            Dictionary<string, bool> currentScope = GetCurrentScope();
            
            // Check if this variable is already marked in the current scope
            if (TryFindInScopes(varName, out _))
                return;
                
            // Check if this variable exists in any scope, as an instance variable, or as a class property
            if (!IsVariableDefined(varName) && !instanceVariables.Contains(varName) && !classProperties.Contains(varName))
            {
                // Variable is undefined, mark it
                Indicators?.Add(new Indicator
                {
                    Color = HIGHLIGHT_COLOR,
                    Start = context.Start.StartIndex,
                    Length = context.Stop.StopIndex - context.Start.StartIndex + 1,
                    Tooltip = "Undefined variable",
                    Type = IndicatorType.HIGHLIGHTER,
                    QuickFixes = []
                });
                
                // Add this variable to the current scope's marked variables
                AddToCurrentScope(varName, true);
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
        
        public override void Reset()
        {
            base.Reset();
            instanceVariables.Clear();
            classProperties.Clear();
        }
    }
} 