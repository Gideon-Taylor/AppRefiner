using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Same-program return refinement (spec §9 "Same-program calls"): a routine declared to return
/// ApiObject, Record or Field whose returns agree gets a refined return type, and calls visited
/// after its body pick it up. Methods and getters are visited callee-first.
/// </summary>
public class ReturnRefinementTests
{
    private static (ProgramNode Program, List<TypeError> Errors, List<TypeError> Warnings) TypeCheck(string source, string? qualifiedName = null)
    {
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);

        var metadata = qualifiedName == null
            ? TypeMetadataBuilder.ExtractMetadata(program)
            : TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);

        return (program, program.GetAllTypeErrors().ToList(), program.GetAllTypeWarnings().ToList());
    }

    private static IdentifierNode LastUse(ProgramNode program, string name)
    {
        return program.FindDescendants<IdentifierNode>()
            .Where(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.SourceSpan.Start.Line)
            .ThenBy(n => n.SourceSpan.Start.Column)
            .Last();
    }

    private static TypeInfo? LastInitializer(ProgramNode program) =>
        program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>()
            .OrderBy(d => d.SourceSpan.Start.Index)
            .Last()
            .InitialValue.GetInferredType();

    private static bool IsR1(TypeError warning) => warning.Message.Contains("returns more than one API type");

    [Fact]
    public void Function_called_after_definition_gets_refined_return()
    {
        var (program, _, _) = TypeCheck(
            "Function GetS() Returns ApiObject\n   Return %Session;\nEnd-Function;\nLocal ApiObject &s = GetS();\n&s.GetCompIntfc(CompIntfc.A);");

        var use = LastUse(program, "&s");
        Assert.Equal("Session", use.GetInferredType()!.Name, ignoreCase: true);
        Assert.Equal(RefinementKind.Assignment, use.GetRefinementSource()!.Kind);
    }

    [Fact]
    public void Record_function_gets_named_return()
    {
        var (program, _, _) = TypeCheck(
            "Function GetJ() Returns Record\n   Return CreateRecord(Record.JOB);\nEnd-Function;\nLocal Record &r = GetJ();");

        var record = Assert.IsType<RecordTypeInfo>(LastInitializer(program));
        Assert.Equal("JOB", record.RecordName);
    }

    [Fact]
    public void Field_function_gets_named_return()
    {
        var (program, _, _) = TypeCheck(
            "Function GetF() Returns Field\n   Return GetRecord(Record.JOB).EMPLID;\nEnd-Function;\nLocal Field &f = GetF();");

        var field = Assert.IsType<FieldTypeInfo>(LastInitializer(program));
        Assert.Equal("JOB", field.RecordName);
        Assert.Equal("EMPLID", field.FieldName);
    }

    [Fact]
    public void Disagreeing_api_returns_warn_and_stay_apiobject()
    {
        var (program, _, warnings) = TypeCheck(
            "Function F(&b As boolean) Returns ApiObject\n" +
            "   If &b Then\n" +
            "      Return %Session;\n" +
            "   Else\n" +
            "      Return %Session.GetQuery();\n" +
            "   End-If;\n" +
            "End-Function;\n" +
            "Local ApiObject &x = F(True);");

        var r1 = Assert.Single(warnings, IsR1);
        Assert.Equal("F returns more than one API type (Session on line 3, Query on line 5).", r1.Message);
        Assert.Equal(PeopleCodeType.ApiObject, LastInitializer(program)!.PeopleCodeType);
    }

    [Theory]
    [InlineData("Null")]
    [InlineData("(Null)")]
    [InlineData("((Null))")]
    public void Null_return_does_not_disagree(string nullValue)
    {
        var (program, _, warnings) = TypeCheck(
            "Function F(&b As boolean) Returns ApiObject\n" +
            "   If &b Then\n" +
            "      Return %Session;\n" +
            "   End-If;\n" +
            $"   Return {nullValue};\n" +
            "End-Function;\n" +
            "Local ApiObject &x = F(True);");

        Assert.DoesNotContain(warnings, IsR1);
        Assert.Equal("Session", LastInitializer(program)!.Name, ignoreCase: true);
    }

    [Fact]
    public void Disagreeing_record_returns_stay_plain_silently()
    {
        var (program, _, warnings) = TypeCheck(
            "Function GetJ(&b As boolean) Returns Record\n" +
            "   If &b Then\n" +
            "      Return CreateRecord(Record.JOB);\n" +
            "   Else\n" +
            "      Return CreateRecord(Record.PERSONAL_DATA);\n" +
            "   End-If;\n" +
            "End-Function;\n" +
            "Local Record &r = GetJ(True);");

        var type = LastInitializer(program);
        Assert.NotNull(type);
        Assert.IsNotType<RecordTypeInfo>(type);
        Assert.Equal(PeopleCodeType.Record, type.PeopleCodeType);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Annotated_return_is_not_overridden()
    {
        var (program, _, warnings) = TypeCheck(
            "Function F() Returns ApiObject /* as Query */\n   Return %Session;\nEnd-Function;\nLocal ApiObject &q = F();");

        Assert.Equal("Query", LastInitializer(program)!.Name, ignoreCase: true);
        Assert.DoesNotContain(warnings, IsR1);
    }

    [Fact]
    public void Method_called_from_later_body_gets_refined_return()
    {
        var (program, _, _) = TypeCheck(
            "class Svc\n" +
            "   method GetS() Returns ApiObject;\n" +
            "   method Use();\n" +
            "end-class;\n" +
            "\n" +
            "method GetS\n" +
            "   Return %Session;\n" +
            "end-method;\n" +
            "\n" +
            "method Use\n" +
            "   Local ApiObject &s = %This.GetS();\n" +
            "end-method;\n",
            "PKG:Svc");

        Assert.Equal("Session", LastInitializer(program)!.Name, ignoreCase: true);
    }

    [Fact]
    public void Caller_declared_before_helper_gets_refined_return()
    {
        var (program, _, _) = TypeCheck(
            "class Svc\n" +
            "   method Use();\n" +
            "   method GetS() Returns ApiObject;\n" +
            "end-class;\n" +
            "\n" +
            "method Use\n" +
            "   Local ApiObject &s = %This.GetS();\n" +
            "end-method;\n" +
            "\n" +
            "method GetS\n" +
            "   Return %Session;\n" +
            "end-method;\n",
            "PKG:Svc");

        Assert.Equal("Session", LastInitializer(program)!.Name, ignoreCase: true);
    }

    [Fact]
    public void Methods_returning_only_each_other_stay_unrefined()
    {
        const string source =
            "class Svc\n" +
            "   method A() Returns ApiObject;\n" +
            "   method B() Returns ApiObject;\n" +
            "end-class;\n" +
            "\n" +
            "method A\n" +
            "   Return %This.B();\n" +
            "end-method;\n" +
            "\n" +
            "method B\n" +
            "   Return %This.A();\n" +
            "end-method;\n";
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "PKG:Svc");

        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        Assert.Null(metadata.Methods["A"].RefinedReturnType);
        Assert.Null(metadata.Methods["B"].RefinedReturnType);
        Assert.Empty(program.GetAllTypeWarnings());
        Assert.All(program.FindDescendants<FunctionCallNode>(),
            call => Assert.Equal(PeopleCodeType.ApiObject, call.GetInferredType()!.PeopleCodeType));
    }

    private static TypeMetadata Refine(string source)
    {
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "PKG:Svc");
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        return metadata;
    }

    [Fact]
    public void Mutually_recursive_methods_share_the_type_one_returns()
    {
        var metadata = Refine(
            "class Svc\n" +
            "   method A(&b As boolean) Returns ApiObject;\n" +
            "   method B(&b As boolean) Returns ApiObject;\n" +
            "end-class;\n" +
            "\n" +
            "method A\n" +
            "   If &b Then\n" +
            "      Return %Session;\n" +
            "   End-If;\n" +
            "   Return %This.B( Not &b);\n" +
            "end-method;\n" +
            "\n" +
            "method B\n" +
            "   Return %This.A(&b);\n" +
            "end-method;\n");

        Assert.Equal(PeopleCodeType.Session, metadata.Methods["A"].RefinedReturnType!.Value.Type);
        Assert.Equal(PeopleCodeType.Session, metadata.Methods["B"].RefinedReturnType!.Value.Type);
    }

    [Fact]
    public void Self_recursive_method_takes_its_other_returns_type()
    {
        var metadata = Refine(
            "class Svc\n" +
            "   method A(&n As number) Returns ApiObject;\n" +
            "end-class;\n" +
            "\n" +
            "method A\n" +
            "   If &n = 0 Then\n" +
            "      Return %Session;\n" +
            "   End-If;\n" +
            "   Return (%This.A(&n - 1));\n" +
            "end-method;\n");

        Assert.Equal(PeopleCodeType.Session, metadata.Methods["A"].RefinedReturnType!.Value.Type);
    }

    [Fact]
    public void Partner_whose_returns_disagree_blocks_the_cycle()
    {
        // B returns a Query itself, so A can return a Query or a Session: neither is refined
        var metadata = Refine(
            "class Svc\n" +
            "   method A(&b As boolean) Returns ApiObject;\n" +
            "   method B(&b As boolean) Returns ApiObject;\n" +
            "end-class;\n" +
            "\n" +
            "method A\n" +
            "   If &b Then\n" +
            "      Return %Session;\n" +
            "   End-If;\n" +
            "   Return %This.B( Not &b);\n" +
            "end-method;\n" +
            "\n" +
            "method B\n" +
            "   If &b Then\n" +
            "      Return %Session.GetQuery();\n" +
            "   End-If;\n" +
            "   Return %This.A(&b);\n" +
            "end-method;\n");

        Assert.Null(metadata.Methods["A"].RefinedReturnType);
        Assert.Null(metadata.Methods["B"].RefinedReturnType);
    }

    [Fact]
    public void Getter_in_a_cycle_with_a_method_is_refined()
    {
        var metadata = Refine(
            "class Svc\n" +
            "   method Make(&b As boolean) Returns ApiObject;\n" +
            "   property ApiObject Current get;\n" +
            "end-class;\n" +
            "\n" +
            "method Make\n" +
            "   If &b Then\n" +
            "      Return %Session;\n" +
            "   End-If;\n" +
            "   Return %This.Current;\n" +
            "end-method;\n" +
            "\n" +
            "get Current\n" +
            "   Return %This.Make(True);\n" +
            "end-get;\n");

        Assert.Equal(PeopleCodeType.Session, metadata.Methods["Make"].RefinedReturnType!.Value.Type);
        Assert.Equal(PeopleCodeType.Session, metadata.Properties["Current"].RefinedType!.Value.Type);
    }

    [Fact]
    public void Caller_outside_the_cycle_sees_its_refined_return()
    {
        var (program, _, _) = TypeCheck(
            "class Svc\n" +
            "   method A(&b As boolean) Returns ApiObject;\n" +
            "   method B(&b As boolean) Returns ApiObject;\n" +
            "   method Use();\n" +
            "end-class;\n" +
            "\n" +
            "method Use\n" +
            "   Local ApiObject &s = %This.B(True);\n" +
            "end-method;\n" +
            "\n" +
            "method A\n" +
            "   If &b Then\n" +
            "      Return %Session;\n" +
            "   End-If;\n" +
            "   Return %This.B( Not &b);\n" +
            "end-method;\n" +
            "\n" +
            "method B\n" +
            "   Return %This.A(&b);\n" +
            "end-method;\n", "PKG:Svc");

        Assert.Equal(PeopleCodeType.Session, LastInitializer(program)!.PeopleCodeType);
    }

    [Fact]
    public void Callees_first_with_header_order_for_ties()
    {
        var (program, parseErrors) = Parse(
            "class Svc\n" +
            "   method M1();\n" +
            "   method M2();\n" +
            "   method M3();\n" +
            "   property ApiObject G get set;\n" +
            "end-class;\n" +
            "\n" +
            "method M1\n" +
            "   %This.M3();\n" +
            "end-method;\n" +
            "\n" +
            "method M2\n" +
            "   Local ApiObject &g = %This.G;\n" +
            "end-method;\n" +
            "\n" +
            "method M3\n" +
            "   %This.M2();\n" +
            "end-method;\n" +
            "\n" +
            "get G\n" +
            "   Return %Session;\n" +
            "end-get;\n" +
            "\n" +
            "set G\n" +
            "end-set;\n");
        Assert.Empty(parseErrors);
        var appClass = program.AppClass!;

        var order = RoutineVisitOrder.CalleesFirst(appClass).Select(Describe).ToList();

        Assert.Equal(new[] { "get G", "M2", "M3", "M1", "set G" }, order);
    }

    private static string Describe(AstNode node) => node switch
    {
        MethodNode method => method.Name,
        PropertyImplNode { IsGetter: true } getter => $"get {getter.Name}",
        PropertyImplNode setter => $"set {setter.Name}",
        _ => node.GetType().Name
    };

    [Fact]
    public void Getter_property_gets_refined_type()
    {
        var (program, _, _) = TypeCheck(
            "class Svc\n" +
            "   method Use();\n" +
            "   property ApiObject Sess get;\n" +
            "end-class;\n" +
            "\n" +
            "method Use\n" +
            "   Local ApiObject &s = %This.Sess;\n" +
            "end-method;\n" +
            "\n" +
            "get Sess\n" +
            "   Return %Session;\n" +
            "end-get;\n",
            "PKG:Svc");

        Assert.Equal("Session", LastInitializer(program)!.Name, ignoreCase: true);
    }

    [Fact]
    public void Inherited_return_annotation_is_not_overridden()
    {
        var (baseProgram, baseErrors) = Parse(
            "class Base\n" +
            "   method GetS() Returns ApiObject /* as Query */;\n" +
            "end-class;\n" +
            "\n" +
            "method GetS\n" +
            "end-method;\n");
        Assert.Empty(baseErrors);
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Base", TypeMetadataBuilder.ExtractMetadata(baseProgram, "PKG:Base"));

        var (program, parseErrors) = Parse(
            "class Sub extends PKG:Base\n" +
            "   method GetS() Returns ApiObject;\n" +
            "   method Use();\n" +
            "end-class;\n" +
            "\n" +
            "method GetS\n" +
            "   Return %Session;\n" +
            "end-method;\n" +
            "\n" +
            "method Use\n" +
            "   Local ApiObject &q = %This.GetS();\n" +
            "end-method;\n");
        Assert.Empty(parseErrors);
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "PKG:Sub");

        TypeInferenceVisitor.Run(program, metadata, resolver);

        Assert.Null(metadata.Methods["GetS"].RefinedReturnType);
        Assert.Equal("Query", LastInitializer(program)!.Name, ignoreCase: true);
    }

    [Fact]
    public void Inferred_base_refinement_is_not_inherited()
    {
        Assert.Equal("Query", InferredBaseCase("   Return %Session.GetQuery();\n")!.Name, ignoreCase: true);
    }

    [Fact]
    public void Inferred_base_refinement_is_not_inherited_by_unrefined_override()
    {
        Assert.Equal(PeopleCodeType.ApiObject, InferredBaseCase("   Local ApiObject &x;\n   Return &x;\n")!.PeopleCodeType);
    }

    /// <summary>
    /// Base metadata whose GetS carries an inferred (unannotated) Session refinement, and an
    /// unannotated override with <paramref name="overrideBody"/>; the type a later caller gets.
    /// </summary>
    private static TypeInfo? InferredBaseCase(string overrideBody)
    {
        var (baseProgram, baseErrors) = Parse(
            "class Base\n" +
            "   method GetS() Returns ApiObject;\n" +
            "end-class;\n" +
            "\n" +
            "method GetS\n" +
            "end-method;\n");
        Assert.Empty(baseErrors);
        var baseMetadata = TypeMetadataBuilder.ExtractMetadata(baseProgram, "PKG:Base");
        // As if return refinement had inferred Session for the base: no annotation
        baseMetadata.Methods["GetS"].RefinedReturnType = new TypeWithDimensionality(PeopleCodeType.Session);
        Assert.False(baseMetadata.Methods["GetS"].HasReturnAnnotation);
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Base", baseMetadata);

        var (program, parseErrors) = Parse(
            "class Sub extends PKG:Base\n" +
            "   method GetS() Returns ApiObject;\n" +
            "   method Use();\n" +
            "end-class;\n" +
            "\n" +
            "method GetS\n" +
            overrideBody +
            "end-method;\n" +
            "\n" +
            "method Use\n" +
            "   Local ApiObject &q = %This.GetS();\n" +
            "end-method;\n");
        Assert.Empty(parseErrors);
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "PKG:Sub");

        TypeInferenceVisitor.Run(program, metadata, resolver);

        return LastInitializer(program);
    }

    [Fact]
    public void Pass_through_parameter_return_uses_parameter_type()
    {
        var (program, _, _) = TypeCheck(
            "Function P(&s As ApiObject /* as Session */) Returns ApiObject\n   Return &s;\nEnd-Function;\nLocal ApiObject &x = P(%Session);");

        Assert.Equal("Session", LastInitializer(program)!.Name, ignoreCase: true);
    }

    [Fact]
    public void Self_recursive_function_is_not_refined()
    {
        var (program, _, warnings) = TypeCheck(
            "Function F() Returns ApiObject\n   Return F();\nEnd-Function;\nLocal ApiObject &f = F();");

        Assert.Equal(PeopleCodeType.ApiObject, LastInitializer(program)!.PeopleCodeType);
        Assert.Empty(warnings);
    }
}
