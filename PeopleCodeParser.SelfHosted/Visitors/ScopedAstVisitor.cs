using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeParser.SelfHosted.Visitors.Utilities;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// Enhanced AST visitor that provides comprehensive scope management and variable tracking
/// with proper lifecycle management and extensive query capabilities.
/// </summary>
/// <typeparam name="T">Type of custom data to track in each scope</typeparam>
public abstract class ScopedAstVisitor<T> : AstVisitorBase
{
    #region Private Fields

    /// <summary>
    /// Stack for managing scope hierarchy
    /// </summary>
    private readonly Stack<ScopeContext> scopeStack = new();

    /// <summary>
    /// Stack for custom scope-specific data
    /// </summary>
    private readonly Stack<Dictionary<string, T>> customScopeData = new();

    /// <summary>
    /// Central registry for all variables and scopes
    /// </summary>
    private readonly VariableRegistry variableRegistry = new();

    /// <summary>
    /// Current scope context (top of stack)
    /// </summary>
    private ScopeContext? currentScope;

    /// <summary>
    /// Flag to track if we're currently processing an assignment expression
    /// to prevent duplicate reference tracking
    /// </summary>
    private bool isInAssignmentContext = false;

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the current scope context
    /// There is always a current scope during AST traversal (at minimum, the global scope)
    /// </summary>
    public ScopeContext GetCurrentScope()
    {
        if (currentScope == null)
        {
            throw new InvalidOperationException("No current scope available. Make sure VisitProgram() has been called and is currently executing.");
        }
        return currentScope;
    }

    /// <summary>
    /// Gets the variable registry containing all program variables and scopes
    /// </summary>
    public VariableRegistry VariableRegistry => variableRegistry;

    /// <summary>
    /// Gets all scopes in the program
    /// </summary>
    public IEnumerable<ScopeContext> GetAllScopes() => variableRegistry.AllScopes;

    /// <summary>
    /// Gets all variables in the program
    /// </summary>
    public IEnumerable<VariableInfo> GetAllVariables() => variableRegistry.AllVariables;

    #endregion

    #region Constructor

    protected ScopedAstVisitor()
    {
        // Constructor intentionally empty - initialization happens in VisitProgram
    }

    #endregion

    #region Core Scope Management

    /// <summary>
    /// Enters a new scope with proper lifecycle management
    /// </summary>
    protected void EnterScope(EnhancedScopeType scopeType, string scopeName, AstNode sourceNode)
    {
        var newScope = new ScopeContext(scopeType, scopeName, sourceNode, currentScope);

        // Push onto stack and update current scope
        scopeStack.Push(newScope);
        currentScope = newScope;

        // Register the scope in the variable registry
        variableRegistry.RegisterScope(newScope);

        // Initialize custom data for this scope
        customScopeData.Push(new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase));

        // Call the appropriate OnEnter method based on scope type
        CallOnEnterScopeMethod(newScope, sourceNode);
    }

    /// <summary>
    /// Exits the current scope with proper lifecycle management
    /// CRITICAL: OnExit is called BEFORE popping the scope to ensure GetCurrentScope() works correctly
    /// </summary>
    protected void ExitScope()
    {
        if (scopeStack.Count == 0)
            return;

        var exitingScope = scopeStack.Peek();
        var customData = customScopeData.Count > 0 ? customScopeData.Peek() : new Dictionary<string, T>();

        // CRITICAL: Call OnExit BEFORE popping so GetCurrentScope() returns the correct scope
        CallOnExitScopeMethod(exitingScope, exitingScope.SourceNode, customData);

        // Now pop the scope from the stack
        scopeStack.Pop();
        customScopeData.Pop();

        // Update current scope reference
        currentScope = scopeStack.Count > 0 ? scopeStack.Peek() : null;
    }

    /// <summary>
    /// Calls the appropriate OnEnter method based on scope type
    /// </summary>
    private void CallOnEnterScopeMethod(ScopeContext scope, AstNode sourceNode)
    {
        switch (scope.Type)
        {
            case EnhancedScopeType.Global:
                OnEnterGlobalScope(scope, (ProgramNode)sourceNode);
                break;
            case EnhancedScopeType.Method:
                OnEnterMethodScope(scope, (MethodNode)sourceNode);
                break;
            case EnhancedScopeType.Function:
                OnEnterFunctionScope(scope, (FunctionNode)sourceNode);
                break;
            case EnhancedScopeType.PropertyGetter:
                OnEnterPropertyGetterScope(scope, (PropertyNode)sourceNode);
                break;
            case EnhancedScopeType.PropertySetter:
                OnEnterPropertySetterScope(scope, (PropertyNode)sourceNode);
                break;
            case EnhancedScopeType.Class:
                OnEnterClassScope(scope, (AppClassNode)sourceNode);
                break;
        }
    }

    /// <summary>
    /// Calls the appropriate OnExit method based on scope type
    /// </summary>
    private void CallOnExitScopeMethod(ScopeContext scope, AstNode sourceNode, Dictionary<string, T> customData)
    {
        switch (scope.Type)
        {
            case EnhancedScopeType.Global:
                OnExitGlobalScope(scope, (ProgramNode)sourceNode, customData);
                break;
            case EnhancedScopeType.Method:
                OnExitMethodScope(scope, (MethodNode)sourceNode, customData);
                break;
            case EnhancedScopeType.Function:
                OnExitFunctionScope(scope, (FunctionNode)sourceNode, customData);
                break;
            case EnhancedScopeType.PropertyGetter:
                OnExitPropertyGetterScope(scope, (PropertyNode)sourceNode, customData);
                break;
            case EnhancedScopeType.PropertySetter:
                OnExitPropertySetterScope(scope, (PropertyNode)sourceNode, customData);
                break;
            case EnhancedScopeType.Class:
                OnExitClassScope(scope, (AppClassNode)sourceNode, customData);
                break;
        }
    }

    #endregion

    #region Custom Data Management

    /// <summary>
    /// Gets the custom data dictionary for the current scope
    /// </summary>
    protected Dictionary<string, T> GetCurrentCustomData()
    {
        return customScopeData.Count > 0 ? customScopeData.Peek() : new Dictionary<string, T>();
    }

    /// <summary>
    /// Adds custom data to the current scope
    /// </summary>
    protected void AddToCurrentScope(string key, T value)
    {
        var currentData = GetCurrentCustomData();
        currentData[key] = value;
    }

    /// <summary>
    /// Tries to find custom data by key in any accessible scope
    /// </summary>
    protected bool TryFindInScopes(string key, out T? value)
    {
        value = default;
        foreach (var scopeData in customScopeData)
        {
            if (scopeData.TryGetValue(key, out value))
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Variable Management

    /// <summary>
    /// Registers a variable in the current scope
    /// </summary>
    protected void RegisterVariable(VariableNameInfo variableNameInfo, string typeName, VariableKind variableKind, AstNode declaringNode)
    {
        var current = GetCurrentScope(); // Will throw if no current scope
        var variable = new VariableInfo(variableNameInfo, typeName, variableKind, current, declaringNode);
        variableRegistry.RegisterVariable(variable);

        // Call event handler
        OnVariableDeclared(variable);
    }

    /// <summary>
    /// Adds a reference to a variable
    /// </summary>
    protected void AddVariableReference(string variableName, SourceSpan location, ReferenceType referenceType, string? context = null)
    {
        var current = GetCurrentScope(); // Will throw if no current scope
        var reference = new VariableReference(variableName, referenceType, location, current, context: context);
        variableRegistry.AddVariableReference(variableName, current, reference);

        // Call event handler
        OnVariableReferenced(variableName, reference);
    }

    /// <summary>
    /// Finds a variable by name that is accessible from the current scope
    /// </summary>
    protected VariableInfo? FindVariable(string name)
    {
        return variableRegistry.FindVariableInScope(name, GetCurrentScope());
    }

    #endregion

    #region Query API

    /// <summary>
    /// Gets all variables accessible from the specified scope (including parent scopes)
    /// </summary>
    public IEnumerable<VariableInfo> GetAccessibleVariables(ScopeContext scope)
    {
        return variableRegistry.GetAccessibleVariables(scope);
    }

    /// <summary>
    /// Gets all variables declared directly in the specified scope (local to that scope only)
    /// For variables accessible from a scope (including parent scopes), use GetAccessibleVariables() instead
    /// </summary>
    public IEnumerable<VariableInfo> GetVariablesDeclaredInScope(ScopeContext scope)
    {
        return variableRegistry.GetVariablesInScope(scope);
    }

    /// <summary>
    /// Gets all variables accessible from the specified scope (includes variables from parent scopes)
    /// This is usually what you want when analyzing what variables a scope can use
    /// </summary>
    public IEnumerable<VariableInfo> GetVariablesInScope(ScopeContext scope)
    {
        return GetAccessibleVariables(scope);
    }

    /// <summary>
    /// Gets all references to a variable by name in the specified scope
    /// </summary>
    public IEnumerable<VariableReference> GetVariableReferences(string variableName, ScopeContext scope)
    {
        var variable = variableRegistry.FindVariableInScope(variableName, scope);
        return variable?.References ?? Enumerable.Empty<VariableReference>();
    }

    /// <summary>
    /// Checks if a variable is safe to refactor (rename) within the current program
    /// </summary>
    public bool IsVariableSafeToRefactor(string variableName, ScopeContext scope)
    {
        var variable = variableRegistry.FindVariableInScope(variableName, scope);
        return variable?.IsSafeToRefactor ?? false;
    }

    /// <summary>
    /// Gets all unused variables across the entire program
    /// </summary>
    public IEnumerable<VariableInfo> GetUnusedVariables()
    {
        return variableRegistry.GetUnusedVariables();
    }

    #endregion

    #region AST Visitor Overrides

    /// <summary>
    /// Visits the program node and establishes the global scope
    /// </summary>
    public override void VisitProgram(ProgramNode node)
    {
        // Initialize global scope
        EnterScope(EnhancedScopeType.Global, "Global", node);

        try
        {
            // Visit all program contents
            base.VisitProgram(node);
        }
        finally
        {
            // Exit global scope
            ExitScope();
        }
    }

    /// <summary>
    /// Visits an app class node and manages class scope
    /// </summary>
    public override void VisitAppClass(AppClassNode node)
    {
        EnterScope(EnhancedScopeType.Class, node.Name, node);

        try
        {
            base.VisitAppClass(node);
        }
        finally
        {
            ExitScope();
        }
    }

    /// <summary>
    /// Visits a method node and manages method scope
    /// </summary>
    public override void VisitMethod(MethodNode node)
    {
        EnterScope(EnhancedScopeType.Method, node.Name, node);

        try
        {
            // Register method parameters
            foreach (var parameter in node.Parameters)
            {
                var typeName = AstTypeExtractor.GetTypeFromNode(parameter.Type);
                var nameInfo = new VariableNameInfo(parameter.Name, parameter.NameToken);
                RegisterVariable(nameInfo, typeName, VariableKind.Parameter, parameter);
            }

            // Track parameter annotations as references
            if (node.Implementation != null)
            {
                foreach (var annotation in node.Implementation.ParameterAnnotations)
                {
                    AddVariableReference(annotation.Name, annotation.NameToken.SourceSpan, ReferenceType.ParameterAnnotation, "method parameter annotation");
                }
            }
            base.VisitMethod(node);
        }
        finally
        {
            ExitScope();
        }
    }



    /// <summary>
    /// Visits a function node and manages function scope (only for implementations)
    /// </summary>
    public override void VisitFunction(FunctionNode node)
    {
        // Only create scopes for function implementations, not declarations
        if (!node.IsImplementation)
        {
            // For function declarations, just visit without creating a scope
            base.VisitFunction(node);
            return;
        }

        EnterScope(EnhancedScopeType.Function, node.Name, node);

        try
        {
            // Register function parameters
            foreach (var parameter in node.Parameters)
            {
                var typeName = AstTypeExtractor.GetTypeFromNode(parameter.Type);
                var nameInfo = new VariableNameInfo(parameter.Name, parameter.FirstToken);
                RegisterVariable(nameInfo, typeName, VariableKind.Parameter, parameter);
            }

            base.VisitFunction(node);
        }
        finally
        {
            ExitScope();
        }
    }

    /// <summary>
    /// Visits a property node and manages property scope with separate getter/setter scopes
    /// </summary>
    public override void VisitProperty(PropertyNode node)
    {
        // Register property in parent scope first
        var nameInfo = new VariableNameInfo(node.Name, node.NameToken);
        RegisterVariable(nameInfo, node.Type.TypeName, VariableKind.Property, node);

        // Handle getter scope if getter exists (explicit implementation)
        if (node.GetterImplementation != null)
        {
            var getterScopeName = $"{node.Name}_Get";
            EnterScope(EnhancedScopeType.PropertyGetter, getterScopeName, node);

            try
            {
                // Register getter parameter annotations as references
                foreach (var annotation in node.GetterImplementation.ParameterAnnotations)
                {
                    AddVariableReference(annotation.Name, annotation.NameToken.SourceSpan, ReferenceType.ParameterAnnotation, "property getter parameter annotation");
                }

                // Visit the getter implementation
                node.GetterImplementation.Accept(this);
            }
            finally
            {
                ExitScope();
            }
        }

        // Handle setter scope if setter exists (explicit implementation)
        if (node.SetterImplementation != null)
        {
            var setterScopeName = $"{node.Name}_Set";
            EnterScope(EnhancedScopeType.PropertySetter, setterScopeName, node);

            try
            {
                // Register the implicit &NewValue parameter for setter
                RegisterImplicitNewValueParameter(node);

                // Register setter parameter annotations as references
                foreach (var annotation in node.SetterImplementation.ParameterAnnotations)
                {
                    AddVariableReference(annotation.Name, annotation.NameToken.SourceSpan, ReferenceType.ParameterAnnotation, "property setter parameter annotation");
                }

                // Visit the setter implementation
                node.SetterImplementation.Accept(this);
            }
            finally
            {
                ExitScope();
            }
        }
    }
    /// <summary>
    /// Visits a program variable node and registers it in the appropriate scope
    /// </summary>
    public override void VisitProgramVariable(ProgramVariableNode node)
    {
        var typeName = node.Type.ToString();
        var variableKind = GetVariableKindFromScope(node.Scope);

        // Register all variable names
        foreach (var nameInfo in node.NameInfos)
        {
            RegisterVariable(nameInfo, typeName, variableKind, node);
        }

        base.VisitProgramVariable(node);
    }

    /// <summary>
    /// Visits a local variable declaration and registers it in the current scope
    /// </summary>
    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        var typeName = AstTypeExtractor.GetTypeFromNode(node.Type);

        for (int i = 0; i < node.VariableNames.Count && i < node.VariableNameInfos.Count; i++)
        {
            RegisterVariable(node.VariableNameInfos[i], typeName, VariableKind.Local, node);
        }

        base.VisitLocalVariableDeclaration(node);
    }

    /// <summary>
    /// Visits a local variable declaration with assignment
    /// </summary>
    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        var typeName = AstTypeExtractor.GetTypeFromNode(node.Type);
        RegisterVariable(node.VariableNameInfo, typeName, VariableKind.Local, node);

        base.VisitLocalVariableDeclarationWithAssignment(node);
    }

    /// <summary>
    /// Visits a constant declaration
    /// </summary>
    public override void VisitConstant(ConstantNode node)
    {
        var typeName = AstTypeExtractor.GetDefaultTypeForExpression(node.Value);
        var nameInfo = new VariableNameInfo(node.Name, node.FirstToken);
        RegisterVariable(nameInfo, $"Constant({typeName})", VariableKind.Constant, node);

        base.VisitConstant(node);
    }

    /// <summary>
    /// Visits an identifier node - reference tracking now handled by assignment-aware visitors
    /// This method only handles identifiers that appear outside of assignment contexts
    /// </summary>
    public override void VisitIdentifier(IdentifierNode node)
    {
        // Only add reference if this identifier is not part of an assignment expression
        // Assignment expressions are handled by VisitAssignment which provides proper context
        if (!isInAssignmentContext)
        {
            AddVariableReference(node.Name, node.SourceSpan, ReferenceType.Read, "identifier reference");

            // Handle property access with & prefix
            if (node.Name.StartsWith("&"))
            {
                var propertyName = node.Name.Substring(1);
                AddVariableReference(propertyName, node.SourceSpan, ReferenceType.Read, "property reference");
            }
        }

        base.VisitIdentifier(node);
    }

    /// <summary>
    /// Visits a FOR statement and tracks iterator variable usage
    /// Only tracks user variables (&var), not RECORD.FIELD (which are record buffer accesses)
    /// </summary>
    public override void VisitFor(ForStatementNode node)
    {
        // Only track if the iterator is a user variable
        // RECORD.FIELD accesses are not variables and should not be tracked
        if (node.Iterator is IdentifierNode identifier && identifier.IdentifierType == IdentifierType.UserVariable)
        {
            AddVariableReference(identifier.Name, node.IteratorToken.SourceSpan, ReferenceType.Write, "for loop iterator");
        }

        base.VisitFor(node);
    }

    /// <summary>
    /// Visits function calls and tracks variable references in method calls
    /// </summary>
    public override void VisitFunctionCall(FunctionCallNode node)
    {
        if (node.Function is MemberAccessNode member && member.Target is IdentifierNode ident)
        {
            if (ident.Name.StartsWith('&'))
            {
                AddVariableReference(ident.Name, ident.SourceSpan, ReferenceType.Read, "method call on property");
            }
        }
        base.VisitFunctionCall(node);
    }

    /// <summary>
    /// Visits member access and tracks %THIS references
    /// </summary>
    public override void VisitMemberAccess(MemberAccessNode node)
    {
        if (node.Target is IdentifierNode identNode && identNode.Name.Equals("%THIS", StringComparison.OrdinalIgnoreCase))
        {
            var memberName = node.MemberName;
            AddVariableReference(memberName, node.SourceSpan, ReferenceType.Read, "%THIS member access");

            var varNameWithPrefix = $"&{memberName}";
            AddVariableReference(varNameWithPrefix, node.SourceSpan, ReferenceType.Read, "%THIS property access");
        }

        base.VisitMemberAccess(node);
    }

    /// <summary>
    /// Visits a catch statement and properly handles exception variable scoping
    /// Exception variables are scoped to the containing method/function/global scope, not just the catch block
    /// </summary>
    public override void VisitCatch(CatchStatementNode node)
    {
        // If there's an exception variable, register it in the current scope (method/function/global)
        if (node.ExceptionVariable != null)
        {

            // Create variable name info from the exception variable identifier
            var nameInfo = new VariableNameInfo(node.ExceptionVariable.Name, node.ExceptionVariable.FirstToken);

            // Register the exception variable in the current scope (which is the containing method/function/global scope)
            RegisterVariable(nameInfo, node.ExceptionType?.TypeName ?? "Exception", VariableKind.Exception, node);
        }

        // Continue with the base implementation to visit the catch body
        base.VisitCatch(node);
    }

    /// <summary>
    /// Visits an assignment and properly classifies left-hand side as Write and right-hand side as Read
    /// </summary>
    public override void VisitAssignment(AssignmentNode node)
    {
        // Set flag to prevent duplicate reference tracking from VisitIdentifier
        bool wasInAssignmentContext = isInAssignmentContext;
        isInAssignmentContext = true;

        try
        {
            // Visit the target (left-hand side) and mark identifiers as Write operations
            VisitExpressionAsWrite(node.Target);

            // Visit the value (right-hand side) and mark identifiers as Read operations
            VisitExpressionAsRead(node.Value);
        }
        finally
        {
            // Restore previous context
            isInAssignmentContext = wasInAssignmentContext;
        }
        
        // DO NOT call base.VisitAssignment(node) - we handle the traversal manually above
        // This prevents duplicate visits to the same nodes
    }

    /// <summary>
    /// Visits an expression statement and checks if it contains an assignment
    /// </summary>
    public override void VisitExpressionStatement(ExpressionStatementNode node)
    {
        // Check if the expression is an assignment
        if (node.Expression is AssignmentNode assignment)
        {
            // Handle assignment directly
            VisitAssignment(assignment);
        }
        else
        {
            // Handle other expression statements normally
            base.VisitExpressionStatement(node);
        }
    }


    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts PeopleCode variable scope to VariableKind
    /// </summary>
    private VariableKind GetVariableKindFromScope(VariableScope scope)
    {
        return scope switch
        {
            VariableScope.Instance => VariableKind.Instance,
            VariableScope.Global => VariableKind.Global,
            VariableScope.Component => VariableKind.Component,
            _ => throw new ArgumentException($"Unexpected VariableScope: {scope}", nameof(scope))
        };
    }

    /// <summary>
    /// Resets the visitor to its initial state
    /// </summary>
    public virtual void Reset()
    {
        // Clear all stacks
        scopeStack.Clear();
        customScopeData.Clear();
        variableRegistry.Clear();
        currentScope = null;
        isInAssignmentContext = false;

        OnReset();
    }

    /// <summary>
    /// Registers the implicit &NewValue parameter in property setter scopes
    /// </summary>
    private void RegisterImplicitNewValueParameter(PropertyNode propertyNode)
    {
        // Create a synthetic token for the &NewValue parameter using the setter implementation's location
        var newValueToken = new Token(TokenType.UserVariable, "&NewValue", propertyNode.SetterImplementation!.SourceSpan);
        var newValueNameInfo = new VariableNameInfo("&NewValue", newValueToken);
        
        // Register &NewValue as a parameter with the property's type, using the setter implementation as the declaring node
        RegisterVariable(newValueNameInfo, propertyNode.Type.TypeName, VariableKind.Parameter, propertyNode.SetterImplementation);
    }

    /// <summary>
    /// Visits an expression and marks all identifiers as Write operations
    /// Used for assignment targets (left-hand side)
    /// </summary>
    private void VisitExpressionAsWrite(ExpressionNode expression)
    {
        switch (expression)
        {
            case IdentifierNode identifier:
                AddVariableReference(identifier.Name, identifier.SourceSpan, ReferenceType.Write, "assignment target");
                
                // Handle property access with & prefix
                if (identifier.Name.StartsWith("&"))
                {
                    var propertyName = identifier.Name.Substring(1);
                    AddVariableReference(propertyName, identifier.SourceSpan, ReferenceType.Write, "property assignment");
                }
                // Also allow other visitors to process the member access
                expression.Accept(this);
                break;

            case MemberAccessNode memberAccess:
                // For member access like %This.PropertyName, the target is read, member is written
                VisitExpressionAsRead(memberAccess.Target);
                
                if (memberAccess.Target is IdentifierNode targetIdent && 
                    targetIdent.Name.Equals("%THIS", StringComparison.OrdinalIgnoreCase))
                {
                    // Mark the property as being written to
                    AddVariableReference(memberAccess.MemberName, memberAccess.SourceSpan, ReferenceType.Write, "%THIS property write");
                    
                    var varNameWithPrefix = $"&{memberAccess.MemberName}";
                    AddVariableReference(varNameWithPrefix, memberAccess.SourceSpan, ReferenceType.Write, "%THIS property write");
                }
                // Also allow other visitors to process the member access
                expression.Accept(this);
                break;

            case ArrayAccessNode arrayAccess:
                // For array access like &arr[i], the array is written, index is read
                VisitExpressionAsWrite(arrayAccess.Array);
                foreach (var index in arrayAccess.Indices)
                {
                    VisitExpressionAsRead(index);
                }
                // Also allow other visitors to process the array access
                expression.Accept(this);
                break;

            case PropertyAccessNode propertyAccess:
                // For property access, target is read, property is written
                VisitExpressionAsRead(propertyAccess.Target);
                // Also allow other visitors to process the property access
                expression.Accept(this);
                break;

            default:
                // For other expressions, visit normally (as read context)
                VisitExpressionAsRead(expression);
                // Also allow other visitors to process the member access
                expression.Accept(this);
                break;
        }
    }

    /// <summary>
    /// Visits an expression and marks all identifiers as Read operations
    /// Used for assignment values (right-hand side) and other read contexts
    /// </summary>
    private void VisitExpressionAsRead(ExpressionNode expression)
    {
        switch (expression)
        {
            case IdentifierNode identifier:
                AddVariableReference(identifier.Name, identifier.SourceSpan, ReferenceType.Read, "identifier reference");
                
                // Handle property access with & prefix
                if (identifier.Name.StartsWith("&"))
                {
                    var propertyName = identifier.Name.Substring(1);
                    AddVariableReference(propertyName, identifier.SourceSpan, ReferenceType.Read, "property reference");
                }
                // Also allow other visitors to process the member access
                expression.Accept(this);
                break;

            case MemberAccessNode memberAccess:
                // Visit the target
                VisitExpressionAsRead(memberAccess.Target);
                
                if (memberAccess.Target is IdentifierNode targetIdent && 
                    targetIdent.Name.Equals("%THIS", StringComparison.OrdinalIgnoreCase))
                {
                    // Mark the property as being read
                    AddVariableReference(memberAccess.MemberName, memberAccess.SourceSpan, ReferenceType.Read, "%THIS property read");
                    
                    var varNameWithPrefix = $"&{memberAccess.MemberName}";
                    AddVariableReference(varNameWithPrefix, memberAccess.SourceSpan, ReferenceType.Read, "%THIS property read");
                }
                // Also allow other visitors to process the member access
                expression.Accept(this);
                break;

            case AssignmentNode assignment:
                // Nested assignment - recursively handle
                VisitExpressionAsWrite(assignment.Target);
                VisitExpressionAsRead(assignment.Value);
                // Also allow other visitors to process the assignment
                expression.Accept(this);
                break;

            case BinaryOperationNode binaryOp:
                // Both operands are read
                VisitExpressionAsRead(binaryOp.Left);
                VisitExpressionAsRead(binaryOp.Right);
                // Also allow other visitors to process the binary operation
                expression.Accept(this);
                break;

            case UnaryOperationNode unaryOp:
                // Operand is read
                VisitExpressionAsRead(unaryOp.Operand);
                // Also allow other visitors to process the unary operation
                expression.Accept(this);
                break;

            case FunctionCallNode functionCall:
                // Function and all arguments are read
                VisitExpressionAsRead(functionCall.Function);
                foreach (var arg in functionCall.Arguments)
                {
                    VisitExpressionAsRead(arg);
                }
                // Also allow other visitors to process the function call
                expression.Accept(this);
                break;

            case ArrayAccessNode arrayAccess:
                // Array and indices are read
                VisitExpressionAsRead(arrayAccess.Array);
                foreach (var index in arrayAccess.Indices)
                {
                    VisitExpressionAsRead(index);
                }
                // Also allow other visitors to process the array access
                expression.Accept(this);
                break;

            case PropertyAccessNode propertyAccess:
                // Target is read
                VisitExpressionAsRead(propertyAccess.Target);
                // Also allow other visitors to process the property access
                expression.Accept(this);
                break;

            case ParenthesizedExpressionNode parenthesized:
                // Visit the inner expression
                VisitExpressionAsRead(parenthesized.Expression);
                // Also allow other visitors to process the parenthesized expression
                expression.Accept(this);
                break;

            case TypeCastNode typeCast:
                // Visit the expression being cast
                VisitExpressionAsRead(typeCast.Expression);
                // Also allow other visitors to process the type cast
                expression.Accept(this);
                break;

            case LiteralNode:
                // Literals don't reference variables - no action needed
                // But we still need to allow other visitors (like TypeInferenceVisitor) to process them
                expression.Accept(this);
                break;

            default:
                // For unknown expression types, use the base visitor
                expression.Accept(this);
                break;
        }
    }

    #endregion

    #region Virtual Methods for Subclasses (Event Handlers)

    /// <summary>
    /// Called when entering the global scope
    /// </summary>
    protected virtual void OnEnterGlobalScope(ScopeContext scope, ProgramNode node) { }

    /// <summary>
    /// Called when exiting the global scope (BEFORE scope is popped from stack)
    /// </summary>
    protected virtual void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, T> customData) { }

    /// <summary>
    /// Called when entering a class scope
    /// </summary>
    protected virtual void OnEnterClassScope(ScopeContext scope, AppClassNode node) { }

    /// <summary>
    /// Called when exiting a class scope (BEFORE scope is popped from stack)
    /// </summary>
    protected virtual void OnExitClassScope(ScopeContext scope, AppClassNode node, Dictionary<string, T> customData) { }

    /// <summary>
    /// Called when entering a method scope
    /// </summary>
    protected virtual void OnEnterMethodScope(ScopeContext scope, MethodNode node) { }

    /// <summary>
    /// Called when exiting a method scope (BEFORE scope is popped from stack)
    /// </summary>
    protected virtual void OnExitMethodScope(ScopeContext scope, MethodNode node, Dictionary<string, T> customData) { }

    /// <summary>
    /// Called when entering a function scope
    /// </summary>
    protected virtual void OnEnterFunctionScope(ScopeContext scope, FunctionNode node) { }

    /// <summary>
    /// Called when exiting a function scope (BEFORE scope is popped from stack)
    /// </summary>
    protected virtual void OnExitFunctionScope(ScopeContext scope, FunctionNode node, Dictionary<string, T> customData) { }

    /// <summary>
    /// Called when entering a property getter scope
    /// </summary>
    protected virtual void OnEnterPropertyGetterScope(ScopeContext scope, PropertyNode node) { }

    /// <summary>
    /// Called when exiting a property getter scope (BEFORE scope is popped from stack)
    /// </summary>
    protected virtual void OnExitPropertyGetterScope(ScopeContext scope, PropertyNode node, Dictionary<string, T> customData) { }

    /// <summary>
    /// Called when entering a property setter scope
    /// </summary>
    protected virtual void OnEnterPropertySetterScope(ScopeContext scope, PropertyNode node) { }

    /// <summary>
    /// Called when exiting a property setter scope (BEFORE scope is popped from stack)
    /// </summary>
    protected virtual void OnExitPropertySetterScope(ScopeContext scope, PropertyNode node, Dictionary<string, T> customData) { }

    /// <summary>
    /// Called when a variable is declared in any scope
    /// </summary>
    protected virtual void OnVariableDeclared(VariableInfo variable) { }

    /// <summary>
    /// Called when a variable is referenced
    /// </summary>
    protected virtual void OnVariableReferenced(string variableName, VariableReference reference) { }

    /// <summary>
    /// Called when the visitor is reset
    /// </summary>
    protected virtual void OnReset() { }

    #endregion
}