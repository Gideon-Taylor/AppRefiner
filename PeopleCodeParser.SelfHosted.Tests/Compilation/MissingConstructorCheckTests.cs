using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class MissingConstructorCheckTests
{
    private const string SubWithoutConstructor = @"
import PKG:Base;

class Sub extends PKG:Base
   method DoWork();
end-class;

method DoWork
end-method;
";

    private const string SubWithOwnConstructor = @"
import PKG:Base;

class Sub extends PKG:Base
   method Sub();
end-class;

method Sub
end-method;
";

    /// <summary>
    /// Base constructor with one required parameter (an explicit, parameterized
    /// constructor in the base class source).
    /// </summary>
    private static FunctionInfo ParameterizedConstructor() => new()
    {
        Name = "Base",
        ParameterOverloads = new() { new() { new SingleParameter(PeopleCodeType.String, 0, "&arg1") } },
        ReturnType = new TypeWithDimensionality(PeopleCodeType.AppClass, 0, "PKG:Base"),
    };

    /// <summary>
    /// Zero-parameter constructor, shaped exactly like the synthetic default that
    /// TypeMetadataBuilder emits when a class has no explicit constructor.
    /// </summary>
    private static FunctionInfo DefaultConstructor() => new()
    {
        Name = "Base",
        ParameterOverloads = new() { new List<Parameter>() },
        ReturnType = new TypeWithDimensionality(PeopleCodeType.Void, 0),
    };

    private static FakeTypeMetadataResolver ResolverWithBase(FunctionInfo constructor)
    {
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Base", new TypeMetadata
        {
            QualifiedName = "PKG:Base",
            Name = "Base",
            Kind = ProgramKind.AppClass,
            Constructor = constructor,
        });
        return fake;
    }

    [Fact]
    public void Reports_when_subclass_lacks_constructor_and_base_has_parameterized_one()
    {
        var ctor = ParameterizedConstructor();
        // Sanity-check the semantics the check relies on: Parameters is a view over
        // ParameterOverloads[0], so a one-parameter overload yields Count == 1.
        Assert.Single(ctor.Parameters);

        var (program, errors) = ParseTestHelper.Parse(SubWithoutConstructor);
        var diags = CompileChecker.Check(program, errors, ResolverWithBase(ctor),
            new CompileCheckContextInput(null));

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.MissingConstructor &&
            d.Severity == DiagnosticSeverity.Error &&
            d.Message.Contains("Sub") &&
            d.Message.Contains("PKG:Base"));
    }

    [Fact]
    public void Does_not_report_when_base_constructor_is_parameterless()
    {
        var ctor = DefaultConstructor();
        // The synthetic default has an empty first overload → Parameters.Count == 0.
        Assert.Empty(ctor.Parameters);

        var (program, errors) = ParseTestHelper.Parse(SubWithoutConstructor);
        var diags = CompileChecker.Check(program, errors, ResolverWithBase(ctor),
            new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingConstructor);
    }

    [Fact]
    public void Does_not_report_when_subclass_defines_own_constructor()
    {
        var (program, errors) = ParseTestHelper.Parse(SubWithOwnConstructor);
        var diags = CompileChecker.Check(program, errors, ResolverWithBase(ParameterizedConstructor()),
            new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingConstructor);
    }

    [Fact]
    public void Does_not_report_without_resolver()
    {
        var (program, errors) = ParseTestHelper.Parse(SubWithoutConstructor);
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingConstructor);
    }

    [Fact]
    public void Does_not_report_for_class_without_base_type()
    {
        const string src = @"
class Standalone
   method DoWork();
end-class;

method DoWork
end-method;
";
        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, ResolverWithBase(ParameterizedConstructor()),
            new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingConstructor);
    }
}
