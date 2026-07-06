using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// LM-1: numeric overflow must widen (int → long → decimal) and fall back to the
/// raw source text, never silently become 0.
/// LM-2: line/column numbering is one-based from the first line.
/// LM-3: REM is a valid comment after Then/Else and after a closing comment.
/// LM-4: multiple trailing comments at EOF must all survive.
/// </summary>
public class LexerMediumTests
{
    [Theory]
    [InlineData("&x = 9999999999;", "9999999999")]                     // > int, fits long
    [InlineData("&x = 12345678901234567890123456789012345;", "12345678901234567890123456789012345")] // > decimal, raw text
    [InlineData("&x = 79999999999999999999999999999.5;", "79999999999999999999999999999.5")]          // > decimal, raw text
    public void LargeNumericLiterals_PreserveTheirValue(string source, string expected)
    {
        var (program, errors) = Parse(source);

        Assert.Empty(errors);
        var literal = program.FindDescendants<LiteralNode>().Single();
        Assert.Equal(expected, literal.Value?.ToString());
    }

    [Fact]
    public void LineAndColumnNumbers_AreZeroBasedOnEveryLine()
    {
        // Zero-based throughout, matching Scintilla and all AppRefiner consumers —
        // previously columns were 0-based on line one and 1-based after
        var lexer = new PeopleCodeLexer("ab\ncd");
        var tokens = lexer.TokenizeAll();

        var ab = tokens.First(t => t.Text == "ab");
        var cd = tokens.First(t => t.Text == "cd");
        Assert.Equal(0, ab.SourceSpan.Start.Line);
        Assert.Equal(0, ab.SourceSpan.Start.Column);
        Assert.Equal(1, cd.SourceSpan.Start.Line);
        Assert.Equal(0, cd.SourceSpan.Start.Column);
    }

    [Theory]
    [InlineData("If True Then Rem note; End-If;")]
    [InlineData("If True Then\n   &x = 1;\nElse Rem other branch;\nEnd-If;")]
    [InlineData("/* hdr */ REM old style;\n&x = 1;")]
    public void MidLineRem_LexesAsComment(string source)
    {
        var (_, errors) = Parse(source);

        Assert.Empty(errors);
    }

    [Fact]
    public void RecordFieldNamedRem_IsNotAComment()
    {
        var (program, errors) = Parse("&x = REC.REM;");

        Assert.Empty(errors);
        Assert.Contains(program.FindDescendants<MemberAccessNode>(),
            m => m.MemberName.Equals("REM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MultipleTrailingCommentsAtEof_AllSurvive()
    {
        var (program, _) = Parse("&x = 1; /* first */ /* second */");

        Assert.Equal(2, program.Comments.Count);
    }
}
