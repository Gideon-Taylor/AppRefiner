using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using TypeInfo = PeopleCodeTypeInfo.Types.TypeInfo;

namespace AppRefiner.LanguageExtensions.BuiltIn
{
    /// <summary>
    /// Provides a .Length property extension for strings that transforms to Len() function
    /// Example: &string.Length → Len(&string)
    /// </summary>
    public class StringLengthExtension : BaseLanguageExtension
    {
        public override string Name => "Length";

        public override string Description => "Get the length of a string (transforms to Len())";

        public override LanguageExtensionType ExtensionType => LanguageExtensionType.Property;

        public override List<TypeInfo> TargetTypes => new()
        {
            PrimitiveTypeInfo.String
        };

        public override TypeInfo? ReturnType => PrimitiveTypeInfo.Integer;

        public override void Transform(ScintillaEditor editor, AstNode node, TypeInfo matchedType)
        {
            if (node is MemberAccessNode memberAccess)
            {
                // Get the target object text from source
                string content = ScintillaManager.GetScintillaText(editor) ?? "";
                var targetSpan = memberAccess.Target.SourceSpan;
                string targetText = content.Substring(
                    targetSpan.Start.ByteIndex,
                    targetSpan.End.ByteIndex - targetSpan.Start.ByteIndex
                );

                // Create replacement: &string.Length → Len(&string)
                string newText = $"Len({targetText})";

                // Replace entire member access expression
                ScintillaManager.ReplaceTextRange(
                    editor,
                    memberAccess.SourceSpan.Start.ByteIndex,
                    memberAccess.SourceSpan.End.ByteIndex,
                    newText
                );
            }
        }
    }
}
