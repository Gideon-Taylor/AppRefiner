using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// R-1..R-5 from the recovery-opportunity audit: mid-typing edits must stay
/// localized — one half-typed construct must not swallow its neighbor, walk
/// through block terminators, or hang the parser.
/// </summary>
public class RecoveryHardeningTests
{
    [Fact]
    public void StrayLibraryToken_DoesNotHang()
    {
        // R-4: previously an infinite loop in ParseProgramPreambles
        var (_, errors) = Parse("Library \"MYLIB\" (arg1 REF) Returns integer;");

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void MissingEndIf_DoesNotMergeFollowingMethods()
    {
        // R-2: statement recovery must stop at block terminators
        var source = """
            class TestClass
               method Foo();
               method Bar();
            end-class;

            method Foo
               If &x Then
                  &y = 1;
            end-method;

            method Bar
               &z = 2;
            end-method;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var bar = program.AppClass!.Methods.Single(m => m.Name == "Bar");
        Assert.NotNull(bar.Implementation);
    }

    [Fact]
    public void HalfTypedIf_DoesNotStealLaterIfsThen()
    {
        // R-3: Then-synchronization must not cross statement boundaries
        var source = """
            If
            &x = 1;
            If &y Then
               &z = 2;
            End-If;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        Assert.Equal(2, program.FindDescendants<IfStatementNode>().Count());
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&z");
    }

    [Fact]
    public void HalfTypedImport_DoesNotDestroyProgram()
    {
        // R-1: a bare "import" must not swallow the next import/class keyword
        var source = """
            import
            import TestPkg:ClassA;

            class TestClass
               method Foo();
            end-class;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        Assert.NotNull(program.AppClass);
        Assert.Contains(program.Imports, i => i.FullPath == "TestPkg:ClassA");
    }

    [Fact]
    public void HalfTypedMethodImpl_DoesNotSwallowNextImpl()
    {
        // R-5a
        var source = """
            class TestClass
               method Foo();
            end-class;

            method
            method Foo
               &x = 1;
            end-method;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var foo = program.AppClass!.Methods.Single(m => m.Name == "Foo");
        Assert.NotNull(foo.Implementation);
    }

    [Fact]
    public void HalfTypedGetter_DoesNotSwallowNextGetter()
    {
        // R-5b
        var source = """
            class TestClass
               property string Foo get;
            end-class;

            get
            get Foo
               return "y";
            end-get;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var foo = program.AppClass!.Properties.Single(p => p.Name == "Foo");
        Assert.NotNull(foo.Getter);
    }

    [Fact]
    public void HalfTypedFunction_DoesNotSwallowNextFunction()
    {
        // R-5c
        var source = """
            Function
            Function Bar()
               &x = 1;
            End-Function;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var bar = program.Functions.SingleOrDefault(f => f.Name == "Bar");
        Assert.NotNull(bar);
        Assert.NotNull(bar!.Body);
    }

    [Fact]
    public void HalfTypedPropertyName_DoesNotSwallowNextMember()
    {
        // R-5d
        var source = """
            class TestClass
               property string
               property number Bar;
            end-class;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        Assert.Contains(program.AppClass!.Properties, p => p.Name == "Bar");
    }

    [Fact]
    public void MissingPropertyName_DoesNotConsumeEndClass()
    {
        // R-5 deny-list: End-* keywords can never be identifiers
        var source = """
            class TestClass
               property string
            end-class;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        Assert.NotNull(program.AppClass);
        Assert.DoesNotContain(errors, e => e.Message.Contains("not at end of file"));
    }

    [Fact]
    public void HalfTypedMemberAccess_DoesNotSilentlyAbsorbNextStatement()
    {
        // R-5 deny-list: &-variables can never be generic ids ("&rec." + Enter
        // used to absorb the next line's "&x" as the member name with ZERO errors)
        var (program, errors) = Parse("&rec.\n&x = 1;");

        Assert.NotEmpty(errors);
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&x");
    }
}
