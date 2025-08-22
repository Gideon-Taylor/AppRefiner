using FluentAssertions;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;

namespace PeopleCodeParser.Tests.ParserTests;

public class AstStructurePrintingTests
{
    [Fact]
    public void Should_Print_Simple_Expression_AST_Structure()
    {
        // Arrange
        var sourceCode = "CREATE MyPackage:MyClass(&param1, \"test\")";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull();
        
        // Print the AST structure
        var astStructure = parser.PrintAstStructure(result!, useTreeCharacters: true);
        astStructure.Should().NotBeNullOrEmpty();
        
        // Verify it contains expected node types
        astStructure.Should().Contain("ObjectCreationNode");
        astStructure.Should().Contain("AppClassTypeNode");
        astStructure.Should().Contain("LiteralNode");
        
        // Print to output for manual verification
        System.Console.WriteLine("AST Structure with Tree Characters:");
        System.Console.WriteLine(astStructure);
        
        // Also test simple indentation
        var simpleStructure = parser.PrintAstStructure(result!, useTreeCharacters: false);
        System.Console.WriteLine("\nAST Structure with Simple Indentation:");
        System.Console.WriteLine(simpleStructure);
    }
    
    [Fact]
    public void Should_Print_Complex_Program_AST_Structure()
    {
        // Arrange
        var sourceCode = @"
            LOCAL any &result = CREATE MyPackage:MyClass(&param1, &param2);
            &result.Method();
        ";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        // Assert
        result.Should().NotBeNull();
        
        // Print the AST structure
        var astStructure = parser.PrintAstStructure(result, useTreeCharacters: true);
        astStructure.Should().NotBeNullOrEmpty();
        
        // Verify it contains expected node types
        astStructure.Should().Contain("ProgramNode");
        astStructure.Should().Contain("BlockNode");
        
        // Print to output for manual verification
        System.Console.WriteLine("Complex Program AST Structure:");
        System.Console.WriteLine(astStructure);
    }
    
    [Fact]
    public void Should_Handle_Null_Node_Gracefully()
    {
        // Arrange
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(new List<PeopleCodeParser.SelfHosted.Lexing.Token>());
        
        // Act
        var result = parser.PrintAstStructure(null!);
        
        // Assert
        result.Should().Be("null");
    }
}