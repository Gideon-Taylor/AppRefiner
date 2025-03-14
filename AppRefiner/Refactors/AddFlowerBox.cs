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
        private const string FLOWER_BOX_HEADER = 
@"/* =======================================================================================
 Change Log.

 Date        Modified By  Description
 ----------  -----------  --------------------------------------------------------------        
 01/01/1900  Your Name    Initial creation of the file.
 ======================================================================================= */
";
        public override void EnterProgram(ProgramContext context)
        {
            base.EnterProgram(context);
            InsertText(0, FLOWER_BOX_HEADER, "Add flower box");
        }
    }
}
