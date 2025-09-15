using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.TypeSystem.Tests.Infrastructure;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

[Collection("TypeSystemCache")]
public class FullProgramTypeCheckTests : TypeInferenceTestBase
{
    private static ProgramNode ParseProgramFromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"PeopleCode program not found: {path}");
        }

        var source = File.ReadAllText(path);
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();

        PeopleCodeParser.ToolsRelease = new ToolsVersion("99.99.99");
        var parser = new PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        if (program == null || parser.Errors.Any())
        {
            var details = string.Join(Environment.NewLine, parser.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Parsing failed for {path}:{Environment.NewLine}{details}");
        }

        return program;
    }

    [Fact]
    public async Task TestFullProgram()
    {
        const string programPath = @"C:\\temp\\IH91U019\\PeopleCode\\Application Packages\\ADS\\Common.pcode";

        var program = ParseProgramFromFile(programPath);

        // Use the real type inference engine with no external provider for now
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider: null);

        if (!result.Success)
        {
            var errorLog = string.Join(Environment.NewLine,
                result.Errors.Select(e =>
                    $"[Line {e.Location.Start.Line}, Col {e.Location.Start.Column}] {e.Kind}: {e.Message}"));

            // Fail the test with a full dump of inference issues
            Assert.True(result.Success, $"Type inference reported {result.Errors.Count} error(s):{Environment.NewLine}{errorLog}");
        }

        Assert.True(result.Success);
    }
}
