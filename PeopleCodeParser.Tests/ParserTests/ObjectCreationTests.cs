using FluentAssertions;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;

namespace PeopleCodeParser.Tests.ParserTests;

public class ObjectCreationTests
{
    [Fact]
    public void Should_Parse_Simple_Object_Creation_Expression()
    {
        // Arrange
        var sourceCode = "CREATE MyPackage:MyClass()";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ObjectCreationNode>();
        
        var objectCreation = result as ObjectCreationNode;
        objectCreation!.Type.Should().BeOfType<AppClassTypeNode>();
        
        var appClassType = objectCreation.Type as AppClassTypeNode;
        appClassType!.ClassName.Should().Be("MyClass");
        appClassType.PackagePath.Should().ContainSingle("MyPackage");
        objectCreation.Arguments.Should().BeEmpty();
        
        parser.Errors.Should().BeEmpty("Parser should not report any errors");
    }
    
    [Fact]
    public void Should_Parse_Object_Creation_With_Arguments()
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
        result.Should().BeOfType<ObjectCreationNode>();
        
        var objectCreation = result as ObjectCreationNode;
        objectCreation!.Type.Should().BeOfType<AppClassTypeNode>();
        
        var appClassType = objectCreation.Type as AppClassTypeNode;
        appClassType!.ClassName.Should().Be("MyClass");
        appClassType.PackagePath.Should().ContainSingle("MyPackage");
        objectCreation.Arguments.Should().HaveCount(2);
        
        parser.Errors.Should().BeEmpty("Parser should not report any errors");
    }
    
    [Fact]
    public void Should_Parse_Object_Creation_With_Nested_Package()
    {
        // Arrange
        var sourceCode = "CREATE MyPackage:SubPackage:MyClass(&param)";
        
        // Act
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ObjectCreationNode>();
        
        var objectCreation = result as ObjectCreationNode;
        objectCreation!.Type.Should().BeOfType<AppClassTypeNode>();
        
        var appClassType = objectCreation.Type as AppClassTypeNode;
        appClassType!.ClassName.Should().Be("MyClass");
        appClassType.PackagePath.Should().BeEquivalentTo(new[] { "MyPackage", "SubPackage" });
        objectCreation.Arguments.Should().HaveCount(1);
        
        parser.Errors.Should().BeEmpty("Parser should not report any errors");
    }
}