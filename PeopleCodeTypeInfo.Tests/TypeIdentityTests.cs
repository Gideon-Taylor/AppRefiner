using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// TypeIdentity comparisons and how identity travels on TypeInfo.
/// </summary>
public class TypeIdentityTests
{
    [Fact]
    public void Refinement_adds_detail()
    {
        Assert.True(new TypeIdentity(RecordName: "JOB", FieldName: "EMPLID").IsRefinementOf(new TypeIdentity(RecordName: "JOB")));
        Assert.False(new TypeIdentity(RecordName: "JOB").IsRefinementOf(new TypeIdentity(RecordName: "JOB", FieldName: "EMPLID")));
        Assert.False(new TypeIdentity(RecordName: "JOB").IsRefinementOf(new TypeIdentity(RecordName: "PERSON")));
    }

    [Fact]
    public void Null_or_empty_less_is_always_refined()
    {
        var x = new TypeIdentity(CompIntfcName: "FOO");
        Assert.True(x.IsRefinementOf(null));
        Assert.True(x.IsRefinementOf(new TypeIdentity()));
        Assert.True(x.IsRefinementOf(new TypeIdentity(RecordName: "")));
        Assert.True(new TypeIdentity(RecordName: "").IsEmpty);
        Assert.True(new TypeIdentity().IsEmpty);
        Assert.False(x.IsEmpty);
    }

    [Fact]
    public void Different_names_conflict()
    {
        Assert.True(new TypeIdentity(CompIntfcName: "A").ConflictsWith(new TypeIdentity(CompIntfcName: "b")));
        Assert.False(new TypeIdentity(CompIntfcName: "a").ConflictsWith(new TypeIdentity(CompIntfcName: "A")));
        Assert.False(new TypeIdentity(CompIntfcName: "A").ConflictsWith(null));
        Assert.False(new TypeIdentity(CompIntfcName: "A").ConflictsWith(new TypeIdentity(CompIntfcName: "")));
    }

    [Fact]
    public void Disjoint_names_do_not_conflict()
    {
        Assert.False(new TypeIdentity(FieldName: "X").ConflictsWith(new TypeIdentity(RecordName: "R")));
    }

    [Fact]
    public void Identity_survives_With_methods()
    {
        var foo = new TypeIdentity(CompIntfcName: "FOO");
        var t = TypeInfo.FromPeopleCodeType(PeopleCodeType.ComponentInterface).WithIdentity(foo);

        Assert.Equal(foo, t.Identity);
        Assert.Equal(foo, t.WithAssignable(true).Identity);
        Assert.Equal(foo, t.WithAutoDeclared(true).Identity);
        Assert.Equal(foo, t.WithBufferField(new BufferFieldReference("JOB", "EMPLID")).Identity);
        Assert.Same(t, t.WithIdentity(new TypeIdentity(CompIntfcName: "FOO")));
    }

    [Fact]
    public void Identity_does_not_affect_Equals()
    {
        var ci = TypeInfo.FromPeopleCodeType(PeopleCodeType.ComponentInterface);
        Assert.True(ci.WithIdentity(new TypeIdentity(CompIntfcName: "FOO"))
            .Equals(ci.WithIdentity(new TypeIdentity(CompIntfcName: "BAR"))));
    }

    [Fact]
    public void Record_and_field_expose_effective_identity()
    {
        Assert.Equal(new TypeIdentity(RecordName: "JOB"), new RecordTypeInfo("JOB", null).EffectiveIdentity);
        Assert.Equal(new TypeIdentity(RecordName: "JOB", FieldName: "EMPLID"), new FieldTypeInfo("JOB", "EMPLID", null).EffectiveIdentity);

        var ci = TypeInfo.FromPeopleCodeType(PeopleCodeType.ComponentInterface).WithIdentity(new TypeIdentity(CompIntfcName: "FOO"));
        Assert.Equal(ci.Identity, ci.EffectiveIdentity);
    }
}
