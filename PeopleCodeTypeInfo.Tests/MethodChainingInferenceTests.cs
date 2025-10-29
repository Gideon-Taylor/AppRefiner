using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Tests for method chaining type inference including:
/// - Message.GetXmlDoc() returns XmlDoc
/// - XmlDoc.GenXmlString() returns string
/// - Full method chain inference: obj.method1().method2().method3() -> string
/// </summary>
public class MethodChainingInferenceTests
{
    /// <summary>
    /// Test method chaining through multiple return types:
    /// %This.onAuthRequest(&msgAuthReq).GetXmlDoc().GenXmlString()
    ///
    /// Verifies:
    /// - %This.onAuthRequest() returns Message
    /// - .GetXmlDoc() returns XmlDoc
    /// - .GenXmlString() returns string
    /// - CreateSOAPDoc() accepts string argument
    /// - All intermediate expressions are properly typed (not unknown)
    /// </summary>
    [Fact]
    public void MethodChaining_MessageGetXmlDocGenXmlString_InfersStringType()
    {
        var source = @"
class A
   method Test();
   method onAuthRequest(&msg As Message) Returns Message;
private
   instance Message &msgAuthReq;
end-class;

method Test
   Local Message &message = %This.onAuthRequest(&msgAuthReq);
   Local XmlDoc &xmlDoc1 = &message.GetXmlDoc();
   Local XmlDoc &xmlDoc2 = %This.onAuthRequest(&msgAuthReq).GetXmlDoc();

   Local string &string1 = &xmlDoc1.GenXmlString();
   Local string &string2 = %This.onAuthRequest(&msgAuthReq).GetXmlDoc().GenXmlString();

   Local SOAPDoc &s = CreateSOAPDoc(%This.onAuthRequest(&msgAuthReq).GetXmlDoc().GenXmlString());
end-method;

method onAuthRequest
   /+ &msg as Message +/
   /+ Returns Message +/
   Return &msg;
end-method;
";

        // Parse the source
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        // Extract metadata
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram:A");

        // Run type inference
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance, cache);

        // Find the Test method implementation
        Assert.NotNull(program.AppClass);
        var testMethod = program.AppClass.Methods.FirstOrDefault(m =>
            m.Name.Equals("Test", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(testMethod);

        // Find all local variable declarations
        var localDecls = FindAllLocalDeclarations(testMethod);

        // Test 1: &message = %This.onAuthRequest(&msgAuthReq) should be Message
        var messageDecl = localDecls.FirstOrDefault(d =>
            d.VariableName.Equals("&message", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(messageDecl);

        var messageInitializer = messageDecl.InitialValue;
        Assert.NotNull(messageInitializer);

        var messageType = messageInitializer.GetInferredType();
        Assert.NotNull(messageType);
        Assert.Equal(TypeKind.BuiltinObject, messageType.Kind);
        Assert.Equal(PeopleCodeType.Message, messageType.PeopleCodeType);
        Assert.Equal("Message", messageType.Name,true);

        // Test 2: &xmlDoc1 = &message.GetXmlDoc() should be XmlDoc
        var xmlDoc1Decl = localDecls.FirstOrDefault(d =>
            d.VariableName.Equals("&xmlDoc1", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(xmlDoc1Decl);

        var xmlDoc1Initializer = xmlDoc1Decl.InitialValue;
        Assert.NotNull(xmlDoc1Initializer);

        var xmlDoc1Type = xmlDoc1Initializer.GetInferredType();
        Assert.NotNull(xmlDoc1Type);
        Assert.Equal(TypeKind.BuiltinObject, xmlDoc1Type.Kind);
        Assert.Equal(PeopleCodeType.Xmldoc, xmlDoc1Type.PeopleCodeType);
        Assert.Equal("Xmldoc", xmlDoc1Type.Name, true);

        // Test 3: &xmlDoc2 = %This.onAuthRequest(&msgAuthReq).GetXmlDoc() should be XmlDoc
        var xmlDoc2Decl = localDecls.FirstOrDefault(d =>
            d.VariableName.Equals("&xmlDoc2", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(xmlDoc2Decl);

        var xmlDoc2Initializer = xmlDoc2Decl.InitialValue;
        Assert.NotNull(xmlDoc2Initializer);

        var xmlDoc2Type = xmlDoc2Initializer.GetInferredType();
        Assert.NotNull(xmlDoc2Type);
        Assert.Equal(TypeKind.BuiltinObject, xmlDoc2Type.Kind);
        Assert.Equal(PeopleCodeType.Xmldoc, xmlDoc2Type.PeopleCodeType);
        Assert.Equal("Xmldoc", xmlDoc2Type.Name, true);

        // Test 4: &string1 = &xmlDoc1.GenXmlString() should be string
        var string1Decl = localDecls.FirstOrDefault(d =>
            d.VariableName.Equals("&string1", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(string1Decl);

        var string1Initializer = string1Decl.InitialValue;
        Assert.NotNull(string1Initializer);

        var string1Type = string1Initializer.GetInferredType();
        Assert.NotNull(string1Type);
        Assert.Equal(TypeKind.Primitive, string1Type.Kind);
        Assert.Equal(PeopleCodeType.String, string1Type.PeopleCodeType);

        // Test 5: &string2 = %This.onAuthRequest(&msgAuthReq).GetXmlDoc().GenXmlString() should be string
        var string2Decl = localDecls.FirstOrDefault(d =>
            d.VariableName.Equals("&string2", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(string2Decl);

        var string2Initializer = string2Decl.InitialValue;
        Assert.NotNull(string2Initializer);

        var string2Type = string2Initializer.GetInferredType();
        Assert.NotNull(string2Type);
        Assert.Equal(TypeKind.Primitive, string2Type.Kind);
        Assert.Equal(PeopleCodeType.String, string2Type.PeopleCodeType);

        // Test 6: CreateSOAPDoc(%This.onAuthRequest(&msgAuthReq).GetXmlDoc().GenXmlString())
        // The argument should be inferred as string
        var soapDocDecl = localDecls.FirstOrDefault(d =>
            d.VariableName.Equals("&s", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(soapDocDecl);

        var soapDocInitializer = soapDocDecl.InitialValue;
        Assert.NotNull(soapDocInitializer);

        // soapDocInitializer should be a FunctionCallNode
        var createSOAPDocCall = soapDocInitializer as FunctionCallNode;
        Assert.NotNull(createSOAPDocCall);

        // Check that CreateSOAPDoc has one argument
        Assert.Single(createSOAPDocCall.Arguments);

        // The argument is the full method chain
        var argumentExpr = createSOAPDocCall.Arguments[0];
        var argumentType = argumentExpr.GetInferredType();
        Assert.NotNull(argumentType);
        Assert.Equal(TypeKind.Primitive, argumentType.Kind);
        Assert.Equal(PeopleCodeType.String, argumentType.PeopleCodeType);
    }

    // Helper method to find all local declarations in a method
    private static List<LocalVariableDeclarationWithAssignmentNode> FindAllLocalDeclarations(MethodNode method)
    {
        var collector = new LocalDeclarationCollector();
        method.Accept(collector);
        return collector.LocalDeclarations;
    }

    // Helper visitor to collect all local declaration nodes
    private class LocalDeclarationCollector : AstVisitorBase
    {
        public List<LocalVariableDeclarationWithAssignmentNode> LocalDeclarations { get; } = new();

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            LocalDeclarations.Add(node);
            base.VisitLocalVariableDeclarationWithAssignment(node);
        }
    }
}
