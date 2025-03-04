using Antlr4.Runtime;
using AppRefiner.Linters.Models;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    public class FlowerBoxHeader : BaseLintRule
    {
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
            
            Reports?.Add(new Report
            {
                Type = ReportType.Warning,
                Line = 0,
                Span = (0, 1),
                Message = MISSING_HEADER_MESSAGE
            });
        }

        public override void EnterProgram(ProgramContext context)
        {
            var firstComment = Comments?.First();
            
            if (firstComment == null)
            {
                AddMissingHeaderReport();
                return;
            }

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
