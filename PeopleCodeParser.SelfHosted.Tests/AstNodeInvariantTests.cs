using System.Reflection;
using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;
using AstNodeBase = PeopleCodeParser.SelfHosted.AstNode;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// AST-1: the parent/child invariant must be enforced by the API, not by convention.
/// Child slots must not expose public setters (use SetX methods that manage AddChild),
/// and structural collections must not be publicly mutable (use AddX methods).
/// PC-2/PC-3 were caused by exactly this convention failing.
/// </summary>
public class AstNodeInvariantTests
{
    /// <summary>
    /// Cross-links that intentionally do not participate in the parent/child tree
    /// (back-references between declaration and implementation).
    /// </summary>
    private static readonly HashSet<string> CrossLinkProperties = new()
    {
        "Parent", "Declaration"
    };

    private static IEnumerable<Type> NodeTypes =>
        typeof(AstNodeBase).Assembly.GetTypes()
            .Where(t => typeof(AstNodeBase).IsAssignableFrom(t));

    [Fact]
    public void ChildNodeProperties_HaveNoPublicSetter()
    {
        var violations = new List<string>();

        foreach (var type in NodeTypes)
        {
            foreach (var prop in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!typeof(AstNodeBase).IsAssignableFrom(prop.PropertyType))
                    continue;
                if (CrossLinkProperties.Contains(prop.Name))
                    continue;

                if (prop.SetMethod?.IsPublic == true)
                    violations.Add($"{type.Name}.{prop.Name}");
            }
        }

        Assert.True(violations.Count == 0,
            "Public setters on child node slots:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void NodeCollections_AreNotPubliclyMutable()
    {
        var violations = new List<string>();

        foreach (var type in NodeTypes)
        {
            foreach (var prop in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var pt = prop.PropertyType;
                if (pt.IsGenericType &&
                    pt.GetGenericTypeDefinition() == typeof(List<>) &&
                    typeof(AstNodeBase).IsAssignableFrom(pt.GetGenericArguments()[0]))
                {
                    violations.Add($"{type.Name}.{prop.Name}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Publicly mutable node collections:\n" + string.Join("\n", violations));
    }

    private const string KitchenSinkSource = """
        Local number &i;

        If &a = 1 Then
           &x = 1;
        Else
           &y = 2;
        End-If;

        For &i = 1 To 10 Step 2
           &t = &i;
        End-For;

        While &a < 5
           &a = &a + 1;
        End-While;

        Repeat
           &a = &a - 1;
        Until &a <= 0;

        Try
           &z = 1;
        Catch Exception &ex
           &z = 2;
        End-Try;

        Exit;
        """;

    private const string AppClassSource = """
        class TestClass extends BasePkg:BaseClass
           method DoStuff(&p As number);
           property string Foo get set;
        end-class;

        method DoStuff
           /+ &p as Number +/
           Local number &x = &p;
        end-method;

        get Foo
           /+ Returns String +/
           return "bar";
        end-get;

        set Foo
           /+ &NewValue as String +/
           Local string &v = &NewValue;
        end-set;
        """;

    [Theory]
    [InlineData(KitchenSinkSource)]
    [InlineData(AppClassSource)]
    public void ParsedTree_HasConsistentParentLinks(string source)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);

        var visited = new HashSet<AstNodeBase>();
        var queue = new Queue<AstNodeBase>();
        queue.Enqueue(program);

        var orphans = new List<string>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!visited.Add(node))
                continue;

            if (!ReferenceEquals(node, program))
            {
                if (node.Parent == null)
                    orphans.Add($"{node.GetType().Name} '{node}' has no Parent");
                else if (!node.Parent.Children.Contains(node))
                    orphans.Add($"{node.GetType().Name} '{node}' missing from Parent.Children");
            }

            // Follow every structural link (single-child properties and node collections),
            // not just Children — orphan bugs are invisible via Children alone
            foreach (var prop in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (CrossLinkProperties.Contains(prop.Name))
                    continue;
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                if (typeof(AstNodeBase).IsAssignableFrom(prop.PropertyType))
                {
                    if (prop.GetValue(node) is AstNodeBase child)
                        queue.Enqueue(child);
                }
                else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType)
                         && prop.PropertyType != typeof(string))
                {
                    if (prop.GetValue(node) is System.Collections.IEnumerable items)
                    {
                        foreach (var item in items)
                        {
                            if (item is AstNodeBase child)
                                queue.Enqueue(child);
                        }
                    }
                }
            }
        }

        Assert.Empty(orphans);
    }
}
