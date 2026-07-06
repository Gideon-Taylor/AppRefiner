# Message Catalog Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Message Catalog visibility inside Application Designer: hover tooltips on MsgGet-family calls, a searchable browser dialog with context-aware insert and free-number finding, and Ctrl+Space-in-call escalation to that dialog.

**Architecture:** A shared static map (`MessageCatalogFunctions`) describes the six built-in functions that take `message_set`/`message_num` and where those args sit. Four new read-only `IDataManager` queries read `PSMSGSETDEFN`/`PSMSGCATDEFN`, memoized by a static `MessageCatalogCache`. Three consumers drive off the map + cache: a tooltip provider, a WinForms dialog (browse / insert / new-message panel), and a Ctrl+Space branch in `InvokeAutocompleteCommand`.

**Tech Stack:** C# / .NET 8 WinForms (AppRefiner.csproj only — no C++ hook changes, no parser changes).

**Spec:** `docs/superpowers/specs/2026-07-03-message-catalog-integration-design.md`

## Global Constraints

- Work directly on `main` (per Tim). Commit after each task.
- AppRefiner is **strictly read-only** against the database. SELECT only — never INSERT/UPDATE/DELETE.
- There is no unit-test project for AppRefiner (only `PeopleCodeTypeInfo.Tests`, which tests a different assembly). Verification per task = `dotnet build AppRefiner/AppRefiner.csproj` (builds in ~5 s) + the manual test plan at the end, executed by Tim against a live Application Designer. Do not build `AppRefinerHook` (C++, untouched by this plan).
- Use AppRefiner's custom `Debug.Log(...)` (namespace `AppRefiner`), never `System.Diagnostics.Debug`.
- Oracle queries use named binds with colon-prefixed keys (`{ ":name", value }` and `WHERE X = :name`); SQL Server queries use `?` positional placeholders with unprefixed keys (`{ "name", value }`). Match `GetRecordFields` in each manager.
- `PSMSGCATDEFN.DESCRLONG` is a CLOB (confirmed by Tim). Plain `ExecuteQuery`/adapter fill materializes CLOBs as strings — read it in SELECT lists normally. Server-side search matches it via `UPPER(DBMS_LOB.SUBSTR(DESCRLONG, 4000, 1)) LIKE :searchTerm` on Oracle (first 4000 chars — a deliberate, documented bound) and `UPPER(CAST(DESCRLONG AS NVARCHAR(MAX))) LIKE ?` on SQL Server (PeopleSoft SQL Server databases use a binary collation, so explicit `UPPER` is required).
- Borderless-dialog gotchas (learned on SnapshotHistoryDialog, follow it exactly): SplitContainer `Panel*MinSize`/`SplitterDistance` must be set **after** `ResumeLayout`, right-anchored buttons must be re-positioned against the real panel width after layout, and the form needs `Padding = new Padding(6)` for resize grips.
- Known pre-existing caveat (inherited, do not fix here): Scintilla positions are UTF-8 byte indexes but `GetScintillaText` returns a C# string indexed by char; non-ASCII text above the cursor can misalign scans. Same caveat as the function-autosuggest feature.

## File Structure

| File | Responsibility |
|---|---|
| `AppRefiner/MessageCatalogFunctions.cs` (new) | The six-function → arg-positions map |
| `AppRefiner/Database/Models/MessageCatalog.cs` (new) | `MessageSetInfo`, `MessageCatalogEntry` models |
| `AppRefiner/Database/IDataManager.cs` (modify) | 4 new query signatures |
| `AppRefiner/Database/OraclePeopleSoftDataManager.cs` (modify) | Oracle implementations |
| `AppRefiner/Database/SqlServerPeopleSoftDataManager.cs` (modify) | SQL Server implementations |
| `AppRefiner/Database/MessageCatalogCache.cs` (new) | Per-data-manager memoization |
| `AppRefiner/TooltipProviders/MessageCatalogTooltipProvider.cs` (new) | Hover lookups |
| `AppRefiner/Dialogs/MessageCatalogDialog.cs` (new) | Browser + insert + new-message panel |
| `AppRefiner/MessageCatalogFreeRanges.cs` (new) | Pure free-range computation |
| `AppRefiner/MessageCatalogCallContext.cs` (new) | AST detection of cursor-in-mapped-call |
| `AppRefiner/Commands/BuiltIn/BrowseMessageCatalogCommand.cs` (new) | Palette command + shortcut |
| `AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs` (modify) | Ctrl+Space branch |
| `AppRefiner/Properties/Settings.settings` + `Settings.Designer.cs` (modify) | Remember last insert function |
| `AppRefiner/whats-new.txt`, `docs/features/message-catalog.md` (modify/new) | User-facing docs |

---

### Task 1: Shared function map

**Files:**
- Create: `AppRefiner/MessageCatalogFunctions.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `MessageCatalogFunctions.TryGetArgPositions(string functionName, out MessageCatalogArgInfo info) : bool` and `record MessageCatalogArgInfo(int SetArg, int NumArg, int DefaultTxtArg, string ColdLeadingArgs)` (indices **0-based**). Used by Tasks 4, 5, 6, 7, 8.

- [ ] **Step 1: Write the map**

```csharp
namespace AppRefiner
{
    /// <summary>
    /// Argument positions (0-based) of the message_set / message_num / default_msg_txt
    /// parameters for a built-in function that reads the Message Catalog.
    /// ColdLeadingArgs holds placeholder text for the arguments that precede message_set,
    /// used when inserting a complete call from scratch (empty for most functions).
    /// </summary>
    public record MessageCatalogArgInfo(int SetArg, int NumArg, int DefaultTxtArg, string ColdLeadingArgs);

    /// <summary>
    /// The exhaustive set of built-in functions that take message_set/message_num
    /// parameters, and where those parameters sit in each signature:
    ///
    ///   CreateException(message_set, message_num, default_txt, any*)
    ///   MessageBox(style, title, message_set, message_num, default_msg_txt, paramlist*)
    ///   MsgBoxButtonOverride(style, title, array_of_button_labels, message_set, message_num, default_msg_txt, paramlist*)
    ///   MsgGet(message_set, message_num, default_msg_txt, any*)
    ///   MsgGetExplainText(message_set, message_num, default_msg_txt, any*)
    ///   MsgGetText(message_set, message_num, default_msg_txt, any*)
    ///
    /// Single source of truth for the tooltip provider, the Ctrl+Space detection,
    /// and the catalog dialog's insert logic.
    /// </summary>
    public static class MessageCatalogFunctions
    {
        private static readonly Dictionary<string, MessageCatalogArgInfo> Map =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["MsgGet"] = new(0, 1, 2, ""),
                ["MsgGetText"] = new(0, 1, 2, ""),
                ["MsgGetExplainText"] = new(0, 1, 2, ""),
                ["CreateException"] = new(0, 1, 2, ""),
                ["MessageBox"] = new(2, 3, 4, "0, \"\", "),
                ["MsgBoxButtonOverride"] = new(3, 4, 5, "0, \"\", CreateArray(\"OK\"), "),
            };

        /// <summary>All mapped function names, for UI pickers (alphabetical).</summary>
        public static IReadOnlyList<string> FunctionNames { get; } =
            Map.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        public static bool TryGetArgPositions(string functionName, out MessageCatalogArgInfo info)
        {
            return Map.TryGetValue(functionName, out info!);
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AppRefiner/MessageCatalogFunctions.cs
git commit -m "feat: shared map of message-catalog function argument positions"
```

---

### Task 2: Models + IDataManager queries (Oracle and SQL Server)

**Files:**
- Create: `AppRefiner/Database/Models/MessageCatalog.cs`
- Modify: `AppRefiner/Database/IDataManager.cs` (add signatures inside `interface IDataManager`, after `GetPackagesForClass`)
- Modify: `AppRefiner/Database/OraclePeopleSoftDataManager.cs`
- Modify: `AppRefiner/Database/SqlServerPeopleSoftDataManager.cs`

**Interfaces:**
- Consumes: `_connection.ExecuteQuery(string sql, Dictionary<string, object> parameters) : DataTable` (existing, both managers).
- Produces (used by Tasks 3–8):
  - `class MessageSetInfo { int SetNumber; string Description; }`
  - `class MessageCatalogEntry { int SetNumber; int MessageNumber; string SeverityCode; string MessageText; string ExplainText; string Severity /* decoded */ }`
  - `List<MessageSetInfo> GetMessageSets()`
  - `List<MessageCatalogEntry> GetMessagesForSet(int setNumber)`
  - `MessageCatalogEntry? GetMessageCatalogEntry(int setNumber, int messageNumber)`
  - `List<MessageCatalogEntry> SearchMessageCatalog(string searchTerm, int? setNumber, int limit)`

- [ ] **Step 1: Write the models**

`AppRefiner/Database/Models/MessageCatalog.cs`:

```csharp
namespace AppRefiner.Database.Models
{
    /// <summary>
    /// A Message Catalog set (one PSMSGSETDEFN row).
    /// </summary>
    public class MessageSetInfo
    {
        public int SetNumber { get; }
        public string Description { get; }

        public MessageSetInfo(int setNumber, string description)
        {
            SetNumber = setNumber;
            Description = description;
        }
    }

    /// <summary>
    /// A Message Catalog entry (one PSMSGCATDEFN row).
    /// </summary>
    public class MessageCatalogEntry
    {
        public int SetNumber { get; }
        public int MessageNumber { get; }

        /// <summary>Raw MSG_SEVERITY code: M, W, E, or C.</summary>
        public string SeverityCode { get; }

        public string MessageText { get; }

        /// <summary>Long explain text (DESCRLONG); empty when none.</summary>
        public string ExplainText { get; }

        public string Severity => SeverityCode switch
        {
            "M" => "Message",
            "W" => "Warning",
            "E" => "Error",
            "C" => "Cancel",
            _ => SeverityCode
        };

        public MessageCatalogEntry(int setNumber, int messageNumber, string severityCode,
            string messageText, string explainText)
        {
            SetNumber = setNumber;
            MessageNumber = messageNumber;
            SeverityCode = severityCode;
            MessageText = messageText;
            ExplainText = explainText;
        }
    }
}
```

- [ ] **Step 2: Add the interface signatures**

In `AppRefiner/Database/IDataManager.cs`, inside `interface IDataManager`, after the `GetPackagesForClass` declaration:

```csharp
        /// <summary>
        /// Gets all Message Catalog sets (PSMSGSETDEFN), ordered by set number.
        /// </summary>
        List<MessageSetInfo> GetMessageSets();

        /// <summary>
        /// Gets all messages in one catalog set (PSMSGCATDEFN), ordered by message number.
        /// </summary>
        List<MessageCatalogEntry> GetMessagesForSet(int setNumber);

        /// <summary>
        /// Gets a single Message Catalog entry, or null when it does not exist.
        /// </summary>
        MessageCatalogEntry? GetMessageCatalogEntry(int setNumber, int messageNumber);

        /// <summary>
        /// Case-insensitive search of MESSAGE_TEXT and explain text across the catalog
        /// (optionally one set), capped at <paramref name="limit"/> rows. DESCRLONG is a
        /// CLOB; only its first 4000 characters are matched (DBMS_LOB.SUBSTR bound).
        /// </summary>
        List<MessageCatalogEntry> SearchMessageCatalog(string searchTerm, int? setNumber, int limit);
```

- [ ] **Step 3: Implement in OraclePeopleSoftDataManager**

Add near `GetRecordFields` (same region). Note the Oracle bind style and `FETCH FIRST n ROWS ONLY` (Oracle 12c+; every supported PeopleSoft platform qualifies). The limit is an `int` interpolated directly — no injection surface.

```csharp
        public List<MessageSetInfo> GetMessageSets()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            string sql = @"
                SELECT MESSAGE_SET_NBR, DESCR
                FROM PSMSGSETDEFN
                ORDER BY MESSAGE_SET_NBR";

            List<MessageSetInfo> sets = new();
            try
            {
                DataTable result = _connection.ExecuteQuery(sql, new Dictionary<string, object>());
                foreach (DataRow row in result.Rows)
                {
                    sets.Add(new MessageSetInfo(
                        Convert.ToInt32(row["MESSAGE_SET_NBR"]),
                        row["DESCR"].ToString() ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"GetMessageSets failed: {ex.Message}");
            }
            return sets;
        }

        public List<MessageCatalogEntry> GetMessagesForSet(int setNumber)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            string sql = @"
                SELECT MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
                FROM PSMSGCATDEFN
                WHERE MESSAGE_SET_NBR = :setNumber
                ORDER BY MESSAGE_NBR";

            Dictionary<string, object> parameters = new()
            {
                { ":setNumber", setNumber }
            };

            List<MessageCatalogEntry> messages = new();
            try
            {
                DataTable result = _connection.ExecuteQuery(sql, parameters);
                foreach (DataRow row in result.Rows)
                {
                    messages.Add(new MessageCatalogEntry(
                        setNumber,
                        Convert.ToInt32(row["MESSAGE_NBR"]),
                        row["MSG_SEVERITY"].ToString() ?? string.Empty,
                        row["MESSAGE_TEXT"].ToString() ?? string.Empty,
                        row["DESCRLONG"] == DBNull.Value ? string.Empty : row["DESCRLONG"].ToString() ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"GetMessagesForSet({setNumber}) failed: {ex.Message}");
            }
            return messages;
        }

        public MessageCatalogEntry? GetMessageCatalogEntry(int setNumber, int messageNumber)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            string sql = @"
                SELECT MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
                FROM PSMSGCATDEFN
                WHERE MESSAGE_SET_NBR = :setNumber
                AND MESSAGE_NBR = :messageNumber";

            Dictionary<string, object> parameters = new()
            {
                { ":setNumber", setNumber },
                { ":messageNumber", messageNumber }
            };

            try
            {
                DataTable result = _connection.ExecuteQuery(sql, parameters);
                if (result.Rows.Count == 0)
                {
                    return null;
                }
                DataRow row = result.Rows[0];
                return new MessageCatalogEntry(
                    setNumber,
                    messageNumber,
                    row["MSG_SEVERITY"].ToString() ?? string.Empty,
                    row["MESSAGE_TEXT"].ToString() ?? string.Empty,
                    row["DESCRLONG"] == DBNull.Value ? string.Empty : row["DESCRLONG"].ToString() ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.Log($"GetMessageCatalogEntry({setNumber}, {messageNumber}) failed: {ex.Message}");
                return null;
            }
        }

        public List<MessageCatalogEntry> SearchMessageCatalog(string searchTerm, int? setNumber, int limit)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            // BindByName = true lets one named parameter serve both LIKE placeholders.
            // DESCRLONG is a CLOB: DBMS_LOB.SUBSTR bounds the match to its first 4000 chars.
            string sql = $@"
                SELECT MESSAGE_SET_NBR, MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
                FROM PSMSGCATDEFN
                WHERE (UPPER(MESSAGE_TEXT) LIKE :searchTerm
                       OR UPPER(DBMS_LOB.SUBSTR(DESCRLONG, 4000, 1)) LIKE :searchTerm)
                {(setNumber.HasValue ? "AND MESSAGE_SET_NBR = :setNumber" : "")}
                ORDER BY MESSAGE_SET_NBR, MESSAGE_NBR
                FETCH FIRST {limit} ROWS ONLY";

            Dictionary<string, object> parameters = new()
            {
                { ":searchTerm", $"%{searchTerm.ToUpperInvariant()}%" }
            };
            if (setNumber.HasValue)
            {
                parameters.Add(":setNumber", setNumber.Value);
            }

            List<MessageCatalogEntry> messages = new();
            try
            {
                DataTable result = _connection.ExecuteQuery(sql, parameters);
                foreach (DataRow row in result.Rows)
                {
                    messages.Add(new MessageCatalogEntry(
                        Convert.ToInt32(row["MESSAGE_SET_NBR"]),
                        Convert.ToInt32(row["MESSAGE_NBR"]),
                        row["MSG_SEVERITY"].ToString() ?? string.Empty,
                        row["MESSAGE_TEXT"].ToString() ?? string.Empty,
                        row["DESCRLONG"] == DBNull.Value ? string.Empty : row["DESCRLONG"].ToString() ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"SearchMessageCatalog failed: {ex.Message}");
            }
            return messages;
        }
```

- [ ] **Step 4: Implement in SqlServerPeopleSoftDataManager**

Same four methods, translated to the SQL Server house style: `?` placeholders, unprefixed parameter keys, `SELECT TOP ({limit})` instead of `FETCH FIRST`. The bodies are otherwise identical to Step 3, so only the SQL/parameter differences are shown — copy the loop/`try`/`Debug.Log` structure from Step 3 verbatim:

```csharp
        // GetMessageSets — identical, no parameters:
        //   SELECT MESSAGE_SET_NBR, DESCR FROM PSMSGSETDEFN ORDER BY MESSAGE_SET_NBR

        // GetMessagesForSet:
        //   SELECT MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
        //   FROM PSMSGCATDEFN WHERE MESSAGE_SET_NBR = ? ORDER BY MESSAGE_NBR
        //   parameters: { "setNumber", setNumber }

        // GetMessageCatalogEntry:
        //   SELECT MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
        //   FROM PSMSGCATDEFN WHERE MESSAGE_SET_NBR = ? AND MESSAGE_NBR = ?
        //   parameters: { "setNumber", setNumber }, { "messageNumber", messageNumber }
        //   (positional: add setNumber first, then messageNumber)

        // SearchMessageCatalog (positional ?s — the search term must be added TWICE):
        //   SELECT TOP ({limit}) MESSAGE_SET_NBR, MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
        //   FROM PSMSGCATDEFN
        //   WHERE (UPPER(MESSAGE_TEXT) LIKE ? OR UPPER(CAST(DESCRLONG AS NVARCHAR(MAX))) LIKE ?)
        //   {setNumber.HasValue ? "AND MESSAGE_SET_NBR = ?" : ""}
        //   ORDER BY MESSAGE_SET_NBR, MESSAGE_NBR
        //   parameters (in order): { "searchTerm1", $"%{searchTerm.ToUpperInvariant()}%" },
        //   { "searchTerm2", $"%{searchTerm.ToUpperInvariant()}%" },
        //   then, only if setNumber.HasValue: { "setNumber", setNumber.Value }
```

- [ ] **Step 5: Verify it compiles**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors. (A compile error about unimplemented interface members in either manager means Step 3 or 4 was missed.)

- [ ] **Step 6: Commit**

```bash
git add AppRefiner/Database/Models/MessageCatalog.cs AppRefiner/Database/IDataManager.cs AppRefiner/Database/OraclePeopleSoftDataManager.cs AppRefiner/Database/SqlServerPeopleSoftDataManager.cs
git commit -m "feat: message catalog queries in IDataManager (Oracle + SQL Server)"
```

---

### Task 3: MessageCatalogCache

**Files:**
- Create: `AppRefiner/Database/MessageCatalogCache.cs`

**Interfaces:**
- Consumes: Task 2's `IDataManager` methods and models.
- Produces (used by Tasks 4, 5, 7):
  - `MessageCatalogCache.GetMessageSets(IDataManager dm) : List<MessageSetInfo>`
  - `MessageCatalogCache.GetMessagesForSet(IDataManager dm, int setNumber) : List<MessageCatalogEntry>`
  - `MessageCatalogCache.GetEntry(IDataManager dm, int setNumber, int messageNumber) : MessageCatalogEntry?` (caches negative lookups)
  - `MessageCatalogCache.Clear(IDataManager dm) : void`

- [ ] **Step 1: Write the cache**

```csharp
using AppRefiner.Database.Models;
using System.Runtime.CompilerServices;

namespace AppRefiner.Database
{
    /// <summary>
    /// Memoizes Message Catalog queries per data manager. Keyed with a
    /// ConditionalWeakTable so cache state dies with the connection.
    /// Negative single-entry lookups are cached too, so hovering a typo'd
    /// MsgGet doesn't re-query on every mouse move. The catalog is nearly
    /// static; the dialog's Refresh button calls Clear().
    /// </summary>
    public static class MessageCatalogCache
    {
        private class State
        {
            public List<MessageSetInfo>? Sets;
            public readonly Dictionary<int, List<MessageCatalogEntry>> MessagesBySet = new();
            public readonly Dictionary<(int Set, int Num), MessageCatalogEntry?> SingleLookups = new();
        }

        private static readonly ConditionalWeakTable<IDataManager, State> States = new();

        public static List<MessageSetInfo> GetMessageSets(IDataManager dataManager)
        {
            var state = States.GetOrCreateValue(dataManager);
            lock (state)
            {
                state.Sets ??= dataManager.GetMessageSets();
                return state.Sets;
            }
        }

        public static List<MessageCatalogEntry> GetMessagesForSet(IDataManager dataManager, int setNumber)
        {
            var state = States.GetOrCreateValue(dataManager);
            lock (state)
            {
                if (!state.MessagesBySet.TryGetValue(setNumber, out var messages))
                {
                    messages = dataManager.GetMessagesForSet(setNumber);
                    state.MessagesBySet[setNumber] = messages;
                }
                return messages;
            }
        }

        public static MessageCatalogEntry? GetEntry(IDataManager dataManager, int setNumber, int messageNumber)
        {
            var state = States.GetOrCreateValue(dataManager);
            lock (state)
            {
                // A fully loaded set answers without another query
                if (state.MessagesBySet.TryGetValue(setNumber, out var loaded))
                {
                    return loaded.FirstOrDefault(m => m.MessageNumber == messageNumber);
                }

                if (!state.SingleLookups.TryGetValue((setNumber, messageNumber), out var entry))
                {
                    entry = dataManager.GetMessageCatalogEntry(setNumber, messageNumber);
                    state.SingleLookups[(setNumber, messageNumber)] = entry;
                }
                return entry;
            }
        }

        public static void Clear(IDataManager dataManager)
        {
            if (States.TryGetValue(dataManager, out var state))
            {
                lock (state)
                {
                    state.Sets = null;
                    state.MessagesBySet.Clear();
                    state.SingleLookups.Clear();
                }
            }
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AppRefiner/Database/MessageCatalogCache.cs
git commit -m "feat: per-connection message catalog cache with negative lookups"
```

---

### Task 4: Tooltip provider

**Files:**
- Create: `AppRefiner/TooltipProviders/MessageCatalogTooltipProvider.cs`

**Interfaces:**
- Consumes: `MessageCatalogFunctions.TryGetArgPositions` (Task 1), `MessageCatalogCache` (Task 3), `BaseTooltipProvider` members: `DataManager`, `ContainsPosition(SourceSpan)`, `RegisterTooltip(SourceSpan, string)`.
- Produces: nothing consumed by later tasks. Auto-discovered by `TooltipManager` via reflection — **no registration step**.

- [ ] **Step 1: Write the provider**

```csharp
using AppRefiner.Database;
using AppRefiner.Database.Models;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Shows the Message Catalog entry for MsgGet/MsgGetText/MsgGetExplainText/
    /// CreateException/MessageBox/MsgBoxButtonOverride calls whose message_set and
    /// message_num arguments are integer literals.
    /// </summary>
    public class MessageCatalogTooltipProvider : BaseTooltipProvider
    {
        private const int ExplainTruncateLength = 500;

        public override string Name => "Message Catalog";
        public override string Description => "Shows message catalog text when hovering MsgGet-style calls";
        public override int Priority => 60;
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            base.VisitFunctionCall(node);

            if (DataManager == null) return;
            // Only the hovered call triggers a lookup — the visitor walks the whole program
            if (!node.SourceSpan.IsValid || !ContainsPosition(node.SourceSpan)) return;
            if (node.Function is not IdentifierNode ident) return;
            if (!MessageCatalogFunctions.TryGetArgPositions(ident.Name, out var argInfo)) return;
            if (!TryGetIntLiteral(node, argInfo.SetArg, out int setNumber)) return;
            if (!TryGetIntLiteral(node, argInfo.NumArg, out int messageNumber)) return;

            var entry = MessageCatalogCache.GetEntry(DataManager, setNumber, messageNumber);
            if (entry == null)
            {
                RegisterTooltip(node.SourceSpan, $"No catalog entry for {setNumber}/{messageNumber}");
                return;
            }

            var setDescription = MessageCatalogCache.GetMessageSets(DataManager)
                .FirstOrDefault(s => s.SetNumber == setNumber)?.Description;

            RegisterTooltip(node.SourceSpan, FormatTooltip(entry, setDescription));
        }

        private static bool TryGetIntLiteral(FunctionCallNode node, int argIndex, out int value)
        {
            value = 0;
            if (node.Arguments.Count <= argIndex) return false;
            if (node.Arguments[argIndex] is not LiteralNode literal) return false;
            if (literal.LiteralType != LiteralType.Integer) return false;

            try
            {
                value = Convert.ToInt32(literal.Value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatTooltip(MessageCatalogEntry entry, string? setDescription)
        {
            var parts = new List<string>
            {
                $"Message Catalog {entry.SetNumber}/{entry.MessageNumber}" +
                    (string.IsNullOrEmpty(setDescription) ? "" : $" — {setDescription}"),
                $"Severity: {entry.Severity}",
                $"\"{entry.MessageText}\""
            };

            if (!string.IsNullOrWhiteSpace(entry.ExplainText))
            {
                string explain = entry.ExplainText.Trim();
                if (explain.Length > ExplainTruncateLength)
                {
                    explain = explain.Substring(0, ExplainTruncateLength) + "…";
                }
                parts.Add("");
                parts.Add($"Explain: {explain}");
            }

            return string.Join("\n", parts);
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AppRefiner/TooltipProviders/MessageCatalogTooltipProvider.cs
git commit -m "feat: message catalog hover tooltips on MsgGet-family calls"
```

---

### Task 5: MessageCatalogDialog (browse mode) + palette command

**Files:**
- Create: `AppRefiner/Dialogs/MessageCatalogDialog.cs`
- Create: `AppRefiner/Commands/BuiltIn/BrowseMessageCatalogCommand.cs`
- Reference for boilerplate: `AppRefiner/Dialogs/SnapshotHistoryDialog.cs` (borderless-form conventions: `WndProc` resize grips at lines 600–625, `OnShown` centering + `ModalDialogMouseHandler` at 627–640, `OnPaint` border, `ProcessDialogKey` Escape, `OnFormClosed` cleanup at 661+)

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces (used by Tasks 6–8):
  - `MessageCatalogDialog(IDataManager dataManager, IntPtr owner)` — browse/cold-insert constructor
  - `public string? TextToInsert { get; }` — set when the user chose something to insert; caller performs the Scintilla insert
  - `private MessageCatalogEntry? SelectedEntry` and `private int? SelectedSetNumber` internal state Tasks 6/7 build on

This task delivers: sets list + filter, per-set message grid, client-side search (text **and** explain) within the loaded set, all-sets server-side search (Enter to run, capped at 200, cap surfaced), preview pane, Copy `set, num` button, Refresh button. Insert comes in Task 6.

- [ ] **Step 1: Write the dialog**

```csharp
using AppRefiner.Database;
using AppRefiner.Database.Models;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Message Catalog browser: browse sets, search messages, preview explain text,
    /// copy or insert message references. Read-only — creating catalog rows still
    /// happens in the PIA; this dialog removes every other reason to leave App Designer.
    /// </summary>
    public class MessageCatalogDialog : Form
    {
        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int ResizeGripSize = 6;
        private const int SearchResultCap = 200;

        private readonly IDataManager dataManager;
        private readonly IntPtr owner;

        private readonly Panel headerPanel = new();
        private readonly Label headerLabel = new();
        private readonly SplitContainer splitContainer = new();
        private readonly TextBox setsFilterTextBox = new();
        private readonly ListView setsListView = new();
        private readonly FlowLayoutPanel searchPanel = new();
        private readonly TextBox searchTextBox = new();
        private readonly CheckBox allSetsCheckBox = new();
        private readonly Label resultCapLabel = new();
        private readonly ListView messagesListView = new();
        private readonly RichTextBox previewTextBox = new();
        private readonly Panel buttonPanel = new();
        private readonly Button refreshButton = new();
        private readonly Button copyButton = new();
        private readonly Button closeButton = new();

        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        private List<MessageSetInfo> allSets = new();
        private List<MessageCatalogEntry> currentSetMessages = new();

        public string? TextToInsert { get; private set; }

        private MessageCatalogEntry? SelectedEntry =>
            messagesListView.SelectedItems.Count > 0
                ? (MessageCatalogEntry)messagesListView.SelectedItems[0].Tag!
                : null;

        private int? SelectedSetNumber =>
            setsListView.SelectedItems.Count > 0
                ? ((MessageSetInfo)setsListView.SelectedItems[0].Tag!).SetNumber
                : null;

        public MessageCatalogDialog(IDataManager dataManager, IntPtr owner = default)
        {
            this.dataManager = dataManager;
            this.owner = owner;
            InitializeComponent();
            LoadSets();
        }

        private void InitializeComponent()
        {
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            this.Text = "Message Catalog";
            this.ClientSize = new Size(950, 620);
            this.MinimumSize = new Size(760, 480);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Padding = new Padding(ResizeGripSize);

            // header
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerLabel.Text = "Message Catalog";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.headerPanel.Controls.Add(this.headerLabel);

            // left pane: set filter + sets list
            this.setsFilterTextBox.Dock = DockStyle.Top;
            this.setsFilterTextBox.PlaceholderText = "Filter sets (number or description)";
            this.setsFilterTextBox.TextChanged += (s, e) => PopulateSetsList();

            this.setsListView.Dock = DockStyle.Fill;
            this.setsListView.View = View.Details;
            this.setsListView.FullRowSelect = true;
            this.setsListView.HideSelection = false;
            this.setsListView.MultiSelect = false;
            this.setsListView.Columns.Add("Set", 70);
            this.setsListView.Columns.Add("Description", 180);
            this.setsListView.SelectedIndexChanged += (s, e) => OnSetSelected();

            // right pane: search row + message grid + preview
            this.searchPanel.Dock = DockStyle.Top;
            this.searchPanel.Height = 34;
            this.searchPanel.FlowDirection = FlowDirection.LeftToRight;
            this.searchPanel.WrapContents = false;
            this.searchPanel.Padding = new Padding(6, 4, 6, 0);

            this.searchTextBox.Width = 320;
            this.searchTextBox.PlaceholderText = "Search messages (Enter to search all sets)";
            this.searchTextBox.TextChanged += (s, e) => { if (!allSetsCheckBox.Checked) ApplyClientFilter(); };
            this.searchTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && allSetsCheckBox.Checked)
                {
                    e.SuppressKeyPress = true;
                    RunServerSearch();
                }
            };

            this.allSetsCheckBox.Text = "All sets";
            this.allSetsCheckBox.AutoSize = true;
            this.allSetsCheckBox.Margin = new Padding(8, 6, 0, 0);
            this.allSetsCheckBox.CheckedChanged += (s, e) =>
            {
                resultCapLabel.Text = "";
                if (!allSetsCheckBox.Checked) OnSetSelected();
            };

            this.resultCapLabel.AutoSize = true;
            this.resultCapLabel.Margin = new Padding(10, 9, 0, 0);
            this.resultCapLabel.ForeColor = Color.FromArgb(120, 120, 130);

            this.searchPanel.Controls.Add(this.searchTextBox);
            this.searchPanel.Controls.Add(this.allSetsCheckBox);
            this.searchPanel.Controls.Add(this.resultCapLabel);

            this.messagesListView.Dock = DockStyle.Fill;
            this.messagesListView.View = View.Details;
            this.messagesListView.FullRowSelect = true;
            this.messagesListView.HideSelection = false;
            this.messagesListView.MultiSelect = false;
            this.messagesListView.Columns.Add("Set", 60);
            this.messagesListView.Columns.Add("Num", 60);
            this.messagesListView.Columns.Add("Severity", 70);
            this.messagesListView.Columns.Add("Text", 380);
            this.messagesListView.SelectedIndexChanged += (s, e) => RenderPreview();

            this.previewTextBox.Dock = DockStyle.Bottom;
            this.previewTextBox.Height = 140;
            this.previewTextBox.ReadOnly = true;
            this.previewTextBox.BorderStyle = BorderStyle.FixedSingle;
            this.previewTextBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.previewTextBox.BackColor = Color.White;

            // bottom buttons
            this.buttonPanel.Dock = DockStyle.Bottom;
            this.buttonPanel.Height = 50;

            this.refreshButton.Text = "Refresh";
            this.refreshButton.Size = new Size(90, 30);
            this.refreshButton.Location = new Point(20, 10);
            this.refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            StyleAccentButton(this.refreshButton);
            this.refreshButton.Click += (s, e) => RefreshFromDatabase();

            this.copyButton.Text = "Copy set, num";
            this.copyButton.Size = new Size(120, 30);
            this.copyButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.copyButton.Enabled = false;
            StyleAccentButton(this.copyButton);
            this.copyButton.Click += (s, e) =>
            {
                var entry = SelectedEntry;
                if (entry != null) Clipboard.SetText($"{entry.SetNumber}, {entry.MessageNumber}");
            };

            this.closeButton.Text = "Close";
            this.closeButton.Size = new Size(90, 30);
            this.closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.closeButton.BackColor = Color.FromArgb(100, 100, 100);
            this.closeButton.ForeColor = Color.White;
            this.closeButton.FlatStyle = FlatStyle.Flat;
            this.closeButton.FlatAppearance.BorderSize = 0;
            this.closeButton.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.buttonPanel.Controls.Add(this.refreshButton);
            this.buttonPanel.Controls.Add(this.copyButton);
            this.buttonPanel.Controls.Add(this.closeButton);

            // split container
            this.splitContainer.Dock = DockStyle.Fill;
            this.splitContainer.Orientation = Orientation.Vertical;
            this.splitContainer.FixedPanel = FixedPanel.Panel1;
            this.splitContainer.Panel1.Controls.Add(this.setsListView);
            this.splitContainer.Panel1.Controls.Add(this.setsFilterTextBox);
            this.splitContainer.Panel2.Controls.Add(this.messagesListView);
            this.splitContainer.Panel2.Controls.Add(this.previewTextBox);
            this.splitContainer.Panel2.Controls.Add(this.searchPanel);

            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.buttonPanel);
            this.Controls.Add(this.headerPanel);

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

            // Min sizes / splitter distance only after layout has resumed —
            // while suspended the SplitContainer is its default 150px and the
            // internal clamp throws (see SnapshotHistoryDialog).
            this.splitContainer.Panel1MinSize = 200;
            this.splitContainer.Panel2MinSize = 400;
            this.splitContainer.SplitterDistance = 270;

            // Right-anchored buttons captured offsets at default panel width;
            // re-position against the real width so anchors recapture.
            this.copyButton.Location = new Point(this.buttonPanel.ClientSize.Width - 240, 10);
            this.closeButton.Location = new Point(this.buttonPanel.ClientSize.Width - 110, 10);
        }

        private static void StyleAccentButton(Button button)
        {
            button.BackColor = Color.FromArgb(0, 122, 204);
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
        }

        private void LoadSets()
        {
            allSets = MessageCatalogCache.GetMessageSets(dataManager);
            PopulateSetsList();
        }

        private void PopulateSetsList()
        {
            string filter = setsFilterTextBox.Text.Trim();
            setsListView.BeginUpdate();
            setsListView.Items.Clear();
            foreach (var set in allSets)
            {
                if (filter.Length > 0
                    && !set.SetNumber.ToString().Contains(filter)
                    && !set.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var item = new ListViewItem(set.SetNumber.ToString());
                item.SubItems.Add(set.Description);
                item.Tag = set;
                setsListView.Items.Add(item);
            }
            setsListView.EndUpdate();
        }

        private void OnSetSelected()
        {
            var setNumber = SelectedSetNumber;
            if (setNumber == null) return;

            currentSetMessages = MessageCatalogCache.GetMessagesForSet(dataManager, setNumber.Value);
            resultCapLabel.Text = "";
            ApplyClientFilter();
        }

        /// <summary>Filter the loaded set client-side: matches message text OR explain text.</summary>
        private void ApplyClientFilter()
        {
            string term = searchTextBox.Text.Trim();
            var visible = term.Length == 0
                ? currentSetMessages
                : currentSetMessages.Where(m =>
                        m.MessageText.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || m.ExplainText.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            PopulateMessagesList(visible);
        }

        /// <summary>All-sets server search: matches message text plus the first
        /// 4000 characters of explain text (CLOB substring bound).</summary>
        private void RunServerSearch()
        {
            string term = searchTextBox.Text.Trim();
            if (term.Length == 0) return;

            var results = dataManager.SearchMessageCatalog(term, null, SearchResultCap);
            resultCapLabel.Text = results.Count >= SearchResultCap
                ? $"showing first {SearchResultCap}"
                : $"{results.Count} match{(results.Count == 1 ? "" : "es")}";
            PopulateMessagesList(results);
        }

        private void PopulateMessagesList(List<MessageCatalogEntry> messages)
        {
            messagesListView.BeginUpdate();
            messagesListView.Items.Clear();
            foreach (var message in messages)
            {
                var item = new ListViewItem(message.SetNumber.ToString());
                item.SubItems.Add(message.MessageNumber.ToString());
                item.SubItems.Add(message.Severity);
                item.SubItems.Add(message.MessageText);
                item.Tag = message;
                messagesListView.Items.Add(item);
            }
            messagesListView.EndUpdate();
            RenderPreview();
        }

        private void RenderPreview()
        {
            var entry = SelectedEntry;
            copyButton.Enabled = entry != null;
            if (entry == null)
            {
                previewTextBox.Text = "";
                return;
            }
            previewTextBox.Text = string.IsNullOrWhiteSpace(entry.ExplainText)
                ? entry.MessageText
                : $"{entry.MessageText}\n\nExplain:\n{entry.ExplainText.Trim()}";
        }

        private void RefreshFromDatabase()
        {
            var keepSet = SelectedSetNumber;
            MessageCatalogCache.Clear(dataManager);
            LoadSets();
            if (keepSet != null)
            {
                foreach (ListViewItem item in setsListView.Items)
                {
                    if (((MessageSetInfo)item.Tag!).SetNumber == keepSet.Value)
                    {
                        item.Selected = true;
                        break;
                    }
                }
            }
        }

        // ---- borderless-form boilerplate: identical in structure to SnapshotHistoryDialog ----

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
                // Signed extraction handles negative coords on multi-monitor setups
                int x = unchecked((short)(long)m.LParam);
                int y = unchecked((short)((long)m.LParam >> 16));
                var pos = PointToClient(new Point(x, y));

                bool left = pos.X < ResizeGripSize;
                bool right = pos.X >= ClientSize.Width - ResizeGripSize;
                bool top = pos.Y < ResizeGripSize;
                bool bottom = pos.Y >= ClientSize.Height - ResizeGripSize;
                if (top && left) m.Result = (IntPtr)13;        // HTTOPLEFT
                else if (top && right) m.Result = (IntPtr)14;  // HTTOPRIGHT
                else if (bottom && left) m.Result = (IntPtr)16; // HTBOTTOMLEFT
                else if (bottom && right) m.Result = (IntPtr)17; // HTBOTTOMRIGHT
                else if (left) m.Result = (IntPtr)10;          // HTLEFT
                else if (right) m.Result = (IntPtr)11;         // HTRIGHT
                else if (top) m.Result = (IntPtr)12;           // HTTOP
                else if (bottom) m.Result = (IntPtr)15;        // HTBOTTOM
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (owner != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, owner);
            }

            if (this.Modal && owner != IntPtr.Zero)
            {
                mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, owner);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Color.FromArgb(100, 100, 120), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            mouseHandler?.Dispose();
            mouseHandler = null;
        }
    }
}
```

**Note for the implementer:** before finalizing, open `SnapshotHistoryDialog.cs` lines 600–670 and confirm the `WndProc`/`OnShown`/`OnPaint`/`OnFormClosed` bodies above match its current conventions (exact HT* constants, `WindowHelper`/centering call name, `ModalDialogMouseHandler` constructor arity). If they differ, follow SnapshotHistoryDialog — it is the source of truth for this boilerplate.

- [ ] **Step 2: Write the palette command**

`AppRefiner/Commands/BuiltIn/BrowseMessageCatalogCommand.cs`:

```csharp
using AppRefiner.Dialogs;
using AppRefiner.Services;
using System.Diagnostics;

namespace AppRefiner.Commands.BuiltIn
{
    public class BrowseMessageCatalogCommand : BaseCommand
    {
        public override string CommandName => "Browse Message Catalog";
        public override string CommandDescription => "Browse and search the PeopleSoft Message Catalog";
        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            var shortcuts = new[]
            {
                (ModifierKeys.Control | ModifierKeys.Alt, Keys.M),
                (ModifierKeys.Control | ModifierKeys.Shift, Keys.M),
            };

            foreach (var (modifiers, key) in shortcuts)
            {
                if (registrar.IsShortcutAvailable(modifiers, key)
                    && registrar.TryRegisterShortcut(commandId, modifiers, key, this))
                {
                    SetRegisteredShortcut(registrar.GetShortcutDisplayText(modifiers, key));
                    return;
                }
            }

            Debug.Log($"{CommandName}: could not register a shortcut");
        }

        public override void Execute(CommandContext context)
        {
            var editor = context.ActiveEditor;
            if (editor == null) return;

            var dataManager = editor.DataManager;
            var mainHandle = editor.AppDesignerProcess.MainWindowHandle;

            if (dataManager == null)
            {
                Task.Delay(100).ContinueWith(_ =>
                {
                    var handleWrapper = new WindowWrapper(mainHandle);
                    new MessageBoxDialog("Connect to a database to browse the Message Catalog.",
                        "Message Catalog", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                });
                return;
            }

            context.MainForm?.Invoke(() =>
            {
                using var dialog = new MessageCatalogDialog(dataManager, mainHandle);
                dialog.ShowDialog(new WindowWrapper(mainHandle));
                // TextToInsert handling arrives with the Insert feature (cold-insert task)
            });
        }
    }
}
```

Commands are auto-discovered by `CommandManager.DiscoverAndCacheCommands()` — no registration step.

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors. Fix any boilerplate-API mismatches against SnapshotHistoryDialog (see the implementer note in Step 1).

- [ ] **Step 4: Commit**

```bash
git add AppRefiner/Dialogs/MessageCatalogDialog.cs AppRefiner/Commands/BuiltIn/BrowseMessageCatalogCommand.cs
git commit -m "feat: message catalog browser dialog and palette command"
```

---

### Task 6: Cold insert with function picker

**Files:**
- Modify: `AppRefiner/Dialogs/MessageCatalogDialog.cs`
- Modify: `AppRefiner/Commands/BuiltIn/BrowseMessageCatalogCommand.cs`
- Modify: `AppRefiner/Properties/Settings.settings` and `AppRefiner/Properties/Settings.Designer.cs`

**Interfaces:**
- Consumes: Task 5's dialog internals, `MessageCatalogFunctions.FunctionNames` / `TryGetArgPositions` (Task 1), `ScintillaManager.InsertTextAtCursor(ScintillaEditor editor, string text) : bool` (existing, `ScintillaManager.cs:406`).
- Produces: `MessageCatalogDialog.TextToInsert` populated on OK; `BuildInsertText(int setNumber, int messageNumber, string defaultText) : string` private method (extended by Tasks 7 and 8); `Properties.Settings.Default.MessageCatalogInsertFunction : string`.

- [ ] **Step 1: Add the user setting**

In `AppRefiner/Properties/Settings.settings`, add inside `<Settings>` (match the XML shape of the existing `SnapshotDiffViewMode` entry):

```xml
    <Setting Name="MessageCatalogInsertFunction" Type="System.String" Scope="User">
      <Value Profile="(Default)">MsgGetText</Value>
    </Setting>
```

In `AppRefiner/Properties/Settings.Designer.cs`, add inside the `Settings` class (match the existing generated property shape exactly):

```csharp
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("MsgGetText")]
        public string MessageCatalogInsertFunction {
            get {
                return ((string)(this["MessageCatalogInsertFunction"]));
            }
            set {
                this["MessageCatalogInsertFunction"] = value;
            }
        }
```

- [ ] **Step 2: Add the function picker and Insert button to the dialog**

In `MessageCatalogDialog`, add two fields next to the other controls:

```csharp
        private readonly ComboBox functionCombo = new();
        private readonly Button insertButton = new();
```

In `InitializeComponent()`, after the `copyButton` block and before `closeButton`:

```csharp
            // function picker + insert
            this.functionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this.functionCombo.Size = new Size(160, 30);
            this.functionCombo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.functionCombo.Items.AddRange(MessageCatalogFunctions.FunctionNames.Cast<object>().ToArray());
            this.functionCombo.SelectedItem =
                MessageCatalogFunctions.FunctionNames.Contains(Properties.Settings.Default.MessageCatalogInsertFunction)
                    ? Properties.Settings.Default.MessageCatalogInsertFunction
                    : "MsgGetText";

            this.insertButton.Text = "Insert";
            this.insertButton.Size = new Size(90, 30);
            this.insertButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.insertButton.Enabled = false;
            StyleAccentButton(this.insertButton);
            this.insertButton.Click += (s, e) => AcceptSelection();
```

Add both to `buttonPanel` (with `copyButton`), and extend the post-layout re-anchor block at the end of `InitializeComponent()` to place four right-side controls:

```csharp
            this.copyButton.Location = new Point(this.buttonPanel.ClientSize.Width - 500, 10);
            this.functionCombo.Location = new Point(this.buttonPanel.ClientSize.Width - 370, 12);
            this.insertButton.Location = new Point(this.buttonPanel.ClientSize.Width - 205, 10);
            this.closeButton.Location = new Point(this.buttonPanel.ClientSize.Width - 105, 10);
```

- [ ] **Step 3: Implement accept + snippet building**

Add to `MessageCatalogDialog`:

```csharp
        private void AcceptSelection()
        {
            var entry = SelectedEntry;
            if (entry == null) return;

            TextToInsert = BuildInsertText(entry.SetNumber, entry.MessageNumber, entry.MessageText);
            Properties.Settings.Default.MessageCatalogInsertFunction = (string)functionCombo.SelectedItem!;
            Properties.Settings.Default.Save();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Cold insert: a complete call using the picked function, with the catalog
        /// text as the default-message string. PeopleCode escapes quotes by doubling.
        /// </summary>
        private string BuildInsertText(int setNumber, int messageNumber, string defaultText)
        {
            string escaped = defaultText.Replace("\"", "\"\"");
            string functionName = (string)functionCombo.SelectedItem!;
            MessageCatalogFunctions.TryGetArgPositions(functionName, out var argInfo);
            return $"{functionName}({argInfo.ColdLeadingArgs}{setNumber}, {messageNumber}, \"{escaped}\")";
        }
```

In `RenderPreview()`, alongside `copyButton.Enabled = entry != null;` add:

```csharp
            insertButton.Enabled = entry != null;
```

Wire Enter/double-click in the grid to accept — in `InitializeComponent()` after the `messagesListView` setup:

```csharp
            this.messagesListView.DoubleClick += (s, e) => AcceptSelection();
            this.messagesListView.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AcceptSelection(); }
            };
```

- [ ] **Step 4: Perform the insert in the command**

In `BrowseMessageCatalogCommand.Execute`, replace the dialog-show block:

```csharp
            context.MainForm?.Invoke(() =>
            {
                using var dialog = new MessageCatalogDialog(dataManager, mainHandle);
                if (dialog.ShowDialog(new WindowWrapper(mainHandle)) == DialogResult.OK
                    && !string.IsNullOrEmpty(dialog.TextToInsert))
                {
                    ScintillaManager.InsertTextAtCursor(editor, dialog.TextToInsert);
                }
            });
```

- [ ] **Step 5: Verify it compiles**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add AppRefiner/Dialogs/MessageCatalogDialog.cs AppRefiner/Commands/BuiltIn/BrowseMessageCatalogCommand.cs AppRefiner/Properties/Settings.settings AppRefiner/Properties/Settings.Designer.cs
git commit -m "feat: insert message catalog calls from the browser dialog"
```

---

### Task 7: New-message panel + free-range computation

**Files:**
- Create: `AppRefiner/MessageCatalogFreeRanges.cs`
- Modify: `AppRefiner/Dialogs/MessageCatalogDialog.cs`

**Interfaces:**
- Consumes: Task 5/6 dialog internals (`currentSetMessages`, `SelectedSetNumber`, `BuildInsertText`).
- Produces: `MessageCatalogFreeRanges.Compute(IReadOnlyCollection<int> usedNumbers) : List<MessageNumberRange>` with `record MessageNumberRange(int Start, int? End)` (`End == null` = open-ended tail).

Design agreement with Tim: **ranges are informational, the user owns the number.** The number box pre-fills with next-free but is freely editable; clicking a range pre-fills its start; live validation shows free/taken (with the colliding message text when taken).

- [ ] **Step 1: Write the pure range computation**

`AppRefiner/MessageCatalogFreeRanges.cs`:

```csharp
namespace AppRefiner
{
    /// <summary>A free run of message numbers. End == null means open-ended.</summary>
    public record MessageNumberRange(int Start, int? End)
    {
        public string Label => End == null
            ? $"{Start}+ (open)"
            : $"{Start}–{End} ({End.Value - Start + 1} free)";
    }

    /// <summary>
    /// Computes the free number ranges of a message set from its used numbers.
    /// Message numbers start at 1. An empty set yields a single open range at 1.
    /// </summary>
    public static class MessageCatalogFreeRanges
    {
        public static List<MessageNumberRange> Compute(IReadOnlyCollection<int> usedNumbers)
        {
            var ranges = new List<MessageNumberRange>();
            var sorted = usedNumbers.Distinct().OrderBy(n => n).ToList();

            if (sorted.Count == 0)
            {
                ranges.Add(new MessageNumberRange(1, null));
                return ranges;
            }

            if (sorted[0] > 1)
            {
                ranges.Add(new MessageNumberRange(1, sorted[0] - 1));
            }

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i + 1] > sorted[i] + 1)
                {
                    ranges.Add(new MessageNumberRange(sorted[i] + 1, sorted[i + 1] - 1));
                }
            }

            ranges.Add(new MessageNumberRange(sorted[^1] + 1, null));
            return ranges;
        }
    }
}
```

- [ ] **Step 2: Add the collapsible panel to the dialog**

Fields:

```csharp
        private readonly Button newMessageToggle = new();
        private readonly Panel newMessagePanel = new();
        private readonly FlowLayoutPanel rangesFlow = new();
        private readonly TextBox numberTextBox = new();
        private readonly Label validationLabel = new();
        private readonly TextBox intendedTextBox = new();
        private readonly Button insertNewButton = new();
```

In `InitializeComponent()`, before the split container is assembled (the panel docks to the bottom of `splitContainer.Panel1`, under the sets list):

```csharp
            // new-message panel (collapsed by default)
            this.newMessageToggle.Text = "▸ New message…";
            this.newMessageToggle.Dock = DockStyle.Bottom;
            this.newMessageToggle.Height = 28;
            this.newMessageToggle.FlatStyle = FlatStyle.Flat;
            this.newMessageToggle.FlatAppearance.BorderSize = 0;
            this.newMessageToggle.TextAlign = ContentAlignment.MiddleLeft;
            this.newMessageToggle.Click += (s, e) =>
            {
                newMessagePanel.Visible = !newMessagePanel.Visible;
                newMessageToggle.Text = newMessagePanel.Visible ? "▾ New message" : "▸ New message…";
                if (newMessagePanel.Visible) UpdateNewMessagePanel();
            };

            this.newMessagePanel.Dock = DockStyle.Bottom;
            this.newMessagePanel.Height = 190;
            this.newMessagePanel.Visible = false;
            this.newMessagePanel.Padding = new Padding(6);

            this.rangesFlow.Dock = DockStyle.Top;
            this.rangesFlow.Height = 52;
            this.rangesFlow.FlowDirection = FlowDirection.LeftToRight;
            this.rangesFlow.WrapContents = true;
            this.rangesFlow.AutoScroll = true;

            this.numberTextBox.Location = new Point(8, 60);
            this.numberTextBox.Width = 80;
            this.numberTextBox.TextChanged += (s, e) => ValidateChosenNumber();

            this.validationLabel.Location = new Point(96, 63);
            this.validationLabel.AutoSize = true;

            this.intendedTextBox.Location = new Point(8, 92);
            this.intendedTextBox.Width = 240;
            this.intendedTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.intendedTextBox.PlaceholderText = "Intended message text (optional)";

            this.insertNewButton.Text = "Insert as new";
            this.insertNewButton.Size = new Size(110, 28);
            this.insertNewButton.Location = new Point(8, 126);
            this.insertNewButton.Enabled = false;
            StyleAccentButton(this.insertNewButton);
            this.insertNewButton.Click += (s, e) => AcceptNewMessage();

            this.newMessagePanel.Controls.Add(this.rangesFlow);
            this.newMessagePanel.Controls.Add(this.numberTextBox);
            this.newMessagePanel.Controls.Add(this.validationLabel);
            this.newMessagePanel.Controls.Add(this.intendedTextBox);
            this.newMessagePanel.Controls.Add(this.insertNewButton);
```

And change the Panel1 assembly to include them (dock order matters — Fill control added first):

```csharp
            this.splitContainer.Panel1.Controls.Add(this.setsListView);
            this.splitContainer.Panel1.Controls.Add(this.setsFilterTextBox);
            this.splitContainer.Panel1.Controls.Add(this.newMessageToggle);
            this.splitContainer.Panel1.Controls.Add(this.newMessagePanel);
```

- [ ] **Step 3: Implement panel behavior**

```csharp
        private void UpdateNewMessagePanel()
        {
            rangesFlow.Controls.Clear();
            if (SelectedSetNumber == null)
            {
                validationLabel.Text = "Select a message set first";
                validationLabel.ForeColor = Color.FromArgb(150, 60, 60);
                return;
            }

            var usedNumbers = currentSetMessages.Select(m => m.MessageNumber).ToList();
            var ranges = MessageCatalogFreeRanges.Compute(usedNumbers);

            foreach (var range in ranges)
            {
                var link = new LinkLabel
                {
                    Text = range.Label,
                    AutoSize = true,
                    Margin = new Padding(0, 4, 12, 0),
                    Tag = range
                };
                link.LinkClicked += (s, e) =>
                    numberTextBox.Text = ((MessageNumberRange)((LinkLabel)s!).Tag!).Start.ToString();
                rangesFlow.Controls.Add(link);
            }

            // Pre-fill with next free after the highest used number (the last range is the open tail)
            numberTextBox.Text = ranges[^1].Start.ToString();
        }

        private void ValidateChosenNumber()
        {
            if (!int.TryParse(numberTextBox.Text.Trim(), out int number) || number < 1)
            {
                validationLabel.Text = "Enter a positive number";
                validationLabel.ForeColor = Color.FromArgb(150, 60, 60);
                insertNewButton.Enabled = false;
                return;
            }

            var colliding = currentSetMessages.FirstOrDefault(m => m.MessageNumber == number);
            if (colliding != null)
            {
                string text = colliding.MessageText.Length > 60
                    ? colliding.MessageText.Substring(0, 60) + "…"
                    : colliding.MessageText;
                validationLabel.Text = $"✗ {number} exists: \"{text}\"";
                validationLabel.ForeColor = Color.FromArgb(150, 60, 60);
                insertNewButton.Enabled = false;
            }
            else
            {
                validationLabel.Text = $"✓ {number} is free";
                validationLabel.ForeColor = Color.FromArgb(40, 130, 60);
                insertNewButton.Enabled = true;
            }
        }

        private void AcceptNewMessage()
        {
            if (SelectedSetNumber == null) return;
            if (!int.TryParse(numberTextBox.Text.Trim(), out int number)) return;

            TextToInsert = BuildInsertText(SelectedSetNumber.Value, number, intendedTextBox.Text);
            Properties.Settings.Default.MessageCatalogInsertFunction = (string)functionCombo.SelectedItem!;
            Properties.Settings.Default.Save();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
```

Also call `UpdateNewMessagePanel()` at the end of `OnSetSelected()` when the panel is visible:

```csharp
            if (newMessagePanel.Visible) UpdateNewMessagePanel();
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add AppRefiner/MessageCatalogFreeRanges.cs AppRefiner/Dialogs/MessageCatalogDialog.cs
git commit -m "feat: free message number finder in the catalog dialog"
```

---

### Task 8: Ctrl+Space in-call integration + context-aware insert

**Files:**
- Create: `AppRefiner/MessageCatalogCallContext.cs`
- Modify: `AppRefiner/Dialogs/MessageCatalogDialog.cs` (insert-mode constructor + tail-only insert)
- Modify: `AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs`

**Interfaces:**
- Consumes: `editor.GetParsedProgram()` (`ScintillaEditor.cs:363`, hash-cached best-effort AST), `ScintillaManager.GetScintillaText`, `program.FindDescendants<FunctionCallNode>()`, `MessageCatalogFunctions` (Task 1), the dialog (Tasks 5–7).
- Produces:
  - `class MessageCatalogCallContext { string FunctionName; MessageCatalogArgInfo ArgInfo; int CursorArgIndex; int? TypedSetNumber; bool HasDefaultTextArg; }`
  - `MessageCatalogCallContext.TryDetect(ScintillaEditor editor, int position) : MessageCatalogCallContext?`
  - `MessageCatalogDialog(IDataManager dataManager, MessageCatalogCallContext? callContext, IntPtr owner)` constructor overload

- [ ] **Step 1: Write the call-context detection**

`AppRefiner/MessageCatalogCallContext.cs`:

```csharp
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner
{
    /// <summary>
    /// Where the cursor sits inside a MsgGet-family call: which function, which
    /// argument index, whether the set is already a typed literal, and whether the
    /// call already has a default-text argument. Drives the dialog's insert mode.
    /// </summary>
    public class MessageCatalogCallContext
    {
        public string FunctionName { get; }
        public MessageCatalogArgInfo ArgInfo { get; }
        public int CursorArgIndex { get; }
        public int? TypedSetNumber { get; }
        public bool HasDefaultTextArg { get; }

        public MessageCatalogCallContext(string functionName, MessageCatalogArgInfo argInfo,
            int cursorArgIndex, int? typedSetNumber, bool hasDefaultTextArg)
        {
            FunctionName = functionName;
            ArgInfo = argInfo;
            CursorArgIndex = cursorArgIndex;
            TypedSetNumber = typedSetNumber;
            HasDefaultTextArg = hasDefaultTextArg;
        }

        /// <summary>
        /// Detects whether the cursor is inside the message_set or message_num argument
        /// of a mapped call. Returns null otherwise (caller falls through to the existing
        /// Ctrl+Space behavior). Uses the error-recovering AST for the call itself and a
        /// lexical comma count for the argument index, which tolerates half-typed args.
        /// Inherits the pre-existing byte-vs-char index caveat shared by all Ctrl+Space
        /// context detection.
        /// </summary>
        public static MessageCatalogCallContext? TryDetect(ScintillaEditor editor, int position)
        {
            var program = editor.GetParsedProgram();
            if (program == null) return null;

            string? text = ScintillaManager.GetScintillaText(editor);
            if (string.IsNullOrEmpty(text)) return null;

            // Innermost mapped call whose parens contain the cursor
            FunctionCallNode? call = null;
            MessageCatalogArgInfo argInfo = null!;
            foreach (var candidate in program.FindDescendants<FunctionCallNode>())
            {
                if (candidate.Function is not IdentifierNode ident) continue;
                if (!MessageCatalogFunctions.TryGetArgPositions(ident.Name, out var info)) continue;
                if (!candidate.SourceSpan.IsValid || !candidate.Function.SourceSpan.IsValid) continue;
                if (position <= candidate.Function.SourceSpan.End.ByteIndex) continue;   // must be past the name
                if (position > candidate.SourceSpan.End.ByteIndex) continue;
                if (call == null || candidate.SourceSpan.Start.ByteIndex > call.SourceSpan.Start.ByteIndex)
                {
                    call = candidate;
                    argInfo = info;
                }
            }
            if (call == null) return null;

            // Argument index = commas between the call's '(' and the cursor at depth 0,
            // outside string literals ("" escapes toggle twice and self-correct).
            int openParen = call.Function.SourceSpan.End.ByteIndex;
            while (openParen < text.Length && text[openParen] != '(') openParen++;
            if (openParen >= position) return null;

            int argIndex = 0, depth = 0;
            bool inString = false;
            for (int i = openParen + 1; i < position && i < text.Length; i++)
            {
                char c = text[i];
                if (inString) { if (c == '"') inString = false; continue; }
                if (c == '"') inString = true;
                else if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0) argIndex++;
            }

            if (argIndex != argInfo.SetArg && argIndex != argInfo.NumArg) return null;

            int? typedSet = null;
            if (call.Arguments.Count > argInfo.SetArg
                && call.Arguments[argInfo.SetArg] is LiteralNode literal
                && literal.LiteralType == LiteralType.Integer)
            {
                try { typedSet = Convert.ToInt32(literal.Value); } catch { }
            }

            bool hasDefaultText = call.Arguments.Count > argInfo.DefaultTxtArg;
            string functionName = ((IdentifierNode)call.Function).Name;

            return new MessageCatalogCallContext(functionName, argInfo, argIndex, typedSet, hasDefaultText);
        }
    }
}
```

- [ ] **Step 2: Add insert mode to the dialog**

In `MessageCatalogDialog`, add a field and the constructor overload (the Task 5 constructor chains to it):

```csharp
        private readonly MessageCatalogCallContext? callContext;
        private readonly LinkLabel unlockSetLink = new();

        public MessageCatalogDialog(IDataManager dataManager, IntPtr owner = default)
            : this(dataManager, null, owner)
        {
        }

        public MessageCatalogDialog(IDataManager dataManager, MessageCatalogCallContext? callContext,
            IntPtr owner = default)
        {
            this.dataManager = dataManager;
            this.callContext = callContext;
            this.owner = owner;
            InitializeComponent();
            LoadSets();
            ApplyCallContext();
        }
```

Add `ApplyCallContext` and the unlock link. In `InitializeComponent()`, after `setsFilterTextBox` setup:

```csharp
            this.unlockSetLink.Text = "Unlock set";
            this.unlockSetLink.AutoSize = true;
            this.unlockSetLink.Dock = DockStyle.Top;
            this.unlockSetLink.Visible = false;
            this.unlockSetLink.LinkClicked += (s, e) =>
            {
                setsListView.Enabled = true;
                setsFilterTextBox.Enabled = true;
                unlockSetLink.Visible = false;
            };
```

Add it to Panel1 between the filter box and the list (dock order: added after `setsFilterTextBox` in the `Controls.Add` sequence so it lands below the filter box).

```csharp
        private void ApplyCallContext()
        {
            if (callContext == null) return;

            // The function is already typed at the call site — no picker
            functionCombo.Visible = false;
            headerLabel.Text = $"Message Catalog — inserting into {callContext.FunctionName}(…)";

            if (callContext.TypedSetNumber != null)
            {
                foreach (ListViewItem item in setsListView.Items)
                {
                    if (((MessageSetInfo)item.Tag!).SetNumber == callContext.TypedSetNumber.Value)
                    {
                        item.Selected = true;
                        item.EnsureVisible();
                        break;
                    }
                }
                // Lock to the typed set, with an escape hatch in case the typed set was the mistake
                setsListView.Enabled = false;
                setsFilterTextBox.Enabled = false;
                unlockSetLink.Visible = true;
            }

            searchTextBox.Focus();
        }
```

Extend `BuildInsertText` to produce the in-call tail instead of a full call when a context exists (replace the Task 6 body):

```csharp
        private string BuildInsertText(int setNumber, int messageNumber, string defaultText)
        {
            string escaped = defaultText.Replace("\"", "\"\"");

            if (callContext == null)
            {
                string functionName = (string)functionCombo.SelectedItem!;
                MessageCatalogFunctions.TryGetArgPositions(functionName, out var argInfo);
                return $"{functionName}({argInfo.ColdLeadingArgs}{setNumber}, {messageNumber}, \"{escaped}\")";
            }

            // In-call: emit only what the call still needs from the cursor's position.
            // If a default-text arg already exists, don't add another.
            string tail = callContext.HasDefaultTextArg ? "" : $", \"{escaped}\"";
            return callContext.CursorArgIndex == callContext.ArgInfo.SetArg
                ? $"{setNumber}, {messageNumber}{tail}"
                : $"{messageNumber}{tail}";
        }
```

Guard the settings save in `AcceptSelection`/`AcceptNewMessage` (the combo is hidden in insert mode):

```csharp
            if (callContext == null)
            {
                Properties.Settings.Default.MessageCatalogInsertFunction = (string)functionCombo.SelectedItem!;
                Properties.Settings.Default.Save();
            }
```

- [ ] **Step 3: Add the Ctrl+Space branch**

In `AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs`, add usings for `AppRefiner.Dialogs` if missing, then in `Execute`, immediately after the `if (string.IsNullOrEmpty(text)) return;` guard and **before** `DetectAutocompleteContext` is called (this intentionally outranks the `(`/`,` → FunctionCallTip mapping for mapped calls at set/num positions; at default-text and later positions `TryDetect` returns null and the call tip behaves as before):

```csharp
            // Message Catalog: Ctrl+Space in the message_set/message_num argument of a
            // MsgGet-family call opens the catalog dialog in insert mode instead of a popup.
            if (editor.DataManager != null)
            {
                var catalogContext = MessageCatalogCallContext.TryDetect(editor, position);
                if (catalogContext != null)
                {
                    var mainHandle = editor.AppDesignerProcess.MainWindowHandle;
                    mainForm.Invoke(() =>
                    {
                        using var dialog = new MessageCatalogDialog(editor.DataManager, catalogContext, mainHandle);
                        if (dialog.ShowDialog(new WindowWrapper(mainHandle)) == DialogResult.OK
                            && !string.IsNullOrEmpty(dialog.TextToInsert))
                        {
                            ScintillaManager.InsertTextAtCursor(editor, dialog.TextToInsert);
                        }
                    });
                    return;
                }
            }
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add AppRefiner/MessageCatalogCallContext.cs AppRefiner/Dialogs/MessageCatalogDialog.cs AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs
git commit -m "feat: Ctrl+Space in MsgGet-family args opens catalog dialog in insert mode"
```

---

### Task 9: What's New + feature doc

**Files:**
- Modify: `AppRefiner/whats-new.txt` (add a bullet under `NEW FEATURES` in the current version block at the top)
- Create: `docs/features/message-catalog.md`

- [ ] **Step 1: Add the What's New bullet**

Insert after the last existing `NEW FEATURES` bullet in the top version block of `AppRefiner/whats-new.txt`:

```
• Message Catalog - Browse and search the Message Catalog without leaving
  Application Designer. Hover any MsgGet, MsgGetText, MsgGetExplainText,
  CreateException, MessageBox, or MsgBoxButtonOverride call to see the catalog
  message, severity, and explain text. Press Ctrl+Space inside the message set
  or number argument to pick a message from a searchable browser — it fills in
  the numbers and default text for you. Planning a new message? The dialog
  shows each set's free number ranges and validates the number you pick, with
  your intended text carried into the inserted code. Open it any time from the
  command palette: "Browse Message Catalog".
```

- [ ] **Step 2: Write the feature doc**

Create `docs/features/message-catalog.md` (match the tone/structure of `docs/features/snapshot-history.md`):

```markdown
# Message Catalog

AppRefiner brings the PeopleSoft Message Catalog into Application Designer.
It is read-only — creating catalog entries still happens in the browser — but
every lookup, search, and "which number is free?" question is answered in place.

## Hover tooltips

With a database connection, hover any call to `MsgGet`, `MsgGetText`,
`MsgGetExplainText`, `CreateException`, `MessageBox`, or `MsgBoxButtonOverride`
whose message set and number are literal values. The tooltip shows the set
description, severity, message text, and explain text. A reference with no
catalog entry shows "No catalog entry for set/num" — a quick typo check.

## The browser dialog

Open with **Browse Message Catalog** from the command palette (Ctrl+Shift+P)
or its keyboard shortcut. Message sets on the left (filter by number or
description), messages on the right with severity and text, and a preview pane
with the full explain text.

Searching filters the selected set as you type — matching message text and
explain text. Tick **All sets** and press Enter to search the whole catalog
(message text plus explain text, capped at 200 results).

**Insert** puts a complete call at your cursor — pick the function from the
dropdown (your choice is remembered) — with the catalog text as the default
message string. **Copy set, num** copies just the numbers.

## Inserting into an existing call

Press **Ctrl+Space** inside the message set or number argument of any of the
six functions. The catalog dialog opens in insert mode: if you already typed
the set number, it is pre-selected and locked (click "Unlock set" if that was
the mistake). Picking a message inserts only what the call still needs —
numbers plus default text.

## Finding a free number

Expand **New message** under the set list. Free ranges are shown for
orientation ("48–99 (52 free) · 205+ (open)") — click one to pre-fill its
start, or type any number you like; sometimes you want to leave a buffer in a
gap. Validation is live: a taken number shows the colliding message. Add your
intended text and **Insert as new** writes the call with your chosen number,
ready to paste into the catalog page when you create the real entry.

Data is cached per connection; the **Refresh** button re-reads the catalog
after someone adds entries.
```

- [ ] **Step 3: Commit**

```bash
git add AppRefiner/whats-new.txt docs/features/message-catalog.md
git commit -m "docs: message catalog integration in what's new and feature docs"
```

---

## Manual test plan (Tim, against a live Application Designer + DB)

1. **Tooltips:** hover each of the six functions with literal set/num → tooltip shows set description, severity, text, explain. `MessageBox(0, "", 20001, 13, ...)` and `MsgBoxButtonOverride(0, "", &btns, 20001, 13, ...)` resolve despite leading args. Nonexistent set/num → "No catalog entry". Variable args → no tooltip. Hover repeatedly → no repeated DB queries (check debug log).
2. **Explain text:** confirm DESCRLONG text appears in tooltips/preview, and that an all-sets search term appearing only in a message's explain text finds that message.
3. **Browse:** palette → Browse Message Catalog. Filter sets by number and by description; select a set → messages appear; type in search → grid filters on text *and* explain; tick All sets + Enter → cross-catalog results with cap label.
4. **Cold insert:** each function in the picker produces a well-formed call; catalog text with embedded quotes comes out doubled; picker choice persists across dialog opens (and app restarts).
5. **Ctrl+Space:** at `MsgGet(|`, `MsgGet(20001, |`, `MessageBox(0, "", |`, `MsgBoxButtonOverride(0, "", &btns, |` → dialog opens in insert mode; set locked when arg 1 is a typed literal; Unlock works; insert fills exactly the missing args; at `MsgGet(20001, 13, |` (default-text position) → normal call tip, not the dialog; outside any mapped call (including immediately after a closed call's `)`) → existing Ctrl+Space behavior unchanged. **Cross-set guard:** at `MsgGet(20000, |`, exercise both escape hatches — (a) all-sets search, pick a message from a different set; (b) Unlock, switch set, use Insert-as-new — and confirm a red hint appears instead of an insert; picking a same-set message still inserts normally.
6. **New message:** ranges match the set's real gaps (verify against a set with mid-range holes); typing a taken number shows the colliding text; a free mid-gap number validates green; Insert as new emits chosen number + intended text; empty set shows "1+ (open)".
7. **No DB:** command shows the "connect to a database" message; tooltips inert; Ctrl+Space falls through to normal behavior.
8. **Refresh:** add a catalog row in the PIA → Refresh → new row visible, gap ranges and validation updated.
9. **Dialog chrome:** resize grips on all edges, drag by header, Esc closes, Enter in grid inserts, dialog centers over App Designer.
