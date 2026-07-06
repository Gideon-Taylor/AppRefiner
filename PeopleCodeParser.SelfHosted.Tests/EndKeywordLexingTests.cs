using PeopleCodeParser.SelfHosted.Lexing;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// LL-3: END keywords require exactly "End-Xxx" — "End Class" and "End -Class"
/// must not silently fuse into End-Class.
/// LL-4: TokenType.EndSet.GetText() typo ("snd-set").
/// </summary>
public class EndKeywordLexingTests
{
    [Theory]
    [InlineData("End Class")]
    [InlineData("End -Class")]
    [InlineData("End  -  Class")]
    public void SpacedEndKeyword_DoesNotFuse(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();

        Assert.DoesNotContain(tokens, t => t.Type == TokenType.EndClass);
    }

    [Fact]
    public void HyphenatedEndKeywords_StillLex()
    {
        var (_, errors) = Parse("If True Then\n   &x = 1;\nEnd-If;");

        Assert.Empty(errors);
    }

    [Fact]
    public void EndSetTokenText_IsCorrect()
    {
        Assert.Equal("end-set", TokenType.EndSet.GetText());
    }
}
