using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Record/Field return naming (record(X) / field(R.F)) is guessed from arguments and the
/// default record only for builtin functions; user functions keep their declared bare type.
/// </summary>
public class NamedInstanceReturnTests
{
    private static TypeInfo? InferLastLocalInitializer(string source, string? defaultRecordName = null)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance, defaultRecordName);

        var decl = program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>().Last();
        return decl.InitialValue.GetInferredType();
    }

    [Fact]
    public void CreateRecord_with_record_reference_is_named()
    {
        var type = InferLastLocalInitializer("Local Record &r = CreateRecord(Record.JOB);");

        var record = Assert.IsType<RecordTypeInfo>(type);
        Assert.Equal("JOB", record.RecordName);
    }

    [Fact]
    public void Builtin_GetRecord_with_no_args_in_record_peoplecode_uses_default_record()
    {
        var type = InferLastLocalInitializer("Local Record &r = GetRecord();", "MY_REC");

        var record = Assert.IsType<RecordTypeInfo>(type);
        Assert.Equal("MY_REC", record.RecordName);
    }

    [Fact]
    public void Builtin_GetField_with_string_literal_is_named()
    {
        var type = InferLastLocalInitializer("Local Field &f = GetField(\"OPRID\");");

        var field = Assert.IsType<FieldTypeInfo>(type);
        Assert.Equal("OPRID", field.FieldName);
    }

    [Fact]
    public void Record_GetField_with_field_reference_keeps_receiver_record()
    {
        var type = InferLastLocalInitializer(
            "Local Record &r = CreateRecord(Record.JOB); Local Field &f = &r.GetField(Field.EMPLID);");

        var field = Assert.IsType<FieldTypeInfo>(type);
        Assert.Equal("JOB", field.RecordName);
        Assert.Equal("EMPLID", field.FieldName);
    }

    [Fact]
    public void Zero_arg_user_function_returning_record_is_not_named_after_default_record()
    {
        var type = InferLastLocalInitializer(
            "Function GetIt() Returns Record\n   Return CreateRecord(Record.JOB);\nEnd-Function;\nLocal Record &r = GetIt();",
            "MY_REC");

        Assert.NotNull(type);
        Assert.IsNotType<RecordTypeInfo>(type);
        Assert.Equal(PeopleCodeType.Record, type.PeopleCodeType);
    }

    [Fact]
    public void User_function_with_record_argument_is_not_named_after_argument()
    {
        var type = InferLastLocalInitializer(
            "Function CopyTo(&rec As Record) Returns Record\n   Return &rec;\nEnd-Function;\nLocal Record &r = CopyTo(Record.FOO);");

        Assert.NotNull(type);
        Assert.IsNotType<RecordTypeInfo>(type);
    }

    [Fact]
    public void User_function_returning_field_with_string_argument_is_not_named()
    {
        var type = InferLastLocalInitializer(
            "Function MyFunc(&s As string) Returns Field\n   Return GetField(\"X\");\nEnd-Function;\nLocal Field &f = MyFunc(\"hello\");");

        Assert.NotNull(type);
        Assert.IsNotType<FieldTypeInfo>(type);
        Assert.Equal(PeopleCodeType.Field, type.PeopleCodeType);
    }
}
