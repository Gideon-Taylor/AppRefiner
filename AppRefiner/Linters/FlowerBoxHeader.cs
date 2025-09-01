using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;

namespace AppRefiner.Linters
{
    public class FlowerBoxHeader : BaseLintRule
    {
        public override string LINTER_ID => "FLOWERBOX";
        private const string MISSING_HEADER_MESSAGE = "File is missing a flowerbox header comment /* ===== */";

        public FlowerBoxHeader()
        {
            Description = "Validates that files start with a flowerbox header comment /* ===== */";
            Type = ReportType.Warning;
            Active = true;
        }

        private void AddMissingHeaderReport()
        {
            // Create a basic span for line 1
            var span = new SourceSpan(0, 10);

            AddReport(
                1,
                MISSING_HEADER_MESSAGE,
                ReportType.Warning,
                1, // Report at line 1
                span
            );
        }

        public override void VisitProgram(ProgramNode node)
        {
            if (node.Comments == null || node.Comments.Count == 0)
            {
                AddMissingHeaderReport();
                return;
            }

            var firstComment = node.Comments.First();

            if (firstComment == null)
            {
                AddMissingHeaderReport();
                return;
            }

            // Check if it's a block comment (/* */ style)
            if (firstComment.Type != TokenType.BlockComment)
            {
                AddMissingHeaderReport();
                return;
            }

            // Check if it's on line 1
            if (firstComment.SourceSpan.Start.Line != 1)
            {
                AddMissingHeaderReport();
                return;
            }

            // Check if it starts with the flowerbox pattern
            if (!firstComment.Text.StartsWith("/* ====="))
            {
                AddMissingHeaderReport();
                return;
            }

            base.VisitProgram(node);
        }
    }
}
