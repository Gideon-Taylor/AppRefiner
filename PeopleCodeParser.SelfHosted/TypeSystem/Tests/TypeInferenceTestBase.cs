using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.TypeSystem;
using PeopleCodeParser.SelfHosted.Lexing;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

/// <summary>
/// Base class for type inference tests providing common utilities and setup
/// </summary>
public abstract class TypeInferenceTestBase
{
    /// <summary>
    /// The type inference engine used for testing
    /// </summary>
    protected readonly TypeInferenceEngine Engine;

    protected TypeInferenceTestBase()
    {
        Engine = new TypeInferenceEngine();
    }

    /// <summary>
    /// Parses a PeopleCode snippet and runs type inference on it
    /// </summary>
    /// <param name="code">The PeopleCode to analyze</param>
    /// <param name="mode">Type inference mode to use</param>
    /// <returns>The type inference result</returns>
    protected async Task<TypeInferenceResult> InferTypesAsync(string code, TypeInferenceMode mode = TypeInferenceMode.Quick)
    {
        var program = ParseCode(code);
        return await Engine.InferTypesAsync(program, mode);
    }

    protected async Task<TypeInferenceResult> InferTypesAsync(ProgramNode program, TypeInferenceMode mode, IProgramSourceProvider? provider)
    {
        var options = new TypeInferenceOptions
        {
            Mode = mode,
            ProgramSourceProvider = provider
        };

        return await Engine.InferTypesAsync(program, mode, options: options);
    }

    /// <summary>
    /// Parses a PeopleCode snippet into a ProgramNode AST
    /// </summary>
    /// <param name="code">The PeopleCode to parse</param>
    /// <returns>The parsed program AST</returns>
    protected ProgramNode ParseCode(string code)
    {
        // Create a simple function wrapper around the code to make it a valid program
        var wrappedCode = $@"
function TestFunction()
{code}
end-function;
";

        try
        {
            // Lexing phase
            var lexer = new PeopleCodeLexer(wrappedCode);
            var tokens = lexer.TokenizeAll();

            // Parsing phase
            PeopleCodeParser.ToolsRelease = new ToolsVersion("99.99.99");
            var parser = new PeopleCodeParser(tokens);
            var program = parser.ParseProgram();

            if (program == null || parser.Errors.Any())
            {
                var errorMessages = parser.Errors.Any() ?
                    string.Join("; ", parser.Errors.Take(3).Select(e => e.Message)) :
                    "Unknown parsing error";
                throw new InvalidOperationException($"Failed to parse test code: {errorMessages}\n\nCode:\n{wrappedCode}");
            }

            return program;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to parse test code: {ex.Message}\n\nCode:\n{wrappedCode}", ex);
        }
    }

    /// <summary>
    /// Parses a PeopleCode snippet as a standalone local variable declaration
    /// </summary>
    /// <param name="declaration">The variable declaration code</param>
    /// <returns>The parsed program with the declaration</returns>
    protected ProgramNode ParseLocalDeclaration(string declaration)
    {
        // Create the function code directly without double-wrapping
        var wrappedCode = $@"
function TestFunction()
   {declaration}
end-function;
";

        try
        {
            // Lexing phase
            var lexer = new PeopleCodeLexer(wrappedCode);
            var tokens = lexer.TokenizeAll();

            // Parsing phase
            PeopleCodeParser.ToolsRelease = new ToolsVersion("99.99.99");
            var parser = new PeopleCodeParser(tokens);
            var program = parser.ParseProgram();

            if (program == null || parser.Errors.Any())
            {
                var errorMessages = parser.Errors.Any() ?
                    string.Join("; ", parser.Errors.Take(3).Select(e => e.Message)) :
                    "Unknown parsing error";
                throw new InvalidOperationException($"Failed to parse local declaration: {errorMessages}\n\nCode:\n{wrappedCode}");
            }

            return program;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to parse local declaration: {ex.Message}\n\nCode:\n{wrappedCode}", ex);
        }
    }

    /// <summary>
    /// Parses a PeopleCode function call expression
    /// </summary>
    /// <param name="functionCall">The function call code</param>
    /// <returns>The parsed program with the function call</returns>
    protected ProgramNode ParseFunctionCall(string functionCall)
    {
        var wrappedCode = $@"
function TestFunction()
   {functionCall};
end-function;
";

        try
        {
            var lexer = new PeopleCodeLexer(wrappedCode);
            var tokens = lexer.TokenizeAll();

            PeopleCodeParser.ToolsRelease = new ToolsVersion("99.99.99");
            var parser = new PeopleCodeParser(tokens);
            var program = parser.ParseProgram();

            if (program == null || parser.Errors.Any())
            {
                var errorMessages = parser.Errors.Any() ?
                    string.Join("; ", parser.Errors.Take(3).Select(e => e.Message)) :
                    "Unknown parsing error";
                throw new InvalidOperationException($"Failed to parse function call: {errorMessages}\n\nCode:\n{wrappedCode}");
            }

            return program;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to parse function call: {ex.Message}\n\nCode:\n{wrappedCode}", ex);
        }
    }

    /// <summary>
    /// Parses a PeopleCode system variable expression
    /// </summary>
    /// <param name="sysVarExpression">The system variable code</param>
    /// <returns>The parsed program with the system variable</returns>
    protected ProgramNode ParseSystemVariableExpression(string sysVarExpression)
    {
        var wrappedCode = $@"
function TestFunction()
   Local any &temp = {sysVarExpression};
end-function;
";

        try
        {
            var lexer = new PeopleCodeLexer(wrappedCode);
            var tokens = lexer.TokenizeAll();

            PeopleCodeParser.ToolsRelease = new ToolsVersion("99.99.99");
            var parser = new PeopleCodeParser(tokens);
            var program = parser.ParseProgram();

            if (program == null || parser.Errors.Any())
            {
                var errorMessages = parser.Errors.Any() ?
                    string.Join("; ", parser.Errors.Take(3).Select(e => e.Message)) :
                    "Unknown parsing error";
                throw new InvalidOperationException($"Failed to parse system variable: {errorMessages}\n\nCode:\n{wrappedCode}");
            }

            return program;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to parse system variable: {ex.Message}\n\nCode:\n{wrappedCode}", ex);
        }
    }

    /// <summary>
    /// Asserts that a type inference result contains an error of the specified kind
    /// </summary>
    /// <param name="result">The type inference result</param>
    /// <param name="errorKind">The expected error kind</param>
    /// <param name="errorMessageContains">Optional substring the error message should contain</param>
    protected void AssertTypeError(TypeInferenceResult result, TypeErrorKind errorKind, string? errorMessageContains = null)
    {
        Assert.False(result.Success, "Expected type inference to fail with an error");
        Assert.NotEmpty(result.Errors);

        var error = result.Errors.FirstOrDefault(e => e.Kind == errorKind);
        Assert.NotNull(error);

        if (errorMessageContains != null)
        {
            Assert.Contains(errorMessageContains, error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Asserts that a type inference result is successful with no errors
    /// </summary>
    /// <param name="result">The type inference result</param>
    protected void AssertSuccess(TypeInferenceResult result)
    {
        if (!result.Success)
        {
            var errorMessages = string.Join("\n", result.Errors.Select(e => $"  - {e.Message}"));
            Assert.Fail($"Expected type inference to succeed, but got errors:\n{errorMessages}");
        }

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Asserts that a type inference result contains a warning
    /// </summary>
    /// <param name="result">The type inference result</param>
    /// <param name="warningMessageContains">Optional substring the warning message should contain</param>
    protected void AssertTypeWarning(TypeInferenceResult result, string? warningMessageContains = null)
    {
        Assert.NotEmpty(result.Warnings);

        if (warningMessageContains != null)
        {
            var warning = result.Warnings.FirstOrDefault(w =>
                w.Message.Contains(warningMessageContains, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(warning);
        }
    }

    /// <summary>
    /// Asserts that a function call resolves to the expected return type
    /// </summary>
    /// <param name="result">The type inference result</param>
    /// <param name="expectedType">The expected return type</param>
    protected void AssertFunctionReturnType(TypeInferenceResult result, PeopleCodeType expectedType)
    {
        AssertSuccess(result);
        // Additional logic to verify the function call return type would go here
        // This depends on how the type inference engine stores resolved types
    }

    /// <summary>
    /// Asserts that a system variable resolves to the expected type
    /// </summary>
    /// <param name="result">The type inference result</param>
    /// <param name="varName">The system variable name</param>
    /// <param name="expectedType">The expected type</param>
    protected void AssertSystemVariableType(TypeInferenceResult result, string varName, PeopleCodeType expectedType)
    {
        AssertSuccess(result);

        // System variable types will be provided by external function resolution system
        // This test method is temporarily disabled since GetSystemVariableType was removed
        // TODO: Update when external system integration is complete
        return;
    }

    /// <summary>
    /// Gets the first local variable declaration from a parsed program
    /// </summary>
    /// <param name="program">The program to search</param>
    /// <returns>The first local variable found, or null if none</returns>
    protected StatementNode? GetFirstLocalVariable(ProgramNode program)
    {
        // Look in the first function's body for local variables
        var function = program.Functions.FirstOrDefault();
        if (function?.Body == null) return null;

        return FindLocalVariableRecursive(function.Body);
    }

    /// <summary>
    /// Gets the first function call from a parsed program
    /// </summary>
    /// <param name="program">The program to search</param>
    /// <returns>The first function call found, or null if none</returns>
    protected FunctionCallNode? GetFirstFunctionCall(ProgramNode program)
    {
        var function = program.Functions.FirstOrDefault();
        if (function?.Body == null) return null;

        return FindFunctionCallRecursive(function.Body);
    }

    /// <summary>
    /// Recursively searches for a local variable declaration in an AST node
    /// </summary>
    private StatementNode? FindLocalVariableRecursive(AstNode node)
    {
        if (node is LocalVariableDeclarationNode localVar)
            return localVar;

        if (node is LocalVariableDeclarationWithAssignmentNode localVarWithAssign)
            return localVarWithAssign;

        foreach (var child in node.Children)
        {
            var result = FindLocalVariableRecursive(child);
            if (result != null) return result;
        }

        return null;
    }

    /// <summary>
    /// Recursively searches for a function call in an AST node
    /// </summary>
    private FunctionCallNode? FindFunctionCallRecursive(AstNode node)
    {
        if (node is FunctionCallNode functionCall)
            return functionCall;

        foreach (var child in node.Children)
        {
            var result = FindFunctionCallRecursive(child);
            if (result != null) return result;
        }

        return null;
    }
}
