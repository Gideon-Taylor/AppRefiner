using FluentAssertions;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;

namespace PeopleCodeParser.Tests.ParserTests;

public class TypeCastPrecedenceValidation
{
    [Fact]
    public void Arithmetic_Plus_TypeCast_Should_Parse_As_Arithmetic_First()
    {
        // Test: &a + &b AS MyClass should parse as (&a + &b) AS MyClass
        var sourceCode = "&a + &b AS MyClass";
        
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Should be: TypeCastNode { Expression: BinaryOperationNode(+), TargetType: MyClass }
        result.Should().BeOfType<TypeCastNode>();
        var typeCast = result as TypeCastNode;
        typeCast!.Expression.Should().BeOfType<BinaryOperationNode>();
        
        var binaryOp = typeCast.Expression as BinaryOperationNode;
        binaryOp!.Operator.Should().Be(BinaryOperator.Add);
        
        parser.Errors.Should().BeEmpty();
    }
    
    [Fact]
    public void TypeCast_Then_DotAccess_Should_Parse_As_Cast_First()
    {
        // Test: &obj AS MyClass.method() should parse as (&obj AS MyClass).method()
        var sourceCode = "&obj AS MyClass.method()";
        
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Should be: FunctionCallNode { Function: MemberAccessNode { Target: TypeCastNode } }
        result.Should().BeOfType<FunctionCallNode>();
        var funcCall = result as FunctionCallNode;
        funcCall!.Function.Should().BeOfType<MemberAccessNode>();
        
        var memberAccess = funcCall.Function as MemberAccessNode;
        memberAccess!.Target.Should().BeOfType<TypeCastNode>();
        
        parser.Errors.Should().BeEmpty();
    }
    
    [Fact]  
    public void TypeCast_Then_ArrayAccess_Should_Parse_As_Cast_First()
    {
        // Test: &obj AS MyClass[0] should parse as (&obj AS MyClass)[0]
        var sourceCode = "&obj AS MyClass[0]";
        
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        // Should be: ArrayIndexNode { Array: TypeCastNode }
        result.Should().BeOfType<ArrayIndexNode>();
        var arrayAccess = result as ArrayIndexNode;
        arrayAccess!.Array.Should().BeOfType<TypeCastNode>();
        
        parser.Errors.Should().BeEmpty();
    }
}