# Project Linting

AppRefiner's Project Linting feature provides comprehensive code quality analysis across entire PeopleSoft projects. Instead of linting individual files one at a time, Project Linting processes all PeopleCode definitions in a project simultaneously and generates a detailed HTML report—perfect for release validation, code reviews, and quality gates.

## Overview

Project Linting offers:

- **Batch Analysis** - Lint all PeopleCode in a project with one command
- **Comprehensive Reports** - HTML reports with filterable, sortable results
- **Progress Tracking** - Real-time progress dialog with cancellation support
- **Multi-Linter Support** - Runs all active linters simultaneously
- **Persistent Reports** - Timestamped HTML files for archival and sharing
- **Database Integration** - Directly queries project metadata from PeopleSoft
- **Type Filtering** - Organize results by PeopleCode type (Record, AppClass, etc.)
- **Severity Filtering** - Filter by Error, Warning, or Info
- **Automatic Report Opening** - Optionally open report in browser immediately

## Opening Project Linting

**Command Palette:** Search for "Project: Lint Project"

**Requirements:**
- Active database connection (required to query project metadata)
- Open PeopleCode file from the project you want to lint
- Configured lint report output directory

**Steps:**
1. Open any PeopleCode file from the target project in Application Designer
2. Open Command Palette (Ctrl+Shift+P)
3. Search for "Project: Lint Project"
4. Press Enter to start linting

## Linting Process

### Process Overview

When you invoke Project Linting, AppRefiner:

1. **Validates Prerequisites** - Checks database connection and report path
2. **Identifies Project** - Extracts project name from open editor
3. **Queries Project Metadata** - Retrieves all PeopleCode items from database
4. **Opens Progress Dialog** - Shows real-time linting progress
5. **Processes Each Item:**
   - Loads source code from database
   - Parses code into AST
   - Runs all active linters
   - Collects reports
   - Updates progress bar
6. **Generates HTML Report** - Creates formatted report with statistics
7. **Prompts to Open** - Optionally opens report in default browser

**Total Time:** Typically 1-5 minutes depending on project size and linter count.

### Progress Dialog

The linting progress dialog displays:

- **Header** - Current phase ("Linting Project", "Finalizing Report")
- **Progress Bar** - Visual progress (e.g., "45 of 200 items")
- **Status Text** - Current operation description
- **Cancel Button** - Gracefully stop linting mid-process

**Dialog Features:**
- Always-on-top display
- Centered on Application Designer
- Modal (blocks interaction until complete)
- Cancellable at any time
- Real-time progress updates

### Cancellation

Click the "Cancel" button to stop linting:

1. **Cancel button clicked** - Button disabled, text changes to "Cancelling..."
2. **Current item completes** - Finishes processing current PeopleCode file
3. **Loop exits** - No more items are processed
4. **Dialog closes** - No report generated
5. **Status returned** - Dialog returns DialogResult.Cancel

**Note:** Cancellation is graceful—the current item finishes processing before stopping.

## HTML Report

### Report File

Reports are saved with timestamped filenames:

```
<ProjectName>_LintReport_<Timestamp>.html
```

**Example:**
```
INSTALL_MENU_LintReport_20250115_143022.html
```

**Location:** Configured via LintReportPath setting (default: My Documents\LintReports)

### Report Structure

The HTML report contains:

#### 1. Header Section
- Project name
- Generation timestamp
- Total issues count

#### 2. Statistics Summary
Three stat boxes showing totals:
- **Errors** - Critical issues (red)
- **Warnings** - Potential problems (orange)
- **Info** - Informational findings (blue)

#### 3. Applied Linters Section
Collapsible list showing which linters were active:
- Linter name (e.g., "UnusedVariableLintRule")
- Linter description
- Clickable to expand/collapse

#### 4. Type Tabs
Filter results by PeopleCode type:
- **All Types** - Shows all issues
- **AppClass** - Application Class PeopleCode
- **FieldChange** - Record field events
- **RowInit** - Component row events
- **SaveEdit** - Component events
- **Workflow** - App Engine PeopleCode
- (Additional tabs for other types present in results)

#### 5. Severity Filters
Button-based filters:
- **All** - Shows all issues
- **Errors Only** - Shows only errors
- **Warnings Only** - Shows only warnings
- **Info Only** - Shows only info

#### 6. Program Results
Grouped sections for each PeopleCode program:

**Program Header:**
- Full program path (e.g., "EMPLOYEE.EMPLID.FieldChange")
- PeopleCode type indicator
- Issue count for this program

**Issue Table:**
| Type | Line | Message |
|------|------|---------|
| Error | 45 | Variable &empId declared but never used |
| Warning | 102 | Potential null reference in method call |

Issues are sorted by line number within each program.

### Report Interactivity

The HTML report is fully interactive:

**Filtering:**
- Click type tabs to show only that type
- Click severity filters to show only that severity
- Filters apply immediately without page reload

**Expanding/Collapsing:**
- Click linters header to toggle linter list
- Programs with zero issues (after filtering) are hidden

**Sorting:**
- Programs are alphabetically sorted
- Issues within programs are sorted by line number

**Styling:**
- Color-coded severity (red/orange/blue)
- Hover effects on rows for readability
- Responsive layout for different screen sizes

## Configuration

### Lint Report Path

**Setting:** `LintReportPath`
**Default:** `%USERPROFILE%\Documents\LintReports`

Specifies where HTML reports are saved.

**Changing the Path:**

1. Open AppRefiner Settings dialog
2. Navigate to "Lint Report Path" field
3. Click "Browse" button
4. Select desired directory
5. Click OK to save

**Or via File Menu:**
- AppRefiner > Settings > Browse (next to Lint Report Path field)

**Considerations:**
- Directory must exist before linting
- Ensure write permissions
- Network drives supported but may be slower
- Reports accumulate—consider periodic cleanup

### Active Linters

Project Linting uses **all active linters** configured in LinterManager.

**Viewing Active Linters:**
1. Open AppRefiner main window
2. View "Active Linters" section in main grid
3. Check "Active" column

**Enabling/Disabling Linters:**
- Use checkboxes in main AppRefiner window
- Changes apply immediately to next project lint
- Disabled linters are excluded from analysis

**Common Active Linters:**
- UnusedVariableLintRule
- UndeclaredVariableLintRule
- TypeErrorLintRule
- DeprecatedFunctionLintRule
- SQLInjectionLintRule
- (See [Linter Reference](linters-reference.md) for complete list)

## Database Requirements

### Required for Project Linting

Project Linting **requires an active database connection** to function:

**Required Database Access:**
- Read access to `PSPROJECTITEM` table (project metadata)
- Read access to `PSPCMPROG` table (PeopleCode program source)
- Valid connection to the PeopleSoft database

**Without Database Connection:**
- Project Linting command is disabled
- Error message shown if attempted

### Project Metadata Query

AppRefiner queries the following to identify project contents:

```sql
SELECT OBJECTTYPE, OBJECTID1, OBJECTVALUE1, OBJECTID2, OBJECTVALUE2,
       OBJECTID3, OBJECTVALUE3, OBJECTID4, OBJECTVALUE4
FROM PSPROJECTITEM
WHERE PROJECTNAME = ?
```

This retrieves all objects (records, app classes, components, etc.) in the specified project.

### PeopleCode Source Query

For each PeopleCode item found, AppRefiner retrieves source code:

```sql
SELECT PROGSEQ, PROGTXT
FROM PSPCMPROG
WHERE OBJECTID1 = ? AND OBJECTVALUE1 = ? ...
ORDER BY PROGSEQ
```

This reconstructs the full source code for parsing and analysis.

## Use Cases and Workflows

### Pre-Migration Validation

**Scenario:** Validate project quality before migrating to test environment

**Workflow:**
1. Complete all development work on project
2. Run Project Linting on the project
3. Review HTML report for any errors
4. Fix all errors found
5. Re-run Project Linting to verify fixes
6. Migrate to test environment when clean

**Benefits:**
- Catches issues before they reach test
- Reduces test environment failures
- Ensures code quality standards

### Release Quality Gate

**Scenario:** Enforce quality standards before production release

**Workflow:**
1. Create project with all production-bound changes
2. Run Project Linting
3. Require zero errors before approval
4. Archive report with release documentation
5. Approve migration to production

**Benefits:**
- Automated quality enforcement
- Documented quality proof
- Reduces production incidents

### Code Review Automation

**Scenario:** Augment manual code review with automated checks

**Workflow:**
1. Developer completes feature in project
2. Run Project Linting before code review
3. Developer fixes linting issues
4. Manual reviewer focuses on business logic
5. Both automated and manual review complete

**Benefits:**
- Frees reviewers from mechanical checks
- Faster review cycles
- Consistent code quality

### Technical Debt Assessment

**Scenario:** Measure code quality across legacy project

**Workflow:**
1. Create project containing legacy PeopleCode
2. Run Project Linting with all linters enabled
3. Generate report showing all issues
4. Categorize issues by severity and type
5. Create remediation backlog
6. Track progress with periodic re-linting

**Benefits:**
- Quantifies technical debt
- Prioritizes remediation work
- Tracks improvement over time

### Team Coding Standards Enforcement

**Scenario:** Ensure team follows coding standards

**Workflow:**
1. Configure linters matching team standards
2. Run Project Linting on team projects weekly
3. Share reports in team meetings
4. Address recurring issues in training
5. Monitor trends over time

**Benefits:**
- Objective standards enforcement
- Identifies training needs
- Improves team consistency

## Advanced Features

### Batch Processing Multiple Projects

For large-scale analysis, process multiple projects:

**Manual Batch Workflow:**
1. Open file from Project A
2. Run Project Linting → Report A generated
3. Open file from Project B
4. Run Project Linting → Report B generated
5. Compare reports

**Future Enhancement:** Batch mode could automate this.

### Report Comparison

Compare reports across time or environments:

1. **Baseline Report** - Run linting on stable version
2. **Current Report** - Run linting after changes
3. **Compare** - Open both HTML reports in browser tabs
4. **Analyze Differences:**
   - New issues introduced
   - Issues resolved
   - Severity changes

### Custom Linter Sets

Create focused analyses by enabling specific linters:

**Security Audit:**
- Enable only security-related linters
- SQLInjectionLintRule
- XSSVulnerabilityLintRule
- HardcodedCredentialsLintRule

**Performance Audit:**
- Enable performance-related linters
- InefficientLoopLintRule
- RedundantQueryLintRule
- UnoptimizedSQLLintRule

**Type Safety Audit:**
- Enable type-checking linters
- TypeErrorLintRule
- UndeclaredVariableLintRule
- NullReferenceLintRule

### Report Archival

Maintain historical reports for compliance:

**Organize by Release:**
```
LintReports/
├── Release_2025.01/
│   ├── INSTALL_MENU_LintReport_20250115_143022.html
│   ├── UPGRADE_PROJ_LintReport_20250115_150000.html
├── Release_2024.12/
│   ├── INSTALL_MENU_LintReport_20241220_120000.html
```

**Version Control Integration:**
- Commit reports to Git/SVN alongside code
- Track quality trends over releases
- Demonstrate compliance during audits

## Performance Considerations

### Linting Speed

Project linting performance depends on:

| Factor | Impact |
|--------|--------|
| **Project Size** | 100 items ≈ 30 seconds, 500 items ≈ 2-3 minutes |
| **Active Linters** | More linters = proportionally longer |
| **Database Speed** | Network latency affects retrieval |
| **File Size** | Large files (1000+ lines) take longer to parse |

**Optimization Tips:**
- Disable unnecessary linters before linting
- Use fast database connection (local or low-latency)
- Consider splitting very large projects (500+ items)
- Run during non-peak hours for large projects

### Memory Usage

Project linting uses memory for:
- Parsed ASTs (released after each file)
- Accumulated reports (grows throughout process)
- Database query results

**Memory Management:**
- AppRefiner calls `GC.Collect()` every 10 files
- Memory released after each file completes
- Typical usage: 200-500 MB for large projects

### Report Generation

HTML report generation is fast:
- JSON serialization: < 100ms
- HTML template processing: < 50ms
- File write: < 50ms

**Total report generation time:** < 200ms even for large projects

## Troubleshooting

### Project Name Not Detected

**Problem:** Error message "Please open a project first"

**Solutions:**
- Ensure you have a file from the target project open
- Verify Application Designer shows project name in caption
- Try opening a different file from the same project
- Check that file is not "Untitled"

### Database Connection Error

**Problem:** "Database connection required for project linting"

**Solutions:**
- Verify database connection is active in AppRefiner
- Check connection string in settings
- Test connection with other database features
- Ensure database credentials are valid
- Verify network connectivity to database

### Lint Report Path Error

**Problem:** "Lint report directory is not set or does not exist"

**Solutions:**
- Set LintReportPath in settings
- Verify directory exists on disk
- Ensure write permissions to directory
- Check for typos in path
- Use absolute path, not relative

### No Issues Found (Unexpected)

**Problem:** Report shows zero issues but code has obvious problems

**Solutions:**
- Verify linters are enabled (check Active column)
- Ensure linters are loaded correctly
- Check linter suppression comments in code
- Review linter configuration
- Test linters on individual files first

### Linting Takes Too Long

**Problem:** Project linting runs for 10+ minutes

**Solutions:**
- Check project size (may be very large)
- Disable unused linters to speed up
- Verify database connection speed
- Consider splitting project into smaller projects
- Check for network latency issues

### Report Not Opening

**Problem:** Clicking "Yes" to open report does nothing

**Solutions:**
- Verify report file exists at expected path
- Check file permissions (not read-only)
- Ensure default browser is configured
- Try opening report file manually from Explorer
- Check for antivirus blocking file access

### Cancelled Lint Still Running

**Problem:** Clicked Cancel but dialog stays open

**Solutions:**
- Wait for current file to finish processing
- Dialog closes after current iteration completes
- Check for very large file being processed
- If frozen, close Application Designer and restart

## Tips and Best Practices

### 1. Lint Before Migration

Always run Project Linting before migrating:

```
Development → Run Lint → Fix Issues → Re-lint → Migrate to Test
```

**Why:** Catches issues early in the pipeline.

### 2. Enable Relevant Linters Only

Don't enable every linter for every project:

```
Security-critical project: Enable security linters
Data processing project: Enable performance linters
New development: Enable all linters
```

**Why:** Reduces noise and focuses on relevant issues.

### 3. Establish Error-Free Policy

Require zero errors before migration:

```
Policy: Projects must have 0 errors in lint report
Warnings: Reviewed but not blocking
Info: Optional review
```

**Why:** Enforces minimum quality standard.

### 4. Archive Reports with Releases

Save reports alongside release documentation:

```
\\releases\2025.01\
  ├── migration_package.zip
  ├── release_notes.pdf
  └── lint_report.html
```

**Why:** Demonstrates due diligence and enables traceability.

### 5. Run Linting Weekly

Schedule regular linting during development:

```
Every Friday: Run lint on active projects
Review in team meeting
Track issue trends
```

**Why:** Prevents accumulation of technical debt.

### 6. Use Linting in CI/CD

Integrate with automated build/deployment:

```
Build Pipeline:
1. Pull code from repository
2. Deploy to dev environment
3. Run Project Linting (automated)
4. Fail build if errors found
5. Proceed to test if clean
```

**Why:** Automates quality gates.

## Integration with Other Features

### Works With Individual File Linting

Project Linting complements file-level linting:

- **File Linting** - Real-time feedback during development
- **Project Linting** - Comprehensive batch analysis before release

Use both for optimal workflow.

### Works With Custom Linters

All custom linters automatically included:

1. Develop custom linter (see [Linter Development](../developer-guide/custom-linters.md))
2. Mark as Active in AppRefiner
3. Project Linting includes it automatically

No additional configuration needed.

### Works With Database Features

Leverages database integration:

- Uses same connection as other features
- Shares metadata cache
- Consistent with Smart Open and navigation

## Comparison to File Linting

| Aspect | File Linting | Project Linting |
|--------|--------------|-----------------|
| **Scope** | Single file | Entire project |
| **Trigger** | Automatic (on edit) | Manual command |
| **Results** | Inline annotations | HTML report |
| **Purpose** | Real-time feedback | Batch validation |
| **Performance** | Instant | 1-5 minutes |
| **Persistence** | Temporary | Permanent report |
| **Sharing** | Not sharable | HTML file sharable |

### When to Use Each

**Use File Linting when:**
- Actively developing code
- Need immediate feedback
- Working on single file

**Use Project Linting when:**
- Completing feature development
- Preparing for migration
- Conducting code reviews
- Generating quality reports

## Related Features

- **[Linters Reference](linters-reference.md)** - Complete list of available linters
- **[Custom Linters](../developer-guide/custom-linters.md)** - Developing your own linters
- **[Code Styling](code-styling.md)** - Visual indicators for code issues
- **[Type Checking](type-checking.md)** - Type-related error detection
- **[Smart Open](smart-open.md)** - Navigate to definitions in reports

## Next Steps

- Configure [LintReportPath setting](#lint-report-path) for your team
- Review [active linters](#active-linters) and enable relevant ones
- Try [Project Linting](#opening-project-linting) on a small project first
- Establish [team policy](#establish-error-free-policy) for lint results
- Integrate into [release workflow](#release-quality-gate)
