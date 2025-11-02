using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeEnum = PeopleCodeTypeInfo.Types.PeopleCodeType;

namespace PeopleCodeTypeInfo.Types;

/// <summary>
/// This is used to represent a field.Value. This is only necessary so we can allow this to be used when a variable is required (out parms)
/// </summary>
public class FieldValueTypeInfo : PrimitiveTypeInfo
{
    public FieldTypeInfo FieldTypeInfo { get; }
    public FieldValueTypeInfo(FieldTypeInfo fieldType) : base(fieldType.Name, fieldType.GetFieldDataType().PeopleCodeType)
    {
        FieldTypeInfo = fieldType;
    }

    public override string ToString()
    {
        return $"{FieldTypeInfo}.Value";
    }
}

/// <summary>
/// Represents a Field builtin type with knowledge of which record/field it refers to,
/// enabling resolution of the underlying field data type for type compatibility checking.
/// This allows implicit .Value access - a Field can be used where its data type is expected.
/// </summary>
public class FieldTypeInfo : BuiltinObjectTypeInfo
{
    /// <summary>
    /// The record name this field belongs to (e.g., "AAP_YEAR")
    /// </summary>
    public string RecordName { get; }

    /// <summary>
    /// The field name (e.g., "START_DT")
    /// </summary>
    public string FieldName { get; }

    private readonly ITypeMetadataResolver? _resolver;
    private TypeInfo? _cachedFieldDataType;

    /// <summary>
    /// Create a Field type with record/field context for data type resolution
    /// </summary>
    /// <param name="recordName">The record name (e.g., "AAP_YEAR")</param>
    /// <param name="fieldName">The field name (e.g., "START_DT")</param>
    /// <param name="resolver">Optional type resolver to get actual field data type</param>
    public FieldTypeInfo(string recordName, string fieldName, ITypeMetadataResolver? resolver)
        : base("field", GetFieldEnumValue())
    {
        RecordName = recordName ?? throw new ArgumentNullException(nameof(recordName));
        FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
        _resolver = resolver;
    }

    /// <summary>
    /// Get the actual data type of this field (e.g., Date, String, Number).
    /// This represents the type of the field's .Value property.
    /// </summary>
    /// <returns>The field's data type, or Any if unknown</returns>
    public TypeInfo GetFieldDataType()
    {
        if (_cachedFieldDataType == null)
        {
            _cachedFieldDataType = _resolver?.GetFieldType(RecordName, FieldName)
                ?? AnyTypeInfo.Instance;
        }
        return _cachedFieldDataType;
    }

    /// <summary>
    /// Override to provide a more descriptive name
    /// </summary>
    public override string ToString()
    {
        return $"field({RecordName}.{FieldName})";
    }

    /// <summary>
    /// Helper to get the Field enum value without naming conflict
    /// </summary>
    private static PeopleCodeType? GetFieldEnumValue()
    {
        // Use alias to avoid ambiguity with parent class property
        return PeopleCodeTypeEnum.Field;
    }
}
