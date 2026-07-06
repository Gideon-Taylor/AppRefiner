using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeTypeInfo.Database;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class UndeclaredFunctionCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        // No resolver, no inference: the check is pure AST + the static builtin DB.
        var (program, errors) = ParseTestHelper.Parse(source);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Reports_unknown_function_with_unknown_fix_context()
    {
        var diags = Check("DoesNotExist();");

        var diag = Assert.Single(diags, d => d.Code == DiagnosticCode.UndeclaredFunction);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("is not declared or defined", diag.Message);
        var fix = Assert.IsType<UndeclaredFunctionUnknownFix>(diag.FixContext);
        Assert.Equal("DoesNotExist", fix.FunctionName);
    }

    [Fact]
    public void Does_not_report_declared_function()
    {
        var diags = Check(@"
Declare Function Foo PeopleCode FUNCLIB_REC.FIELD1 FieldFormula;
Foo();");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UndeclaredFunction);
    }

    [Fact]
    public void Reports_forward_reference_with_forward_ref_fix_context()
    {
        // Foo is implemented BELOW its first use: PeopleCode is single-pass, so this
        // is a compile error with a "move the implementation up" fix.
        var diags = Check(@"
Function Caller()
   Foo();
End-Function;

Function Foo()
End-Function;");

        var diag = Assert.Single(diags, d => d.Code == DiagnosticCode.UndeclaredFunction);
        Assert.Contains("is defined below its first use", diag.Message);
        var fix = Assert.IsType<UndeclaredFunctionForwardRefFix>(diag.FixContext);
        Assert.Equal("Foo", fix.FunctionName);
        Assert.Equal("Foo", fix.ImplName);
        Assert.Equal("Move Function 'Foo' above 'Caller'", fix.MoveDescription);
    }

    [Fact]
    public void Does_not_report_when_implementation_is_above_the_call()
    {
        var diags = Check(@"
Function Foo()
End-Function;

Function Caller()
   Foo();
End-Function;");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UndeclaredFunction);
    }

    [Fact]
    public void Does_not_report_builtin_function()
    {
        // Guard the premise: MsgGet must actually be a known builtin.
        Assert.NotNull(PeopleCodeTypeDatabase.GetFunction("MsgGet"));

        var diags = Check(@"MsgGet(0, 0, ""message not found"");");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UndeclaredFunction);
    }

    [Fact]
    public void Does_not_report_method_style_or_variable_callees()
    {
        // &rowset(1) is a default-method call on a variable, %This.X() is a member
        // access — neither has a Generic IdentifierNode callee.
        var diags = Check(@"
Local Rowset &rs = GetLevel0();
&rs(1);");

        Assert.DoesNotContain(diags, d =>
            d.Code == DiagnosticCode.UndeclaredFunction && d.Message.Contains("&rs"));
    }
}
