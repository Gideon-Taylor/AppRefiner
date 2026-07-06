using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// LX-2: '+' followed by a block comment (`+/*`) must not lex as the `+/`
/// annotation-close operator — `&x = &y +/* note */ 1;` is valid PeopleCode.
/// </summary>
public class AnnotationOperatorLexingTests
{
    [Fact]
    public void PlusFollowedByBlockComment_ParsesAsAddition()
    {
        var (program, errors) = Parse("&x = &y +/* note */ 1;");

        Assert.Empty(errors);
        var add = program.FindDescendants<BinaryOperationNode>().Single();
        Assert.Equal(BinaryOperator.Add, add.Operator);
    }

    [Fact]
    public void MethodAnnotations_StillParse()
    {
        var source = """
            class TestClass
               method DoStuff(&p As number);
            end-class;

            method DoStuff
               /+ &p as Number +/
               Local number &x = &p;
            end-method;
            """;

        var (program, errors) = Parse(source);

        Assert.Empty(errors);
        Assert.NotNull(program.AppClass);
    }
}
