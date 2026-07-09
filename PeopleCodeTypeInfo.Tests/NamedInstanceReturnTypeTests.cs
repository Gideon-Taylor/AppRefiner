using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// CreateRecord / GetRecord / GetField should return named RecordTypeInfo / FieldTypeInfo
/// when given definition references, and that identity should flow into variables.
/// </summary>
public class NamedInstanceReturnTypeTests
{
    private static (ProgramNode program, TypeInferenceVisitor visitor) Infer(
        string source, string? defaultRecordName = null)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();
        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");
        var visitor = TypeInferenceVisitor.Run(
            program, metadata, NullTypeMetadataResolver.Instance, defaultRecordName);
        return (program, visitor);
    }

    private static FunctionCallNode FindCall(ProgramNode program, string functionName)
    {
        var found = program.FindDescendants<FunctionCallNode>()
            .FirstOrDefault(fc =>
                fc.Function is IdentifierNode id
                && id.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(found);
        return found!;
    }

    private static FunctionCallNode FindMethodCall(ProgramNode program, string methodName)
    {
        var found = program.FindDescendants<FunctionCallNode>()
            .FirstOrDefault(fc =>
                fc.Function is MemberAccessNode ma
                && ma.MemberName.Equals(methodName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(found);
        return found!;
    }

    [Fact]
    public void CreateRecord_WithRecordReference_ReturnsNamedRecord()
    {
        var source = @"
function test();
   Local Record &r = CreateRecord(Record.MY_REC);
end-function;
";
        var (program, visitor) = Infer(source);

        var call = FindCall(program, "CreateRecord");
        var callType = Assert.IsType<RecordTypeInfo>(visitor.GetInferredType(call));
        Assert.Equal("MY_REC", callType.RecordName);
    }

    [Fact]
    public void CreateRecord_FlowsNameIntoDeclaredVariable()
    {
        var source = @"
function test();
   Local Record &r = CreateRecord(Record.MY_REC);
   Local any &x = &r;
end-function;
";
        var (program, visitor) = Infer(source);

        var assignments = program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>().ToList();
        Assert.Equal(2, assignments.Count);

        // Second initializer is &r
        var rIdent = Assert.IsType<IdentifierNode>(assignments[1].InitialValue);
        Assert.Equal("&r", rIdent.Name);

        var rType = Assert.IsType<RecordTypeInfo>(visitor.GetInferredType(rIdent));
        Assert.Equal("MY_REC", rType.RecordName);
    }

    [Fact]
    public void NamedRecordVariable_MemberAccess_IsNamedField()
    {
        var source = @"
function test();
   Local Record &r = CreateRecord(Record.MY_REC);
   Local any &f = &r.MY_FIELD;
end-function;
";
        var (program, visitor) = Infer(source);

        var fieldAccess = program.FindDescendants<MemberAccessNode>()
            .First(m => m.MemberName.Equals("MY_FIELD", StringComparison.OrdinalIgnoreCase));

        var fieldType = Assert.IsType<FieldTypeInfo>(visitor.GetInferredType(fieldAccess));
        Assert.Equal("MY_REC", fieldType.RecordName);
        Assert.Equal("MY_FIELD", fieldType.FieldName);
    }

    [Fact]
    public void GetRecord_WithRecordReference_ReturnsNamedRecord()
    {
        var source = @"
function test();
   Local Record &r = GetRecord(Record.PSOPRDEFN);
end-function;
";
        var (program, visitor) = Infer(source);

        var call = FindCall(program, "GetRecord");
        var callType = Assert.IsType<RecordTypeInfo>(visitor.GetInferredType(call));
        Assert.Equal("PSOPRDEFN", callType.RecordName);
    }

    [Fact]
    public void GetRecord_NoArgs_InRecordPeopleCode_UsesDefaultRecord()
    {
        var source = @"
function test();
   Local Record &r = GetRecord();
end-function;
";
        var (program, visitor) = Infer(source, defaultRecordName: "WEBLIB_TS_TEST");

        var call = FindCall(program, "GetRecord");
        var callType = Assert.IsType<RecordTypeInfo>(visitor.GetInferredType(call));
        Assert.Equal("WEBLIB_TS_TEST", callType.RecordName);
    }

    [Fact]
    public void GetField_Global_WithFieldReference_ReturnsNamedField()
    {
        // Standalone GetField(Field.X) — field name known; record from default if present
        var source = @"
function test();
   Local Field &f = GetField(Field.OPRID);
end-function;
";
        var (program, visitor) = Infer(source, defaultRecordName: "PSOPRDEFN");

        var call = FindCall(program, "GetField");
        var callType = Assert.IsType<FieldTypeInfo>(visitor.GetInferredType(call));
        Assert.Equal("OPRID", callType.FieldName);
        Assert.Equal("PSOPRDEFN", callType.RecordName);
    }

    [Fact]
    public void RecordGetField_UsesReceiverRecordName()
    {
        var source = @"
function test();
   Local Record &r = CreateRecord(Record.MY_REC);
   Local Field &f = &r.GetField(Field.MY_FIELD);
end-function;
";
        var (program, visitor) = Infer(source);

        var call = FindMethodCall(program, "GetField");
        var callType = Assert.IsType<FieldTypeInfo>(visitor.GetInferredType(call));
        Assert.Equal("MY_REC", callType.RecordName);
        Assert.Equal("MY_FIELD", callType.FieldName);
    }

    [Fact]
    public void Assignment_RefinesExistingRecordVariable()
    {
        var source = @"
function test();
   Local Record &r;
   &r = CreateRecord(Record.OTHER_REC);
   Local any &x = &r;
end-function;
";
        var (program, visitor) = Infer(source);

        var xInit = program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>()
            .First(d => d.VariableNameInfo.Name.Equals("&x", StringComparison.OrdinalIgnoreCase));
        var rIdent = Assert.IsType<IdentifierNode>(xInit.InitialValue);

        var rType = Assert.IsType<RecordTypeInfo>(visitor.GetInferredType(rIdent));
        Assert.Equal("OTHER_REC", rType.RecordName);
    }

    [Fact]
    public void CreateRecord_WithoutReference_StaysGenericRecord()
    {
        // Dynamic reference — no static name
        var source = @"
function test();
   Local string &name;
   Local Record &r = CreateRecord(@(""RECORD."" | &name));
end-function;
";
        var (program, visitor) = Infer(source);

        var call = FindCall(program, "CreateRecord");
        var callType = visitor.GetInferredType(call);
        Assert.NotNull(callType);
        Assert.Equal(PeopleCodeType.Record, callType.PeopleCodeType);
        // Should not invent a RecordTypeInfo with a fake name
        Assert.IsNotType<RecordTypeInfo>(callType);
    }

    [Fact]
    public void CreateRecord_AtStringLiteral_ReturnsNamedRecord()
    {
        // CreateRecord(@("Record.MY_REC")) — pure string after @ is statically resolvable
        var source = @"
function test();
   Local Record &r = CreateRecord(@(""Record.MY_REC""));
end-function;
";
        var (program, visitor) = Infer(source);

        var call = FindCall(program, "CreateRecord");
        var callType = Assert.IsType<RecordTypeInfo>(visitor.GetInferredType(call));
        Assert.Equal("MY_REC", callType.RecordName);

        // @("Record.MY_REC") itself should be a typed @RECORD reference
        var atExpr = program.FindDescendants<UnaryOperationNode>()
            .First(u => u.Operator == PeopleCodeParser.SelfHosted.Nodes.UnaryOperator.Reference);
        var refType = Assert.IsType<ReferenceTypeInfo>(visitor.GetInferredType(atExpr));
        Assert.Equal(PeopleCodeType.Record, refType.ReferenceCategory);
        Assert.Equal("MY_REC", refType.ReferencedName);
    }

    [Fact]
    public void GetField_StringLiteral_ReturnsNamedField()
    {
        var source = @"
function test();
   Local Field &f = GetField(""OPRID"");
end-function;
";
        var (program, visitor) = Infer(source, defaultRecordName: "PSOPRDEFN");

        var call = FindCall(program, "GetField");
        var callType = Assert.IsType<FieldTypeInfo>(visitor.GetInferredType(call));
        Assert.Equal("OPRID", callType.FieldName);
        Assert.Equal("PSOPRDEFN", callType.RecordName);
    }

    [Fact]
    public void GetField_AtFieldStringLiteral_ReturnsNamedField()
    {
        var source = @"
function test();
   Local Field &f = GetField(@(""Field.OPRID""));
end-function;
";
        var (program, visitor) = Infer(source, defaultRecordName: "PSOPRDEFN");

        var call = FindCall(program, "GetField");
        var callType = Assert.IsType<FieldTypeInfo>(visitor.GetInferredType(call));
        Assert.Equal("OPRID", callType.FieldName);
        Assert.Equal("PSOPRDEFN", callType.RecordName);
    }

    [Fact]
    public void RecordGetField_StringLiteral_UsesReceiverRecordName()
    {
        var source = @"
function test();
   Local Record &r = CreateRecord(Record.MY_REC);
   Local Field &f = &r.GetField(""MY_FIELD"");
end-function;
";
        var (program, visitor) = Infer(source);

        var call = FindMethodCall(program, "GetField");
        var callType = Assert.IsType<FieldTypeInfo>(visitor.GetInferredType(call));
        Assert.Equal("MY_REC", callType.RecordName);
        Assert.Equal("MY_FIELD", callType.FieldName);
    }

    [Fact]
    public void AtString_WithConcatenation_StaysDynamicReference()
    {
        var source = @"
function test();
   Local string &suffix;
   Local any &x = @(""Record."" | &suffix);
end-function;
";
        var (program, visitor) = Infer(source);

        var atExpr = program.FindDescendants<UnaryOperationNode>()
            .First(u => u.Operator == PeopleCodeParser.SelfHosted.Nodes.UnaryOperator.Reference);
        var refType = Assert.IsType<ReferenceTypeInfo>(visitor.GetInferredType(atExpr));
        Assert.Equal(PeopleCodeType.Any, refType.ReferenceCategory);
    }
}
