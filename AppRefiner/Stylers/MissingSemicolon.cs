using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers
{
    internal class MissingSemicolon : BaseStyler
    {
        public override string Description => "Marks statements that require a semicolon but are missing it.";

        public override void VisitBlock(BlockNode node)
        {
            base.VisitBlock(node);

            foreach (var statement in node.Statements.SkipLast(1))
            {
                if (statement.HasSemicolon == false)
                {
                    AddIndicator(statement.SourceSpan, IndicatorType.SQUIGGLE, 0x0000FFA0, "Missing semicolon");
                }
            }
        }

    }
}
