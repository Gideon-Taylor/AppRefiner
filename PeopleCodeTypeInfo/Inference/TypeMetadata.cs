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
    /// True if BaseClassName refers to a builtin type (like Record, Field, etc.)
    /// </summary>
    public bool IsBaseClassBuiltin { get; init; }

    /// <summary>
    /// The PeopleCodeType of the builtin base class, if IsBaseClassBuiltin is true
    /// </summary>
    public PeopleCodeType? BuiltinBaseType { get; init; }

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
    /// Constructor declaration in this class (PeopleCode allows at most 1 explicit constructor).
    /// For any class resolved through <c>TypeMetadataBuilder</c> this is never null: the
    /// builder synthesizes a zero-parameter default constructor when the class declares
    /// none. It is only null if a TypeMetadata is constructed directly, bypassing the
    /// builder.
    /// </summary>
    public FunctionInfo? Constructor { get; init; }

    /// <summary>
    /// Signatures of members a deriving class is required to implement: every
    /// method/property of an interface, and only the abstract methods/properties of a
    /// class. Constructors and instance variables are never included. Signature scheme
    /// is "M:{name}({paramCount})" for methods and "P:{name}" for properties — always
    /// built via <see cref="MethodSignature"/> / <see cref="PropertySignature"/>.
    /// Populated at runtime by TypeMetadataBuilder; not part of the serialized
    /// builtin type database.
    /// </summary>
    public IReadOnlyCollection<string> AbstractMemberSignatures { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Signatures of members this type concretely implements (non-abstract methods and
    /// properties of a class; always empty for interfaces). Used to stop abstract
    /// requirements from bases higher up the hierarchy from propagating past a class
    /// that already implements them. Same scheme and exclusions as
    /// <see cref="AbstractMemberSignatures"/>.
    /// </summary>
    public IReadOnlyCollection<string> ConcreteMemberSignatures { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Canonical method signature used by AbstractMemberSignatures/ConcreteMemberSignatures.
    /// Both TypeMetadataBuilder and the compile checks must use this helper so the
    /// scheme cannot drift.
    /// </summary>
    public static string MethodSignature(string name, int paramCount) => $"M:{name}({paramCount})";

    /// <summary>
    /// Canonical property signature used by AbstractMemberSignatures/ConcreteMemberSignatures.
    /// </summary>
    public static string PropertySignature(string name) => $"P:{name}";
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
