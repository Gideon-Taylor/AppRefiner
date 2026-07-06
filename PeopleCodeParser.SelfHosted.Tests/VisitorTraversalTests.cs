using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// VT-1..VT-5 (+VT-7): visitor traversal gaps — type nodes that no visitor could
/// ever reach, double-visited base types, and colliding catch variables.
/// </summary>
public class VisitorTraversalTests
{
    private sealed class AppClassTypeCollector : ScopedAstVisitor<object>
    {
        public List<string> Visited { get; } = new();

        public override void VisitAppClassType(AppClassTypeNode node)
        {
            Visited.Add(node.TypeName);
            base.VisitAppClassType(node);
        }
    }

    private static AppClassTypeCollector Collect(string source)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        var collector = new AppClassTypeCollector();
        program.Accept(collector);
        return collector;
    }

    [Fact]
    public void PropertyDeclaredType_IsVisited()
    {
        var collector = Collect("""
            class TestClass
               property TestPkg:PropType PropFoo get;
            end-class;

            get PropFoo
               return Null;
            end-get;
            """);

        Assert.Contains("TestPkg:PropType", collector.Visited);
    }

    [Fact]
    public void MethodParameterAnnotationType_IsVisited()
    {
        var collector = Collect("""
            class TestClass
               method DoIt(&p As TestPkg:ParamType);
            end-class;

            method DoIt
               /+ &p as TestPkg:AnnType +/
               &x = 1;
            end-method;
            """);

        Assert.Contains("TestPkg:AnnType", collector.Visited);
    }

    [Fact]
    public void SetterParameterAnnotationType_IsVisited()
    {
        var collector = Collect("""
            class TestClass
               property string Foo get set;
            end-class;

            get Foo
               return "y";
            end-get;

            set Foo
               /+ &NewValue as TestPkg:SetType +/
               Local string &v = &NewValue;
            end-set;
            """);

        Assert.Contains("TestPkg:SetType", collector.Visited);
    }

    [Fact]
    public void BaseType_IsVisitedExactlyOnce()
    {
        var collector = Collect("""
            class TestClass extends TestPkg:BaseClass
               method DoIt();
            end-class;

            method DoIt
               &x = 1;
            end-method;
            """);

        Assert.Equal(1, collector.Visited.Count(t => t == "TestPkg:BaseClass"));
    }

    [Fact]
    public void SiblingCatchesReusingVariable_MergeIntoOneVariable()
    {
        var (program, errors) = Parse("""
            Try
               &z = 1;
            Catch Exception &ex
               &a = &ex;
            Catch Exception &ex
               &b = &ex;
            End-Try;
            """);
        Assert.Empty(errors);

        var visitor = new AppClassTypeCollector();
        program.Accept(visitor);

        var exVariable = Assert.Single(visitor.GetAllVariables().Where(v => v.Name == "&ex"));
        Assert.Equal(2, exVariable.References.Count(r => r.ReferenceType == ReferenceType.Declaration));
        Assert.Equal(2, exVariable.References.Count(r => r.ReferenceType == ReferenceType.Read));
    }

    [Fact]
    public void ExceptionVariable_IsSafeToRefactor()
    {
        var (program, errors) = Parse("""
            Try
               &z = 1;
            Catch Exception &ex
               &a = &ex;
            End-Try;
            """);
        Assert.Empty(errors);

        var visitor = new AppClassTypeCollector();
        program.Accept(visitor);

        var exVariable = visitor.GetAllVariables().Single(v => v.Name == "&ex");
        Assert.True(exVariable.IsSafeToRefactor);
    }
}
