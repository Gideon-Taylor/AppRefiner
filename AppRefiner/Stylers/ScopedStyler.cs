using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.Linters.Models;
using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public abstract class ScopedStyler<T> : BaseStyler
    {
        protected readonly Stack<Dictionary<string, T>> scopeStack = new();
        protected readonly Stack<Dictionary<string, VariableInfo>> variableScopeStack = new();

        protected ScopedStyler()
        {
            // Start with a global scope
            scopeStack.Push(new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase));
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.InvariantCultureIgnoreCase));
        }

        protected void AddLocalVariable(string name, string type, int line, (int Start, int Stop) span)
        {
            var currentScope = variableScopeStack.Peek();
            if (!currentScope.ContainsKey(name))
            {
                currentScope[name] = new VariableInfo(name, type, line, span);
            }
        }

        // Helper method for token-based variable addition with automatic byte conversion
        protected void AddLocalVariable(string name, string type, int line, IToken token)
        {
            AddLocalVariable(name, type, line,
                (token.ByteStartIndex(), token.ByteStopIndex())
            );
        }

        // Helper method for adding indicators using VariableInfo with byte spans
        protected void AddScopedIndicator(VariableInfo varInfo, IndicatorType type, uint color, string? tooltip = null, List<(Type RefactorClass, string Description)>? quickFixes = null)
        {
            Indicators?.Add(new Indicator
            {
                Start = varInfo.Span.Start,
                Length = varInfo.Span.Stop - varInfo.Span.Start + 1,
                Type = type,
                Color = color,
                Tooltip = tooltip,
                QuickFixes = quickFixes ?? new List<(Type RefactorClass, string Description)>()
            });
        }


        // Helper method for adding indicators using context with automatic byte conversion
        protected void AddScopedIndicator(ParserRuleContext context, IndicatorType type, uint color, string? tooltip = null, List<(Type RefactorClass, string Description)>? quickFixes = null)
        {
            var endToken = (context.Stop ?? context.Start);
            Indicators?.Add(new Indicator
            {
                Start = context.Start.ByteStartIndex(),
                Length = endToken.ByteStopIndex() - context.Start.ByteStartIndex() + 1,
                Type = type,
                Color = color,
                Tooltip = tooltip,
                QuickFixes = quickFixes ?? new List<(Type RefactorClass, string Description)>()
            });
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
            if (variableScopeStack.Count == 0) return [];
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

        // Scope management methods
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


        public override void EnterNonLocalVarDeclaration([NotNull] NonLocalVarDeclarationContext context)
        {
            base.EnterNonLocalVarDeclaration(context);

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

        // Parser overrides for scope management
        public override void EnterMethod(MethodContext context)
        {
            scopeStack.Push(new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase));
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.InvariantCultureIgnoreCase));
            OnEnterScope();
        }

        public override void ExitMethod(MethodContext context)
        {
            var scope = scopeStack.Pop();
            var varScope = variableScopeStack.Pop();
            OnExitScope(scope, varScope);
        }

        public override void EnterFunctionDefinition(FunctionDefinitionContext context)
        {
            scopeStack.Push(new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase));
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.InvariantCultureIgnoreCase));
            OnEnterScope();
        }

        public override void ExitFunctionDefinition(FunctionDefinitionContext context)
        {
            var scope = scopeStack.Pop();
            var varScope = variableScopeStack.Pop();
            OnExitScope(scope, varScope);
        }

        public override void EnterGetter(GetterContext context)
        {
            scopeStack.Push(new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase));
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.InvariantCultureIgnoreCase));
            OnEnterScope();
        }

        public override void ExitGetter(GetterContext context)
        {
            var scope = scopeStack.Pop();
            var varScope = variableScopeStack.Pop();
            OnExitScope(scope, varScope);
        }

        public override void EnterSetter(SetterContext context)
        {
            scopeStack.Push(new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase));
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.InvariantCultureIgnoreCase));
            OnEnterScope();
        }

        public override void ExitSetter(SetterContext context)
        {
            var scope = scopeStack.Pop();
            var varScope = variableScopeStack.Pop();
            OnExitScope(scope, varScope);
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

            scopeStack.Push(new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase));
            variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.InvariantCultureIgnoreCase));
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

        // Virtual methods for derived classes to handle scope and variable changes
        protected virtual void OnVariableDeclared(VariableInfo varInfo) { }
        protected virtual void OnVariableUsed(VariableInfo varInfo) { }
        protected virtual void OnEnterScope() { }
        protected virtual void OnExitScope(Dictionary<string, T> scope, Dictionary<string, VariableInfo> variableScope) { }
    }
}
