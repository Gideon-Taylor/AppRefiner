using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Function and property metadata can carry a refined type (e.g. Session behind a declared
/// ApiObject) and a definition identity; inference prefers the refined slot when present.
/// </summary>
public class RefinedMetadataTests
{
    private static FakeTypeMetadataResolver Resolver()
    {
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Svc", new TypeMetadata
        {
            QualifiedName = "PKG:Svc",
            Name = "Svc",
            Kind = ProgramKind.AppClass,
            Methods = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["GetSess"] = new FunctionInfo
                {
                    Name = "GetSess",
                    ReturnType = new(PeopleCodeType.ApiObject),
                    RefinedReturnType = new(PeopleCodeType.Session),
                },
                ["GetJob"] = new FunctionInfo
                {
                    Name = "GetJob",
                    ReturnType = new(PeopleCodeType.Record),
                    RefinedReturnType = new(PeopleCodeType.Record) { Identity = new(RecordName: "JOB") },
                },
                ["GetCI"] = new FunctionInfo
                {
                    Name = "GetCI",
                    ReturnType = new(PeopleCodeType.ApiObject),
                    RefinedReturnType = new(PeopleCodeType.ComponentInterface) { Identity = new(CompIntfcName: "FOO") },
                },
                ["GetPlain"] = new FunctionInfo
                {
                    Name = "GetPlain",
                    ReturnType = new(PeopleCodeType.ApiObject),
                },
            },
            Properties = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["Sess"] = new PropertyInfo(PeopleCodeType.ApiObject)
                {
                    Name = "Sess",
                    RefinedType = new(PeopleCodeType.Session),
                },
            },
        });
        return fake;
    }

    private static TypeInfo? InferInitializer(string line)
    {
        var (program, errors) = Parse("Local PKG:Svc &svc = create PKG:Svc();\n" + line);
        Assert.Empty(errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, Resolver());

        var decl = program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>().Last();
        return decl.InitialValue.GetInferredType();
    }

    [Fact]
    public void Refined_return_type_is_used_for_calls()
    {
        var type = InferInitializer("Local ApiObject &x = &svc.GetSess();");

        Assert.Equal("Session", type!.Name, ignoreCase: true);
    }

    [Fact]
    public void Refined_record_return_carries_record_name()
    {
        var type = InferInitializer("Local Record &r = &svc.GetJob();");

        var record = Assert.IsType<RecordTypeInfo>(type);
        Assert.Equal("JOB", record.RecordName);
    }

    [Fact]
    public void Refined_ci_return_carries_identity()
    {
        var type = InferInitializer("Local ApiObject &c = &svc.GetCI();");

        Assert.Equal("ComponentInterface", type!.Name, ignoreCase: true);
        Assert.Equal(new TypeIdentity(CompIntfcName: "FOO"), type.Identity);
    }

    [Fact]
    public void Refined_property_type_is_used()
    {
        var type = InferInitializer("Local ApiObject &p = &svc.Sess;");

        Assert.Equal("Session", type!.Name, ignoreCase: true);
    }

    [Fact]
    public void Unrefined_return_is_unchanged()
    {
        var type = InferInitializer("Local ApiObject &x = &svc.GetPlain();");

        Assert.Equal("ApiObject", type!.Name, ignoreCase: true);
    }

    [Fact]
    public void Identity_is_not_part_of_TypeWithDimensionality_equality()
    {
        var plain = new TypeWithDimensionality(PeopleCodeType.Record);
        var named = new TypeWithDimensionality(PeopleCodeType.Record) { Identity = new(RecordName: "JOB") };

        Assert.Equal(plain, named);
        Assert.Equal(plain.GetHashCode(), named.GetHashCode());
    }
}
