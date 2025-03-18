using Antlr4.Runtime;
using AppRefiner.Linters.Models;
using static AppRefiner.PeopleCode.PeopleCodeParser;

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

        public override void Reset()
        {
        }

        private void AddMissingHeaderReport()
        {
            AddReport(
                1,
                MISSING_HEADER_MESSAGE,
                ReportType.Warning,
                0,
                (0, 1)
            );
        }

        public override void EnterProgram(ProgramContext context)
        {
            if (Comments?.Count == 0)
            {
                AddMissingHeaderReport();
                return;
            }

            var firstComment = Comments?.First();

            if (firstComment.Type != PeopleCodeLexer.BLOCK_COMMENT_SLASH)
            {
                AddMissingHeaderReport();
                return;
            }

            if (firstComment.Line != 1)
            {
                AddMissingHeaderReport();
                return;
            }

            if (!firstComment.Text.StartsWith("/* ====="))
            {
                AddMissingHeaderReport();
                return;
            }
        }
    }
}
