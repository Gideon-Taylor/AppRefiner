using PeopleCodeParser.SelfHosted;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

namespace AppRefiner.LanguageExtensions.BuiltIn
{
    /// <summary>
    /// Provides a .Length property extension for multiple types (String, Rowset)
    /// Example: &string.Length → Len(&string)
    /// Example: &rowset.Length → &rowset.ActiveRowCount
    /// </summary>
    public class MultiTypeLengthExtension : BaseLanguageExtension
    {
        public override string Name => "Length";

        public override string Description => "Get the length/count (transforms to Len() for strings, ActiveRowCount for rowsets)";

        public override LanguageExtensionType ExtensionType => LanguageExtensionType.Property;

        public override List<TypeWithDimensionality> TargetTypes => new()
        {
            new TypeWithDimensionality(PeopleCodeType.String),
            new TypeWithDimensionality(PeopleCodeType.Rowset)
        };

        public override TypeWithDimensionality? ReturnType => new(PeopleCodeType.Integer);

        public override void Transform(ScintillaEditor editor, AstNode node, TypeWithDimensionality matchedType)
        {
            // TODO: Implementation deferred
            // matchedType tells us which type was actually matched:
            // - If matchedType is String: transform &string.Length → Len(&string)
            // - If matchedType is Rowset: transform &rowset.Length → &rowset.ActiveRowCount

            // Example logic (deferred):
            // if (matchedType.Type == PeopleCodeType.String)
            // {
            //     // Transform to Len() call
            // }
            // else if (matchedType.Type == PeopleCodeType.Rowset)
            // {
            //     // Transform to .ActiveRowCount property access
            // }

            throw new NotImplementedException("Transform trigger mechanism deferred");
        }
    }
}
