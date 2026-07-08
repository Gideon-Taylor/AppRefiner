using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class InvalidMemberAccessCheckTests
{
    /// <summary>
    /// The check is driven by INFERRED types, which CompileChecker.Check does not
    /// populate itself (inference is a documented precondition). Mirror production:
    /// parse, build program metadata, run TypeInferenceVisitor, then Check.
    /// </summary>
    private static IReadOnlyList<CompileDiagnostic> Run(
        string source, FakeTypeMetadataResolver? resolver = null)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        resolver ??= new FakeTypeMetadataResolver();
        var programMetadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, programMetadata, resolver);
        return CompileChecker.Check(program, errors, resolver, new CompileCheckContextInput(null));
    }

    /// <summary>
    /// Runs the check for a program that IS an application class, mirroring how the
    /// styler drives it: build metadata for the current class from the live program under
    /// <paramref name="qualifiedName"/>, run inference with it, then Check — passing that
    /// same live metadata as SelfMetadata when <paramref name="provideSelfMetadata"/> is
    /// true. Passing false reproduces the pre-fix behavior (self resolved purely from the
    /// DB-backed resolver), which isolates exactly what the self special-case adds.
    /// </summary>
    private static IReadOnlyList<CompileDiagnostic> RunClass(
        string source, string qualifiedName, FakeTypeMetadataResolver resolver,
        bool provideSelfMetadata = true)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        var programMetadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
        TypeInferenceVisitor.Run(program, programMetadata, resolver);
        return CompileChecker.Check(program, errors, resolver,
            new CompileCheckContextInput(null, provideSelfMetadata ? programMetadata : null));
    }

    /// <summary>
    /// Mirrors <see cref="Run"/> but supplies a default record name, reproducing a
    /// record-field PeopleCode program (e.g. WEBLIB_TS_TEST.ISCRIPT2). This is the
    /// context in which a bare identifier is inferred as a field on the default record.
    /// </summary>
    private static IReadOnlyList<CompileDiagnostic> RunInRecordField(
        string source, string defaultRecordName, FakeTypeMetadataResolver? resolver = null)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        resolver ??= new FakeTypeMetadataResolver();
        var programMetadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, programMetadata, resolver, defaultRecordName);
        return CompileChecker.Check(program, errors, resolver, new CompileCheckContextInput(null));
    }

    private static FakeTypeMetadataResolver ResolverWithKnownClass()
    {
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Known", new TypeMetadata
        {
            QualifiedName = "PKG:Known",
            Name = "Known",
            Kind = ProgramKind.AppClass,
            Methods = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["DoWork"] = new FunctionInfo { Name = "DoWork" },
            },
            Properties = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["Score"] = new PropertyInfo(PeopleCodeTypeInfo.Types.PeopleCodeType.Number, 0) { Name = "Score" },
            },
        });
        return fake;
    }

    // ------------------------------------------------------------------
    // Builtin object members (PeopleCodeTypeDatabase)
    // ------------------------------------------------------------------
    // NOTE: the styler (and this port) skips TypeKind.Primitive targets, so a
    // 'Local string &s' never reaches member validation — builtin coverage uses
    // a BuiltinObject type (Rowset) instead.

    [Fact]
    public void Reports_unknown_method_on_builtin_object()
    {
        var diags = Run(@"
Local Rowset &rs;
&rs.TotallyBogusMethod();");

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Severity == DiagnosticSeverity.Error &&
            d.Message.Contains("TotallyBogusMethod") &&
            d.Message.Contains("method"));
    }

    [Fact]
    public void Does_not_report_valid_builtin_method()
    {
        var diags = Run(@"
Local Rowset &rs;
&rs.GetRow(1);");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
    }

    [Fact]
    public void Reports_unknown_property_on_builtin_object_but_not_valid_one()
    {
        var diags = Run(@"
Local Rowset &rs;
Local number &n = &rs.ActiveRowCount;
Local number &m = &rs.BogusCount;");

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("BogusCount") &&
            d.Message.Contains("property"));
        Assert.DoesNotContain(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("ActiveRowCount"));
    }

    [Fact]
    public void Skips_primitive_targets_entirely()
    {
        // string is TypeKind.Primitive — the check never validates members on it.
        var diags = Run(@"
Local string &s;
&s.NoSuchMethod();");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
    }

    // ------------------------------------------------------------------
    // App class members (resolver-backed inheritance walk)
    // ------------------------------------------------------------------

    [Fact]
    public void Reports_unknown_method_on_app_class()
    {
        var diags = Run(@"
import PKG:Known;
Local PKG:Known &o;
&o.MissingMethod();", ResolverWithKnownClass());

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("MissingMethod") &&
            d.Message.Contains("PKG:Known"));
    }

    [Fact]
    public void Does_not_report_known_app_class_method_or_property()
    {
        var diags = Run(@"
import PKG:Known;
Local PKG:Known &o;
&o.DoWork();
Local number &n = &o.Score;", ResolverWithKnownClass());

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
    }

    [Fact]
    public void Property_access_does_not_fall_back_to_methods()
    {
        // DoWork exists as a METHOD; accessing it without () is strictly a
        // property / instance-variable lookup and must be flagged.
        var diags = Run(@"
import PKG:Known;
Local PKG:Known &o;
Local any &x = &o.DoWork;", ResolverWithKnownClass());

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("DoWork") &&
            d.Message.Contains("property"));
    }

    [Fact]
    public void Assumes_valid_when_class_metadata_cannot_be_resolved()
    {
        // PKG:Unknown is not registered in the fake resolver: GetTypeMetadata
        // returns null and the check must assume the member is valid.
        var diags = Run(@"
import PKG:Unknown;
Local PKG:Unknown &o;
&o.Anything();", new FakeTypeMetadataResolver());

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
    }

    // ------------------------------------------------------------------
    // Graceful skips
    // ------------------------------------------------------------------

    [Fact]
    public void Skips_dynamic_member_access()
    {
        var diags = Run(@"
Local Rowset &rs;
Local any &v = &rs.""SomeDynamicName"";");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
    }

    [Fact]
    public void Skips_unresolved_or_any_typed_targets()
    {
        var diags = Run(@"
Local any &a;
&a.Whatever();
&undeclared.Foo();");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
    }

    [Fact]
    public void Skips_record_property_access_but_validates_record_method_calls()
    {
        var diags = Run(@"
Local Record &r;
Local any &v = &r.SOME_RANDOM_FIELD;
&r.BogusRecordMethod();");

        // Property access on a Record is a dynamic field lookup — never flagged.
        Assert.DoesNotContain(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("SOME_RANDOM_FIELD"));
        // Method calls on a Record ARE validated against the builtin.
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("BogusRecordMethod"));
    }

    // ------------------------------------------------------------------
    // Self ("current class") member resolution from the live in-editor
    // program — recognizes members added but not yet saved to the DB.
    // ------------------------------------------------------------------

    /// <summary>
    /// Resolver holding a STALE copy of PKG:Foo (missing NewMethod), simulating the
    /// last-saved DB source not yet containing a method just added in the editor.
    /// </summary>
    private static FakeTypeMetadataResolver ResolverWithStaleFoo()
    {
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Foo", new TypeMetadata
        {
            QualifiedName = "PKG:Foo",
            Name = "Foo",
            Kind = ProgramKind.AppClass,
            Methods = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["Existing"] = new FunctionInfo { Name = "Existing" },
            },
        });
        return fake;
    }

    private const string FooCallsNewMethod = @"
class Foo
   method Existing();
   method NewMethod();
end-class;

method Existing
   %This.NewMethod();
end-method;

method NewMethod
end-method;
";

    [Fact]
    public void Recognizes_unsaved_method_on_current_class_via_self_metadata()
    {
        // NewMethod exists in the live program but not in the resolver's stale copy.
        // The self special-case must resolve it from the live metadata → no diagnostic.
        var diags = RunClass(FooCallsNewMethod, "PKG:Foo", ResolverWithStaleFoo());

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
    }

    [Fact]
    public void Flags_unsaved_method_on_current_class_without_self_metadata()
    {
        // Same program, but no live self metadata supplied (pre-fix behavior): the check
        // falls back to the resolver's stale PKG:Foo and flags NewMethod. This pins down
        // that the self special-case is exactly what suppresses the false positive above.
        var diags = RunClass(FooCallsNewMethod, "PKG:Foo", ResolverWithStaleFoo(),
            provideSelfMetadata: false);

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("NewMethod") &&
            d.Message.Contains("method"));
    }

    [Fact]
    public void Resolves_inherited_member_through_self_then_base_chain()
    {
        // BaseMethod lives on PKG:Base (resolver), not on the current class. The walk must
        // start at the live self metadata, miss, then advance to the base via the resolver.
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Base", new TypeMetadata
        {
            QualifiedName = "PKG:Base",
            Name = "Base",
            Kind = ProgramKind.AppClass,
            Methods = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["BaseMethod"] = new FunctionInfo { Name = "BaseMethod" },
            },
        });

        const string src = @"
import PKG:Base;

class Foo extends PKG:Base
   method Existing();
end-class;

method Existing
   %This.BaseMethod();
end-method;
";
        var diags = RunClass(src, "PKG:Foo", fake);

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
    }

    [Fact]
    public void Still_flags_genuinely_nonexistent_member_on_current_class()
    {
        // Nope() is in neither the live buffer nor any base — the self special-case only
        // ADDS members that genuinely exist in the editor; it must not mask real errors.
        const string src = @"
class Foo
   method Existing();
end-class;

method Existing
   %This.Nope();
end-method;
";
        var diags = RunClass(src, "PKG:Foo", new FakeTypeMetadataResolver());

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("Nope") &&
            d.Message.Contains("method"));
    }

    // ------------------------------------------------------------------
    // Definition reference keywords (Field.X, Record.X, ...) in record-field
    // PeopleCode. The keyword half must NOT be inferred as a field on the default
    // record, or the member-access target type is poisoned and the reference is
    // falsely flagged (e.g. "'OPRID' is not a property of 'Field(REC.Field)'").
    // ------------------------------------------------------------------

    [Fact]
    public void Does_not_report_field_definition_reference_in_record_field_program()
    {
        // Faithful reproduction of the reported bug: Field.OPRID inside a record-field
        // PeopleCode (WEBLIB_TS_TEST.ISCRIPT2). Field is a definition-reference keyword,
        // not a field named "Field" on WEBLIB_TS_TEST.
        var diags = RunInRecordField(@"
Function StrangeWhen()
   Local string &fieldName;
   If (&fieldName = Field.OPRID) Then
   End-If;
End-Function;", "WEBLIB_TS_TEST");

        Assert.DoesNotContain(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("OPRID"));
    }

    [Fact]
    public void Does_not_report_record_definition_reference_in_record_field_program()
    {
        // The whole keyword family is affected; Record.X must be immune too.
        var diags = RunInRecordField(@"
Function StrangeWhen()
   Local Record &rec = GetRecord(Record.DERIVED);
End-Function;", "WEBLIB_TS_TEST");

        Assert.DoesNotContain(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("DERIVED"));
    }

    [Fact]
    public void Does_not_report_instance_variable_accessed_as_property_on_self()
    {
        // Instance variables are declared with a leading '&' but are validly accessed as a
        // property via %This.Name (without the '&'). The check must recognize CVProduct even
        // though its metadata key is "&CVProduct" (this is the mismatch that was fixed).
        const string src = @"
class Foo
   method FooBar();
private
   instance IS_CO:Product &CVProduct;
end-class;

method FooBar
   %This.CVProduct.DoAThing();
end-method;
";
        var diags = RunClass(src, "PKG:Foo", new FakeTypeMetadataResolver());

        Assert.DoesNotContain(diags, d =>
            d.Code == DiagnosticCode.InvalidMemberAccess &&
            d.Message.Contains("CVProduct"));
    }
}
