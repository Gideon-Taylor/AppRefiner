using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Test implementation of ITypeMetadataResolver that uses LocalDirectorySourceProvider
/// to read PeopleCode source files, parse them, and extract TypeMetadata.
/// Caching is handled automatically by the base class.
/// </summary>
public class TestTypeMetadataResolver : ITypeMetadataResolver
{
    private readonly LocalDirectorySourceProvider _sourceProvider;

    public TestTypeMetadataResolver(string basePath) : base()
    {
        _sourceProvider = new LocalDirectorySourceProvider(basePath);
    }

    protected override TypeMetadata? GetTypeMetadataCore(string qualifiedName)
    {
        // Get source from file system
        var (found, source) = _sourceProvider.GetSourceAsync(qualifiedName).Result;
        if (!found || source == null)
        {
            return null;
        }

        // Lex and parse the source
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        if (parser.Errors.Count > 0)
        {
            // Parser errors - return null
            return null;
        }

        // Extract metadata
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);

        return metadata;
    }

    protected override async Task<TypeMetadata?> GetTypeMetadataCoreAsync(string qualifiedName)
    {
        // Get source from file system
        var (found, source) = await _sourceProvider.GetSourceAsync(qualifiedName);
        if (!found || source == null)
        {
            return null;
        }

        // Lex and parse the source
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        if (parser.Errors.Count > 0)
        {
            // Parser errors - return null
            return null;
        }

        // Extract metadata
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);

        return metadata;
    }

    protected override Types.TypeInfo GetFieldTypeCore(string recordName, string fieldName)
    {
        // Empty record name means runtime-inferred context - return any
        if (string.IsNullOrEmpty(recordName))
            return Types.AnyTypeInfo.Instance;

        // For testing purposes, return AnyTypeInfo
        return Types.AnyTypeInfo.Instance;
    }

    protected override Task<Types.TypeInfo> GetFieldTypeCoreAsync(string recordName, string fieldName)
    {
        // Empty record name means runtime-inferred context - return any
        if (string.IsNullOrEmpty(recordName))
            return Task.FromResult<Types.TypeInfo>(Types.AnyTypeInfo.Instance);

        return Task.FromResult<Types.TypeInfo>(Types.AnyTypeInfo.Instance);
    }
}
