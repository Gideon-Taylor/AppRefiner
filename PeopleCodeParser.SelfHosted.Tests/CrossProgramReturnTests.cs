using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Cross-program return types (spec §9): a program fetched by a resolver is refined by
/// <see cref="ReturnTypeRefiner"/>, so callers in other programs see the refined return,
/// property and instance-variable types. Cycles and the depth limit leave provisional metadata:
/// the top-level result is cached, the nested cut-short ones are evicted.
/// </summary>
public class CrossProgramReturnTests
{
    private const string SvcCaller =
        "Local PKG:Svc &svc = create PKG:Svc();\n";

    /// <summary>
    /// Runs inference on a caller program against the resolver and returns the last
    /// <c>Local</c> initializer's inferred type.
    /// </summary>
    private static TypeInfo? CallerInitializer(SourceTypeMetadataResolver resolver, string callerSource)
    {
        var (program, parseErrors) = Parse(callerSource);
        Assert.Empty(parseErrors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, resolver);

        return program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>()
            .OrderBy(d => d.SourceSpan.Start.Index)
            .Last()
            .InitialValue.GetInferredType();
    }

    private static string GetMethodClass(string className, string body) =>
        $"class {className}\n" +
        "   method Get() Returns ApiObject;\n" +
        "end-class;\n" +
        "\n" +
        "method Get\n" +
        body +
        "end-method;\n";

    [Fact]
    public void External_method_returning_one_subtype_refines_caller()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc", GetMethodClass("Svc", "   Return %Session;\n"));

        var type = CallerInitializer(resolver, SvcCaller + "Local ApiObject &s = &svc.Get();");

        Assert.Equal("Session", type!.Name, ignoreCase: true);
    }

    [Fact]
    public void Declared_function_library_return_refines_caller()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("REC.FLD.FieldFormula",
            "Function GetS() Returns ApiObject\n" +
            "   Return %Session;\n" +
            "End-Function;\n");

        var type = CallerInitializer(resolver,
            "Declare Function GetS PeopleCode REC.FLD FieldFormula;\n" +
            "Local ApiObject &s = GetS();");

        Assert.Equal("Session", type!.Name, ignoreCase: true);
    }

    [Fact]
    public void Disagreeing_external_returns_stay_apiobject()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc",
            "class Svc\n" +
            "   method Get(&b As boolean) Returns ApiObject;\n" +
            "end-class;\n" +
            "\n" +
            "method Get\n" +
            "   If &b Then\n" +
            "      Return %Session;\n" +
            "   Else\n" +
            "      Return %Session.GetQuery();\n" +
            "   End-If;\n" +
            "end-method;\n");

        var type = CallerInitializer(resolver, SvcCaller + "Local ApiObject &s = &svc.Get(True);");

        Assert.Equal(PeopleCodeType.ApiObject, type!.PeopleCodeType);
        Assert.Null(resolver.GetTypeMetadata("PKG:Svc")!.Methods["Get"].RefinedReturnType);
    }

    [Fact]
    public void External_record_return_carries_name()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc",
            "class Svc\n" +
            "   method Get() Returns Record;\n" +
            "end-class;\n" +
            "\n" +
            "method Get\n" +
            "   Return CreateRecord(Record.JOB);\n" +
            "end-method;\n");

        var type = CallerInitializer(resolver, SvcCaller + "Local Record &r = &svc.Get();");

        var record = Assert.IsType<RecordTypeInfo>(type);
        Assert.Equal("JOB", record.RecordName);
    }

    [Fact]
    public void External_property_assigned_in_constructor_is_refined()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc",
            "class Svc\n" +
            "   method Svc();\n" +
            "   property ApiObject Sess;\n" +
            "end-class;\n" +
            "\n" +
            "method Svc\n" +
            "   &Sess = %Session;\n" +
            "end-method;\n");

        var type = CallerInitializer(resolver, SvcCaller + "Local ApiObject &s = &svc.Sess;");

        Assert.Equal("Session", type!.Name, ignoreCase: true);
    }

    [Fact]
    public void This_property_and_instance_variable_assignments_refine()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc",
            "class Svc\n" +
            "   method Svc();\n" +
            "   method Reset(&b As boolean);\n" +
            "   property ApiObject Sess;\n" +
            "   property ApiObject Mixed;\n" +
            "private\n" +
            "   instance ApiObject &q;\n" +
            "end-class;\n" +
            "\n" +
            "method Svc\n" +
            "   %This.Sess = %Session;\n" +
            "   &q = %Session.GetQuery();\n" +
            "   &Mixed = %Session;\n" +
            "end-method;\n" +
            "\n" +
            "method Reset\n" +
            "   Local ApiObject &q;\n" +
            "   &q = %Session;\n" +
            "   &Mixed = %Session.GetQuery();\n" +
            "end-method;\n");

        var metadata = resolver.GetTypeMetadata("PKG:Svc")!;

        Assert.Equal(0, resolver.ParseErrorCount("PKG:Svc"));
        Assert.Equal(PeopleCodeType.Session, metadata.Properties["Sess"].RefinedType!.Value.Type);
        // The local &q in Reset shadows the instance variable, so only the constructor counts
        Assert.Equal(PeopleCodeType.Query, metadata.InstanceVariables["&q"].RefinedType!.Value.Type);
        Assert.Null(metadata.Properties["Mixed"].RefinedType);
        Assert.False(metadata.IsProvisional);
    }

    [Fact]
    public void Getter_only_property_refines_caller()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc",
            "class Svc\n" +
            "   property ApiObject Sess get;\n" +
            "end-class;\n" +
            "\n" +
            "get Sess\n" +
            "   Return %Session;\n" +
            "end-get;\n");

        var type = CallerInitializer(resolver, SvcCaller + "Local ApiObject &s = &svc.Sess;");

        Assert.Equal(0, resolver.ParseErrorCount("PKG:Svc"));
        Assert.Equal("Session", type!.Name, ignoreCase: true);
    }

    [Fact]
    public void Instance_variable_cleared_with_null_still_refines()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc",
            "class Svc\n" +
            "   method Svc();\n" +
            "   method Close();\n" +
            "private\n" +
            "   instance ApiObject &sess;\n" +
            "end-class;\n" +
            "\n" +
            "method Svc\n" +
            "   &sess = %Session;\n" +
            "end-method;\n" +
            "\n" +
            "method Close\n" +
            "   &sess = Null;\n" +
            "end-method;\n");

        var metadata = resolver.GetTypeMetadata("PKG:Svc")!;

        Assert.Equal(0, resolver.ParseErrorCount("PKG:Svc"));
        Assert.Equal(PeopleCodeType.Session, metadata.InstanceVariables["&sess"].RefinedType!.Value.Type);
    }

    [Fact]
    public void Only_null_assignments_do_not_refine()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc",
            "class Svc\n" +
            "   method Close();\n" +
            "private\n" +
            "   instance ApiObject &sess;\n" +
            "end-class;\n" +
            "\n" +
            "method Close\n" +
            "   &sess = Null;\n" +
            "end-method;\n");

        var metadata = resolver.GetTypeMetadata("PKG:Svc")!;

        Assert.Equal(0, resolver.ParseErrorCount("PKG:Svc"));
        Assert.Null(metadata.InstanceVariables["&sess"].RefinedType);
    }

    [Fact]
    public void Function_returning_null_on_one_path_still_refines()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("REC.FLD.FieldFormula",
            "Function GetS(&b As boolean) Returns ApiObject\n" +
            "   If &b Then\n" +
            "      Return %Session;\n" +
            "   End-If;\n" +
            "   Return Null;\n" +
            "End-Function;\n");

        var type = CallerInitializer(resolver,
            "Declare Function GetS PeopleCode REC.FLD FieldFormula;\n" +
            "Local ApiObject &s = GetS(True);");

        Assert.Equal(0, resolver.ParseErrorCount("REC.FLD.FieldFormula"));
        Assert.Equal("Session", type!.Name, ignoreCase: true);
    }

    [Fact]
    public void Annotation_overrides_inference()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc",
            "class Svc\n" +
            "   method Get() Returns ApiObject /* as Query */;\n" +
            "end-class;\n" +
            "\n" +
            "method Get\n" +
            "   Return %Session;\n" +
            "end-method;\n");

        var type = CallerInitializer(resolver, SvcCaller + "Local ApiObject &s = &svc.Get();");

        Assert.Equal("Query", type!.Name, ignoreCase: true);
    }

    [Fact]
    public void Cycle_caches_outermost_and_evicts_nested_member()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:A", GetMethodClass("A", "   Return (create PKG:B()).Get();\n"));
        resolver.Add("PKG:B", GetMethodClass("B", "   Return (create PKG:A()).Get();\n"));

        var metadata = resolver.GetTypeMetadata("PKG:A");

        Assert.NotNull(metadata);
        Assert.Equal(0, resolver.ParseErrorCount("PKG:A"));
        Assert.Equal(0, resolver.ParseErrorCount("PKG:B"));
        Assert.Null(metadata.Methods["Get"].RefinedReturnType);
        Assert.True(metadata.IsProvisional);

        // A, the outermost, is cached with what the cycle guard allowed
        int parsesA = resolver.ParseCount("PKG:A");
        Assert.Same(metadata, resolver.GetTypeMetadata("PKG:A"));
        Assert.Equal(parsesA, resolver.ParseCount("PKG:A"));

        // B, cut short inside A's refinement, was evicted: its first top-level fetch re-parses it
        Assert.False(resolver.Cache.Contains("PKG:B"));
        int parsesB = resolver.ParseCount("PKG:B");
        resolver.GetTypeMetadata("PKG:B");
        Assert.Equal(parsesB + 1, resolver.ParseCount("PKG:B"));
    }

    [Fact]
    public void Depth_limit_cuts_chain()
    {
        var resolver = new SourceTypeMetadataResolver();
        for (int i = 1; i <= 4; i++)
            resolver.Add($"PKG:C{i}", GetMethodClass($"C{i}", $"   Return (create PKG:C{i + 1}()).Get();\n"));
        resolver.Add("PKG:C5", GetMethodClass("C5", "   Return %Session;\n"));

        var metadata = resolver.GetTypeMetadata("PKG:C1");

        Assert.NotNull(metadata);
        Assert.Equal(0, resolver.ParseErrorCount("PKG:C1"));
        Assert.True(metadata.IsProvisional);
        Assert.Null(metadata.Methods["Get"].RefinedReturnType);
        // C4 was cut short, so C5 was never fetched
        Assert.Equal(0, resolver.ParseCount("PKG:C5"));
        // The outermost is cached; the cut-short chain under it is evicted
        Assert.True(resolver.Cache.Contains("PKG:C1"));
        for (int i = 2; i <= 4; i++)
            Assert.False(resolver.Cache.Contains($"PKG:C{i}"));
    }

    [Fact]
    public void Mutual_reference_pair_is_parsed_a_bounded_number_of_times()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:A",
            "class A\n" +
            "   method A();\n" +
            "   method GetB() Returns PKG:B;\n" +
            "   method GetSess() Returns ApiObject;\n" +
            "private\n" +
            "   instance PKG:B &b;\n" +
            "end-class;\n" +
            "\n" +
            "method A\n" +
            "   &b = create PKG:B();\n" +
            "end-method;\n" +
            "\n" +
            "method GetB\n" +
            "   Return &b;\n" +
            "end-method;\n" +
            "\n" +
            "method GetSess\n" +
            "   Return %Session;\n" +
            "end-method;\n");
        resolver.Add("PKG:B",
            "class B\n" +
            "   method Parent() Returns PKG:A;\n" +
            "   method GetRec() Returns Record;\n" +
            "private\n" +
            "   instance PKG:A &parent;\n" +
            "end-class;\n" +
            "\n" +
            "method Parent\n" +
            "   Return &parent;\n" +
            "end-method;\n" +
            "\n" +
            "method GetRec\n" +
            "   Return CreateRecord(Record.PSOPRDEFN);\n" +
            "end-method;\n");

        var type = CallerInitializer(resolver,
            "Local PKG:B &b = create PKG:B();\n" +
            "Local Record &r1 = &b.GetRec();\n" +
            "Local Record &r2 = &b.GetRec();\n" +
            "Local Record &r3 = &b.GetRec();\n" +
            "Local Record &r4 = &b.GetRec();\n" +
            "Local Record &r5 = &b.GetRec();\n" +
            "Local PKG:A &a1 = &b.Parent();\n" +
            "Local PKG:A &a2 = &b.Parent();\n" +
            "Local PKG:B &b2 = &b.Parent().GetB();\n" +
            "Local Record &r6 = &b.Parent().GetB().GetRec();\n" +
            "Local ApiObject &s = &b.Parent().GetSess();");

        Assert.Equal(0, resolver.ParseErrorCount("PKG:A"));
        Assert.Equal(0, resolver.ParseErrorCount("PKG:B"));
        Assert.Equal("Session", type!.Name, ignoreCase: true);
        Assert.True(resolver.ParseCount("PKG:A") <= 2 && resolver.ParseCount("PKG:B") <= 2,
            $"A parsed {resolver.ParseCount("PKG:A")} times, B {resolver.ParseCount("PKG:B")}");
    }

    [Fact]
    public void Saving_callee_evicts_dependent_callers()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:B", GetMethodClass("B", "   Return %Session;\n"));
        resolver.Add("PKG:C", GetMethodClass("C", "   Return (create PKG:B()).Get();\n"));
        resolver.Add("PKG:D", GetMethodClass("D", "   Return %Session;\n"));

        var c = resolver.GetTypeMetadata("PKG:C");
        resolver.GetTypeMetadata("PKG:D");
        Assert.Equal(PeopleCodeType.Session, c!.Methods["Get"].RefinedReturnType!.Value.Type);
        Assert.True(resolver.Cache.Contains("PKG:C"));

        resolver.Cache.Remove("PKG:B");

        Assert.False(resolver.Cache.Contains("PKG:B"));
        Assert.False(resolver.Cache.Contains("PKG:C"));
        // An unrelated class stays cached
        Assert.True(resolver.Cache.Contains("PKG:D"));
    }

    [Fact]
    public void Callee_fetched_from_cache_is_still_a_dependency()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:B", GetMethodClass("B", "   Return %Session;\n"));
        resolver.Add("PKG:C", GetMethodClass("C", "   Return (create PKG:B()).Get();\n"));
        resolver.Add("PKG:E", GetMethodClass("E", "   Return (create PKG:C()).Get();\n"));

        // B is cached before C's refinement asks for it, and E sees C only through the cache
        resolver.GetTypeMetadata("PKG:B");
        resolver.GetTypeMetadata("PKG:C");
        var e = resolver.GetTypeMetadata("PKG:E");
        Assert.Equal(PeopleCodeType.Session, e!.Methods["Get"].RefinedReturnType!.Value.Type);

        // Transitive: E depends on C, which depends on B
        resolver.Cache.Remove("PKG:B");

        Assert.False(resolver.Cache.Contains("PKG:C"));
        Assert.False(resolver.Cache.Contains("PKG:E"));
    }

    [Fact]
    public void Chain_within_depth_limit_refines()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:C1", GetMethodClass("C1", "   Return (create PKG:C2()).Get();\n"));
        resolver.Add("PKG:C2", GetMethodClass("C2", "   Return (create PKG:C3()).Get();\n"));
        resolver.Add("PKG:C3", GetMethodClass("C3", "   Return %Session;\n"));

        var metadata = resolver.GetTypeMetadata("PKG:C1");

        Assert.False(metadata!.IsProvisional);
        Assert.Equal(PeopleCodeType.Session, metadata.Methods["Get"].RefinedReturnType!.Value.Type);
    }

    [Fact]
    public void Complete_refinement_is_cached()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc", GetMethodClass("Svc", "   Return %Session;\n"));

        var first = resolver.GetTypeMetadata("PKG:Svc");
        var second = resolver.GetTypeMetadata("PKG:Svc");

        Assert.False(first!.IsProvisional);
        Assert.Same(first, second);
        Assert.Equal(1, resolver.ParseCount("PKG:Svc"));
    }

    [Fact]
    public void Self_reference_is_not_a_cycle()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc",
            "class Svc\n" +
            "   method Get() Returns ApiObject;\n" +
            "   method Use();\n" +
            "end-class;\n" +
            "\n" +
            "method Get\n" +
            "   Return %Session;\n" +
            "end-method;\n" +
            "\n" +
            "method Use\n" +
            "   Local PKG:Svc &o = create PKG:Svc();\n" +
            "   Local ApiObject &x = %This.Get();\n" +
            "end-method;\n");

        var first = resolver.GetTypeMetadata("PKG:Svc");
        resolver.GetTypeMetadata("PKG:Svc");

        Assert.Equal(0, resolver.ParseErrorCount("PKG:Svc"));
        Assert.False(first!.IsProvisional);
        Assert.Equal(PeopleCodeType.Session, first.Methods["Get"].RefinedReturnType!.Value.Type);
        Assert.Equal(1, resolver.ParseCount("PKG:Svc"));
    }

    [Fact]
    public void Refiner_failure_keeps_unrefined_metadata()
    {
        var resolver = new SourceTypeMetadataResolver();
        resolver.Add("PKG:Svc",
            "class Svc\n" +
            "   method Broken();\n" +
            "   method Get() Returns ApiObject;\n" +
            "end-class;\n" +
            "\n" +
            "method Broken\n" +
            "   Local number &n = (1 + ;\n" +
            "   If &n Then\n" +
            "end-method;\n" +
            "\n" +
            "method Get\n" +
            "   Return %Session;\n" +
            "end-method;\n");

        var metadata = resolver.GetTypeMetadata("PKG:Svc");

        Assert.NotNull(metadata);
        Assert.True(resolver.ParseErrorCount("PKG:Svc") > 0);
        Assert.True(metadata.Methods.ContainsKey("Get"));
        Assert.False(metadata.IsProvisional);
        // Error recovery keeps Get's body intact, so it still refines
        Assert.Equal(PeopleCodeType.Session, metadata.Methods["Get"].RefinedReturnType!.Value.Type);
    }

    [Fact]
    public void Refiner_exception_restores_extracted_metadata()
    {
        // Get is visited (and refined) before Use, whose other-class reference throws
        var (program, parseErrors) = Parse(
            "class Svc\n" +
            "   method Get() Returns ApiObject;\n" +
            "   method Use();\n" +
            "   property ApiObject Sess;\n" +
            "end-class;\n" +
            "\n" +
            "method Get\n" +
            "   Return %Session;\n" +
            "end-method;\n" +
            "\n" +
            "method Use\n" +
            "   Local ApiObject &x = (create PKG:Other()).Get();\n" +
            "end-method;\n");
        Assert.Empty(parseErrors);
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "PKG:Svc");
        var resolver = new ThrowingResolver();
        Assert.ThrowsAny<Exception>(() => TypeInferenceVisitor.Run(program, TypeMetadataBuilder.ExtractMetadata(program, "PKG:Svc"), resolver));

        ReturnTypeRefiner.Refine(program, metadata, resolver);

        Assert.Null(metadata.Methods["Get"].RefinedReturnType);
        Assert.False(metadata.IsProvisional);
    }

    /// <summary>
    /// A resolver whose cache access throws, so inference fails partway through Refine.
    /// </summary>
    private sealed class ThrowingResolver : PeopleCodeTypeInfo.Contracts.ITypeMetadataResolver
    {
        protected override TypeMetadata? GetTypeMetadataCore(string qualifiedName) => throw new InvalidOperationException("boom");
        protected override Task<TypeMetadata?> GetTypeMetadataCoreAsync(string qualifiedName) => throw new InvalidOperationException("boom");
        protected override TypeInfo GetFieldTypeCore(string fieldName) => throw new InvalidOperationException("boom");
        protected override Task<TypeInfo> GetFieldTypeCoreAsync(string fieldName) => throw new InvalidOperationException("boom");
        protected override List<string> GetClassesInPackageCore(string packagePath) => throw new InvalidOperationException("boom");
    }
}
