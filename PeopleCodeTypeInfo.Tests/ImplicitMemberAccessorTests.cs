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
/// Tests for the implicit dot-form member accessors in TypeInferenceVisitor
/// (ResolveMemberAccessReturnType). When a bare member isn't a real property of the
/// object, certain builtin types transform the access into an implicit accessor call:
///   Row.RECNAME   → GetRecord()  → Record
///   Record.FIELD  → GetField()   → Field
///   Grid.COLUMN   → GetColumn()  → GridColumn
/// </summary>
public class ImplicitMemberAccessorTests
{
    private static TypeInfo? InferMemberAccess(string source, string memberName)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        var collector = new MemberAccessCollector(memberName);
        program.Accept(collector);
        Assert.NotNull(collector.FoundNode);

        return visitor.GetInferredType(collector.FoundNode!);
    }

    [Fact]
    public void GridColumnAccess_InfersGridColumnType()
    {
        // &MYGRID.CHECKLIST_ITEMCODE is equivalent to &MYGRID.GetColumn("CHECKLIST_ITEMCODE")
        // and must infer as GridColumn (PeopleBooks: Grid default column accessor).
        var source = @"
Function test()
   Local Grid &MYGRID;
   Local any &result;
   &result = &MYGRID.CHECKLIST_ITEMCODE;
End-Function;
";
        var inferred = InferMemberAccess(source, "CHECKLIST_ITEMCODE");

        Assert.NotNull(inferred);
        Assert.Equal(PeopleCodeType.Gridcolumn, inferred!.PeopleCodeType);
    }

    // Helper visitor to find a member access expression by member name.
    private class MemberAccessCollector : AstVisitorBase
    {
        private readonly string _memberName;
        public MemberAccessNode? FoundNode { get; private set; }

        public MemberAccessCollector(string memberName) => _memberName = memberName;

        public override void VisitMemberAccess(MemberAccessNode node)
        {
            if (node.MemberName.Equals(_memberName, StringComparison.OrdinalIgnoreCase))
                FoundNode = node;
            base.VisitMemberAccess(node);
        }
    }
}
