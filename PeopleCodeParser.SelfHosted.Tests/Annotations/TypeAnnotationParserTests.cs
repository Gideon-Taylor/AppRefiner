using PeopleCodeParser.SelfHosted.Annotations;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Tests.Annotations;

public class TypeAnnotationParserTests
{
    private static Token Comment(TokenType type, string text) =>
        Token.CreateTrivia(type, text, new SourceSpan(new SourcePosition(0), new SourcePosition(text.Length)));

    private static ParsedAnnotation? Parse(string text) =>
        TypeAnnotationParser.TryParse(Comment(TokenType.BlockComment, text));

    [Theory]
    [InlineData("/* as Session */", PeopleCodeType.Session, null, null, null)]
    [InlineData("/*AS session*/", PeopleCodeType.Session, null, null, null)]
    [InlineData("/* as CompIntfc.USER_PROFILE */", PeopleCodeType.ComponentInterface, null, null, "USER_PROFILE")]
    [InlineData("/* as Record.JOB */", PeopleCodeType.Record, "JOB", null, null)]
    [InlineData("/* as Record.JOB.EMPLID */", PeopleCodeType.Field, "JOB", "EMPLID", null)]
    [InlineData("/* as Field.EMPLID */", PeopleCodeType.Field, null, "EMPLID", null)]
    public void Parses_valid_annotations(string text, PeopleCodeType type, string? rec, string? fld, string? ci)
    {
        var parsed = Parse(text);

        Assert.NotNull(parsed);
        Assert.Null(parsed!.Error);
        var target = Assert.IsType<AnnotationTarget>(parsed.Target);
        Assert.Equal(type, target.Type);
        Assert.Equal(rec, target.Identity?.RecordName);
        Assert.Equal(fld, target.Identity?.FieldName);
        Assert.Equal(ci, target.Identity?.CompIntfcName);
        if (rec == null && fld == null && ci == null)
            Assert.Null(target.Identity);
    }

    [Theory]
    [InlineData("/* as Session */", "Session", AnnotationCategory.ApiObject)]
    [InlineData("/* as CompIntfc.USER_PROFILE */", "CompIntfc.USER_PROFILE", AnnotationCategory.ApiObject)]
    [InlineData("/* as Record.JOB */", "Record.JOB", AnnotationCategory.Record)]
    [InlineData("/* as Record.JOB.EMPLID */", "Record.JOB.EMPLID", AnnotationCategory.Field)]
    [InlineData("/* as Field.EMPLID */", "Field.EMPLID", AnnotationCategory.Field)]
    public void Records_written_text_and_category(string text, string written, AnnotationCategory category)
    {
        var target = Parse(text)!.Target!;

        Assert.Equal(written, target.Written);
        Assert.Equal(category, target.Category);
    }

    [Fact]
    public void Keeps_the_comment_token()
    {
        var token = Comment(TokenType.BlockComment, "/* as Session */");

        Assert.Same(token, TypeAnnotationParser.TryParse(token)!.Comment);
    }

    [Theory]
    [InlineData("/* store it as Session for later */")]
    [InlineData("/* &x = %Session; */")]
    [InlineData("/* Session */")]
    [InlineData("/* as */")]
    public void Ignores_non_annotations(string text)
    {
        Assert.Null(Parse(text));
    }

    [Fact]
    public void Ignores_other_comment_kinds()
    {
        Assert.Null(TypeAnnotationParser.TryParse(Comment(TokenType.ApiComment, "/+ as Session +/")));
        Assert.Null(TypeAnnotationParser.TryParse(Comment(TokenType.NestedComment, "<* as Session *>")));
        Assert.Null(TypeAnnotationParser.TryParse(Comment(TokenType.LineComment, "REM as Session;")));
    }

    [Theory]
    [InlineData("/* as Foo */", "Unknown type 'Foo' in annotation.")]
    [InlineData("/* as CompIntfc */", "Unknown type 'CompIntfc' in annotation. Did you mean ComponentInterface or CompIntfc.NAME?")]
    [InlineData("/* as Record */", "Unknown type 'Record' in annotation. Did you mean Record.NAME?")]
    [InlineData("/* as Field */", "Unknown type 'Field' in annotation. Did you mean Field.NAME or Record.REC.FIELD?")]
    [InlineData("/* as Rowset */", "Unknown type 'Rowset' in annotation.")]
    public void Reports_unknown_types(string text, string error)
    {
        var parsed = Parse(text);

        Assert.NotNull(parsed);
        Assert.Null(parsed!.Target);
        Assert.Equal(error, parsed.Error);
    }
}
