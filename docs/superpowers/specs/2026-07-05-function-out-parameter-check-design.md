# Design: Reject `OUT` Parameters on Functions

**Date:** 2026-07-05
**Status:** Approved (brainstorm) — pending implementation plan

## 1. Problem

App class `Method` declarations support `OUT` parameters (e.g.
`method Foo(&a as number, &b as string out)`), which the real PeopleCode compiler
honors. Plain top-level `Function`s cannot — function parameters are always passed
by reference already, so `OUT` is meaningless there and the real compiler rejects it.

AppRefiner's self-hosted parser does not currently make this distinction: `Method`
and `Function` parameter lists both go through the same routine,
`ParseMethodArgument()` (`PeopleCodeParser.SelfHosted/PeopleCodeParser.cs:1433`),
which accepts a trailing `OUT` unconditionally.

A second, related gap was found during design: `DECLARE FUNCTION ... PEOPLECODE`
declarations (e.g. `Declare Function AccessCheck PeopleCode QRYFUNCTIONS.QRYQUERYFUNCS
FieldFormula;`) take **no** parameter list at all in real PeopleCode — the parameters
are implicit, defined by the actual field PeopleCode. The parser nonetheless has an
"optional parameter list" branch here (`PeopleCodeParser.cs:2953-2970`) that
syntactically accepts `(...)`. Today those parsed parameters are silently discarded
before being attached to the returned `FunctionNode` (the copy at line 2980-2990 only
carries `RecordName`/`FieldName`/`RecordEvent`/`ReturnType`), so the invalid syntax is
both accepted *and* the resulting AST silently drops it — no error is ever surfaced.

Both are genuine syntax errors, not semantic/type concerns, so both are reported via
the parser's existing `ReportError(...)` mechanism rather than the
`ICompileCheck`/`DiagnosticCode`/quick-fix pipeline (see §3 for why that pipeline was
considered and rejected).

## 2. Fixes

### 2a. `OUT` on a Function parameter

In the top-level `Function` parameter-parsing loop
(`PeopleCodeParser.cs:3092-3115`), immediately after each
`var param = ParseMethodArgument();` call, add:

```csharp
if (param.IsOut)
{
    ReportError(
        "Function parameters cannot be marked OUT — function parameters are always passed by reference.",
        Previous);
}
```

`Previous` at this point is exactly the `OUT` token: `ParseMethodArgument()`'s last
action is `Match(TokenType.Out)`, so if `OUT` was present, `Previous` refers to it (if
not, `Previous` is the last type token and the `if` guard skips the whole branch
anyway). This gives a precise squiggle on the offending `out` keyword with no changes
to `ParameterNode` or to `ParseMethodArgument()`'s signature — the routine stays
shared and unaware of the calling context.

Parsing continues normally after `ReportError` (it only records a diagnostic); the
parameter still ends up in `functionNode.Parameters` with `IsOut = true`, matching the
existing pattern used by the chained-assignment check (`ParseAssignmentExpression()`,
`PeopleCodeParser.cs:4556-4560`), which also reports-and-continues rather than
aborting the parse.

This check applies only to the plain `Function ... End-Function` form. The
`DECLARE FUNCTION ... PEOPLECODE` parameter loop (`PeopleCodeParser.cs:2960`) is
handled by 2b below and does not need its own `OUT` check — the whole parameter list
there is already invalid, so flagging `OUT` specifically inside it would be redundant.
DLL/`Library` declarations are unaffected: they use a separate `REF`/`VALUE` parsing
routine (`ParseDllArgument()`, `PeopleCodeParser.cs:3222`) that never reuses
`ParseMethodArgument()`.

### 2b. Parameter list on `DECLARE FUNCTION ... PEOPLECODE`

In `PeopleCodeParser.cs:2953-2970`, capture the `(` token before the parameter loop
and report an error spanning the whole parenthesized list once it's consumed:

```csharp
if (Match(TokenType.LeftParen))
{
    var leftParenToken = Previous;
    if (!Check(TokenType.RightParen))
    {
        do
        {
            var param = ParseMethodArgument();
            if (param != null) declNode.AddParameter(param);
            else
            {
                while (!IsAtEnd && !(Check(TokenType.Comma) || Check(TokenType.RightParen)))
                    _position++;
            }
        } while (Match(TokenType.Comma));
    }
    Consume(TokenType.RightParen, "Expected ')' after parameters");
    ReportError(
        "'DECLARE FUNCTION ... PEOPLECODE' declarations cannot have a parameter list; parameters are not allowed here.",
        leftParenToken, Previous);
}
```

The existing parameter-parsing loop is left otherwise untouched (still needed for
error recovery / robust token consumption); only the trailing `ReportError` call is
new.

## 3. Why raw `ReportError` instead of the `ICompileCheck` pipeline (no quick fix)

Both issues were initially scoped as a new `DiagnosticCode` + `ICompileCheck` (in
`PeopleCodeParser.SelfHosted/Compilation/Checks/`) with a paired quick-fix refactor,
mirroring `MissingMethodImplementationCheck`. That was dropped in favor of raw parser
`ReportError` calls once we confirmed a quick fix isn't wanted here, because:

- `ParseError` (`PeopleCodeParser.cs:5845`) carries only `Message`, `Location`,
  `Severity`, `Context` — no `FixContext` field.
- `CompileChecker.Check()`'s merge step
  (`PeopleCodeParser.SelfHosted/Compilation/CompileChecker.cs:50-52`) wraps **every**
  parser error as the same generic `DiagnosticCode.SyntaxError`, with nothing carried
  through to distinguish one raw parse error from another.
- `CompileDiagnosticQuickFixMap` (`AppRefiner/Services/CompileDiagnosticQuickFixMap.cs`)
  routes fixes by `DiagnosticCode` (plus a typed `FixContext` for disambiguation where
  several fixes share a code); there is no case for `SyntaxError`, and matching against
  the raw message string would be fragile.

Making either of these fixable would require adding a `FixContext` field to
`ParseError`, threading it through the merge step, and adding a `SyntaxError`-routed
case in the quick-fix map — exactly the plumbing the `ICompileCheck` pipeline exists to
avoid. Since both are being reported as errors with no quick fix, the simpler,
already-precedented raw-`ReportError` path (used today for chained assignment) is the
right shape: two small, local edits inside the parser, no new files, no new enum
members.

**Explicit non-goal:** no quick fix for either diagnostic. Users remove the offending
`OUT` keyword or parameter list by hand.

## 4. Testing

Both are pure parser-level changes, testable directly against
`PeopleCodeParser.Errors` without needing a `CompileChecker`/resolver:

- `Function Foo(&a As number, &b As string Out) ... End-Function` → exactly one
  parse error, located on the `Out` token.
- `Method Foo(&a As number, &b As string Out); ... end-method;` inside an app class →
  no error (existing, unchanged behavior).
- `Declare Function Foo PeopleCode REC.FLD FieldFormula;` → no error (existing,
  unchanged behavior).
- `Declare Function Foo PeopleCode REC.FLD FieldFormula(&a As number);` → exactly one
  parse error spanning `(&a As number)`.
- `Declare Function Foo Library "mylib.dll" (&a As number Ref);` → no error (DLL `Ref`
  parameters are unaffected; they never go through `ParseMethodArgument()`).
