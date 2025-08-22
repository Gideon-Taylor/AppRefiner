using FluentAssertions;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.Tests.Utilities;

namespace PeopleCodeParser.Tests.ParserTests;

public class LocalVariableDeclarationTests
{
    [Fact]
    public void Should_Parse_Simple_Local_Variable_Declaration()
    {
        // Arrange
        var sourceCode = "LOCAL string &myVar;";
        
        // Act
        var result = TestHelper.ParseAndAssertSuccess(sourceCode);
        
        // Assert
        result.Should().NotBeNull();
        result.MainBlock.Should().NotBeNull();
        result.MainBlock!.Statements.Should().HaveCount(1);
        
        var localVarDecl = result.MainBlock.Statements[0].Should().BeOfType<LocalVariableDeclarationNode>().Subject;
        localVarDecl.Type.Should().BeOfType<BuiltInTypeNode>();
        localVarDecl.VariableNames.Should().ContainSingle("&myVar");
    }
    
    [Fact]
    public void Should_Parse_Local_Variable_Declaration_With_Assignment()
    {
        // Arrange
        var sourceCode = "LOCAL string &myVar = \"test value\";";
        
        // Act
        var result = TestHelper.ParseAndAssertSuccess(sourceCode);
        
        // Assert
        result.Should().NotBeNull();
        result.MainBlock.Should().NotBeNull();
        result.MainBlock!.Statements.Should().HaveCount(1);
        
        var localVarDecl = result.MainBlock.Statements[0].Should().BeOfType<LocalVariableDeclarationWithAssignmentNode>().Subject;
        localVarDecl.Type.Should().BeOfType<BuiltInTypeNode>();
        localVarDecl.VariableName.Should().Be("&myVar");
        localVarDecl.InitialValue.Should().BeOfType<LiteralNode>();
    }
    
    [Fact]
    public void Should_Parse_Multiple_Local_Variables()
    {
        // Arrange
        var sourceCode = "LOCAL string &var1, &var2, &var3;";
        
        // Act
        var result = TestHelper.ParseAndAssertSuccess(sourceCode);
        
        // Assert
        result.Should().NotBeNull();
        result.MainBlock.Should().NotBeNull();
        result.MainBlock!.Statements.Should().HaveCount(1);
        
        var localVarDecl = result.MainBlock.Statements[0].Should().BeOfType<LocalVariableDeclarationNode>().Subject;
        localVarDecl.Type.Should().BeOfType<BuiltInTypeNode>();
        localVarDecl.VariableNames.Should().BeEquivalentTo(new[] { "&var1", "&var2", "&var3" });
    }
    
    [Fact]
    public void Should_Parse_Local_Variable_With_Trailing_Comma()
    {
        // Arrange
        var sourceCode = "LOCAL integer &count, &total,;";
        
        // Act
        var result = TestHelper.ParseAndAssertSuccess(sourceCode);
        
        // Assert
        result.Should().NotBeNull();
        result.MainBlock.Should().NotBeNull();
        result.MainBlock!.Statements.Should().HaveCount(1);
        
        var localVarDecl = result.MainBlock.Statements[0].Should().BeOfType<LocalVariableDeclarationNode>().Subject;
        localVarDecl.VariableNames.Should().BeEquivalentTo(new[] { "&count", "&total" });
    }
    
    [Fact]
    public void Should_Parse_Local_Variable_With_Complex_Type()
    {
        // Arrange
        var sourceCode = "LOCAL array of string &names;";
        
        // Act
        var result = TestHelper.ParseAndAssertSuccess(sourceCode);
        
        // Assert
        result.Should().NotBeNull();
        result.MainBlock.Should().NotBeNull();
        result.MainBlock!.Statements.Should().HaveCount(1);
        
        var localVarDecl = result.MainBlock.Statements[0].Should().BeOfType<LocalVariableDeclarationNode>().Subject;
        localVarDecl.Type.Should().BeOfType<ArrayTypeNode>();
        localVarDecl.VariableNames.Should().ContainSingle("&names");
    }
    
    [Fact]
    public void Should_Parse_Local_Variable_With_App_Class_Type()
    {
        // Arrange
        var sourceCode = "LOCAL MyPackage:MyClass &obj;";
        
        // Act
        var result = TestHelper.ParseAndAssertSuccess(sourceCode);
        
        // Assert
        result.Should().NotBeNull();
        result.MainBlock.Should().NotBeNull();
        result.MainBlock!.Statements.Should().HaveCount(1);
        
        var localVarDecl = result.MainBlock.Statements[0].Should().BeOfType<LocalVariableDeclarationNode>().Subject;
        localVarDecl.Type.Should().BeOfType<AppClassTypeNode>();
        localVarDecl.VariableNames.Should().ContainSingle("&obj");
    }
    
    [Fact]
    public void Should_Parse_Local_Variable_Assignment_With_Complex_Expression()
    {
        // Arrange
        var sourceCode = "LOCAL any &result = CREATE MyPackage:MyClass(&param1, &param2);";
        
        // Act
        var result = TestHelper.ParseAndAssertSuccess(sourceCode);
        
        // Assert
        result.Should().NotBeNull();
        result.MainBlock.Should().NotBeNull();
        result.MainBlock!.Statements.Should().HaveCount(1);
        
        var localVarDecl = result.MainBlock.Statements[0].Should().BeOfType<LocalVariableDeclarationWithAssignmentNode>().Subject;
        localVarDecl.Type.Should().BeOfType<BuiltInTypeNode>();
        localVarDecl.VariableName.Should().Be("&result");
        localVarDecl.InitialValue.Should().BeOfType<ObjectCreationNode>();
    }
    
    [Fact]
    public void Should_Handle_Missing_Type_Error()
    {
        // Arrange
        var sourceCode = "LOCAL &missingType;";
        
        // Act & Assert
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        // Should have errors but still produce an AST
        parser.Errors.Should().NotBeEmpty();
        parser.Errors.Should().Contain(e => e.Message.Contains("Expected variable type after 'LOCAL'"));
    }
    
    [Fact]
    public void Should_Handle_Missing_Variable_Name_Error()
    {
        // Arrange
        var sourceCode = "LOCAL string;";
        
        // Act & Assert
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        // Should have errors but still produce an AST
        parser.Errors.Should().NotBeEmpty();
        parser.Errors.Should().Contain(e => e.Message.Contains("Expected variable name"));
    }
    
    [Fact]
    public void Should_Handle_Missing_Assignment_Expression_Error()
    {
        // Arrange
        var sourceCode = "LOCAL string &var =;";
        
        // Act & Assert
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        // Should have errors but still produce an AST
        parser.Errors.Should().NotBeEmpty();
        parser.Errors.Should().Contain(e => e.Message.Contains("Expected expression after '='"));
    }
    
    [Fact]
    public void Should_Print_AST_Structure_For_Local_Variables()
    {
        // Arrange
        var sourceCode = "LOCAL string &var1, &var2; LOCAL integer &count = 10;";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        // Assert
        result.Should().NotBeNull();
        parser.Errors.Should().BeEmpty();
        
        // Print AST structure to verify hierarchy
        var astStructure = parser.PrintAstStructure(result, useTreeCharacters: true);
        astStructure.Should().Contain("LocalVariableDeclarationNode");
        astStructure.Should().Contain("LocalVariableDeclarationWithAssignmentNode");
        
        // Output for manual verification
        System.Console.WriteLine("Local Variable AST Structure:");
        System.Console.WriteLine(astStructure);
    }
}