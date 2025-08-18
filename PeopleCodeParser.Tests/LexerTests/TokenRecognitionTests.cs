using FluentAssertions;
using Xunit;
using AppRefiner.PeopleCode;

namespace PeopleCodeParser.Tests.LexerTests;

/// <summary>
/// Tests for basic token recognition functionality
/// </summary>
public class TokenRecognitionTests
{
    [Fact]
    public void Should_Recognize_Basic_Tokens()
    {
        // This is a placeholder test that will be implemented once the lexer is created
        // For now, we'll test the current ANTLR implementation to ensure our test framework works
        
        var sourceCode = "LOCAL string &test;";
        var parseTree = ProgramParser.Parse(sourceCode);
        
        parseTree.Should().NotBeNull();
    }

    [Theory]
    [InlineData("LOCAL", "LOCAL keyword")]
    [InlineData("GLOBAL", "GLOBAL keyword")]
    [InlineData("COMPONENT", "COMPONENT keyword")]
    [InlineData("METHOD", "METHOD keyword")]
    [InlineData("CLASS", "CLASS keyword")]
    public void Should_Recognize_Keywords(string keyword, string description)
    {
        // Placeholder for keyword recognition tests
        // Will be implemented with self-hosted lexer
        var sourceCode = $"{keyword} string &test;";
        var parseTree = ProgramParser.Parse(sourceCode);
        
        parseTree.Should().NotBeNull(description);
    }

    [Theory]
    [InlineData("123", "Integer literal")]
    [InlineData("123.45", "Decimal literal")]
    [InlineData("\"hello world\"", "String literal with double quotes")]
    [InlineData("'hello world'", "String literal with single quotes")]
    [InlineData("TRUE", "Boolean literal TRUE")]
    [InlineData("FALSE", "Boolean literal FALSE")]
    public void Should_Recognize_Literals(string literal, string description)
    {
        // Placeholder for literal recognition tests
        var sourceCode = $"LOCAL any &test = {literal};";
        var parseTree = ProgramParser.Parse(sourceCode);
        
        parseTree.Should().NotBeNull(description);
    }

    [Theory]
    [InlineData("&variable", "User variable")]
    [InlineData("%USERID", "System variable")]
    [InlineData("%THIS", "System constant")]
    [InlineData("MyFunction", "Generic identifier")]
    public void Should_Recognize_Identifiers(string identifier, string description)
    {
        // Placeholder for identifier recognition tests
        var sourceCode = $"LOCAL any &test = {identifier};";
        var parseTree = ProgramParser.Parse(sourceCode);
        
        parseTree.Should().NotBeNull(description);
    }
}