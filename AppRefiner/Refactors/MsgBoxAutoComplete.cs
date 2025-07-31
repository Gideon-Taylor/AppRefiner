using Antlr4.Runtime;
using System.Diagnostics;
using AppRefiner.Services; // For ScintillaEditor
using static AppRefiner.PeopleCode.PeopleCodeParser; // For ANTLR parser contexts

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that provides auto-completion for MsgBox() statements, expanding them to MessageBox() calls
    /// </summary>
    public class MsgBoxAutoComplete : ScopedRefactor<string>
    {
        public new static string RefactorName => "MsgBox Auto Complete";
        public new static string RefactorDescription => "Auto-completes MsgBox() statements with MessageBox() expansion";

        /// <summary>
        /// This refactor should not have a keyboard shortcut
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from refactoring lists
        /// </summary>
        public new static bool IsHidden => true;

        private bool isAppropriateContext = false;
        private int msgboxStartPos = -1;
        private int msgboxEndPos = -1;
        private bool autoPairingEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsgBoxAutoComplete"/> class
        /// </summary>
        /// <param name="editor">The Scintilla editor instance to use for this refactor</param>
        /// <param name="autoPairingEnabled">Whether auto-pairing is enabled, determines if closing parenthesis should be added</param>
        public MsgBoxAutoComplete(ScintillaEditor editor, bool autoPairingEnabled = true)
            : base(editor)
        {
            this.autoPairingEnabled = autoPairingEnabled;
            Debug.Log($"MsgBoxAutoComplete initialized with auto-pairing: {autoPairingEnabled}");
        }

        /// <summary>
        /// Check if the expression is a MsgBox() call and cursor is between the parentheses
        /// </summary>
        private bool IsMsgBoxExpressionAtCursor(ExpressionContext expr)
        {
            if (expr is FunctionCallExprContext functionCallExpr)
            {
                var simpleFunc = functionCallExpr.simpleFunctionCall();
                if (simpleFunc != null && simpleFunc.genericID()?.GetText().ToLower() == "msgbox")
                {
                    var args = simpleFunc.functionCallArguments();
                    if (simpleFunc.LPAREN() != null && simpleFunc.RPAREN() != null)
                    {
                        msgboxStartPos = simpleFunc.genericID().Start.StartIndex;  // Position at start of "MsgBox"
                        msgboxEndPos = simpleFunc.RPAREN().Symbol.StopIndex;      // Position of the closing parenthesis

                        // Check if cursor is between the parentheses or right after "MsgBox"
                        return CurrentPosition >= msgboxStartPos && CurrentPosition <= msgboxEndPos + 1;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Process expression statements to find MsgBox() calls
        /// </summary>
        public override void EnterExpressionStmt(ExpressionStmtContext context)
        {
            var expr = context.expression();
            if (expr != null && IsMsgBoxExpressionAtCursor(expr))
            {
                isAppropriateContext = true;
                Debug.Log($"MsgBoxAutoComplete: Found MsgBox() call at cursor position {CurrentPosition}");
            }
        }

        /// <summary>
        /// Process function call expressions directly
        /// </summary>
        public override void EnterFunctionCallExpr(FunctionCallExprContext context)
        {
            base.EnterFunctionCallExpr(context);
            
            if (IsMsgBoxExpressionAtCursor(context))
            {
                isAppropriateContext = true;
                Debug.Log($"MsgBoxAutoComplete: Found MsgBox() function call at cursor position {CurrentPosition}");
            }
        }

        /// <summary>
        /// Complete the traversal and generate changes
        /// </summary>
        public override void ExitProgram(ProgramContext context)
        {
            if (!isAppropriateContext)
            {
                Debug.Log("MsgBoxAutoComplete: Not in appropriate context, skipping");
                return;
            }

            if (msgboxStartPos < 0 || msgboxEndPos < 0)
            {
                Debug.Log("MsgBoxAutoComplete: Invalid position ranges, skipping");
                return;
            }

            // Replace MsgBox() with MessageBox(0,"",0,0,"")
            string replacementText = "MessageBox(0,\"\",0,0,\"\");";

            Debug.Log($"MsgBoxAutoComplete: Replacing MsgBox() from pos {msgboxStartPos} to {msgboxEndPos}");
            Debug.Log($"MsgBoxAutoComplete: Replacement text: '{replacementText}'");

            // Replace the entire MsgBox( call with MessageBox( expansion
            ReplaceText(
                msgboxStartPos,
                msgboxEndPos,
                replacementText,
                RefactorDescription
            );
        }
    }
}