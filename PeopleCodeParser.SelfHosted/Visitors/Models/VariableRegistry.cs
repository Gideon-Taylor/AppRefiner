namespace PeopleCodeParser.SelfHosted.Visitors.Models;

/// <summary>
/// Centralized registry for managing all variables and their references across the entire program
/// </summary>
public class VariableRegistry
{
    /// <summary>
    /// All variables indexed by their unique key (name + scope ID)
    /// </summary>
    private readonly Dictionary<VariableKey, VariableInfo> variables = new();
    
    /// <summary>
    /// Variables grouped by scope for efficient scope-based queries
    /// </summary>
    private readonly Dictionary<Guid, List<VariableInfo>> variablesByScope = new();
    
    /// <summary>
    /// All variables grouped by name (case-insensitive) for efficient name-based lookups
    /// </summary>
    private readonly Dictionary<string, List<VariableInfo>> variablesByName = 
        new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// All scopes in the program indexed by their ID
    /// </summary>
    private readonly Dictionary<Guid, ScopeContext> scopes = new();
    
    /// <summary>
    /// Gets all variables in the registry
    /// </summary>
    public IEnumerable<VariableInfo> AllVariables => variables.Values;
    
    /// <summary>
    /// Gets all scopes in the registry
    /// </summary>
    public IEnumerable<ScopeContext> AllScopes => scopes.Values;
    
    /// <summary>
    /// Gets the total number of variables registered
    /// </summary>
    public int VariableCount => variables.Count;
    
    /// <summary>
    /// Gets the total number of scopes registered
    /// </summary>
    public int ScopeCount => scopes.Count;
    
    /// <summary>
    /// Registers a scope in the registry
    /// </summary>
    public void RegisterScope(ScopeContext scope)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        
        if (!scopes.ContainsKey(scope.Id))
        {
            scopes[scope.Id] = scope;
            variablesByScope[scope.Id] = new List<VariableInfo>();
        }
    }
    
    /// <summary>
    /// Registers a variable in the registry
    /// </summary>
    public void RegisterVariable(VariableInfo variable)
    {
        if (variable == null) throw new ArgumentNullException(nameof(variable));
        
        var key = new VariableKey(variable.Name, variable.DeclarationScope.Id);
        
        // Register the variable
        variables[key] = variable;
        
        // Ensure the scope is registered
        RegisterScope(variable.DeclarationScope);
        
        // Add to scope-based index
        variablesByScope[variable.DeclarationScope.Id].Add(variable);
        
        // Add to name-based index
        if (!variablesByName.TryGetValue(variable.Name, out var nameList))
        {
            nameList = new List<VariableInfo>();
            variablesByName[variable.Name] = nameList;
        }
        nameList.Add(variable);
    }
    
    /// <summary>
    /// Adds a reference to an existing variable
    /// </summary>
    public void AddVariableReference(string variableName, ScopeContext referencingScope, VariableReference reference)
    {
        var variable = FindVariableInScope(variableName, referencingScope);
        if (variable != null)
        {
            variable.AddReference(reference);
        }
    }
    
    /// <summary>
    /// Gets a variable by name and scope
    /// </summary>
    public VariableInfo? GetVariable(string name, ScopeContext scope)
    {
        var key = new VariableKey(name, scope.Id);
        return variables.TryGetValue(key, out var variable) ? variable : null;
    }
    
    /// <summary>
    /// Gets all variables declared in a specific scope
    /// </summary>
    public IEnumerable<VariableInfo> GetVariablesInScope(ScopeContext scope)
    {
        return variablesByScope.TryGetValue(scope.Id, out var vars) ? vars : Enumerable.Empty<VariableInfo>();
    }
    
    /// <summary>
    /// Gets all variables accessible from a specific scope (including parent scopes)
    /// </summary>
    public IEnumerable<VariableInfo> GetAccessibleVariables(ScopeContext scope)
    {
        var accessibleVars = new List<VariableInfo>();
        
        // Get variables from current scope and all ancestor scopes
        foreach (var currentScope in scope.GetScopeChain())
        {
            accessibleVars.AddRange(GetVariablesInScope(currentScope));
        }
        
        return accessibleVars;
    }
    
    /// <summary>
    /// Finds a variable by name that is accessible from the given scope
    /// Uses PeopleCode scoping rules to find the closest matching variable
    /// </summary>
    public VariableInfo? FindVariableInScope(string name, ScopeContext scope)
    {
        // Search in current scope first, then parent scopes
        foreach (var currentScope in scope.GetScopeChain())
        {
            var variable = GetVariable(name, currentScope);
            if (variable != null)
            {
                return variable;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets all variables with the specified name (case-insensitive)
    /// </summary>
    public IEnumerable<VariableInfo> GetVariablesByName(string name)
    {
        return variablesByName.TryGetValue(name, out var vars) ? vars : Enumerable.Empty<VariableInfo>();
    }
    
    /// <summary>
    /// Gets all unused variables across the entire program
    /// </summary>
    public IEnumerable<VariableInfo> GetUnusedVariables()
    {
        return AllVariables.Where(v => v.IsUnused);
    }
    
    /// <summary>
    /// Gets all unused variables in a specific scope
    /// </summary>
    public IEnumerable<VariableInfo> GetUnusedVariablesInScope(ScopeContext scope)
    {
        return GetVariablesInScope(scope).Where(v => v.IsUnused);
    }
    
    /// <summary>
    /// Gets all variables that are safe to refactor (rename)
    /// </summary>
    public IEnumerable<VariableInfo> GetSafeToRefactorVariables()
    {
        return AllVariables.Where(v => v.IsSafeToRefactor);
    }
    
    /// <summary>
    /// Gets all variables of a specific kind (local, instance, global, etc.)
    /// </summary>
    public IEnumerable<VariableInfo> GetVariablesByKind(VariableKind kind)
    {
        return AllVariables.Where(v => v.Kind == kind);
    }
    
    /// <summary>
    /// Gets variables that shadow other variables (have the same name in nested scopes)
    /// </summary>
    public IEnumerable<(VariableInfo Shadowing, VariableInfo Shadowed)> GetShadowingVariables()
    {
        var shadowing = new List<(VariableInfo, VariableInfo)>();
        
        foreach (var variable in AllVariables)
        {
            var sameNameVars = GetVariablesByName(variable.Name).Where(v => v != variable);
            foreach (var other in sameNameVars)
            {
                if (variable.Shadows(other))
                {
                    shadowing.Add((variable, other));
                }
            }
        }
        
        return shadowing;
    }
    
    /// <summary>
    /// Gets a scope by its ID
    /// </summary>
    public ScopeContext? GetScope(Guid scopeId)
    {
        return scopes.TryGetValue(scopeId, out var scope) ? scope : null;
    }
    
    /// <summary>
    /// Gets the global scope (root scope)
    /// </summary>
    public ScopeContext? GetGlobalScope()
    {
        return AllScopes.FirstOrDefault(s => s.Type == EnhancedScopeType.Global);
    }
    
    /// <summary>
    /// Gets all scopes of a specific type
    /// </summary>
    public IEnumerable<ScopeContext> GetScopesByType(EnhancedScopeType type)
    {
        return AllScopes.Where(s => s.Type == type);
    }
    
    /// <summary>
    /// Gets usage statistics for all variables
    /// </summary>
    public VariableRegistryStatistics GetStatistics()
    {
        var stats = new VariableRegistryStatistics
        {
            TotalVariables = VariableCount,
            TotalScopes = ScopeCount,
            UnusedVariables = GetUnusedVariables().Count(),
            SafeToRefactorVariables = GetSafeToRefactorVariables().Count(),
            VariablesByKind = Enum.GetValues<VariableKind>()
                .ToDictionary(kind => kind, kind => GetVariablesByKind(kind).Count()),
            ScopesByType = Enum.GetValues<EnhancedScopeType>()
                .ToDictionary(type => type, type => GetScopesByType(type).Count()),
            ShadowingVariables = GetShadowingVariables().Count()
        };
        
        return stats;
    }
    
    /// <summary>
    /// Clears all registered variables and scopes
    /// </summary>
    public void Clear()
    {
        variables.Clear();
        variablesByScope.Clear();
        variablesByName.Clear();
        scopes.Clear();
    }
    
    /// <summary>
    /// Gets detailed information about variables for debugging
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"=== Variable Registry Debug Info ===");
        info.AppendLine($"Total Variables: {VariableCount}");
        info.AppendLine($"Total Scopes: {ScopeCount}");
        info.AppendLine();
        
        foreach (var scope in AllScopes.OrderBy(s => s.Depth).ThenBy(s => s.Name))
        {
            info.AppendLine($"Scope: {scope}");
            var scopeVars = GetVariablesInScope(scope);
            foreach (var variable in scopeVars.OrderBy(v => v.Name))
            {
                info.AppendLine($"  {variable}");
                foreach (var reference in variable.References.Take(3))
                {
                    info.AppendLine($"    {reference}");
                }
                if (variable.References.Count > 3)
                {
                    info.AppendLine($"    ... and {variable.References.Count - 3} more references");
                }
            }
            info.AppendLine();
        }
        
        return info.ToString();
    }
}

/// <summary>
/// Unique key for identifying variables by name and scope
/// </summary>
public class VariableKey
{
    public string Name { get; }
    public Guid ScopeId { get; }
    
    public VariableKey(string name, Guid scopeId)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ScopeId = scopeId;
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is not VariableKey other)
            return false;
            
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
               ScopeId == other.ScopeId;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Name.ToLowerInvariant(), ScopeId);
    }
    
    public override string ToString()
    {
        return $"{Name}@{ScopeId:N}";
    }
}

/// <summary>
/// Statistics about the variable registry
/// </summary>
public class VariableRegistryStatistics
{
    public int TotalVariables { get; init; }
    public int TotalScopes { get; init; }
    public int UnusedVariables { get; init; }
    public int SafeToRefactorVariables { get; init; }
    public Dictionary<VariableKind, int> VariablesByKind { get; init; } = new();
    public Dictionary<EnhancedScopeType, int> ScopesByType { get; init; } = new();
    public int ShadowingVariables { get; init; }
}