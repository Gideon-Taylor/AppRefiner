using FluentAssertions;
using Xunit;
using PeopleCodeParser.SelfHosted.Nodes;
using SelfHostedLexer = PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer;
using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.Tests.SelfHostedTests;

/// <summary>
/// Tests for the self-hosted lexer
/// </summary>
public class LexerTests
{
    [Fact]
    public void Should_Tokenize_Simple_Variable_Declaration()
    {
        var source = "LOCAL string &test = \"hello\";";
        var lexer = new SelfHostedLexer(source);
        var tokens = lexer.TokenizeAll();

        // Remove whitespace tokens for easier testing
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(7); // LOCAL, string, &test, =, "hello", ;, EOF
        
        nonTriviaTokens[0].Type.Should().Be(TokenType.Local);
        nonTriviaTokens[0].Text.Should().Be("LOCAL");
        
        nonTriviaTokens[1].Type.Should().Be(TokenType.String);
        nonTriviaTokens[1].Text.Should().Be("string");
        
        nonTriviaTokens[2].Type.Should().Be(TokenType.UserVariable);
        nonTriviaTokens[2].Text.Should().Be("&test");
        
        nonTriviaTokens[3].Type.Should().Be(TokenType.Equal);
        nonTriviaTokens[3].Text.Should().Be("=");
        
        nonTriviaTokens[4].Type.Should().Be(TokenType.StringLiteral);
        nonTriviaTokens[4].Text.Should().Be("\"hello\"");
        nonTriviaTokens[4].Value.Should().Be("hello");
        
        nonTriviaTokens[5].Type.Should().Be(TokenType.Semicolon);
        nonTriviaTokens[5].Text.Should().Be(";");

        lexer.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("LOCAL", TokenType.Local)]
    [InlineData("GLOBAL", TokenType.Global)]
    [InlineData("CLASS", TokenType.Class)]
    [InlineData("METHOD", TokenType.Method)]
    [InlineData("IF", TokenType.If)]
    [InlineData("FOR", TokenType.For)]
    [InlineData("WHILE", TokenType.While)]
    [InlineData("ARRAY", TokenType.Array)]
    [InlineData("ARRAY2", TokenType.Array2)]
    [InlineData("STRING", TokenType.String)]
    [InlineData("INTEGER", TokenType.Integer)]
    [InlineData("BOOLEAN", TokenType.Boolean)]
    [InlineData("AND", TokenType.And)]
    [InlineData("OR", TokenType.Or)]
    [InlineData("NOT", TokenType.Not)]
    public void Should_Recognize_Keywords(string keyword, TokenType expectedType)
    {
        var lexer = new SelfHostedLexer(keyword);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(2); // keyword + EOF
        nonTriviaTokens[0].Type.Should().Be(expectedType);
        nonTriviaTokens[0].Text.Should().Be(keyword);
    }

    [Theory]
    [InlineData("END-CLASS", TokenType.EndClass)]
    [InlineData("END-IF", TokenType.EndIf)]
    [InlineData("END-FOR", TokenType.EndFor)]
    [InlineData("END-WHILE", TokenType.EndWhile)]
    [InlineData("END-METHOD", TokenType.EndMethod)]
    [InlineData("END-FUNCTION", TokenType.EndFunction)]
    public void Should_Recognize_End_Keywords(string keyword, TokenType expectedType)
    {
        var lexer = new SelfHostedLexer(keyword);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(2); // keyword + EOF
        nonTriviaTokens[0].Type.Should().Be(expectedType);
        nonTriviaTokens[0].Text.Should().Be(keyword);
    }

    [Theory]
    [InlineData("+", TokenType.Plus)]
    [InlineData("-", TokenType.Minus)]
    [InlineData("*", TokenType.Star)]
    [InlineData("/", TokenType.Div)]
    [InlineData("**", TokenType.Power)]
    [InlineData("=", TokenType.Equal)]
    [InlineData("<>", TokenType.NotEqual)]
    [InlineData("!=", TokenType.NotEqual)]
    [InlineData("<", TokenType.LessThan)]
    [InlineData("<=", TokenType.LessThanOrEqual)]
    [InlineData(">", TokenType.GreaterThan)]
    [InlineData(">=", TokenType.GreaterThanOrEqual)]
    [InlineData("|", TokenType.Pipe)]
    [InlineData("+=", TokenType.PlusEqual)]
    [InlineData("-=", TokenType.MinusEqual)]
    [InlineData("|=", TokenType.PipeEqual)]
    public void Should_Recognize_Operators(string op, TokenType expectedType)
    {
        var lexer = new SelfHostedLexer(op);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(2); // operator + EOF
        nonTriviaTokens[0].Type.Should().Be(expectedType);
        nonTriviaTokens[0].Text.Should().Be(op);
    }

    [Theory]
    [InlineData("123", TokenType.IntegerLiteral, 123)]
    [InlineData("0", TokenType.IntegerLiteral, 0)]
    [InlineData("-456", TokenType.IntegerLiteral, -456)]
    [InlineData("123.45", TokenType.DecimalLiteral, 123.45)]
    [InlineData("0.0", TokenType.DecimalLiteral, 0.0)]
    [InlineData("-67.89", TokenType.DecimalLiteral, -67.89)]
    public void Should_Recognize_Number_Literals(string literal, TokenType expectedType, object expectedValue)
    {
        var lexer = new SelfHostedLexer(literal);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(2); // literal + EOF
        nonTriviaTokens[0].Type.Should().Be(expectedType);
        nonTriviaTokens[0].Text.Should().Be(literal);
        
        if (expectedType == TokenType.DecimalLiteral)
        {
            nonTriviaTokens[0].Value.Should().BeOfType<decimal>();
            ((decimal)nonTriviaTokens[0].Value!).Should().Be(Convert.ToDecimal(expectedValue));
        }
        else
        {
            nonTriviaTokens[0].Value.Should().Be(expectedValue);
        }
    }

    [Theory]
    [InlineData("\"hello world\"", "hello world")]
    [InlineData("'single quoted'", "single quoted")]
    [InlineData("\"\"", "")]
    [InlineData("''", "")]
    [InlineData("\"quote \"\" inside\"", "quote \" inside")]
    [InlineData("'quote '' inside'", "quote ' inside")]
    public void Should_Recognize_String_Literals(string literal, string expectedValue)
    {
        var lexer = new SelfHostedLexer(literal);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(2); // literal + EOF
        nonTriviaTokens[0].Type.Should().Be(TokenType.StringLiteral);
        nonTriviaTokens[0].Text.Should().Be(literal);
        nonTriviaTokens[0].Value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("TRUE", true)]
    [InlineData("FALSE", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void Should_Recognize_Boolean_Literals(string literal, bool expectedValue)
    {
        var lexer = new SelfHostedLexer(literal);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(2); // literal + EOF
        nonTriviaTokens[0].Type.Should().Be(TokenType.BooleanLiteral);
        nonTriviaTokens[0].Text.Should().Be(literal);
        nonTriviaTokens[0].Value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("&variable", TokenType.UserVariable)]
    [InlineData("&test123", TokenType.UserVariable)]
    [InlineData("&my_var", TokenType.UserVariable)]
    [InlineData("&_private", TokenType.UserVariable)]
    public void Should_Recognize_User_Variables(string identifier, TokenType expectedType)
    {
        var lexer = new SelfHostedLexer(identifier);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(2); // identifier + EOF
        nonTriviaTokens[0].Type.Should().Be(expectedType);
        nonTriviaTokens[0].Text.Should().Be(identifier);
    }

    [Theory]
    [InlineData("%USERID", TokenType.SystemVariable)]
    [InlineData("%DATE", TokenType.SystemVariable)]
    [InlineData("%TIME", TokenType.SystemVariable)]
    [InlineData("%THIS", TokenType.SystemVariable)]
    [InlineData("%SUPER", TokenType.Super)]
    [InlineData("%METADATA", TokenType.Metadata)]
    public void Should_Recognize_System_Identifiers(string identifier, TokenType expectedType)
    {
        var lexer = new SelfHostedLexer(identifier);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(2); // identifier + EOF
        nonTriviaTokens[0].Type.Should().Be(expectedType);
        nonTriviaTokens[0].Text.Should().Be(identifier);
    }

    [Theory]
    [InlineData("FIELDDEFAULT")]
    [InlineData("FIELDEDIT")]
    [InlineData("FIELDCHANGE")]
    [InlineData("ROWINIT")]
    [InlineData("SAVEEDIT")]
    [InlineData("WORKFLOW")]
    public void Should_Recognize_Record_Events(string recordEvent)
    {
        var lexer = new SelfHostedLexer(recordEvent);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(2); // record event + EOF
        nonTriviaTokens[0].Type.Should().Be(TokenType.RecordEvent);
        nonTriviaTokens[0].Text.Should().Be(recordEvent);
    }

    [Theory]
    [InlineData("/* simple comment */")]
    [InlineData("/* multi\nline\ncomment */")]
    [InlineData("/**/")]
    public void Should_Recognize_Block_Comments(string comment)
    {
        var lexer = new SelfHostedLexer(comment);
        var tokens = lexer.TokenizeAll();

        tokens.Should().HaveCount(2); // comment + EOF
        tokens[0].Type.Should().Be(TokenType.BlockComment);
        tokens[0].Text.Should().Be(comment);
    }

    [Fact]
    public void Should_Handle_Non_Nested_Block_Comments()
    {
        // PeopleCode /* */ comments do NOT nest - first */ closes the comment
        var source = "/* /* nested markers */ */";
        var lexer = new SelfHostedLexer(source);
        var tokens = lexer.TokenizeAll();

        // Should be: BlockComment, Star, Div, EOF
        tokens.Should().HaveCount(4);
        tokens[0].Type.Should().Be(TokenType.BlockComment);
        tokens[0].Text.Should().Be("/* /* nested markers */");
        tokens[1].Type.Should().Be(TokenType.Star);
        tokens[2].Type.Should().Be(TokenType.Div);
        tokens[3].Type.Should().Be(TokenType.EndOfFile);
    }

    [Theory]
    [InlineData("/** API comment */")]
    [InlineData("/** multi\nline\nAPI comment */")]
    [InlineData("/**\n * Detailed API documentation\n * @param test\n */")]
    public void Should_Recognize_API_Comments(string comment)
    {
        var lexer = new SelfHostedLexer(comment);
        var tokens = lexer.TokenizeAll();

        tokens.Should().HaveCount(2); // comment + EOF
        tokens[0].Type.Should().Be(TokenType.ApiComment);
        tokens[0].Text.Should().Be(comment);
    }

    [Theory]
    [InlineData("<* nested comment *>")]
    [InlineData("<* <* double nested *> *>")]
    [InlineData("<* multi\nline\nnested *>")]
    public void Should_Recognize_Nested_Comments(string comment)
    {
        var lexer = new SelfHostedLexer(comment);
        var tokens = lexer.TokenizeAll();

        tokens.Should().HaveCount(2); // comment + EOF
        tokens[0].Type.Should().Be(TokenType.NestedComment);
        tokens[0].Text.Should().Be(comment);
    }

    [Theory]
    [InlineData("REM This is a comment;")]
    [InlineData("REMARK This is a longer comment;")]
    [InlineData("rem lowercase comment;")]
    public void Should_Recognize_REM_Comments(string comment)
    {
        var lexer = new SelfHostedLexer(comment);
        var tokens = lexer.TokenizeAll();

        tokens.Should().HaveCount(2); // comment + EOF
        tokens[0].Type.Should().Be(TokenType.LineComment);
        tokens[0].Text.Should().Be(comment);
    }

    [Fact]
    public void Should_Handle_Complex_Expression()
    {
        var source = "&result = (&a + &b) * %PI ** 2;";
        var lexer = new SelfHostedLexer(source);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(13); // All tokens + EOF
        
        var expectedTypes = new[]
        {
            TokenType.UserVariable,     // &result
            TokenType.Equal,            // =
            TokenType.LeftParen,        // (
            TokenType.UserVariable,     // &a
            TokenType.Plus,             // +
            TokenType.UserVariable,     // &b
            TokenType.RightParen,       // )
            TokenType.Star,             // *
            TokenType.SystemConstant,   // %PI
            TokenType.Power,            // **
            TokenType.IntegerLiteral,   // 2
            TokenType.Semicolon,        // ;
            TokenType.EndOfFile
        };

        for (int i = 0; i < expectedTypes.Length; i++)
        {
            nonTriviaTokens[i].Type.Should().Be(expectedTypes[i], $"Token {i} should be {expectedTypes[i]}");
        }

        lexer.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Should_Handle_Class_Declaration()
    {
        var source = @"
        CLASS MyClass EXTENDS BaseClass
            METHOD Test() RETURNS string;
        END-CLASS;
        ";

        var lexer = new SelfHostedLexer(source);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        var keywordTokens = nonTriviaTokens.Where(t => t.Type.IsKeyword()).ToList();
        keywordTokens.Should().Contain(t => t.Type == TokenType.Class);
        keywordTokens.Should().Contain(t => t.Type == TokenType.Extends);
        keywordTokens.Should().Contain(t => t.Type == TokenType.Method);
        keywordTokens.Should().Contain(t => t.Type == TokenType.Returns);
        keywordTokens.Should().Contain(t => t.Type == TokenType.String);
        keywordTokens.Should().Contain(t => t.Type == TokenType.EndClass);

        lexer.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Should_Handle_Trivia_Correctly()
    {
        var source = "LOCAL /* comment */ string &test; // More trivia";
        var lexer = new SelfHostedLexer(source);
        var tokens = lexer.TokenizeAll();

        // Check that trivia is attached to tokens
        var localToken = tokens.First(t => t.Type == TokenType.Local);
        var stringToken = tokens.First(t => t.Type == TokenType.String);

        // The comment should be attached as leading trivia to the string token
        stringToken.LeadingTrivia.Should().NotBeEmpty();
        stringToken.LeadingTrivia.Should().Contain(t => t.Type == TokenType.BlockComment);
    }

    [Fact]
    public void Should_Report_Errors_For_Invalid_Input()
    {
        var source = "LOCAL string &test = \"unterminated string;";
        var lexer = new SelfHostedLexer(source);
        var tokens = lexer.TokenizeAll();

        lexer.Errors.Should().NotBeEmpty();
        lexer.Errors[0].Message.Should().Contain("Unterminated string literal");
    }

    [Fact]
    public void Should_Handle_Special_Annotation_Operators()
    {
        var source = "/+ &param AS string +/";
        var lexer = new SelfHostedLexer(source);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        nonTriviaTokens.Should().HaveCount(6); // /+, &param, AS, string, +/, EOF
        nonTriviaTokens[0].Type.Should().Be(TokenType.SlashPlus);
        nonTriviaTokens[4].Type.Should().Be(TokenType.PlusSlash);
    }

    [Fact]
    public void Should_Preserve_Source_Positions()
    {
        var source = "LOCAL\nstring\n&test;";
        var lexer = new SelfHostedLexer(source);
        var tokens = lexer.TokenizeAll();
        var nonTriviaTokens = tokens.Where(t => !t.Type.IsTrivia()).ToList();

        // LOCAL should be at line 1
        nonTriviaTokens[0].SourceSpan.Start.Line.Should().Be(1);
        nonTriviaTokens[0].SourceSpan.Start.Column.Should().Be(1);

        // string should be at line 2
        nonTriviaTokens[1].SourceSpan.Start.Line.Should().Be(2);
        nonTriviaTokens[1].SourceSpan.Start.Column.Should().Be(1);

        // &test should be at line 3
        nonTriviaTokens[2].SourceSpan.Start.Line.Should().Be(3);
        nonTriviaTokens[2].SourceSpan.Start.Column.Should().Be(1);
    }
}