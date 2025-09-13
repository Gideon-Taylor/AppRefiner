using PeopleCodeParser.SelfHosted.Nodes;
using System.Collections.Generic;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Debug-only refactor that marks statement numbers in the code for debugging purposes.
    /// Inserts formatted statement numbers at the beginning of each line to visualize
    /// how the parser assigns statement numbers to code lines.
    /// </summary>
    public class MarkStatementNumbersRefactor : BaseRefactor
    {
        #region Static Properties

        public new static string RefactorName => "Mark Statement Numbers";
        public new static string RefactorDescription => "Debug tool that marks statement numbers at the beginning of each line";
        
#if DEBUG
        public new static bool IsHidden => false;
#else
        public new static bool IsHidden => true;
#endif
        
        public new static bool RegisterKeyboardShortcut => false;

        #endregion

        public MarkStatementNumbersRefactor(ScintillaEditor editor) : base(editor)
        {
        }

        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);

            // Get the total line count from the editor
            int lineCount = ScintillaManager.GetLineCount(Editor);
            
            // Get the statement number mapping from the program node
            var statementMap = node.StatementNumberMap;
            
            // Create a reverse lookup: line number -> statement number
            var lineToStatementMap = new Dictionary<int, int>();
            foreach (var kvp in statementMap)
            {
                int statementNumber = kvp.Key;
                int lineNumber = kvp.Value;
                lineToStatementMap[lineNumber] = statementNumber;
            }

            // Process each line and insert appropriate text at the beginning
            // Note: lineNumber is 1-based for the mapping, but Scintilla uses 0-based indexing
            for (int lineNumber = 0; lineNumber < lineCount; lineNumber++)
            {
                int lineStartIndex = ScintillaManager.GetLineStartIndex(Editor, lineNumber);
                
                string textToInsert;
                if (lineToStatementMap.ContainsKey(lineNumber))
                {
                    // Line has a statement number - format as "(###)  " left-padded to 5 wide
                    int statementNumber = lineToStatementMap[lineNumber];
                    textToInsert = $"({statementNumber,5})  ";
                }
                else
                {
                    // Line has no statement number - insert 9 spaces
                    textToInsert = "         ";
                }

                InsertText(lineStartIndex, textToInsert, $"Mark statement number for line {lineNumber}");
            }
        }
    }
}