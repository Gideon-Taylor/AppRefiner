using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Lexing;

namespace AppRefiner.Linters
{
    public class MultiLineRemCommentLinter : BaseLintRule
    {
        public override string LINTER_ID => "MULTILINE_REM";
        private const string MULTILINE_REM_MESSAGE = "REM comment spans multiple lines, possiblee missing semicolon termination.";

        public MultiLineRemCommentLinter()
        {
            Description = "Detects REM comments that span multiple lines.";
            Type = ReportType.Warning;
            Active = true;
        }

        public override void VisitProgram(ProgramNode node)
        {
            if (node.Comments == null || node.Comments.Count == 0)
                return;

            foreach (var comment in node.Comments)
            {
                // Check only for line comments (REM comments)
                if (comment.Type != TokenType.LineComment)
                    continue;

                // If line count is greater than 1 and no proper terminator, flag it
                if (comment.Text.Split("\n").Length > 1)
                {
                    AddReport(
                        1,
                        "REM comment spans multiple lines, possible missing semicolon termination.",
                        Type,
                        comment.SourceSpan.Start.Line,
                        comment.SourceSpan
                    );
                }
            }

            base.VisitProgram(node);
        }
    }
}
