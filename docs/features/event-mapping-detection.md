# Event Mapping Detection

AppRefiner's Event Mapping Detection feature automatically identifies and displays PeopleSoft Event Mapping configurations directly within the code editor. When viewing Component or Page PeopleCode, AppRefiner queries the database and shows which Application Classes are event mapped to execute before, after, or instead of your code—eliminating the need to manually check definitions or trace execution flow.

## Overview

Event Mapping Detection provides:

- **Automatic Detection** - Identifies event mappings on file open and save
- **Visual Annotations** - Displays mappings as gray annotations above/below code
- **Sequence Display** - Shows Pre, Post, and Override (Replace) mappings
- **Class Path or Full Text** - Toggle between compact paths and complete source
- **Cross-Reference Tracking** - Shows where current Application Class is event mapped
- **Database Integration** - Queries `PSAEEVENTMAP` table for mappings
- **Support for All Types** - Component, Page, Record, and Field events
- **Zero Configuration** - Works automatically with database connection

## How It Works

### Event Mapping in PeopleSoft

PeopleSoft Event Mapping allows Application Classes to be injected into the execution flow of Component and Page PeopleCode events:

**Execution Sequence:**
```
1. Pre-Event Mapped Classes (Sequence 1, 2, 3...)
2. Original PeopleCode Event (if not overridden)
3. Post-Event Mapped Classes (Sequence 1, 2, 3...)
```

**Override Behavior:**
- Replace/Override mappings **completely replace** the original event code
- Original code never executes when overridden
- Only one Override mapping allowed per event

### AppRefiner Detection

When you open or save a Component/Page PeopleCode file:

1. **File Type Detection** - Caption parsed to identify event type
2. **Event Map Query** - Database queried for mappings on this event
3. **Annotation Creation** - Results displayed as annotations in editor
4. **Cross-Reference Query** - (If enabled) Shows where current class is event mapped

**Supported Event Types:**
- Component events (e.g., `PreBuild`, `PostBuild`, `SavePreChg`)
- Component Record events (e.g., `RowInit`, `SaveEdit`)
- Component Record Field events (e.g., `FieldChange`, `FieldEdit`)
- Page events (e.g., `Activate`, `PageActivate`)

## Enabling Event Mapping Detection

### Detect Event Mapping

**Setting:** "Detect Event Mapping" checkbox in AppRefiner main window

**Location:** Event Mapping group box in General Settings

When enabled, AppRefiner automatically shows event mappings for the current file.

**Steps to Enable:**
1. Open AppRefiner main window
2. Locate "Event Mapping" group box
3. Check "Detect Event Mapping" checkbox
4. Open or save a Component/Page PeopleCode file
5. Annotations appear automatically

### Show Event Mapped References

**Setting:** "Show Event Mapped References" checkbox in AppRefiner main window

**Location:** Event Mapping group box (right of "Detect Event Mapping")

When enabled, AppRefiner shows cross-references—where the current Application Class is event mapped.

**Use Case:**
- You're viewing an Application Class
- You want to know where this class is event mapped
- Enable "Show Event Mapped References"
- Annotations show all mappings of this class

### Display Modes

Two display modes control how event mapped classes are shown:

#### Class Path (Default)

**Radio Button:** "Class Path" in Event Mapping > Show group

Shows compact package:class references without full source:

```
(Sequence: 1) Event Mapped Pre Class: PT_SECURITY:SecurityManager:ValidateAccess
(Sequence: 2) Event Mapped Pre Class: CUSTOM:Logging:LogUserAction
```

**Benefits:**
- Compact display (single line per mapping)
- Quick overview of mapped classes
- Easy to read class names
- Minimal clutter

#### Class Text

**Radio Button:** "Class Text" in Event Mapping > Show group

Shows full source code of mapped classes:

```
/****************************************************************************************
/* Sequence: 1) Event Mapped Pre Class: PT_SECURITY:SecurityManager:ValidateAccess */
/****************************************************************************************/
   class SecurityManager
      method ValidateAccess(&user as string) Returns boolean;
      /* Full class source code here... */
   end-class;
```

**Benefits:**
- See complete implementation
- Understand mapping logic without opening class
- Review all mappings in context
- Useful for debugging

**Note:** Class Text mode is only available for Component/Component Record/Component Record Field events, not Page events.

## Annotation Display

### Pre-Event Mappings

**Location:** Top of file (above existing code)

**Format:**
```
Event Mapping Information:
Content Reference: EMPL_WEB.EMPL_WEB
   (Sequence: 1) Event Mapped Pre Class: PT_SECURITY:SecurityManager:ValidateAccess
   (Sequence: 2) Event Mapped Pre Class: CUSTOM:Logging:LogUserAction
```

**Meaning:**
- Classes execute **before** the original event code
- Numbered by sequence order
- All Pre mappings complete before original code runs

### Post-Event Mappings

**Location:** Bottom of file (after existing code)

**Format:**
```
Event Mapping Information:
Content Reference: EMPL_WEB.EMPL_WEB
   (Sequence: 1) Event Mapped Post Class: CUSTOM:Audit:LogComponentAccess
   (Sequence: 2) Event Mapped Post Class: PT_ANALYTICS:Tracker:RecordPageView
```

**Meaning:**
- Classes execute **after** the original event code
- Numbered by sequence order
- All Post mappings run after original code completes

### Override Mappings

**Location:** Top of file (above existing code)

**Format:**
```
Event Mapping Information:
Content Reference: EMPL_WEB.EMPL_WEB
   WARNING: This code is currently being overriden by an event mapped class.
Class: PT_OVERRIDE:CustomPreBuild:Execute
```

**Meaning:**
- Current code **will not execute**
- Override class runs instead
- Only one Override allowed
- Original code is bypassed completely

### Cross-Reference Annotations

**Location:** Top of file

**Format:**
```
Event Mapping Xrefs:
Content Reference: EMPLOYEE_DATA.GBL_EMPLOYEE
  EMPLOYEE.EMPLID.RowInit (Sequence: Pre, SeqNum: 1)
  EMPLOYEE.NAME.FieldChange (Sequence: Post, SeqNum: 2)
Content Reference: BENEFITS.BENEFITS_COMP
  BENEFITS.PLAN_TYPE.FieldChange (Sequence: Pre, SeqNum: 1)
```

**Meaning:**
- Shows where **this class** is event mapped
- Grouped by Content Reference (Component)
- Lists specific events and sequence numbers

## Database Requirements

### Required Tables

Event Mapping Detection queries:

**PSAEEVENTMAP** - Event mapping configurations
```sql
SELECT AEAPPLID, AEEVENTMAPPINGID, AEAPPCLASS, AEEVENTSEQNBR, AEEVENTPREPOST
FROM PSAEEVENTMAP
WHERE ... (component/page criteria)
```

**PSAPPCLASSDEFN** - Application Class metadata
```sql
SELECT OBJECTOWNERID, OBJECTVALUE1, OBJECTVALUE2, OBJECTVALUE3
FROM PSAPPCLASSDEFN
WHERE OBJECTVALUE1 = ? -- Package root
```

### Permissions Required

- **SELECT** access to `PSAEEVENTMAP`
- **SELECT** access to `PSAPPCLASSDEFN`
- **SELECT** access to `PSPROJECTITEM` (for cross-references)

### Without Database Connection

Without an active database connection:
- Event Mapping Detection is disabled
- No annotations appear
- No error messages shown
- Other features continue to work

## Use Cases and Workflows

### Understanding Event Flow

**Scenario:** Component PreBuild isn't behaving as expected

**Workflow:**
1. Open Component PreBuild PeopleCode
2. Check annotations at top of file
3. See Pre-Event mapped classes
4. Review sequence order
5. Understand execution flow:
   ```
   Pre-Event Class 1 → Pre-Event Class 2 → Your Code → Post-Event Class 1
   ```
6. Identify which mapped class might be causing issue

### Debugging Override Behavior

**Scenario:** Code changes have no effect

**Workflow:**
1. Open affected event PeopleCode
2. See WARNING annotation at top
3. Realize code is overridden by event mapping
4. Note override class path
5. Open override class to make changes
6. Or remove override mapping if unintended

### Reviewing Event Mapped Classes

**Scenario:** Need to verify what Pre classes execute

**Workflow:**
1. Enable "Detect Event Mapping"
2. Select "Class Text" display mode
3. Open Component event
4. Read full source of all Pre-Event classes
5. Understand complete Pre-Event logic
6. No need to open each class separately

### Finding Event Map Usage

**Scenario:** Determine where SecurityManager class is used

**Workflow:**
1. Open SecurityManager Application Class
2. Enable "Show Event Mapped References"
3. Check annotation at top
4. See all Components/Pages where it's mapped
5. Review sequence numbers
6. Understand deployment scope

### Validating Event Map Configuration

**Scenario:** Confirm event mapping was applied correctly

**Workflow:**
1. Apply event mapping in Application Designer
2. Open affected Component event in AppRefiner
3. Verify annotation shows new mapping
4. Check sequence number
5. Confirm Pre/Post/Override placement

## Advanced Features

### Page Event Mapping

Page events display slightly differently due to Page-Component relationship:

**Page Event Annotation:**
```
Event Mapping Information:
Content Reference: EMP_PERSONAL_DATA
   (Sequence: 1) When viewed on Component: EMPLOYEE.GBL_EMPLOYEE Event Mapped Pre Class: PT_PAGE:PageSecurity:ValidateAccess
```

**Key Difference:**
- Shows which **Component** uses this Page
- Same Page can appear on multiple Components
- Each Component may have different mappings

### Content Reference Grouping

When multiple mappings exist for different Content References:

```
Event Mapping Information:
Content Reference: EMPLOYEE_DATA.GBL_EMPLOYEE
   (Sequence: 1) Event Mapped Pre Class: PT_SECURITY:SecurityManager:ValidateUser

Content Reference: BENEFITS.BENEFITS_COMP
   (Sequence: 1) Event Mapped Pre Class: PT_AUDIT:AuditLogger:LogAccess
```

**Meaning:**
- Different Components have different mappings
- Grouped by Content Reference for clarity
- Each group shows its own sequence

### Sequence Number Significance

Sequence numbers determine execution order:

```
(Sequence: 1) → Runs first
(Sequence: 2) → Runs second
(Sequence: 3) → Runs third
...
```

**Important:**
- Lower numbers execute first
- Within Pre group, 1 executes before 2
- Within Post group, 1 executes before 2
- Gap in sequences is allowed (1, 3, 5, etc.)

### Automatic Re-Detection

Annotations are refreshed automatically:

**Triggers:**
- Opening a Component/Page PeopleCode file
- Saving a Component/Page PeopleCode file
- Toggling "Detect Event Mapping" checkbox
- Changing display mode (Class Path ↔ Class Text)

**No Manual Refresh Needed** - Changes to event mappings appear on next open/save.

## Configuration

### Settings Persistence

Event Mapping settings are saved per user:

**Saved Settings:**
- "Detect Event Mapping" checkbox state
- "Show Event Mapped References" checkbox state
- Display mode (Class Path vs. Class Text)

**Settings Location:** AppRefiner user configuration

Settings persist across:
- Application Designer restarts
- AppRefiner restarts
- Different PeopleSoft databases

### Performance Optimization

Event Mapping queries are fast:

- **Query Time:** < 50ms typical
- **Caching:** Results cached per editor
- **Lazy Loading:** Queries only when file opened/saved
- **No Background Polling:** No continuous database queries

**For Large Environments:**
- Minimal performance impact
- Queries are indexed (PSAEEVENTMAP)
- Results reused until file changes

## Troubleshooting

### No Annotations Appear

**Problem:** Event Mapping checkbox is enabled but no annotations shown

**Solutions:**
- Verify file is Component or Page PeopleCode (not Record field event)
- Check database connection is active
- Ensure event has actual mappings in PeopleSoft
- Try saving the file to trigger re-detection
- Check debug log for database errors

### Wrong Annotations Shown

**Problem:** Annotations show incorrect or outdated mappings

**Solutions:**
- Save the file to refresh annotations
- Verify database connection is to correct environment
- Check that event mapping changes were saved in PeopleSoft
- Close and reopen the file
- Verify you're viewing the correct Component/Page

### Override Warning Incorrect

**Problem:** Override warning shown but code still executes

**Solutions:**
- Verify override is actually configured in database
- Check Content Reference matches current Component
- Confirm override wasn't removed but database not refreshed
- Check for multiple Content References

### Cross-References Not Showing

**Problem:** "Show Event Mapped References" enabled but no xrefs appear

**Solutions:**
- Verify you're viewing an Application Class (not Component event)
- Ensure Application Class is actually event mapped somewhere
- Check database connection
- Confirm ClassPath is being detected (check debug log)
- Try toggling checkbox off and on

### Class Text Not Displaying

**Problem:** "Class Text" mode selected but only Class Path shown

**Solutions:**
- Verify you're viewing Component event (not Page event)
- Page events only support Class Path mode
- Check that Application Class source exists in database
- Ensure database permissions allow reading `PSAPPCLASSDEFN`

### Annotations Clutter Code

**Problem:** Too many annotations making code hard to read

**Solutions:**
- Switch to "Class Path" mode (more compact)
- Disable "Detect Event Mapping" when not needed
- Close and reopen file to clear annotations temporarily
- Use F5 to toggle annotations on/off (if supported)

## Tips and Best Practices

### 1. Enable for Event-Heavy Development

If you work extensively with event mappings:

```
Settings:
☑ Detect Event Mapping
☑ Show Event Mapped References
⦿ Class Path (for compact display)
```

**Why:** Constant visibility of mappings prevents surprises.

### 2. Use Class Text for Debugging

When troubleshooting event mapping issues:

```
Settings:
☑ Detect Event Mapping
⦿ Class Text
```

**Why:** See complete mapped class implementation inline.

### 3. Check for Overrides First

Before modifying Component event code:

1. Open event PeopleCode
2. Check for Override warning at top
3. If present, modify override class instead
4. Save time by not editing bypassed code

### 4. Document Cross-References

When documenting Application Classes:

1. Open Application Class
2. Enable "Show Event Mapped References"
3. Document where class is mapped
4. Include in code comments or external docs

### 5. Validate Event Map Changes

After configuring event mappings:

1. Open affected Component event
2. Verify annotations show new mapping
3. Check sequence number is correct
4. Confirm Pre/Post placement

### 6. Toggle Off for Cleaner View

When not actively working with event mappings:

```
Settings:
☐ Detect Event Mapping
```

**Why:** Reduces visual clutter during regular development.

## Integration with Other Features

### Works With Code Folding

Event mapping annotations don't interfere with code folding:

- Annotations appear above/below foldable regions
- Folding still works normally
- Annotations remain visible when code folded

### Works With Linting

Event mapping annotations are separate from lint annotations:

- Lint annotations (errors/warnings) use different styling
- Event mapping uses gray informational styling
- Both can appear simultaneously
- No conflicts or overlap

### Works With Type Checking

Type checking works across event mappings:

- Type errors consider Pre-Event class changes
- Variables set by Pre classes recognized
- Post classes can access variables from original code

### Works With Navigation

Navigate to event mapped classes:

1. View event mapping annotation
2. Note class path (e.g., `PT_SECURITY:SecurityManager:ValidateUser`)
3. Use Smart Open (Ctrl+O) to open Application Class
4. Navigate to specific method

## Comparison to Manual Checking

| Aspect | Manual Method | Event Mapping Detection |
|--------|---------------|-------------------------|
| **Find Mappings** | Open PeopleTools > Event Mapping | Automatic on file open |
| **View Sequence** | Check Event Mapping dialog | Shown in annotation |
| **View Source** | Open each class separately | Class Text mode shows inline |
| **Check Override** | Remember to check mappings | WARNING displayed prominently |
| **Cross-Refs** | Query PSAEEVENTMAP manually | Automatic with checkbox |
| **Time per Check** | 1-2 minutes | Instant (automatic) |

**Time Savings:** ~90% reduction in time spent identifying event mappings.

## Related Features

- **[Smart Open](smart-open.md)** - Quickly open event mapped Application Classes
- **[Navigation](navigation.md)** - Navigate to class definitions from annotations
- **[Code Styling](code-styling.md)** - Visual indicators work alongside event mapping
- **[Linting](linting.md)** - Lint both original and event mapped code

## Next Steps

- Enable [Detect Event Mapping](#detect-event-mapping) in AppRefiner settings
- Try [Class Text mode](#class-text) to view full mapped class source
- Enable [Show Event Mapped References](#show-event-mapped-references) for cross-reference tracking
- Review [event mapping configurations](#override-mappings) for Override warnings
