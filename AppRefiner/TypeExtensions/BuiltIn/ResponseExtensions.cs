using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using TypeInfo = PeopleCodeTypeInfo.Types.TypeInfo;

namespace AppRefiner.LanguageExtensions.BuiltIn
{
    /// <summary>
    /// Provides multiple string manipulation extensions for the String type.
    /// Uses simple pattern-based transforms for common operations.
    /// </summary>
    public class ResponseExtensions: BaseTypeExtension
    {
        public override TypeInfo TargetType => new BuiltinObjectTypeInfo("Response", PeopleCodeType.Response);

        public override List<ExtensionTransform> Transforms => new()
        {
            // ========== SEARCH METHODS ==========

            ExtensionTransform.CreateSimple(
                signature: "WriteHTMLLine(text: string) -> void",
                description: "Writes a line of text to the output with a break tag afterward",
                transformPattern: "Write(%1 | \"<br />\")"
            ),

        };
    }
}
