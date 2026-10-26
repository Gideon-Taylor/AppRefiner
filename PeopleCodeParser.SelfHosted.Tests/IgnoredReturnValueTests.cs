using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Which ignored return values are errors, matching App Designer's compiler (checked 2026-10):
/// user-written functions and typed builtin methods not marked optional, but never API object
/// methods, which are late-bound.
/// </summary>
public class IgnoredReturnValueTests
{
    private const string Setup =
        "Function UserFn() Returns number\n   Return 1;\nEnd-Function;\n" +
        "Local ApiObject &ci = %Session.GetCompIntfc(CompIntfc.USER_PROFILE);\n" +
        "Local ApiObject &s = %Session;\n" +
        "Local Rowset &rs = CreateRowset(Record.PSOPRDEFN);\n";

    private static List<string> Errors(string statement)
    {
        var (program, parseErrors) = Parse(Setup + statement);
        Assert.Empty(parseErrors);

        TypeInferenceVisitor.Run(program, TypeMetadataBuilder.ExtractMetadata(program), NullTypeMetadataResolver.Instance);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);
        return program.GetAllTypeErrors().Select(e => e.Message).ToList();
    }

    [Theory]
    [InlineData("&ci.Cancel();")]
    [InlineData("&ci.Save();")]
    [InlineData("%Session.GetCompIntfc(CompIntfc.USER_PROFILE);")]
    [InlineData("&s.GetQuery();")]
    [InlineData("&rs.Fill();")]
    public void Ignored_return_is_allowed(string statement)
    {
        Assert.Empty(Errors(statement));
    }

    [Theory]
    [InlineData("&rs.GetRow(1);", "GetRow")]
    [InlineData("UserFn();", "UserFn")]
    public void Ignored_return_is_an_error(string statement, string function)
    {
        var error = Assert.Single(Errors(statement));
        Assert.StartsWith($"Return values must assigned to a variable. Function {function}()", error);
    }
}
