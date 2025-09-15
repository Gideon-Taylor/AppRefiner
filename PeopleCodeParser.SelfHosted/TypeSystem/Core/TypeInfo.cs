using System;
using System.Collections.Generic;
using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Represents the kind/category of a type in the PeopleCode type system
/// </summary>
public enum TypeKind
{
    /// <summary>
    /// Built-in primitive types (string, integer, number, date, boolean, etc.)
    /// </summary>
    Primitive,

    /// <summary>
    /// Built-in object types (Record, Rowset, Field, Component, etc.)
    /// </summary>
    BuiltinObject,

    /// <summary>
    /// User-defined application classes
    /// </summary>
    AppClass,

    /// <summary>
    /// Interface types
    /// </summary>
    Interface,

    /// <summary>
    /// Array types (ARRAY OF type)
    /// </summary>
    Array,

    /// <summary>
    /// The special "Any" type that can hold any value
    /// </summary>
    Any,

    /// <summary>
    /// No return value (like void in other languages)
    /// Used for functions/methods that don't return a value
    /// </summary>
    Void,

    /// <summary>
    /// Unknown/unresolved type
    /// </summary>
    Unknown,

    /// <summary>
    /// Reference type for named references like HTML.OBJECT_NAME, SQL.FOO, RECORD.FOO
    /// Also includes dynamic references via @ expressions
    /// </summary>
    Reference
}

/// <summary>
/// Base class for all type information in the PeopleCode type system
/// </summary>
public abstract class TypeInfo
{
    /// <summary>
    /// The name of this type as it appears in code
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The kind/category of this type
    /// </summary>
    public abstract TypeKind Kind { get; }

    /// <summary>
    /// The PeopleCode type enum value, if this type corresponds to a PeopleCode type
    /// </summary>
    public virtual PeopleCodeType? PeopleCodeType => null;

    /// <summary>
    /// True if this type can be assigned null values (most types can in PeopleCode)
    /// </summary>
    public virtual bool IsNullable => true;

    /// <summary>
    /// Determines if a value of 'other' type can be assigned to this type
    /// </summary>
    public abstract bool IsAssignableFrom(TypeInfo other);

    /// <summary>
    /// Gets the most specific common type between this and another type
    /// </summary>
    public virtual TypeInfo GetCommonType(TypeInfo other)
    {
        // Fast path: if both have the same PeopleCodeType, return this
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue && PeopleCodeType.Value == other.PeopleCodeType.Value)
        {
            return this;
        }

        // Fast path: check for common primitive promotions
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue &&
            PeopleCodeType.Value.IsPrimitive() && other.PeopleCodeType.Value.IsPrimitive())
        {
            var commonType = GetCommonPrimitiveType(PeopleCodeType.Value, other.PeopleCodeType.Value);
            if (commonType.HasValue)
            {
                return TypeInfo.FromPeopleCodeType(commonType.Value);
            }
        }

        // Standard logic - prioritize more general types
        // If one is Any, return Any
        if (Kind == TypeKind.Any) return this;
        if (other.Kind == TypeKind.Any) return other;

        // Check assignability
        if (IsAssignableFrom(other)) return this;
        if (other.IsAssignableFrom(this)) return other;
        return AnyTypeInfo.Instance; // Default to Any if no common type
    }

    /// <summary>
    /// Gets the common type for primitive type promotions
    /// </summary>
    private static TypeSystem.PeopleCodeType? GetCommonPrimitiveType(TypeSystem.PeopleCodeType type1, TypeSystem.PeopleCodeType type2)
    {
        // Same type
        if (type1 == type2) return type1;

        // Integer and Number are bidirectionally compatible, common type is Number
        if ((type1 == TypeSystem.PeopleCodeType.Integer && type2 == TypeSystem.PeopleCodeType.Number) ||
            (type1 == TypeSystem.PeopleCodeType.Number && type2 == TypeSystem.PeopleCodeType.Integer))
        {
            return TypeSystem.PeopleCodeType.Number;
        }

        // Date, DateTime, and Time are NOT compatible with each other - no common type
        var dateTimeTypes = new[] { TypeSystem.PeopleCodeType.Date, TypeSystem.PeopleCodeType.DateTime, TypeSystem.PeopleCodeType.Time };
        if (dateTimeTypes.Contains(type1) && dateTimeTypes.Contains(type2))
        {
            return null; // No common type for date/datetime/time
        }

        // No other common primitive types
        return null;
    }

    public override string ToString() => Name;

    public override bool Equals(object? obj)
    {
        if (obj is not TypeInfo other) return false;

        // Fast path: if both have PeopleCodeType values, compare those first
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue)
        {
            return PeopleCodeType.Value == other.PeopleCodeType.Value;
        }

        // Fallback to name comparison
        return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        // Use PeopleCodeType for hash if available, otherwise use name
        return PeopleCodeType?.GetHashCode() ?? Name.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a TypeInfo instance from a PeopleCodeType enum value
    /// </summary>
    public static TypeInfo FromPeopleCodeType(PeopleCodeType peopleCodeType)
    {
        return peopleCodeType switch
        {
            // Special types
            TypeSystem.PeopleCodeType.Any => AnyTypeInfo.Instance,
            TypeSystem.PeopleCodeType.Void => VoidTypeInfo.Instance,
            TypeSystem.PeopleCodeType.Unknown => UnknownTypeInfo.Instance,
            TypeSystem.PeopleCodeType.Reference => ReferenceTypeInfo.Instance,
            TypeSystem.PeopleCodeType.AppClass => throw new InvalidOperationException("AppClass types require additional context - use PeopleCodeTypeRegistry.GetTypeByName() with appClassContext parameter"),

            // Primitive types
            TypeSystem.PeopleCodeType.String => PrimitiveTypeInfo.String,
            TypeSystem.PeopleCodeType.Integer => PrimitiveTypeInfo.Integer,
            TypeSystem.PeopleCodeType.Number => PrimitiveTypeInfo.Number,
            TypeSystem.PeopleCodeType.Date => PrimitiveTypeInfo.Date,
            TypeSystem.PeopleCodeType.DateTime => PrimitiveTypeInfo.DateTime,
            TypeSystem.PeopleCodeType.Time => PrimitiveTypeInfo.Time,
            TypeSystem.PeopleCodeType.Boolean => PrimitiveTypeInfo.Boolean,

            // Builtin object types - create new instances with the enum type
            _ when peopleCodeType.IsBuiltinObject() => new BuiltinObjectTypeInfo(peopleCodeType.GetTypeName(), peopleCodeType),

            _ => throw new ArgumentException($"Unknown PeopleCode type: {peopleCodeType}", nameof(peopleCodeType))
        };
    }

    /// <summary>
    /// Creates a TypeInfo instance from a BuiltinType enum value (legacy compatibility)
    /// </summary>
    [Obsolete("Use FromPeopleCodeType() instead")]
    public static TypeInfo FromBuiltinType(PeopleCodeType peopleCodeType)
    {
        return FromPeopleCodeType(peopleCodeType);
    }
}

/// <summary>
/// Represents a built-in primitive type in PeopleCode (string, integer, number, etc.)
/// </summary>
public class PrimitiveTypeInfo : TypeInfo
{
    public override string Name { get; }
    public override TypeKind Kind => TypeKind.Primitive;
    public override PeopleCodeType? PeopleCodeType { get; }

    // Common primitive type instances
    public static readonly PrimitiveTypeInfo String = new StringTypeInfo();
    public static readonly PrimitiveTypeInfo Integer = new("integer", TypeSystem.PeopleCodeType.Integer);
    public static readonly PrimitiveTypeInfo Number = new NumberTypeInfo();
    public static readonly PrimitiveTypeInfo Date = new("date", TypeSystem.PeopleCodeType.Date);
    public static readonly PrimitiveTypeInfo DateTime = new("datetime", TypeSystem.PeopleCodeType.DateTime);
    public static readonly PrimitiveTypeInfo Time = new("time", TypeSystem.PeopleCodeType.Time);
    public static readonly PrimitiveTypeInfo Boolean = new("boolean", TypeSystem.PeopleCodeType.Boolean);

    public PrimitiveTypeInfo(string name, PeopleCodeType? peopleCodeType = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));

        // If PeopleCodeType not provided, try to infer from name
        if (peopleCodeType == null)
        {
            PeopleCodeTypeRegistry.TryGetPeopleCodeTypeEnum(name, out var inferredType);
            if (inferredType.IsPrimitive())
            {
                peopleCodeType = inferredType;
            }
        }

        PeopleCodeType = peopleCodeType;
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Any can be assigned to any primitive
        if (other.Kind == TypeKind.Any) return true;

        // Fast path: if both have PeopleCodeType values, compare those first
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue)
        {
            // Same type
            if (PeopleCodeType.Value == other.PeopleCodeType.Value) return true;

            // PeopleCode implicit conversions using enum values
            return CanImplicitlyConvert(PeopleCodeType.Value, other.PeopleCodeType.Value);
        }

        // Fallback: Same primitive type by name
        if (other is PrimitiveTypeInfo primitive && primitive.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))
            return true;

        // PeopleCode has some implicit conversions
        return CanImplicitlyConvert(other);
    }

    private bool CanImplicitlyConvert(TypeSystem.PeopleCodeType thisType, TypeSystem.PeopleCodeType otherType)
    {
        // Both must be primitive types
        if (!thisType.IsPrimitive() || !otherType.IsPrimitive()) return false;

        // Bidirectional number/integer compatibility
        if ((thisType == TypeSystem.PeopleCodeType.Number && otherType == TypeSystem.PeopleCodeType.Integer) ||
            (thisType == TypeSystem.PeopleCodeType.Integer && otherType == TypeSystem.PeopleCodeType.Number))
        {
            return true;
        }

        return false;
    }

    private bool CanImplicitlyConvert(TypeInfo other)
    {
        if (other.Kind != TypeKind.Primitive) return false;

        var otherName = other.Name.ToLowerInvariant();
        var thisName = Name.ToLowerInvariant();

        // Bidirectional number/integer compatibility
        if ((thisName == "number" && otherName == "integer") ||
            (thisName == "integer" && otherName == "number"))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Represents a built-in object type in PeopleCode (Record, Rowset, Field, etc.)
/// </summary>
public class BuiltinObjectTypeInfo : TypeInfo
{
    public override string Name { get; }
    public override TypeKind Kind => TypeKind.BuiltinObject;
    public override PeopleCodeType? PeopleCodeType { get; }

    // Common builtin object instances
    public static readonly BuiltinObjectTypeInfo Record = new("Record", TypeSystem.PeopleCodeType.Record);
    public static readonly BuiltinObjectTypeInfo Rowset = new("Rowset", TypeSystem.PeopleCodeType.Rowset);

    public BuiltinObjectTypeInfo(string name, PeopleCodeType? peopleCodeType = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        PeopleCodeType = peopleCodeType;
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Any can be assigned to any builtin object
        if (other.Kind == TypeKind.Any) return true;

        // Fast path: if both have PeopleCodeType values, compare those first
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue)
        {
            return PeopleCodeType.Value == other.PeopleCodeType.Value;
        }

        // Fallback: Same builtin object type by name
        if (other is BuiltinObjectTypeInfo builtin && builtin.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

/// <summary>
/// Represents a user-defined application class type
/// </summary>
public class AppClassTypeInfo : TypeInfo
{
    public override string Name => QualifiedName;
    public override TypeKind Kind => TypeKind.AppClass;

    /// <summary>
    /// Fully qualified class name (e.g., "MyPackage:MyClass")
    /// </summary>
    public string QualifiedName { get; }

    /// <summary>
    /// Package path components (e.g., ["MyPackage"] for "MyPackage:MyClass")
    /// </summary>
    public IReadOnlyList<string> PackagePath { get; }

    /// <summary>
    /// Simple class name (e.g., "MyClass" from "MyPackage:MyClass")
    /// </summary>
    public string ClassName { get; }

    public AppClassTypeInfo(string qualifiedName)
    {
        QualifiedName = qualifiedName ?? throw new ArgumentNullException(nameof(qualifiedName));

        var parts = qualifiedName.Split(':');
        if (parts.Length == 1)
        {
            PackagePath = Array.Empty<string>();
            ClassName = parts[0];
        }
        else
        {
            PackagePath = parts.Take(parts.Length - 1).ToArray();
            ClassName = parts[parts.Length - 1];
        }
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Any can be assigned to any app class
        if (other.Kind == TypeKind.Any) return true;

        // Same app class type (TODO: inheritance checking in thorough mode)
        if (other is not AppClassTypeInfo appClass)
        {
            return false;
        }

        if (appClass.QualifiedName.Equals(QualifiedName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!PeopleCodeTypeRegistry.TryGetClassInfo(QualifiedName, out var targetInfo) || targetInfo == null)
        {
            return false;
        }

        // Walk the inheritance chain looking for this class as an ancestor.
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentName = appClass.QualifiedName;

        while (!string.IsNullOrWhiteSpace(currentName) && visited.Add(currentName))
        {
            if (!PeopleCodeTypeRegistry.TryGetClassInfo(currentName, out var classInfo) || classInfo == null)
            {
                break;
            }

            var baseName = classInfo.BaseClassName;
            if (!string.IsNullOrWhiteSpace(baseName) && baseName.Equals(QualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (baseName == null)
            {
                break;
            }

            currentName = baseName;
        }

        if (!targetInfo.IsInterface)
        {
            return false;
        }

        if (!PeopleCodeTypeRegistry.TryGetClassInfo(appClass.QualifiedName, out var otherInfo) || otherInfo == null)
        {
            return false;
        }

        return ImplementsInterface(otherInfo, QualifiedName);

        static bool ImplementsInterface(ClassTypeInfo info, string interfaceName)
        {
            var classVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var interfaceVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return ImplementsInterfaceRecursive(info, interfaceName, classVisited, interfaceVisited);
        }

        static bool ImplementsInterfaceRecursive(
            ClassTypeInfo info,
            string interfaceName,
            HashSet<string> visitedClasses,
            HashSet<string> visitedInterfaces)
        {
            foreach (var implemented in info.ImplementedInterfaces)
            {
                if (implemented.Equals(interfaceName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (visitedInterfaces.Add(implemented) &&
                    PeopleCodeTypeRegistry.TryGetClassInfo(implemented, out var implementedInfo) &&
                    implementedInfo != null &&
                    ImplementsInterfaceRecursive(implementedInfo, interfaceName, visitedClasses, visitedInterfaces))
                {
                    return true;
                }
            }

            if (!info.IsInterface &&
                !string.IsNullOrWhiteSpace(info.BaseClassName) &&
                visitedClasses.Add(info.BaseClassName) &&
                PeopleCodeTypeRegistry.TryGetClassInfo(info.BaseClassName!, out var baseInfo) &&
                baseInfo != null)
            {
                if (ImplementsInterfaceRecursive(baseInfo, interfaceName, visitedClasses, visitedInterfaces))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

/// <summary>
/// Represents an array type (ARRAY OF type)
/// </summary>
public class ArrayTypeInfo : TypeInfo
{
    public override string Name => ElementType != null ? $"array of {ElementType.Name}" : "array";
    public override TypeKind Kind => TypeKind.Array;

    /// <summary>
    /// Number of dimensions (1 for ARRAY, 2 for ARRAY2, etc.)
    /// </summary>
    public int Dimensions { get; }

    /// <summary>
    /// Element type, null for untyped arrays
    /// </summary>
    public TypeInfo? ElementType { get; }

    public ArrayTypeInfo(int dimensions = 1, TypeInfo? elementType = null)
    {
        if (dimensions < 1 || dimensions > 9)
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Array dimensions must be between 1 and 9");

        Dimensions = dimensions;
        ElementType = elementType;
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Any can be assigned to any array
        if (other.Kind == TypeKind.Any) return true;

        // Same array type with compatible element types
        if (other is ArrayTypeInfo array && array.Dimensions == Dimensions)
        {
            // Untyped arrays are compatible
            if (ElementType == null || array.ElementType == null) return true;

            // Check element type compatibility
            return ElementType.IsAssignableFrom(array.ElementType);
        }

        return false;
    }
}

/// <summary>
/// Represents the special "Any" type that can hold any value in PeopleCode
/// </summary>
public class AnyTypeInfo : TypeInfo
{
    public override string Name => "any";
    public override TypeKind Kind => TypeKind.Any;
    public override PeopleCodeType? PeopleCodeType => TypeSystem.PeopleCodeType.Any;

    // Singleton instance
    public static readonly AnyTypeInfo Instance = new();

    private AnyTypeInfo() { }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Any can accept any type
        return true;
    }

    public override TypeInfo GetCommonType(TypeInfo other)
    {
        // Any is the ultimate common type
        return this;
    }
}

/// <summary>
/// Represents a "void" type for functions/methods that don't return a value
/// This helps catch errors where users try to assign the result of void functions
/// </summary>
public class VoidTypeInfo : TypeInfo
{
    public override string Name => "void";
    public override TypeKind Kind => TypeKind.Void;
    public override PeopleCodeType? PeopleCodeType => TypeSystem.PeopleCodeType.Void;
    public override bool IsNullable => false; // Void cannot be assigned or be null

    // Singleton instance
    public static readonly VoidTypeInfo Instance = new();

    private VoidTypeInfo() { }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Void cannot accept any assignment
        // The only exception might be Any in very special cases, but for strict checking, return false
        return false;
    }

    public override TypeInfo GetCommonType(TypeInfo other)
    {
        // Void has no meaningful common type with anything
        // This should generally be an error condition
        return UnknownTypeInfo.Instance;
    }
}

/// <summary>
/// Represents an unknown or unresolved type
/// </summary>
public class UnknownTypeInfo : TypeInfo
{
    public override string Name { get; }
    public override TypeKind Kind => TypeKind.Unknown;
    public override PeopleCodeType? PeopleCodeType => Name.Equals("unknown", StringComparison.OrdinalIgnoreCase) ? TypeSystem.PeopleCodeType.Unknown : null;

    // Common instance for truly unknown types
    public static readonly UnknownTypeInfo Instance = new("unknown");

    public UnknownTypeInfo(string name = "unknown")
    {
        Name = name ?? "unknown";
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Unknown types are not assignable (except from Any)
        return other.Kind == TypeKind.Any;
    }

    public override TypeInfo GetCommonType(TypeInfo other)
    {
        // If we can't resolve this type, default to Any
        return AnyTypeInfo.Instance;
    }
}

/// <summary>
/// Represents a reference type for named references like HTML.OBJECT_NAME, SQL.FOO, RECORD.FOO
/// Also includes dynamic references via @ expressions
/// </summary>
public class ReferenceTypeInfo : TypeInfo
{
    public override TypeKind Kind => TypeKind.Reference;
    public override PeopleCodeType? PeopleCodeType => TypeSystem.PeopleCodeType.Reference;

    /// <summary>
    /// The specific reference type identifier (for static references)
    /// </summary>
    public ReferenceTypeIdentifier? ReferenceIdentifier { get; }

    /// <summary>
    /// The member name (for static references)
    /// </summary>
    public string? MemberName { get; }

    /// <summary>
    /// Whether this is a dynamic reference (using @ operator)
    /// </summary>
    public bool IsDynamic { get; }

    // Singleton instance for generic reference type
    public static readonly ReferenceTypeInfo Instance = new();

    // Private constructor for singleton
    private ReferenceTypeInfo()
    {
        IsDynamic = false;
    }

    /// <summary>
    /// Constructor for specific static reference types
    /// </summary>
    /// <param name="referenceIdentifier">The reference type identifier</param>
    /// <param name="memberName">The member name</param>
    public ReferenceTypeInfo(ReferenceTypeIdentifier referenceIdentifier, string memberName)
    {
        ReferenceIdentifier = referenceIdentifier;
        MemberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
        IsDynamic = false;
    }

    /// <summary>
    /// Constructor for dynamic reference types
    /// </summary>
    /// <param name="isDynamic">Must be true for dynamic references</param>
    public ReferenceTypeInfo(bool isDynamic)
    {
        if (!isDynamic)
            throw new ArgumentException("Use parameterless constructor for non-dynamic references", nameof(isDynamic));

        IsDynamic = true;
    }

    public override string Name =>
        ReferenceIdentifier.HasValue && !string.IsNullOrEmpty(MemberName)
            ? $"{ReferenceIdentifier.Value}.{MemberName}"
            : IsDynamic ? "reference (dynamic)" : "reference";

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Any can be assigned to reference
        if (other.Kind == TypeKind.Any) return true;

        // Same reference type
        if (other.Kind == TypeKind.Reference) return true;

        // String can be converted to reference (for dynamic references)
        if (other.Kind == TypeKind.Primitive && other is PrimitiveTypeInfo primitive &&
            primitive.Name.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public override TypeInfo GetCommonType(TypeInfo other)
    {
        // If both are references, return the more general one
        if (other is ReferenceTypeInfo otherRef)
        {
            // If both are the same specific reference, return this
            if (ReferenceIdentifier.HasValue && otherRef.ReferenceIdentifier.HasValue &&
                ReferenceIdentifier.Value == otherRef.ReferenceIdentifier.Value &&
                MemberName == otherRef.MemberName)
            {
                return this;
            }

            // Otherwise return generic reference type
            return Instance;
        }

        // Default to Any for everything else
        return AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Creates a ReferenceTypeInfo from a member access expression
    /// </summary>
    /// <param name="memberAccess">The member access node</param>
    /// <returns>A ReferenceTypeInfo instance, or null if not a valid reference</returns>
    public static ReferenceTypeInfo? FromMemberAccess(Nodes.MemberAccessNode memberAccess)
    {
        var validation = ReferenceTypeValidation.ValidateStaticReference(memberAccess);
        if (!validation.IsValid || !validation.ReferenceType.HasValue)
            return null;

        return new ReferenceTypeInfo(validation.ReferenceType.Value, validation.MemberName!);
    }

    /// <summary>
    /// Creates a ReferenceTypeInfo from a unary operation expression (@ operator)
    /// </summary>
    /// <param name="unaryOp">The unary operation node</param>
    /// <returns>A dynamic ReferenceTypeInfo instance, or null if not a valid reference</returns>
    public static ReferenceTypeInfo? FromUnaryOperation(Nodes.UnaryOperationNode unaryOp)
    {
        var validation = ReferenceTypeValidation.ValidateDynamicReference(unaryOp);
        if (!validation.IsValid)
            return null;

        return new ReferenceTypeInfo(isDynamic: true);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ReferenceTypeInfo other) return false;

        // Use PeopleCode type comparison first if available
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue)
        {
            // For references, also compare the specific details
            return PeopleCodeType.Value == other.PeopleCodeType.Value &&
                   ReferenceIdentifier == other.ReferenceIdentifier &&
                   MemberName == other.MemberName &&
                   IsDynamic == other.IsDynamic;
        }

        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        if (PeopleCodeType.HasValue)
        {
            return HashCode.Combine(PeopleCodeType.Value, ReferenceIdentifier, MemberName, IsDynamic);
        }

        return base.GetHashCode();
    }
}

/// <summary>
/// Specialized string type that cannot be assigned null values
/// </summary>
public class StringTypeInfo : PrimitiveTypeInfo
{
    public StringTypeInfo() : base("string", TypeSystem.PeopleCodeType.String)
    {
    }

    public override bool IsNullable => false; // String cannot be assigned null in PeopleCode

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Any can be assigned to string
        if (other.Kind == TypeKind.Any) return true;

        // Same type (string)
        if (other.PeopleCodeType.HasValue && other.PeopleCodeType.Value == TypeSystem.PeopleCodeType.String) return true;
        if (other is PrimitiveTypeInfo primitive && primitive.Name.Equals("string", StringComparison.OrdinalIgnoreCase)) return true;

        // String can ONLY accept string values - no implicit conversions
        return false;
    }
}

/// <summary>
/// Specialized number type that accepts both number and integer values
/// </summary>
public class NumberTypeInfo : PrimitiveTypeInfo
{
    public NumberTypeInfo() : base("number", TypeSystem.PeopleCodeType.Number)
    {
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Any can be assigned to number
        if (other.Kind == TypeKind.Any) return true;

        // Same type (number)
        if (other.PeopleCodeType.HasValue && other.PeopleCodeType.Value == TypeSystem.PeopleCodeType.Number) return true;
        if (other is PrimitiveTypeInfo primitive && primitive.Name.Equals("number", StringComparison.OrdinalIgnoreCase)) return true;

        // Number accepts integer (bidirectional compatibility)
        if (other.PeopleCodeType.HasValue && other.PeopleCodeType.Value == TypeSystem.PeopleCodeType.Integer) return true;
        if (other is PrimitiveTypeInfo intPrimitive && intPrimitive.Name.Equals("integer", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}

/// <summary>
/// Represents type errors found during type inference
/// </summary>
public class TypeError
{
    public string Message { get; }
    public SourceSpan Location { get; }
    public TypeErrorKind Kind { get; }
    public TypeInfo? ExpectedType { get; }
    public TypeInfo? ActualType { get; }

    public TypeError(string message, SourceSpan location, TypeErrorKind kind = TypeErrorKind.General,
                    TypeInfo? expectedType = null, TypeInfo? actualType = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Location = location;
        Kind = kind;
        ExpectedType = expectedType;
        ActualType = actualType;
    }

    public override string ToString()
    {
        return $"{Location}: {Message}";
    }
}

/// <summary>
/// Represents type warnings found during type inference
/// </summary>
public class TypeWarning
{
    public string Message { get; }
    public SourceSpan Location { get; }

    public TypeWarning(string message, SourceSpan location)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Location = location;
    }

    public override string ToString()
    {
        return $"{Location}: {Message}";
    }
}

/// <summary>
/// Categories of type errors
/// </summary>
public enum TypeErrorKind
{
    General,
    TypeMismatch,
    UnknownType,
    UnknownFunction,
    UnknownMethod,
    ArgumentCountMismatch,
    UnresolvableReference,
    VoidAssignment
}
