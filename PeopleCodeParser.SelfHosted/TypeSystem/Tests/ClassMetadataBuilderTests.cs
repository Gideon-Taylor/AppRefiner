using System;
using System.Linq;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.TypeSystem;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

public class ClassMetadataBuilderTests
{
    private static ProgramNode ParseProgram(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();

        PeopleCodeParser.ToolsRelease = new ToolsVersion("99.99.99");
        var parser = new PeopleCodeParser(tokens);
        var program = parser.ParseProgram() ?? throw new InvalidOperationException("Failed to parse program");

        if (parser.Errors.Any())
        {
            var message = string.Join("\n", parser.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Parser errors: {message}");
        }

        return program;
    }

    [Fact]
    public void Build_ShouldExtractClassMetadata()
    {
        const string source = @"
import SamplePkg:Utilities;

class SampleClass extends BaseClass
   method DoWork(&param As string);
   property string MyProperty get set;
   method ProtectedMethod(&value As number);
   property boolean InternalFlag get;
end-class;
";

        var program = ParseProgram(source);
        var classInfo = ClassMetadataBuilder.Build(program);

        Assert.NotNull(classInfo);
        Assert.Equal("SampleClass", classInfo!.QualifiedName);
        Assert.Equal("BaseClass", classInfo.BaseClassName);

        Assert.Contains("MyProperty", classInfo.Properties.Keys);
        var property = classInfo.Properties["MyProperty"];
        Assert.Equal(MemberAccessibility.Public, property.Accessibility);
        Assert.Equal(PrimitiveTypeInfo.String, property.Type);

        Assert.Contains("InternalFlag", classInfo.Properties.Keys);
        Assert.Equal(MemberAccessibility.Public, classInfo.Properties["InternalFlag"].Accessibility);

        Assert.Contains("DoWork", classInfo.Methods.Keys);
        var method = classInfo.Methods["DoWork"];
        Assert.Equal(MemberAccessibility.Public, method.Accessibility);
        Assert.Equal("DoWork", method.Name);
        Assert.Single(method.ParameterTypes);
        Assert.Equal(PrimitiveTypeInfo.String, method.ParameterTypes[0]);
        Assert.Equal(VoidTypeInfo.Instance, method.ReturnType);

        Assert.Contains("ProtectedMethod", classInfo.Methods.Keys);
        Assert.Equal(MemberAccessibility.Public, classInfo.Methods["ProtectedMethod"].Accessibility);
    }

    [Fact]
    public void Build_ShouldExtractInterfaceMetadata()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();

        const string source = @"
interface SampleInterface
   method DoSomething(&value As string) returns number;
   property string Data get;
end-interface;
";

        var program = ParseProgram(source);
        var interfaceInfo = ClassMetadataBuilder.Build(program, "SamplePkg:SampleInterface");

        Assert.NotNull(interfaceInfo);
        Assert.True(interfaceInfo!.IsInterface);
        Assert.Equal("SamplePkg:SampleInterface", interfaceInfo.QualifiedName);
        Assert.True(interfaceInfo.Properties.ContainsKey("Data"));
        Assert.True(interfaceInfo.Methods.ContainsKey("DoSomething"));

        PeopleCodeTypeRegistry.ClearClassInfoCache();
    }

    [Fact]
    public void Build_ShouldIncludeBaseInterfaceMembers()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();

        const string baseInterfaceSource = @"
interface SampleInterfaceBase
   property number Count get;
end-interface;
";

        const string derivedInterfaceSource = @"
import SamplePkg:SampleInterfaceBase;
interface SampleInterface extends SamplePkg:SampleInterfaceBase
   method Execute();
end-interface;
";

        var baseProgram = ParseProgram(baseInterfaceSource);
        var baseInfo = ClassMetadataBuilder.Build(baseProgram, "SamplePkg:SampleInterfaceBase");
        Assert.NotNull(baseInfo);
        PeopleCodeTypeRegistry.CacheClassInfo(baseInfo!);

        var derivedProgram = ParseProgram(derivedInterfaceSource);
        var derivedInfo = ClassMetadataBuilder.Build(derivedProgram, "SamplePkg:SampleInterface");

        Assert.NotNull(derivedInfo);
        Assert.True(derivedInfo!.IsInterface);
        Assert.Contains("Count", derivedInfo.Properties.Keys);
        Assert.Contains("Execute", derivedInfo.Methods.Keys);

        PeopleCodeTypeRegistry.ClearClassInfoCache();
    }

    [Fact]
    public void Build_ShouldCaptureConstructorSignature()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();

        const string source = @"
class SampleClass
   method SampleClass(&value As string);
end-class;
";

        var program = ParseProgram(source);
        var classInfo = ClassMetadataBuilder.Build(program, "SamplePkg:SampleClass");

        Assert.NotNull(classInfo);
        Assert.NotNull(classInfo!.Constructor);
        Assert.Equal(1, classInfo.Constructor!.ParameterTypes.Count);
        Assert.Single(classInfo.Constructor.ParameterTypes);
        Assert.Equal(PrimitiveTypeInfo.String, classInfo.Constructor.ParameterTypes[0]);

        PeopleCodeTypeRegistry.ClearClassInfoCache();
    }
}
