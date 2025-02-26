using System;
using System.Collections.Generic;
using AppRefiner.Linters.Models;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    public abstract class ScopedLintRule<T> : BaseLintRule
    {
        protected readonly Stack<Dictionary<string, T>> scopeStack = new();
        protected readonly Stack<Dictionary<string, VariableInfo>> variableScopeStack = new();

        protected ScopedLintRule()
        {
            // Start with a global scope
            scopeStack.Push(new Dictionary<string, T>());
            variableScopeStack.Push(new Dictionary<string, VariableInfo>());
        }

        // Variable tracking methods
        protected void AddLocalVariable(string name, string type, int line, int start, int stop)
        {
            var currentScope = variableScopeStack.Peek();
            if (!currentScope.ContainsKey(name))
            {
                currentScope[name] = new VariableInfo(name, type, line, (start, stop));
            }
        }

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

        protected IEnumerable<VariableInfo> GetVariablesInCurrentScope()
        {
            return variableScopeStack.Peek().Values;
        }

        protected void MarkVariableAsUsed(string name)
        {
            foreach (var scope in variableScopeStack)
            {
                if (scope.TryGetValue(name, out var info))
                {
                    info.Used = true;
                    break;
                }
            }
        }

        // New variable tracking parser overrides
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
                    varNode.Symbol.StartIndex,
                    varNode.Symbol.StopIndex
                );
                OnVariableDeclared(variableScopeStack.Peek()[varName]);
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
                varNode.Symbol.StartIndex,
                varNode.Symbol.StopIndex
            );
            OnVariableDeclared(variableScopeStack.Peek()[varName]);
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

        // Helper method to extract type information from the type context
        private string GetTypeFromContext(TypeTContext typeContext)
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

        // Existing scope management methods and other code...
        protected Dictionary<string, T> GetCurrentScope() => scopeStack.Peek();

        protected void AddToCurrentScope(string key, T value)
        {
            var currentScope = GetCurrentScope();
            if (!currentScope.ContainsKey(key))
            {
                currentScope[key] = value;
            }
        }

        protected void ReplaceInFoundScope(string key, T newValue)
        {
            foreach (var scope in scopeStack)
            {
                if (scope.ContainsKey(key))
                {
                    scope[key] = newValue;
                    return;
                }
            }
        }

        protected bool TryFindInScopes(string key, out T? value)
        {
            value = default;
            foreach (var scope in scopeStack)
            {
                if (scope.TryGetValue(key, out value))
                {
                    return true;
                }
            }
            return false;
        }

        // Updated scope management overrides
        public override void EnterMethod(MethodContext context)
        {
            scopeStack.Push(new Dictionary<string, T>());
            variableScopeStack.Push(new Dictionary<string, VariableInfo>());
            OnEnterScope();
        }

        public override void ExitMethod(MethodContext context)
        {
            var scope = scopeStack.Pop();
            var varScope = variableScopeStack.Pop();
            OnExitScope(scope);
        }

        public override void EnterFunctionDefinition(FunctionDefinitionContext context)
        {
            scopeStack.Push(new Dictionary<string, T>());
            variableScopeStack.Push(new Dictionary<string, VariableInfo>());
            OnEnterScope();
        }

        public override void ExitFunctionDefinition(FunctionDefinitionContext context)
        {
            var scope = scopeStack.Pop();
            var varScope = variableScopeStack.Pop();
            OnExitScope(scope);
        }

        public override void EnterGetter(GetterContext context)
        {
            scopeStack.Push(new Dictionary<string, T>());
            variableScopeStack.Push(new Dictionary<string, VariableInfo>());
            OnEnterScope();
        }

        public override void ExitGetter(GetterContext context)
        {
            var scope = scopeStack.Pop();
            var varScope = variableScopeStack.Pop();
            OnExitScope(scope);
        }

        public override void EnterSetter(SetterContext context)
        {
            scopeStack.Push(new Dictionary<string, T>());
            variableScopeStack.Push(new Dictionary<string, VariableInfo>());
            OnEnterScope();
        }

        public override void ExitSetter(SetterContext context)
        {
            var scope = scopeStack.Pop();
            var varScope = variableScopeStack.Pop();
            OnExitScope(scope);
        }

        public override void Reset()
        {
            while (scopeStack.Count > 0)
            {
                var dict = scopeStack.Pop();
                dict.Clear();
            }

            while (variableScopeStack.Count > 0)
            {
                var dict = variableScopeStack.Pop();
                dict.Clear();
            }

            scopeStack.Push(new Dictionary<string, T>());
            variableScopeStack.Push(new Dictionary<string, VariableInfo>());
        }

        // Virtual methods for variable tracking that subclasses can override
        protected virtual void OnVariableDeclared(VariableInfo varInfo) { }
        protected virtual void OnVariableUsed(VariableInfo varInfo) { }

        // Protected virtual methods for derived classes to handle scope changes
        protected virtual void OnEnterScope() { }
        protected virtual void OnExitScope(Dictionary<string, T> scope) { }
    }
}
