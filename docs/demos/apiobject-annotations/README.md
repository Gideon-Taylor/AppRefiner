# ApiObject / annotation demo programs (phases 0–4)

Every line that should show something has a trailing comment: `OK` (no diagnostic), `WARNING`, or `ERROR`.
The expected results below were produced by running these exact files through AppRefiner's
compile checker (parser + inference + checker) at the commit that added them. The narrowing
(`04_`) and cross-program refinement (`Svc.Make2`/`MakeRec`, `Caller.Refined`) rows reflect the
commit that added those files and methods ("Refine cross-program return types in the database
resolver"); the cross-program rows also need the database resolver, which refines each class it
loads.

Only delivered metadata is used: `CompIntfc.USER_PROFILE` and `Record.PSOPRDEFN`.

## Setup

1. **Non-class programs** (`01_` to `04_`): paste each into any non-class PeopleCode event
   (a FieldFormula or page Activate on a scratch record/page). They don't depend on each other.
2. **App package `ZZ_AR_DEMO`**: create it with these root-level classes, saved in this order
   (each depends on the ones before it): `Config`, `Base`, `Svc`, `Sub`, `Factory`, `Sink`, `Caller`.
   Paste each `.pcode` file into the class of the same name.

## Expected diagnostics

| File | Line | Expected |
|---|---|---|
| 01_Reassignment | 7 | Warning: `&x is assigned more than one API type (Session on line 4, ComponentInterface on line 7)` |
| | 9 | Error: `'Bogus' is not a known method on 'ComponentInterface' (&x is ComponentInterface, assigned on line 7)` |
| | 18 | Error: `Cannot assign type 'string' to 'ApiObject'` |
| | 12, 15, 20 | Nothing (CI property, `any`, record hover) |
| 02_FunctionAnnotations | 11 | Warning: `'as Session' needs an ApiObject declaration; &r is Rowset.` |
| | 14 | Warning: `Unknown type 'Sesion' in annotation.` |
| | 23 | Warning: `Take expects &s as Session (annotated); the argument is ComponentInterface.` |
| 03_LocalAnnotations | 5 | Error: `... (&s is Session, annotated on line 3)` |
| | 9 | Warning: `The value is ComponentInterface, but it is annotated as Query. Using Query.` |
| | 12 | Warning: unknown type `Sesion` |
| | 13 | **Two** warnings, one for `&a`, one for `&b` |
| | 16 | Warning: `&d is annotated as Session (line 15) but is assigned ComponentInterface here.` |
| | 17 | **One** warning (declaration conflict only) |
| | 18 | **Two** warnings: unknown `Bogus` + declaration conflict |
| | 21 | Warning: `&anyS is annotated as Session (line 20) but is assigned string here.` |
| 04_Narrowing | 9 | Warning (N2): `&q was inferred as Query from RunToRowset() on line 8, but Query has no member 'Disconnect'.` |
| | 14 | Warning (N1): `'Cancel' is not a member of PortalRegistry or Query. Earlier uses of &p (Open() on line 13) suggested one of those types.` |
| | 18 | Warning (N3): `'Zzz' is not a member of any API object type.` |
| | 23 | Warning (N4): `Uses of &s suggest Session (GetCompIntfc() on line 22), but it is assigned Query on line 23.` |
| | 6, 7, 8, 13, 22, 24, 28, 32, 33 | Nothing (uses before the deciding one, the deciding use, a use that leaves several candidates, latest assignment wins, undecided, CI property) |
| Svc | 43 | Warning: `Svc expects &s as Session (annotated); the argument is ComponentInterface.` |
| | everything else | Nothing: `Return %This`, `Local Svc &me = %This`, `create Svc(%This.Sess)` are all clean, with no "not imported" errors |
| Sub | 18 | Error: `'Bogus' is not a known method on 'Session' (&s is Session, annotated on line 6)` (inherited from Base) |
| Factory, Sink, Base, Config | | Nothing |
| Caller | 21 | Error (real): `Use(): Argument 1 should be Config, found ZZ_AR_DEMO:Caller` |
| | 29 | Warning: constructor `Svc expects &s as Session ...` |
| | 30 | Warning: `Use expects &s as Session ...` |
| | 35 | Error: `... (&sess is Session, assigned on line 33)` |
| | 39 | Warning: inherited `Use expects &s as Session ...` |
| | 47 | Error: `'Bogus' is not a known method on 'Session' (&s2 is Session, assigned on line 45)` (Make2's return refined from Svc's body; without refinement this would be narrowing's N2 warning naming `Disconnect() on line 46`) |
| | everything else | Nothing |

## Also worth checking by hand

- Autosuggest after `&s.` on an annotated variable (03 line 4) lists Session members.
- Hover on `&sess` (Caller line 34) shows `Inferred Type: ApiObject (Session), assigned on line 33`.
- Hover on `GetSession` in `&svc.GetSession()` (Caller line 33) shows `Returns: ApiObject (Session)`;
  hover on `Use` in `&sub.Use(&ci)` shows the inherited `&s As ApiObject (Session)`.
- Hover on `&r` in `01_Reassignment` shows PSOPRDEFN's fields and `record (Record.PSOPRDEFN)`.
- Hover/go-to-definition on `Config` in Factory's `method Get() Returns Config;` now works.
- Narrowing hovers in `04_Narrowing`:
  - `&q` on line 6 (before the deciding use) shows `Inferred Type: ApiObject (Query), inferred from RunToRowset() on line 8`.
  - `&p` (lines 13–14) and `&u` (line 28) show `Inferred Type: ApiObject (PortalRegistry or Query)`: undecided.
  - `&s` on line 24 shows `ApiObject (Query), assigned on line 23` (the assignment replaced the narrowed Session).
  - `&c` on line 32 shows `ApiObject (ComponentInterface), inferred from Get() on line 33`: the `UserID`
    property alone didn't settle it, since it could also be a DataItem field.
  - Autosuggest after `&q.` lists Query members.
- Cross-program refinement in `Caller.Refined`:
  - Hover on `&s2` (line 46) shows `Inferred Type: ApiObject (Session), assigned on line 45`, although
    `Svc.Make2` is declared plain `Returns ApiObject`.
  - Hover on `&rec` (line 49) shows `record (Record.PSOPRDEFN)` from `Svc.MakeRec`'s `Return`.
- Edit `Base.Use`'s annotation to `/* as Query */`, save, then switch back to `Caller`: line 39's warning
  should update to Query, not show both the old and the new message.
