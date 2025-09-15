using PeopleCodeParser.SelfHosted.TypeSystem;
using Xunit;
using Xunit.Abstractions;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

/// <summary>
/// Debug test to understand what's happening in type inference
/// </summary>
public class DebugTypeInferenceTest : TypeInferenceTestBase
{
    private readonly ITestOutputHelper _output;

    public DebugTypeInferenceTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Debug_StringIntegerAssignment()
    {
        // The failing test case
        var declaration = "Local string &x = 3;";

        _output.WriteLine($"Testing: {declaration}");

        // Parse the code
        var program = ParseLocalDeclaration(declaration);
        _output.WriteLine($"Parsed program with {program.Functions.Count} functions");

        var function = program.Functions.First();
        _output.WriteLine($"Function has body: {function.Body != null}");

        if (function.Body != null)
        {
            _output.WriteLine($"Body has {function.Body.Children.Count} children");
            foreach (var child in function.Body.Children)
            {
                _output.WriteLine($"  Child type: {child.GetType().Name}");
            }
        }

        // Run type inference
        var result = await Engine.InferTypesAsync(program, TypeInferenceMode.Quick);

        _output.WriteLine($"Type inference result:");
        _output.WriteLine($"  Success: {result.Success}");
        _output.WriteLine($"  Errors: {result.Errors.Count}");
        _output.WriteLine($"  Warnings: {result.Warnings.Count}");
        _output.WriteLine($"  Nodes analyzed: {result.NodesAnalyzed}");
        _output.WriteLine($"  Types inferred: {result.TypesInferred}");

        if (result.Errors.Any())
        {
            _output.WriteLine($"Errors:");
            foreach (var error in result.Errors)
            {
                _output.WriteLine($"  - {error.Kind}: {error.Message}");
            }
        }

        if (result.Warnings.Any())
        {
            _output.WriteLine($"Warnings:");
            foreach (var warning in result.Warnings)
            {
                _output.WriteLine($"  - {warning.Message}");
            }
        }
    }
}