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
            ),

            /* TODO: This one should only show up from string *literals */
            new ExtensionTransform
            {
                Signature = "Interp -> string",
                Description = "Expands to any string interpolation markers into a concatenated string",
                TransformAction = InterpolateString
            }

        };

        private static void InterpolateString(ScintillaEditor editor, AstNode node, TypeInfo matchedType, VariableRegistry? variableRegistry)
        {
            if (node is not MemberAccessNode memberAccess) return;

            if (matchedType.IsAssignable)
            {
                /* This was called on a non-literal string, so we cannot transform it */
                /* ex: &fooVar.Interpolate() */
                /* ex: ("Hello " | &name).Interpolate() */
                /* We should just replace this with &fooVar, and ("Hello " | &name) respectively */
                /* Just remove the .Interpolate() call */

                return;
            }

            /* At this point we should have a string literal but lets confirm it */
            if (memberAccess.Target is LiteralNode literalNode &&
                literalNode.LiteralType == LiteralType.String)
            {
                /* We need to find any {expression} patterns in the string and replace them with concatenation */
                /* example: "Hello, {&name}! Today is {GetDate().DateValue().ToString(\"MM/dd/yyyy\")}." */
                /* becomes: "Hello, " | &name | "! Today is " | GetDate().DateValue().ToString("MM/dd/yyyy") | "." */

                // Get the source text
                string content = ScintillaManager.GetScintillaText(editor) ?? "";

                // Extract the literal string from source
                var literalSpan = literalNode.SourceSpan;
                string literalSource = content.Substring(
                    literalSpan.Start.ByteIndex,
                    literalSpan.End.ByteIndex - literalSpan.Start.ByteIndex
                );

                // Remove surrounding quotes
                if (literalSource.StartsWith("\"") && literalSource.EndsWith("\""))
                {
                    literalSource = literalSource.Substring(1, literalSource.Length - 2);
                }
                else
                {
                    return; // Not a properly quoted string
                }

                // Parse the string to find {expression} patterns
                List<string> parts = new List<string>();
                int currentPos = 0;
                int braceDepth = 0;
                int expressionStart = -1;

                for (int i = 0; i < literalSource.Length; i++)
                {
                    char c = literalSource[i];

                    // In PeopleCode, "" is the escape sequence for a literal quote
                    // Skip the second quote in a "" pair
                    if (c == '"' && i + 1 < literalSource.Length && literalSource[i + 1] == '"')
                    {
                        i++; // Skip the next quote
                        continue;
                    }

                    if (c == '{')
                    {
                        if (braceDepth == 0)
                        {
                            // Start of an expression
                            if (i > currentPos)
                            {
                                // Add the string literal part before the expression
                                string literalPart = literalSource.Substring(currentPos, i - currentPos);
                                parts.Add($"\"{literalPart}\"");
                            }
                            expressionStart = i + 1;
                        }
                        braceDepth++;
                    }
                    else if (c == '}')
                    {
                        braceDepth--;
                        if (braceDepth == 0 && expressionStart >= 0)
                        {
                            // End of an expression
                            string expression = literalSource.Substring(expressionStart, i - expressionStart);
                            parts.Add(expression);
                            currentPos = i + 1;
                            expressionStart = -1;
                        }
                    }
                }

                // Add any remaining literal part
                if (currentPos < literalSource.Length)
                {
                    string literalPart = literalSource.Substring(currentPos);
                    parts.Add($"\"{literalPart}\"");
                }

                // If no interpolation expressions were found, don't transform
                if (parts.Count <= 1)
                {
                    return;
                }

                // Remove empty string parts to clean up output
                parts = parts.Where(p => p != "\"\"").ToList();

                // Build the concatenated expression
                string result = string.Join(" | ", parts);

                // Replace the entire function call with the concatenated expression
                ScintillaManager.ReplaceTextRange(
                    editor,
                    memberAccess.SourceSpan.Start.ByteIndex,
                    memberAccess.SourceSpan.End.ByteIndex,
                    result
                );
            }

        }
    }
}
