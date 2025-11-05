using PeopleCodeTypeInfo.Types;
using System.Text;

namespace PeopleCodeTypeInfo.Functions;

public enum MemberVisibility
{
    Public,
    Protected,
    Private
}

/// <summary>
/// Represents a property with type information including array dimensionality and AppClass path support.
/// Used for both system variables and object properties.
/// </summary>
public class PropertyInfo : IEquatable<PropertyInfo>
{
    /// <summary>
    /// The name of the property
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Index into the name table for efficient storage (when using name table format)
    /// </summary>
    public int? NameIndex { get; set; }

    /// <summary>
    /// Visibility modifier for this function (set at runtime for app class methods)
    /// </summary>
    public MemberVisibility Visibility { get; set; } = MemberVisibility.Public;

    /// <summary>
    /// The base type of the property
    /// </summary>
    public PeopleCodeType Type { get; set; }

    /// <summary>
    /// Array dimensionality (0=scalar, 1=1D array, 2=2D array, etc.)
    /// </summary>
    public byte ArrayDimensionality { get; set; }

    /// <summary>
    /// AppClass path for custom application classes (e.g., "PACKAGE:Class")
    /// </summary>
    public string? AppClassPath { get; set; }

    /// <summary>
    /// Union types when the property can be multiple types
    /// When null or empty, use Type. When populated, this takes precedence.
    /// </summary>
    public List<TypeWithDimensionality>? UnionTypes { get; set; }

    /// <summary>
    /// Whether the return value is optional (caller can ignore it)
    /// Indicated by ? suffix in return type (e.g., "-> string?")
    /// </summary>
    public bool IsOptionalReturn { get; set; }

    public PropertyInfo(PeopleCodeType type, byte arrayDimensionality = 0, string? appClassPath = null)
    {
        Type = type;
        ArrayDimensionality = arrayDimensionality;
        AppClassPath = appClassPath;
    }

    /// <summary>
    /// Whether this property is an array
    /// </summary>
    public bool IsArray => ArrayDimensionality > 0;

    /// <summary>
    /// Whether this property is a scalar (non-array)
    /// </summary>
    public bool IsScalar => ArrayDimensionality == 0;

    /// <summary>
    /// Whether this property represents an AppClass type
    /// </summary>
    public bool IsAppClass => Type == PeopleCodeType.AppClass && !string.IsNullOrEmpty(AppClassPath);

    /// <summary>
    /// Whether this property has a union type (multiple possible types)
    /// </summary>
    public bool IsUnion => UnionTypes != null && UnionTypes.Count > 1;

    /// <summary>
    /// Create a PropertyInfo for a scalar built-in type
    /// </summary>
    public static PropertyInfo CreateScalar(PeopleCodeType type)
    {
        return new PropertyInfo(type, 0);
    }

    /// <summary>
    /// Create a PropertyInfo for an array of a built-in type
    /// </summary>
    public static PropertyInfo CreateArray(PeopleCodeType type, byte arrayDimensionality)
    {
        return new PropertyInfo(type, arrayDimensionality);
    }

    /// <summary>
    /// Create a PropertyInfo for an AppClass type
    /// </summary>
    public static PropertyInfo CreateAppClass(string appClassPath, byte arrayDimensionality = 0)
    {
        return new PropertyInfo(PeopleCodeType.AppClass, arrayDimensionality, appClassPath);
    }

    /// <summary>
    /// Create a PropertyInfo for a union type
    /// </summary>
    public static PropertyInfo CreateUnion(params TypeWithDimensionality[] unionTypes)
    {
        if (unionTypes == null || unionTypes.Length == 0)
            throw new ArgumentException("Union types cannot be empty", nameof(unionTypes));

        var property = new PropertyInfo(unionTypes[0].Type, unionTypes[0].ArrayDimensionality, unionTypes[0].AppClassPath)
        {
            UnionTypes = unionTypes.ToList()
        };

        return property;
    }

    /// <summary>
    /// Get the TypeWithDimensionality representation
    /// </summary>
    public TypeWithDimensionality ToTypeWithDimensionality()
    {
        return new TypeWithDimensionality(Type, ArrayDimensionality, AppClassPath);
    }

    /// <summary>
    /// Create PropertyInfo from TypeWithDimensionality
    /// </summary>
    public static PropertyInfo FromTypeWithDimensionality(TypeWithDimensionality typeWithDim)
    {
        return new PropertyInfo(typeWithDim.Type, typeWithDim.ArrayDimensionality, typeWithDim.AppClassPath);
    }

    public override bool Equals(object? obj) => obj is PropertyInfo other && Equals(other);

    public bool Equals(PropertyInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Type == other.Type &&
               ArrayDimensionality == other.ArrayDimensionality &&
               AppClassPath == other.AppClassPath;
    }

    public override int GetHashCode() => HashCode.Combine(Type, ArrayDimensionality, AppClassPath);

    public static bool operator ==(PropertyInfo? left, PropertyInfo? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(PropertyInfo? left, PropertyInfo? right) => !(left == right);

    public override string ToString()
    {
        if (IsUnion)
        {
            var typeStrings = UnionTypes!.Select(t => t.ToString());
            return string.Join("|", typeStrings);
        }

        var baseTypeName = Type == PeopleCodeType.AppClass && !string.IsNullOrEmpty(AppClassPath)
            ? AppClassPath
            : Type.ToString().ToLowerInvariant();

        if (ArrayDimensionality == 0)
            return Type == PeopleCodeType.AppClass ? baseTypeName : $"{baseTypeName}";

        var arrayPrefix = string.Join("", Enumerable.Repeat("array_", ArrayDimensionality));
        return $"{arrayPrefix}{baseTypeName}";
    }
}

/// <summary>
/// Extension methods for PropertyInfo serialization and utilities
/// </summary>
public static class PropertyInfoExtensions
{
    /// <summary>
    /// Serialize PropertyInfo to binary writer
    /// </summary>
    public static void Write(this BinaryWriter writer, PropertyInfo propertyInfo)
    {
        // Write flags byte to indicate features (union types, etc.)
        byte flags = 0;
        if (propertyInfo.IsUnion) flags |= 0x01; // Bit 0: Has union types
        if (propertyInfo.IsOptionalReturn) flags |= 0x02; // Bit 1: Optional return

        writer.Write(flags);

        if (propertyInfo.IsUnion)
        {
            // Write union types
            writer.Write((byte)propertyInfo.UnionTypes!.Count);
            foreach (var unionType in propertyInfo.UnionTypes)
            {
                writer.Write((byte)unionType.Type);
                writer.Write(unionType.ArrayDimensionality);
                WriteString(writer, unionType.AppClassPath ?? "");
            }
        }
        else
        {
            // Write single type (legacy format)
            writer.Write((byte)propertyInfo.Type);
            writer.Write(propertyInfo.ArrayDimensionality);
            WriteString(writer, propertyInfo.AppClassPath ?? "");
        }
    }

    /// <summary>
    /// Deserialize PropertyInfo from binary reader
    /// </summary>
    public static PropertyInfo ReadPropertyInfo(this BinaryReader reader)
    {
        byte flags = reader.ReadByte();
        bool hasUnionTypes = (flags & 0x01) != 0;
        bool isOptionalReturn = (flags & 0x02) != 0;

        if (hasUnionTypes)
        {
            // Read union types
            byte unionCount = reader.ReadByte();
            var unionTypes = new List<TypeWithDimensionality>();

            for (int i = 0; i < unionCount; i++)
            {
                var type = (PeopleCodeType)reader.ReadByte();
                var arrayDim = reader.ReadByte();
                var appClassPath = ReadString(reader);

                unionTypes.Add(string.IsNullOrEmpty(appClassPath)
                    ? new TypeWithDimensionality(type, arrayDim)
                    : new TypeWithDimensionality(type, arrayDim, appClassPath));
            }

            var propertyInfo = PropertyInfo.CreateUnion(unionTypes.ToArray());
            propertyInfo.IsOptionalReturn = isOptionalReturn;
            return propertyInfo;
        }
        else
        {
            // Read single type (legacy format)
            var type = (PeopleCodeType)reader.ReadByte();
            var arrayDim = reader.ReadByte();
            var appClassPath = ReadString(reader);

            var propertyInfo = string.IsNullOrEmpty(appClassPath)
                ? new PropertyInfo(type, arrayDim)
                : new PropertyInfo(type, arrayDim, appClassPath);
            propertyInfo.IsOptionalReturn = isOptionalReturn;
            return propertyInfo;
        }
    }

    /// <summary>
    /// Convert from existing SystemVariableInfo format
    /// </summary>
    public static PropertyInfo ToPropertyInfo(this (PeopleCodeType type, bool isArray, byte dimensionality) sysVarInfo)
    {
        return new PropertyInfo(sysVarInfo.type, sysVarInfo.isArray ? sysVarInfo.dimensionality : (byte)0);
    }

    private static void WriteString(BinaryWriter writer, string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            writer.Write((byte)0);
        }
        else
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            writer.Write((byte)Math.Min(bytes.Length, 255)); // Limit to 255 bytes
            writer.Write(bytes, 0, Math.Min(bytes.Length, 255));
        }
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadByte();
        if (length == 0)
            return "";

        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }
}
