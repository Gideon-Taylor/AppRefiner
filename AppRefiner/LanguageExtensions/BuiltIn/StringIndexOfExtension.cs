using PeopleCodeParser.SelfHosted;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

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

        public override List<TypeWithDimensionality> TargetTypes => new()
        {
            new TypeWithDimensionality(PeopleCodeType.String)
        };

        public override List<Parameter>? Parameters => new()
        {
            new SingleParameter
            {
                Name = "searchString",
                ParameterType = new TypeWithDimensionality(PeopleCodeType.String)
            },
            new VariableParameter
            {
                Name = "startPos",
                InnerParameter = new SingleParameter
                {
                    ParameterType = new TypeWithDimensionality(PeopleCodeType.Integer)
                },
                MinCount = 0,
                MaxCount = 1
            }
        };

        public override TypeWithDimensionality? ReturnType => new(PeopleCodeType.Integer);

        public override void Transform(ScintillaEditor editor, AstNode node, TypeWithDimensionality matchedType)
        {
            // TODO: Implementation deferred (transform &string.IndexOf(x) → Find(x, &string))
            // The trigger mechanism and actual transformation logic will be implemented later
            // when autocomplete integration is complete.
            // matchedType will be String for this single-type extension
            throw new NotImplementedException("Transform trigger mechanism deferred");
        }
    }
}
