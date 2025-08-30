using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Visitors.Models;

/// <summary>
/// Represents the type of scope in the PeopleCode program structure
/// </summary>
public enum EnhancedScopeType
{
    /// <summary>
    /// Global program scope - contains global/component variables, functions, constants
    /// </summary>
    Global,
    
    /// <summary>
    /// Class scope - contains class members, instance variables
    /// </summary>
    Class,
    
    /// <summary>
    /// Method scope - contains method parameters and local variables
    /// </summary>
    Method,
    
    /// <summary>
    /// Function scope - contains function parameters and local variables
    /// </summary>
    Function,
    
    /// <summary>
    /// Property scope - contains property getter/setter logic
    /// </summary>
    Property
}

/// <summary>
/// Rich context information for a scope, including hierarchical relationships and metadata
/// </summary>
public class ScopeContext
{
    /// <summary>
    /// Unique identifier for this scope instance
    /// </summary>
    public Guid Id { get; }
    
    /// <summary>
    /// Type of this scope
    /// </summary>
    public EnhancedScopeType Type { get; }
    
    /// <summary>
    /// Name of this scope (method name, class name, "Global", etc.)
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Parent scope (null for global scope)
    /// </summary>
    public ScopeContext? Parent { get; }
    
    /// <summary>
    /// Child scopes created within this scope
    /// </summary>
    public List<ScopeContext> Children { get; } = new();
    
    /// <summary>
    /// AST node that created this scope
    /// </summary>
    public AstNode SourceNode { get; }
    
    /// <summary>
    /// Depth level in the scope hierarchy (0 = global, 1 = class/function, 2 = method, etc.)
    /// </summary>
    public int Depth { get; }
    
    /// <summary>
    /// Full qualified name showing the scope hierarchy (e.g., "Global.MyClass.MyMethod")
    /// </summary>
    public string FullQualifiedName { get; }

    public ScopeContext(EnhancedScopeType type, string name, AstNode sourceNode, ScopeContext? parent = null)
    {
        Id = Guid.NewGuid();
        Type = type;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SourceNode = sourceNode ?? throw new ArgumentNullException(nameof(sourceNode));
        Parent = parent;
        Depth = parent?.Depth + 1 ?? 0;
        
        // Build full qualified name
        if (parent != null)
        {
            FullQualifiedName = $"{parent.FullQualifiedName}.{name}";
            parent.Children.Add(this);
        }
        else
        {
            FullQualifiedName = name;
        }
    }
    
    /// <summary>
    /// Gets all ancestor scopes from this scope up to the global scope
    /// </summary>
    public IEnumerable<ScopeContext> GetAncestors()
    {
        var current = Parent;
        while (current != null)
        {
            yield return current;
            current = current.Parent;
        }
    }
    
    /// <summary>
    /// Gets all ancestor scopes including this scope
    /// </summary>
    public IEnumerable<ScopeContext> GetScopeChain()
    {
        yield return this;
        foreach (var ancestor in GetAncestors())
        {
            yield return ancestor;
        }
    }
    
    /// <summary>
    /// Gets all descendant scopes recursively
    /// </summary>
    public IEnumerable<ScopeContext> GetDescendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.GetDescendants())
            {
                yield return descendant;
            }
        }
    }
    
    /// <summary>
    /// Checks if this scope is an ancestor of the specified scope
    /// </summary>
    public bool IsAncestorOf(ScopeContext scope)
    {
        return scope.GetAncestors().Contains(this);
    }
    
    /// <summary>
    /// Checks if this scope is a descendant of the specified scope
    /// </summary>
    public bool IsDescendantOf(ScopeContext scope)
    {
        return GetAncestors().Contains(scope);
    }
    
    /// <summary>
    /// Finds the closest common ancestor scope with another scope
    /// </summary>
    public ScopeContext? FindCommonAncestor(ScopeContext other)
    {
        var thisChain = GetScopeChain().ToList();
        var otherChain = other.GetScopeChain().ToList();
        
        // Find the first common scope by comparing from the root down
        thisChain.Reverse();
        otherChain.Reverse();
        
        ScopeContext? commonAncestor = null;
        int minLength = Math.Min(thisChain.Count, otherChain.Count);
        
        for (int i = 0; i < minLength; i++)
        {
            if (thisChain[i].Id == otherChain[i].Id)
            {
                commonAncestor = thisChain[i];
            }
            else
            {
                break;
            }
        }
        
        return commonAncestor;
    }
    
    /// <summary>
    /// Gets the global scope by traversing up the hierarchy
    /// </summary>
    public ScopeContext GetGlobalScope()
    {
        var current = this;
        while (current.Parent != null)
        {
            current = current.Parent;
        }
        return current;
    }
    
    /// <summary>
    /// Gets the class scope if this scope is within a class, null otherwise
    /// </summary>
    public ScopeContext? GetClassScope()
    {
        return GetScopeChain().FirstOrDefault(s => s.Type == EnhancedScopeType.Class);
    }
    
    /// <summary>
    /// Checks if this scope can access variables from the specified scope based on PeopleCode scoping rules
    /// </summary>
    public bool CanAccessScope(ScopeContext targetScope)
    {
        // Can always access your own scope
        if (Id == targetScope.Id)
            return true;
            
        // Can access ancestor scopes (parent, grandparent, etc.)
        if (IsDescendantOf(targetScope))
            return true;
            
        // Cannot access sibling scopes or descendant scopes
        return false;
    }

    public override string ToString()
    {
        return $"{Type}: {FullQualifiedName}";
    }
    
    public override bool Equals(object? obj)
    {
        return obj is ScopeContext other && Id == other.Id;
    }
    
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}