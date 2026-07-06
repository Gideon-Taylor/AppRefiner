using System.Globalization;
using System.Text;

namespace AppRefiner.Database
{
    /// <summary>
    /// A single alias entry parsed from tnsnames.ora (or one of its IFILE includes)
    /// </summary>
    public record TnsEntry(string Alias, string Descriptor, string SourceFile, bool FromInclude);

    /// <summary>
    /// Result of parsing a tnsnames.ora file, including entries from IFILE includes
    /// </summary>
    public class TnsParseResult
    {
        public List<TnsEntry> Entries { get; } = new();

        /// <summary>Include files that could not be read, formatted "path — error"</summary>
        public List<string> FailedIncludes { get; } = new();

        /// <summary>Non-fatal parse anomalies (unbalanced parens, cycles, depth limit, unreadable root file)</summary>
        public List<string> Warnings { get; } = new();
    }

    /// <summary>
    /// Parses tnsnames.ora files with IFILE include support. The managed ODP.NET
    /// driver ignores IFILE directives entirely (verified against 23.26.100), so
    /// AppRefiner resolves includes itself and connects with the full descriptor
    /// for aliases that live in included files.
    /// </summary>
    public static class TnsNamesParser
    {
        // Oracle documents up to three levels of IFILE nesting
        private const int MaxIncludeDepth = 3;

        public static TnsParseResult Parse(string tnsNamesPath)
        {
            var result = new TnsParseResult();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ParseFile(tnsNamesPath, 0, false, visited, seenAliases, result);
            return result;
        }

        private static void ParseFile(string path, int depth, bool fromInclude,
            HashSet<string> visited, HashSet<string> seenAliases, TnsParseResult result)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                RecordReadFailure(path, ex, fromInclude, result);
                return;
            }

            if (!visited.Add(fullPath))
            {
                result.Warnings.Add($"Skipped circular include of {fullPath}");
                return;
            }

            string content;
            try
            {
                content = File.ReadAllText(fullPath);
            }
            catch (Exception ex)
            {
                RecordReadFailure(fullPath, ex, fromInclude, result);
                return;
            }

            ParseContent(content, fullPath, depth, fromInclude, visited, seenAliases, result);
        }

        private static void RecordReadFailure(string path, Exception ex, bool fromInclude, TnsParseResult result)
        {
            if (fromInclude)
            {
                result.FailedIncludes.Add($"{path} — {ex.Message}");
            }
            else
            {
                result.Warnings.Add($"Could not read tnsnames.ora at {path}: {ex.Message}");
            }
        }

        private static void ParseContent(string content, string fullPath, int depth, bool fromInclude,
            HashSet<string> visited, HashSet<string> seenAliases, TnsParseResult result)
        {
            int pos = 0;
            while (pos < content.Length)
            {
                SkipInsignificant(content, ref pos);
                if (pos >= content.Length)
                {
                    break;
                }

                // Read the name part: everything up to '=' on the current line
                int nameStart = pos;
                int eq = -1;
                while (pos < content.Length && content[pos] != '\n')
                {
                    if (content[pos] == '#')
                    {
                        break;
                    }
                    if (content[pos] == '=')
                    {
                        eq = pos;
                        break;
                    }
                    pos++;
                }

                if (eq < 0)
                {
                    // No '=' on this line — not an entry; skip the line
                    SkipToEol(content, ref pos);
                    continue;
                }

                string namePart = content[nameStart..eq].Trim();
                pos = eq + 1;

                if (namePart.Length == 0)
                {
                    SkipToEol(content, ref pos);
                    continue;
                }

                // Skip spaces/tabs after '=' (but not newlines yet — a scalar value
                // like an IFILE path lives on the same line)
                while (pos < content.Length && (content[pos] == ' ' || content[pos] == '\t'))
                {
                    pos++;
                }

                bool valueOnSameLine = pos < content.Length
                    && content[pos] != '\n' && content[pos] != '\r' && content[pos] != '#';

                if (valueOnSameLine && content[pos] != '(')
                {
                    // Scalar value (e.g. IFILE=path)
                    int eol = content.IndexOf('\n', pos);
                    string value = eol < 0 ? content[pos..] : content[pos..eol];
                    int hash = value.IndexOf('#');
                    if (hash >= 0)
                    {
                        value = value[..hash];
                    }
                    pos = eol < 0 ? content.Length : eol + 1;

                    if (namePart.Equals("IFILE", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleInclude(value, fullPath, depth, visited, seenAliases, result);
                    }
                    // Other top-level scalar directives are ignored
                    continue;
                }

                // Allow the '(' to start on a following line
                SkipInsignificant(content, ref pos);
                if (pos >= content.Length || content[pos] != '(')
                {
                    result.Warnings.Add($"Entry '{namePart}' in {fullPath} has no descriptor; skipped");
                    continue;
                }

                string? descriptor = ScanBalanced(content, ref pos);
                if (descriptor == null)
                {
                    result.Warnings.Add($"Unbalanced parentheses in {fullPath}; entry '{namePart}' skipped");
                    break; // EOF reached
                }

                foreach (string rawAlias in namePart.Split(','))
                {
                    string alias = rawAlias.Trim();
                    if (alias.Length == 0)
                    {
                        continue;
                    }
                    if (!seenAliases.Add(alias))
                    {
                        continue; // first definition wins (Oracle resolution order)
                    }
                    result.Entries.Add(new TnsEntry(alias, descriptor, fullPath, fromInclude));
                }
            }
        }

        /// <summary>
        /// Resolves and parses one IFILE include. Only the single-path form
        /// (IFILE=path) is supported — Oracle's parenthesized comma-list form
        /// (IFILE=(a.ora, b.ora)) is not handled and would be skipped.
        /// </summary>
        private static void HandleInclude(string rawPath, string includingFile, int depth,
            HashSet<string> visited, HashSet<string> seenAliases, TnsParseResult result)
        {
            string cleaned = CleanIncludePath(rawPath);
            if (cleaned.Length == 0)
            {
                result.Warnings.Add($"Empty IFILE path in {includingFile}");
                return;
            }

            if (depth + 1 > MaxIncludeDepth)
            {
                result.Warnings.Add(
                    $"IFILE nesting deeper than {MaxIncludeDepth} levels; skipped {cleaned} (included from {includingFile})");
                return;
            }

            if (!Path.IsPathRooted(cleaned))
            {
                string? baseDir = Path.GetDirectoryName(includingFile);
                if (!string.IsNullOrEmpty(baseDir))
                {
                    cleaned = Path.Combine(baseDir, cleaned);
                }
            }

            ParseFile(cleaned, depth + 1, true, visited, seenAliases, result);
        }

        /// <summary>
        /// Trims whitespace, surrounding quotes, and invisible Unicode format
        /// characters (zero-width space U+200B, BOM U+FEFF) that show up when the
        /// file was edited with rich-text tooling — observed in a real customer file.
        /// </summary>
        private static string CleanIncludePath(string rawPath)
        {
            var sb = new StringBuilder(rawPath.Length);
            foreach (char c in rawPath)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
                {
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString().Trim().Trim('"', '\'').Trim();
        }

        /// <summary>
        /// Scans a balanced (...) block starting at content[pos] == '('.
        /// Returns the block collapsed to single-line form (whitespace runs become
        /// one space), or null if EOF is hit before the parens balance.
        /// Comments (# to end of line) are skipped.
        /// </summary>
        private static string? ScanBalanced(string content, ref int pos)
        {
            var sb = new StringBuilder();
            int parenDepth = 0;
            bool lastWasSpace = false;

            while (pos < content.Length)
            {
                char c = content[pos];

                if (c == '#')
                {
                    SkipToEol(content, ref pos);
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace && sb.Length > 0)
                    {
                        sb.Append(' ');
                        lastWasSpace = true;
                    }
                    pos++;
                    continue;
                }

                sb.Append(c);
                lastWasSpace = false;

                if (c == '(')
                {
                    parenDepth++;
                }
                else if (c == ')')
                {
                    parenDepth--;
                    if (parenDepth == 0)
                    {
                        pos++;
                        return sb.ToString().Trim();
                    }
                }

                pos++;
            }

            return null; // unbalanced at EOF
        }

        /// <summary>Skips whitespace (including newlines) and comment lines</summary>
        private static void SkipInsignificant(string content, ref int pos)
        {
            while (pos < content.Length)
            {
                char c = content[pos];
                if (char.IsWhiteSpace(c))
                {
                    pos++;
                    continue;
                }
                if (c == '#')
                {
                    SkipToEol(content, ref pos);
                    continue;
                }
                break;
            }
        }

        private static void SkipToEol(string content, ref int pos)
        {
            while (pos < content.Length && content[pos] != '\n')
            {
                pos++;
            }
            if (pos < content.Length)
            {
                pos++; // consume the newline
            }
        }
    }
}
