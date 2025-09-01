using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// Tracks variable usage across different scopes
/// </summary>
public class VariableUsageTracker : IVariableUsageTracker
{
    public class VariableKey
    {
        public string Name { get; }
        public ScopeInfo Scope { get; }

        public VariableKey(string name, ScopeInfo scope)
        {
            Name = name;
            Scope = scope;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not VariableKey other)
                return false;

            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
                   Scope == other.Scope;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Name.ToLowerInvariant().GetHashCode(),
                Scope.GetHashCode()
            );
        }
    }

    private readonly Dictionary<VariableKey, (VariableInfo Variable, bool Used, List<SourceSpan> References)> usageMap = new();
    private readonly Dictionary<ScopeInfo, List<VariableKey>> scopeToVariablesMap = new();

    // Track undefined variable references
    private readonly List<(string Name, SourceSpan Location, ScopeInfo Scope)> undefinedReferences = new();

    /// <summary>
    /// Registers a variable in the specified scope
    /// </summary>
    public void RegisterVariable(VariableInfo variable, ScopeInfo scope)
    {
        var key = new VariableKey(variable.Name, scope);
        usageMap[key] = (variable, false, [variable.VariableNameInfo.SourceSpan]);

        if (!scopeToVariablesMap.TryGetValue(scope, out var variables))
        {
            variables = new List<VariableKey>();
            scopeToVariablesMap[scope] = variables;
        }

        variables.Add(key);
    }

    /// <summary>
    /// Marks a variable as used by name, searching in the current scope and parent scopes
    /// </summary>
    public bool MarkAsUsed(string name, ScopeInfo currentScope)
    {
        // Use the location-aware version with default SourceSpan for backward compatibility
        return MarkAsUsedWithLocation(name, default(SourceSpan), currentScope);
    }

    /// <summary>
    /// Marks a variable as used by name with location tracking, searching in the current scope and parent scopes
    /// </summary>
    public bool MarkAsUsedWithLocation(string name, SourceSpan location, ScopeInfo currentScope)
    {
        // First try to find in current scope
        var key = FindVariableKey(name, currentScope);
        if (key != null)
        {
            if (usageMap.TryGetValue(key, out var entry))
            {
                // Add the location to references if it's not default
                var references = entry.References;
                if (location != default(SourceSpan))
                {
                    references.Add(location);
                }

                usageMap[key] = (entry.Variable, true, references);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a variable is used
    /// </summary>
    public bool IsUsed(VariableInfo variable, ScopeInfo scope)
    {
        var key = new VariableKey(variable.Name, scope);
        return usageMap.TryGetValue(key, out var entry) && entry.Used;
    }

    /// <summary>
    /// Gets all unused variables across all scopes
    /// </summary>
    public IEnumerable<(VariableInfo Variable, ScopeInfo Scope)> GetUnusedVariables()
    {
        return usageMap
            .Where(kvp => !kvp.Value.Used)
            .Select(kvp => (kvp.Value.Variable, kvp.Key.Scope));
    }

    /// <summary>
    /// Gets all unused variables in a specific scope
    /// </summary>
    public IEnumerable<VariableInfo> GetUnusedVariablesInScope(ScopeInfo scope)
    {
        if (!scopeToVariablesMap.TryGetValue(scope, out var variables))
            return Enumerable.Empty<VariableInfo>();

        return variables
            .Where(key => usageMap.TryGetValue(key, out var entry) && !entry.Used)
            .Select(key => usageMap[key].Variable);
    }

    /// <summary>
    /// Resets the tracker to its initial state
    /// </summary>
    public void Reset()
    {
        usageMap.Clear();
        scopeToVariablesMap.Clear();
        undefinedReferences.Clear();
    }

    /// <summary>
    /// Checks if a variable is defined in any accessible scope
    /// </summary>
    public bool IsVariableDefined(string name, ScopeInfo currentScope)
    {
        return FindVariableKey(name, currentScope) != null;
    }

    /// <summary>
    /// Tracks a reference to an undefined variable
    /// </summary>
    public void TrackUndefinedReference(string name, SourceSpan location, ScopeInfo scope)
    {
        undefinedReferences.Add((name, location, scope));
    }

    /// <summary>
    /// Gets all tracked undefined variable references
    /// </summary>
    public IEnumerable<(string Name, SourceSpan Location, ScopeInfo Scope)> GetUndefinedReferences()
    {
        return undefinedReferences.ToList();
    }

    /// <summary>
    /// Gets all reference locations for a variable by name in the specified scope
    /// </summary>
    public IEnumerable<SourceSpan> GetVariableReferences(string name, ScopeInfo scope)
    {
        var key = new VariableKey(name, scope);
        if (usageMap.TryGetValue(key, out var entry))
        {
            return entry.References.ToList();
        }

        return Enumerable.Empty<SourceSpan>();
    }

    /// <summary>
    /// Gets all reference locations for a variable in the specified scope
    /// </summary>
    public IEnumerable<SourceSpan> GetVariableReferences(VariableInfo variable, ScopeInfo scope)
    {
        return GetVariableReferences(variable.Name, scope);
    }

    public IEnumerable<VariableInfo> GetAllVariablesInScope(ScopeInfo scope)
    {
        if (scopeToVariablesMap.TryGetValue(scope, out var variables))
        {
            return variables.Select(key => usageMap[key].Variable);
        }
        return Enumerable.Empty<VariableInfo>();
    }

    /// <summary>
    /// Finds a variable key by name in the specified scope or any parent scope
    /// </summary>
    private VariableKey? FindVariableKey(string name, ScopeInfo scope)
    {
        var currentScope = scope;

        while (currentScope != null)
        {
            if (scopeToVariablesMap.TryGetValue(currentScope, out var variables))
            {
                foreach (var key in variables)
                {
                    if (string.Equals(key.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return key;
                    }
                }
            }

            currentScope = currentScope.Parent;
        }

        return null;
    }
}
