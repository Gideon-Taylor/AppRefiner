using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Function parameters cannot be marked OUT — they are always passed by reference
/// already. App class Method parameters and DLL REF parameters are unaffected.
/// </summary>
public class FunctionOutParameterTests
{
    [Fact]
    public void FunctionWithOutParameter_ReportsParseError()
    {
        var (program, errors) = Parse("Function Foo(&a As number, &b As string Out) End-Function;");

        Assert.Contains(errors, e => e.Message.Contains("OUT"));
        var function = program.Functions.Single();
        Assert.True(function.Parameters[1].IsOut);
    }

    [Fact]
    public void FunctionWithoutOutParameter_IsNotAnError()
    {
        var (_, errors) = Parse("Function Foo(&a As number, &b As string) End-Function;");

        Assert.Empty(errors);
    }

    [Fact]
    public void MethodWithOutParameter_IsStillNotAnError()
    {
        var source = """
            class TestClass
               method Bar(&a As number, &b As string Out);
            end-class;
            """;
        var (_, errors) = Parse(source);

        Assert.Empty(errors);
    }

    [Fact]
    public void DeclareFunctionPeopleCode_WithParameterList_ReportsParseError()
    {
        var (_, errors) = Parse("Declare Function Foo PeopleCode REC.FLD FieldFormula(&a As number);");

        Assert.Contains(errors, e => e.Message.Contains("parameter list"));
    }

    [Fact]
    public void DeclareFunctionPeopleCode_WithoutParameterList_IsNotAnError()
    {
        var (_, errors) = Parse("Declare Function Foo PeopleCode REC.FLD FieldFormula;");

        Assert.Empty(errors);
    }

    [Fact]
    public void DeclareFunctionLibrary_WithRefParameter_IsNotAnError()
    {
        var (_, errors) = Parse("""Declare Function Foo Library "mylib.dll" (arg1 Ref As number);""");

        Assert.Empty(errors);
    }
}
