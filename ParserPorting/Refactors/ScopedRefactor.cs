using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted;
using AppRefiner.Services;

namespace ParserPorting.Refactors
{
    /// <summary>
    /// Represents scope information for tracking variables, methods, and context
    /// </summary>
    public class ScopeInfo : IDisposable
    {
        /// <summary>
        /// Variables defined in this scope
        /// </summary>
        public Dictionary<string, List<VariableReference>> Variables { get; } = new();

        /// <summary>
        /// Methods defined in this scope
        /// </summary>
        public Dictionary<string, List<MethodReference>> Methods { get; } = new();

        /// <summary>
        /// The AST node that defines this scope
        /// </summary>
        public AstNode? ScopeNode { get; }

        /// <summary>
        /// The type of scope this represents
        /// </summary>
        public ScopeType Type { get; }

        /// <summary>
        /// Parent scope (null for global scope)
        /// </summary>
        public ScopeInfo? Parent { get; }

        public ScopeInfo(AstNode? scopeNode, ScopeType type, ScopeInfo? parent = null)
        {
            ScopeNode = scopeNode;
            Type = type;
            Parent = parent;
        }

        /// <summary>
        /// Adds a variable reference to this scope
        /// </summary>
        public void AddVariable(string name, SourceSpan location, VariableReferenceType referenceType)
        {
            if (!Variables.ContainsKey(name))
            {
                Variables[name] = new List<VariableReference>();
            }
            Variables[name].Add(new VariableReference(name, location, referenceType));
        }

        /// <summary>
        /// Adds a method reference to this scope
        /// </summary>
        public void AddMethod(string name, SourceSpan location, MethodReferenceType referenceType)
        {
            if (!Methods.ContainsKey(name))
            {
                Methods[name] = new List<MethodReference>();
            }
            Methods[name].Add(new MethodReference(name, location, referenceType));
        }

        /// <summary>
        /// Finds a variable in this scope or parent scopes
        /// </summary>
        public List<VariableReference>? FindVariable(string name)
        {
            if (Variables.TryGetValue(name, out var references))
            {
                return references;
            }
            return Parent?.FindVariable(name);
        }

        /// <summary>
        /// Finds a method in this scope or parent scopes
        /// </summary>
        public List<MethodReference>? FindMethod(string name)
        {
            if (Methods.TryGetValue(name, out var references))
            {
                return references;
            }
            return Parent?.FindMethod(name);
        }

        public void Dispose()
        {
            // Cleanup logic if needed
        }
    }

    /// <summary>
    /// Types of scopes in PeopleCode
    /// </summary>
    public enum ScopeType
    {
        Global,
        Class,
        Method,
        Function,
        Property,
        Block
    }

    /// <summary>
    /// Represents a reference to a variable
    /// </summary>
    public class VariableReference
    {
        public string Name { get; }
        public SourceSpan Location { get; }
        public VariableReferenceType Type { get; }

        public VariableReference(string name, SourceSpan location, VariableReferenceType type)
        {
            Name = name;
            Location = location;
            Type = type;
        }
    }

    /// <summary>
    /// Types of variable references
    /// </summary>
    public enum VariableReferenceType
    {
        Declaration,
        Assignment,
        Usage
    }

    /// <summary>
    /// Represents a reference to a method
    /// </summary>
    public class MethodReference
    {
        public string Name { get; }
        public SourceSpan Location { get; }
        public MethodReferenceType Type { get; }

        public MethodReference(string name, SourceSpan location, MethodReferenceType type)
        {
            Name = name;
            Location = location;
            Type = type;
        }
    }

    /// <summary>
    /// Types of method references
    /// </summary>
    public enum MethodReferenceType
    {
        Declaration,
        Implementation,
        Call
    }

    /// <summary>
    /// Enhanced base class for refactors that need scope-aware processing
    /// </summary>
    public abstract class ScopedRefactor : BaseRefactor
    {
        /// <summary>
        /// Stack of active scopes during AST traversal
        /// </summary>
        private readonly Stack<ScopeInfo> scopeStack = new();

        /// <summary>
        /// Gets the current active scope
        /// </summary>
        protected ScopeInfo? CurrentScope => scopeStack.Count > 0 ? scopeStack.Peek() : null;

        /// <summary>
        /// Gets the global scope (first scope pushed)
        /// </summary>
        protected ScopeInfo? GlobalScope { get; private set; }

        protected ScopedRefactor(AppRefiner.ScintillaEditor editor) : base(editor)
        {
        }

        /// <summary>
        /// Enters a new scope and returns a disposable that will exit the scope
        /// </summary>
        protected ScopeInfo EnterScope(AstNode? scopeNode, ScopeType scopeType)
        {
            var parentScope = CurrentScope;
            var newScope = new ScopeInfo(scopeNode, scopeType, parentScope);
            
            scopeStack.Push(newScope);
            
            if (GlobalScope == null)
            {
                GlobalScope = newScope;
            }

            OnEnterScope(newScope);
            return newScope;
        }

        /// <summary>
        /// Exits the current scope
        /// </summary>
        protected void ExitScope()
        {
            if (scopeStack.Count > 0)
            {
                var exitingScope = scopeStack.Pop();
                OnExitScope(exitingScope);
                exitingScope.Dispose();
            }
        }

        /// <summary>
        /// Called when entering a new scope (override for custom behavior)
        /// </summary>
        protected virtual void OnEnterScope(ScopeInfo scope)
        {
        }

        /// <summary>
        /// Called when exiting a scope (override for custom behavior)
        /// </summary>
        protected virtual void OnExitScope(ScopeInfo scope)
        {
        }

        /// <summary>
        /// Adds a variable reference to the current scope
        /// </summary>
        protected void TrackVariable(string name, SourceSpan location, VariableReferenceType referenceType)
        {
            CurrentScope?.AddVariable(name, location, referenceType);
        }

        /// <summary>
        /// Adds a method reference to the current scope
        /// </summary>
        protected void TrackMethod(string name, SourceSpan location, MethodReferenceType referenceType)
        {
            CurrentScope?.AddMethod(name, location, referenceType);
        }

        /// <summary>
        /// Finds all references to a variable in current scope chain
        /// </summary>
        protected List<VariableReference>? FindVariableReferences(string name)
        {
            return CurrentScope?.FindVariable(name);
        }

        /// <summary>
        /// Finds all references to a method in current scope chain
        /// </summary>
        protected List<MethodReference>? FindMethodReferences(string name)
        {
            return CurrentScope?.FindMethod(name);
        }

        /// <summary>
        /// Checks if a position is within the current scope
        /// </summary>
        protected bool IsInCurrentScope(int position)
        {
            if (CurrentScope?.ScopeNode?.SourceSpan.IsValid == true)
            {
                var span = CurrentScope.ScopeNode.SourceSpan;
                return position >= span.Start.Index && position <= span.End.Index;
            }
            return false;
        }

        /// <summary>
        /// Gets all scopes that contain the given position
        /// </summary>
        protected IEnumerable<ScopeInfo> GetScopesContaining(int position)
        {
            var scopes = new List<ScopeInfo>();
            var current = CurrentScope;
            
            while (current != null)
            {
                if (current.ScopeNode?.SourceSpan.IsValid == true)
                {
                    var span = current.ScopeNode.SourceSpan;
                    if (position >= span.Start.Index && position <= span.End.Index)
                    {
                        scopes.Add(current);
                    }
                }
                current = current.Parent;
            }
            
            return scopes;
        }

        // Override visitor methods to handle scope management

        public override void VisitProgram(ProgramNode node)
        {
            var scope = EnterScope(node, ScopeType.Global);
            try
            {
                base.VisitProgram(node);
            }
            finally
            {
                ExitScope();
            }
        }

        public override void VisitAppClass(AppClassNode node)
        {
            var scope = EnterScope(node, ScopeType.Class);
            try
            {
                base.VisitAppClass(node);
            }
            finally
            {
                ExitScope();
            }
        }

        public override void VisitMethod(MethodNode node)
        {
            // Track method declaration
            if (node.SourceSpan.IsValid)
            {
                TrackMethod(node.Name, node.SourceSpan, MethodReferenceType.Declaration);
            }

            var scope = EnterScope(node, ScopeType.Method);
            try
            {
                // Track method parameters as variables
                foreach (var parameter in node.Parameters)
                {
                    if (parameter.SourceSpan.IsValid)
                    {
                        TrackVariable(parameter.Name, parameter.SourceSpan, VariableReferenceType.Declaration);
                    }
                }

                base.VisitMethod(node);
            }
            finally
            {
                ExitScope();
            }
        }

        public override void VisitFunction(FunctionNode node)
        {
            // Track function declaration
            if (node.SourceSpan.IsValid)
            {
                TrackMethod(node.Name, node.SourceSpan, MethodReferenceType.Declaration);
            }

            var scope = EnterScope(node, ScopeType.Function);
            try
            {
                // Track function parameters as variables
                foreach (var parameter in node.Parameters)
                {
                    if (parameter.SourceSpan.IsValid)
                    {
                        TrackVariable(parameter.Name, parameter.SourceSpan, VariableReferenceType.Declaration);
                    }
                }

                base.VisitFunction(node);
            }
            finally
            {
                ExitScope();
            }
        }

        public override void VisitProperty(PropertyNode node)
        {
            var scope = EnterScope(node, ScopeType.Property);
            try
            {
                base.VisitProperty(node);
            }
            finally
            {
                ExitScope();
            }
        }

        public override void VisitBlock(BlockNode node)
        {
            var scope = EnterScope(node, ScopeType.Block);
            try
            {
                base.VisitBlock(node);
            }
            finally
            {
                ExitScope();
            }
        }

        public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
        {
            // Track variable declarations
            if (node.VariableNameInfos != null)
            {
                foreach (var varInfo in node.VariableNameInfos)
                {
                    if (varInfo.SourceSpan.IsValid)
                    {
                        TrackVariable(varInfo.Name, varInfo.SourceSpan, VariableReferenceType.Declaration);
                    }
                }
            }

            base.VisitLocalVariableDeclaration(node);
        }

        public override void VisitIdentifier(IdentifierNode node)
        {
            // Track variable usage
            if (node.SourceSpan.IsValid)
            {
                TrackVariable(node.Name, node.SourceSpan, VariableReferenceType.Usage);
            }

            base.VisitIdentifier(node);
        }

        public override void VisitMethodCall(MethodCallNode node)
        {
            // Track method calls
            if (node.SourceSpan.IsValid)
            {
                TrackMethod(node.MethodName, node.SourceSpan, MethodReferenceType.Call);
            }

            base.VisitMethodCall(node);
        }

        public override void VisitAssignment(AssignmentNode node)
        {
            // Track variable assignments
            if (node.Target is IdentifierNode targetId && targetId.SourceSpan.IsValid)
            {
                TrackVariable(targetId.Name, targetId.SourceSpan, VariableReferenceType.Assignment);
            }

            base.VisitAssignment(node);
        }
    }
}