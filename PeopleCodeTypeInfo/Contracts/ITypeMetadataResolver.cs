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

    private NullTypeMetadataResolver() { }

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
}
