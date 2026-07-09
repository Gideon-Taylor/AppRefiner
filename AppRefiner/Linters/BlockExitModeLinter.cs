using PeopleCodeParser.SelfHosted.Analysis;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Diagnostic linter that runs <see cref="CompletionAnalyzer"/> and annotates the last
    /// line of every <see cref="BlockNode"/> with its determined <see cref="ExitMode"/> set.
    /// Inactive by default; run via the command palette ("Lint: Annotate block exit modes")
    /// or enable it and use Editor: Lint Current Code.
    /// </summary>
    public class BlockExitModeLinter : BaseLintRule
    {
        public override string LINTER_ID => "BLOCK_EXIT_MODE";

        public BlockExitModeLinter()
        {
            Description = "Annotate block exit modes";
            Type = ReportType.Info;
            Active = false;
        }

        public override void VisitProgram(ProgramNode node)
        {
            // Analyze each block as its own root so method/function/Main bodies and every
            // nested Then/Else/When/loop/try body is annotated. Nested re-analysis is
            // correct and cheap enough for interactive linting.
            foreach (var block in node.FindDescendants<BlockNode>())
            {
                ExitMode mode = CompletionAnalyzer.Analyze(block);
                int line = GetLastLine(block);

                AddReport(
                    1,
                    $"ExitMode: {FormatExitMode(mode)}",
                    Type,
                    line,
                    block.SourceSpan);
            }

            // No base.VisitProgram — we walk blocks via FindDescendants and do not need
            // ScopedAstVisitor scope tracking for this diagnostic.
        }

        /// <summary>
        /// Last line of the block for Scintilla annotation placement (0-based).
        /// Prefer the last statement when present so empty exclusive end positions
        /// on the following line do not push the note past the block's content.
        /// </summary>
        private static int GetLastLine(BlockNode block)
        {
            if (block.Statements.Count > 0)
                return block.Statements[^1].SourceSpan.End.Line;

            if (block.SourceSpan.IsValid)
                return block.SourceSpan.End.Line;

            return block.SourceSpan.Start.Line;
        }

        /// <summary>
        /// Formats the [Flags] set as a stable pipe-joined list (e.g. "Normal|Return").
        /// </summary>
        private static string FormatExitMode(ExitMode mode)
        {
            if (mode == ExitMode.None)
                return "None";

            // Enum.ToString() on flags yields "Normal, Return" — normalize for readability.
            return mode.ToString().Replace(", ", "|");
        }
    }
}
