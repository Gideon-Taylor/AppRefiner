using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Array properties are typed from the Array builtin, and array method arguments typed
/// <c>$element</c> are checked against the receiver's element type.
/// </summary>
public class ArrayMemberTypeTests
{
    private static (ProgramNode Program, List<TypeError> Errors) TypeCheck(string source)
    {
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);

        return (program, program.GetAllTypeErrors().ToList());
    }

    private static TypeInfo? MemberType(ProgramNode program, string memberName) =>
        program.FindDescendants<MemberAccessNode>().Single(m => m.MemberName == memberName).GetInferredType();

    [Theory]
    [InlineData("Len")]
    [InlineData("Dimension")]
    public void Array_property_is_number(string property)
    {
        var (program, errors) = TypeCheck($"Local array of string &a;\nLocal number &n = &a.{property};");

        Assert.Empty(errors);
        Assert.Equal(PeopleCodeType.Number, MemberType(program, property)?.PeopleCodeType);
    }

    [Fact]
    public void Push_of_wrong_element_type_is_error()
    {
        var (program, errors) = TypeCheck("Local array of string &names;\n&names.Push(\"a\", 42);");

        var error = Assert.Single(errors);
        var literal = program.FindDescendants<LiteralNode>().Single(l => l.Value is not string);
        Assert.Same(literal, error.Node);
    }

    [Theory]
    [InlineData("Local array of string &a;\nLocal string &s;\n&a.Push(\"x\", &s);")]
    [InlineData("Local array of number &a;\nLocal integer &i;\n&a.Push(&i, 3);")]
    [InlineData("Local array &a;\n&a.Push(1, \"x\");")]
    [InlineData("Local array of any &a;\n&a.Push(1, \"x\");")]
    [InlineData("Local array of string &a;\n&a.Unshift(\"x\");\n&a.Substitute(\"x\", \"y\");\nLocal number &i = &a.Find(\"y\");")]
    // Multi-dimensional receivers aren't checked: App Designer's rules for pushing into them aren't pinned down
    [InlineData("Local array of array of string &a;\n&a.Push(\"x\");\n&a.Push(CreateArray(\"y\"));")]
    public void Matching_or_unchecked_elements_are_accepted(string source)
    {
        var (_, errors) = TypeCheck(source);
        Assert.Empty(errors);
    }

    [Fact]
    public void Substitute_of_wrong_element_type_is_error()
    {
        var (_, errors) = TypeCheck("Local array of number &a;\n&a.Substitute(1, \"two\");");
        Assert.Single(errors);
    }
}
