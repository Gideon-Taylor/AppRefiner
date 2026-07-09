using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class MissingSuperCallCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Constructor_without_super_when_base_exists_is_error()
    {
        var diags = Check(@"
import PKG:Base;

class Sub extends PKG:Base
   method Sub();
end-class;

method Sub
end-method;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.MissingSuperCall &&
            d.Message.Contains("Sub") &&
            d.Message.Contains("%Super"));
    }

    [Fact]
    public void Constructor_with_super_assign_is_ok()
    {
        var diags = Check(@"
import PKG:Base;

class Sub extends PKG:Base
   method Sub();
end-class;

method Sub
   %Super = create PKG:Base();
end-method;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Super_anywhere_in_body_is_ok()
    {
        var diags = Check(@"
import PKG:Base;

class Sub extends PKG:Base
   method Sub();
end-class;

method Sub
   Local number &x;
   &x = 1;
   %Super = create PKG:Base();
end-method;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void No_base_class_no_error()
    {
        var diags = Check(@"
class Root
   method Root();
end-class;

method Root
end-method;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Non_constructor_method_no_error()
    {
        var diags = Check(@"
import PKG:Base;

class Sub extends PKG:Base
   method DoWork();
end-class;

method DoWork
end-method;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }
}
