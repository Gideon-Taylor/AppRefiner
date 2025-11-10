# Snapshot History

AppRefiner's Snapshot History feature provides automatic version control for PeopleCode files directly within Application Designer. Every time you save a file, AppRefiner captures a snapshot of the content, allowing you to view previous versions, compare changes, and revert to any point in time—all without external version control systems.

## Overview

Snapshot History offers:

- **Automatic Snapshots** - Every save creates a versioned snapshot
- **Unlimited History** - Configurable retention with automatic cleanup
- **Visual Diff Viewer** - See exactly what changed between versions
- **One-Click Revert** - Restore any previous version instantly
- **Local Storage** - SQLite database with fast access and no server dependency
- **Per-File History** - Track changes to individual definitions independently
- **Database Scoped** - Separate history per PeopleSoft database
- **Content Preview** - View full content of any historical version

## How It Works

### Automatic Snapshot Creation

Snapshots are created automatically whenever you save a file in Application Designer:

1. **User saves file** - Press Ctrl+S or use File > Save
2. **Application Designer writes to database** - File is saved to PeopleSoft database
3. **AppRefiner captures content** - After save completes, AppRefiner captures the content
4. **Snapshot stored** - Content saved to local SQLite database with timestamp
5. **Old snapshots cleaned up** - If limit exceeded, oldest snapshots are removed

**No user action required** - Snapshots happen transparently in the background.

### Storage Location

Snapshots are stored in a local SQLite database:

```
%APPDATA%\AppRefiner\Snapshots.db
```

**Example path:**
```
C:\Users\YourName\AppData\Roaming\AppRefiner\Snapshots.db
```

This database is:
- Stored locally on your machine
- Independent of PeopleSoft databases
- Portable (can be backed up or moved)
- Fast and lightweight (SQLite)
- Indexed for quick retrieval

### Snapshot Retention

By default, AppRefiner keeps the **10 most recent snapshots** per file. This can be configured in settings.

**Automatic Cleanup:**
- When the 11th snapshot is saved, the oldest is deleted
- Cleanup happens automatically after each save
- Only affects files that exceed the limit
- Ensures database doesn't grow indefinitely

## Opening Snapshot History

**Command Palette:** Search for "Snapshot: Revert to Previous Version"

To view snapshot history:

1. Open the file in Application Designer
2. Open Command Palette (Ctrl+Shift+P)
3. Search for "Snapshot: Revert to Previous Version"
4. Press Enter to open the Snapshot History dialog

**Requirements:**
- File must have been saved at least once through AppRefiner
- File must have a valid path (not "Untitled")

## Dialog Layout

The Snapshot History dialog consists of:

- **Header Bar** - "Snapshot History" title with dark background
- **History List** - All snapshots for the current file, newest first
- **Action Buttons** - Revert, View Content, View Diff, Cancel
- **Selection Highlight** - Selected snapshot is highlighted

### History List Columns

| Column | Width | Description |
|--------|-------|-------------|
| **Date** | 120px | Timestamp when snapshot was created |
| **Message** | 420px | File caption at time of save |

### Snapshot Display Format

Each snapshot displays:

```
Date                     Message
2025-01-15 14:23:45     EMPLOYEE.EMPLID.FieldChange
2025-01-15 13:10:22     EMPLOYEE.EMPLID.FieldChange
2025-01-14 16:45:00     EMPLOYEE.EMPLID.FieldChange
```

Snapshots are sorted **newest first** - the most recent save appears at the top.

## Viewing Snapshots

### View Content

**Action:** Click "View Content" button

Opens a read-only text viewer showing the complete content of the selected snapshot.

**Use Cases:**
- Review what code looked like at a specific point in time
- Verify a snapshot contains the correct version before reverting
- Reference old implementations during refactoring
- Compare against external code

**Features:**
- Syntax-highlighted display (PeopleCode)
- Scrollable view for large files
- Modal dialog stays on top
- Title shows file name and timestamp

### View Diff

**Action:** Click "View Diff" button

Opens a unified diff viewer showing changes between the selected snapshot and current editor content.

**Diff Format:**

```diff
--- RECORD.FIELD.EVENT (Old)
+++ RECORD.FIELD.EVENT (New)

 Local string &name;
-&name = "Original";
+&name = "Modified";
 MessageBox(0, "", 0, 0, &name);
```

**Diff Symbols:**
- ` ` (space) - Unchanged line
- `-` (minus) - Line deleted (present in current, removed in snapshot)
- `+` (plus) - Line added (not in current, present in snapshot)
- `~` (tilde) - Line modified

**Use Cases:**
- See what changed between now and a specific save
- Understand what would be reverted
- Identify when a bug was introduced
- Track evolution of implementation

**Note:** The diff shows what would change if you reverted. Old = current editor, New = selected snapshot.

## Reverting to a Snapshot

### Revert Process

**Action:** Click "Revert to Selected" button

1. **Select a snapshot** from the history list
2. **Click "Revert to Selected"**
3. **Confirmation dialog appears:**
   ```
   Are you sure you want to revert [filename] to the version from [timestamp]?

   This will replace the current content in the editor.
   ```
4. **Click Yes** to proceed or **No** to cancel
5. **Editor content is replaced** with snapshot content
6. **Dialog closes** automatically

### What Happens During Revert

When you revert to a snapshot:

1. **Editor content is replaced** - Current content is overwritten
2. **No automatic save** - You must save manually (Ctrl+S) to persist the revert
3. **Navigation history cleared** - Undo/redo state is reset
4. **New snapshot created** - When you save, a new snapshot is created

### Important Notes

**Revert is NOT Permanent Until Saved:**
- Reverting only changes the editor content
- You must save (Ctrl+S) to commit the revert to the database
- You can undo the revert by closing without saving

**Original Content Lost:**
- If you save after reverting, the current content becomes a new snapshot
- The content you reverted from is still available as a snapshot
- You can revert back if needed

**Best Practice:**
- Always use "View Diff" before reverting to verify changes
- Save immediately after reverting to create a snapshot of the pre-revert state
- Test reverted code before saving to database

## Configuration

### Max Snapshots Per File

**Setting:** `MaxFileSnapshots`
**Default:** `10`
**Location:** App.config

Controls how many snapshots are retained per file. When this limit is exceeded, the oldest snapshots are automatically deleted.

**Changing the Setting:**

1. Close AppRefiner
2. Open `%APPDATA%\AppRefiner\user.config` (or App.config in installation directory)
3. Find the `MaxFileSnapshots` setting:
   ```xml
   <setting name="MaxFileSnapshots" serializeAs="String">
       <value>10</value>
   </setting>
   ```
4. Change the value (e.g., `20`, `50`, `100`)
5. Save the file
6. Restart AppRefiner

**Recommended Values:**
- **10** - Default, suitable for most users (minimal disk space)
- **20-30** - More history for frequently edited files
- **50-100** - Extensive history for critical files
- **0** - Unlimited (no automatic cleanup, database grows indefinitely)

**Disk Space Considerations:**
- Each snapshot stores full file content
- Average PeopleCode file: 10-50 KB
- 10 snapshots per file ≈ 100-500 KB per file
- Database can grow large with unlimited snapshots

### Snapshot Database Path

**Setting:** `SnapshotDatabasePath`
**Default:** `%APPDATA%\AppRefiner\Snapshots.db`

The location where snapshots are stored. Can be customized to use a network drive or different location.

**Changing the Path:**

1. Close AppRefiner
2. Edit user.config or App.config
3. Set `SnapshotDatabasePath` to desired location
4. Restart AppRefiner

**Considerations:**
- Network drives may be slower
- Ensure write permissions exist
- Database is created automatically if missing
- Moving database moves all history

## Database Schema

The SQLite database uses a simple schema:

### Snapshots Table

| Column | Type | Description |
|--------|------|-------------|
| **Id** | INTEGER PRIMARY KEY | Unique snapshot identifier |
| **DBName** | TEXT | PeopleSoft database name (e.g., "HRDEV") |
| **FilePath** | TEXT | Relative path to file (e.g., "RECORD.FIELD.EVENT") |
| **Caption** | TEXT | File caption at time of save |
| **CreatedAt** | TEXT | Timestamp (ISO 8601 format) |
| **Content** | TEXT | Full file content |

### Indexes

- **idx_snapshots_filepath** - Speeds up queries by file path
- **idx_snapshots_dbname** - Speeds up queries by database name

### Query Examples

**Get all snapshots for a file:**
```sql
SELECT * FROM Snapshots
WHERE FilePath = 'RECORD.EMPLOYEE.FieldChange'
  AND DBName = 'HRDEV'
ORDER BY CreatedAt DESC;
```

**Count total snapshots:**
```sql
SELECT COUNT(*) FROM Snapshots;
```

**Find files with most snapshots:**
```sql
SELECT FilePath, COUNT(*) as SnapshotCount
FROM Snapshots
GROUP BY FilePath
ORDER BY SnapshotCount DESC;
```

## Use Cases and Workflows

### Emergency Rollback

**Scenario:** You just saved breaking changes and need to restore the working version.

**Workflow:**
1. Open Command Palette: "Snapshot: Revert to Previous Version"
2. Select the snapshot from before your changes
3. Click "View Diff" to verify it's the correct version
4. Click "Revert to Selected"
5. Confirm the revert
6. Test the code in the editor
7. Save (Ctrl+S) to commit the rollback

**Time to recovery:** ~30 seconds

### Compare Recent Changes

**Scenario:** You want to see what changed in your last few edits.

**Workflow:**
1. Open Snapshot History
2. Select a snapshot from several hours/days ago
3. Click "View Diff"
4. Review the unified diff to see all changes
5. Close dialog (no revert needed)

### Reference Old Implementation

**Scenario:** You refactored code but need to reference the old approach.

**Workflow:**
1. Open Snapshot History
2. Select snapshot from before refactoring
3. Click "View Content"
4. Review the old implementation
5. Copy any needed code snippets
6. Close dialog

### Track Bug Introduction

**Scenario:** A bug appeared recently, and you need to find when it was introduced.

**Workflow:**
1. Open Snapshot History
2. Start with most recent snapshot
3. Click "View Content" to check if bug exists
4. Move to older snapshots until bug disappears
5. Use "View Diff" to see exact change that introduced bug
6. Fix the bug based on the diff

### Incremental Undo Across Sessions

**Scenario:** You made changes over multiple days and want to undo step-by-step.

**Workflow:**
1. Open Snapshot History
2. Select the snapshot immediately before your last change
3. Revert to that snapshot
4. Save to create a new snapshot
5. Repeat for each change you want to undo
6. Each revert creates a new snapshot, preserving history

## Integration with PeopleSoft

### Database Scope

Snapshots are tied to the PeopleSoft database name:

- **HRDEV** snapshots are separate from **HRPROD** snapshots
- Same file path in different databases has independent history
- Allows tracking dev vs. prod changes separately

**Example:**
```
Database: HRDEV
FilePath: RECORD.EMPLOYEE.FieldChange
Snapshots: 10 versions from development work

Database: HRPROD
FilePath: RECORD.EMPLOYEE.FieldChange
Snapshots: 3 versions from production hotfixes
```

### File Path Identification

Files are identified by their relative path format:

**Record Field Event:**
```
RECORD.EMPLOYEE.FieldChange
```

**Application Class:**
```
AppPackage:AppClass.MethodName
```

**Component Event:**
```
COMPONENT.EMP_PERSONAL_DATA.Activate
```

This ensures snapshots are correctly associated with the right definition.

### Snapshots vs. PeopleSoft Database

**Important Distinctions:**

| Aspect | Snapshots | PeopleSoft Database |
|--------|-----------|---------------------|
| **Storage** | Local SQLite file | PeopleSoft database tables |
| **Scope** | Development machine only | Shared across environment |
| **Purpose** | Personal version control | Authoritative source |
| **Revert Impact** | Editor only | None until saved |
| **Migration** | Not migrated | Migrated via projects |

**Key Point:** Reverting to a snapshot only changes your editor. You must save (Ctrl+S) to write the reverted content to the PeopleSoft database.

## Performance Considerations

### Snapshot Creation

- **Trigger:** SAVEPOINTREACHED event (after Ctrl+S)
- **Timing:** Asynchronous, doesn't block save operation
- **Duration:** < 100ms for typical files
- **Impact:** Negligible performance impact

### Database Access

- **SQLite Performance:**
  - Index-based queries are extremely fast
  - Typical query time: < 10ms
  - No network latency (local file)
  - Concurrent access supported

- **Large Files:**
  - Files up to 1 MB: No noticeable delay
  - Files > 1 MB: Slight delay during save
  - Content is compressed (gzip) for storage

### Database Size

**Growth Rate:**
- 10 snapshots per file × 50 KB average = 500 KB per file
- 100 edited files = 50 MB database size
- Typical database: 10-100 MB

**Management:**
- Automatic cleanup prevents unbounded growth
- Manual cleanup possible (SQL DELETE queries)
- Vacuum database periodically to reclaim space

## Advanced Usage

### Manual Database Queries

Since snapshots are stored in SQLite, you can query them directly:

**Open Database:**
```powershell
# Using SQLite command-line tool
sqlite3 "$env:APPDATA\AppRefiner\Snapshots.db"
```

**Useful Queries:**

**Find all snapshots for a specific file:**
```sql
SELECT Id, CreatedAt, Caption
FROM Snapshots
WHERE FilePath LIKE '%EMPLOYEE%'
ORDER BY CreatedAt DESC;
```

**Export snapshot content to file:**
```sql
.output snapshot_content.txt
SELECT Content FROM Snapshots WHERE Id = 123;
.output stdout
```

**Delete snapshots older than 30 days:**
```sql
DELETE FROM Snapshots
WHERE CreatedAt < datetime('now', '-30 days');
```

**Vacuum to reclaim space:**
```sql
VACUUM;
```

### Backup and Restore

**Backup Snapshots:**
```powershell
# Copy database file
Copy-Item "$env:APPDATA\AppRefiner\Snapshots.db" "C:\Backup\Snapshots_backup.db"
```

**Restore Snapshots:**
```powershell
# Replace current database
Copy-Item "C:\Backup\Snapshots_backup.db" "$env:APPDATA\AppRefiner\Snapshots.db"
```

**Schedule Automated Backups:**
Use Windows Task Scheduler to copy the database file daily/weekly.

### Migrating Snapshots Between Machines

1. **Export database** from old machine
2. **Copy Snapshots.db** to new machine
3. **Place in AppRefiner folder** on new machine
4. **Restart AppRefiner** to recognize the database

Snapshots will be available immediately on the new machine.

## Troubleshooting

### Dialog Shows No History

**Problem:** Snapshot History dialog is empty

**Solutions:**
- Verify you've saved the file at least once through AppRefiner
- Check that file has a valid path (not "Untitled")
- Ensure `SnapshotDatabasePath` setting is correct
- Verify database file exists at the configured path
- Check database permissions (read/write access)

### Snapshots Not Being Created

**Problem:** Saving files doesn't create snapshots

**Solutions:**
- Check `SaveSnapshot()` is being called (view debug log)
- Verify AppRefiner is enabled for Application Designer process
- Ensure file has a valid `RelativePath` (not empty)
- Check disk space on drive containing database
- Review debug log for database errors

### Revert Not Working

**Problem:** Clicking "Revert to Selected" doesn't change editor content

**Solutions:**
- Verify snapshot ID is valid (not corrupted)
- Check editor is still open and valid
- Ensure you have write permissions to editor window
- Try closing and reopening the file
- Check for error messages in debug log

### Database Corruption

**Problem:** SQLite database is corrupted or damaged

**Solutions:**
- Try opening database with SQLite tool to diagnose
- Use SQLite `.recover` command to extract data
- Restore from backup if available
- Delete database and start fresh (loses all history)

**Recovery Commands:**
```sql
# In sqlite3 command-line
PRAGMA integrity_check;
VACUUM;
```

### Diff Shows Unexpected Changes

**Problem:** Diff viewer shows changes you didn't make

**Solutions:**
- Remember: diff compares current editor vs. selected snapshot
- Current = what you have now, Snapshot = what you're reverting to
- Use "View Content" to see exact snapshot content
- Verify you selected the correct snapshot from the list

### Large Database Size

**Problem:** Snapshots.db file is very large (> 500 MB)

**Solutions:**
- Reduce `MaxFileSnapshots` setting (e.g., from 50 to 10)
- Delete old snapshots manually using SQL queries
- Run VACUUM to reclaim space after deletions
- Consider archiving old snapshots to backup location
- Increase cleanup frequency

**Cleanup Example:**
```sql
-- Delete snapshots older than 90 days
DELETE FROM Snapshots WHERE CreatedAt < datetime('now', '-90 days');

-- Reclaim disk space
VACUUM;
```

## Tips and Best Practices

### 1. Review Diffs Before Reverting

Always click "View Diff" before "Revert to Selected":

```
Workflow:
1. Select snapshot
2. View Diff (verify changes)
3. Revert to Selected
4. Save immediately
```

**Why:** Prevents accidental data loss and ensures you're reverting to the correct version.

### 2. Save Immediately After Reverting

After reverting, save right away:

```
Revert → Ctrl+S → New snapshot created
```

**Why:** Creates a snapshot of the pre-revert state, allowing you to undo the revert if needed.

### 3. Use Descriptive Captions

When saving, Application Designer's caption becomes the snapshot message. Use clear names:

```
Good: EMPLOYEE.EMPLID.FieldChange
Bad: Untitled
```

**Why:** Makes snapshot history easier to navigate.

### 4. Set Appropriate Retention

Adjust `MaxFileSnapshots` based on your needs:

- **Frequently edited files:** 20-50 snapshots
- **Rarely edited files:** 5-10 snapshots
- **Critical files:** 50-100 snapshots

**Why:** Balances history depth with database size.

### 5. Backup Regularly

Schedule periodic backups of Snapshots.db:

```powershell
# Weekly backup via Task Scheduler
Copy-Item "$env:APPDATA\AppRefiner\Snapshots.db" "\\backup\Snapshots_$(Get-Date -f 'yyyy-MM-dd').db"
```

**Why:** Protects against database corruption or accidental deletion.

### 6. Combine with External Version Control

Use snapshots for:
- Immediate rollback during development
- Comparing recent changes
- Recovering from accidental edits

Use Git/SVN for:
- Long-term history
- Team collaboration
- Branching and merging

**Why:** Snapshots provide instant local history, while version control provides comprehensive team history.

## Comparison to Other Version Control

| Feature | Snapshots | Git/SVN | PeopleSoft Change Control |
|---------|-----------|---------|---------------------------|
| **Automatic** | ✅ Yes | ❌ Manual commits | ❌ Manual checkout |
| **Local Storage** | ✅ Yes | ✅ Yes | ❌ Server-based |
| **Instant Access** | ✅ Yes | ⚠️ Requires checkout | ❌ Requires process |
| **Team Sharing** | ❌ No | ✅ Yes | ✅ Yes |
| **Branching** | ❌ No | ✅ Yes | ❌ Limited |
| **Merge Support** | ❌ No | ✅ Yes | ⚠️ Manual |
| **Setup Required** | ❌ No | ✅ Yes | ✅ Yes |
| **Granularity** | Per save | Per commit | Per migration |

### When to Use Snapshots

**Use Snapshots when:**
- Making frequent experimental changes
- Need instant rollback capability
- Working solo on development tasks
- Want automatic version control without setup

**Use Git/SVN when:**
- Working on team projects
- Need branching and merging
- Require long-term history tracking
- Want to share code with others

**Use PeopleSoft Change Control when:**
- Migrating between environments
- Tracking official releases
- Coordinating team changes
- Managing production deployments

### Combining All Three

**Best Practice Workflow:**
1. **Snapshots** - Automatic safety net during development
2. **Git** - Commit major milestones and working features
3. **PeopleSoft** - Migrate completed changes to test/prod

This provides comprehensive version control at all levels.

## Related Features

- **[Navigation](navigation.md)** - Navigate through code with history tracking
- **[Better Find](better-find-replace.md)** - Search and replace across files
- **[Code Styling](code-styling.md)** - Visual indicators for code issues
- **[Type Checking](type-checking.md)** - Detect type-related errors

## Next Steps

- Configure [MaxFileSnapshots setting](#max-snapshots-per-file) for your needs
- Set up [automated backups](#backup-and-restore) of snapshot database
- Practice [reverting workflows](#reverting-to-a-snapshot) on non-critical files
- Explore [manual database queries](#manual-database-queries) for advanced usage
