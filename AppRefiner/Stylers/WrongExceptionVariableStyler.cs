using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using AppRefiner.Refactors.QuickFixes;

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Highlights use of exception variables from other catch blocks within a catch block.
    /// This addresses issue #43 by detecting potentially incorrect exception variable usage.
    /// </summary>
    public class WrongExceptionVariableStyler : BaseStyler
    {
        private const uint WARNING_COLOR = 0x0000FFA0; // Red squiggle color
        private CatchStatementNode? currentCatch;

        public override string Description => "Used wrong exception variable";

        public override void VisitProgram(ProgramNode node)
        {
            Reset();
            base.VisitProgram(node);
        }

        public override void VisitCatch(CatchStatementNode node)
        {
            currentCatch = node;
            base.VisitCatch(node);
            currentCatch = null;
        }

        public override void VisitIdentifier(IdentifierNode node)
        {
            if (node.IdentifierType == IdentifierType.UserVariable && currentCatch != null)
            {
                var varInfo = FindVariable(node.Name);
                if (varInfo != null && 
                    varInfo.Kind == VariableKind.Exception && 
                    varInfo.DeclarationNode is CatchStatementNode declaringCatch &&
                    declaringCatch != currentCatch)
                {
                    string tooltip = $"Using exception variable '{node.Name}' from different catch block. Use the one declared in this catch.";
                    var quickFixes = new List<(Type RefactorClass, string Description)>
                    {
                        (typeof(FixExceptionVariable), "Rename to correct exception variable")
                    };
                    AddIndicator(node.SourceSpan, IndicatorType.SQUIGGLE, WARNING_COLOR, tooltip, quickFixes);
                }
            }

            base.VisitIdentifier(node);
        }
    }
}
