using Antlr4.Runtime;
using AppRefiner.Linters.Models;
using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Base class for lint rules that need to track variables and other elements within code scopes.
    /// Works with LinterSuppressionListener to respect scope-based suppression directives.
    /// </summary>
    /// <typeparam name="T">The type of data to track in scopes</typeparam>
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
        protected void AddLocalVariable(string name, string type, int line, (int Start, int Stop) span)
        {
            var currentScope = variableScopeStack.Peek();
            if (!currentScope.ContainsKey(name))
            {
                currentScope[name] = new VariableInfo(name, type, line, span);
            }
        }

        // Helper method for context-based variable addition with automatic byte conversion
        protected void AddLocalVariable(string name, string type, int line, ParserRuleContext context)
        {
            AddLocalVariable(name, type, line, 
                (context.Start.ByteStartIndex(), (context.Stop ?? context.Start).ByteStopIndex())
            );
        }

        // Helper method for token-based variable addition with automatic byte conversion
        protected void AddLocalVariable(string name, string type, int line, IToken token)
        {
            AddLocalVariable(name, type, line,
                (token.ByteStartIndex(), token.ByteStopIndex())
            );
        }

        // Helper method for adding reports using VariableInfo with byte spans
        protected void AddScopedReport(int reportNumber, string message, ReportType type, int line, VariableInfo varInfo)
        {
            AddReport(reportNumber, message, type, line, varInfo.Span);
        }

        // Helper method for adding reports using character span with automatic byte conversion
        protected void AddScopedReport(int reportNumber, string message, ReportType type, int line, (int Start, int Stop) span)
        {
            AddReport(reportNumber, message, type, line, span);
        }

        // Helper method for adding reports using context with automatic byte conversion
        protected void AddScopedReport(int reportNumber, string message, ReportType type, int line, ParserRuleContext context)
        {
            AddReport(reportNumber, message, type, line, (context.Start.ByteStartIndex(), (context.Stop ?? context.Start).ByteStopIndex()));
        }

        // Helper method for adding reports using token pair with automatic byte conversion
        protected void AddScopedReport(int reportNumber, string message, ReportType type, int line, IToken startToken, IToken stopToken)
        {
            AddReport(reportNumber, message, type, line, (startToken.ByteStartIndex(), stopToken.ByteStopIndex()));
        }

        // Helper method for adding reports using single token with automatic byte conversion
        protected void AddScopedReport(int reportNumber, string message, ReportType type, int line, IToken token)
        {
            AddReport(reportNumber, message, type, line, (token.ByteStartIndex(), token.ByteStopIndex()));
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
                    varNode.Symbol
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
                varNode.Symbol
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

        /// <summary>
        /// Override of the OnExitScope method with additional variable scope information.
        /// Provided for backwards compatibility.
        /// </summary>
        protected virtual void OnExitScope(Dictionary<string, T> scope, Dictionary<string, VariableInfo> variableScope)
        {
            OnExitScope(scope);
        }
    }
}
