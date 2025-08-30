using PeopleCodeParser.SelfHosted.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.Stylers
{
    internal class SyntaxErrors : BaseStyler
    {
        public override string Description => "Marks syntax errors in the code.";
        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);

            foreach(var error in Editor.ParserErrors)
            {
                AddIndicator(error.Location, IndicatorType.SQUIGGLE, 0x0000FFA0, error.Message);
            }

        }
    }
}
