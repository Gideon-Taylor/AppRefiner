using AppRefiner.Database.Models;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

namespace AppRefiner.Database
{
    /// <summary>
    /// Implementation of ITypeMetadataResolver that uses IDataManager to retrieve and parse
    /// PeopleCode programs from the database, extracting type metadata for type inference.
    /// </summary>
    /// <remarks>
    /// This resolver:
    /// - Uses generational cache eviction (provided by base class) for performance
    /// - Retrieves source code from the database via IDataManager
    /// - Parses source using PeopleCodeParser
    /// - Extracts metadata using TypeMetadataBuilder
    /// - Handles parse errors gracefully by extracting partial metadata
    /// </remarks>
    public class DatabaseTypeMetadataResolver : ITypeMetadataResolver
    {
        private readonly IDataManager _dataManager;

        /// <summary>
        /// Creates a new DatabaseTypeMetadataResolver
        /// </summary>
        /// <param name="dataManager">The data manager to use for retrieving source code</param>
        public DatabaseTypeMetadataResolver(IDataManager dataManager) : base()
        {
            _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
        }

        /// <summary>
        /// Attempts to retrieve type metadata for a custom type.
        /// Caching is handled by the base class.
        /// </summary>
        /// <param name="qualifiedName">
        /// The qualified name of the type:
        /// - App Class/Interface: "PKG:Class", "PKG:SUBPKG:Class", etc.
        /// - Function Library: Any identifier for a program containing function declarations
        /// </param>
        /// <returns>
        /// TypeMetadata if the type is found and parsed, null otherwise.
        /// </returns>
        protected override TypeMetadata? GetTypeMetadataCore(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName))
            {
                return null;
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
            if (metadata == null)
            {
                metadata = TryResolveAsFunctionLibrary(qualifiedName);
            }

            return metadata;
        }

        /// <summary>
        /// Attempts to retrieve type metadata for a custom type asynchronously.
        /// Caching is handled by the base class.
        /// </summary>
        /// <param name="qualifiedName">
        /// The qualified name of the type:
        /// - App Class/Interface: "PKG:Class", "PKG:SUBPKG:Class", etc.
        /// - Function Library: Any identifier for a program containing function declarations
        /// </param>
        /// <returns>
        /// Task that resolves to TypeMetadata if the type is found, null otherwise.
        /// </returns>
        protected override Task<TypeMetadata?> GetTypeMetadataCoreAsync(string qualifiedName)
        {
            // Wrap synchronous call in a Task
            return Task.FromResult(GetTypeMetadataCore(qualifiedName));
        }

        /// <summary>
        /// Resolves the data type of a field on a record by querying the database.
        /// </summary>
        /// <param name="recordName">The record name (e.g., "AAP_YEAR")</param>
        /// <param name="fieldName">The field name (e.g., "START_DT")</param>
        /// <returns>TypeInfo representing the field's data type, or AnyTypeInfo if unknown</returns>
        protected override TypeInfo GetFieldTypeCore(string fieldName)
        {
            // Check if database is connected
            if (!_dataManager.IsConnected)
            {
                return AnyTypeInfo.Instance;
            }

            try
            {
                // TODO: Query database for field metadata
                // For now, return Any until database schema is implemented
                // Future implementation should call something like:
                // var fieldMetadata = _dataManager.GetFieldMetadata(recordName, fieldName);
                // return ConvertFieldTypeToTypeInfo(fieldMetadata.DataType);
                var fieldType = _dataManager.GetFieldType(fieldName);
                if (fieldType == PeopleCodeType.Unknown) fieldType = PeopleCodeType.Any;

                return new PrimitiveTypeInfo(fieldType.ToString(), fieldType) { IsAssignable = true};
            }
            catch (Exception ex)
            {
                Debug.Log($"DatabaseTypeMetadataResolver: Error resolving field type '{fieldName}': {ex.Message}");
                return AnyTypeInfo.Instance;
            }
        }

        /// <summary>
        /// Resolves the data type of a field on a record asynchronously.
        /// </summary>
        /// <param name="recordName">The record name (e.g., "AAP_YEAR")</param>
        /// <param name="fieldName">The field name (e.g., "START_DT")</param>
        /// <returns>Task that resolves to TypeInfo representing the field's data type, or AnyTypeInfo if unknown</returns>
        protected override Task<TypeInfo> GetFieldTypeCoreAsync(string fieldName)
        {
            // Wrap synchronous call in a Task
            return Task.FromResult(GetFieldTypeCore(fieldName));
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
        /// Attempts to resolve the qualified name as an application class
        /// </summary>
        private TypeMetadata? TryResolveAsFunctionLibrary(string qualifiedName)
        {
            var parts = qualifiedName.Split('.');
            if (parts.Length < 3) { return null; }
            var openTarget = new OpenTarget(
                OpenTargetType.RecordFieldPeopleCode, 
                qualifiedName, "", 
                [
                    (PSCLASSID.RECORD, parts[0]),
                    (PSCLASSID.FIELD, parts[1]),
                    (PSCLASSID.METHOD, parts[2]),
                ]
            );

            try
            {
                // Get the source code
                var sourceCode = _dataManager.GetPeopleCodeProgram(openTarget);
                if (string.IsNullOrEmpty(sourceCode))
                {
                    Debug.Log($"DatabaseTypeMetadataResolver: Funclib '{qualifiedName}' exists but has no source code");
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
    }
}
