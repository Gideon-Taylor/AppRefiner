using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using AstNodeBase = PeopleCodeParser.SelfHosted.AstNode;
using SourcePos = PeopleCodeParser.SelfHosted.SourcePosition;
using SourceSpanT = PeopleCodeParser.SelfHosted.SourceSpan;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// AST-2..AST-6: latent API traps — overload misbinding, span fallbacks,
/// wildcard import paths, parent-chain cycles, and unchecked casts.
/// </summary>
public class AstApiTrapTests
{
    [Fact]
    public void SourcePosition_HasNoAmbiguousConstructors()
    {
        // AST-2: a 3-arg call used to bind (index, line, column) while looking like
        // (index, byteIndex, line) — only 1-arg and full 4-arg forms may exist
        var ctors = typeof(SourcePos).GetConstructors();
        Assert.All(ctors, c => Assert.True(c.GetParameters().Length is 1 or 4,
            $"Ambiguous constructor with {c.GetParameters().Length} parameters"));
    }

    [Fact]
    public void SourceSpan_FallsBackToFirstToken_WhenLastTokenMissing()
    {
        var token = new Token(TokenType.GenericId, "x",
            new SourceSpanT(new SourcePos(5, 5, 0, 4), new SourcePos(6, 6, 0, 5)));
        var node = new IdentifierNode("x", IdentifierType.Generic) { FirstToken = token };

        Assert.Equal(5, node.SourceSpan.Start.Index);
        Assert.Equal(6, node.SourceSpan.End.Index);
    }

    [Fact]
    public void WildcardImport_FromPathList_HasWildcardFullPath()
    {
        var import = new ImportNode(new[] { "MyPkg" }, null);

        Assert.Equal("MyPkg:*", import.FullPath);
    }

    [Fact]
    public void ParentChainWalkers_FailFastOnCycles()
    {
        var a = new BlockNode();
        var b = new BlockNode();
        a.AddStatement(b);
        b.AddStatement(a); // accidental cycle

        Assert.Throws<InvalidOperationException>(() => a.GetRoot());
        Assert.Throws<InvalidOperationException>(() => a.FindAncestor<ProgramNode>());
    }

    [Fact]
    public void IteratorName_ToleratesNestedMemberAccess()
    {
        var span = new SourceSpanT(new SourcePos(0), new SourcePos(1));
        var inner = new MemberAccessNode(new IdentifierNode("REC", IdentifierType.Generic), "A", span);
        var outer = new MemberAccessNode(inner, "B", span);
        var token = new Token(TokenType.GenericId, "REC", span);

        var forNode = new ForStatementNode(
            outer, token,
            new LiteralNode(1, LiteralType.Integer),
            new LiteralNode(2, LiteralType.Integer),
            new BlockNode());

        // Must not throw InvalidCastException
        Assert.NotNull(forNode.IteratorName);
    }
}
