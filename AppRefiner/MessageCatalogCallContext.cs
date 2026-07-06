using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner
{
    /// <summary>
    /// Where the cursor sits inside a MsgGet-family call: which function, which
    /// argument index, whether the set is already a typed literal, and whether the
    /// call already has a default-text argument. Drives the dialog's insert mode.
    /// </summary>
    public class MessageCatalogCallContext
    {
        public string FunctionName { get; }
        public MessageCatalogArgInfo ArgInfo { get; }
        public int CursorArgIndex { get; }
        public int? TypedSetNumber { get; }
        public bool HasDefaultTextArg { get; }

        public MessageCatalogCallContext(string functionName, MessageCatalogArgInfo argInfo,
            int cursorArgIndex, int? typedSetNumber, bool hasDefaultTextArg)
        {
            FunctionName = functionName;
            ArgInfo = argInfo;
            CursorArgIndex = cursorArgIndex;
            TypedSetNumber = typedSetNumber;
            HasDefaultTextArg = hasDefaultTextArg;
        }

        /// <summary>
        /// Detects whether the cursor is inside the message_set or message_num argument
        /// of a mapped call. Returns null otherwise (caller falls through to the existing
        /// Ctrl+Space behavior). Uses the error-recovering AST for the call itself and a
        /// lexical comma count for the argument index, which tolerates half-typed args.
        /// Inherits the pre-existing byte-vs-char index caveat shared by all Ctrl+Space
        /// context detection.
        /// </summary>
        public static MessageCatalogCallContext? TryDetect(ScintillaEditor editor, int position)
        {
            var program = editor.GetParsedProgram();
            if (program == null) return null;

            string? text = ScintillaManager.GetScintillaText(editor);
            if (string.IsNullOrEmpty(text)) return null;

            // Innermost mapped call whose parens contain the cursor
            FunctionCallNode? call = null;
            MessageCatalogArgInfo argInfo = null!;
            foreach (var candidate in program.FindDescendants<FunctionCallNode>())
            {
                if (candidate.Function is not IdentifierNode ident) continue;
                if (!MessageCatalogFunctions.TryGetArgPositions(ident.Name, out var info)) continue;
                if (!candidate.SourceSpan.IsValid || !candidate.Function.SourceSpan.IsValid) continue;
                if (position <= candidate.Function.SourceSpan.End.ByteIndex) continue;   // must be past the name

                int callEnd = candidate.SourceSpan.End.ByteIndex;
                if (position > callEnd) continue;
                // End.ByteIndex is one PAST the call's last character. When the call is
                // syntactically closed (last char is ')'), a cursor sitting exactly at
                // End is OUTSIDE the parens — don't match there. Half-typed calls whose
                // recovered span doesn't end in ')' must still match at End (the primary
                // use case: Ctrl+Space right after typing "MsgGet(").
                if (position == callEnd && callEnd - 1 >= 0 && callEnd - 1 < text.Length
                    && text[callEnd - 1] == ')') continue;
                if (call == null || candidate.SourceSpan.Start.ByteIndex > call.SourceSpan.Start.ByteIndex)
                {
                    call = candidate;
                    argInfo = info;
                }
            }
            if (call == null) return null;

            // Argument index = commas between the call's '(' and the cursor at depth 0,
            // outside string literals ("" escapes toggle twice and self-correct).
            int openParen = call.Function.SourceSpan.End.ByteIndex;
            while (openParen < text.Length && text[openParen] != '(') openParen++;
            if (openParen >= position) return null;

            int argIndex = 0, depth = 0;
            bool inString = false;
            for (int i = openParen + 1; i < position && i < text.Length; i++)
            {
                char c = text[i];
                if (inString) { if (c == '"') inString = false; continue; }
                if (c == '"') inString = true;
                else if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0) argIndex++;
            }

            if (argIndex != argInfo.SetArg && argIndex != argInfo.NumArg) return null;

            int? typedSet = null;
            if (call.Arguments.Count > argInfo.SetArg
                && call.Arguments[argInfo.SetArg] is LiteralNode literal
                && literal.LiteralType == LiteralType.Integer)
            {
                try { typedSet = Convert.ToInt32(literal.Value); } catch { }
            }

            bool hasDefaultText = call.Arguments.Count > argInfo.DefaultTxtArg;
            string functionName = ((IdentifierNode)call.Function).Name;

            return new MessageCatalogCallContext(functionName, argInfo, argIndex, typedSet, hasDefaultText);
        }
    }
}
