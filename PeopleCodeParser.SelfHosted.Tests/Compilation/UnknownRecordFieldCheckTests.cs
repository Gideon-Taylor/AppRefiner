using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Inference;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class UnknownRecordFieldCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> RunInRecordField(
        string source,
        string defaultRecordName,
        FakeTypeMetadataResolver resolver)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        var programMetadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, programMetadata, resolver, defaultRecordName);
        return CompileChecker.Check(
            program, errors, resolver,
            new CompileCheckContextInput(null, DefaultRecordName: defaultRecordName));
    }

    private static IReadOnlyList<CompileDiagnostic> Run(
        string source, FakeTypeMetadataResolver resolver)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        var programMetadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, programMetadata, resolver);
        return CompileChecker.Check(program, errors, resolver, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Bare_unknown_field_on_default_record_reports()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddRecordFields("WEBLIB_TS_TEST", "ISCRIPT1", "DESCR");

        var diags = RunInRecordField(@"
Local number &n;
&n = ACCTLOCK;
", "WEBLIB_TS_TEST", resolver);

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.UnknownRecordField &&
            d.Severity == DiagnosticSeverity.Error &&
            d.Message.Contains("ACCTLOCK") &&
            d.Message.Contains("WEBLIB_TS_TEST"));
    }

    [Fact]
    public void Bare_known_field_on_default_record_is_ok()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddRecordFields("WEBLIB_TS_TEST", "ISCRIPT1", "DESCR");

        var diags = RunInRecordField(@"
Local any &x;
&x = ISCRIPT1;
", "WEBLIB_TS_TEST", resolver);

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnknownRecordField);
    }

    [Fact]
    public void Bare_field_without_default_record_is_not_checked()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddRecordFields("WEBLIB_TS_TEST", "ISCRIPT1");

        // Outside record PeopleCode bare names are not buffer fields
        var diags = Run(@"
Local any &x;
&x = ACCTLOCK;
", resolver);

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnknownRecordField);
    }

    [Fact]
    public void Direct_REC_FIELD_unknown_reports()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddRecordFields("WEBLIB_TS_TEST", "ISCRIPT1");

        var diags = Run(@"
Local number &n;
&n = WEBLIB_TS_TEST.ACCTLOCK;
", resolver);

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.UnknownRecordField &&
            d.Message.Contains("ACCTLOCK") &&
            d.Message.Contains("WEBLIB_TS_TEST"));
    }

    [Fact]
    public void Direct_REC_FIELD_known_is_ok()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddRecordFields("WEBLIB_TS_TEST", "ISCRIPT1");

        var diags = Run(@"
Local any &x;
&x = WEBLIB_TS_TEST.ISCRIPT1;
", resolver);

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnknownRecordField);
    }

    [Fact]
    public void Record_keyword_FIELD_unknown_reports()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddRecordFields("PSOPRDEFN", "OPRID");

        var diags = Run(@"
Local Field &f;
&f = Record.PSOPRDEFN.ACCTLOCK;
", resolver);

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.UnknownRecordField &&
            d.Message.Contains("ACCTLOCK") &&
            d.Message.Contains("PSOPRDEFN"));
    }

    [Fact]
    public void Record_IsChanged_is_not_treated_as_field()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddRecordFields("PSOPRDEFN", "OPRID");

        var diags = Run(@"
Local boolean &b;
&b = Record.PSOPRDEFN.IsChanged;
", resolver);

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnknownRecordField);
    }

    [Fact]
    public void Unknown_record_metadata_does_not_false_positive()
    {
        // No AddRecordFields — RecordHasField returns null → skip
        var resolver = new FakeTypeMetadataResolver();

        var diags = RunInRecordField(@"
Local any &x;
&x = ACCTLOCK;
", "WEBLIB_TS_TEST", resolver);

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnknownRecordField);
    }

    [Fact]
    public void Left_of_dot_is_record_name_not_field_check()
    {
        var resolver = new FakeTypeMetadataResolver();
        // WEBLIB_TS_TEST is the record name (left of dot), not a field on itself
        resolver.AddRecordFields("WEBLIB_TS_TEST", "ISCRIPT1");

        var diags = RunInRecordField(@"
Local any &x;
&x = WEBLIB_TS_TEST.ISCRIPT1;
", "WEBLIB_TS_TEST", resolver);

        Assert.DoesNotContain(diags, d =>
            d.Code == DiagnosticCode.UnknownRecordField &&
            d.Message.Contains("'WEBLIB_TS_TEST' is not defined"));
    }
}
