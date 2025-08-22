using FluentAssertions;
using Xunit;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.Tests.Utilities;

namespace PeopleCodeParser.Tests.ErrorRecoveryTests;

/// <summary>
/// Tests for error recovery and handling of malformed code
/// </summary>
public class SyntaxErrorTests
{
    [Theory]
    [InlineData("LOCAL string &var", "Missing semicolon")]
    [InlineData("LOCAL string &var;;", "Double semicolon")]
    [InlineData("LOCAL &var;", "Missing type")]
    [InlineData("LOCAL string;", "Missing variable name")]
    public void Should_Handle_Variable_Declaration_Errors(string malformedCode, string description)
    {
        // Test that the parser recovers gracefully and reports errors
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(malformedCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull($"Parser should not crash on: {description}");
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        hasErrors.Should().BeTrue($"Parser should report errors for: {description}");
    }

    [Theory]
    [InlineData("IF &condition THEN &x = 1;", "Missing END-IF")]
    [InlineData("IF &condition &x = 1; END-IF;", "Missing THEN")]
    [InlineData("IF THEN &x = 1; END-IF;", "Missing condition")]
    [InlineData("IF &condition THEN &x = 1; ELSE &y = 2;", "Missing END-IF after ELSE")]
    public void Should_Handle_Conditional_Statement_Errors(string malformedCode, string description)
    {
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(malformedCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull($"Parser should not crash on: {description}");
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        hasErrors.Should().BeTrue($"Parser should report errors for: {description}");
    }

    [Theory]
    [InlineData("FOR &i = 1 10 &sum = &sum + &i; END-FOR;", "Missing TO keyword")]
    [InlineData("FOR &i = 1 TO &sum = &sum + &i; END-FOR;", "Missing TO value")]
    [InlineData("FOR &i = 1 TO 10 &sum = &sum + &i;", "Missing END-FOR")]
    [InlineData("WHILE &sum = &sum + 1; END-WHILE;", "Missing condition")]
    public void Should_Handle_Loop_Statement_Errors(string malformedCode, string description)
    {
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(malformedCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull($"Parser should not crash on: {description}");
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        hasErrors.Should().BeTrue($"Parser should report errors for: {description}");
    }

    [Theory]
    [InlineData("METHOD MyMethod( &param AS string RETURNS string;", "Unmatched parenthesis")]
    [InlineData("METHOD MyMethod &param AS string) RETURNS string;", "Missing opening parenthesis")]
    [InlineData("METHOD MyMethod(&param AS string RETURNS string;", "Missing closing parenthesis")]
    [InlineData("METHOD MyMethod(&param string) RETURNS string;", "Missing AS keyword")]
    public void Should_Handle_Method_Declaration_Errors(string malformedCode, string description)
    {
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(malformedCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull($"Parser should not crash on: {description}");
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        hasErrors.Should().BeTrue($"Parser should report errors for: {description}");
    }

    [Theory]
    [InlineData("CLASS MyClass METHOD Test(); END-CLASS;", "Missing visibility section")]
    [InlineData("CLASS MyClass END-CLASS", "Missing semicolon after END-CLASS")]
    [InlineData("CLASS EXTENDS BaseClass END-CLASS;", "Missing class name")]
    [InlineData("CLASS MyClass EXTENDS END-CLASS;", "Missing base class name")]
    public void Should_Handle_Class_Declaration_Errors(string malformedCode, string description)
    {
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(malformedCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull($"Parser should not crash on: {description}");
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        hasErrors.Should().BeTrue($"Parser should report errors for: {description}");
    }

    [Theory]
    [InlineData("1 + + 2", "Double operator")]
    [InlineData("1 + * 2", "Invalid operator sequence")]
    [InlineData("(1 + 2", "Unmatched parenthesis")]
    [InlineData("1 + 2)", "Extra closing parenthesis")]
    [InlineData("&obj.", "Incomplete dot access")]
    [InlineData("&obj.Method(", "Incomplete method call")]
    public void Should_Handle_Expression_Errors(string malformedExpression, string description)
    {
        var malformedCode = $"LOCAL any &result = {malformedExpression};";
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(malformedCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull($"Parser should not crash on: {description}");
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        hasErrors.Should().BeTrue($"Parser should report errors for: {description}");
    }

    [Theory]
    [InlineData("\"unclosed string", "Unclosed string literal")]
    [InlineData("'unclosed string", "Unclosed single-quoted string")]
    [InlineData("123.45.67", "Invalid decimal number")]
    [InlineData("&", "Incomplete user variable")]
    [InlineData("%", "Incomplete system variable")]
    public void Should_Handle_Literal_Errors(string malformedLiteral, string description)
    {
        var malformedCode = $"LOCAL any &result = {malformedLiteral};";
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(malformedCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull($"Parser should not crash on: {description}");
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        hasErrors.Should().BeTrue($"Parser should report errors for: {description}");
    }

    [Fact]
    public void Should_Handle_Complex_Malformed_Code()
    {
        var malformedCode = @"
        CLASS MyClass EXTENDS
            METHOD Test(&param AS RETURNS string
                LOCAL string &var = ""unclosed
                IF &condition THEN
                    FOR &i = 1 TO
                        &result = &i +
                    END-FOR
                ELSE
                    WHILE &condition
                        TRY
                            &result = SomeFunction(;
                        CATCH
                    END-WHILE;
        ";

        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(malformedCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull("Parser should not crash on complex malformed code");
        var totalErrors = lexer.Errors.Count + parser.Errors.Count;
        totalErrors.Should().BeGreaterThan(0, "Parser should report multiple errors for complex malformed code");
        totalErrors.Should().BeGreaterThan(5, "Complex malformed code should generate multiple errors");
    }

    [Theory]
    [InlineData("/* unclosed comment", "Unclosed block comment")]
    [InlineData("<* unclosed nested comment", "Unclosed nested comment")]
    [InlineData("/** unclosed API comment", "Unclosed API comment")]
    public void Should_Handle_Comment_Errors(string malformedComment, string description)
    {
        var malformedCode = $"{malformedComment}\nLOCAL string &var;";
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(malformedCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull($"Parser should not crash on: {description}");
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        hasErrors.Should().BeTrue($"Parser should report errors for: {description}");
    }

    [Fact]
    public void Should_Continue_Parsing_After_Errors()
    {
        // Test that the parser can recover and continue parsing valid code after errors
        var codeWithErrors = @"
        LOCAL string &var1; // This should parse fine
        INVALID SYNTAX HERE // This should cause an error
        LOCAL string &var2; // This should also parse fine after recovery
        ";

        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(codeWithErrors);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull("Parser should recover and continue parsing after errors");
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        hasErrors.Should().BeTrue("Parser should report errors for invalid syntax");
        
        // The parser should have recovered and parsed some valid statements
        result.MainBlock.Should().NotBeNull("Parser should construct partial AST even with errors");
    }

    [Fact]
    public void Should_Provide_Meaningful_Error_Messages()
    {
        var malformedCode = "LOCAL string &var"; // Missing semicolon
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(malformedCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull();
        
        var totalErrors = lexer.Errors.Count + parser.Errors.Count;
        totalErrors.Should().BeGreaterThan(0, "Should have errors for malformed code");
        
        // Test error message quality from either lexer or parser
        if (parser.Errors.Count > 0)
        {
            var error = parser.Errors.First();
            error.Message.Should().NotBeNullOrEmpty("Error should have a meaningful message");
            error.Location.Should().NotBeNull("Error should have source position information");
            error.Location.Start.Line.Should().BeGreaterThan(0, "Error should have valid line number");
        }
    }
}