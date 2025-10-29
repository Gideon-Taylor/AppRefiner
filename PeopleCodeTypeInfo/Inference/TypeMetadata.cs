using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Inference;

/// <summary>
/// Represents extracted type metadata from a PeopleCode program.
/// This is the result of parsing and analyzing a program to understand its type structure.
/// </summary>
public class TypeMetadata
{
    /// <summary>
    /// The fully qualified name of the type (e.g., "MY_PKG:MyClass")
    /// </summary>
    public string QualifiedName { get; init; } = string.Empty;

    /// <summary>
    /// The simple name of the type (e.g., "MyClass")
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The package name, if any (e.g., "MY_PKG")
    /// </summary>
    public string? PackageName { get; init; }

    /// <summary>
    /// The kind of program this represents
    /// </summary>
    public ProgramKind Kind { get; init; }

    /// <summary>
    /// Base class name if this is a class that extends another class
    /// </summary>
    public string? BaseClassName { get; init; }

    /// <summary>
    /// Interface that this class implements (PeopleCode allows at most 1 interface)
    /// </summary>
    public string? InterfaceName { get; init; }

    /// <summary>
    /// Methods/functions defined in this program
    /// Key is the method name (case-insensitive)
    /// </summary>
    public IReadOnlyDictionary<string, FunctionInfo> Methods { get; init; } =
        new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Properties defined in this class (only applicable for app classes)
    /// Key is the property name (case-insensitive)
    /// </summary>
    public IReadOnlyDictionary<string, PropertyInfo> Properties { get; init; } =
        new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Instance variables defined in this class (only applicable for app classes)
    /// Key is the property name (case-insensitive)
    /// </summary>
    public IReadOnlyDictionary<string, PropertyInfo> InstanceVariables { get; init; } =
        new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Constructor declaration in this class (PeopleCode allows at most 1 explicit constructor)
    /// Null indicates an implicit/default constructor
    /// </summary>
    public FunctionInfo? Constructor { get; init; }
}

/// <summary>
/// Represents the kind of PeopleCode program
/// </summary>
public enum ProgramKind
{
    /// <summary>
    /// Application class (can have methods and properties)
    /// </summary>
    AppClass,

    /// <summary>
    /// Interface definition (only method signatures)
    /// </summary>
    Interface,

    /// <summary>
    /// Plain function program (functions only, no class structure)
    /// </summary>
    FunctionLibrary
}
