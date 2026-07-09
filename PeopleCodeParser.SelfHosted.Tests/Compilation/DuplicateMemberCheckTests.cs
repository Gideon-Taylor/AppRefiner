using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class DuplicateMemberCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Two_methods_same_name_is_error()
    {
        var diags = Check(@"
class Sample
   method Foo();
   method Foo();
end-class;

method Foo
end-method;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.DuplicateMember &&
            d.Message.Contains("Foo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Two_properties_same_name_is_error()
    {
        var diags = Check(@"
class Sample
   property number Bar get;
   property string Bar get;
end-class;

get Bar
   Return 1;
end-get;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.DuplicateMember &&
            d.Message.Contains("Bar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Two_instance_vars_same_name_is_error()
    {
        var diags = Check(@"
class Sample
private
   instance number &x;
   instance string &x;
end-class;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.DuplicateMember &&
            d.Message.Contains("&x", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Method_and_property_same_name_is_error()
    {
        var diags = Check(@"
class Sample
   method Widget();
   property number Widget get;
end-class;

method Widget
end-method;

get Widget
   Return 1;
end-get;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.DuplicateMember &&
            d.Message.Contains("Widget", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Interface_duplicate_methods_is_error()
    {
        var diags = Check(@"
interface ISample
   method Run();
   method Run();
end-interface;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.DuplicateMember &&
            d.Message.Contains("Run", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Distinct_names_are_ok()
    {
        var diags = Check(@"
class Sample
   method Foo();
   property number Bar get;
private
   instance number &x;
   Constant &MAX = 1;
end-class;

method Foo
end-method;

get Bar
   Return &x;
end-get;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.DuplicateMember);
    }

    [Fact]
    public void Same_name_in_different_classes_not_applicable()
    {
        // Each program is one class; no cross-class check needed.
        var diags = Check(@"
class Sample
   method Foo();
end-class;

method Foo
end-method;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.DuplicateMember);
    }
}
