using PeopleCodeParser.SelfHosted;
using AppRefiner.Services;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Represents a text edit to be applied to the source code
    /// </summary>
    public class TextEdit
    {
        /// <summary>
        /// The starting index in the source where the edit begins
        /// </summary>
        public int StartIndex { get; }

        /// <summary>
        /// The ending index in the source where the edit ends
        /// </summary>
        public int EndIndex { get; }

        /// <summary>
        /// The new text to replace the old text
        /// </summary>
        public string NewText { get; }

        /// <summary>
        /// Description of the edit for logging and debugging
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// The change in length that this edit will cause
        /// </summary>
        public int LengthDelta => NewText.Length - (EndIndex - StartIndex);

        /// <summary>
        /// Creates a new text edit
        /// </summary>
        public TextEdit(int startIndex, int endIndex, string newText, string description)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
            NewText = newText;
            Description = description;
        }

        /// <summary>
        /// Creates a new text edit from a source span
        /// </summary>
        public TextEdit(SourceSpan span, string newText, string description)
            : this(span.Start.ByteIndex, span.End.ByteIndex, newText, description)
        {
        }

        /// <summary>
        /// Updates cursor position based on this edit
        /// </summary>
        public int UpdateCursorPosition(int cursorPosition)
        {
            if (cursorPosition < StartIndex)
            {
                // Cursor is before edit, no change needed
                return cursorPosition;
            }
            else if (cursorPosition <= EndIndex)
            {
                // Cursor is within edited text, move to end of new text
                return StartIndex + NewText.Length;
            }
            else
            {
                // Cursor is after edit, adjust by the change in length
                return cursorPosition + LengthDelta;
            }
        }

        /// <summary>
        /// Applies this edit to the given ScintillaEditor
        /// </summary>
        public bool ApplyToScintilla(ScintillaEditor editor)
        {
            try
            {
                ScintillaManager.ReplaceTextRange(editor, StartIndex, EndIndex, NewText);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, $"Error applying edit: {Description}");
                return false;
            }
        }
    }
}
