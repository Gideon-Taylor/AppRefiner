using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using System.Collections.Concurrent;

namespace AppRefiner.Database
{
    /// <summary>
    /// Implementation of ITypeMetadataResolver that uses IDataManager to retrieve and parse
    /// PeopleCode programs from the database, extracting type metadata for type inference.
    /// </summary>
    /// <remarks>
    /// This resolver:
    /// - Caches parsed TypeMetadata for performance
    /// - Retrieves source code from the database via IDataManager
    /// - Parses source using PeopleCodeParser
    /// - Extracts metadata using TypeMetadataBuilder
    /// - Handles parse errors gracefully by extracting partial metadata
    /// </remarks>
    public class DatabaseTypeMetadataResolver : ITypeMetadataResolver
    {
        private readonly IDataManager _dataManager;
        private readonly ConcurrentDictionary<string, TypeMetadata> _cache;

        /// <summary>
        /// Creates a new DatabaseTypeMetadataResolver
        /// </summary>
        /// <param name="dataManager">The data manager to use for retrieving source code</param>
        public DatabaseTypeMetadataResolver(IDataManager dataManager)
        {
            _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
            _cache = new ConcurrentDictionary<string, TypeMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Attempts to retrieve type metadata for a custom type.
        /// </summary>
        /// <param name="qualifiedName">
        /// The qualified name of the type:
        /// - App Class/Interface: "PKG:Class", "PKG:SUBPKG:Class", etc.
        /// - Function Library: Any identifier for a program containing function declarations
        /// </param>
        /// <returns>
        /// TypeMetadata if the type is found and parsed, null otherwise.
        /// </returns>
        public TypeMetadata? GetTypeMetadata(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName))
            {
                return null;
            }

            // Check cache first
            if (_cache.TryGetValue(qualifiedName, out var cached))
            {
                return cached;
            }

            // Check if database is connected
            if (!_dataManager.IsConnected)
            {
                Debug.Log($"DatabaseTypeMetadataResolver: Cannot resolve '{qualifiedName}' - database not connected");
                return null;
            }

            // Try to resolve as an application class
            var metadata = TryResolveAsAppClass(qualifiedName);

            // TODO: Add function library resolution strategy
            // if (metadata == null)
            // {
            //     metadata = TryResolveAsFunctionLibrary(qualifiedName);
            // }

            // Cache the result if found
            if (metadata != null)
            {
                _cache[qualifiedName] = metadata;
            }

            return metadata;
        }

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
        public Task<TypeMetadata?> GetTypeMetadataAsync(string qualifiedName)
        {
            // Wrap synchronous call in a Task
            return Task.FromResult(GetTypeMetadata(qualifiedName));
        }

        /// <summary>
        /// Attempts to resolve the qualified name as an application class
        /// </summary>
        private TypeMetadata? TryResolveAsAppClass(string qualifiedName)
        {
            try
            {
                // Check if the app class exists in the database
                if (!_dataManager.CheckAppClassExists(qualifiedName))
                {
                    return null;
                }

                // Get the source code
                var sourceCode = _dataManager.GetAppClassSourceByPath(qualifiedName);
                if (string.IsNullOrEmpty(sourceCode))
                {
                    Debug.Log($"DatabaseTypeMetadataResolver: App class '{qualifiedName}' exists but has no source code");
                    return null;
                }

                // Parse and extract metadata
                return ParseAndExtractMetadata(sourceCode, qualifiedName);
            }
            catch (Exception ex)
            {
                Debug.Log($"DatabaseTypeMetadataResolver: Error resolving app class '{qualifiedName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses source code and extracts type metadata
        /// </summary>
        /// <param name="sourceCode">The PeopleCode source to parse</param>
        /// <param name="qualifiedName">The qualified name to associate with the metadata</param>
        /// <returns>TypeMetadata extracted from the source, or null if parsing completely fails</returns>
        private TypeMetadata? ParseAndExtractMetadata(string sourceCode, string qualifiedName)
        {
            try
            {
                // Lex the source code
                var lexer = new PeopleCodeLexer(sourceCode);
                var tokens = lexer.TokenizeAll();

                // Parse the source code
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var program = parser.ParseProgram();

                // Log parse errors but continue - we want to extract partial metadata
                if (parser.Errors.Count > 0)
                {
                    Debug.Log($"DatabaseTypeMetadataResolver: Parse errors in '{qualifiedName}' ({parser.Errors.Count} errors), extracting partial metadata");
                }

                // Extract metadata from the AST
                var metadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);

                return metadata;
            }
            catch (Exception ex)
            {
                Debug.Log($"DatabaseTypeMetadataResolver: Fatal error parsing '{qualifiedName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Invalidates the cache entry for a specific type
        /// </summary>
        /// <param name="qualifiedName">The qualified name of the type to invalidate</param>
        public void InvalidateCache(string qualifiedName)
        {
            _cache.TryRemove(qualifiedName, out _);
        }

        /// <summary>
        /// Clears the entire metadata cache
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Gets the number of cached metadata entries
        /// </summary>
        public int CacheSize => _cache.Count;
    }
}
