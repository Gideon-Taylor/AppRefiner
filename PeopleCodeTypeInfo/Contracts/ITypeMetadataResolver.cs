using PeopleCodeTypeInfo.Inference;

namespace PeopleCodeTypeInfo.Contracts;

/// <summary>
/// Resolves type metadata for custom PeopleCode types (classes, interfaces, function libraries).
/// This interface is implemented by the host application to provide type information
/// for user-defined types without requiring PeopleCodeTypeInfo to parse source code.
/// </summary>
/// <remarks>
/// This design avoids circular dependencies by separating concerns:
/// - PeopleCodeParser extracts TypeMetadata from parsed AST
/// - Host application stores and provides TypeMetadata through this resolver
/// - PeopleCodeTypeInfo queries metadata without needing parser reference
///
/// Typical implementation pattern:
/// 1. Host gets source code (from database, file system, etc.)
/// 2. Host parses source using PeopleCodeParser
/// 3. Host runs TypeMetadataBuilder visitor to extract TypeMetadata
/// 4. Host stores TypeMetadata in a resolver implementation
/// 5. PeopleCodeTypeInfo uses resolver to query custom types
/// </remarks>
public interface ITypeMetadataResolver
{
    /// <summary>
    /// Attempts to retrieve type metadata for a custom type.
    /// </summary>
    /// <param name="qualifiedName">
    /// The qualified name of the type:
    /// - App Class/Interface: "PKG:Class", "PKG:SUBPKG:Class", etc.
    /// - Function Library: Any identifier for a program containing function declarations
    /// </param>
    /// <returns>
    /// TypeMetadata if the type is found, null otherwise.
    /// </returns>
    TypeMetadata? GetTypeMetadata(string qualifiedName);

    /// <summary>
    /// Attempts to retrieve type metadata for a custom type asynchronously.
    /// </summary>
    /// <param name="qualifiedName">
    /// The qualified name of the type:
    /// - App Class/Interface: "PKG:Class", "PKG:SUBPKG:Class", etc.
    /// - Function Library: Any identifier for a program containing function declarations
    /// </param>
    /// <returns>
    /// Task that resolves to TypeMetadata if the type is found, null otherwise.
    /// </returns>
    Task<TypeMetadata?> GetTypeMetadataAsync(string qualifiedName);

    /// <summary>
    /// Resolves the data type of a field on a record.
    /// </summary>
    /// <param name="recordName">The record name (e.g., "AAP_YEAR")</param>
    /// <param name="fieldName">The field name (e.g., "START_DT")</param>
    /// <returns>TypeInfo representing the field's data type, or AnyTypeInfo if unknown</returns>
    Types.TypeInfo GetFieldType(string recordName, string fieldName);

    /// <summary>
    /// Resolves the data type of a field on a record asynchronously.
    /// </summary>
    /// <param name="recordName">The record name (e.g., "AAP_YEAR")</param>
    /// <param name="fieldName">The field name (e.g., "START_DT")</param>
    /// <returns>Task that resolves to TypeInfo representing the field's data type, or AnyTypeInfo if unknown</returns>
    Task<Types.TypeInfo> GetFieldTypeAsync(string recordName, string fieldName);
}

/// <summary>
/// Null implementation of ITypeMetadataResolver that never resolves any types.
/// Useful for testing or when custom type resolution is not available.
/// </summary>
public class NullTypeMetadataResolver : ITypeMetadataResolver
{
    /// <summary>
    /// Singleton instance
    /// </summary>
    public static readonly NullTypeMetadataResolver Instance = new();

    public NullTypeMetadataResolver() { }

    /// <summary>
    /// Always returns null indicating no type metadata was found.
    /// </summary>
    public TypeMetadata? GetTypeMetadata(string qualifiedName)
    {
        return null;
    }

    /// <summary>
    /// Always returns null indicating no type metadata was found.
    /// </summary>
    public Task<TypeMetadata?> GetTypeMetadataAsync(string qualifiedName)
    {
        return Task.FromResult<TypeMetadata?>(null);
    }

    /// <summary>
    /// Always returns AnyTypeInfo.Instance indicating field type is unknown.
    /// Empty record name means runtime-inferred context.
    /// </summary>
    public Types.TypeInfo GetFieldType(string recordName, string fieldName)
    {
        // Empty record name means runtime-inferred context - return any
        if (string.IsNullOrEmpty(recordName))
            return Types.AnyTypeInfo.Instance;

        return Types.AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Always returns AnyTypeInfo.Instance indicating field type is unknown.
    /// Empty record name means runtime-inferred context.
    /// </summary>
    public Task<Types.TypeInfo> GetFieldTypeAsync(string recordName, string fieldName)
    {
        // Empty record name means runtime-inferred context - return any
        if (string.IsNullOrEmpty(recordName))
            return Task.FromResult<Types.TypeInfo>(Types.AnyTypeInfo.Instance);

        return Task.FromResult<Types.TypeInfo>(Types.AnyTypeInfo.Instance);
    }
}
