using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// PC-4: ParseGenericId must not consume structural tokens (';', ')', operators, EOF)
/// as identifiers — doing so corrupts the surrounding declarations.
/// PC-5: declaration/implementation member matching must be case-insensitive,
/// like all PeopleCode identifiers.
/// </summary>
public class GenericIdAndMemberMatchingTests
{
    [Fact]
    public void CreateWithoutClassPath_DoesNotEatSemicolon()
    {
        var (program, errors) = Parse("&obj = create;\n&y = 2;");

        Assert.NotEmpty(errors);
        // The statement after the broken one must survive
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&y");
    }

    [Fact]
    public void IncompleteMethodDecl_DoesNotCorruptNextDeclaration()
    {
        // "method" with no name typed above an existing property declaration:
        // the property must not be swallowed as the method's name
        var source = """
            class TestClass
               method
               property string Foo get;
            end-class;

            get Foo
               /+ Returns String +/
               return "y";
            end-get;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var propertyFoo = program.AppClass!.Properties.SingleOrDefault(p => p.Name == "Foo");
        Assert.NotNull(propertyFoo);
    }

    [Fact]
    public void KeywordNamedMethods_AreValidDeclarations()
    {
        // Keywords are legal member names in PeopleCode — this compiles in App Designer
        var source = """
            class MyClass
               /* Constructor */
               method MyClass();
               method Property();
               property string F;
            end-class;
            """;

        var (program, errors) = Parse(source);

        Assert.Empty(errors);
        var methodNames = program.AppClass!.Methods.Select(m => m.Name).ToList();
        Assert.Contains("MyClass", methodNames);
        Assert.Contains("Property", methodNames);
        Assert.Single(program.AppClass.Properties, p => p.Name == "F");
    }

    [Fact]
    public void MethodImplementation_MatchesDeclarationCaseInsensitively()
    {
        var source = """
            class TestClass
               method Foo();
            end-class;

            method foo
               &x = 1;
            end-method;
            """;

        var (program, errors) = Parse(source);

        Assert.Empty(errors);
        var method = program.AppClass!.Methods.Single();
        Assert.NotNull(method.Implementation);
    }

    [Fact]
    public void PropertyGetter_MatchesDeclarationCaseInsensitively()
    {
        var source = """
            class TestClass
               property string Foo get;
            end-class;

            get foo
               /+ Returns String +/
               return "y";
            end-get;
            """;

        var (program, errors) = Parse(source);

        Assert.Empty(errors);
        var property = program.AppClass!.Properties.Single();
        Assert.NotNull(property.Getter);
    }
}
