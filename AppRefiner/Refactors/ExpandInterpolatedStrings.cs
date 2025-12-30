using PeopleCodeParser.SelfHosted.Nodes;
using System.Text;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Hidden refactor that expands interpolated strings to native PeopleCode string concatenation.
    /// Transforms $"Hello, {&name}!" to "Hello, " | &name | "!"
    /// </summary>
    public class ExpandInterpolatedStrings : BaseRefactor
    {
        #region Static Properties

        public new static string RefactorName => "QuickFix: Expand Interpolated Strings";
        public new static string RefactorDescription => "Expands interpolated strings to native PeopleCode concatenation using the | operator";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => true;

        #endregion

        #region Private Fields

        private List<InterpolatedStringNode> _nodesToExpand = new();
        private string _originalSource = string.Empty;

        #endregion

        #region Constructor

        public ExpandInterpolatedStrings(ScintillaEditor editor) : base(editor)
        {
        }

        #endregion

        #region Visitor Overrides

        public override void VisitProgram(ProgramNode node)
        {
            // Reset state for each refactor execution
            _nodesToExpand.Clear();

            // Store original source text for extracting expression text
            _originalSource = ScintillaManager.GetScintillaText(Editor) ?? string.Empty;

            // Traverse the AST to collect all interpolated string nodes
            base.VisitProgram(node);

            // After traversal, transform all collected nodes
            TransformInterpolatedStrings();
        }

        public override void VisitInterpolatedString(InterpolatedStringNode node)
        {
            // Skip nodes with errors (incomplete/malformed during editing)
            if (!node.HasErrors)
            {
                _nodesToExpand.Add(node);
            }

            // Continue traversal
            base.VisitInterpolatedString(node);
        }

        #endregion

        #region Transformation Logic

        private void TransformInterpolatedStrings()
        {
            foreach (var node in _nodesToExpand)
            {
                var parts = new List<string>();

                // Build parts list from the AST
                foreach (var part in node.Parts)
                {
                    if (part is StringFragment fragment)
                    {
                        // Add quoted text with proper escaping
                        if (!string.IsNullOrEmpty(fragment.Text))
                        {
                            // Escape internal quotes: " becomes ""
                            string escapedText = fragment.Text.Replace("\"", "\"\"");
                            parts.Add($"\"{escapedText}\"");
                        }
                    }
                    else if (part is Interpolation interpolation)
                    {
                        // Extract expression source text from original source
                        if (interpolation.Expression != null && !interpolation.HasErrors)
                        {
                            var exprSpan = interpolation.Expression.SourceSpan;
                            string exprText = _originalSource.Substring(
                                exprSpan.Start.ByteIndex,
                                exprSpan.End.ByteIndex - exprSpan.Start.ByteIndex
                            );

                            // Wrap complex expressions in parentheses for correct operator precedence
                            if (NeedsParentheses(interpolation.Expression))
                            {
                                exprText = $"({exprText})";
                            }

                            parts.Add(exprText);
                        }
                    }
                }

                // Remove empty string literals to avoid output like "Hello" | "" | &name
                parts = parts.Where(p => p != "\"\"").ToList();

                // Build the final result based on part count
                string result;
                if (parts.Count == 0)
                {
                    // Empty interpolated string $"" → ""
                    result = "\"\"";
                }
                else if (parts.Count == 1)
                {
                    // Single part → use as-is (no concatenation needed)
                    // Example: $"{&x}" → &x
                    result = parts[0];
                }
                else
                {
                    // Multiple parts → join with | operator
                    // Example: $"Hello, {&name}!" → "Hello, " | &name | "!"
                    result = string.Join(" | ", parts);
                }

                // Apply the transformation
                EditText(
                    node.SourceSpan,
                    result,
                    "Expand interpolated string to concatenation"
                );
            }
        }

        /// <summary>
        /// Determines if an expression needs to be wrapped in parentheses when used in string concatenation.
        /// Simple atomic expressions like identifiers, function calls, and member access don't need wrapping.
        /// </summary>
        private bool NeedsParentheses(ExpressionNode expression)
        {
            // Simple expressions that don't need parentheses
            return expression switch
            {
                IdentifierNode => false,           // &name
                MemberAccessNode => false,         // &obj.Property
                FunctionCallNode => false,         // Func()
                LiteralNode => false,              // 5, "text"
                _ => true                          // All other expressions (binary ops, unary ops, etc.)
            };
        }

        #endregion
    }
}
