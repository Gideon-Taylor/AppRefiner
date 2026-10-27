using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Function headers accept an optional NoExport marker after RETURNS and before DOC.
/// It is matched contextually, so identifiers named NoExport keep working.
/// </summary>
public class FunctionNoExportTests
{
    [Fact]
    public void NoExport_is_recorded()
    {
        var (program, errors) = Parse("Function Foo() NoExport\nEnd-Function;");

        Assert.Empty(errors);
        Assert.True(program.Functions.Single().IsNoExport);
    }

    [Fact]
    public void NoExport_after_returns_and_before_doc()
    {
        var (program, errors) = Parse("Function Foo() Returns string NoExport Doc \"d\"\n   Return \"x\";\nEnd-Function;");

        Assert.Empty(errors);
        var function = program.Functions.Single();
        Assert.True(function.IsNoExport);
        var returnType = Assert.IsType<BuiltInTypeNode>(function.ReturnType);
        Assert.Equal(PeopleCodeType.String, returnType.Type);
        Assert.Equal("d", function.Documentation);
    }

    [Fact]
    public void Function_without_NoExport_is_exported()
    {
        var (program, errors) = Parse("Function Foo()\nEnd-Function;");

        Assert.Empty(errors);
        Assert.False(program.Functions.Single().IsNoExport);
    }

    [Fact]
    public void NoExport_named_variable_still_parses()
    {
        var (_, variableErrors) = Parse("Local string &NoExport;\n&NoExport = \"x\";");
        Assert.Empty(variableErrors);

        var (_, functionErrors) = Parse("Function NoExport()\nEnd-Function;");
        Assert.Empty(functionErrors);
    }
}
