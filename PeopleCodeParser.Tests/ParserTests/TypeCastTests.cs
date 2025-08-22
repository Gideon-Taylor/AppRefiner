using FluentAssertions;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;

namespace PeopleCodeParser.Tests.ParserTests;

public class TypeCastTests
{
    [Fact]
    public void Should_Parse_Simple_Type_Cast_With_AppClass()
    {
        // Arrange
        var sourceCode = "&obj AS MyPackage:MyClass";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<TypeCastNode>();
        
        var typeCast = result as TypeCastNode;
        typeCast!.Expression.Should().BeOfType<IdentifierNode>();
        typeCast.TargetType.Should().BeOfType<AppClassTypeNode>();
        
        var appClassType = typeCast.TargetType as AppClassTypeNode;
        appClassType!.ClassName.Should().Be("MyClass");
        appClassType.PackagePath.Should().ContainSingle("MyPackage");
        
        parser.Errors.Should().BeEmpty("Parser should not report any errors");
    }
    
    [Fact]
    public void Should_Parse_Type_Cast_With_Simple_Class_Name()
    {
        // Arrange
        var sourceCode = "&obj AS MyClass";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<TypeCastNode>();
        
        var typeCast = result as TypeCastNode;
        typeCast!.Expression.Should().BeOfType<IdentifierNode>();
        typeCast.TargetType.Should().BeOfType<AppClassTypeNode>();
        
        var appClassType = typeCast.TargetType as AppClassTypeNode;
        appClassType!.ClassName.Should().Be("MyClass");
        appClassType.PackagePath.Should().BeEmpty();
        
        parser.Errors.Should().BeEmpty("Parser should not report any errors");
    }
    
    [Fact]
    public void Should_Parse_Type_Cast_With_Built_In_Type()
    {
        // Arrange
        var sourceCode = "&obj AS STRING";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<TypeCastNode>();
        
        var typeCast = result as TypeCastNode;
        typeCast!.Expression.Should().BeOfType<IdentifierNode>();
        typeCast.TargetType.Should().BeOfType<BuiltInTypeNode>();
        
        var builtInType = typeCast.TargetType as BuiltInTypeNode;
        builtInType!.Type.Should().Be(BuiltInType.String);
        
        parser.Errors.Should().BeEmpty("Parser should not report any errors");
    }
    
    
    [Fact]
    public void Should_Handle_Missing_Type_Specifier()
    {
        // Arrange
        var sourceCode = "&obj AS";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull(); // Should return the original expression before AS
        result.Should().BeOfType<IdentifierNode>();
        parser.Errors.Should().ContainSingle(error => 
            error.Message.Contains("Expected type specifier after 'AS'"));
    }
    
    [Fact]
    public void Should_Handle_Invalid_Type_Specifier()
    {
        // Arrange - using a keyword that's not a valid type
        var sourceCode = "&obj AS IF";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull(); // Should return the original expression before AS
        result.Should().BeOfType<IdentifierNode>();
        parser.Errors.Should().NotBeEmpty();
    }
    
    [Fact]
    public void Should_Parse_Complex_Expression_With_Type_Cast()
    {
        // Arrange - Real-world scenario: method call result cast to interface
        var sourceCode = "getObject().getValue() AS MyInterface";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<TypeCastNode>();
        
        var typeCast = result as TypeCastNode;
        typeCast!.Expression.Should().BeOfType<FunctionCallNode>();
        typeCast.TargetType.Should().BeOfType<AppClassTypeNode>();
        
        var appClassType = typeCast.TargetType as AppClassTypeNode;
        appClassType!.ClassName.Should().Be("MyInterface");
        
        parser.Errors.Should().BeEmpty("Parser should not report any errors");
    }
    
    [Fact]
    public void Should_Handle_Type_Cast_In_Assignment()
    {
        // Arrange - Type cast in assignment context
        var sourceCode = "&result = &obj AS Package:MyClass";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<AssignmentNode>();
        
        var assignment = result as AssignmentNode;
        assignment!.Value.Should().BeOfType<TypeCastNode>();
        
        var typeCast = assignment.Value as TypeCastNode;
        typeCast!.Expression.Should().BeOfType<IdentifierNode>();
        
        parser.Errors.Should().BeEmpty("Parser should not report any errors");
    }
    
    [Fact]
    public void Should_Parse_Type_Cast_With_Nested_Package_Path()
    {
        // Arrange - Deep package hierarchy
        var sourceCode = "&obj AS Level1:Level2:MyClass;";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<TypeCastNode>();
        
        var typeCast = result as TypeCastNode;
        typeCast!.TargetType.Should().BeOfType<AppClassTypeNode>();
        
        var appClassType = typeCast.TargetType as AppClassTypeNode;
        appClassType!.ClassName.Should().Be("MyClass");
        appClassType.PackagePath.Should().BeEquivalentTo(new[] { "Level1", "Level2"});
        
        parser.Errors.Should().BeEmpty("Parser should not report any errors");
    }
}