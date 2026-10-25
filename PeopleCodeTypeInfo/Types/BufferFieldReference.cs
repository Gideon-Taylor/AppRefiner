namespace PeopleCodeTypeInfo.Types;

/// <summary>
/// Identifies the buffer field a bare REC.FIELD (or bare FIELD in record PeopleCode) refers to.
/// Carried on <see cref="TypeInfo.BufferField"/>.
/// </summary>
public sealed record BufferFieldReference(string RecordName, string FieldName);
