using PeopleCodeParser.SelfHosted.Annotations;
using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted.Tests.Annotations;

public class AnnotationSuggestTests
{
    // Category is passed as a string because InlineData can't hold a nullable enum; "" means null
    [Theory]
    [InlineData("Local ApiObject /* as ", "ApiObject", "")]
    [InlineData("instance any /*as Fo", "", "Fo")]
    [InlineData("Local Record /* as ", "Record", "")]
    [InlineData("Global Field /* AS ", "Field", "")]
    [InlineData("Function F(&a As ApiObject /* as ", "ApiObject", "")]
    [InlineData("property ApiObject /* as ", "ApiObject", "")]
    [InlineData("method M() Returns ApiObject /* as ", "ApiObject", "")]
    [InlineData("&f = &s.GetFolder() /* as ", "", "")]
    [InlineData("&f = &s.GetFolder(); /* as ", "", "")]
    [InlineData("Local ApiObject &f = &s.GetFolder() /* as ", "", "")]
    [InlineData("If &ok Then\n   &f = &s.Folders.Item(1) /* as ", "", "")]
    [InlineData("&f = &s.GetFolder(&a, (&b)) /* as ", "", "")]
    public void Detects_partial_annotations(string text, string category, string partial)
    {
        Assert.True(TypeAnnotationParser.TryGetPartialAnnotation(text, out var actualCategory, out var actualPartial));
        AnnotationCategory? expected = category == "" ? null : Enum.Parse<AnnotationCategory>(category);
        Assert.Equal(expected, actualCategory);
        Assert.Equal(partial, actualPartial);
    }

    [Theory]
    [InlineData("Local string /* as ")]
    [InlineData("If &x Then /* as ")]
    [InlineData("/* as ")]
    [InlineData("Local ApiObject /* note as ")]
    [InlineData("Local ApiObject /* as CompIntfc.")]
    [InlineData("x = \"/* as ")]
    [InlineData("Local MyClass /* as ")]
    [InlineData("Local ApiObject /* as Fo\n")]
    [InlineData("&x = 1;\n/* as ")]
    [InlineData("Local ApiObject\n/* as ")]
    [InlineData("Local MyPkg:MyClass /* as ")]
    [InlineData("Local array of ApiObject /* as ")]
    // Only an assigned value takes a value annotation
    [InlineData("Local ApiObject &x /* as ")]
    [InlineData("Local ApiObject &x; /* as ")]
    [InlineData("instance ApiObject &a, &b /* as ")]
    [InlineData("Function F(&a /* as ")]
    [InlineData("Foo(&a /* as ")]
    [InlineData("&x = Foo(&a /* as ")]
    [InlineData("&s.Open() /* as ")]
    [InlineData("Return &x /* as ")]
    [InlineData("If &a = &b Then\n   DoIt(&c) /* as ")]
    public void Rejects_text_that_is_not_a_partial_annotation(string text)
    {
        Assert.False(TypeAnnotationParser.TryGetPartialAnnotation(text, out _, out _));
    }

    [Fact]
    public void Annotation_detected_before_a_later_comment()
    {
        // The text before the cursor when the document continues with "*/ /* old */"
        Assert.True(TypeAnnotationParser.TryGetPartialAnnotation("Local ApiObject /* as ", out var category, out var partial));
        Assert.Equal(AnnotationCategory.ApiObject, category);
        Assert.Equal("", partial);
    }

    [Fact]
    public void Record_list()
    {
        Assert.Equal(new[] { "Record." }, TypeAnnotationParser.SuggestedTypes(AnnotationCategory.Record));
    }

    [Fact]
    public void Field_list()
    {
        Assert.Equal(new[] { "Field.", "Record." }, TypeAnnotationParser.SuggestedTypes(AnnotationCategory.Field));
    }

    [Fact]
    public void ApiObject_list()
    {
        var list = TypeAnnotationParser.SuggestedTypes(AnnotationCategory.ApiObject);

        Assert.Equal("CompIntfc.", list[0]);
        Assert.Contains("Session", list);
        Assert.DoesNotContain("Record.", list);
    }

    [Fact]
    public void Null_list()
    {
        var list = TypeAnnotationParser.SuggestedTypes(null);

        Assert.Equal(new[] { "CompIntfc.", "Field.", "Record." }, list.Take(3));
    }

    [Fact]
    public void Every_subtype_entry_parses()
    {
        foreach (var entry in TypeAnnotationParser.SuggestedTypes(null).Where(e => !e.EndsWith('.')))
        {
            var comment = new PeopleCodeLexer($"/* as {entry} */").TokenizeAll()
                .Single(t => t.Type == TokenType.BlockComment);

            var parsed = TypeAnnotationParser.TryParse(comment);

            Assert.True(parsed?.Target != null, $"'{entry}' does not parse as an annotation target");
        }
    }
}
