using DiffPlex;
using DiffPlex.Model;
using System.Text;

namespace AppRefiner.Snapshots
{
    /// <summary>
    /// Kind of a line within a diff result
    /// </summary>
    public enum DiffLineKind
    {
        Context,
        Added,
        Removed
    }

    /// <summary>
    /// A single line in a diff result
    /// </summary>
    public class DiffLine
    {
        public DiffLineKind Kind { get; }
        public string Text { get; }

        public DiffLine(DiffLineKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }

    /// <summary>
    /// A contiguous group of changed lines with surrounding context lines
    /// </summary>
    public class DiffHunk
    {
        public int OldStart { get; set; }
        public int OldCount { get; set; }
        public int NewStart { get; set; }
        public int NewCount { get; set; }
        public List<DiffLine> Lines { get; } = new();

        /// <summary>
        /// Unified diff hunk header, e.g. "@@ -45,7 +45,8 @@". Follows the git
        /// convention of referencing the line before the hunk when one side
        /// contributes no lines.
        /// </summary>
        public string Header
        {
            get
            {
                int oldStart = OldCount == 0 ? OldStart - 1 : OldStart;
                int newStart = NewCount == 0 ? NewStart - 1 : NewStart;
                return $"@@ -{oldStart},{OldCount} +{newStart},{NewCount} @@";
            }
        }
    }

    /// <summary>
    /// Added/removed line counts between two versions of a text
    /// </summary>
    public class DiffStats
    {
        public int Added { get; set; }
        public int Removed { get; set; }
    }

    /// <summary>
    /// Builds line diffs, collapsed hunks and unified diff text from two
    /// versions of a text. Shared by the snapshot history dialog's preview
    /// pane, its per-snapshot change stats, and its Copy Diff action.
    /// </summary>
    public static class UnifiedDiffBuilder
    {
        private static readonly IDiffer differ = new Differ();

        /// <summary>
        /// Counts lines added and removed going from oldText to newText
        /// </summary>
        public static DiffStats ComputeStats(string oldText, string newText)
        {
            var result = differ.CreateLineDiffs(oldText ?? string.Empty, newText ?? string.Empty, false);
            var stats = new DiffStats();

            foreach (var block in result.DiffBlocks)
            {
                stats.Removed += block.DeleteCountA;
                stats.Added += block.InsertCountB;
            }

            return stats;
        }

        /// <summary>
        /// Produces the full interleaved line list: context lines from the old
        /// text with removed lines followed by their replacement added lines.
        /// </summary>
        public static List<DiffLine> BuildLines(string oldText, string newText)
        {
            var result = differ.CreateLineDiffs(oldText ?? string.Empty, newText ?? string.Empty, false);
            var lines = new List<DiffLine>();
            int aIndex = 0;

            foreach (var block in result.DiffBlocks)
            {
                while (aIndex < block.DeleteStartA)
                {
                    lines.Add(new DiffLine(DiffLineKind.Context, result.PiecesOld[aIndex]));
                    aIndex++;
                }

                for (int i = 0; i < block.DeleteCountA; i++)
                {
                    lines.Add(new DiffLine(DiffLineKind.Removed, result.PiecesOld[block.DeleteStartA + i]));
                }

                for (int i = 0; i < block.InsertCountB; i++)
                {
                    lines.Add(new DiffLine(DiffLineKind.Added, result.PiecesNew[block.InsertStartB + i]));
                }

                aIndex = block.DeleteStartA + block.DeleteCountA;
            }

            while (aIndex < result.PiecesOld.Count)
            {
                lines.Add(new DiffLine(DiffLineKind.Context, result.PiecesOld[aIndex]));
                aIndex++;
            }

            return lines;
        }

        /// <summary>
        /// Groups changes into hunks with the given number of context lines.
        /// Changes whose context regions touch or overlap share a hunk.
        /// </summary>
        public static List<DiffHunk> BuildHunks(string oldText, string newText, int context = 3)
        {
            var lines = BuildLines(oldText, newText);
            var hunks = new List<DiffHunk>();

            // 1-based old/new line numbers at each position in the line list
            var oldNums = new int[lines.Count];
            var newNums = new int[lines.Count];
            int oldNum = 1, newNum = 1;
            for (int i = 0; i < lines.Count; i++)
            {
                oldNums[i] = oldNum;
                newNums[i] = newNum;
                if (lines[i].Kind != DiffLineKind.Added) oldNum++;
                if (lines[i].Kind != DiffLineKind.Removed) newNum++;
            }

            int pos = 0;
            while (pos < lines.Count)
            {
                if (lines[pos].Kind == DiffLineKind.Context)
                {
                    pos++;
                    continue;
                }

                int start = Math.Max(pos - context, 0);
                int lastChange = pos;
                int scan = pos + 1;
                while (scan < lines.Count && scan - lastChange <= context * 2 + 1)
                {
                    if (lines[scan].Kind != DiffLineKind.Context)
                    {
                        lastChange = scan;
                    }
                    scan++;
                }
                int end = Math.Min(lastChange + context, lines.Count - 1);

                var hunk = new DiffHunk { OldStart = oldNums[start], NewStart = newNums[start] };
                for (int i = start; i <= end; i++)
                {
                    hunk.Lines.Add(lines[i]);
                    if (lines[i].Kind != DiffLineKind.Added) hunk.OldCount++;
                    if (lines[i].Kind != DiffLineKind.Removed) hunk.NewCount++;
                }
                hunks.Add(hunk);

                pos = end + 1;
            }

            return hunks;
        }

        /// <summary>
        /// Formats hunks as unified diff text with ---/+++ header lines.
        /// Always emits the header lines, even when there are no hunks.
        /// </summary>
        public static string FormatUnifiedDiff(string oldLabel, string newLabel, List<DiffHunk> hunks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- {oldLabel}");
            sb.AppendLine($"+++ {newLabel}");

            foreach (var hunk in hunks)
            {
                sb.AppendLine(hunk.Header);
                foreach (var line in hunk.Lines)
                {
                    char prefix = line.Kind switch
                    {
                        DiffLineKind.Added => '+',
                        DiffLineKind.Removed => '-',
                        _ => ' '
                    };
                    sb.AppendLine($"{prefix}{line.Text}");
                }
            }

            return sb.ToString();
        }
    }
}
