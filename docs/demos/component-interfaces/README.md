# Component interface typing demos

A CI named by `CompIntfc.X` in `GetCompIntfc()` or in an annotation is typed from its definition:
properties get their record field's type, collections and items know their path, and members
the CI doesn't have are errors. Every line that should show something has a trailing comment:
`OK` (no diagnostic), `WARNING`, or `ERROR`.

The expected results below were produced by running these exact files through AppRefiner's
compile checker (parser, inference, checker) with a fake resolver holding a USER_PROFILE definition
shaped like the delivered one. It was built from a subset of USER_PROFILE's PSBCITEM rows:
- `UserID`, `UserDescription`, `AccountLockDate` (date) and a read-only `WorklistEntriesCount`;
- `EmailAddresses` → `EmailAddress`;
- `Roles` → `RoleName`, read-only `Dynamic`, `RouteControls` → `RouteControlProfile`.

Its Methods program held a single `Function SetPassword(&pwd As string) Returns boolean`, and every
standard method was enabled. Real results depend on your database: what your USER_PROFILE
defines, which properties are read-only, which standard methods it enables, and which custom
methods its Methods program has, with what signatures. If a line reports something the table
doesn't list, check USER_PROFILE in App Designer first.

The only definition the files name is `CompIntfc.USER_PROFILE`, which is delivered. Record names
such as `PSOPRDEFN` appear only in hover text, where they come from your USER_PROFILE.

## Setup

1. Connect AppRefiner to the database. Without a connection none of this appears: CI members stay
   unchecked and `any`, and every file below is clean (the same compile-checker run with no
   definition available confirms that too).
2. Paste each `.pcode` file into any non-class PeopleCode event, such as a FieldFormula on a
   scratch record. The files don't depend on each other.
3. Don't save `02_Errors`: App Designer itself rejects `CompIntfc.ZZ_NO_SUCH_CI`.
4. For the disabled-method line (`02_Errors` line 16):
   - open USER_PROFILE in App Designer and clear one standard method, `Find`, then save the CI;
   - run **Refresh Component Interface Cache** from the command palette (saving the CI's
     definition doesn't refresh the cache; only saving its PeopleCode does). The command reports
     how many component interfaces it refreshed;
   - check line 16, then turn `Find` back on, save, and refresh again.

## Expected diagnostics

| File | Line | Expected |
|---|---|---|
| 01_Typing | | Nothing. Typed properties, items and collections two levels down, annotated helpers and a `[]` annotation on a local are all clean |
| 02_Errors | 8 | Error: `'UserDescr' is not a property of CompIntfc.USER_PROFILE (&ci is ComponentInterface, assigned on line 3)` |
| | 9 | Error: `'Rolez' is not a property of CompIntfc.USER_PROFILE (&ci is ComponentInterface, assigned on line 3)` |
| | 10 | Error: `'Bogus' is not a method of CompIntfc.USER_PROFILE (&ci is ComponentInterface, assigned on line 3)` |
| | 11 | Error: `'Bogus' is not a property of CompIntfc.USER_PROFILE.Roles (&role is DataItem, assigned on line 4)` |
| | 12 | Error: `'Bogus' is not a property of CompIntfc.USER_PROFILE.Roles.RouteControls (&control is DataItem, assigned on line 5)` |
| | 13 | Error: `'WorklistEntriesCount' is read-only on CompIntfc.USER_PROFILE` |
| | 14 | Error: `'Dynamic' is read-only on CompIntfc.USER_PROFILE.Roles` |
| | 15 | Error: `Cannot assign type 'string' to 'date'` |
| | 16 | Nothing while Find is enabled. With Find cleared (Setup step 4): Error: `CompIntfc.USER_PROFILE doesn't enable Find()` |
| | 17 | Error: `Component interface 'ZZ_NO_SUCH_CI' doesn't exist` |
| | 21 | Warning: `&wrongItem is annotated as CompIntfc.USER_PROFILE.Roles (line 20) but is assigned CompIntfc.USER_PROFILE.EmailAddresses here.` |
| | 24 | Warning: `Component interface 'ZZ_NO_SUCH_CI' doesn't exist` |
| | 25 | Warning: `'Rolez' is not a collection of CompIntfc.USER_PROFILE` |
| | 26 | Warning: `'Bogus' is not a collection of CompIntfc.USER_PROFILE.Roles` |
| | everything else | Nothing |
| 03_Methods | 16 | Error: `'ZZ_AR_Ping' is not a method of CompIntfc.USER_PROFILE (&ci is ComponentInterface, assigned on line 3)`. Nothing after the cache refresh check adds `ZZ_AR_Ping` |
| | 17 | Error: `'ZZ_AR_Helper' is not a method of CompIntfc.USER_PROFILE (&ci is ComponentInterface, assigned on line 3)`, before and after the cache refresh check: `ZZ_AR_Helper` is `NoExport` |
| | everything else | Nothing: `SetPassword` is a custom method, `GetPropertyByName` is on every CI, `Save` is enabled |

The annotation warnings on lines 24–26 need the definition too: with no database they, like
everything else, don't appear.

## Hovers and editor checks

- `01_Typing`:
  - `&ci` (line 13) shows `Inferred Type: ApiObject (CompIntfc.USER_PROFILE), assigned on line 13`.
  - `&roles` (line 25) shows `Inferred Type: ApiObject (CompIntfc.USER_PROFILE.Roles[]), assigned on line 24`,
    and `&role` (line 27) shows `Inferred Type: ApiObject (CompIntfc.USER_PROFILE.Roles), assigned on line 26`.
  - Inside `ProcessRole`:
    - `&role` (line 3) shows `Inferred Type: ApiObject (CompIntfc.USER_PROFILE.Roles), annotated on line 2`;
    - `&controls` (line 4) shows `Inferred Type: ApiObject (CompIntfc.USER_PROFILE.Roles.RouteControls[]), assigned on line 4`;
    - `&control` (line 5) shows `Inferred Type: ApiObject (CompIntfc.USER_PROFILE.Roles.RouteControls), assigned on line 5`.
  - `&email` (line 34) shows `Inferred Type: ApiObject (CompIntfc.USER_PROFILE.EmailAddresses), assigned on line 33`,
    typed through the `[]` annotation on `&annotated`.
  - Hover a property name for its details. `UserDescription` (line 21) shows
    `UserDescription: string` and `Record field: PSOPRDEFN.OPRDEFNDESC` (the record and field come
    from your database). A read-only property such as `WorklistEntriesCount` (`02_Errors` line 13)
    adds `Read-only`, and one with a comment in the CI's definition, such as `Dynamic`
    (`02_Errors` line 14) if yours has one, shows the comment.
- Autosuggest:
  - after `&ci.` lists USER_PROFILE's properties and keys, its collections (`Roles`,
    `EmailAddresses`, …), the standard methods it enables and its custom methods (`SetPassword()`).
    With `Find` cleared, `Find` is missing from the list.
  - after `&role.` (inside `ProcessRole`) lists `RoleName`, `Dynamic`, `RouteControls` and
    DataItem's own members such as `ItemNum`.
- `03_Methods`:
  - type `(` after `SetPassword` (line 11): the parameters tooltip says `CI method:` and shows the
    signature from your USER_PROFILE's Methods PeopleCode.
  - F12 on `SetPassword` opens USER_PROFILE's Methods PeopleCode at that function.

## Cache refresh check

AppRefiner caches each CI's definition. Saving the CI's PeopleCode refreshes that CI; anything
else needs the **Refresh Component Interface Cache** command (Setup step 4 uses it for a change to
the CI's definition). Use a test database: step 2 edits a delivered program.

1. Open `03_Methods`: lines 16 and 17 are errors.
2. Open USER_PROFILE's Methods PeopleCode and add these two functions after the existing ones:

   ```
   Function ZZ_AR_Ping() Returns boolean
      Return True;
   End-Function;

   Function ZZ_AR_Helper() NoExport
   End-Function;
   ```

   Save it, then switch back to `03_Methods`. Without editing it:
   - line 16 is clean, and autosuggest after `&ci.` lists `ZZ_AR_Ping()`;
   - line 17 is still an error, because `NoExport` functions aren't methods.
3. Remove the two functions and save again: line 16 is an error again.
4. Without a database connection the command says `Connect to a database to refresh component
   interfaces.`
