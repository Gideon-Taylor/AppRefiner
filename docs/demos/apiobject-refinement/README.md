# ApiObject narrowing and return refinement demos (phases 3–4)

Nothing in these files is annotated. Every type comes from how a variable is used (narrowing) or
from the values a method or function returns (return refinement). Every line that should show
something has a trailing comment: `OK` (no diagnostic), `WARNING`, or `ERROR`.

The expected results below were produced by running these exact files through AppRefiner's
compile checker, with a resolver that refines each class it loads the way the database resolver
does. Only delivered metadata is used: `CompIntfc.USER_PROFILE` and `Record.PSOPRDEFN`.

`probes/` holds two App Designer experiments for open questions; they aren't demos (see the end).

## Setup

1. **`05_ProgramFlow`** and **`06_CiCollections`**: paste each into any non-class PeopleCode event.
2. **App package `ZZ_AR_REF`**: create it with these root-level classes, saved in this order:
   `Provider`, `Wrapper`, `Derived`, `PingA`, `PingB`, `Consumer`. Paste each `.pcode` file into the
   class of the same name. PingA and PingB import each other, so PingA's first save may complain
   that PingB doesn't exist yet. Save PingB, then save PingA again.

## Expected diagnostics

| File | Line | Expected |
|---|---|---|
| 05_ProgramFlow | 10 | Warning (N2): `&x was inferred as Session from GetCompIntfc() on line 8, but Session has no member 'RunToRowset'.` |
| | 34 | Warning (R1): `Mixed returns more than one API type (Session on line 32, Query on line 34).` |
| | 50 | Error: `'Bogus' is not a known method on 'Session' (&s is Session, assigned on line 48)` |
| | 69 | Warning (N2): `&gConn was inferred as Session from GetCompIntfc() on line 68, but Session has no member 'Bogus'.` |
| | everything else | Nothing |
| 06_CiCollections | 13 | Error: `'Bogus' is not a known method on 'DataItem' (&role is DataItem, assigned on line 10)` |
| | everything else | Nothing: DataItem fields (`RoleName`), `InsertItem` into an ApiObject, `DeleteItem` into a boolean, and a field on an untyped ApiObject (line 20) are all clean |
| Provider | 38 | Warning (R1): `Mixed returns more than one API type (Session on line 36, Query on line 38).` |
| | 43 | Error: `'Disconnect' is not a known method on 'Query' (&qry is Query, assigned on line 18)` |
| | everything else | Nothing. Line 22 calls `GetSess`, which is implemented below `GetCI`, and still gets Session |
| Wrapper, Derived, PingA, PingB | | Nothing |
| Consumer | 19 | Error: `'Bogus' is not a known method on 'Session' (&s is Session, assigned on line 17)` |
| | 26 | Error: `... on 'Session' (&g is Session, assigned on line 24)`: getter-only property |
| | 27 | Error: `... on 'Session' (&c is Session, assigned on line 25)`: property typed from the constructor's assignment |
| | 33 | Error: `... on 'Session' (&s is Session, assigned on line 32)`: through Wrapper |
| | 35 | Error: `... on 'ComponentInterface' (&ci is ComponentInterface, assigned on line 34)` |
| | 41 | Error: `... on 'Query' (&q is Query, assigned on line 40)`: Derived's own body, not Provider's |
| | 43 | Warning (N3): `'Bogus' is not a member of any API object type.` (Derived.GetCI stays plain ApiObject) |
| | 54 | Warning (N2): `&conn was inferred as Session from GetCompIntfc() on line 53, but Session has no member 'Bogus'.` |
| | everything else | Nothing |

## Hovers worth checking

- `05_ProgramFlow`:
  - `&x` on line 8 shows `Inferred Type: ApiObject (Session), inferred from GetCompIntfc() on line 8`.
  - `&s` on line 20 (inside `MakeCI`) shows `ApiObject (Session), assigned on line 19`: a same-program
    function refined from its `Return`.
  - `&maybe` (line 56) is Session: `Return Null` doesn't count against `Return %Session`.
  - `&mixed` (line 59) shows `ApiObject (Session), inferred from Disconnect() on line 59`: `Mixed`
    disagrees with itself, so it stays plain ApiObject and `&mixed` narrows from its own use.
  - `&rec` (line 62) shows `record (Record.PSOPRDEFN)`.
  - `&cd` (line 65) is plain `ApiObject`: a function that returns itself can't be refined.
  - `&gConn` (line 68) and `&cQry` (line 70) narrow to Session and Query.
- `06_CiCollections`:
  - `&roles` (line 10) shows `ApiObject (DataCollection), inferred from CurrentItemNumber on line 9`.
  - `&role` (line 11) shows `ApiObject (DataItem), assigned on line 10`.
  - `&other` (line 20) shows plain `ApiObject`: a property nothing declares doesn't narrow.
- `Consumer`:
  - `&s`, `&ci`, `&g`, `&c` in `Direct` and `ThroughWrapper` show Session or ComponentInterface,
    each `assigned on line N`.
  - `&rec` (line 22) shows `record (Record.PSOPRDEFN)`.
  - `&r` (line 49) is plain `ApiObject`: PingA and PingB call each other, so neither is refined,
    and the hover appears without delay.
  - Hover the method name in `&p.GetSess()` (line 17): `Returns: ApiObject (Session)`.

## Saving updates callers

1. Open `Consumer` and hover `&s` on line 18: Session.
2. In `Provider`, change `GetSess` to `Return %Session.GetQuery();` and save.
3. Switch back to `Consumer`. Without editing it, you should see:
   - line 18 is a new error, `'Disconnect' is not a known method on 'Query' (&s is Query, assigned on line 17)`;
   - lines 19 and 33 now name Query (33 goes through `Wrapper`, which you didn't save);
   - `GetCI` now calls `GetCompIntfc` on a Query, so it stays plain ApiObject. Line 21's `&ci`
     shows `inferred from Get() on line 21`, and line 35 becomes the N3 warning
     `'Bogus' is not a member of any API object type.`;
   - stale results here, such as line 19 still naming Session, mean the cache wasn't cleared.
4. Put `GetSess` back to `Return %Session;` and save.

## Probes (App Designer experiments, not demos)

- `probes/ReturnValueProbe.pcode`: which ignored return values the PeopleCode compiler rejects.
  Done (2026-10): only E (a typed builtin method not marked optional) is rejected; API object
  methods (A–D) never are. AppRefiner now matches.
- `probes/DataCollectionProbe.pcode`: what a component interface collection's `Item()` returns at
  run time. Done (2026-10): an item doesn't convert to Row, `InsertItem` returns the new item, and
  `DeleteItem` returns a boolean. `typeinfo.dat` now says so (see `06_CiCollections`).
