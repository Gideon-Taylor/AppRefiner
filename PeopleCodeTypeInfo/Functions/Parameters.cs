using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Tag IDs for parameter serialization - each concrete parameter type has a unique tag
/// </summary>
public enum ParameterTag : byte
{
    Single = 1,
    Union = 2,
    Group = 3,
    Variable = 4,
    Reference = 5
}

/// <summary>
/// Base class for all parameter types in the composable parameter system.
/// Provides a hierarchical, composable approach to modeling complex PeopleCode function signatures.
/// </summary>
public abstract class Parameter
{

    public string Name { get; set; } = "";
    public int? NameIndex { get; set; }


    /// <summary>
    /// Unique tag ID for this parameter type, used for serialization
    /// </summary>
    public abstract ParameterTag Tag { get; }

    /// <summary>
    /// Whether this parameter (or parameter group) is optional
    /// </summary>
    public abstract bool IsOptional { get; }

    /// <summary>
    /// Validate if the given number of arguments matches this parameter's requirements
    /// </summary>
    /// <param name="argCount">Number of arguments provided</param>
    /// <returns>True if argument count is valid for this parameter</returns>
    public abstract bool ValidateArgumentCount(int argCount);

    /// <summary>
    /// Validate if the given argument types match this parameter's type requirements
    /// </summary>
    /// <param name="argTypes">Types of arguments provided</param>
    /// <returns>True if argument types are compatible with this parameter</returns>
    public abstract bool ValidateArgumentTypes(TypeInfo[] argTypes);

    /// <summary>
    /// Get the minimum number of arguments this parameter requires
    /// </summary>
    public abstract int MinArgumentCount { get; }

    /// <summary>
    /// Get the maximum number of arguments this parameter can accept
    /// </summary>
    public abstract int MaxArgumentCount { get; }
}

/// <summary>
/// Type information that includes array dimensionality and AppClass path support
/// PeopleCode arrays are always dynamic-sized, no fixed sizing
/// </summary>
public struct TypeWithDimensionality : IEquatable<TypeWithDimensionality>
{
    public PeopleCodeType Type { get; set; }
    public byte ArrayDimensionality { get; set; } // 0=scalar, 1=1D array, 2=2D array, etc.
    public string? AppClassPath { get; set; } // For AppClass types: "PACKAGE:Class", "PACKAGE:SubPackage:Class", etc.
    public bool IsReference { get; set; } // True for references (@RECORD, @FIELD), false for instances

    public TypeWithDimensionality(PeopleCodeType type, byte arrayDimensionality = 0, string? appClassPath = null, bool isReference = false)
    {
        Type = type;
        ArrayDimensionality = arrayDimensionality;
        AppClassPath = appClassPath;
        IsReference = isReference;
    }

    public bool IsArray => ArrayDimensionality > 0;
    public bool IsScalar => ArrayDimensionality == 0;
    public bool IsAppClass => Type == PeopleCodeType.AppClass && !string.IsNullOrEmpty(AppClassPath);

    public override bool Equals(object? obj) => obj is TypeWithDimensionality other && Equals(other);

    public bool Equals(TypeWithDimensionality other) =>
        Type == other.Type &&
        ArrayDimensionality == other.ArrayDimensionality &&
        AppClassPath == other.AppClassPath &&
        IsReference == other.IsReference;

    public override int GetHashCode() => HashCode.Combine(Type, ArrayDimensionality, AppClassPath, IsReference);

    public static bool operator ==(TypeWithDimensionality left, TypeWithDimensionality right) => left.Equals(right);
    public static bool operator !=(TypeWithDimensionality left, TypeWithDimensionality right) => !left.Equals(right);

    public override string ToString()
    {
        var baseTypeName = Type == PeopleCodeType.AppClass && !string.IsNullOrEmpty(AppClassPath)
            ? AppClassPath
            : Type.ToString().ToLowerInvariant();

        // Handle reference types with @ prefix
        if (IsReference)
        {
            if (ArrayDimensionality == 0)
                return $"@{baseTypeName}";
            var refArrayPrefix = string.Join("", Enumerable.Repeat("array_", ArrayDimensionality));
            return $"@{refArrayPrefix}{baseTypeName}";
        }

        // Handle regular types
        if (ArrayDimensionality == 0)
            return Type == PeopleCodeType.AppClass ? baseTypeName : $"{baseTypeName}";

        var regularArrayPrefix = string.Join("", Enumerable.Repeat("array_", ArrayDimensionality));
        return $"{regularArrayPrefix}{baseTypeName}";
    }

    /// <summary>
    /// Create a TypeWithDimensionality for an AppClass with package path
    /// </summary>
    public static TypeWithDimensionality CreateAppClass(string packagePath, byte arrayDimensionality = 0)
    {
        return new TypeWithDimensionality(PeopleCodeType.AppClass, arrayDimensionality, packagePath);
    }

    /// <summary>
    /// Create a TypeWithDimensionality for a built-in type
    /// </summary>
    public static TypeWithDimensionality CreateBuiltIn(PeopleCodeType type, byte arrayDimensionality = 0)
    {
        return new TypeWithDimensionality(type, arrayDimensionality);
    }

    /// <summary>
    /// Parse a type string that may include AppClass paths like "PACKAGE:Class",
    /// reference types like "@FIELD", and array dimensionality via leading "array_" prefixes.
    /// Preserves the reference flag indicated by the '@' prefix.
    /// </summary>
    public static TypeWithDimensionality Parse(string typeStr)
    {
        if (typeStr == null) throw new ArgumentNullException(nameof(typeStr));
        typeStr = typeStr.Trim();

        // Detect reference '@' first and strip it for further parsing
        bool isReference = false;
        if (typeStr.StartsWith("@"))
        {
            isReference = true;
            typeStr = typeStr.Substring(1);
        }

        // Handle array notation: array_type, array_array_type, etc. on the remaining string
        byte arrayDimensionality = 0;
        while (typeStr.StartsWith("array_"))
        {
            arrayDimensionality++;
            typeStr = typeStr.Substring(6); // Remove "array_"
        }

        // AppClass path (contains colon)
        if (typeStr.Contains(':'))
        {
            // AppClass references are not expected; treat as regular AppClass
            return new TypeWithDimensionality(PeopleCodeType.AppClass, arrayDimensionality, typeStr, isReference: false);
        }

        // Builtin or special type
        var builtinType = BuiltinTypeExtensions.FromString(typeStr);
        return new TypeWithDimensionality(builtinType, arrayDimensionality, null, isReference);
    }

    /// <summary>
    /// Parse reference type from string like "FIELD", "RECORD", "SCROLL"
    /// </summary>
    private static PeopleCodeType ParseReferenceType(string refTypeName)
    {
        return refTypeName.ToUpperInvariant() switch
        {
            "FIELD" => PeopleCodeType.Field,
            "RECORD" => PeopleCodeType.Record,
            "SCROLL" => PeopleCodeType.Scroll,
            "ROW" => PeopleCodeType.Row,
            "ROWSET" => PeopleCodeType.Rowset,
            "PAGE" => PeopleCodeType.Page,
            "COMPONENT" => PeopleCodeType.Object, // COMPONENT doesn't exist, using Object
            "GRID" => PeopleCodeType.Grid,
            "CHART" => PeopleCodeType.Chart,
            "PANEL" => PeopleCodeType.Panel,
            _ => PeopleCodeType.Object // Default for unrecognized reference types
        };
    }

    /// <summary>
    /// Validate AppClass path format
    /// </summary>
    public bool IsValidAppClassPath()
    {
        if (!IsAppClass) return true; // Not an AppClass, so path validation doesn't apply

        if (string.IsNullOrEmpty(AppClassPath)) return false;

        // Basic validation: should contain at least one colon and valid identifiers
        var parts = AppClassPath.Split(':');
        if (parts.Length < 2) return false;

        // Each part should be a valid identifier (simplified check)
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part) || !IsValidIdentifier(part))
                return false;
        }

        return true;
    }

    private static bool IsValidIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return false;

        // Simple check: starts with letter or underscore, contains only letters, digits, underscores
        return char.IsLetter(identifier[0]) || identifier[0] == '_' &&
               identifier.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}

/// <summary>
/// Single parameter that accepts one specific type (with optional array dimensionality)
/// </summary>
public class SingleParameter : Parameter
{
    public TypeWithDimensionality ParameterType { get; set; }

    public override ParameterTag Tag => ParameterTag.Single;
    public override bool IsOptional => false;
    public override int MinArgumentCount => 1;
    public override int MaxArgumentCount => 1;

    public SingleParameter() { }

    public SingleParameter(PeopleCodeType type, byte arrayDimensionality = 0, string name = "")
    {
        ParameterType = new TypeWithDimensionality(type, arrayDimensionality);
        Name = name;
    }

    public SingleParameter(TypeWithDimensionality parameterType, string name = "")
    {
        ParameterType = parameterType;
        Name = name;
    }

    /// <summary>
    /// Create a SingleParameter for an AppClass type
    /// </summary>
    public static SingleParameter CreateAppClass(string appClassPath, byte arrayDimensionality = 0, string name = "")
    {
        return new SingleParameter(TypeWithDimensionality.CreateAppClass(appClassPath, arrayDimensionality), name);
    }

    public override bool ValidateArgumentCount(int argCount) => argCount == 1;

    public override bool ValidateArgumentTypes(TypeInfo[] argTypes)
    {
        if (argTypes.Length != 1) return false;

        var argType = argTypes[0];

        // Convert ParameterType to TypeInfo for comparison
        TypeInfo expectedType;
        if (ParameterType.IsAppClass)
        {
            expectedType = new AppClassTypeInfo(ParameterType.AppClassPath!);
        }
        else
        {
            expectedType = TypeInfo.FromPeopleCodeType(ParameterType.Type);
        }

        // Handle array types
        if (ParameterType.IsArray)
        {
            expectedType = new ArrayTypeInfo(ParameterType.ArrayDimensionality, expectedType);
        }

        // Use the new assignability checking
        return expectedType.IsAssignableFrom(argType);
    }

    public override string ToString() => $"{(string.IsNullOrEmpty(Name) ? "": Name + ": ")}{ParameterType}";
}

/// <summary>
/// Union parameter that accepts one of several possible types
/// Examples: &string | &array_string, @(RECORD) | @(SCROLL)
/// </summary>
public class UnionParameter : Parameter
{
    public List<TypeWithDimensionality> AllowedTypes { get; set; } = new();

    public override ParameterTag Tag => ParameterTag.Union;
    public override bool IsOptional => false;
    public override int MinArgumentCount => 1;
    public override int MaxArgumentCount => 1;

    public UnionParameter() { }

    public UnionParameter(IEnumerable<TypeWithDimensionality> allowedTypes, string name = "")
    {
        AllowedTypes = allowedTypes.ToList();
        Name = name;
    }

    /// <summary>
    /// Add an AppClass type to the union
    /// </summary>
    public UnionParameter AddAppClass(string appClassPath, byte arrayDimensionality = 0)
    {
        AllowedTypes.Add(TypeWithDimensionality.CreateAppClass(appClassPath, arrayDimensionality));
        return this;
    }

    /// <summary>
    /// Add a built-in type to the union
    /// </summary>
    public UnionParameter AddBuiltIn(PeopleCodeType type, byte arrayDimensionality = 0)
    {
        AllowedTypes.Add(TypeWithDimensionality.CreateBuiltIn(type, arrayDimensionality));
        return this;
    }

    /// <summary>
    /// Create a UnionParameter that accepts either a built-in type or AppClass types
    /// </summary>
    public static UnionParameter CreateMixed(PeopleCodeType builtInType, params string[] appClassPaths)
    {
        var types = new List<TypeWithDimensionality>
        {
            TypeWithDimensionality.CreateBuiltIn(builtInType)
        };

        foreach (var path in appClassPaths)
        {
            types.Add(TypeWithDimensionality.CreateAppClass(path));
        }

        return new UnionParameter(types);
    }

    public override bool ValidateArgumentCount(int argCount) => argCount == 1;

    public override bool ValidateArgumentTypes(TypeInfo[] argTypes)
    {
        if (argTypes.Length != 1) return false;

        var argType = argTypes[0];

        // Check if the argument type is assignable to any of the allowed types
        foreach (var allowedType in AllowedTypes)
        {
            // Convert the allowed TypeWithDimensionality to a TypeInfo for comparison
            TypeInfo expectedType;
            if (allowedType.IsAppClass)
            {
                expectedType = new AppClassTypeInfo(allowedType.AppClassPath!);
            }
            else
            {
                expectedType = TypeInfo.FromPeopleCodeType(allowedType.Type);
            }

            // Handle array types
            if (allowedType.IsArray)
            {
                expectedType = new ArrayTypeInfo(allowedType.ArrayDimensionality, expectedType);
            }

            // Use the new assignability checking
            if (expectedType.IsAssignableFrom(argType))
            {
                return true;
            }
        }

        // Check for Any type fallback
        return AllowedTypes.Any(t => t.Type == PeopleCodeType.Any);
    }
    public override string ToString() => $"{(string.IsNullOrEmpty(Name) ? "" : Name + ": ")}[{string.Join(" | ", AllowedTypes)}]";
}

/// <summary>
/// Parameter group that represents a sequence of parameters that must appear together
/// Example: (@(FIELD), &string) - both field and string must be provided as a pair
/// </summary>
public class ParameterGroup : Parameter
{
    public List<Parameter> Parameters { get; set; } = new();

    public override ParameterTag Tag => ParameterTag.Group;
    public override bool IsOptional => Parameters.All(p => p.IsOptional);

    public override int MinArgumentCount => Parameters.Sum(p => p.MinArgumentCount);
    public override int MaxArgumentCount => Parameters.Sum(p => p.MaxArgumentCount);

    public ParameterGroup() { }

    public ParameterGroup(IEnumerable<Parameter> parameters, string name = "")
    {
        Parameters = parameters.ToList();
        Name = name;
    }

    public override bool ValidateArgumentCount(int argCount)
    {
        return argCount >= MinArgumentCount && argCount <= MaxArgumentCount;
    }

    public override bool ValidateArgumentTypes(TypeInfo[] argTypes)
    {
        int argIndex = 0;

        foreach (var param in Parameters)
        {
            var paramArgCount = Math.Min(param.MaxArgumentCount, argTypes.Length - argIndex);

            if (paramArgCount < param.MinArgumentCount)
                return false;

            var paramArgs = argTypes.Skip(argIndex).Take(paramArgCount).ToArray();

            if (!param.ValidateArgumentTypes(paramArgs))
                return false;

            argIndex += paramArgCount;
        }

        return argIndex == argTypes.Length;
    }

    public override string ToString() => $"{(string.IsNullOrEmpty(Name) ? "" : Name + ": ")} ({string.Join(", ", Parameters)})";
}


/// <summary>
/// Variable parameter (varargs) that can repeat with min/max constraints
/// Examples:
/// - &any... (min=0, max=unlimited)
/// - (@(FIELD), &string)+ (min=1, max=unlimited)
/// - [@(SCROLL)...{0-2}] (min=0, max=2)
/// </summary>
public class VariableParameter : Parameter
{
    public Parameter InnerParameter { get; set; } = null!;
    public int MinCount { get; set; } = 1;
    public int MaxCount { get; set; } = int.MaxValue;

    public override ParameterTag Tag => ParameterTag.Variable;
    public override bool IsOptional => MinCount == 0;

    public override int MinArgumentCount => MinCount * (InnerParameter?.MinArgumentCount ?? 0);
    public override int MaxArgumentCount =>
        MaxCount == int.MaxValue ? int.MaxValue : MaxCount * (InnerParameter?.MaxArgumentCount ?? 0);

    public bool IsUnlimited => MaxCount == int.MaxValue;

    public VariableParameter() { }

    public VariableParameter(Parameter innerParameter, int minCount = 1, int maxCount = int.MaxValue, string name = "")
    {
        InnerParameter = innerParameter;
        MinCount = minCount;
        MaxCount = maxCount;
        Name = name;
    }

    public override bool ValidateArgumentCount(int argCount)
    {
        if (InnerParameter == null) return false;

        var innerArgCount = InnerParameter.MinArgumentCount;
        if (innerArgCount == 0) return true; // Can't validate if inner takes 0 args

        var repetitions = argCount / innerArgCount;
        var remainder = argCount % innerArgCount;

        // Must be complete repetitions
        if (remainder != 0) return false;

        return repetitions >= MinCount && repetitions <= MaxCount;
    }

    public override bool ValidateArgumentTypes(TypeInfo[] argTypes)
    {
        if (InnerParameter == null) return false;

        var innerArgCount = InnerParameter.MinArgumentCount;
        if (innerArgCount == 0) return true;

        var repetitions = argTypes.Length / innerArgCount;

        if (!ValidateArgumentCount(argTypes.Length)) return false;

        // Validate each repetition
        for (int i = 0; i < repetitions; i++)
        {
            var repetitionArgs = argTypes.Skip(i * innerArgCount).Take(innerArgCount).ToArray();
            if (!InnerParameter.ValidateArgumentTypes(repetitionArgs))
                return false;
        }

        return true;
    }

    public override string ToString()
    {
        var suffix = (MinCount, MaxCount) switch
        {
            (0, int.MaxValue) => "*",
            (1, int.MaxValue) => "+",
            (0, 1) => "?",
            var (min, max) when min == max => $"{{{min}}}",
            var (min, max) when max == int.MaxValue => $"{{{min}-}}",
            var (min, max) => $"{{{min}-{max}}}"
        };

        return $"{(string.IsNullOrEmpty(Name) ? "" : Name + ": ")}{InnerParameter}{suffix}";
    }
}

/// <summary>
/// Represents a reference parameter (e.g., @RECORD, @FIELD, @ANY)
/// References are not instances - they point to definitions.
/// Example: CreateRecord(@RECORD) expects a record reference like Record.MY_RECORD
/// </summary>
public class ReferenceParameter : Parameter
{
    public override ParameterTag Tag => ParameterTag.Reference;

    /// <summary>
    /// The category of reference required (Field, Record, SQL, etc.)
    /// Use PeopleCodeType.Any for @ANY (accepts any reference)
    /// </summary>
    public PeopleCodeType ReferenceCategory { get; }

    public override bool IsOptional { get; }
    public override int MinArgumentCount => IsOptional ? 0 : 1;
    public override int MaxArgumentCount => 1;

    public ReferenceParameter(PeopleCodeType referenceCategory, bool isOptional = false)
    {
        ReferenceCategory = referenceCategory;
        IsOptional = isOptional;
    }

    public override bool ValidateArgumentCount(int argCount)
    {
        if (IsOptional) return argCount == 0 || argCount == 1;
        return argCount == 1;
    }

    public override bool ValidateArgumentTypes(TypeInfo[] argTypes)
    {
        if (argTypes.Length == 0) return IsOptional;
        if (argTypes.Length > 1) return false;

        var argType = argTypes[0];

        // Must be a reference type
        if (argType is not ReferenceTypeInfo refType)
            return false;

        // If we accept any reference (@ANY), allow it
        if (ReferenceCategory == PeopleCodeType.Any)
            return true;

        // Otherwise, must match the specific category
        return refType.ReferenceCategory == ReferenceCategory;
    }

    public override string ToString()
    {
        var refName = ReferenceCategory == PeopleCodeType.Any
            ? "@ANY"
            : $"@{ReferenceCategory.GetTypeName().ToUpperInvariant()}";

        return IsOptional ? $"{(string.IsNullOrEmpty(Name) ? "" : Name + ": ")} [{refName}]" : refName;
    }
}
