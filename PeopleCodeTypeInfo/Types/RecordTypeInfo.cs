using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeEnum = PeopleCodeTypeInfo.Types.PeopleCodeType;

namespace PeopleCodeTypeInfo.Types;



/// <summary>
/// Represents a named Record instance (e.g. from CreateRecord / GetRecord) or a bare
/// buffer record name used as DirectRecordAccess (PSOPRDEFN in PSOPRDEFN.ACCTLOCK).
/// </summary>
public class RecordTypeInfo : BuiltinObjectTypeInfo
{
    /// <summary>
    /// The record definition name (e.g., "PSOPRDEFN")
    /// </summary>
    public string RecordName { get; }

    /// <summary>
    /// True when this is a bare buffer record name (REC in REC.FIELD), not a Record object.
    /// Direct access: members are always fields; terminal REC.FIELD is the field data type.
    /// False for &amp;rec, Record.REC after promotion, GetRecord results, etc. (Field objects).
    /// </summary>
    public bool DirectRecordAccess { get; set; } = false;

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
