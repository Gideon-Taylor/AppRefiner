using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Refactors
{
    class AddFlowerBox : BaseRefactor
    {
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
