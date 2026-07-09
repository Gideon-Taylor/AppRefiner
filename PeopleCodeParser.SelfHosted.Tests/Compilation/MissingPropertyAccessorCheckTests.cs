using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class MissingPropertyAccessorCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Get_only_missing_getter_is_error()
    {
        var diags = Check(@"
class Sample
   property number Foo get;
end-class;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.MissingPropertyAccessor &&
            d.Message.Contains("Foo") &&
            d.Message.Contains("getter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Get_set_missing_setter_is_error()
    {
        var diags = Check(@"
class Sample
   property number Foo get set;
end-class;

get Foo
   Return 1;
end-get;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.MissingPropertyAccessor &&
            d.Message.Contains("Foo") &&
            d.Message.Contains("setter", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(diags, d =>
            d.Code == DiagnosticCode.MissingPropertyAccessor &&
            d.Message.Contains("getter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Get_set_missing_both_reports_two()
    {
        var diags = Check(@"
class Sample
   property number Foo get set;
end-class;
");
        Assert.Equal(2, diags.Count(d => d.Code == DiagnosticCode.MissingPropertyAccessor));
    }

    [Fact]
    public void Abstract_property_is_ok()
    {
        var diags = Check(@"
class Sample
   property number Foo abstract;
end-class;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingPropertyAccessor);
    }

    [Fact]
    public void Fully_implemented_get_set_is_ok()
    {
        var diags = Check(@"
class Sample
   property number Foo get set;
end-class;

get Foo
   Return 1;
end-get;

set Foo
end-set;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingPropertyAccessor);
    }

    [Fact]
    public void Readonly_without_body_is_ok()
    {
        var diags = Check(@"
class Sample
   property number Foo readonly;
end-class;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingPropertyAccessor);
    }
}
