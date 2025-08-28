using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted;
using AppRefiner.Services;
using AppRefiner;

namespace ParserPorting.Refactors.Impl
{
    /// <summary>
    /// Refactoring operation that provides auto-completion for MsgBox() statements, expanding them to MessageBox() calls
    /// </summary>
    public class MsgBoxAutoComplete : BaseRefactor
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
        private FunctionCallNode? targetMsgBoxCall;
        private readonly bool autoPairingEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsgBoxAutoComplete"/> class
        /// </summary>
        /// <param name="editor">The Scintilla editor instance to use for this refactor</param>
        /// <param name="autoPairingEnabled">Whether auto-pairing is enabled, determines if closing parenthesis should be added</param>
        public MsgBoxAutoComplete(ScintillaEditor editor, bool autoPairingEnabled = true) : base(editor)
        {
            this.autoPairingEnabled = autoPairingEnabled;
            Debug.Log($"MsgBoxAutoComplete initialized with auto-pairing: {autoPairingEnabled}");
        }

        /// <summary>
        /// Check if the function call is a MsgBox() call and cursor is in the appropriate position
        /// </summary>
        private bool IsMsgBoxCallAtCursor(FunctionCallNode functionCall)
        {
            // Check if it's a MsgBox call
            if (functionCall.Function is IdentifierNode identifier && 
                identifier.Name.Equals("msgbox", StringComparison.OrdinalIgnoreCase))
            {
                if (functionCall.SourceSpan.IsValid)
                {
                    var span = functionCall.SourceSpan;
                    // Check if cursor is within the function call
                    return CurrentPosition >= span.Start.Index && CurrentPosition <= span.End.Index + 1;
                }
            }
            return false;
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            if (IsMsgBoxCallAtCursor(node))
            {
                isAppropriateContext = true;
                targetMsgBoxCall = node;
                Debug.Log($"MsgBoxAutoComplete: Found MsgBox() call at cursor position {CurrentPosition}");
            }

            base.VisitFunctionCall(node);
        }

        public override void VisitExpressionStatement(ExpressionStatementNode node)
        {
            // Check if the expression statement contains a MsgBox function call
            if (node.Expression is FunctionCallNode functionCall && IsMsgBoxCallAtCursor(functionCall))
            {
                isAppropriateContext = true;
                targetMsgBoxCall = functionCall;
                Debug.Log($"MsgBoxAutoComplete: Found MsgBox() call in expression statement at cursor position {CurrentPosition}");
            }

            base.VisitExpressionStatement(node);
        }

        /// <summary>
        /// Complete the traversal and generate changes
        /// </summary>
        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);

            if (!isAppropriateContext)
            {
                Debug.Log("MsgBoxAutoComplete: Not in appropriate context, skipping");
                return;
            }

            if (targetMsgBoxCall == null || !targetMsgBoxCall.SourceSpan.IsValid)
            {
                Debug.Log("MsgBoxAutoComplete: Invalid target MsgBox call or position ranges, skipping");
                return;
            }

            // Replace MsgBox() with MessageBox(0,"",0,0,"")
            string replacementText = "MessageBox(0,\"\",0,0,\"\")";
            
            // Check if we need to add a semicolon (if it's a standalone statement)
            var originalText = GetOriginalText(targetMsgBoxCall);
            if (!string.IsNullOrEmpty(originalText) && originalText.TrimEnd().EndsWith(")"))
            {
                replacementText += ";";
            }

            Debug.Log($"MsgBoxAutoComplete: Replacing MsgBox() from pos {targetMsgBoxCall.SourceSpan.Start.Index} to {targetMsgBoxCall.SourceSpan.End.Index}");
            Debug.Log($"MsgBoxAutoComplete: Replacement text: '{replacementText}'");

            // Replace the entire MsgBox call with MessageBox expansion
            ReplaceNode(targetMsgBoxCall, replacementText, RefactorDescription);
        }
    }
}