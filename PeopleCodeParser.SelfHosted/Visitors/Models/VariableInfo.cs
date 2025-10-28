using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Visitors.Models;

/// <summary>
/// Enhanced variable classification for PeopleCode variables
/// </summary>
public enum VariableKind
{
    /// <summary>
    /// Local variable declared within a method/function
    /// </summary>
    Local,

    /// <summary>
    /// Instance variable declared in a class
    /// </summary>
    Instance,

    /// <summary>
    /// Global variable accessible across programs
    /// </summary>
    Global,

    /// <summary>
    /// Component variable shared across component sessions
    /// </summary>
    Component,

    /// <summary>
    /// Method or function parameter
    /// </summary>
    Parameter,

    /// <summary>
    /// Constant value
    /// </summary>
    Constant,

    /// <summary>
    /// Property declaration
    /// </summary>
    Property,

    /// <summary>
    /// Exception variable introduced by catch statement
    /// </summary>
    Exception
}

/// <summary>
/// Enhanced variable information with comprehensive reference tracking and safety classification
/// </summary>
public class VariableInfo
{
    /// <summary>
    /// Variable name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Variable data type
    /// </summary>
    public string Type { get; private set; }

    /// <summary>
    /// Kind of variable (local, instance, global, etc.)
    /// </summary>
    public VariableKind Kind { get; }

    /// <summary>
    /// True if this variable was implicitly declared (auto-declared on first use in non-class programs)
    /// Used by type checker to differentiate error vs warning severity
    /// </summary>
    public bool IsAutoDeclared { get; init; }

    /// <summary>
    /// Scope where this variable is declared
    /// </summary>
    public ScopeContext DeclarationScope { get; }

    /// <summary>
    /// Original VariableNameInfo from the parser
    /// </summary>
    public VariableNameInfo VariableNameInfo { get; }

    /// <summary>
    /// All references to this variable (including declaration)
    /// </summary>
    public List<VariableReference> References { get; } = new();

    /// <summary>
    /// True if this variable is safe to refactor (rename) within the current program
    /// Safe variables are those that are only accessible within the current program
    /// </summary>
    public bool IsSafeToRefactor => Kind is VariableKind.Local or VariableKind.Instance or VariableKind.Parameter;

    /// <summary>
    /// True if this variable has been used (referenced) anywhere in the program
    /// Parameter annotations do not count as usage - only Read and Write references count
    /// </summary>
    public bool IsUsed => References.Any(r => r.ReferenceType == ReferenceType.Read || r.ReferenceType == ReferenceType.Write);

    /// <summary>
    /// True if this variable is only declared but never used
    /// </summary>
    public bool IsUnused => !IsUsed;

    /// <summary>
    /// Number of times this variable has been referenced (excluding declaration and parameter annotations)
    /// Only Read and Write references count as actual usage
    /// </summary>
    public int UsageCount => References.Count(r => r.ReferenceType == ReferenceType.Read || r.ReferenceType == ReferenceType.Write);

    /// <summary>
    /// Line number where this variable was declared
    /// </summary>
    public int DeclarationLine => VariableNameInfo.SourceSpan.Start.Line;

    /// <summary>
    /// The declaration reference for this variable
    /// </summary>
    public VariableReference? DeclarationReference => References.FirstOrDefault(r => r.ReferenceType == ReferenceType.Declaration);

    public AstNode DeclarationNode;

    public VariableInfo(
        VariableNameInfo variableNameInfo,
        string type,
        VariableKind kind,
        ScopeContext declarationScope,
        AstNode declaringNode,
        bool isAutoDeclared = false)
    {
        Name = variableNameInfo?.Name ?? throw new ArgumentNullException(nameof(variableNameInfo));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Kind = kind;
        DeclarationScope = declarationScope ?? throw new ArgumentNullException(nameof(declarationScope));
        VariableNameInfo = variableNameInfo;
        DeclarationNode = declaringNode;
        IsAutoDeclared = isAutoDeclared;

        // Add the declaration reference
        AddReference(VariableReference.FromVariableNameInfo(
            variableNameInfo,
            ReferenceType.Declaration,
            declarationScope));
    }

    /// <summary>
    /// Adds a reference to this variable
    /// </summary>
    public void AddReference(VariableReference reference)
    {
        if (reference == null) throw new ArgumentNullException(nameof(reference));

        // Ensure the reference is for this variable
        if (!reference.VariableName.Equals(Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Reference variable name '{reference.VariableName}' does not match variable name '{Name}'");
        }

        // Avoid duplicate references
        if (!References.Contains(reference))
        {
            References.Add(reference);
        }
    }

    /// <summary>
    /// Updates the type of this variable (used during type inference for auto-declared variables)
    /// </summary>
    public void UpdateType(string newType)
    {
        if (string.IsNullOrEmpty(newType))
            throw new ArgumentNullException(nameof(newType));

        Type = newType;
    }

    /// <summary>
    /// Gets all references of a specific type
    /// </summary>
    public IEnumerable<VariableReference> GetReferences(ReferenceType referenceType)
    {
        return References.Where(r => r.ReferenceType == referenceType);
    }

    /// <summary>
    /// Gets all read references to this variable
    /// </summary>
    public IEnumerable<VariableReference> GetReadReferences()
    {
        return GetReferences(ReferenceType.Read);
    }

    /// <summary>
    /// Gets all write references to this variable
    /// </summary>
    public IEnumerable<VariableReference> GetWriteReferences()
    {
        return GetReferences(ReferenceType.Write);
    }

    /// <summary>
    /// Gets all parameter annotation references to this variable
    /// </summary>
    public IEnumerable<VariableReference> GetParameterAnnotationReferences()
    {
        return GetReferences(ReferenceType.ParameterAnnotation);
    }

    /// <summary>
    /// Gets all references within a specific scope
    /// </summary>
    public IEnumerable<VariableReference> GetReferencesInScope(ScopeContext scope)
    {
        return References.Where(r => r.Scope.Id == scope.Id);
    }

    /// <summary>
    /// Gets all references that are accessible from the specified scope
    /// </summary>
    public IEnumerable<VariableReference> GetAccessibleReferences(ScopeContext scope)
    {
        return References.Where(r => scope.CanAccessScope(r.Scope) || r.Scope.CanAccessScope(scope));
    }

    /// <summary>
    /// Checks if this variable is accessible from the specified scope
    /// </summary>
    public bool IsAccessibleFrom(ScopeContext scope)
    {
        return scope.CanAccessScope(DeclarationScope);
    }

    /// <summary>
    /// Gets references sorted by line number
    /// </summary>
    public IEnumerable<VariableReference> GetReferencesSortedByLocation()
    {
        return References.OrderBy(r => r.Line).ThenBy(r => r.Column);
    }

    /// <summary>
    /// Gets references within a specific line range
    /// </summary>
    public IEnumerable<VariableReference> GetReferencesInRange(int startLine, int endLine)
    {
        return References.Where(r => r.Line >= startLine && r.Line <= endLine);
    }

    /// <summary>
    /// Checks if this variable shadows (hides) another variable with the same name in a parent scope
    /// </summary>
    public bool Shadows(VariableInfo other)
    {
        if (!Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase))
            return false;

        // This variable shadows the other if this scope is a descendant of the other's scope
        return DeclarationScope.IsDescendantOf(other.DeclarationScope);
    }

    /// <summary>
    /// Gets a summary of variable usage statistics
    /// </summary>
    public VariableUsageSummary GetUsageSummary()
    {
        return new VariableUsageSummary
        {
            VariableName = Name,
            TotalReferences = References.Count,
            ReadCount = GetReadReferences().Count(),
            WriteCount = GetWriteReferences().Count(),
            ParameterAnnotationCount = GetParameterAnnotationReferences().Count(),
            IsUsed = IsUsed,
            IsSafeToRefactor = IsSafeToRefactor,
            DeclarationLine = DeclarationLine,
            Kind = Kind,
            Type = Type,
            DeclarationScope = DeclarationScope.FullQualifiedName
        };
    }

    public override string ToString()
    {
        var safetyStr = IsSafeToRefactor ? "SAFE" : "UNSAFE";
        var usageStr = IsUsed ? $"({UsageCount} uses)" : "UNUSED";
        return $"{Kind} {Type} {Name} [{safetyStr}] {usageStr} in {DeclarationScope.Name}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not VariableInfo other)
            return false;

        return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) &&
               DeclarationScope.Id == other.DeclarationScope.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name.ToLowerInvariant(), DeclarationScope.Id);
    }
}

/// <summary>
/// Summary of variable usage statistics
/// </summary>
public class VariableUsageSummary
{
    public string VariableName { get; init; } = string.Empty;
    public int TotalReferences { get; init; }
    public int ReadCount { get; init; }
    public int WriteCount { get; init; }
    public int ParameterAnnotationCount { get; init; }
    public bool IsUsed { get; init; }
    public bool IsSafeToRefactor { get; init; }
    public int DeclarationLine { get; init; }
    public VariableKind Kind { get; init; }
    public string Type { get; init; } = string.Empty;
    public string DeclarationScope { get; init; } = string.Empty;
}