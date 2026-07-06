using PeopleCodeParser.SelfHosted.Nodes;
using System.Text;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Moves a local function implementation (with its leading comments) above the
    /// function containing the cursor — the quick fix for forward references, which
    /// PeopleCode does not allow. The function name rides in editor.QuickFixContext.
    /// Operates on whole lines in byte space (Scintilla/SourceSpan indices are UTF-8
    /// byte indices — never index the C# string directly with them).
    /// </summary>
    public class MoveFunctionAbove : BaseRefactor
    {
        public new static string RefactorName => "Move Function Above";
        public new static string RefactorDescription => "Moves a function implementation above its first use";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => true;

        private readonly string _functionName;

        public MoveFunctionAbove(ScintillaEditor editor) : base(editor)
        {
            if (editor.QuickFixContext is not string functionName || string.IsNullOrEmpty(functionName))
                throw new InvalidOperationException("QuickFix context does not contain the function name");

            _functionName = functionName;
        }

        public override void VisitProgram(ProgramNode node)
        {
            var target = node.Functions.FirstOrDefault(f => f.IsImplementation &&
                string.Equals(f.Name, _functionName, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                SetFailure($"Function '{_functionName}' implementation not found");
                return;
            }

            // Destination: the implementation containing the cursor (the call site the
            // quick fix was invoked from), else the start of main code
            var containing = node.Functions.FirstOrDefault(f => f.IsImplementation &&
                f != target &&
                f.SourceSpan.Start.ByteIndex <= CurrentPosition &&
                CurrentPosition <= f.SourceSpan.End.ByteIndex);

            int destLine;
            if (containing != null)
            {
                destLine = StartLineIncludingComments(containing);
            }
            else if (node.MainBlock != null)
            {
                destLine = node.MainBlock.SourceSpan.Start.Line;
            }
            else
            {
                SetFailure("Could not determine where to move the function");
                return;
            }

            int targetStartLine = StartLineIncludingComments(target);
            if (targetStartLine <= destLine)
            {
                SetFailure($"Function '{_functionName}' is already above this location");
                return;
            }

            // Whole-line byte ranges. End boundary = start of the line after End-Function,
            // which naturally carries the trailing line break with the block.
            int destIndex = ScintillaManager.GetLineStartIndex(Editor, destLine);
            int targetStart = ScintillaManager.GetLineStartIndex(Editor, targetStartLine);
            int targetEnd = ScintillaManager.GetLineStartIndex(Editor, target.SourceSpan.End.Line + 1);
            var contentBytes = Encoding.UTF8.GetBytes(ScintillaManager.GetScintillaText(Editor) ?? string.Empty);
            if (targetEnd < 0 || targetEnd > contentBytes.Length)
            {
                targetEnd = contentBytes.Length; // Function block ends at EOF
            }
            if (destIndex < 0 || targetStart < 0 || targetStart >= targetEnd)
            {
                SetFailure("Could not resolve the function block boundaries");
                return;
            }

            string blockText = Encoding.UTF8.GetString(contentBytes, targetStart, targetEnd - targetStart);
            if (!blockText.EndsWith("\n"))
            {
                // EOF case: ensure separation from the block below, matching the
                // document's own line-ending convention
                blockText += blockText.Contains("\r\n") ? "\r\n" : "\n";
            }

            // Blank line between the moved block and the code below it
            blockText += blockText.Contains("\r\n") ? "\r\n" : "\n";

            // Insert BEFORE delete: BaseRefactor.InsertText has a dedup shortcut that
            // rewrites any pending delete-style edit in place (no position check), which
            // would silently discard destIndex and turn the move into a no-op. DeleteText's
            // own dedup checks StartIndex equality, so this order is safe — same pattern
            // as LocalVariableCollectorRefactor.
            InsertText(destIndex, blockText, $"Insert function '{_functionName}' above its first use");
            DeleteText(targetStart, targetEnd, $"Remove function '{_functionName}' from below");
        }

        private static int StartLineIncludingComments(FunctionNode fn)
        {
            var firstComment = fn.GetLeadingComments().FirstOrDefault();
            return firstComment != null
                ? Math.Min(firstComment.SourceSpan.Start.Line, fn.SourceSpan.Start.Line)
                : fn.SourceSpan.Start.Line;
        }
    }
}
