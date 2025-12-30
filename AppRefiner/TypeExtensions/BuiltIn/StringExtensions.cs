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
    public class StringExtensions : BaseTypeExtension
    {
        public override TypeInfo TargetType => PrimitiveTypeInfo.String;

        public override List<ExtensionTransform> Transforms => new()
        {
            // ========== LENGTH PROPERTIES ==========

            ExtensionTransform.CreateSimple(
                signature: "Len -> number",
                description: "Get the length of a string (transforms to Len())",
                transformPattern: "Len(%1)"
            ),

            // ========== CHARACTER CODE PROPERTIES ==========

            ExtensionTransform.CreateSimple(
                signature: "Code -> number",
                description: "Get character code of first character (transforms to Code())",
                transformPattern: "Code(%1)"
            ),

            // ========== VALIDATION PROPERTIES (IS* WITHOUT PARAMETERS) ==========

            ExtensionTransform.CreateSimple(
                signature: "IsAlpha -> boolean",
                description: "Check if alphabetic (transforms to IsAlpha())",
                transformPattern: "IsAlpha(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "IsAlphaNumeric -> boolean",
                description: "Check if alphanumeric (transforms to IsAlphaNumeric())",
                transformPattern: "IsAlphaNumeric(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "IsDigits -> boolean",
                description: "Check if all digits (transforms to IsDigits())",
                transformPattern: "IsDigits(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "IsNumber -> boolean",
                description: "Check if valid number (transforms to IsNumber())",
                transformPattern: "IsNumber(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "IsDate -> boolean",
                description: "Check if valid date (transforms to IsDate())",
                transformPattern: "IsDate(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "IsDateTime -> boolean",
                description: "Check if valid datetime (transforms to IsDateTime())",
                transformPattern: "IsDateTime(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "IsTime -> boolean",
                description: "Check if valid time (transforms to IsTime())",
                transformPattern: "IsTime(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "IsY -> boolean",
                description: "Returns True if string = \"Y\"",
                transformPattern: "(%1 = \"Y\")"
            ),

            ExtensionTransform.CreateSimple(
                signature: "IsNotY -> boolean",
                description: "Returns True if string <> \"Y\"",
                transformPattern: "(%1 <> \"Y\")"
            ),

            ExtensionTransform.CreateSimple(
                signature: "IsN -> boolean",
                description: "Returns True if string = \"N\"",
                transformPattern: "(%1 = \"N\")"
            ),

            ExtensionTransform.CreateSimple(
                signature: "IsNotN -> boolean",
                description: "Returns True if string <> \"N\"",
                transformPattern: "(%1 <> \"N\")"
            ),

            // ========== SEARCH METHODS ==========

            ExtensionTransform.CreateSimple(
                signature: "Find(search_string: string, start_pos?: number) -> number",
                description: "Find index of substring (transforms to Find())",
                transformPattern: "Find(%2, %1, %3)"
            ),


            // ========== CASE CONVERSION METHODS ==========

            ExtensionTransform.CreateSimple(
                signature: "Upper() -> string",
                description: "Convert to uppercase (transforms to Upper())",
                transformPattern: "Upper(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "Lower() -> string",
                description: "Convert to lowercase (transforms to Lower())",
                transformPattern: "Lower(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "Proper() -> string",
                description: "Convert to proper/title case (transforms to Proper())",
                transformPattern: "Proper(%1)"
            ),

            // ========== TRIMMING & CLEANING METHODS ==========

            ExtensionTransform.CreateSimple(
                signature: "Trim(trim_string?: string) -> string",
                description: "Trim from both left and right (transforms to RTrim(LTrim()))",
                transformPattern: "RTrim(LTrim(%1, %2), %2)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "LTrim(trim_string?: string) -> string",
                description: "Trim from left (transforms to LTrim())",
                transformPattern: "LTrim(%1, %2)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "RTrim(trim_string?: string) -> string",
                description: "Trim from right (transforms to RTrim())",
                transformPattern: "RTrim(%1, %2)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "Clean() -> string",
                description: "Remove non-printable characters (transforms to Clean())",
                transformPattern: "Clean(%1)"
            ),

            // ========== SUBSTRING EXTRACTION METHODS ==========

            ExtensionTransform.CreateSimple(
                signature: "Left(num_chars?: number) -> string",
                description: "Get leftmost characters (transforms to Left())",
                transformPattern: "Left(%1, %2)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "Right(num_chars?: number) -> string",
                description: "Get rightmost characters (transforms to Right())",
                transformPattern: "Right(%1, %2)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "Substring(start_pos: number, length: number) -> string",
                description: "Extract substring (transforms to Substring())",
                transformPattern: "Substring(%1, %2, %3)"
            ),

            // ========== REPLACEMENT & REPETITION METHODS ==========

            ExtensionTransform.CreateSimple(
                signature: "Replace(start: number, num_chars: number, newtext: string) -> string",
                description: "Replace portion of string (transforms to Replace())",
                transformPattern: "Replace(%1, %2, %3, %4)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "Substitute(old_text: string, new_text: string) -> string",
                description: "Substitute text (transforms to Substitute())",
                transformPattern: "Substitute(%1, %2, %3)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "Rept(reps: number) -> string",
                description: "Repeat string N times (transforms to Rept())",
                transformPattern: "Rept(%1, %2)"
            ),

            // ========== ENCODING & ESCAPING METHODS ==========

            ExtensionTransform.CreateSimple(
                signature: "EncodeURL() -> string",
                description: "URL encode (transforms to EncodeURL())",
                transformPattern: "EncodeURL(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "EncodeURLForQueryString() -> string",
                description: "URL encode for query string (transforms to EncodeURLForQueryString())",
                transformPattern: "EncodeURLForQueryString(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "Unencode() -> string",
                description: "URL decode (transforms to Unencode())",
                transformPattern: "Unencode(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "EscapeHTML() -> string",
                description: "Escape HTML entities (transforms to EscapeHTML())",
                transformPattern: "EscapeHTML(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "EscapeJavascriptString() -> string",
                description: "Escape for JavaScript (transforms to EscapeJavascriptString())",
                transformPattern: "EscapeJavascriptString(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "Quote() -> string",
                description: "Add quotes around string (transforms to Quote())",
                transformPattern: "Quote(%1)"
            ),

            // ========== HASHING METHODS ==========

            ExtensionTransform.CreateSimple(
                signature: "Hash() -> string",
                description: "Generate hash (transforms to Hash())",
                transformPattern: "Hash(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "HashSHA256() -> string",
                description: "Generate SHA-256 hash (transforms to HashSHA256())",
                transformPattern: "HashSHA256(%1)"
            ),

            // ========== CONVERSION METHODS ==========

            ExtensionTransform.CreateSimple(
                signature: "Value() -> number",
                description: "Convert to number (transforms to Value())",
                transformPattern: "Value(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "ValueUser() -> number",
                description: "Convert to number using user format (transforms to ValueUser())",
                transformPattern: "ValueUser(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "DateValue() -> date",
                description: "Convert to date (transforms to DateValue())",
                transformPattern: "DateValue(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "DateTimeValue() -> datetime",
                description: "Convert to datetime (transforms to DateTimeValue())",
                transformPattern: "DateTimeValue(%1)"
            ),

            ExtensionTransform.CreateSimple(
                signature: "TimeValue() -> time",
                description: "Convert to time (transforms to TimeValue())",
                transformPattern: "TimeValue(%1)"
            ),

            // ========== SPLITTING METHODS ==========

            ExtensionTransform.CreateSimple(
                signature: "Split(separator?: string) -> array_string",
                description: "Split into array (transforms to Split())",
                transformPattern: "Split(%1, %2)"
            )
        };
    }
}
