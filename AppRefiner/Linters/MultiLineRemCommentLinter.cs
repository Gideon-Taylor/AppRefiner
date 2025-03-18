using Antlr4.Runtime;
using AppRefiner.Linters.Models;
using System;
using System.Text.RegularExpressions;
using static AppRefiner.PeopleCode.PeopleCodeParser;

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

        public override void Reset()
        {
            // Nothing to reset
        }

        public override void EnterProgram(ProgramContext context)
        {
            if (Comments == null || Comments.Count == 0)
                return;

            foreach (var comment in Comments)
            {
                // Check only for line comments
                if (comment.Type != PeopleCodeLexer.LINE_COMMENT)
                    continue;

                // If line count is greater than 1 and no proper terminator, flag it
                if (comment.Text.Split("\n").Length > 1)
                {
                    Reports?.Add(AddReport(
                        1,
                        "REM comment spans multiple lines, possible missing semicolon termination.",
                        Type,
                        comment.Line,
                        (comment.StartIndex, comment.StopIndex)
                    ));
                }
            }
        }
    }
}
