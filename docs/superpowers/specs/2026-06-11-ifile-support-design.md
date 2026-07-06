# IFILE Support and Connect DB Feedback — Design

**Date:** 2026-06-11
**Status:** Approved

## Problem

Oracle's managed driver (Oracle.ManagedDataAccess.Core 23.26.100, the one AppRefiner ships) does not honor `IFILE=` directives in tnsnames.ora — verified empirically: an alias defined in an included file fails with ORA-12154, while the same alias directly in tnsnames.ora resolves. Only Oracle's native client (used by Application Designer itself) supports IFILE.

AppRefiner is broken at two layers for IFILE-based setups, common in enterprises that distribute a central tnsnames.ora via a network share and reference it from a local stub file:

1. **Dropdown:** `OracleDbConnection.GetAllTnsNames()` regex-matches `name =` patterns in the single tnsnames.ora, so an `IFILE=` line appears as a literal "IFILE" entry and the included aliases never appear.
2. **Connect:** even with the correct alias typed in, the managed driver cannot resolve it through the include.

Separately, the Connect DB button on the Linter tab silently does nothing (no popup, no Debug log) when no PeopleCode editor is active (`MainForm.btnConnectDB_Click` early-returns when `activeEditor == null`), which made this support case much harder to diagnose.

## Decisions

| Decision | Choice |
|---|---|
| Data Source at connect time | Pass full descriptor **only** for aliases resolved through an IFILE include; aliases defined directly in the main tnsnames.ora keep current alias-based behavior |
| Connect DB button | Drop the `activeEditor` requirement (only `activeAppDesigner` needed); show feedback when no App Designer session is tracked |
| Unreadable IFILE targets | Debug.Log + skip + warning hint label in the DB Connect dialog |
| UI blocking | Load TNS names / DSNs asynchronously; dialog opens immediately |
| Parser structure | Dedicated `TnsNamesParser` static class |

## Components

### 1. TnsNamesParser (new — `AppRefiner/Database/TnsNamesParser.cs`)

Pure file/string logic, no UI or driver dependencies.

```csharp
public record TnsEntry(string Alias, string Descriptor, string SourceFile, bool FromInclude);

public class TnsParseResult
{
    public List<TnsEntry> Entries { get; }
    public List<string> FailedIncludes { get; }   // "path — error message"
}

public static TnsParseResult Parse(string tnsNamesPath);
```

Parsing rules:

- `#` starts a comment, stripped to end of line.
- At paren depth 0, a line matching `IFILE = <path>` (case-insensitive) is a directive, not an alias. Path cleanup: trim whitespace, surrounding single/double quotes, and invisible Unicode characters (U+200B zero-width space, U+FEFF BOM — observed in a real customer file). Relative paths resolve against the directory of the *including* file. Forward slashes are passed through unchanged (Windows file APIs accept them).
- Includes recurse with a depth limit of 3 (Oracle's documented nesting limit) and a case-insensitive visited-set of full paths for cycle protection.
- An unreadable include (not found, IO error, unreachable network path) is recorded in `FailedIncludes` with the exception message, logged via `Debug.Log`, and parsing continues with remaining content.
- Entry extraction: at depth 0, `alias1 [, alias2 ...] = (` followed by balanced-paren scanning. The descriptor is the balanced `( ... )` block collapsed to a single line. Multi-alias entries yield one `TnsEntry` per alias sharing the descriptor. Unbalanced parens at EOF: keep entries parsed so far, log a warning.
- Includes are processed at the point of the `IFILE` line, in document order. If the same alias is defined more than once (e.g., in both the main file and an include), the **first** occurrence wins and later duplicates are ignored — matching Oracle's first-match resolution.
- A failure reading the *root* tnsnames.ora returns an empty result (current behavior) plus a Debug.Log.

### 2. OracleDbConnection changes

- New `public static TnsParseResult GetTnsEntries()` — resolves the tnsnames.ora path exactly as `GetTnsNamesPath()` does today (Settings TNS_ADMIN → env TNS_ADMIN → ORACLE_HOME) and delegates to `TnsNamesParser.Parse`.
- `GetAllTnsNames()` becomes a thin wrapper returning `GetTnsEntries().Entries.Select(e => e.Alias)` — existing callers unaffected. The literal "IFILE" dropdown entry disappears as a side effect.

### 3. DBConnectDialog changes

- New field `tnsServiceDescriptors` (`Dictionary<string, string>`, OrdinalIgnoreCase), populated **only** from entries with `FromInclude == true` — mirrors the existing `ldapServiceDescriptors` pattern.
- Standard Oracle connect branch (`ConnectButton_Click`): if the selected DB name is in `tnsServiceDescriptors`, pass the descriptor as `Data Source=`; otherwise pass the alias as today. Debug.Log which form was used (mirrors the LDAP path's log line).
- **Async load:** `LoadAllDatabaseConnections()` (Oracle TNS entries + SQL Server DSNs) runs on a background task instead of in the constructor. While loading, the DB Name dropdown is disabled and shows a "Loading…" placeholder. On completion, `BeginInvoke`: populate the dropdown, populate `tnsServiceDescriptors`, run `SelectDatabaseByName(defaultDbName)`, re-enable the dropdown, and show/hide the include-failure hint. The existing auto-connect-on-initial-load flow continues to fire from `DbNameComboBox_SelectedIndexChanged`, just after async population.
- **Include-failure hint:** a small warning label under the DB Name row, visible only when database type is Oracle and `FailedIncludes` is non-empty: `"N tnsnames include file(s) could not be read — see Debug Log."`

### 4. Connect DB entry points

- `MainForm.btnConnectDB_Click` (MainForm.cs ~1362): remove the `activeEditor == null` early return; require only `activeAppDesigner`. The `activeEditor.DataManager = manager` assignment becomes null-conditional (the loop over `Editors.Values` already covers it).
- When `activeAppDesigner == null`: `Debug.Log` the reason and show a `MessageBoxDialog` owned by MainForm explaining that no Application Designer session has been detected.
- `DatabaseConnectCommand.Execute`: add a `Debug.Log` when `ActiveAppDesigner` is null (currently a silent no-op).

## Error handling summary

| Failure | Behavior |
|---|---|
| Root tnsnames.ora missing/unreadable | Empty list (as today) + Debug.Log |
| Include unreadable | Skip, Debug.Log, count surfaces in dialog hint |
| Include depth > 3 or cycle | Skip deeper include, Debug.Log |
| Malformed entry / unbalanced parens | Keep parsed entries, Debug.Log warning |
| No App Designer on Connect DB | Debug.Log + MessageBoxDialog |

## Testing

- **No test project is added to the repo** (project decision). `TnsNamesParser` is verified during development with a throwaway console harness outside the repo (`C:\temp\tnsparse-check`, references AppRefiner.csproj) covering: comments, multi-alias entries, nested parens in descriptors, IFILE absolute / relative / quoted / forward-slash paths, depth limit, cycle detection, missing include, zero-width-character cleanup, unbalanced EOF, duplicate-alias resolution. The harness is not committed.
- **Manual verification:** replicate the customer setup — `C:\temp\TNS_ADMIN_TEST\tnsnames.ora` containing only `IFILE=\\ad.company.com\NETLOGON\tnsnames.ora`, AppRefiner TNS_ADMIN setting pointed at that folder. Expect: full alias list in dropdown, no literal "IFILE" entry, successful connect via descriptor, hint label when the include path is made unreachable.
- The standalone harness at `C:\temp\ifiletest\` can confirm descriptor-as-DataSource reaches the network layer (expect ORA-50201 against a fake listener, not ORA-12154).

## Out of scope

- IFILE in sqlnet.ora / ldap.ora
- EZConnect support in the dropdown
- Any change to the LDAP connection path
- Async/timeout hardening of other network file reads elsewhere in the app
