using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// R-10..R-12 + the Extends/Implements name bug from the recovery audit.
/// </summary>
public class RecoveryLowTests
{
    [Fact]
    public void ExtendsImplementsAnnotation_StoresTheActualMemberName()
    {
        // Bonus bug: ImplementedMethodName was assigned from Current.Text AFTER the
        // name was consumed, so every annotation recorded "+/"
        var source = """
            class TestClass extends BasePkg:BaseClass
               method DoIt();
               property string Foo get;
            end-class;

            method DoIt
               /+ Extends/Implements BasePkg:BaseClass.DoIt +/
               &x = 1;
            end-method;

            get Foo
               /+ Extends/Implements BasePkg:BaseClass.Foo +/
               return "y";
            end-get;
            """;

        var (program, errors) = Parse(source);

        Assert.Empty(errors);
        var doIt = program.AppClass!.Methods.Single(m => m.Name == "DoIt");
        Assert.Equal("DoIt", doIt.Implementation!.ImplementedMethodName);
        var foo = program.AppClass.Properties.Single(p => p.Name == "Foo");
        Assert.Equal("Foo", foo.Getter!.ImplementedPropertyName);
    }

    [Fact]
    public void BrokenInterpolationHole_DoesNotLeakExceptionText()
    {
        // R-10: leftover in-string tokens produced "Error parsing expression:
        // Unexpected literal type: InterpStringEnd"
        var (program, errors) = Parse("&s = $\"a{&x &y}b\";\n&z = 1;");

        Assert.NotEmpty(errors);
        Assert.DoesNotContain(errors, e => e.Message.Contains("Unexpected literal type"));
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&z");
    }

    [Fact]
    public void HalfTypedConstant_KeepsTheNamedConstant()
    {
        // R-11: "Constant &X = ;" dropped the named constant entirely
        var (program, errors) = Parse("Constant &X = ;\n&a = &X;");

        Assert.NotEmpty(errors);
        Assert.Contains(program.Constants, c => c.Name == "&X");
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&a");
    }

    [Fact]
    public void OrphanedEndIf_GetsAHumanMessage()
    {
        // R-12: was "Unexpected token: EndIf"
        var (_, errors) = Parse("&x = 1;\nEnd-If;");

        Assert.Contains(errors, e => e.Message.Contains("'End-If'") && e.Message.Contains("no matching"));
        Assert.DoesNotContain(errors, e => e.Message.Contains("Unexpected token: EndIf"));
    }
}
