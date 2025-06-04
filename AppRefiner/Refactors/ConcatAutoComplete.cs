using Antlr4.Runtime;
using System.Diagnostics;
using AppRefiner.Services; // For ScintillaEditor
using static AppRefiner.PeopleCode.PeopleCodeParser; // For ConcatShortHandExprContext

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that provides auto-completion for += shorthand (concatenation).
    /// </summary>
    public class ConcatAutoComplete : ScopedRefactor<string>
    {
        public new static string RefactorName => "Concat Auto Complete";
        public new static string RefactorDescription => "Auto-completes += shorthand to full concatenation expression";

        /// <summary>
        /// This refactor should not have a keyboard shortcut.
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from refactoring lists as it's auto-triggered.
        /// </summary>
        public new static bool IsHidden => true;

        private bool refactorApplied = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcatAutoComplete"/> class.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance to use for this refactor.</param>
        public ConcatAutoComplete(ScintillaEditor editor)
            : base(editor)
        {
            Debug.Log("ConcatAutoComplete initialized.");
        }

        /// <summary>
        /// Called when the parser enters a ConcatShortHandExpr rule.
        /// This is where the transformation from "lhs += rhs" to "lhs = lhs | rhs" occurs.
        /// </summary>
        /// <param name="context">The context for the ConcatShortHandExpr rule.</param>
        public override void EnterConcatShortHandExpr(ConcatShortHandExprContext context)
        {
            base.EnterConcatShortHandExpr(context);

            if (refactorApplied)
            {
                // Ensure the refactor is applied only once if this method were somehow called multiple times for the same instance.
                return;
            }

            var lhsExprCtx = context.expression(0); // Left-hand side expression
            var rhsExprCtx = context.expression(1); // Right-hand side expression

            // The ANTLR rule 'expression ADD EQ expression' ensures that if ConcatShortHandExprContext is formed,
            // lhsExprCtx and rhsExprCtx will be non-null. Their text content might be empty or represent
            // incompletely parsed input, especially for rhsExprCtx if triggered immediately after "+=".

            string lhsText = lhsExprCtx.GetText();
            string rhsOriginalText = rhsExprCtx.GetText();
            string rhsTextForConcat;

            if (string.IsNullOrWhiteSpace(rhsOriginalText))
            {
                // If the parsed RHS text is empty or consists only of whitespace,
                // substitute with an empty PeopleCode string literal for the concatenation.
                rhsTextForConcat = "";
            }
            else
            {
                rhsTextForConcat = rhsOriginalText;
            }

            var concatChar = "";
            if (context.ADD() != null)
            {
                // If the ADD token is present, it indicates an increment operation.
                concatChar = "+"; // Use PeopleCode concatenation operator
            }
            else if (context.SUBTR() != null)
            {
                concatChar = "-"; // Use assignment operator
            } else if (context.PIPE() != null)
            {
                // If the PIPE token is present, it indicates a pipe operation.
                concatChar = "|"; // Use PeopleCode pipe operator
            }


                // Construct the new expression: lhs = lhs | rhs
            string newText = $"{lhsText} = {lhsText} {concatChar} {rhsTextForConcat}";

            // The ConcatShortHandExpr context spans the entire "lhs += rhs" text.
            int startIndex = context.Start.StartIndex;
            int stopIndex = context.Stop.StopIndex;

            Debug.Log($"ConcatAutoComplete: Applying refactor for ConcatShortHandExpr.");
            Debug.Log($"  Original matched rule text: '{context.GetText()}'");
            Debug.Log($"  LHS text: '{lhsText}'");
            Debug.Log($"  RHS original text: '{rhsOriginalText}'");
            Debug.Log($"  RHS text for concatenation: '{rhsTextForConcat}'");
            Debug.Log($"  Calculated replacement: StartIndex={startIndex}, StopIndex={stopIndex}");
            Debug.Log($"  New expression to insert: '{newText}'");

            // Perform the text replacement in the editor.
            ReplaceText(startIndex, stopIndex, newText, RefactorDescription);
            
            refactorApplied = true; // Mark as applied to prevent re-application.
        }
    }
} 