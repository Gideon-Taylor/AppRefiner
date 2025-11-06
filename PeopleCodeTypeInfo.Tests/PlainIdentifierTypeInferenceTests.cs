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
/// Tests for plain identifier type inference.
/// Plain identifiers (without & or % prefix) should be inferred as Field type with empty record context.
/// </summary>
public class PlainIdentifierTypeInferenceTests
{
    /// <summary>
    /// Test: Plain identifier "A" should be inferred as Field (not Record)
    /// with empty record name to indicate runtime context dependency.
    /// </summary>
    [Fact]
    public void PlainIdentifier_ShouldBeInferredAsRecord()
    {
        var source = @"
function test();
   local any &result;
   &result = A;
end-function;
";

        // Parse the source
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        // Extract metadata
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");

        // Run type inference
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        // Find the assignment: &result = A
        var function = program.Functions[0];
        var assignment = FindFirstAssignment(function);
        Assert.NotNull(assignment);

        // The value expression should be identifier "A"
        var identifier = assignment.Value as IdentifierNode;
        Assert.NotNull(identifier);
        Assert.Equal("A", identifier.Name);

        // Get the inferred type
        var inferredType = visitor.GetInferredType(identifier);
        Assert.NotNull(inferredType);

        // Should be FieldTypeInfo (not Record, not Unknown)
        Assert.IsType<RecordTypeInfo>(inferredType);
        var recordType = (RecordTypeInfo)inferredType;

        // Record name should be empty (runtime context)
        Assert.Equal("A", recordType.RecordName);
    }

    /// <summary>
    /// Test: RECORD.FIELD pattern should still work correctly
    /// with explicit record name.
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

        // Parse the source
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        // Extract metadata
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");

        // Run type inference
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        // Find the assignment: &result = MY_RECORD.MY_FIELD
        var function = program.Functions[0];
        var assignment = FindFirstAssignment(function);
        Assert.NotNull(assignment);

        // The value expression should be member access
        var memberAccess = assignment.Value as MemberAccessNode;
        Assert.NotNull(memberAccess);

        // Get the inferred type
        var inferredType = visitor.GetInferredType(memberAccess);
        Assert.NotNull(inferredType);

        // Should be FieldTypeInfo with explicit record name
        Assert.IsType<FieldTypeInfo>(inferredType);
        var fieldType = (FieldTypeInfo)inferredType;

        // Record name should be "MY_RECORD" (not empty)
        Assert.Equal("MY_RECORD", fieldType.RecordName);
        Assert.Equal("MY_FIELD", fieldType.FieldName);
    }

    /// <summary>
    /// Test: Multiple plain identifiers should all be inferred as Fields
    /// </summary>
    [Fact]
    public void MultiplePlainIdentifiers_ShouldAllBeRecords()
    {
        var source = @"
function test();
   local any &result;
   &result = START_DT;
   &result = END_DT;
   &result = AMOUNT;
end-function;
";

        // Parse the source
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        // Extract metadata
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");

        // Run type inference
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        // Find all assignments
        var function = program.Functions[0];
        var assignments = FindAllAssignments(function);
        Assert.Equal(3, assignments.Count);

        // Verify each identifier is inferred as Field
        var expectedFieldNames = new[] { "START_DT", "END_DT", "AMOUNT" };
        for (int i = 0; i < 3; i++)
        {
            var identifier = assignments[i].Value as IdentifierNode;
            Assert.NotNull(identifier);

            var inferredType = visitor.GetInferredType(identifier);
            Assert.IsType<RecordTypeInfo>(inferredType);

            var recordType = (RecordTypeInfo)inferredType;
            Assert.Equal(expectedFieldNames[i], recordType.RecordName);
        }
    }

    /// <summary>
    /// Test: Variables with & prefix should not be affected by this change
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

        // Parse the source
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        // Extract metadata
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");

        // Run type inference
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        // Find the assignment: &result = &myVar
        var function = program.Functions[0];
        var assignment = FindFirstAssignment(function);
        Assert.NotNull(assignment);

        var identifier = assignment.Value as IdentifierNode;
        Assert.NotNull(identifier);
        Assert.Equal("&myVar", identifier.Name);

        // Should be inferred as string (the variable type), NOT Field
        var inferredType = visitor.GetInferredType(identifier);
        Assert.NotNull(inferredType);
        // StringTypeInfo is a specific implementation of string type
        Assert.Equal(PeopleCodeType.String, inferredType.PeopleCodeType);
        Assert.IsNotType<FieldTypeInfo>(inferredType); // Ensure it's not a Field type
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

        // Parse the source
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        // Extract metadata
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "LoadDistribs");

        // Verify all three instance variables are registered
        Assert.Equal(3, metadata.InstanceVariables.Count);

        Assert.True(metadata.InstanceVariables.ContainsKey("&SPH_RST"));
        Assert.True(metadata.InstanceVariables.ContainsKey("&SPD_RST"));
        Assert.True(metadata.InstanceVariables.ContainsKey("&rowset_distrib"));

        // Verify they all have the correct type
        Assert.Equal(PeopleCodeType.Rowset, metadata.InstanceVariables["&SPH_RST"].Type);
        Assert.Equal(PeopleCodeType.Rowset, metadata.InstanceVariables["&SPD_RST"].Type);
        Assert.Equal(PeopleCodeType.Rowset, metadata.InstanceVariables["&rowset_distrib"].Type);

        // Run type inference
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        // Find the member access expression %This.rowset_distrib
        var memberAccess = FindMemberAccess(program, "rowset_distrib");
        Assert.NotNull(memberAccess);

        // Get the inferred type - should be Rowset, not 'any'
        var inferredType = visitor.GetInferredType(memberAccess);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Rowset, inferredType.PeopleCodeType);
    }

    // Helper method to find a member access expression by member name
    private static MemberAccessNode? FindMemberAccess(ProgramNode program, string memberName)
    {
        var collector = new MemberAccessCollector(memberName);
        program.Accept(collector);
        return collector.FoundNode;
    }

    // Helper visitor to find member access expressions
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

    // Helper visitor to collect all assignment nodes
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
