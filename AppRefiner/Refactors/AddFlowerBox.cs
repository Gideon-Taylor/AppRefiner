using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Adds a flower box header to the current file using the self-hosted parser
    /// </summary>
    public class AddFlowerBox : BaseRefactor
    {
        /// <summary>
        /// Gets the display name for this refactor
        /// </summary>
        public new static string RefactorName => "Add Flower Box";

        /// <summary>
        /// Gets the description for this refactor
        /// </summary>
        public new static string RefactorDescription => "Add a flower box header to the current file";

        public AddFlowerBox(AppRefiner.ScintillaEditor editor) : base(editor)
        {
        }

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

        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);
            InsertText(new SourcePosition(0, 1, 0), GenerateFlowerBoxHeader(), "Add flower box");
        }
    }
}