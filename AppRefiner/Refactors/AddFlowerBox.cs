using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Adds a flower box header to the current file
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="AddFlowerBox"/> class
    /// </remarks>
    /// <param name="editor">The Scintilla editor instance</param>
    public class AddFlowerBox(ScintillaEditor editor) : BaseRefactor(editor)
    {
        /// <summary>
        /// Gets the display name for this refactor
        /// </summary>
        public new static string RefactorName => "Add Flower Box";

        /// <summary>
        /// Gets the description for this refactor
        /// </summary>
        public new static string RefactorDescription => "Add a flower box header to the current file";

        private string GenerateFlowerBoxHeader()
        {
            string today = DateTime.Now.ToString("MM/dd/yyyy");
            return @$"/* =======================================================================================
 Change Log.
 
 Date        Modified By  Description
 ----------  -----------  --------------------------------------------------------------        
 {today}  Your Name    Initial creation of the file.
 ======================================================================================= */
";
        }
        public override void EnterProgram(ProgramContext context)
        {
            base.EnterProgram(context);
            InsertText(0, GenerateFlowerBoxHeader(), "Add flower box");
        }
    }
}
