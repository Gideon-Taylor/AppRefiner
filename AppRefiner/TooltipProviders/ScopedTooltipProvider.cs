using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.Linters.Models;
using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Base class for tooltip providers that need access to variable scoping.
    /// This class tracks variables through different scopes similar to ScopedRefactor.
    /// </summary>
    public abstract class ScopedTooltipProvider : ParseTreeTooltipProvider
    {
        /// <summary>
        /// Gets the display name of this tooltip provider
        /// </summary>
        public override string Name => "Scoped Tooltip Provider";

        /// <summary>
        /// Gets the description of this tooltip provider
        /// </summary>
        public override string Description => "Tooltip provider with variable scope tracking";

        /// <summary>
        /// Stack of variable scopes, with the top of the stack being the current scope
        /// </summary>
        protected readonly Stack<Dictionary<string, VariableInfo>> variableScopeStack = new();

        /// <summary>
        /// Initializes a new instance of the ScopedTooltipProvider class
        /// </summary>
        protected ScopedTooltipProvider()
        {
            // Start with a global scope
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Adds a local variable to the current scope
        /// </summary>
        protected void AddLocalVariable(string name, string type, int line, int start, int stop)
        {
            var currentScope = variableScopeStack.Peek();
            if (!currentScope.ContainsKey(name))
            {
                currentScope[name] = new VariableInfo(name, type, line, (start, stop));
                OnVariableDeclared(currentScope[name]);
            }
        }

        /// <summary>
        /// Attempts to find variable information in all accessible scopes
        /// </summary>
        protected bool TryGetVariableInfo(string name, out VariableInfo? info)
        {
            info = null;
            foreach (var scope in variableScopeStack)
            {
                if (scope.TryGetValue(name, out info))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets all variables in the current scope
        /// </summary>
        protected IEnumerable<VariableInfo> GetVariablesInCurrentScope()
        {
            return variableScopeStack.Peek().Values;
        }

        /// <summary>
        /// Gets all variables in all accessible scopes
        /// </summary>
        protected IEnumerable<VariableInfo> GetAllAccessibleVariables()
        {
            return variableScopeStack.SelectMany(scope => scope.Values);
        }

        /// <summary>
        /// Marks a variable as used in any scope
        /// </summary>
        protected void MarkVariableAsUsed(string name)
        {
            foreach (var scope in variableScopeStack)
            {
                if (scope.TryGetValue(name, out var info))
                {
                    info.Used = true;
                    OnVariableUsed(info);
                    break;
                }
            }
        }

        // Parser overrides for scope management
        public override void EnterMethod(MethodContext context)
        {
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.OrdinalIgnoreCase));
            OnEnterScope();
        }

        public override void ExitMethod(MethodContext context)
        {
            var scope = variableScopeStack.Pop();
            OnExitScope(scope);
        }

        public override void EnterFunctionDefinition(FunctionDefinitionContext context)
        {
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.OrdinalIgnoreCase));
            OnEnterScope();
        }

        public override void ExitFunctionDefinition(FunctionDefinitionContext context)
        {
            var scope = variableScopeStack.Pop();
            OnExitScope(scope);
        }

        public override void EnterGetter(GetterContext context)
        {
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.OrdinalIgnoreCase));
            OnEnterScope();
        }

        public override void ExitGetter(GetterContext context)
        {
            var scope = variableScopeStack.Pop();
            OnExitScope(scope);
        }

        public override void EnterSetter(SetterContext context)
        {
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.OrdinalIgnoreCase));
            OnEnterScope();
        }

        public override void ExitSetter(SetterContext context)
        {
            var scope = variableScopeStack.Pop();
            OnExitScope(scope);
        }

        public override void EnterLocalVariableDefinition(LocalVariableDefinitionContext context)
        {
            // Extract type information from the type context
            var typeContext = context.typeT();
            string typeName = GetTypeFromContext(typeContext);

            // Process each variable declaration in the list
            foreach (var varNode in context.USER_VARIABLE())
            {
                string varName = varNode.GetText();
                AddLocalVariable(
                    varName,
                    typeName,
                    varNode.Symbol.Line,
                    varNode.Symbol.ByteStartIndex(),
                    varNode.Symbol.ByteStopIndex()
                );
            }
        }

        public override void EnterLocalVariableDeclAssignment(LocalVariableDeclAssignmentContext context)
        {
            // Extract type information from the type context
            var typeContext = context.typeT();
            string typeName = GetTypeFromContext(typeContext);

            // Process the single variable declaration
            var varNode = context.USER_VARIABLE();
            string varName = varNode.GetText();
            AddLocalVariable(
                varName,
                typeName,
                varNode.Symbol.Line,
                varNode.Symbol.ByteStartIndex(),
                varNode.Symbol.ByteStopIndex()
            );
        }

        public override void EnterConstantDeclaration(ConstantDeclarationContext context)
        {
            // Extract variable information
            var varNode = context.USER_VARIABLE();
            if (varNode != null)
            {
                string varName = varNode.GetText();
                
                // Constants are implicitly typed based on their literal value
                // For simplicity, we'll just use "Constant" as the type
                AddLocalVariable(
                    varName,
                    "Constant",
                    varNode.Symbol.Line,
                    varNode.Symbol.ByteStartIndex(),
                    varNode.Symbol.ByteStopIndex()
                );
            }
        }

        public override void EnterIdentUserVariable(IdentUserVariableContext context)
        {
            string varName = context.GetText();
            if (TryGetVariableInfo(varName, out var info) && info != null)
            {
                info.Used = true;
                OnVariableUsed(info);
            }
        }

        // Method parameters
        public override void EnterMethodArgument(MethodArgumentContext context)
        {
            if (context.USER_VARIABLE() != null && context.typeT() != null)
            {
                var varNode = context.USER_VARIABLE();
                string varName = varNode.GetText();
                string typeName = GetTypeFromContext(context.typeT());
                
                AddLocalVariable(
                    varName,
                    typeName,
                    varNode.Symbol.Line,
                    varNode.Symbol.ByteStartIndex(),
                    varNode.Symbol.ByteStopIndex()
                );
            }
        }

        // Function parameters
        public override void EnterFunctionArgument(FunctionArgumentContext context)
        {
            if (context.USER_VARIABLE() != null)
            {
                var varNode = context.USER_VARIABLE();
                string varName = varNode.GetText();
                string typeName = context.typeT() != null 
                    ? GetTypeFromContext(context.typeT()) 
                    : "any";
                
                AddLocalVariable(
                    varName,
                    typeName,
                    varNode.Symbol.Line,
                    varNode.Symbol.ByteStartIndex(),
                    varNode.Symbol.ByteStopIndex()
                );
            }
        }

        // Instance variables
        public override void EnterInstanceDecl(InstanceDeclContext context)
        {
            if (context.typeT() != null && context.USER_VARIABLE() != null)
            {
                var typeContext = context.typeT();
                string typeName = GetTypeFromContext(typeContext);
                
                foreach (var varNode in context.USER_VARIABLE())
                {
                    string varName = varNode.GetText();
                    AddLocalVariable(
                        varName,
                        typeName,
                        varNode.Symbol.Line,
                        varNode.Symbol.ByteStartIndex(),
                        varNode.Symbol.ByteStopIndex()
                    );
                }
            }
        }

        /// <summary>
        /// Resets the state of this tooltip provider
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            
            while (variableScopeStack.Count > 0)
            {
                var dict = variableScopeStack.Pop();
                dict.Clear();
            }

            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Helper method to extract type information from the type context
        /// </summary>
        protected static string GetTypeFromContext(TypeTContext typeContext)
        {
            if (typeContext is ArrayTypeContext arrayType)
            {
                var baseType = arrayType.typeT() != null
                    ? GetTypeFromContext(arrayType.typeT())
                    : "Any";
                return $"Array of {baseType}";
            }
            else if (typeContext is BaseExceptionTypeContext)
            {
                return "Exception";
            }
            else if (typeContext is AppClassTypeContext appClass)
            {
                return appClass.appClassPath().GetText();
            }
            else if (typeContext is SimpleTypeTypeContext simpleType)
            {
                return simpleType.simpleType().GetText();
            }

            return "Any"; // Default type if none specified
        }

        /// <summary>
        /// Determines if a type is an Application Class (contains colon characters)
        /// </summary>
        protected static bool IsAppClassType(string typeName)
        {
            return !string.IsNullOrEmpty(typeName) && typeName.Contains(':');
        }

        // Virtual methods for derived classes to handle scope and variable changes
        protected virtual void OnVariableDeclared(VariableInfo varInfo) { }
        protected virtual void OnVariableUsed(VariableInfo varInfo) { }
        protected virtual void OnEnterScope() { }
        protected virtual void OnExitScope(Dictionary<string, VariableInfo> variableScope) { }
    }
} 