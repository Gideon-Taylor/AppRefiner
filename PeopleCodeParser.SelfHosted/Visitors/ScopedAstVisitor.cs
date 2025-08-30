using System.Collections;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeParser.SelfHosted.Visitors.Utilities;
using PeopleCodeParser.SelfHosted.Visitors;

namespace PeopleCodeParser.SelfHosted.Visitors;

public enum VariableType
{
    Local,
    Global,
    Instance,
    Property,
    Parameter,
    Constant
}



/// <summary>
/// Base class for AST visitors that need to track variables and other data across different scopes.
/// This class provides the infrastructure for managing scopes and tracking variable declarations.
/// </summary>
/// <typeparam name="T">Type of custom data to track in each scope</typeparam>
public abstract class ScopedAstVisitor<T> : AstVisitorBase
{
    // Stack for custom scope-specific data
    protected readonly Stack<Dictionary<string, T>> customScopeData = new();
    
    // Stack for variable tracking across scopes
    protected readonly Stack<Dictionary<string, VariableInfo>> variableScopeStack = new();
    
    // Stack for scope metadata
    protected readonly Stack<ScopeInfo> scopeInfoStack = new();
    
    /// <summary>
    /// Gets or sets whether this visitor should track variable usage.
    /// When enabled, provides automatic variable usage tracking via the VariableTracker property.
    /// </summary>
    public bool TrackVariableUsage { get; set; } = false;
    
    /// <summary>
    /// Gets the variable usage tracker instance when TrackVariableUsage is enabled.
    /// Returns null when variable usage tracking is disabled.
    /// </summary>
    protected IVariableUsageTracker? VariableTracker { get; private set; }

    protected ScopedAstVisitor()
    {
        // Initialize with global scope
        var globalScopeInfo = new ScopeInfo(ScopeType.Global, "Global");
        scopeInfoStack.Push(globalScopeInfo);
        customScopeData.Push(new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase));
        variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.InvariantCultureIgnoreCase));
        
        // Initialize variable tracker if tracking is enabled
        UpdateVariableTracker();
    }
    
    /// <summary>
    /// Updates the VariableTracker instance based on the TrackVariableUsage setting.
    /// This method should be called after changing the TrackVariableUsage property.
    /// </summary>
    protected void UpdateVariableTracker()
    {
        if (TrackVariableUsage && VariableTracker == null)
        {
            VariableTracker = new VariableUsageTracker();
        }
        else if (!TrackVariableUsage && VariableTracker != null)
        {
            VariableTracker.Reset();
            VariableTracker = null;
        }
    }

    #region Scope Management

    /// <summary>
    /// Gets the custom data dictionary for the current scope
    /// </summary>
    protected Dictionary<string, T> GetCurrentCustomData() => customScopeData.Peek();
    
    /// <summary>
    /// Gets the variable dictionary for the current scope
    /// </summary>
    protected Dictionary<string, VariableInfo> GetCurrentVariableScope() => variableScopeStack.Peek();
    
    /// <summary>
    /// Gets the current scope information
    /// </summary>
    protected ScopeInfo GetCurrentScopeInfo() => scopeInfoStack.Peek();

    /// <summary>
    /// Adds custom data to the current scope
    /// </summary>
    protected void AddToCurrentScope(string key, T value)
    {
        var currentScope = GetCurrentCustomData();
        if (!currentScope.ContainsKey(key))
        {
            currentScope[key] = value;
        }
    }

    /// <summary>
    /// Replaces custom data in the first scope where the key is found
    /// </summary>
    protected void ReplaceInFoundScope(string key, T newValue)
    {
        foreach (var scope in customScopeData)
        {
            if (scope.ContainsKey(key))
            {
                scope[key] = newValue;
                return;
            }
        }
    }

    /// <summary>
    /// Tries to find custom data by key in any accessible scope
    /// </summary>
    protected bool TryFindInScopes(string key, out T? value)
    {
        value = default;
        foreach (var scope in customScopeData)
        {
            if (scope.TryGetValue(key, out value))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Enters a new scope with the specified type and name
    /// </summary>
    protected void EnterScope(ScopeType scopeType, string scopeName)
    {
        var parentScope = GetCurrentScopeInfo();
        var newScopeInfo = new ScopeInfo(scopeType, scopeName, parentScope);
        
        scopeInfoStack.Push(newScopeInfo);
        customScopeData.Push(new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase));
        variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.InvariantCultureIgnoreCase));
        
        OnEnterScope(newScopeInfo);
    }

    /// <summary>
    /// Exits the current scope
    /// </summary>
    protected void ExitScope()
    {
        if (customScopeData.Count > 1 && variableScopeStack.Count > 1 && scopeInfoStack.Count > 1)
        {
            var customData = customScopeData.Peek();
            var varScope = variableScopeStack.Peek();
            var scopeInfo = scopeInfoStack.Peek();

            // Make sure we alert any subclasses before popping the stacks
            OnExitScope(scopeInfo, varScope, customData);

            customScopeData.Pop();
            variableScopeStack.Pop();
            scopeInfoStack.Pop();
        }
    }

    #endregion

    #region Variable Management
    
    /// <summary>
    /// Registers a variable in the current scope using VariableNameInfo to preserve rich source information
    /// </summary>
    protected void RegisterVariable(VariableNameInfo variableNameInfo, string typeName, VariableType variableType = VariableType.Local)
    {
        var currentScope = GetCurrentVariableScope();
        if (!currentScope.ContainsKey(variableNameInfo.Name))
        {
            var scopeInfo = GetCurrentScopeInfo();
            var variableInfo = new VariableInfo(variableNameInfo, typeName, variableType);
            currentScope[variableNameInfo.Name] = variableInfo;
            
            // Register with usage tracker if enabled
            if (VariableTracker != null)
            {
                VariableTracker.RegisterVariable(variableInfo, scopeInfo);
            }
            
            OnVariableDeclared(variableInfo, scopeInfo);
        }
    }

    /// <summary>
    /// Tries to find a variable by name in any accessible scope
    /// </summary>
    protected bool TryFindVariable(string name, out VariableInfo? info)
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
    /// Gets all variables declared in the current scope
    /// </summary>
    protected IEnumerable<VariableInfo> GetVariablesInCurrentScope()
    {
        if (variableScopeStack.Count == 0) return Enumerable.Empty<VariableInfo>();
        return variableScopeStack.Peek().Values;
    }

    /// <summary>
    /// Gets all variables accessible from the current scope (including parent scopes)
    /// </summary>
    protected IEnumerable<VariableInfo> GetAllAccessibleVariables()
    {
        var allVariables = new List<VariableInfo>();
        foreach (var scope in variableScopeStack)
        {
            allVariables.AddRange(scope.Values);
        }
        return allVariables;
    }

    protected IEnumerable<VariableInfo> GetAllVariablesInScope(ScopeInfo scopeInfo)
    {
        var allVariables = new List<VariableInfo>();
        foreach (var scope in variableScopeStack)
        {
            if (scopeInfoStack.Contains(scopeInfo))
            {
                allVariables.AddRange(scope.Values);
            }
        }
        return allVariables;
    }

    #endregion

    #region Variable Usage Tracking

    /// <summary>
    /// Marks a variable as used by name in the current scope or any parent scope.
    /// Only works when TrackVariableUsage is enabled.
    /// </summary>
    protected bool MarkVariableAsUsed(string name)
    {
        if (VariableTracker == null) return false;
        
        var currentScope = GetCurrentScopeInfo();
        var wasMarked = VariableTracker.MarkAsUsed(name, currentScope);
        
        if (wasMarked)
        {
            OnVariableUsed(name, default(SourceSpan), currentScope);
        }
        
        return wasMarked;
    }
    
    /// <summary>
    /// Marks a variable as used by name with location tracking in the current scope or any parent scope.
    /// Only works when TrackVariableUsage is enabled.
    /// </summary>
    protected bool MarkVariableAsUsedWithLocation(string name, SourceSpan location)
    {
        if (VariableTracker == null) return false;
        
        var currentScope = GetCurrentScopeInfo();
        var wasMarked = VariableTracker.MarkAsUsedWithLocation(name, location, currentScope);
        
        if (wasMarked)
        {
            OnVariableUsed(name, location, currentScope);
        }
        
        return wasMarked;
    }
    
    public IEnumerable<SourceSpan> GetVariableReferences(string variableName, ScopeInfo scope)
    {
        if (VariableTracker == null) return Enumerable.Empty<SourceSpan>();
        
        return VariableTracker.GetVariableReferences(variableName, scope);
    }

    /// <summary>
    /// Checks if a variable is defined in any accessible scope.
    /// Only works when TrackVariableUsage is enabled.
    /// </summary>
    protected bool IsVariableDefined(string name)
    {
        if (VariableTracker == null) return false;
        
        var currentScope = GetCurrentScopeInfo();
        return VariableTracker.IsVariableDefined(name, currentScope);
    }
    
    /// <summary>
    /// Tracks a reference to an undefined variable.
    /// Only works when TrackVariableUsage is enabled.
    /// </summary>
    protected void TrackUndefinedReference(string name, SourceSpan location)
    {
        if (VariableTracker == null) return;
        
        var currentScope = GetCurrentScopeInfo();
        VariableTracker.TrackUndefinedReference(name, location, currentScope);
    }

    #endregion

    #region AST Visitor Overrides

    /// <summary>
    /// Visits a variable node and registers it in the appropriate scope
    /// </summary>
    public override void VisitVariable(VariableNode node)
    {
        var variableName = node.Name;
        var typeName = node.Type.ToString();
        var variableType = GetVariableTypeFromScope(node.Scope);
        
        // Add primary variable if it exists
        if (!string.IsNullOrEmpty(variableName))
        {
            var nameInfo = node.NameInfos.FirstOrDefault(ni => 
                ni.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));
            if (nameInfo != null)
            {
                RegisterVariable(nameInfo, typeName, variableType);
            }
        }
        
        // Handle all names in NameInfos
        foreach (var nameInfo in node.NameInfos)
        {
            if (string.IsNullOrEmpty(variableName) || 
                !nameInfo.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase))
            {
                RegisterVariable(nameInfo, typeName, variableType);
            }
        }
        
        base.VisitVariable(node);
    }

    /// <summary>
    /// Handles identifier references and tracks variable usage for the usage tracker
    /// </summary>
    public override void VisitIdentifier(IdentifierNode node)
    {
        // Track the variable usage with location
        MarkVariableAsUsedWithLocation(node.Name, node.SourceSpan);

        // If this is a property accessed with & prefix, also track the property
        if (node.Name.StartsWith("&"))
        {
            var propertyName = node.Name.Substring(1);
            MarkVariableAsUsedWithLocation(propertyName, node.SourceSpan);
        }

        base.VisitIdentifier(node);
    }
    /// <summary>
    /// Handles FOR statements and tracks iterator variable usage
    /// </summary>
    public override void VisitFor(ForStatementNode node)
    {
        string iteratorName = node.Variable;
        MarkVariableAsUsedWithLocation(iteratorName, node.IteratorToken.SourceSpan);
        base.VisitFor(node);
    }

    public override void VisitFunctionCall(FunctionCallNode node)
    {
        if (node.Function is MemberAccessNode member && member.Target is IdentifierNode ident)
        {
            if (ident.Name.StartsWith('&'))
            {
                MarkVariableAsUsedWithLocation(ident.Name, ident.SourceSpan);
            }
        }
        base.VisitFunctionCall(node);
    }

    public override void VisitMemberAccess(MemberAccessNode node)
    {
        var target = node.Target;
        if (target is IdentifierNode identNode && identNode.Name.Equals("%THIS", StringComparison.OrdinalIgnoreCase))
        {
            var memberName = node.MemberName;
            MarkVariableAsUsedWithLocation(memberName, node.SourceSpan);

            string varNameWithPrefix = $"&{memberName}";
            MarkVariableAsUsedWithLocation(varNameWithPrefix, node.SourceSpan);
        }

        base.VisitMemberAccess(node);
    }

    /// <summary>
    /// Visits a method node and manages its scope
    /// </summary>
    public override void VisitMethod(MethodNode node)
    {
        EnterScope(ScopeType.Method, node.Name);

        foreach (var parameter in node.Parameters)
        {
            var typeName = AstTypeExtractor.GetTypeFromNode(parameter.Type);

            VariableNameInfo nameInfo = new(parameter.Name, parameter.NameToken);

            RegisterVariable(nameInfo, typeName, VariableType.Parameter);
        }

        foreach (var annotation in node.ParameterAnnotations)
        {
            MarkVariableAsUsedWithLocation(annotation.Name, annotation.NameToken.SourceSpan);
        }


        base.VisitMethod(node);
        
        ExitScope();
    }

    /// <summary>
    /// Visits a function node and manages its scope
    /// </summary>
    public override void VisitFunction(FunctionNode node)
    {
        EnterScope(ScopeType.Function, node.Name);
        
        AddFunctionParameters(node);
        
        base.VisitFunction(node);
        
        ExitScope();
    }

    /// <summary>
    /// Visits a property node and manages its scope
    /// </summary>
    public override void VisitProperty(PropertyNode node)
    {
        // Add property to the current scope before entering the property's scope
        var propertyName = node.Name;
        var nameInfo = new VariableNameInfo(propertyName, node.NameToken);
        RegisterVariable(nameInfo, "Property", VariableType.Property);
        
        // Enter property scope
        EnterScope(ScopeType.Property, node.Name);
        
        base.VisitProperty(node);
        
        ExitScope();
    }
    
    /// <summary>
    /// Visits an app class node and tracks class context
    /// </summary>
    public override void VisitAppClass(AppClassNode node)
    {
        OnClassEnter(node);
        base.VisitAppClass(node);
        OnClassExit(node);
    }

    /// <summary>
    /// Visits a local variable declaration and registers variables in the current scope
    /// </summary>
    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        var typeName = AstTypeExtractor.GetTypeFromNode(node.Type);
        
        // Use VariableNameInfos for precise positioning when available
        for (int i = 0; i < node.VariableNames.Count; i++)
        {
            var variableName = node.VariableNames[i];
            
            RegisterVariable(node.VariableNameInfos[i], typeName, VariableType.Local);
        }
        
        base.VisitLocalVariableDeclaration(node);
    }

    /// <summary>
    /// Visits a local variable declaration with assignment and registers the variable in the current scope
    /// </summary>
    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        var typeName = AstTypeExtractor.GetTypeFromNode(node.Type);
        
        // Get precise source span for the variable name if available
        var sourceSpan = node.SourceSpan;

        

        RegisterVariable(node.VariableNameInfo, typeName, VariableType.Local);
        
        base.VisitLocalVariableDeclarationWithAssignment(node);
    }

    /// <summary>
    /// Visits a constant declaration and registers it in the current scope
    /// </summary>
    public override void VisitConstant(ConstantNode node)
    {
        var typeName = AstTypeExtractor.GetDefaultTypeForExpression(node.Value);

        VariableNameInfo nameInfo = new(node.Name, node.FirstToken);

        RegisterVariable(nameInfo, $"Constant({typeName})", VariableType.Constant);
        
        base.VisitConstant(node);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Adds function parameters to the current scope
    /// </summary>
    private void AddFunctionParameters(FunctionNode function)
    {
        foreach (var parameter in function.Parameters)
        {
            var typeName = AstTypeExtractor.GetTypeFromNode(parameter.Type);
            var variableNameInfo = new VariableNameInfo(parameter.Name, parameter.FirstToken);
            RegisterVariable(variableNameInfo, typeName, VariableType.Parameter);
        }
    }
    
    /// <summary>
    /// Converts PeopleCode variable scope to our VariableType enum
    /// </summary>
    private VariableType GetVariableTypeFromScope(VariableScope scope)
    {
        return scope switch
        {
            VariableScope.Instance => VariableType.Instance,
            VariableScope.Global => VariableType.Global,
            VariableScope.Local => VariableType.Local,
            _ => VariableType.Local
        };
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Resets the visitor to its initial state with only the global scope
    /// </summary>
    public void Reset()
    {
        while (customScopeData.Count > 1)
        {
            var dict = customScopeData.Pop();
            dict.Clear();
        }

        while (variableScopeStack.Count > 1)
        {
            var dict = variableScopeStack.Pop();
            dict.Clear();
        }

        while (scopeInfoStack.Count > 1)
        {
            scopeInfoStack.Pop();
        }

        if (customScopeData.Count > 0) customScopeData.Peek().Clear();
        if (variableScopeStack.Count > 0) variableScopeStack.Peek().Clear();
        
        // Reset variable tracker if enabled
        if (VariableTracker != null)
        {
            VariableTracker.Reset();
        }
        
        OnReset();
    }

    #endregion

    #region Virtual Methods for Subclasses

    /// <summary>
    /// Called when a variable is declared in any scope
    /// </summary>
    protected virtual void OnVariableDeclared(VariableInfo varInfo, ScopeInfo scope) { }
    
    /// <summary>
    /// Called when a variable is used (marked as used by the usage tracker)
    /// Only called when TrackVariableUsage is enabled
    /// </summary>
    protected virtual void OnVariableUsed(string name, SourceSpan location, ScopeInfo scope) { }
    
    /// <summary>
    /// Called when entering a new scope
    /// </summary>
    protected virtual void OnEnterScope(ScopeInfo scopeInfo) { }
    
    /// <summary>
    /// Called when exiting a scope
    /// </summary>
    protected virtual void OnExitScope(ScopeInfo scopeInfo, Dictionary<string, VariableInfo> variableScope, Dictionary<string, T> customData) { }
    
    /// <summary>
    /// Called when entering a class
    /// </summary>
    protected virtual void OnClassEnter(AppClassNode node) { }
    
    /// <summary>
    /// Called when exiting a class
    /// </summary>
    protected virtual void OnClassExit(AppClassNode node) { }

    /// <summary>
    /// Called when the visitor is reset
    /// </summary>
    protected virtual void OnReset() {}

    #endregion
}