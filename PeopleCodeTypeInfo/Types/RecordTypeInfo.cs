using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeEnum = PeopleCodeTypeInfo.Types.PeopleCodeType;

namespace PeopleCodeTypeInfo.Types;

/// <summary>
/// Represents a Field builtin type with knowledge of which record/field it refers to,
/// enabling resolution of the underlying field data type for type compatibility checking.
/// This allows implicit .Value access - a Field can be used where its data type is expected.
/// </summary>
public class RecordTypeInfo : BuiltinObjectTypeInfo
{
    /// <summary>
    /// The record name this field belongs to (e.g., "AAP_YEAR")
    /// </summary>
    public string RecordName { get; }

    private readonly ITypeMetadataResolver? _resolver;
    private TypeInfo? _cachedFieldDataType;

    /// <summary>
    /// Create a Field type with record/field context for data type resolution
    /// </summary>
    /// <param name="recordName">The record name (e.g., "AAP_YEAR")</param>
    /// <param name="fieldName">The field name (e.g., "START_DT")</param>
    /// <param name="resolver">Optional type resolver to get actual field data type</param>
    public RecordTypeInfo(string recordName, ITypeMetadataResolver? resolver)
        : base("record", PeopleCodeTypeEnum.Record)
    {
        RecordName = recordName ?? throw new ArgumentNullException(nameof(recordName));
        _resolver = resolver;
    }

    /// <summary>
    /// Override to provide a more descriptive name
    /// </summary>
    public override string ToString()
    {
        return $"record({RecordName})";
    }

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new RecordTypeInfo(RecordName, _resolver)
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
    }

}
