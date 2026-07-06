using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class UndefinedVariableCheckTests
{
    private static System.Collections.Generic.IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        return CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));
    }

    [Fact]
    public void Reports_undefined_variable_in_app_class_method()
    {
        var diags = Check(@"
class Foo
   method Bar();
end-class;

method Bar
   &undeclared = 1;
end-method;");

        Assert.Contains(diags, d => d.Code == DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void Does_not_report_declared_local_in_app_class_method()
    {
        var diags = Check(@"
class Foo
   method Bar();
end-class;

method Bar
   Local number &declared;
   &declared = 1;
end-method;");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void Does_not_report_for_non_class_program()
    {
        // Non-class code: undefined variables are only a code smell there and are
        // handled by the AppRefiner styler, not the compile checker.
        var diags = Check(@"
&undeclared = 1;
");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void Resets_per_run_across_shared_static_instance()
    {
        // UndefinedVariableCheck is a stateful check constructed fresh per Check() run
        // (CompileChecker.CreateChecks). It accumulates undefined vars across OnNode and
        // reports in Finish, and — belt-and-suspenders — resets its accumulators when it
        // sees the ProgramNode (dispatched first each run) in case an instance were ever
        // reused. This test pins that reset invariant: run 1 dirties the state with an
        // undefined var, run 2 (a clean class) must NOT surface run 1's leftovers.

        // Run 1: class with an undefined variable reference.
        var run1 = Check(@"
class Foo
   method Bar();
end-class;

method Bar
   &undef1 = 1;
end-method;");

        Assert.Contains(run1, d => d.Code == DiagnosticCode.UndefinedVariable &&
            d.Message.Contains("undef1", System.StringComparison.OrdinalIgnoreCase));

        // Run 2: a DIFFERENT, clean class (every local declared before use). If the reset
        // were broken, run 1's &undef1 would still be accumulated and reported here.
        var run2 = Check(@"
class Baz
   method Qux();
end-class;

method Qux
   Local number &clean;
   &clean = 1;
end-method;");

        Assert.DoesNotContain(run2, d => d.Code == DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void Reports_undefined_for_loop_iterator_in_app_class_method()
    {
        var diags = Check(@"
class Foo
   method Bar();
end-class;

method Bar
   Local number &sum;
   For &undeclaredIter = 1 To 10
      &sum = &sum + 1;
   End-For;
end-method;");

        Assert.Contains(diags, d => d.Code == DiagnosticCode.UndefinedVariable &&
            d.Message.Contains("for loop iterator", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Does_not_report_declared_for_loop_iterator_in_app_class_method()
    {
        // Negative side: iterator declared as a Local before the loop is defined.
        var diags = Check(@"
class Foo
   method Bar();
end-class;

method Bar
   Local number &iter;
   For &iter = 1 To 10
   End-For;
end-method;");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void Undefined_for_loop_iterator_reported_once_not_also_as_plain_undefined()
    {
        // The For iterator is also an IdentifierNode child, so it is dispatched to OnNode
        // both as a plain user-variable reference AND recorded as a for-loop iterator at
        // the same (name, location). Finish must dedup: report it once, as the iterator
        // message, never additionally as a plain "Undefined variable". Body references only
        // a declared local so &undeclaredIter appears solely in the loop header.
        var diags = Check(@"
class Foo
   method Bar();
end-class;

method Bar
   Local number &sum;
   For &undeclaredIter = 1 To 10
      &sum = 1;
   End-For;
end-method;");

        var undefinedDiags = diags
            .Where(d => d.Code == DiagnosticCode.UndefinedVariable)
            .ToList();

        Assert.Single(undefinedDiags);
        Assert.Contains("for loop iterator", undefinedDiags[0].Message,
            System.StringComparison.OrdinalIgnoreCase);
    }
}
