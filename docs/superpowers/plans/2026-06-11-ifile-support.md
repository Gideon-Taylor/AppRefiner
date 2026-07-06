# tnsnames.ora IFILE Support + Connect DB Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make AppRefiner work with IFILE-based tnsnames.ora setups (the managed ODP.NET driver ignores IFILE) and stop the Connect DB button from silently doing nothing.

**Architecture:** A new pure `TnsNamesParser` static class parses tnsnames.ora with balanced-paren scanning and recursive IFILE resolution. `OracleDbConnection` exposes the parse result; `DBConnectDialog` populates its dropdown from it asynchronously and, for aliases that came from an include, passes the full connect descriptor as the ODP.NET Data Source (mirroring the existing LDAP `ldapServiceDescriptors` pattern). MainForm's Connect DB button drops its unnecessary active-editor requirement and reports when no Application Designer session exists.

**Tech Stack:** .NET 8 (net8.0-windows7.0), WinForms, Oracle.ManagedDataAccess.Core 23.26.100.

**Spec:** `docs/superpowers/specs/2026-06-11-ifile-support-design.md`

**Build and verification notes for the engineer:**
- Build with `dotnet build AppRefiner/AppRefiner.csproj` (~5 s). Do NOT build the whole solution — `AppRefinerHook` is a C++ project that needs MSBuild/VS.
- **No test project is added to the repo** (explicit project decision — do not add one). The parser is verified with a throwaway console harness in `C:\temp\tnsparse-check` that is never committed.
- `Debug` is AppRefiner's custom logger (`AppRefiner/Debug.cs`), not `System.Diagnostics.Debug`. Use `Debug.Log(...)` / `Debug.LogError(...)`.
- The AppRefiner csproj is strong-name signed with a key at `D:\Work\AppRefinerStrongKey\AppRefiner.snk` — that path is intentional and exists on this machine. Don't touch it.

---

### Task 1: Implement TnsNamesParser (verified via throwaway harness)

**Files:**
- Create: `AppRefiner/Database/TnsNamesParser.cs`
- Create (NOT in repo, never committed): `C:\temp\tnsparse-check\tnsparse-check.csproj`, `C:\temp\tnsparse-check\Program.cs`

- [ ] **Step 1: Write the parser**

Create `AppRefiner/Database/TnsNamesParser.cs` with this exact content:

```csharp
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
```

- [ ] **Step 2: Build AppRefiner**

```powershell
dotnet build AppRefiner/AppRefiner.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Create the throwaway verification harness (outside the repo)**

Create `C:\temp\tnsparse-check\tnsparse-check.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows7.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="C:\Users\tslat\repos\GitHub\AppRefiner\AppRefiner\AppRefiner.csproj" />
  </ItemGroup>
</Project>
```

Create `C:\temp\tnsparse-check\Program.cs` (note: the zero-width characters in check 14 are written as `\u200B` / `\uFEFF` escapes — keep them as escapes):

```csharp
using AppRefiner.Database;

int passed = 0, failed = 0;
string root = Path.Combine(Path.GetTempPath(), "tnsparse_check_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

string W(string name, string content)
{
    string p = Path.Combine(root, name);
    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
    File.WriteAllText(p, content);
    return p;
}

void Check(string label, bool condition)
{
    if (condition) { passed++; Console.WriteLine($"PASS  {label}"); }
    else { failed++; Console.WriteLine($"FAIL  {label}"); }
}

// 1-2: single entry, descriptor content, FromInclude=false
var r = TnsNamesParser.Parse(W("t1\\tnsnames.ora",
    "MYDB = (DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=dbhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=MYDB)))"));
Check("single entry alias", r.Entries.Count == 1 && r.Entries[0].Alias == "MYDB" && !r.Entries[0].FromInclude);
Check("descriptor content", r.Entries[0].Descriptor.StartsWith("(DESCRIPTION=") && r.Entries[0].Descriptor.Contains("(SERVICE_NAME=MYDB)"));

// 3: multi-line descriptor collapsed to one line
r = TnsNamesParser.Parse(W("t2\\tnsnames.ora",
    "MYDB =\r\n  (DESCRIPTION =\r\n    (ADDRESS = (PROTOCOL = TCP)(HOST = dbhost)(PORT = 1521))\r\n    (CONNECT_DATA = (SERVICE_NAME = MYDB))\r\n  )\r\n"));
Check("multi-line collapsed", r.Entries.Count == 1 && !r.Entries[0].Descriptor.Contains('\n') && !r.Entries[0].Descriptor.Contains('\r'));

// 4: comments ignored (full-line and trailing)
r = TnsNamesParser.Parse(W("t3\\tnsnames.ora",
    "# central file\r\nMYDB = (DESCRIPTION=(HOST=a)) # trailing\r\n# ANOTHER = (DESCRIPTION=(HOST=commented_out))\r\n"));
Check("comments ignored", r.Entries.Count == 1 && !r.Entries[0].Descriptor.Contains("commented_out"));

// 5: multiple entries including dotted alias and paren-on-next-line form
r = TnsNamesParser.Parse(W("t4\\tnsnames.ora",
    "DB1 = (DESCRIPTION=(HOST=one))\r\nDB2 =\r\n  (DESCRIPTION=(HOST=two))\r\nDB3.WORLD = (DESCRIPTION=(HOST=three))\r\n"));
Check("multiple entries", r.Entries.Select(e => e.Alias).SequenceEqual(new[] { "DB1", "DB2", "DB3.WORLD" }));

// 6: missing root file -> empty result + warning (not a FailedInclude)
r = TnsNamesParser.Parse(Path.Combine(root, "nope.ora"));
Check("missing root", r.Entries.Count == 0 && r.FailedIncludes.Count == 0 && r.Warnings.Count > 0);

// 7: multi-alias entry
r = TnsNamesParser.Parse(W("t5\\tnsnames.ora", "MYDB, MYDB.WORLD = (DESCRIPTION=(HOST=a))"));
Check("multi-alias", r.Entries.Count == 2 && r.Entries[0].Alias == "MYDB" && r.Entries[1].Alias == "MYDB.WORLD"
    && r.Entries[0].Descriptor == r.Entries[1].Descriptor);

// 8: duplicate alias, first definition wins
r = TnsNamesParser.Parse(W("t6\\tnsnames.ora",
    "MYDB = (DESCRIPTION=(HOST=first))\r\nMYDB = (DESCRIPTION=(HOST=second))\r\n"));
Check("duplicate first wins", r.Entries.Count == 1 && r.Entries[0].Descriptor.Contains("first"));

// 9: unbalanced parens at EOF -> earlier entries kept + warning
r = TnsNamesParser.Parse(W("t7\\tnsnames.ora",
    "GOOD = (DESCRIPTION=(HOST=ok))\r\nBROKEN = (DESCRIPTION=(HOST=oops)\r\n"));
Check("unbalanced EOF", r.Entries.Count == 1 && r.Entries[0].Alias == "GOOD" && r.Warnings.Any(w => w.Contains("BROKEN")));

// 10: IFILE absolute path, FromInclude flag, no literal IFILE entry
string inc = W("t8\\central\\tnsnames.ora", "REMOTEDB = (DESCRIPTION=(HOST=remote))");
r = TnsNamesParser.Parse(W("t8\\tnsnames.ora", $"IFILE={inc}\r\nLOCALDB = (DESCRIPTION=(HOST=local))"));
Check("ifile absolute", r.Entries.Count == 2
    && r.Entries.Single(e => e.Alias == "REMOTEDB").FromInclude
    && !r.Entries.Single(e => e.Alias == "LOCALDB").FromInclude
    && !r.Entries.Any(e => e.Alias.Equals("IFILE", StringComparison.OrdinalIgnoreCase)));

// 11: relative IFILE path resolved against including file
W("t9\\sub\\extra.ora", "SUBDB = (DESCRIPTION=(HOST=sub))");
r = TnsNamesParser.Parse(W("t9\\tnsnames.ora", "IFILE=sub\\extra.ora"));
Check("ifile relative", r.Entries.Count == 1 && r.Entries[0].Alias == "SUBDB" && r.Entries[0].FromInclude);

// 12: quoted IFILE path
inc = W("t10\\included.ora", "QDB = (DESCRIPTION=(HOST=q))");
r = TnsNamesParser.Parse(W("t10\\tnsnames.ora", $"IFILE=\"{inc}\""));
Check("ifile quoted", r.Entries.Count == 1);

// 13: forward slashes
inc = W("t11\\included.ora", "FDB = (DESCRIPTION=(HOST=f))");
r = TnsNamesParser.Parse(W("t11\\tnsnames.ora", $"IFILE={inc.Replace('\\', '/')}"));
Check("ifile forward slashes", r.Entries.Count == 1);

// 14: zero-width chars stripped (U+200B, U+FEFF — observed in a real customer file)
inc = W("t12\\included.ora", "ZDB = (DESCRIPTION=(HOST=z))");
r = TnsNamesParser.Parse(W("t12\\tnsnames.ora", $"IFILE=\u200B{inc}\u200B\uFEFF"));
Check("ifile zero-width stripped", r.Entries.Count == 1 && r.Entries[0].Alias == "ZDB");

// 15: IFILE keyword is case-insensitive
inc = W("t13\\included.ora", "CDB = (DESCRIPTION=(HOST=c))");
r = TnsNamesParser.Parse(W("t13\\tnsnames.ora", $"ifile = {inc}"));
Check("ifile case-insensitive", r.Entries.Count == 1);

// 16: duplicate alias across files — main file wins (document order)
inc = W("t14\\included.ora", "MYDB = (DESCRIPTION=(HOST=fromInclude))");
r = TnsNamesParser.Parse(W("t14\\tnsnames.ora", $"MYDB = (DESCRIPTION=(HOST=fromMain))\r\nIFILE={inc}"));
Check("dup across files first wins", r.Entries.Count == 1 && r.Entries[0].Descriptor.Contains("fromMain") && !r.Entries[0].FromInclude);

// 17: missing include -> FailedIncludes, remaining content still parsed
r = TnsNamesParser.Parse(W("t15\\tnsnames.ora",
    $"IFILE={Path.Combine(root, "t15", "missing.ora")}\r\nLOCALDB = (DESCRIPTION=(HOST=local))"));
Check("missing include", r.Entries.Count == 1 && r.Entries[0].Alias == "LOCALDB"
    && r.FailedIncludes.Count == 1 && r.FailedIncludes[0].Contains("missing.ora"));

// 18: nesting depth limited to 3
W("t16\\level4.ora", "DEEP4 = (DESCRIPTION=(HOST=four))");
W("t16\\level3.ora", "DEEP3 = (DESCRIPTION=(HOST=three))\r\nIFILE=level4.ora");
W("t16\\level2.ora", "DEEP2 = (DESCRIPTION=(HOST=two))\r\nIFILE=level3.ora");
W("t16\\level1.ora", "DEEP1 = (DESCRIPTION=(HOST=one))\r\nIFILE=level2.ora");
r = TnsNamesParser.Parse(W("t16\\tnsnames.ora", "IFILE=level1.ora"));
var aliases = r.Entries.Select(e => e.Alias).ToList();
Check("depth limit 3", aliases.Contains("DEEP1") && aliases.Contains("DEEP2") && aliases.Contains("DEEP3")
    && !aliases.Contains("DEEP4") && r.Warnings.Any(w => w.Contains("level4.ora")));

// 19: circular include terminates with warning
string aP = Path.Combine(root, "t17", "a.ora");
string bP = Path.Combine(root, "t17", "b.ora");
W("t17\\a.ora", $"ADB = (DESCRIPTION=(HOST=a))\r\nIFILE={bP}");
W("t17\\b.ora", $"BDB = (DESCRIPTION=(HOST=b))\r\nIFILE={aP}");
r = TnsNamesParser.Parse(aP);
Check("circular include", r.Entries.Count == 2 && r.Warnings.Any(w => w.Contains("circular", StringComparison.OrdinalIgnoreCase)));

Console.WriteLine($"\n{passed} passed, {failed} failed");
try { Directory.Delete(root, true); } catch { /* best effort */ }
return failed == 0 ? 0 : 1;
```

- [ ] **Step 4: Run the harness**

```powershell
dotnet run --project C:\temp\tnsparse-check
```

Expected: 19 lines starting with `PASS`, final line `19 passed, 0 failed`, exit code 0. If any check fails, fix the parser (not the harness) unless the harness contradicts the spec.

- [ ] **Step 5: Commit (parser only — the harness stays out of the repo)**

```powershell
git add AppRefiner/Database/TnsNamesParser.cs
git commit -m "feat: add TnsNamesParser with IFILE include support"
```

`git status --short` must show no other staged/untracked repo files from this task.

---

### Task 2: Expose parse results from OracleDbConnection

**Files:**
- Modify: `AppRefiner/Database/OracleDbConnection.cs:152-188` (the `GetAllTnsNames` method)

- [ ] **Step 1: Replace GetAllTnsNames with GetTnsEntries + thin wrapper**

In `AppRefiner/Database/OracleDbConnection.cs`, replace the entire existing `GetAllTnsNames()` method (lines 152–188, the one using `Regex`) with:

```csharp
/// <summary>
/// Gets all TNS entries from the tnsnames.ora file, following IFILE includes.
/// The managed ODP.NET driver does not resolve IFILE itself, so include
/// failures and warnings are logged here for diagnosis.
/// </summary>
/// <returns>The parse result (empty if no tnsnames.ora could be located)</returns>
public static TnsParseResult GetTnsEntries()
{
    string? tnsNamesPath = GetTnsNamesPath();
    if (string.IsNullOrEmpty(tnsNamesPath))
    {
        return new TnsParseResult();
    }

    TnsParseResult result = TnsNamesParser.Parse(tnsNamesPath);

    foreach (string failure in result.FailedIncludes)
    {
        Debug.Log($"tnsnames.ora include could not be read: {failure}");
    }
    foreach (string warning in result.Warnings)
    {
        Debug.Log($"tnsnames.ora parse warning: {warning}");
    }

    return result;
}

/// <summary>
/// Gets all TNS names from the tnsnames.ora file (including IFILE includes)
/// </summary>
/// <returns>A list of TNS names</returns>
public static List<string> GetAllTnsNames()
{
    return GetTnsEntries().Entries.Select(e => e.Alias).ToList();
}
```

Do NOT change `GetTnsNamesPath()` — its resolution order (Settings TNS_ADMIN → env TNS_ADMIN → ORACLE_HOME) stays as is. Note `GetAllTnsNames` previously checked `File.Exists` and silently returned an empty list for a missing file; the parser now reports that as a logged warning, which the spec requires.

- [ ] **Step 2: Remove the now-unused Regex using (if unused)**

Check whether `System.Text.RegularExpressions` is still referenced anywhere else in `OracleDbConnection.cs`; if not, delete the `using System.Text.RegularExpressions;` line.

- [ ] **Step 3: Build**

```powershell
dotnet build AppRefiner/AppRefiner.csproj
```

Expected: build succeeds with no new warnings.

- [ ] **Step 4: Commit**

```powershell
git add AppRefiner/Database/OracleDbConnection.cs
git commit -m "feat: expose IFILE-resolved TNS entries from OracleDbConnection"
```

---

### Task 3: Descriptor-based connect + include-failure hint in DBConnectDialog

**Files:**
- Modify: `AppRefiner/Dialogs/DBConnectDialog.cs` (fields ~line 57, hint label init ~line 199, `UpdateUIForConnectionType` ~line 703, `ApplyFormLayout` ~line 752, `LoadAllDatabaseConnections` ~line 890, `ConnectButton_Click` Oracle branches ~lines 1066 and 1086)

All locations below are in `AppRefiner/Dialogs/DBConnectDialog.cs`. Line numbers are pre-change approximations — match on the shown code, not the numbers.

- [ ] **Step 1: Add fields**

Directly below the existing field

```csharp
private readonly Dictionary<string, string> ldapServiceDescriptors = new(StringComparer.OrdinalIgnoreCase);
```

add:

```csharp
// Descriptors for aliases that came from a tnsnames.ora IFILE include — the
// managed ODP.NET driver cannot resolve those aliases itself, so we connect
// with the full descriptor instead (mirrors ldapServiceDescriptors)
private readonly Dictionary<string, string> tnsServiceDescriptors = new(StringComparer.OrdinalIgnoreCase);
private int tnsFailedIncludeCount;
```

- [ ] **Step 2: Enlarge the hint label**

In `InitializeComponent`, change the dbNameHintLabel size line from:

```csharp
this.dbNameHintLabel.Size = new Size(250, 20);
```

to:

```csharp
this.dbNameHintLabel.Size = new Size(260, 30);
```

(The label is currently never shown anywhere — it is being repurposed as the include-failure warning. Two-line height accommodates the warning text at 8pt.)

- [ ] **Step 3: Rewrite LoadAllDatabaseConnections**

Replace the body of `LoadAllDatabaseConnections()` (currently calls `OracleDbConnection.GetAllTnsNames()`) with:

```csharp
/// <summary>
/// Loads all available database connections for smart detection
/// </summary>
private void LoadAllDatabaseConnections()
{
    try
    {
        // Load Oracle TNS entries (follows IFILE includes; the managed driver does not)
        var tnsResult = OracleDbConnection.GetTnsEntries();
        oracleNames = tnsResult.Entries.Select(e => e.Alias).ToList();

        tnsServiceDescriptors.Clear();
        foreach (var entry in tnsResult.Entries)
        {
            // Only aliases from IFILE includes need descriptor-based connection;
            // aliases in the main tnsnames.ora resolve through the driver as before
            if (entry.FromInclude)
            {
                tnsServiceDescriptors.TryAdd(entry.Alias, entry.Descriptor);
            }
        }
        tnsFailedIncludeCount = tnsResult.FailedIncludes.Count;

        // Load SQL Server DSNs (both System and User)
        sqlServerDsns = SqlServerDbConnection.GetAvailableDsns();
    }
    catch (Exception ex)
    {
        Debug.LogError($"Error loading database connections: {ex.Message}");

        // Fallback to empty lists
        oracleNames = new List<string>();
        sqlServerDsns = new List<string>();
        tnsServiceDescriptors.Clear();
        tnsFailedIncludeCount = 0;
    }
}
```

- [ ] **Step 4: Show the warning in UpdateUIForConnectionType**

In `UpdateUIForConnectionType()`, replace the single line:

```csharp
dbNameHintLabel.Visible = false;
```

with:

```csharp
bool showTnsWarning = isOracle && !isLdap && tnsFailedIncludeCount > 0;
if (showTnsWarning)
{
    dbNameHintLabel.Text = tnsFailedIncludeCount == 1
        ? "1 tnsnames include file could not be read — see Debug Log"
        : $"{tnsFailedIncludeCount} tnsnames include files could not be read — see Debug Log";
    dbNameHintLabel.ForeColor = Color.DarkOrange;
}
dbNameHintLabel.Visible = showTnsWarning;
```

(Note: `isOracle` and `isLdap` are existing locals declared at the top of this method — keep this code after their declarations.)

- [ ] **Step 5: Reserve layout space for the warning in ApplyFormLayout**

In `ApplyFormLayout(...)`, replace the dbName block:

```csharp
if (!isLdap)
{
    dbNameLabel.Location = new Point(labelX, nextRowY);
    dbNameComboBox.Location = new Point(fieldX, nextRowY);
    nextRowY += rowHeight;
}
```

with:

```csharp
if (!isLdap)
{
    dbNameLabel.Location = new Point(labelX, nextRowY);
    dbNameComboBox.Location = new Point(fieldX, nextRowY);
    nextRowY += rowHeight;

    if (dbNameHintLabel.Visible)
    {
        dbNameHintLabel.Location = new Point(fieldX, nextRowY - 6);
        nextRowY += 30;
    }
}
```

(`UpdateUIForConnectionType` sets the label's visibility before it calls `ApplyFormLayout` at the end of the method, so the visibility check here is reliable.)

- [ ] **Step 6: Add the data source resolver and use it in both Oracle branches**

Add this method near `ConnectButton_Click`:

```csharp
/// <summary>
/// Returns the Data Source to hand to ODP.NET: the full connect descriptor for
/// aliases that came from a tnsnames.ora IFILE include (the managed driver
/// cannot resolve those), otherwise the alias unchanged.
/// </summary>
private string ResolveOracleDataSource()
{
    string alias = dbNameComboBox.Text;
    if (tnsServiceDescriptors.TryGetValue(alias, out string? descriptor) && !string.IsNullOrWhiteSpace(descriptor))
    {
        Debug.Log($"Oracle connection for '{alias}' using descriptor resolved from a tnsnames.ora include.");
        return descriptor;
    }
    Debug.Log($"Oracle connection for '{alias}' using alias data source.");
    return alias;
}
```

Then in `ConnectButton_Click`, there are exactly two occurrences of:

```csharp
connectionString = $"Data Source={dbNameComboBox.Text};User Id={username};Password={password};";
```

(one in the `isReadOnly` branch, one in the bootstrap branch — both inside `dbType == "Oracle"` checks). Replace BOTH with:

```csharp
connectionString = $"Data Source={ResolveOracleDataSource()};User Id={username};Password={password};";
```

Do NOT touch the LDAP branch or the SQL Server `DSN=` lines.

- [ ] **Step 7: Build**

```powershell
dotnet build AppRefiner/AppRefiner.csproj
```

Expected: build succeeds.

- [ ] **Step 8: Commit**

```powershell
git add AppRefiner/Dialogs/DBConnectDialog.cs
git commit -m "feat: connect via descriptor for IFILE-resolved aliases, warn on unreadable includes"
```

---

### Task 4: Asynchronous connection-list loading in DBConnectDialog

**Files:**
- Modify: `AppRefiner/Dialogs/DBConnectDialog.cs` (constructor ~lines 133-143, end of `InitializeComponent` ~lines 424-439, new methods)

- [ ] **Step 1: Add the pending-default field and set it in the constructor**

Add near the other private fields:

```csharp
// Default DB selection is deferred until the async connection-list load completes
private readonly string? pendingDefaultDbName;
```

In the constructor, replace:

```csharp
            // Set default DB name if provided
            if (!string.IsNullOrEmpty(defaultDbName))
            {
                SelectDatabaseByName(defaultDbName);
            }
```

with:

```csharp
            // Default DB selection happens after the async connection load completes
            pendingDefaultDbName = defaultDbName;
```

- [ ] **Step 2: Replace the synchronous load at the end of InitializeComponent**

Replace this block at the end of `InitializeComponent()`:

```csharp
            // Load all database connections for smart detection
            LoadAllDatabaseConnections();

            // Load database names based on initially selected type
            string? dbType = this.dbTypeComboBox.SelectedItem?.ToString();
            if (dbType == "Oracle")
            {
                LoadOracleTnsNames();
            }
            else if (dbType == "SQL Server")
            {
                LoadSqlServerDsns();
            }
```

with:

```csharp
            // Load database connections asynchronously so an unreachable network
            // tnsnames.ora (or IFILE target) cannot freeze the dialog
            BeginLoadDatabaseConnections();
```

(The `UpdateUIForConnectionType();` call that follows stays where it is.)

- [ ] **Step 3: Add the async load methods**

Add these two methods after `LoadAllDatabaseConnections()`:

```csharp
/// <summary>
/// Starts loading TNS names and DSNs on a background task so file reads on
/// unreachable network paths cannot block the UI thread. The dropdown is
/// disabled until the load completes.
/// </summary>
private void BeginLoadDatabaseConnections()
{
    dbNameComboBox.Enabled = false;
    dbNameComboBox.Text = "Loading...";
    connectButton.Enabled = false;

    // The constructor runs on the thread that will pump this dialog's messages,
    // so its synchronization context marshals the continuation back correctly
    var uiScheduler = SynchronizationContext.Current != null
        ? TaskScheduler.FromCurrentSynchronizationContext()
        : TaskScheduler.Current;

    Task.Run(() => LoadAllDatabaseConnections())
        .ContinueWith(_ => OnDatabaseConnectionsLoaded(), uiScheduler);
}

private void OnDatabaseConnectionsLoaded()
{
    if (IsDisposed)
    {
        return;
    }

    dbNameComboBox.Enabled = true;
    dbNameComboBox.Text = string.Empty;
    connectButton.Enabled = true;

    string? dbType = dbTypeComboBox.SelectedItem?.ToString();
    if (dbType == "Oracle")
    {
        LoadOracleTnsNames();
    }
    else if (dbType == "SQL Server")
    {
        LoadSqlServerDsns();
    }

    if (!string.IsNullOrEmpty(pendingDefaultDbName))
    {
        SelectDatabaseByName(pendingDefaultDbName);
    }

    // Re-evaluate the include-failure hint now that load results are known
    UpdateUIForConnectionType();
}
```

(`LoadAllDatabaseConnections` already catches all its own exceptions, so the continuation never observes a fault. Setting `Text` on the combo does not raise `SelectedIndexChanged`, so the settings-apply/auto-connect flow only fires once real names are populated — same as today, just later.)

- [ ] **Step 4: Build**

```powershell
dotnet build AppRefiner/AppRefiner.csproj
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add AppRefiner/Dialogs/DBConnectDialog.cs
git commit -m "feat: load DB connection lists asynchronously in DBConnectDialog"
```

---

### Task 5: Connect DB button feedback (MainForm + palette command)

**Files:**
- Modify: `AppRefiner/MainForm.cs:1362-1400` (`btnConnectDB_Click`)
- Modify: `AppRefiner/Commands/BuiltIn/DatabaseConnectCommand.cs:19-45` (`Execute`)

- [ ] **Step 1: Rewrite btnConnectDB_Click**

Replace the entire `btnConnectDB_Click` method in `AppRefiner/MainForm.cs` with:

```csharp
private void btnConnectDB_Click(object sender, EventArgs e)
{
    if (activeAppDesigner == null)
    {
        Debug.Log("Connect DB: no Application Designer session detected; cannot open the connection dialog.");
        new MessageBoxDialog(
            "AppRefiner has not detected an Application Designer session yet.\r\n\r\nStart Application Designer and try again.",
            "Connect DB",
            MessageBoxButtons.OK,
            this.Handle).ShowDialog(this);
        return;
    }

    if (activeAppDesigner.DataManager != null)
    {
        activeAppDesigner.DataManager.Disconnect();
        foreach (var editor in activeAppDesigner.Editors.Values)
        {
            editor.DataManager = null;
        }

        btnConnectDB.Text = "Connect DB...";
        return;
    }

    var mainHandle = activeAppDesigner.MainWindowHandle;
    var handleWrapper = new WindowWrapper(mainHandle);
    DBConnectDialog dialog = new(mainHandle, activeAppDesigner.DBName);
    dialog.StartPosition = FormStartPosition.CenterParent;

    if (dialog.ShowDialog(handleWrapper) == DialogResult.OK)
    {
        IDataManager? manager = dialog.DataManager;
        if (manager != null)
        {
            activeAppDesigner.DataManager = manager;
            foreach (var editor in activeAppDesigner.Editors.Values)
            {
                editor.DataManager = manager;
            }
            if (activeEditor != null)
            {
                activeEditor.DataManager = manager;
            }
            btnConnectDB.Text = "Disconnect DB";

            // Force refresh all editors to allow DB-dependent stylers to run
            RefreshAllEditorsAfterDatabaseConnection();
        }
    }
}
```

The two behavioral changes versus the old method: (1) `activeEditor == null` no longer blocks the dialog — the dialog doesn't need an editor, and the editors loop already propagates the DataManager; (2) the `activeAppDesigner == null` case now logs and tells the user instead of silently returning. `MessageBoxDialog` lives in `AppRefiner.Dialogs` (already imported by MainForm); its signature is `(string text, string caption, MessageBoxButtons buttons, IntPtr owner)`.

- [ ] **Step 2: Add the log to DatabaseConnectCommand**

In `AppRefiner/Commands/BuiltIn/DatabaseConnectCommand.cs`, the `Execute` method body is one big `if (context.ActiveAppDesigner != null) { ... }`. Add an `else` after it:

```csharp
else
{
    Debug.Log("Database Connect command: no Application Designer session detected; dialog not shown.");
}
```

(`Debug` resolves to `AppRefiner.Debug` via the parent namespace — no new using needed.)

- [ ] **Step 3: Build**

```powershell
dotnet build AppRefiner/AppRefiner.csproj
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```powershell
git add AppRefiner/MainForm.cs AppRefiner/Commands/BuiltIn/DatabaseConnectCommand.cs
git commit -m "fix: give feedback when Connect DB has no Application Designer session"
```

---

### Task 6: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Full automated check**

```powershell
dotnet build AppRefiner/AppRefiner.csproj
dotnet run --project C:\temp\tnsparse-check
```

Expected: build clean; harness reports `19 passed, 0 failed`.

- [ ] **Step 2: Manual verification checklist (requires Application Designer)**

Replicate the customer setup:
1. Create `C:\temp\TNS_ADMIN_TEST\tnsnames.ora` containing only `IFILE=<path to a real tnsnames.ora with multiple entries>` (e.g. `\\ad.company.com\NETLOGON\tnsnames.ora`).
2. In AppRefiner's settings, set the TNS_ADMIN directory to `C:\temp\TNS_ADMIN_TEST`.
3. Open the Connect DB dialog: the dropdown must list the aliases from the included file, with NO literal `IFILE` entry, and the dialog must open instantly (names may populate a moment later).
4. Connect with one of the included aliases: must succeed; Debug Log shows "using descriptor resolved from a tnsnames.ora include".
5. Edit the stub to point at a nonexistent include; reopen the dialog: orange hint "1 tnsnames include file could not be read — see Debug Log" appears under DB Name, and the Debug Log has the path and error.
6. With no Application Designer running, press Connect DB on the Linter tab: a message box appears instead of silence.
7. With App Designer running but no PeopleCode editor window open, press Connect DB: the dialog opens (previously: silent nothing).

- [ ] **Step 3: Done — hand back for review**

No commit in this task; report manual-check results (or which checks could not be run without a PeopleSoft environment).
