using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Test for running type inference on ABS_TYPE_OPTN FieldFormula.pcode.
/// Used for debugging type inference behavior.
/// </summary>
public class FieldFormulaTypeInferenceTest : IDisposable
{
    private readonly string _testBasePath = @"C:\temp\IH91U019\PeopleCode";
    private readonly ProgramNode _program;
    private readonly TypeMetadata _programMetadata;
    private readonly TypeInferenceVisitor _visitor;
    private readonly TestTypeMetadataResolver _resolver;
    private readonly TypeCache _cache;

    public FieldFormulaTypeInferenceTest()
    {
        // Read and parse FieldFormula.pcode
        var sourceFilePath = @"C:\temp\IH91U019\PeopleCode\Records\FUNCLIB_ABS_EA\ABS_TYPE_OPTN\FieldFormula.pcode";
        var source = File.ReadAllText(sourceFilePath);

        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        _program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        // Extract metadata for the program
        _programMetadata = TypeMetadataBuilder.ExtractMetadata(_program, "FUNCLIB_ABS_EA:ABS_TYPE_OPTN");

        // Create resolver and cache
        _resolver = new TestTypeMetadataResolver(_testBasePath);
        _cache = new TypeCache();

        // Run type inference
        _visitor = TypeInferenceVisitor.Run(_program, _programMetadata, _resolver, _cache);
    }

    public void Dispose()
    {
        _cache?.Clear();
    }

    [Fact]
    public void FieldFormula_TypeInference_RunsSuccessfully()
    {
        // This test just verifies that type inference runs without crashing
        // and the visitor is initialized properly.
        // You can set breakpoints here to debug the inference process.

        Assert.NotNull(_program);
        Assert.NotNull(_visitor);
        Assert.NotNull(_programMetadata);

        // Test passes - type inference has run successfully
        Assert.True(true);
    }
}
