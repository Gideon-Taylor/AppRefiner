using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using TypeInfo = PeopleCodeTypeInfo.Types.TypeInfo;

namespace AppRefiner.LanguageExtensions.BuiltIn
{
    /// <summary>
    /// Provides an .IndexOf() method extension for strings that transforms to Find() function
    /// Example: &string.IndexOf("foo") → Find("foo", &string)
    /// Example: &string.IndexOf("foo", 3) → Find("foo", &string, 3)
    /// </summary>
    public class StringIndexOfExtension : BaseLanguageExtension
    {
        public override string Name => "IndexOf";

        public override string Description => "Find index of substring (transforms to Find())";

        public override LanguageExtensionType ExtensionType => LanguageExtensionType.Method;

        public override List<TypeInfo> TargetTypes => new()
        {
            PrimitiveTypeInfo.String
        };

        public override void Transform(ScintillaEditor editor, AstNode node, TypeInfo matchedType, VariableRegistry? variableRegistry = null)
        {
            if (node is FunctionCallNode funcCall &&
                funcCall.Function is MemberAccessNode memberAccess)
            {
                string content = ScintillaManager.GetScintillaText(editor) ?? "";

                // Get target text
                var targetSpan = memberAccess.Target.SourceSpan;
                string targetText = content.Substring(
                    targetSpan.Start.ByteIndex,
                    targetSpan.End.ByteIndex - targetSpan.Start.ByteIndex
                );

                // Get argument texts
                var argTexts = funcCall.Arguments.Select(arg =>
                {
                    var span = arg.SourceSpan;
                    return content.Substring(
                        span.Start.ByteIndex,
                        span.End.ByteIndex - span.Start.ByteIndex
                    );
                }).ToList();

                // Build replacement: &string.IndexOf("foo") → Find("foo", &string)
                // Or: &string.IndexOf("foo", 3) → Find("foo", &string, 3)
                string newText = argTexts.Count > 0
                    ? $"Find({argTexts[0]}, {targetText}{(argTexts.Count > 1 ? ", " + string.Join(", ", argTexts.Skip(1)) : "")})"
                    : $"Find({targetText})";

                // Replace entire function call
                ScintillaManager.ReplaceTextRange(
                    editor,
                    funcCall.SourceSpan.Start.ByteIndex,
                    funcCall.SourceSpan.End.ByteIndex,
                    newText
                );
            }
        }

        public override FunctionInfo? FunctionInfo => new FunctionInfo()
        {
            Parameters = new()
                {
                    new SingleParameter(new TypeWithDimensionality(PeopleCodeType.String), "search_string"),
                    new VariableParameter(new SingleParameter(new TypeWithDimensionality(PeopleCodeType.Number)), 0, 1, "start_index")
            },
            ReturnType = new TypeWithDimensionality(PeopleCodeType.Number)
        };

        public override TypeWithDimensionality ReturnType => new TypeWithDimensionality(PeopleCodeType.Number);
    }
}
