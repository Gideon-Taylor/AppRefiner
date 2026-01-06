using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers
{
    internal class SyntaxErrors : BaseStyler
    {
        public override string Description => "Syntax errors";
        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);

            foreach (var error in Editor.ParserErrors)
            {
                AddIndicator(error.Location, IndicatorType.SQUIGGLE, 0x0000FFA0, error.Message);
            }

        }
    }
}
