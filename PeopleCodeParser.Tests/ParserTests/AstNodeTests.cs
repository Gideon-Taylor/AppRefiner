using FluentAssertions;
using Xunit;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.Tests.SelfHostedTests;

/// <summary>
/// Tests for the self-hosted AST node hierarchy
/// </summary>
public class AstNodeTests
{
    [Fact]
    public void Should_Create_Simple_Program_AST()
    {
        // Create a simple program with a local variable
        var program = new ProgramNode();
        
        var stringType = new BuiltInTypeNode(BuiltInType.String);
        var variable = new VariableNode("test", stringType, VariableScope.Local);
        var literal = new LiteralNode("hello world", LiteralType.String);
        variable.SetInitialValue(literal);
        
        program.AddVariable(variable);
        
        // Verify structure
        program.Should().NotBeNull();
        program.Variables.Should().HaveCount(1);
        program.Variables[0].Name.Should().Be("test");
        program.Variables[0].Type.Should().BeOfType<BuiltInTypeNode>();
        program.Variables[0].InitialValue.Should().BeOfType<LiteralNode>();
        
        // Verify parent-child relationships
        variable.Parent.Should().Be(program);
        stringType.Parent.Should().Be(variable);
        literal.Parent.Should().Be(variable);
    }

    [Fact]
    public void Should_Create_Binary_Expression_AST()
    {
        // Create: a + b * c (should respect precedence)
        var aVar = new IdentifierNode("a", IdentifierType.UserVariable);
        var bVar = new IdentifierNode("b", IdentifierType.UserVariable);
        var cVar = new IdentifierNode("c", IdentifierType.UserVariable);
        
        // b * c (higher precedence)
        var multiply = new BinaryOperationNode(bVar, BinaryOperator.Multiply, cVar);
        
        // a + (b * c)
        var add = new BinaryOperationNode(aVar, BinaryOperator.Add, multiply);
        
        // Verify structure
        add.Left.Should().Be(aVar);
        add.Operator.Should().Be(BinaryOperator.Add);
        add.Right.Should().Be(multiply);
        
        multiply.Left.Should().Be(bVar);
        multiply.Operator.Should().Be(BinaryOperator.Multiply);
        multiply.Right.Should().Be(cVar);
        
        // Verify parent-child relationships
        aVar.Parent.Should().Be(add);
        multiply.Parent.Should().Be(add);
        bVar.Parent.Should().Be(multiply);
        cVar.Parent.Should().Be(multiply);
        
        // Test string representation
        add.ToString().Should().Be("(&a + (&b * &c))");
    }

    [Fact]
    public void Should_Create_If_Statement_AST()
    {
        // Create: IF condition THEN statement1; ELSE statement2; END-IF;
        var condition = new IdentifierNode("condition", IdentifierType.UserVariable);
        
        var stmt1 = new ExpressionStatementNode(
            new AssignmentNode(
                new IdentifierNode("x", IdentifierType.UserVariable),
                AssignmentOperator.Assign,
                new LiteralNode(1, LiteralType.Integer)
            )
        );
        
        var stmt2 = new ExpressionStatementNode(
            new AssignmentNode(
                new IdentifierNode("x", IdentifierType.UserVariable),
                AssignmentOperator.Assign,
                new LiteralNode(2, LiteralType.Integer)
            )
        );
        
        var thenBlock = new BlockNode(new[] { stmt1 });
        var elseBlock = new BlockNode(new[] { stmt2 });
        
        var ifStmt = new IfStatementNode(condition, thenBlock);
        ifStmt.SetElseBlock(elseBlock);
        
        // Verify structure
        ifStmt.Condition.Should().Be(condition);
        ifStmt.ThenBlock.Should().Be(thenBlock);
        ifStmt.ElseBlock.Should().Be(elseBlock);
        
        ifStmt.ThenBlock.Statements.Should().HaveCount(1);
        ifStmt.ElseBlock!.Statements.Should().HaveCount(1);
        
        // Verify parent-child relationships
        condition.Parent.Should().Be(ifStmt);
        thenBlock.Parent.Should().Be(ifStmt);
        elseBlock.Parent.Should().Be(ifStmt);
        stmt1.Parent.Should().Be(thenBlock);
        stmt2.Parent.Should().Be(elseBlock);
    }

    [Fact]
    public void Should_Create_Class_Definition_AST()
    {
        // Create a simple class with a method and property
        var appClass = new AppClassNode("TestClass");
        
        // Add base class
        var baseClassType = new AppClassTypeNode("BasePackage:BaseClass");
        appClass.SetBaseClass(baseClassType);
        
        // Add a method
        var method = new MethodNode("GetValue");
        var returnType = new BuiltInTypeNode(BuiltInType.String);
        method.SetReturnType(returnType);
        appClass.AddMember(method, VisibilityModifier.Public);
        
        // Add a property
        var propertyType = new BuiltInTypeNode(BuiltInType.Integer);
        var property = new PropertyNode("Count", propertyType);
        property.HasGet = true;
        property.HasSet = true;
        appClass.AddMember(property, VisibilityModifier.Private);
        
        // Verify structure
        appClass.Name.Should().Be("TestClass");
        appClass.BaseClass.Should().Be(baseClassType);
        appClass.Methods.Should().HaveCount(1);
        appClass.Properties.Should().HaveCount(1);
        
        appClass.Methods[0].Name.Should().Be("GetValue");
        appClass.Methods[0].ReturnType.Should().Be(returnType);
        
        appClass.Properties[0].Name.Should().Be("Count");
        appClass.Properties[0].Type.Should().Be(propertyType);
        
        // Verify visibility sections
        appClass.VisibilitySections[VisibilityModifier.Public].Should().Contain(method);
        appClass.VisibilitySections[VisibilityModifier.Private].Should().Contain(property);
        
        // Verify parent-child relationships
        baseClassType.Parent.Should().Be(appClass);
        method.Parent.Should().Be(appClass);
        property.Parent.Should().Be(appClass);
        returnType.Parent.Should().Be(method);
        propertyType.Parent.Should().Be(property);
    }

    [Fact]
    public void Should_Handle_Array_Types()
    {
        // Test different array types
        var array1D = new ArrayTypeNode(1);
        var array2D = new ArrayTypeNode(2, new BuiltInTypeNode(BuiltInType.String));
        var array3D = new ArrayTypeNode(3);
        
        array1D.TypeName.Should().Be("ARRAY");
        array1D.Dimensions.Should().Be(1);
        array1D.ElementType.Should().BeNull();
        
        array2D.TypeName.Should().Be("ARRAY2 OF STRING");
        array2D.Dimensions.Should().Be(2);
        array2D.ElementType.Should().NotBeNull();
        array2D.ElementType!.TypeName.Should().Be("STRING");
        
        array3D.TypeName.Should().Be("ARRAY3");
        array3D.Dimensions.Should().Be(3);
        array3D.ElementType.Should().BeNull();
    }

    [Fact]
    public void Should_Handle_App_Class_Types()
    {
        // Test different app class type formats
        var simpleClass = new AppClassTypeNode("MyClass");
        var packagedClass = new AppClassTypeNode("MyPackage:MyClass");
        var deepPackage = new AppClassTypeNode("Level1:Level2:Level3:MyClass");
        
        simpleClass.QualifiedName.Should().Be("MyClass");
        simpleClass.ClassName.Should().Be("MyClass");
        simpleClass.PackagePath.Should().BeEmpty();
        
        packagedClass.QualifiedName.Should().Be("MyPackage:MyClass");
        packagedClass.ClassName.Should().Be("MyClass");
        packagedClass.PackagePath.Should().Equal("MyPackage");
        
        deepPackage.QualifiedName.Should().Be("Level1:Level2:Level3:MyClass");
        deepPackage.ClassName.Should().Be("MyClass");
        deepPackage.PackagePath.Should().Equal("Level1", "Level2", "Level3");
    }

    [Fact]
    public void Should_Handle_Import_Statements()
    {
        // Test different import formats
        var wildcardImport = new ImportNode("MyPackage:Utilities:*");
        var specificImport = new ImportNode("MyPackage:Utils:Logger");
        
        wildcardImport.IsWildcard.Should().BeTrue();
        wildcardImport.ClassName.Should().BeNull();
        wildcardImport.PackagePath.Should().Equal("MyPackage", "Utilities");
        wildcardImport.FullPath.Should().Be("MyPackage:Utilities:*");
        
        specificImport.IsWildcard.Should().BeFalse();
        specificImport.ClassName.Should().Be("Logger");
        specificImport.PackagePath.Should().Equal("MyPackage", "Utils");
        specificImport.FullPath.Should().Be("MyPackage:Utils:Logger");
    }

    [Fact]
    public void Should_Support_Visitor_Pattern()
    {
        // Create a simple AST
        var program = new ProgramNode();
        var literal = new LiteralNode(42, LiteralType.Integer);
        var exprStmt = new ExpressionStatementNode(literal);
        var block = new BlockNode(new[] { exprStmt });
        program.SetMainBlock(block);
        
        // Create a visitor that counts nodes
        var visitor = new NodeCountingVisitor();
        program.Accept(visitor);
        
        visitor.NodeCount.Should().Be(4); // Program, Block, ExpressionStatement, Literal
    }

    [Fact]
    public void Should_Find_Ancestors_And_Descendants()
    {
        // Create a nested AST structure
        var program = new ProgramNode();
        var appClass = new AppClassNode("TestClass");
        var method = new MethodNode("TestMethod");
        var block = new BlockNode();
        var ifStmt = new IfStatementNode(
            new LiteralNode(true, LiteralType.Boolean),
            new BlockNode()
        );
        var literal = new LiteralNode(123, LiteralType.Integer);
        var exprStmt = new ExpressionStatementNode(literal);
        
        // Build hierarchy: Program -> Class -> Method -> Block -> If -> Block -> ExprStmt -> Literal
        program.SetAppClass(appClass);
        appClass.AddMember(method);
        method.SetBody(block);
        block.AddStatement(ifStmt);
        ifStmt.ThenBlock.AddStatement(exprStmt);
        
        // Test ancestor finding
        literal.FindAncestor<MethodNode>().Should().Be(method);
        literal.FindAncestor<AppClassNode>().Should().Be(appClass);
        literal.FindAncestor<ProgramNode>().Should().Be(program);
        literal.FindAncestor<VariableNode>().Should().BeNull();
        
        // Test descendant finding
        var literals = program.FindDescendants<LiteralNode>().ToList();
        literals.Should().HaveCount(2); // The boolean condition and the integer literal
        literals.Should().Contain(literal);
        
        var methods = program.FindDescendants<MethodNode>().ToList();
        methods.Should().HaveCount(1);
        methods[0].Should().Be(method);
    }

    [Fact]
    public void Should_Handle_Source_Spans()
    {
        var span1 = new SourceSpan(new SourcePosition(0, 1, 1), new SourcePosition(10, 1, 11));
        var span2 = new SourceSpan(5, 15);
        
        span1.Start.Index.Should().Be(0);
        span1.Start.Line.Should().Be(1);
        span1.Start.Column.Should().Be(1);
        span1.End.Index.Should().Be(10);
        span1.Length.Should().Be(10);
        span1.IsEmpty.Should().BeFalse();
        
        span2.Start.Index.Should().Be(5);
        span2.End.Index.Should().Be(15);
        span2.Length.Should().Be(10);
        
        // Test equality
        var span3 = new SourceSpan(5, 15);
        span2.Should().Be(span3);
        span1.Should().NotBe(span2);
    }
}

/// <summary>
/// Test visitor that counts nodes
/// </summary>
public class NodeCountingVisitor : AstVisitorBase
{
    public int NodeCount { get; private set; }

    protected override void DefaultVisit(AstNode node)
    {
        NodeCount++;
        base.DefaultVisit(node);
    }
}