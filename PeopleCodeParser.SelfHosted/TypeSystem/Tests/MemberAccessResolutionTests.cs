using System;
using System.Linq;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Extensions;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.TypeSystem;
using PeopleCodeParser.SelfHosted.TypeSystem.Tests.Infrastructure;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

[Collection("TypeSystemCache")]
public class MemberAccessResolutionTests : TypeInferenceTestBase
{
    private const string BaseClassSource = @"
class BaseClass
end-class;
";

    private const string ClassSource = @"
import SamplePkg:BaseClass;
class SampleClass extends SamplePkg:BaseClass
   property string MyProperty get set;
   method ProtectedMethod(&value As number) returns number;
end-class;
";

    private const string ShorthandClassSource = @"
class ShorthandClass
   property string MyProperty get set;
   method Assign();
end-class;

method Assign
   &MyProperty = ""assigned"";
   Local string &copy = &MyProperty;
end-method;
";

    private const string ChildClassSource = @"
class SampleChild
   method GetLabel() returns string;
end-class;
";

    private const string ParentClassSource = @"
import SampleChild;
class SampleParent
   method GetChild() returns SampleChild;
end-class;
";

    private const string ChainedAccessCode = @"
   Local SampleParent &parent;
   Local string &value = &parent.GetChild().GetLabel();
";

    private const string InterfaceSource = @"
interface SampleInterface
   method DoWork(&value As string) returns number;
   property string InterfaceProperty get;
end-interface;
";

    private const string InterfaceImplementationSource = @"
import SamplePkg:SampleInterface;
class SampleImplementation implements SamplePkg:SampleInterface
   method DoWork(&value As string) returns number;
   property string InterfaceProperty get;
end-class;
";

    private const string InterfaceUsageCode = @"
   Local SamplePkg:SampleInterface &contract;
   Local SamplePkg:SampleImplementation &instance = Create SamplePkg:SampleImplementation();
   &contract = &instance;
   Local string &text = &contract.InterfaceProperty;
   Local number &result = &contract.DoWork(""text"");
";

    private const string CodeUnderTest = @"
   Local SamplePkg:SampleClass &obj;
   Local string &text = &obj.MyProperty;
   Local number &result = &obj.ProtectedMethod(1);
";

    private static ProgramNode ParseClassProgram(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();

        PeopleCodeParser.ToolsRelease = new ToolsVersion("99.99.99");
        var parser = new PeopleCodeParser(tokens);
        var program = parser.ParseProgram() ?? throw new InvalidOperationException("Failed to parse class source");

        if (parser.Errors.Any())
        {
            var message = string.Join("\n", parser.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Parser errors: {message}");
        }

        return program;
    }

    private static void PrimeClassCache()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();
        PeopleCodeTypeRegistry.CacheClassInfo(ClassMetadataBuilder.Build(ParseClassProgram(BaseClassSource).AppClass!, "SamplePkg:BaseClass")!);
        var sampleInfo = ClassMetadataBuilder.Build(ParseClassProgram(ClassSource).AppClass!, "SamplePkg:SampleClass")!;
        Assert.NotEmpty(sampleInfo.Properties);
        PeopleCodeTypeRegistry.CacheClassInfo(sampleInfo);
    }

    private static void PrimeInterfaceCache()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();
        PeopleCodeTypeRegistry.CacheClassInfo(ClassMetadataBuilder.Build(ParseClassProgram(InterfaceSource).Interface!, "SamplePkg:SampleInterface")!);
        PeopleCodeTypeRegistry.CacheClassInfo(ClassMetadataBuilder.Build(ParseClassProgram(InterfaceImplementationSource).AppClass!, "SamplePkg:SampleImplementation")!);
    }

    private static void PrimeShorthandCache()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();
        PeopleCodeTypeRegistry.CacheClassInfo(ClassMetadataBuilder.Build(ParseClassProgram(ShorthandClassSource).AppClass!, "ShorthandClass")!);
    }

    private static void PrimeChainedCache()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();
        PeopleCodeTypeRegistry.CacheClassInfo(ClassMetadataBuilder.Build(ParseClassProgram(ChildClassSource).AppClass!, "SampleChild")!);
        PeopleCodeTypeRegistry.CacheClassInfo(ClassMetadataBuilder.Build(ParseClassProgram(ParentClassSource).AppClass!, "SampleParent")!);
    }

    [Fact]
    public async Task MemberAccess_ShouldResolveUsingClassMetadata()
    {
        PrimeClassCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:BaseClass", BaseClassSource),
            new KeyValuePair<string, string>("SamplePkg:SampleClass", ClassSource)
        });

        var program = ParseCode(CodeUnderTest);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        AssertSuccess(result);

        var objIdentifier = program.FindDescendants<IdentifierNode>()
            .First(node => node.Name.Equals("&obj", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(IdentifierType.UserVariable, objIdentifier.IdentifierType);
        var objType = objIdentifier.GetInferredType();
        Assert.IsType<AppClassTypeInfo>(objType);

        var declaration = program.FindDescendants<LocalVariableDeclarationNode>().First();
        Assert.IsType<AppClassTypeNode>(declaration.Type);
        Assert.Equal("&obj", declaration.VariableNames.First());
        Assert.Equal("&obj", declaration.VariableNameInfos.First().Name);

        Assert.True(PeopleCodeTypeRegistry.TryGetClassInfo("SamplePkg:SampleClass", out var classInfo));
        Assert.NotNull(classInfo);
        Assert.Contains("MyProperty", classInfo!.Properties.Keys);

        var propertyAccess = program.FindDescendants<MemberAccessNode>()
            .First(node => node.MemberName.Equals("MyProperty", StringComparison.OrdinalIgnoreCase));
        var targetType = propertyAccess.Target.GetInferredType();
        Assert.NotNull(targetType);
        Assert.IsType<AppClassTypeInfo>(targetType);
        var propertyType = propertyAccess.GetInferredType();
        Assert.IsType<StringTypeInfo>(propertyType);

        var functionCall = program.FindDescendants<FunctionCallNode>()
            .First(call => call.Function is MemberAccessNode member && member.MemberName.Equals("ProtectedMethod", StringComparison.OrdinalIgnoreCase));
        var callType = functionCall.GetInferredType();
        Assert.IsType<NumberTypeInfo>(callType);

        Assert.True(PeopleCodeTypeRegistry.TryGetClassInfo("SamplePkg:SampleClass", out var cached));
        Assert.NotNull(cached);
        Assert.Equal(PrimitiveTypeInfo.String, cached!.Properties["MyProperty"].Type);
        Assert.Equal(PrimitiveTypeInfo.Number, cached.Methods["ProtectedMethod"].ReturnType);
    }

    [Fact]
    public async Task PropertyAssignment_ShouldReportTypeMismatch()
    {
        PrimeClassCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:BaseClass", BaseClassSource),
            new KeyValuePair<string, string>("SamplePkg:SampleClass", ClassSource)
        });

        const string code = @"
   Local SamplePkg:SampleClass &obj;
   &obj.MyProperty = ""valid"";
   &obj.MyProperty = 123;
";

        var program = ParseCode(code);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Kind == TypeErrorKind.TypeMismatch && e.Message.Contains("MyProperty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MethodCall_WithIncorrectArgumentType_ShouldReportError()
    {
        PrimeClassCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:BaseClass", BaseClassSource),
            new KeyValuePair<string, string>("SamplePkg:SampleClass", ClassSource)
        });

        const string code = @"
   Local SamplePkg:SampleClass &obj;
   &obj.ProtectedMethod(""text"");
";

        var program = ParseCode(code);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Kind == TypeErrorKind.TypeMismatch && e.Message.Contains("ProtectedMethod", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MethodCall_WithMissingArgument_ShouldReportError()
    {
        PrimeClassCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:BaseClass", BaseClassSource),
            new KeyValuePair<string, string>("SamplePkg:SampleClass", ClassSource)
        });

        const string code = @"
   Local SamplePkg:SampleClass &obj;
   &obj.ProtectedMethod();
";

        var program = ParseCode(code);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Kind == TypeErrorKind.ArgumentCountMismatch && e.Message.Contains("ProtectedMethod", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateExpression_ShouldInferAppClassType()
    {
        PrimeClassCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:BaseClass", BaseClassSource),
            new KeyValuePair<string, string>("SamplePkg:SampleClass", ClassSource)
        });

        const string code = @"
   Local SamplePkg:SampleClass &obj = Create SamplePkg:SampleClass();
   Local string &text = (Create SamplePkg:SampleClass()).MyProperty;
";

        var program = ParseCode(code);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        AssertSuccess(result);

        var creation = program.FindDescendants<ObjectCreationNode>().First();
        var creationType = creation.GetInferredType();
        var appClassType = Assert.IsType<AppClassTypeInfo>(creationType);
        Assert.Equal("SamplePkg:SampleClass", appClassType.QualifiedName);

        var propertyAccess = program.FindDescendants<MemberAccessNode>()
            .First(node => node.MemberName.Equals("MyProperty", StringComparison.OrdinalIgnoreCase));
        var propertyType = propertyAccess.GetInferredType();
        Assert.IsType<StringTypeInfo>(propertyType);
    }

    [Fact]
    public async Task TypeCast_ShouldProduceTargetType()
    {
        PrimeClassCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:BaseClass", BaseClassSource),
            new KeyValuePair<string, string>("SamplePkg:SampleClass", ClassSource)
        });

        const string code = @"
   Local SamplePkg:BaseClass &base = Create SamplePkg:SampleClass();
   Local SamplePkg:SampleClass &derived = &base as SamplePkg:SampleClass;
";

        var program = ParseCode(code);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        AssertSuccess(result);

        var castNode = program.FindDescendants<TypeCastNode>().Single();
        var castType = castNode.GetInferredType();
        var appClassType = Assert.IsType<AppClassTypeInfo>(castType);
        Assert.Equal("SamplePkg:SampleClass", appClassType.QualifiedName);
    }

    [Fact]
    public async Task InterfaceMemberAccess_ShouldResolveThroughContract()
    {
        PrimeInterfaceCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SamplePkg:SampleInterface", InterfaceSource),
            new KeyValuePair<string, string>("SamplePkg:SampleImplementation", InterfaceImplementationSource)
        });

        var program = ParseCode(InterfaceUsageCode);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        AssertSuccess(result);

        var contractIdentifier = program.FindDescendants<IdentifierNode>()
            .First(node => node.Name.Equals("&contract", StringComparison.OrdinalIgnoreCase));
        var contractType = Assert.IsType<AppClassTypeInfo>(contractIdentifier.GetInferredType());
        Assert.Equal("SamplePkg:SampleInterface", contractType.QualifiedName);

        var propertyAccess = program.FindDescendants<MemberAccessNode>()
            .First(node => node.MemberName.Equals("InterfaceProperty", StringComparison.OrdinalIgnoreCase));
        var propertyType = propertyAccess.GetInferredType();
        Assert.IsType<StringTypeInfo>(propertyType);

        var methodCall = program.FindDescendants<FunctionCallNode>()
            .First(call => call.Function is MemberAccessNode member && member.MemberName.Equals("DoWork", StringComparison.OrdinalIgnoreCase));
        var methodReturn = methodCall.GetInferredType();
        Assert.IsType<NumberTypeInfo>(methodReturn);
    }

    [Fact]
    public async Task ShorthandPropertyAccess_ShouldResolveUsingCurrentClass()
    {
        PrimeShorthandCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("ShorthandClass", ShorthandClassSource)
        });

        var program = ParseClassProgram(ShorthandClassSource);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        AssertSuccess(result);

        var propertyAssignment = program.FindDescendants<AssignmentNode>().FirstOrDefault();
        Assert.NotNull(propertyAssignment);
        var propertyIdentifier = Assert.IsType<IdentifierNode>(propertyAssignment!.Target);
        Assert.Equal("&MyProperty", propertyIdentifier.Name, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(PrimitiveTypeInfo.String, propertyIdentifier.GetInferredType());

        var propertyIdentifiers = program.FindDescendants<IdentifierNode>()
            .Where(node => node.Name.Equals("&MyProperty", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(propertyIdentifiers.Count >= 2);
        foreach (var identifier in propertyIdentifiers)
        {
            Assert.Equal(PrimitiveTypeInfo.String, identifier.GetInferredType());
        }
    }

    [Fact]
    public async Task ChainedMemberAccess_ShouldPropagateReturnTypes()
    {
        PrimeChainedCache();

        var provider = new TestProgramSourceProvider(new[]
        {
            new KeyValuePair<string, string>("SampleChild", ChildClassSource),
            new KeyValuePair<string, string>("SampleParent", ParentClassSource)
        });

        var program = ParseCode(ChainedAccessCode);
        var result = await InferTypesAsync(program, TypeInferenceMode.Quick, provider);

        AssertSuccess(result);

        var outerCall = program.FindDescendants<FunctionCallNode>()
            .First(call => call.Function is MemberAccessNode member && member.MemberName.Equals("GetLabel", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(PrimitiveTypeInfo.String, outerCall.GetInferredType());

        var innerCall = program.FindDescendants<FunctionCallNode>()
            .First(call => call.Function is MemberAccessNode member && member.MemberName.Equals("GetChild", StringComparison.OrdinalIgnoreCase));
        var innerType = innerCall.GetInferredType();
        var appClassType = Assert.IsType<AppClassTypeInfo>(innerType);
        Assert.Equal("SampleChild", appClassType.QualifiedName);
    }
}
