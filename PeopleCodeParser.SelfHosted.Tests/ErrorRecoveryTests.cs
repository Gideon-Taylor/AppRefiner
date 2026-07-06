using System.Text;
using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// ER-1: the error-recovery budget must replenish after each successfully parsed
/// statement, and recovery must always make forward progress — one bad construct
/// must not disable recovery for the rest of the file.
/// ER-2: a stray token in a class body or class header must not silently discard
/// all subsequent valid members.
/// </summary>
public class ErrorRecoveryTests
{
    [Fact]
    public void StallingWhileConstruct_DoesNotPoisonRestOfFile()
    {
        var source = """
            While &x Else End-While;
            &a = 1;
            &b = 2;
            """;

        var (program, errors) = Parse(source);

        Assert.DoesNotContain(errors, e => e.Message.Contains("Too many parse errors"));
        Assert.Equal(2, program.FindDescendants<AssignmentNode>()
            .Count(a => a.Target is IdentifierNode));
    }

    [Fact]
    public void BrokenStatementsAcrossBlocks_BudgetReplenishes()
    {
        // 12 broken statements — more than the old file-wide budget of 10.
        // Each If block routes through ParseStatementList's recovery path.
        var sb = new StringBuilder();
        for (int i = 0; i < 12; i++)
        {
            sb.AppendLine("If True Then");
            sb.AppendLine("   = 5;");
            sb.AppendLine($"   &ok{i} = {i};");
            sb.AppendLine("End-If;");
        }

        var (program, errors) = Parse(sb.ToString());

        Assert.DoesNotContain(errors, e => e.Message.Contains("Too many parse errors"));

        var assignedNames = program.FindDescendants<AssignmentNode>()
            .Select(a => (a.Target as IdentifierNode)?.Name)
            .Where(n => n != null)
            .ToHashSet();
        for (int i = 0; i < 12; i++)
        {
            Assert.Contains($"&ok{i}", assignedNames);
        }
    }

    [Fact]
    public void ClassBody_TypoInFirstImpl_LaterImplsStillAttach()
    {
        var source = """
            class TestClass
               method A();
               method B();
               property string Foo get;
            end-class;

            mthod A
               Local number &broken = 1;
            end-method;

            method B
               &x = 1;
            end-method;

            get Foo
               return "y";
            end-get;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var appClass = program.AppClass!;
        var methodB = appClass.Methods.Single(m => m.Name == "B");
        Assert.NotNull(methodB.Implementation);
        var propertyFoo = appClass.Properties.Single(p => p.Name == "Foo");
        Assert.NotNull(propertyFoo.Getter);
    }

    [Fact]
    public void ClassBody_GarbageBetweenImpls_LaterImplsStillAttach()
    {
        var source = """
            class TestClass
               method A();
               method B();
            end-class;

            method A
               &x = 1;
            end-method;

            some stray garbage here

            method B
               &y = 2;
            end-method;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var appClass = program.AppClass!;
        Assert.NotNull(appClass.Methods.Single(m => m.Name == "A").Implementation);
        Assert.NotNull(appClass.Methods.Single(m => m.Name == "B").Implementation);
    }

    [Fact]
    public void ClassHeader_StrayTokens_LaterDeclarationsStillParse()
    {
        var source = """
            class TestClass
               method A();
               bogus bogus bogus
               method B();
            end-class;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var methodNames = program.AppClass!.Methods.Select(m => m.Name).ToList();
        Assert.Contains("A", methodNames);
        Assert.Contains("B", methodNames);
    }
}
