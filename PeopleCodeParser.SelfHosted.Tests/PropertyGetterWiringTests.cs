using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// PC-2: the property getter must be attached via SetGetterImplementation so it gets
/// a parent link — otherwise FindAncestor/FindDescendants/GetRoot are broken for
/// every getter body in every class.
/// PC-3: SetImplementationType must AddChild the annotation type node for the same reason.
/// </summary>
public class PropertyGetterWiringTests
{
    private const string GetterSource = """
        class TestClass
           property string Foo get;
        end-class;

        get Foo
           /+ Returns String +/
           return "bar";
        end-get;
        """;

    private const string GetterSetterSource = """
        class TestClass
           property string Foo get set;
        end-class;

        get Foo
           /+ Returns String +/
           return "bar";
        end-get;

        set Foo
           /+ &NewValue as String +/
           Local string &v = &NewValue;
        end-set;
        """;

    private const string ImplementsSource = """
        class TestClass extends BasePkg:BaseClass
           property string Foo get;
        end-class;

        get Foo
           /+ Extends/Implements BasePkg:BaseClass.Foo +/
           return "bar";
        end-get;
        """;

    [Fact]
    public void GetterImplementation_HasParentLink()
    {
        var (program, errors) = Parse(GetterSource);

        Assert.Empty(errors);
        var property = program.AppClass!.Properties.Single();
        Assert.NotNull(property.Getter);
        Assert.Same(property, property.Getter!.Parent);
    }

    [Fact]
    public void GetterContents_AreReachableFromRoot()
    {
        var (program, errors) = Parse(GetterSource);

        Assert.Empty(errors);
        Assert.Contains(program.FindDescendants<LiteralNode>(),
            l => l.Value?.ToString() == "bar");
    }

    [Fact]
    public void GetterBody_CanWalkBackToRoot()
    {
        var (program, errors) = Parse(GetterSource);

        Assert.Empty(errors);
        var getter = program.AppClass!.Properties.Single().Getter!;
        Assert.Same(program, getter.GetRoot());
        Assert.NotNull(getter.FindAncestor<AppClassNode>());
    }

    [Fact]
    public void SetterImplementation_HasParentLink()
    {
        var (program, errors) = Parse(GetterSetterSource);

        Assert.Empty(errors);
        var property = program.AppClass!.Properties.Single();
        Assert.NotNull(property.Setter);
        Assert.Same(property, property.Setter!.Parent);
    }

    [Fact]
    public void ImplementsAnnotationType_HasParentLink()
    {
        var (program, errors) = Parse(ImplementsSource);

        Assert.Empty(errors);
        var getter = program.AppClass!.Properties.Single().Getter!;
        Assert.NotNull(getter.ImplementedInterface);
        Assert.Same(getter, getter.ImplementedInterface!.Parent);
    }

    [Fact]
    public void ImplementsAnnotationType_IsReachableFromRoot()
    {
        var (program, errors) = Parse(ImplementsSource);

        Assert.Empty(errors);
        Assert.Contains(program.FindDescendants<AppClassTypeNode>(),
            t => t.Parent is PropertyImplNode);
    }
}
