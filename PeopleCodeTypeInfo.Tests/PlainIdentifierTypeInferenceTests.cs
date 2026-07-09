using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Tests for plain identifier type inference under compiler-aligned rules:
/// - NAME.x  → NAME is always a record
/// - NAME alone + default record → FieldTypeInfo(default, NAME)
/// - NAME alone without default → Unknown
/// </summary>
public class PlainIdentifierTypeInferenceTests
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

    /// <summary>
    /// Bare identifier outside record PeopleCode is not a valid field/record by itself.
    /// </summary>
    [Fact]
    public void PlainIdentifier_WithoutDefaultRecord_IsUnknown()
    {
        var source = @"
function test();
   local any &result;
   &result = A;
end-function;
";
        var (program, visitor) = Infer(source);

        var assignment = FindFirstAssignment(program.Functions[0]);
        Assert.NotNull(assignment);
        var identifier = Assert.IsType<IdentifierNode>(assignment.Value);
        Assert.Equal("A", identifier.Name);

        var inferredType = visitor.GetInferredType(identifier);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Unknown, inferredType.Kind);
    }

    /// <summary>
    /// Bare identifier in record PeopleCode is a field on the default record.
    /// </summary>
    [Fact]
    public void PlainIdentifier_WithDefaultRecord_IsFieldOnDefault()
    {
        var source = @"
function test();
   local any &result;
   &result = START_DT;
end-function;
";
        var (program, visitor) = Infer(source, defaultRecordName: "AAP_YEAR");

        var assignment = FindFirstAssignment(program.Functions[0]);
        Assert.NotNull(assignment);
        var identifier = Assert.IsType<IdentifierNode>(assignment.Value);

        var inferredType = visitor.GetInferredType(identifier);
        Assert.IsType<FieldTypeInfo>(inferredType);
        var fieldType = (FieldTypeInfo)inferredType!;
        Assert.Equal("AAP_YEAR", fieldType.RecordName);
        Assert.Equal("START_DT", fieldType.FieldName);
    }

    /// <summary>
    /// RECORD.FIELD works without a default record (qualified form).
    /// </summary>
    [Fact]
    public void QualifiedFieldAccess_ShouldHaveRecordName()
    {
        var source = @"
function test();
   local any &result;
   &result = MY_RECORD.MY_FIELD;
end-function;
";
        var (program, visitor) = Infer(source);

        var assignment = FindFirstAssignment(program.Functions[0]);
        Assert.NotNull(assignment);
        var memberAccess = Assert.IsType<MemberAccessNode>(assignment.Value);

        // Left side is a record
        var leftType = visitor.GetInferredType(memberAccess.Target);
        Assert.IsType<RecordTypeInfo>(leftType);
        Assert.Equal("MY_RECORD", ((RecordTypeInfo)leftType!).RecordName);
        Assert.True(((RecordTypeInfo)leftType).DirectRecordAccess);

        // Whole access is a field
        var inferredType = visitor.GetInferredType(memberAccess);
        Assert.IsType<FieldTypeInfo>(inferredType);
        var fieldType = (FieldTypeInfo)inferredType!;
        Assert.Equal("MY_RECORD", fieldType.RecordName);
        Assert.Equal("MY_FIELD", fieldType.FieldName);
    }

    /// <summary>
    /// Under default record, NAME left of a dot is still a record (not a field on default).
    /// This is the WEBLIB_TS_TEST.ISCRIPT1 case.
    /// </summary>
    [Fact]
    public void QualifiedFieldAccess_WithDefaultRecord_LeftSideIsRecordNotField()
    {
        var source = @"
function test();
   local any &result;
   &result = WEBLIB_TS_TEST.ISCRIPT1;
end-function;
";
        var (program, visitor) = Infer(source, defaultRecordName: "WEBLIB_TS_TEST");

        var assignment = FindFirstAssignment(program.Functions[0]);
        Assert.NotNull(assignment);
        var memberAccess = Assert.IsType<MemberAccessNode>(assignment.Value);

        var leftType = visitor.GetInferredType(memberAccess.Target);
        Assert.IsType<RecordTypeInfo>(leftType);
        Assert.Equal("WEBLIB_TS_TEST", ((RecordTypeInfo)leftType!).RecordName);

        var fieldType = Assert.IsType<FieldTypeInfo>(visitor.GetInferredType(memberAccess));
        Assert.Equal("WEBLIB_TS_TEST", fieldType.RecordName);
        Assert.Equal("ISCRIPT1", fieldType.FieldName);
    }

    /// <summary>
    /// Multiple bare fields under default record are all fields on that record.
    /// </summary>
    [Fact]
    public void MultiplePlainIdentifiers_WithDefaultRecord_AreAllFields()
    {
        var source = @"
function test();
   local any &result;
   &result = START_DT;
   &result = END_DT;
   &result = AMOUNT;
end-function;
";
        var (program, visitor) = Infer(source, defaultRecordName: "AAP_YEAR");

        var assignments = FindAllAssignments(program.Functions[0]);
        Assert.Equal(3, assignments.Count);

        var expectedFieldNames = new[] { "START_DT", "END_DT", "AMOUNT" };
        for (int i = 0; i < 3; i++)
        {
            var identifier = Assert.IsType<IdentifierNode>(assignments[i].Value);
            var fieldType = Assert.IsType<FieldTypeInfo>(visitor.GetInferredType(identifier));
            Assert.Equal("AAP_YEAR", fieldType.RecordName);
            Assert.Equal(expectedFieldNames[i], fieldType.FieldName);
        }
    }

    /// <summary>
    /// Variables with & prefix should not be affected.
    /// </summary>
    [Fact]
    public void LocalVariable_ShouldNotBeField()
    {
        var source = @"
function test();
   local string &myVar;
   local any &result;
   &result = &myVar;
end-function;
";
        var (program, visitor) = Infer(source);

        var assignment = FindFirstAssignment(program.Functions[0]);
        Assert.NotNull(assignment);
        var identifier = Assert.IsType<IdentifierNode>(assignment.Value);
        Assert.Equal("&myVar", identifier.Name);

        var inferredType = visitor.GetInferredType(identifier);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.String, inferredType.PeopleCodeType);
        Assert.IsNotType<FieldTypeInfo>(inferredType);
    }

    /// <summary>
    /// Record.REC alone is a definition reference (@RECORD).
    /// </summary>
    [Fact]
    public void RecordKeyword_Alone_IsReference()
    {
        var source = @"
function test();
   local any &result;
   &result = Record.MY_REC;
end-function;
";
        var (program, visitor) = Infer(source);

        var assignment = FindFirstAssignment(program.Functions[0]);
        Assert.NotNull(assignment);
        var memberAccess = Assert.IsType<MemberAccessNode>(assignment.Value);

        var inferredType = visitor.GetInferredType(memberAccess);
        Assert.IsType<ReferenceTypeInfo>(inferredType);
        var refType = (ReferenceTypeInfo)inferredType!;
        Assert.Equal(PeopleCodeType.Record, refType.ReferenceCategory);
        Assert.Equal("MY_REC", refType.ReferencedName);
    }

    /// <summary>
    /// Record.REC.IsChanged promotes @RECORD to a record instance and uses the property.
    /// </summary>
    [Fact]
    public void RecordKeyword_IsChanged_IsRecordProperty()
    {
        var source = @"
function test();
   local boolean &changed;
   &changed = Record.MY_REC.IsChanged;
end-function;
";
        var (program, visitor) = Infer(source);

        var assignment = FindFirstAssignment(program.Functions[0]);
        Assert.NotNull(assignment);
        // Record.MY_REC.IsChanged → outer member access is IsChanged
        var isChangedAccess = Assert.IsType<MemberAccessNode>(assignment.Value);
        Assert.Equal("IsChanged", isChangedAccess.MemberName);

        var inferredType = visitor.GetInferredType(isChangedAccess);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Boolean, inferredType.PeopleCodeType);
        Assert.IsNotType<FieldTypeInfo>(inferredType);

        // Middle (Record.MY_REC) should have been re-stamped as record instance
        var midType = visitor.GetInferredType(isChangedAccess.Target);
        Assert.IsType<RecordTypeInfo>(midType);
        Assert.Equal("MY_REC", ((RecordTypeInfo)midType!).RecordName);
        Assert.False(((RecordTypeInfo)midType).DirectRecordAccess);
    }

    /// <summary>
    /// Record.REC.FIELD promotes to a field instance.
    /// </summary>
    [Fact]
    public void RecordKeyword_FieldMember_IsFieldTypeInfo()
    {
        var source = @"
function test();
   local any &result;
   &result = Record.MY_REC.MY_FIELD;
end-function;
";
        var (program, visitor) = Infer(source);

        var assignment = FindFirstAssignment(program.Functions[0]);
        Assert.NotNull(assignment);
        var fieldAccess = Assert.IsType<MemberAccessNode>(assignment.Value);
        Assert.Equal("MY_FIELD", fieldAccess.MemberName);

        var fieldType = Assert.IsType<FieldTypeInfo>(visitor.GetInferredType(fieldAccess));
        Assert.Equal("MY_REC", fieldType.RecordName);
        Assert.Equal("MY_FIELD", fieldType.FieldName);
    }

    /// <summary>
    /// Field.FLD alone is a definition reference (@FIELD).
    /// </summary>
    [Fact]
    public void FieldKeyword_Alone_IsReference()
    {
        var source = @"
function test();
   local any &result;
   &result = Field.MY_FIELD;
end-function;
";
        var (program, visitor) = Infer(source);

        var assignment = FindFirstAssignment(program.Functions[0]);
        Assert.NotNull(assignment);
        var memberAccess = Assert.IsType<MemberAccessNode>(assignment.Value);

        var inferredType = visitor.GetInferredType(memberAccess);
        Assert.IsType<ReferenceTypeInfo>(inferredType);
        var refType = (ReferenceTypeInfo)inferredType!;
        Assert.Equal(PeopleCodeType.Field, refType.ReferenceCategory);
        Assert.Equal("MY_FIELD", refType.ReferencedName);
    }

    /// <summary>
    /// Field.FLD.Value promotes to a field instance using the default record.
    /// </summary>
    [Fact]
    public void FieldKeyword_Value_UsesDefaultRecord()
    {
        var source = @"
function test();
   local any &result;
   &result = Field.MY_FIELD.Value;
end-function;
";
        var (program, visitor) = Infer(source, defaultRecordName: "AAP_YEAR");

        var assignment = FindFirstAssignment(program.Functions[0]);
        Assert.NotNull(assignment);
        var valueAccess = Assert.IsType<MemberAccessNode>(assignment.Value);
        Assert.Equal("Value", valueAccess.MemberName);

        // Target of .Value should be FieldTypeInfo(AAP_YEAR, MY_FIELD)
        var midType = visitor.GetInferredType(valueAccess.Target);
        Assert.IsType<FieldTypeInfo>(midType);
        var fieldType = (FieldTypeInfo)midType!;
        Assert.Equal("AAP_YEAR", fieldType.RecordName);
        Assert.Equal("MY_FIELD", fieldType.FieldName);
    }

    // Helper method to find first assignment in a function
    private static AssignmentNode FindFirstAssignment(FunctionNode function)
    {
        var collector = new AssignmentCollector();
        function.Accept(collector);
        return collector.Assignments.FirstOrDefault();
    }

    // Helper method to find all assignments in a function
    private static List<AssignmentNode> FindAllAssignments(FunctionNode function)
    {
        var collector = new AssignmentCollector();
        function.Accept(collector);
        return collector.Assignments;
    }

    /// <summary>
    /// Test: Multi-variable instance declarations should properly register all variables.
    /// Bug: Previously only the first variable in "instance Rowset &a, &b, &c;" was registered.
    /// </summary>
    [Fact]
    public void MultiVariableInstanceDeclaration_ShouldRegisterAllVariables()
    {
        var source = @"
class LoadDistribs
   method LoadDistribs();
private
   instance Rowset &SPH_RST, &SPD_RST, &rowset_distrib;
end-class;

method LoadDistribs
   %This.rowset_distrib.Flush();
end-method;
";

        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "LoadDistribs");

        Assert.Equal(3, metadata.InstanceVariables.Count);
        Assert.True(metadata.InstanceVariables.ContainsKey("&SPH_RST"));
        Assert.True(metadata.InstanceVariables.ContainsKey("&SPD_RST"));
        Assert.True(metadata.InstanceVariables.ContainsKey("&rowset_distrib"));
        Assert.Equal(PeopleCodeType.Rowset, metadata.InstanceVariables["&SPH_RST"].Type);
        Assert.Equal(PeopleCodeType.Rowset, metadata.InstanceVariables["&SPD_RST"].Type);
        Assert.Equal(PeopleCodeType.Rowset, metadata.InstanceVariables["&rowset_distrib"].Type);

        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        var memberAccess = FindMemberAccess(program, "rowset_distrib");
        Assert.NotNull(memberAccess);

        var inferredType = visitor.GetInferredType(memberAccess);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Rowset, inferredType.PeopleCodeType);
    }

    private static MemberAccessNode? FindMemberAccess(ProgramNode program, string memberName)
    {
        var collector = new MemberAccessCollector(memberName);
        program.Accept(collector);
        return collector.FoundNode;
    }

    private class MemberAccessCollector : AstVisitorBase
    {
        private readonly string _memberName;
        public MemberAccessNode? FoundNode { get; private set; }

        public MemberAccessCollector(string memberName)
        {
            _memberName = memberName;
        }

        public override void VisitMemberAccess(MemberAccessNode node)
        {
            if (node.MemberName.Equals(_memberName, StringComparison.OrdinalIgnoreCase))
            {
                FoundNode = node;
            }
            base.VisitMemberAccess(node);
        }
    }

    private class AssignmentCollector : AstVisitorBase
    {
        public List<AssignmentNode> Assignments { get; } = new();

        public override void VisitAssignment(AssignmentNode node)
        {
            Assignments.Add(node);
            base.VisitAssignment(node);
        }
    }
}
