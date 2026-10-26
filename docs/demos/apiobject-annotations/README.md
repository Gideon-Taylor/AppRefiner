# ApiObject / annotation demo programs (phases 0–2 + follow-ups)

Every line that should show something has a trailing comment: `OK` (no diagnostic), `WARNING`, or `ERROR`.
The expected results below were produced by running these exact files through AppRefiner's
compile checker (parser + inference + checker) at the commit that added them.

Only delivered metadata is used: `CompIntfc.USER_PROFILE` and `Record.PSOPRDEFN`.

## Setup

1. **Non-class programs** (`01_`, `02_`, `03_`): paste each into any non-class PeopleCode event
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
| Svc | 41 | Warning: `Svc expects &s as Session (annotated); the argument is ComponentInterface.` |
| | everything else | Nothing: `Return %This`, `Local Svc &me = %This`, `create Svc(%This.Sess)` are all clean, with no "not imported" errors |
| Sub | 18 | Error: `'Bogus' is not a known method on 'Session' (&s is Session, annotated on line 6)` (inherited from Base) |
| Factory, Sink, Base, Config | | Nothing |
| Caller | 20 | Error (real): `Use(): Argument 1 should be Config, found ZZ_AR_DEMO:Caller` |
| | 28 | Warning: constructor `Svc expects &s as Session ...` |
| | 29 | Warning: `Use expects &s as Session ...` |
| | 34 | Error: `... (&sess is Session, assigned on line 32)` |
| | 38 | Warning: inherited `Use expects &s as Session ...` |
| | everything else | Nothing |

## Also worth checking by hand

- Autosuggest after `&s.` on an annotated variable (03 line 4) lists Session members.
- Hover on `&sess` (Caller line 33) shows `Inferred Type: ApiObject (Session)`.
- Hover on `GetSession` in `&svc.GetSession()` (Caller line 32) shows `Returns: ApiObject (Session)`;
  hover on `Use` in `&sub.Use(&ci)` shows the inherited `&s As ApiObject (Session)`.
- Hover on `&r` in `01_Reassignment` shows PSOPRDEFN's fields and `record (Record.PSOPRDEFN)`.
- Hover/go-to-definition on `Config` in Factory's `method Get() Returns Config;` now works.
- Edit `Base.Use`'s annotation to `/* as Query */`, save, then switch back to `Caller`: line 38's warning
  should update to Query, not show both the old and the new message.
