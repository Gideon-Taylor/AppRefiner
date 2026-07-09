# Reject OUT Parameters on Functions — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the self-hosted PeopleCode parser report a parse error when a plain
`Function` parameter is marked `OUT`, and when a `DECLARE FUNCTION ... PEOPLECODE`
declaration carries a parameter list — both are accepted today but rejected by the
real PeopleCode compiler.

**Architecture:** Two small, independent edits inside
`PeopleCodeParser.SelfHosted/PeopleCodeParser.cs`, each adding a single
`ReportError(...)` call at an existing parse site. No new AST fields, no new
`DiagnosticCode`, no new `ICompileCheck`, no quick fix. Full rationale and code in
the approved spec: `docs/superpowers/specs/2026-07-05-function-out-parameter-check-design.md`.

**Tech Stack:** C# / .NET 8, `PeopleCodeParser.SelfHosted` library, xUnit tests in
`PeopleCodeParser.SelfHosted.Tests`.

## Global Constraints

- No new `ParameterNode` fields, no new `DiagnosticCode` member, no new
  `ICompileCheck`, no quick-fix refactor — per spec §3, this is intentionally scoped
  to raw parser `ReportError` calls only.
- `Method` parameter parsing (app class methods) and DLL/`Library` `REF`/`VALUE`
  parameter parsing must be completely unaffected — verify with non-regression tests.
- Parsing must continue after each `ReportError` call (do not early-return / abort
  the parse) — matches the existing chained-assignment precedent.

---

### Task 1: Reject `OUT` on plain Function parameters

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/PeopleCodeParser.cs:3092-3115` (top-level
  `Function` parameter-parsing loop, inside `ParseFunction()`)
- Test: `PeopleCodeParser.SelfHosted.Tests/FunctionOutParameterTests.cs` (new file)

**Interfaces:**
- Consumes: existing `ParameterNode.IsOut` (bool, already set by
  `ParseMethodArgument()` at `PeopleCodeParser.cs:1475-1478`), existing
  `ReportError(string message, Token highlightToken)` overload
  (`PeopleCodeParser.cs:298`), existing test helper
  `ParseTestHelper.Parse(string source) : (ProgramNode Program, IReadOnlyList<ParseError> Errors)`
  (`PeopleCodeParser.SelfHosted.Tests/ParseTestHelper.cs:7-17`).
- Produces: nothing consumed by later tasks — Task 2 is independent.

- [ ] **Step 1: Write the failing tests**

Create `PeopleCodeParser.SelfHosted.Tests/FunctionOutParameterTests.cs`:

```csharp
using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Function parameters cannot be marked OUT — they are always passed by reference
/// already. App class Method parameters and DLL REF parameters are unaffected.
/// </summary>
public class FunctionOutParameterTests
{
    [Fact]
    public void FunctionWithOutParameter_ReportsParseError()
    {
        var (program, errors) = Parse("Function Foo(&a As number, &b As string Out) End-Function;");

        Assert.Contains(errors, e => e.Message.Contains("OUT"));
        var function = program.Functions.Single();
        Assert.True(function.Parameters[1].IsOut);
    }

    [Fact]
    public void FunctionWithoutOutParameter_IsNotAnError()
    {
        var (_, errors) = Parse("Function Foo(&a As number, &b As string) End-Function;");

        Assert.Empty(errors);
    }

    [Fact]
    public void MethodWithOutParameter_IsStillNotAnError()
    {
        var source = """
            class TestClass
               method Bar(&a As number, &b As string Out);
            end-class;
            """;
        var (_, errors) = Parse(source);

        Assert.Empty(errors);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests/PeopleCodeParser.SelfHosted.Tests.csproj --filter "FullyQualifiedName~FunctionOutParameterTests"`

Expected: `FunctionWithOutParameter_ReportsParseError` FAILS (no error is reported
today); `FunctionWithoutOutParameter_IsNotAnError` and
`MethodWithOutParameter_IsStillNotAnError` already PASS (this confirms the baseline
behavior for both non-regression cases before you touch any code).

- [ ] **Step 3: Add the parse-time check**

In `PeopleCodeParser.SelfHosted/PeopleCodeParser.cs`, inside `ParseFunction()`'s
parameter loop (currently lines 3092-3115), change:

```csharp
                    do
                    {
                        var param = ParseMethodArgument();
                        if (param != null)
                        {
                            functionNode.AddParameter(param);
                        }
                        else
                        {
                            // Parameter parse failed: attempt to recover by skipping to ',' or ')'
                            while (!IsAtEnd && !(Check(TokenType.Comma) || Check(TokenType.RightParen)))
                                _position++;
                        }
                    }
                    while (Match(TokenType.Comma) && !Check(TokenType.RightParen)); // Allow trailing comma
```

to:

```csharp
                    do
                    {
                        var param = ParseMethodArgument();
                        if (param != null)
                        {
                            functionNode.AddParameter(param);
                            if (param.IsOut)
                            {
                                ReportError(
                                    "Function parameters cannot be marked OUT — function parameters are always passed by reference.",
                                    Previous);
                            }
                        }
                        else
                        {
                            // Parameter parse failed: attempt to recover by skipping to ',' or ')'
                            while (!IsAtEnd && !(Check(TokenType.Comma) || Check(TokenType.RightParen)))
                                _position++;
                        }
                    }
                    while (Match(TokenType.Comma) && !Check(TokenType.RightParen)); // Allow trailing comma
```

`Previous` is exactly the `OUT` token here: `ParseMethodArgument()`'s last action
before returning is `Match(TokenType.Out)`, so when `param.IsOut` is true, `Previous`
still refers to that token (nothing else has advanced the parser position between the
call and this check).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests/PeopleCodeParser.SelfHosted.Tests.csproj --filter "FullyQualifiedName~FunctionOutParameterTests"`

Expected: all 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeParser.SelfHosted/PeopleCodeParser.cs PeopleCodeParser.SelfHosted.Tests/FunctionOutParameterTests.cs
git commit -m "fix(parser): reject OUT modifier on Function parameters"
```

---

### Task 2: Reject parameter lists on `DECLARE FUNCTION ... PEOPLECODE`

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/PeopleCodeParser.cs:2953-2970` (parameter-list
  branch inside the `DECLARE FUNCTION ... PEOPLECODE` variant of `ParseFunction()`)
- Test: `PeopleCodeParser.SelfHosted.Tests/FunctionOutParameterTests.cs` (append to
  the file created in Task 1)

**Interfaces:**
- Consumes: existing `ReportError(string message, Token startToken, Token endToken)`
  overload (`PeopleCodeParser.cs:332`), same `ParseTestHelper.Parse(...)` as Task 1.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write the failing tests**

Append to `PeopleCodeParser.SelfHosted.Tests/FunctionOutParameterTests.cs` (inside
the existing `FunctionOutParameterTests` class):

```csharp
    [Fact]
    public void DeclareFunctionPeopleCode_WithParameterList_ReportsParseError()
    {
        var (_, errors) = Parse("Declare Function Foo PeopleCode REC.FLD FieldFormula(&a As number);");

        Assert.Contains(errors, e => e.Message.Contains("parameter list"));
    }

    [Fact]
    public void DeclareFunctionPeopleCode_WithoutParameterList_IsNotAnError()
    {
        var (_, errors) = Parse("Declare Function Foo PeopleCode REC.FLD FieldFormula;");

        Assert.Empty(errors);
    }

    [Fact]
    public void DeclareFunctionLibrary_WithRefParameter_IsNotAnError()
    {
        var (_, errors) = Parse("""Declare Function Foo Library "mylib.dll" (arg1 Ref As number);""");

        Assert.Empty(errors);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests/PeopleCodeParser.SelfHosted.Tests.csproj --filter "FullyQualifiedName~FunctionOutParameterTests"`

Expected: `DeclareFunctionPeopleCode_WithParameterList_ReportsParseError` FAILS (no
error reported today); the other two new tests already PASS.

- [ ] **Step 3: Add the parse-time check**

In `PeopleCodeParser.SelfHosted/PeopleCodeParser.cs`, inside the `DECLARE FUNCTION
... PEOPLECODE` branch (currently lines 2953-2970), change:

```csharp
                    // Optional parameter list
                    if (Match(TokenType.LeftParen))
                    {
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
                    }
```

to:

```csharp
                    // Optional parameter list — real PeopleCode does not allow one
                    // here at all; parameters are implicit to the referenced field.
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

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests/PeopleCodeParser.SelfHosted.Tests.csproj --filter "FullyQualifiedName~FunctionOutParameterTests"`

Expected: all 6 tests in the file PASS.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeParser.SelfHosted/PeopleCodeParser.cs PeopleCodeParser.SelfHosted.Tests/FunctionOutParameterTests.cs
git commit -m "fix(parser): reject parameter lists on DECLARE FUNCTION ... PEOPLECODE"
```

---

### Task 3: Full regression run

**Files:** none (verification only)

**Interfaces:** none

- [ ] **Step 1: Run the full parser test suite**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests/PeopleCodeParser.SelfHosted.Tests.csproj`

Expected: all 191 tests PASS (185 existing + 6 new from this plan), 0 failures. If
anything outside `FunctionOutParameterTests` regresses, it means one of the two
`ReportError` calls fired on a construct it shouldn't have — inspect the failing
test's source snippet against the two `if (Match(TokenType.LeftParen))` /
`if (param.IsOut)` guards added in Tasks 1-2 before changing anything else.

- [ ] **Step 2: Build the main project to confirm no downstream compile breakage**

Run: `dotnet build AppRefiner/AppRefiner.csproj`

Expected: build succeeds (0 errors). This plan makes no AppRefiner-side changes, so
this is a sanity check that nothing in `AppRefiner` depended on the exact shape of
`DECLARE FUNCTION ... PEOPLECODE` parsing that just changed.

No commit for this task — it's verification-only, nothing to stage.
