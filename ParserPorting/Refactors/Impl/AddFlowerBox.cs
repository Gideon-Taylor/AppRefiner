using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted;
using AppRefiner.Services;

namespace ParserPorting.Refactors.Impl
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
            InsertText(Zero, GenerateFlowerBoxHeader(), "Add flower box");
        }
    }
}