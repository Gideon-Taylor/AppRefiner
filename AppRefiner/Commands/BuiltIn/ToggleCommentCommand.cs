using AppRefiner.Commands;
using System.Text;
using System.Windows.Forms;

namespace AppRefiner.Commands.BuiltIn
{
    public class ToggleCommentCommand : BaseCommand
    {
        public override string CommandName => "Editor: Toggle Line Comment";
        public override string CommandDescription => "Comment or uncomment selected lines using block comment markers";
        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control,
                Keys.OemQuestion, // /
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control, Keys.OemQuestion));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor == null)
                return;

            var (lines, startPos, endPos) = ScintillaManager.GetSelectedLines(context.ActiveEditor);
            if (lines == null || lines.Count == 0)
                return;

            // Determine if all non-whitespace lines are commented
            bool allCommented = true;
            bool hasNonWhitespace = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                hasNonWhitespace = true;
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("<* ") || !trimmed.TrimEnd().EndsWith(" *>"))
                {
                    allCommented = false;
                    break;
                }
            }

            if (!hasNonWhitespace)
                return;

            // Find minimum indentation across all non-whitespace lines
            int minIndent = int.MaxValue;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int indent = 0;
                while (indent < line.Length && char.IsWhiteSpace(line[indent]))
                    indent++;
                if (indent < minIndent)
                    minIndent = indent;
            }

            // For commenting, find the max inner content length to align closing *>
            int maxInnerLen = 0;
            if (!allCommented)
            {
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Inner content = extra indent padding + trimmed content
                    int lineIndent = 0;
                    while (lineIndent < line.Length && char.IsWhiteSpace(line[lineIndent]))
                        lineIndent++;

                    int innerLen = (lineIndent - minIndent) + line.Substring(lineIndent).TrimEnd().Length;
                    if (innerLen > maxInnerLen)
                        maxInnerLen = innerLen;
                }
            }

            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                    sb.Append("\n");

                var line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    sb.Append(line);
                    continue;
                }

                if (allCommented)
                {
                    // Uncomment: remove <* from start and *> from end
                    int contentStart = line.IndexOf("<* ");
                    string leading = line.Substring(0, contentStart);
                    string afterMarker = line.Substring(contentStart + 3);

                    // Remove trailing *>
                    int closingIndex = afterMarker.LastIndexOf(" *>");
                    string inner = afterMarker.Substring(0, closingIndex).TrimEnd() + afterMarker.Substring(closingIndex + 3);

                    sb.Append(leading + inner);
                }
                else
                {
                    // Comment: place <* at minimum indent, pad content to preserve alignment
                    int lineIndent = 0;
                    while (lineIndent < line.Length && char.IsWhiteSpace(line[lineIndent]))
                        lineIndent++;

                    string leading = line.Substring(0, minIndent);
                    int extraIndent = lineIndent - minIndent;
                    string innerPadding = extraIndent > 0 ? new string(' ', extraIndent) : "";
                    string content = line.Substring(lineIndent).TrimEnd();

                    int currentInnerLen = extraIndent + content.Length;
                    string closingPadding = currentInnerLen < maxInnerLen
                        ? new string(' ', maxInnerLen - currentInnerLen) : "";

                    sb.Append(leading + "<* " + innerPadding + content + closingPadding + " *>");
                }
            }

            ScintillaManager.ReplaceTextRange(context.ActiveEditor, startPos, endPos, sb.ToString());
        }
    }
}
