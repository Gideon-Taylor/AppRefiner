using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Header and Function-line <c>/* as T */</c> annotations go into type metadata as refined
/// slots (spec §7), so callers in this program and other programs see the annotated type.
/// </summary>
public class AnnotationMetadataTests
{
    private const string ClassSource =
        "class Svc\n" +
        "   method GetSess() Returns ApiObject /* as Session */;\n" +
        "   method GetCI() Returns ApiObject /* as CompIntfc.FOO */;\n" +
        "   method Take(&s As ApiObject /* as Session */, &r As Record /* as Record.JOB */);\n" +
        "   method Plain(&a As ApiObject) Returns ApiObject;\n" +
        "   method Misfit(&r As Rowset /* as Session */) Returns Rowset /* as Session */;\n" +
        "   property ApiObject /* as Session */ Sess;\n" +
        "   property ApiObject PlainProp;\n" +
        "   property Record /* as Session */ Job;\n" +
        "private\n" +
        "   instance Field /* as Record.JOB.EMPLID */ &f;\n" +
        "   instance any /* as Field.EMPLID */ &g;\n" +
        "end-class;\n";

    private static TypeMetadata Extract(string source, string? qualifiedName = null)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        return TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
    }

    private static SingleParameter Param(FunctionInfo function, int index) =>
        Assert.IsType<SingleParameter>(function.Parameters[index]);

    [Fact]
    public void Method_return_annotations_fill_refined_return_type()
    {
        var metadata = Extract(ClassSource);

        var sess = metadata.Methods["GetSess"].RefinedReturnType;
        Assert.NotNull(sess);
        Assert.Equal(PeopleCodeType.Session, sess!.Value.Type);
        Assert.Null(sess!.Value.Identity);

        var ci = metadata.Methods["GetCI"].RefinedReturnType;
        Assert.NotNull(ci);
        Assert.Equal(PeopleCodeType.ComponentInterface, ci!.Value.Type);
        Assert.Equal(new TypeIdentity(CompIntfcName: "FOO"), ci!.Value.Identity);
    }

    [Fact]
    public void Ci_item_and_collection_annotations_carry_the_path()
    {
        var metadata = Extract(
            "class Svc\n" +
            "   method GetRole(&roles As ApiObject /* as CompIntfc.USER_PROFILE.Roles[] */) Returns ApiObject /* as CompIntfc.USER_PROFILE.Roles.RouteControls */;\n" +
            "end-class;\n");

        var getRole = metadata.Methods["GetRole"];
        var item = getRole.RefinedReturnType;
        Assert.NotNull(item);
        Assert.Equal(PeopleCodeType.DataItem, item!.Value.Type);
        Assert.Equal(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: "Roles.RouteControls"), item!.Value.Identity);

        var collection = Param(getRole, 0).RefinedParameterType;
        Assert.NotNull(collection);
        Assert.Equal(PeopleCodeType.DataCollection, collection!.Value.Type);
        Assert.Equal(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: "Roles"), collection!.Value.Identity);
    }

    [Fact]
    public void Method_parameter_annotations_fill_refined_parameter_type()
    {
        var take = Extract(ClassSource).Methods["Take"];

        var s = Param(take, 0).RefinedParameterType;
        Assert.NotNull(s);
        Assert.Equal(PeopleCodeType.Session, s!.Value.Type);

        var r = Param(take, 1).RefinedParameterType;
        Assert.NotNull(r);
        Assert.Equal(PeopleCodeType.Record, r!.Value.Type);
        Assert.Equal(new TypeIdentity(RecordName: "JOB"), r!.Value.Identity);
    }

    [Fact]
    public void Property_and_instance_annotations_fill_refined_type()
    {
        var metadata = Extract(ClassSource);

        var sess = metadata.Properties["Sess"].RefinedType;
        Assert.NotNull(sess);
        Assert.Equal(PeopleCodeType.Session, sess!.Value.Type);
        Assert.Null(sess!.Value.Identity);

        var f = metadata.InstanceVariables["&f"].RefinedType;
        Assert.NotNull(f);
        Assert.Equal(PeopleCodeType.Field, f!.Value.Type);
        Assert.Equal(new TypeIdentity(RecordName: "JOB", FieldName: "EMPLID"), f!.Value.Identity);

        var g = metadata.InstanceVariables["&g"].RefinedType;
        Assert.NotNull(g);
        Assert.Equal(PeopleCodeType.Field, g!.Value.Type);
        Assert.Equal(new TypeIdentity(FieldName: "EMPLID"), g!.Value.Identity);
    }

    [Fact]
    public void Unannotated_and_misfit_declarations_leave_slots_null()
    {
        var metadata = Extract(ClassSource);

        var plain = metadata.Methods["Plain"];
        Assert.Null(plain.RefinedReturnType);
        Assert.Null(Param(plain, 0).RefinedParameterType);

        var misfit = metadata.Methods["Misfit"];
        Assert.Null(misfit.RefinedReturnType);
        Assert.Null(Param(misfit, 0).RefinedParameterType);

        Assert.Null(metadata.Properties["PlainProp"].RefinedType);
        Assert.Null(metadata.Properties["Job"].RefinedType);
    }

    [Fact]
    public void Function_line_annotations_fill_refined_slots()
    {
        var metadata = Extract(
            "Function Foo(&s As ApiObject /* as Session */) Returns ApiObject /* as Session */\n" +
            "End-Function;\n" +
            "Function Bar() Returns ApiObject\n" +
            "End-Function;\n" +
            "Function Baz(&r As Rowset /* as Session */) Returns Rowset /* as Session */\n" +
            "End-Function;\n" +
            "Function Qux() Returns ApiObject /* as Foo */\n" +
            "End-Function;\n");

        var foo = metadata.Methods["Foo"];
        Assert.Equal(PeopleCodeType.Session, foo.RefinedReturnType?.Type);
        Assert.Equal(PeopleCodeType.Session, Param(foo, 0).RefinedParameterType?.Type);

        Assert.Null(metadata.Methods["Bar"].RefinedReturnType);

        // Misfit and unknown annotations are reported by inference, never stored
        var baz = metadata.Methods["Baz"];
        Assert.Null(baz.RefinedReturnType);
        Assert.Null(Param(baz, 0).RefinedParameterType);
        Assert.Null(metadata.Methods["Qux"].RefinedReturnType);
    }

    [Fact]
    public void Same_program_function_call_uses_annotated_return()
    {
        var (program, errors) = Parse(
            "Function Foo() Returns ApiObject /* as Session */\nEnd-Function;\n" +
            "Local ApiObject &x = Foo();\n&x.GetCompIntfc(CompIntfc.A);");
        Assert.Empty(errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        var lastUse = program.FindDescendants<IdentifierNode>()
            .Where(n => n.Name.Equals("&x", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.SourceSpan.Start.Line)
            .ThenBy(n => n.SourceSpan.Start.Column)
            .Last();
        Assert.Equal("Session", lastUse.GetInferredType()?.Name, ignoreCase: true);
    }

    [Fact]
    public void Cross_program_method_call_uses_annotated_return()
    {
        var svc = Extract(
            "class Svc\n" +
            "   method GetSess() Returns ApiObject /* as Session */;\n" +
            "end-class;\n",
            "PKG:Svc");
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Svc", svc);

        var (program, errors) = Parse(
            "Local PKG:Svc &s = create PKG:Svc();\nLocal ApiObject &x = &s.GetSess();");
        Assert.Empty(errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, resolver);

        var decl = program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>().Last();
        Assert.Equal("Session", decl.InitialValue.GetInferredType()?.Name, ignoreCase: true);
    }

    [Fact]
    public void Cross_program_record_return_carries_record_name()
    {
        var svc = Extract(
            "class Svc\n" +
            "   method GetJob() Returns Record /* as Record.JOB */;\n" +
            "end-class;\n",
            "PKG:Svc");
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Svc", svc);

        var (program, errors) = Parse(
            "Local PKG:Svc &s = create PKG:Svc();\nLocal Record &r = &s.GetJob();");
        Assert.Empty(errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, resolver);

        var decl = program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>().Last();
        var record = Assert.IsType<RecordTypeInfo>(decl.InitialValue.GetInferredType());
        Assert.Equal("JOB", record.RecordName);
    }
}
