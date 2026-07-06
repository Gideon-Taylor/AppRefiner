# Fix: recognize unsaved members of the current class in InvalidMemberAccessCheck

**Status:** diagnosed, not yet implemented (deferred). Self-contained so it can be
picked up in a fresh context.

## Symptom

After adding a new method to the class currently open in the editor (e.g. via the
Extract Function/Method refactor, or just hand-typing one), a call to it through
`%This.NewMethod(...)` is flagged as **"'NewMethod' is not a known method on 'Foo'"**.
Saving once does **not** clear it; making another edit and saving again does.

## Root cause

Two consumers resolve members on the current ("self") class, and they disagree:

- **`TypeInferenceVisitor` is correct.** It builds the current class's metadata from
  the **live in-editor `ProgramNode`** (`_programMetadata`, produced by
  `TypeMetadataBuilder.ExtractMetadata(program, qualifiedName)` and passed in by
  `AppRefiner/Services/TypeInferenceRunner.cs:21,35-37`). When it looks up a member on
  the current class it special-cases self inside the inheritance walk:
  `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs:493-496` (methods) and
  `:588-591` (properties), i.e. *"if this level is my own class → use `_programMetadata`;
  else → `_typeResolver.GetTypeMetadata(...)`."* So inference already knows the unsaved
  method.

- **`InvalidMemberAccessCheck` is wrong.** The check that actually emits the diagnostic
  re-derives membership purely from the DB-backed resolver, with **no self special-case**:
  `PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidMemberAccessCheck.cs`
  - Error emitted at `:65-69` (`DiagnosticCode.InvalidMemberAccess`).
  - `AppClassHasMember` (`:104-141`) walks the inheritance chain; the load-bearing line
    is `:117` `var metadata = resolver.GetTypeMetadata(current);` — always the resolver.
  - The resolver (`AppRefiner/Database/DatabaseTypeMetadataResolver.cs:48` →
    `TryResolveAsAppClass:166` → `_dataManager.GetAppClassSourceByPath:177` →
    `ParseAndExtractMetadata:237,256`) reads the **last-saved DB source**, keyed by
    qualified name in `ITypeMetadataResolver._cache`.

So the check validates the new method against **saved** source that doesn't contain it.

**Why "save once = still broken, edit+save = fixed":** on save, `MainForm.cs:4369`
re-runs the stylers *before* `MainForm.cs:4387` invalidates the cache
(`InvalidateTypeCacheForEditor`, `:4525` → `TypeResolver.Cache.Remove` / `dbResolver.Clear()`).
So the first save re-styles against stale metadata; only a later edit+save re-styles
against a cache that will re-read the now-persisted source. Nothing invalidates on text
change — only on save. **Cache-busting after extraction would NOT fix this** — the DB
still lacks the method until it is saved. The real problem is that the check ignores the
live buffer for self.

## The fix (Injection point 1 — mirror inference, localized, no cache/save changes)

Give `InvalidMemberAccessCheck` the same self special-case inference already has: for the
**self level only** of the inheritance walk, use metadata built from the live
`ProgramNode`; keep using the resolver for base classes / interfaces.

1. Build the current class's live `TypeMetadata` **once per check run**:
   `TypeMetadataBuilder.ExtractMetadata(ctx.Program, <full qualified name of current class>)`
   (`PeopleCodeParser.SelfHosted/Visitors/TypeMetadataBuilder.cs:45`). `ctx.Program` is the
   current parsed program — `PeopleCodeParser.SelfHosted/Compilation/CompileCheckContext.cs:14`.
   Do NOT rebuild it per member-access node (it walks the whole program). Build it once and
   thread it (e.g. a new `CompileCheckContext.SelfMetadata` field set by `CompileChecker.Check`,
   or lazily cached on the check instance). Ideally reuse the very `_programMetadata` that
   inference already built for this run instead of rebuilding.

2. In `AppClassHasMember`, replace the unconditional `resolver.GetTypeMetadata(current)`
   at `:117` with:
   ```csharp
   var metadata = current.Equals(liveSelfQualifiedName, StringComparison.OrdinalIgnoreCase)
       ? liveSelfMetadata
       : resolver.GetTypeMetadata(current);
   ```
   Pass `ctx` (or the live metadata + its qualified name) down into `MemberExists`
   (`:81`) / `AppClassHasMember` (`:104`), which currently take only `resolver`.

### Why inherited members still resolve (the concern to verify)

`AppClassHasMember` is a **chain walk** (`:113-138`): after checking a level it advances
via `current = metadata.BaseClassName ?? metadata.InterfaceName` (`:135-137`). The fix
swaps only the *source* of each level's metadata, not the walk. `liveSelfMetadata` (from
`TypeMetadataBuilder`) carries `BaseClassName` / `IsBaseClassBuiltin` / `BuiltinBaseType`
just like DB metadata. So:

- `%This.NewMethod()` → iter 1: `current` = self → `liveSelfMetadata` → found (unsaved). ✅
- `%This.MethodFromSuperClass()` → iter 1: self via `liveSelfMetadata`, not found →
  advance to `liveSelfMetadata.BaseClassName` → iter 2: `current` = superclass ≠ self →
  `resolver.GetTypeMetadata(superclass)` (DB) → found. ✅

This is exactly the pattern `TypeInferenceVisitor.LookupMethodInInheritanceChain`
(`:482-496`) already uses, which is why inference resolves both correctly.

### Critical plumbing detail — qualified-name matching

The self match must compare against the **same qualified name** that `%This`'s
`AppClassTypeInfo.QualifiedName` carries (the full class path, set from
`_programMetadata.QualifiedName` at `TypeInferenceVisitor.cs:920-926`). Do **not** match on
`CompileCheckContext.ExpectedClassName` — that is only the final path segment
(`CompilerErrorsStyler.cs:39` sets it via `ClassPath.Split(':').LastOrDefault()`). Build
`liveSelfMetadata` with the full class path so `liveSelfMetadata.QualifiedName == current`
at iteration 1. Thread the full class path into the context if it isn't already available.

## Safety / non-masking

Cannot hide real errors: a member that doesn't exist in the live buffer either isn't in
`liveSelfMetadata` (→ still flagged after the chain fails) — the fix only *adds* the
unsaved members that genuinely exist in the editor.

## Applies beyond extraction

This is a general type-checking gap, not specific to the Extract refactor — any unsaved
new member (hand-typed too) hits it. Fixing it here fixes it everywhere.

## Verification

- Add a method to an app class in the editor; call it via `%This.NewMethod()` → no
  "unknown method" without saving.
- `%This.<inherited method from base class>()` still resolves (chain walk).
- A genuinely nonexistent `%This.Nope()` is still flagged.
- Property / instance-variable access on `%This` gets the same treatment (the check
  handles both in `AppClassHasMember`).

## Key files

- `PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidMemberAccessCheck.cs` (the fix)
- `PeopleCodeParser.SelfHosted/Compilation/CompileCheckContext.cs` (carries `Program`)
- `PeopleCodeParser.SelfHosted/Compilation/CompileChecker.cs:41` (`Check` — where to seed self metadata)
- `PeopleCodeParser.SelfHosted/Visitors/TypeMetadataBuilder.cs:45` (`ExtractMetadata`)
- `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs:482-496,588-591,920-926` (the pattern to mirror)
- `AppRefiner/Stylers/CompilerErrorsStyler.cs:38-41` (styler entry, resolver + ExpectedClassName)
- `AppRefiner/MainForm.cs:4369,4387,4525` (save-time re-style vs cache invalidation ordering)
