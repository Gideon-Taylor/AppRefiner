using PeopleCodeParser.SelfHosted;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

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

        public override List<TypeWithDimensionality> TargetTypes => new()
        {
            new TypeWithDimensionality(PeopleCodeType.String)
        };

        public override TypeWithDimensionality? ReturnType => new(PeopleCodeType.Integer);

        public override void Transform(ScintillaEditor editor, AstNode node, TypeWithDimensionality matchedType)
        {
            // TODO: Implementation deferred (transform &string.Length → Len(&string))
            // The trigger mechanism and actual transformation logic will be implemented later
            // when autocomplete integration is complete.
            // matchedType will be String for this single-type extension
            throw new NotImplementedException("Transform trigger mechanism deferred");
        }
    }
}
