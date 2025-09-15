using System;
using System.Collections.Generic;
using System.Linq;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Extensions;
using PeopleCodeParser.SelfHosted.TypeSystem.Tests.Infrastructure;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

[Collection("TypeSystemCache")]
public class ConstructorInferenceTests : TypeInferenceTestBase
{
    private const string ConstructorClassSource = @"
class SampleClass
   method SampleClass(&value As string, &count As number);
end-class;
";

    private const string ConstructorUsage = @"
   Local SamplePkg:SampleClass &obj = Create SamplePkg:SampleClass(""text"", 1);
";

    private const string MissingArgumentUsage = @"
   Local SamplePkg:SampleClass &obj = Create SamplePkg:SampleClass();
";

    private const string WrongTypeUsage = @"
   Local SamplePkg:SampleClass &obj = Create SamplePkg:SampleClass(123, 1);
";

    private const string ExtraArgumentUsage = @"
   Local SamplePkg:SampleClass &obj = Create SamplePkg:SampleClass(""text"", 1, ""extra"");
";

    private static ProgramNode ParseClassProgram(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();

        PeopleCodeParser.ToolsRelease = new ToolsVersion("99.99.99");
        var parser = new PeopleCodeParser(tokens);
        var program = parser.ParseProgram() ?? throw new InvalidOperationException("Failed to parse class source");

        if (parser.Errors.Any())
        {
            var message = string.Join("\n", parser.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Parser errors: {message}");
        }

        return program;
    }

    private static void PrimeConstructorCache()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();
        PeopleCodeTypeRegistry.CacheClassInfo(ClassMetadataBuilder.Build(ParseClassProgram(ConstructorClassSource).AppClass!, "SamplePkg:SampleClass")!);
    }

    [Fact]
    public async Task ObjectCreation_ShouldInferTypeAndValidateArguments()
    {
        PrimeConstructorCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:SampleClass", ConstructorClassSource)
        });

        var program = ParseCode(ConstructorUsage);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        AssertSuccess(result);

        var creation = program.FindDescendants<ObjectCreationNode>().Single();
        var creationType = Assert.IsType<AppClassTypeInfo>(creation.GetInferredType());
        Assert.Equal("SamplePkg:SampleClass", creationType.QualifiedName);
    }

    [Fact]
    public async Task ObjectCreation_WithMissingArguments_ShouldReportError()
    {
        PrimeConstructorCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:SampleClass", ConstructorClassSource)
        });

        var program = ParseCode(MissingArgumentUsage);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Kind == TypeErrorKind.ArgumentCountMismatch && e.Message.Contains("expects 2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ObjectCreation_WithWrongArgumentType_ShouldReportError()
    {
        PrimeConstructorCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:SampleClass", ConstructorClassSource)
        });

        var program = ParseCode(WrongTypeUsage);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Kind == TypeErrorKind.TypeMismatch && e.Message.Contains("constructor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ObjectCreation_WithTooManyArguments_ShouldReportError()
    {
        PrimeConstructorCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:SampleClass", ConstructorClassSource)
        });

        var program = ParseCode(ExtraArgumentUsage);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Kind == TypeErrorKind.ArgumentCountMismatch && e.Message.Contains("at most", StringComparison.OrdinalIgnoreCase));
    }
}
