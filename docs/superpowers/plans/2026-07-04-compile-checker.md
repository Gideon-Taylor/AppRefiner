# Compile Checker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a reusable `CompileChecker` in the parser library that produces a
single sorted list of compile diagnostics, and consolidate the ~11 compile-error
stylers behind one "Compiler Errors" styler that renders those diagnostics.

**Architecture:** `CompileChecker` (in `PeopleCodeParser.SelfHosted.Compilation`)
collects parse-level diagnostics from `parser.Errors`, runs `TypeCheckerVisitor` for
type errors, then runs a single `CompileCheckDriver` traversal that multiplexes every
AST node to a registry of `ICompileCheck` units. Each check emits `CompileDiagnostic`s
carrying a stable `DiagnosticCode` + optional `FixContext`; AppRefiner maps the code to
quick-fix refactors when building indicators. The library stays UI- and database-
agnostic — the only external dependency is the injectable `ITypeMetadataResolver`,
which `PeopleCodeParser.SelfHosted` already references.

**Tech Stack:** C# / .NET 8, xUnit (`PeopleCodeParser.SelfHosted.Tests`), Windows
Forms (AppRefiner side only).

## Global Constraints

- Target framework: `net8.0` for all projects.
- The parser library (`PeopleCodeParser.SelfHosted`) and `PeopleCodeTypeInfo` MUST NOT
  reference AppRefiner, Windows Forms, Scintilla, `IDataManager`, or any Oracle type.
  The only metadata dependency is `PeopleCodeTypeInfo.Contracts.ITypeMetadataResolver`.
- Diagnostics never reference AppRefiner refactor types. Quick-fix routing lives in
  AppRefiner and keys off `DiagnosticCode` + `CompileDiagnostic.FixContext`.
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`); no new warnings.
- Do NOT run builds in WSL. Builds are Windows-only; the reviewer/user runs them. Name
  the projects to rebuild at each checkpoint (`PeopleCodeParser.SelfHosted`,
  `PeopleCodeParser.SelfHosted.Tests`, `AppRefiner`).
- Type inference is a precondition for type-dependent checks. In AppRefiner,
  `StylerManager` already runs inference before stylers execute (unchanged). The library
  `CompileChecker` assumes inference has been run when a resolver is supplied; it does
  NOT run inference itself in this plan (a future MCP entry point can add that).

## Execution Environment

- Work in an isolated git worktree (create via `superpowers:using-git-worktrees` before
  Task 1).
- Dispatch a fresh subagent per task. Use **opus** for complex tasks (the driver, the
  resolver re-expressions in Phase 3, the constructor port). Use **fable** for the
  code-review stage between tasks.

---

## File Structure

**New — parser library (`PeopleCodeParser.SelfHosted/Compilation/`):**
- `DiagnosticCode.cs` — the stable diagnostic-code enum.
- `CompileDiagnostic.cs` — `CompileDiagnostic` record + `DiagnosticSeverity` enum.
- `IDiagnosticSink.cs` — sink interface checks emit through.
- `ICompileCheck.cs` — check-unit interface.
- `CompileCheckContext.cs` — caller-supplied context + registry exposure.
- `CompileCheckDriver.cs` — the multiplexing `ScopedAstVisitor` that dispatches nodes.
- `CompileChecker.cs` — the driver/entry point (`Check(...)`).
- `Checks/` — one file per `ICompileCheck` (added across Phases 2–4).

**New — AppRefiner:**
- `AppRefiner/Stylers/CompilerErrorsStyler.cs` — the single consolidated styler.
- `AppRefiner/Services/CompileDiagnosticQuickFixMap.cs` — maps `DiagnosticCode` →
  quick-fix entries (static + deferred).

**New — tests (`PeopleCodeParser.SelfHosted.Tests/Compilation/`):**
- `CompileCheckerTests.cs` — pipeline/ordering/dedupe/no-resolver tests.
- `CompileCheckDriverTests.cs` — dispatch-completeness guard test.
- One `XxxCheckTests.cs` per check.

**Modified:**
- `AppRefiner/StylerManager.cs` — nothing structural; the new styler is auto-discovered.
- **Deleted across phases:** `SyntaxErrors.cs`, `TypeErrorStyler.cs`,
  `ClassNameMismatch.cs`, `RedeclaredVariables.cs`, `MissingSemicolon.cs`,
  `InvalidAppClass.cs`, `UnimportedClassStyler.cs`, `AmbiguousClassReferenceStyler.cs`,
  `InvalidMemberAccess.cs`, `UndeclaredFunctionStyler.cs`, `MissingConstructor.cs`,
  `MissingMethodImplementation.cs`, `UnimplementedAbstractMembersStyler.cs`.
  `UndefinedVariables.cs` is **split**, not deleted (see Phase 2, Task 2.2).

---

# Phase 1 — Framework + syntax/type errors

Delivers a working, shippable increment: the consolidated styler renders syntax errors
and type errors via `CompileChecker`, and `SyntaxErrors`/`TypeErrorStyler` are deleted.
The check registry is empty; the driver + guard test prove the dispatch mechanism.

### Task 1.1: Diagnostic model

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Compilation/DiagnosticCode.cs`
- Create: `PeopleCodeParser.SelfHosted/Compilation/CompileDiagnostic.cs`
- Test: `PeopleCodeParser.SelfHosted.Tests/Compilation/CompileDiagnosticTests.cs`

**Interfaces:**
- Produces: `enum DiagnosticSeverity { Error, Warning }`;
  `enum DiagnosticCode { ... }`;
  `sealed record CompileDiagnostic(DiagnosticCode Code, DiagnosticSeverity Severity, SourceSpan Span, string Message, object? FixContext = null)`.

- [ ] **Step 1: Write the failing test**

```csharp
using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Lexing;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class CompileDiagnosticTests
{
    [Fact]
    public void Diagnostic_carries_code_severity_span_and_message()
    {
        var span = new SourceSpan(new SourceLocation(0, 1, 0), new SourceLocation(5, 1, 5));
        var d = new CompileDiagnostic(DiagnosticCode.SyntaxError, DiagnosticSeverity.Error, span, "boom");

        Assert.Equal(DiagnosticCode.SyntaxError, d.Code);
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("boom", d.Message);
        Assert.Null(d.FixContext);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter CompileDiagnosticTests` (reviewer runs on Windows)
Expected: FAIL — `DiagnosticCode` / `CompileDiagnostic` do not exist.

> Note: confirm the exact `SourceLocation` constructor arity by reading
> `PeopleCodeParser.SelfHosted/Lexing/` (fields include a byte index and line). Adjust
> the test's `SourceLocation` construction to match the real signature.

- [ ] **Step 3: Write the model**

```csharp
// DiagnosticCode.cs
namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// Stable, machine-readable identity for each compile diagnostic. Doubles as the
/// quick-fix routing key on the AppRefiner side and the MCP payload key later.
/// Do not renumber or repurpose existing members.
/// </summary>
public enum DiagnosticCode
{
    SyntaxError,
    TypeError,
    TypeWarning,
    MissingSemicolon,
    RedeclaredVariable,
    UndefinedVariable,
    ClassNameMismatch,
    InvalidAppClass,
    UnimportedClass,
    AmbiguousClassReference,
    InvalidMemberAccess,
    MissingConstructor,
    MissingMethodImplementation,
    UnimplementedAbstractMember,
    UndeclaredFunction,
}
```

```csharp
// CompileDiagnostic.cs
namespace PeopleCodeParser.SelfHosted.Compilation;

public enum DiagnosticSeverity { Error, Warning }

/// <summary>
/// One compile finding. FixContext is an opaque payload the check attaches for the
/// UI layer's quick-fix mapping; the library never interprets it.
/// </summary>
public sealed record CompileDiagnostic(
    DiagnosticCode Code,
    DiagnosticSeverity Severity,
    SourceSpan Span,
    string Message,
    object? FixContext = null);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter CompileDiagnosticTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Compilation/DiagnosticCode.cs \
        PeopleCodeParser.SelfHosted/Compilation/CompileDiagnostic.cs \
        PeopleCodeParser.SelfHosted.Tests/Compilation/CompileDiagnosticTests.cs
git commit -m "feat(compile): add CompileDiagnostic model and DiagnosticCode enum"
```

### Task 1.2: Check contracts (`IDiagnosticSink`, `ICompileCheck`, `CompileCheckContext`)

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Compilation/IDiagnosticSink.cs`
- Create: `PeopleCodeParser.SelfHosted/Compilation/ICompileCheck.cs`
- Create: `PeopleCodeParser.SelfHosted/Compilation/CompileCheckContext.cs`
- Test: covered indirectly by Task 1.3's driver test (no standalone test needed —
  these are pure interfaces/DTOs; fold their verification into 1.3).

**Interfaces:**
- Consumes: `DataManagerRequirement` (existing, in `AppRefiner`? — verify: it currently
  lives in AppRefiner. **It must be reachable from the parser library.** If
  `DataManagerRequirement` is defined in AppRefiner, define a parser-library-local
  `CheckRequirement { NotRequired, Optional, Required }` enum in
  `PeopleCodeParser.SelfHosted.Compilation` instead and use that here. Do NOT reference
  AppRefiner from the library.)
- Produces:
  - `interface IDiagnosticSink { void Report(CompileDiagnostic diagnostic); }`
  - `interface ICompileCheck { CheckRequirement Requirement { get; } void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink); void Finish(CompileCheckContext ctx, IDiagnosticSink sink); }`
  - `enum CheckRequirement { NotRequired, Optional, Required }`
  - `sealed class CompileCheckContext` with: `ProgramNode Program`, `ITypeMetadataResolver? Resolver`, `string? ExpectedClassName`, `ScopedAstVisitor<object> ScopeData` (the driver, exposing `VariableRegistry` and scope queries).

- [ ] **Step 1: Write the interfaces**

```csharp
// CheckRequirement.cs content lives in ICompileCheck.cs for cohesion
namespace PeopleCodeParser.SelfHosted.Compilation;

public enum CheckRequirement { NotRequired, Optional, Required }

public interface IDiagnosticSink
{
    void Report(CompileDiagnostic diagnostic);
}

/// <summary>
/// One compile check. The driver calls OnNode for every AST node during a single
/// traversal, then Finish once after traversal completes (for whole-program or
/// whole-class analyses that need the full variable registry).
/// </summary>
public interface ICompileCheck
{
    CheckRequirement Requirement { get; }
    void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink);
    void Finish(CompileCheckContext ctx, IDiagnosticSink sink);
}
```

```csharp
// CompileCheckContext.cs
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;

namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// Read-only context handed to every check. ScopeData is the driver itself, exposing
/// the completed VariableRegistry and scope queries once traversal has finished (safe
/// to query from Finish; during OnNode the registry is still being populated).
/// </summary>
public sealed class CompileCheckContext
{
    public ProgramNode Program { get; }
    public ITypeMetadataResolver? Resolver { get; }
    public string? ExpectedClassName { get; }
    public ScopedAstVisitor<object> ScopeData { get; }

    public CompileCheckContext(
        ProgramNode program,
        ITypeMetadataResolver? resolver,
        string? expectedClassName,
        ScopedAstVisitor<object> scopeData)
    {
        Program = program;
        Resolver = resolver;
        ExpectedClassName = expectedClassName;
        ScopeData = scopeData;
    }
}
```

- [ ] **Step 2: Provide a no-op base (optional convenience)**

Add to `ICompileCheck.cs`:

```csharp
/// <summary>
/// Convenience base so checks override only the hook they use.
/// </summary>
public abstract class CompileCheckBase : ICompileCheck
{
    public virtual CheckRequirement Requirement => CheckRequirement.NotRequired;
    public virtual void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink) { }
    public virtual void Finish(CompileCheckContext ctx, IDiagnosticSink sink) { }
}
```

- [ ] **Step 3: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Compilation/IDiagnosticSink.cs \
        PeopleCodeParser.SelfHosted/Compilation/ICompileCheck.cs \
        PeopleCodeParser.SelfHosted/Compilation/CompileCheckContext.cs
git commit -m "feat(compile): add ICompileCheck, IDiagnosticSink, CompileCheckContext"
```

### Task 1.3: `CompileCheckDriver` — single-traversal dispatch

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Compilation/CompileCheckDriver.cs`
- Test: `PeopleCodeParser.SelfHosted.Tests/Compilation/CompileCheckDriverTests.cs`

**Interfaces:**
- Consumes: `ICompileCheck`, `CompileCheckContext`, `IDiagnosticSink`, `ScopedAstVisitor<object>`.
- Produces: `class CompileCheckDriver : ScopedAstVisitor<object>` with constructor
  `(IReadOnlyList<ICompileCheck> checks, IDiagnosticSink sink)`, a settable
  `CompileCheckContext Context`, and a `void Run(ProgramNode program)` that performs the
  traversal then calls `Finish` on each check.

**Design note (why override every VisitX):** `AstVisitorBase` traverses most node types
with explicit per-type child walks that bypass `DefaultVisit`, and `Children` omits
some structural nodes (type nodes are `Accept()`-ed explicitly). Therefore dispatch must
ride the visitor's `Accept` path: the driver overrides **every** `VisitX` method with the
identical body `{ DispatchNode(node); base.VisitX(node); }`. Extending `ScopedAstVisitor`
means `base.VisitX` also builds the variable registry in the same pass. Dispatch is
pre-order (fires before children). The guard test enforces exactly-once dispatch and
catches drift if new node types are added.

- [ ] **Step 1: Write the failing guard test**

```csharp
using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Nodes;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class CompileCheckDriverTests
{
    // A recording check that captures every node the driver dispatches to it.
    private sealed class RecordingCheck : CompileCheckBase
    {
        public readonly List<AstNode> Seen = new();
        public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
            => Seen.Add(node);
    }

    private sealed class NullSink : IDiagnosticSink
    {
        public void Report(CompileDiagnostic diagnostic) { }
    }

    // Counts every node reachable via Accept, by using a plain recording visitor.
    private sealed class CountingVisitor : Visitors.AstVisitorBase
    {
        public int Count;
        protected override void DefaultVisit(AstNode node) { Count++; base.DefaultVisit(node); }
        // NOTE: DefaultVisit undercounts because specialized methods bypass it; this
        // helper is only a lower bound. The real assertion below compares against a
        // second RecordingCheck-independent full walk. See Step 1b.
    }

    [Fact]
    public void Driver_dispatches_every_node_exactly_once()
    {
        var src = @"
import PKG:Foo;
class Bar
   method DoIt(&x as string) Returns number;
   property string Name get;
end-class;
method DoIt
   Local number &n = 1 + 2;
   If &n > 0 Then
      &n = &n - 1;
   End-If;
   Return &n;
end-method;
get Name
   Return ""hi"";
end-get;
";
        var (program, _) = ParseTestHelper.Parse(src);

        var recorder = new RecordingCheck();
        var driver = new CompileCheckDriver(new[] { recorder }, new NullSink());
        driver.Context = new CompileCheckContext(program, null, "Bar", driver);
        driver.Run(program);

        // Every dispatched node is unique (exactly-once).
        Assert.Equal(recorder.Seen.Count, recorder.Seen.Distinct().Count());

        // The program root and a representative sample of node types were dispatched.
        Assert.Contains(recorder.Seen, n => n is ProgramNode);
        Assert.Contains(recorder.Seen, n => n is AppClassNode);
        Assert.Contains(recorder.Seen, n => n is MethodNode);
        Assert.Contains(recorder.Seen, n => n is IfStatementNode);
        Assert.Contains(recorder.Seen, n => n is AppClassTypeNode);   // type node reached
        Assert.Contains(recorder.Seen, n => n is BinaryOperationNode);
    }
}
```

> The `CountingVisitor` inner class is illustrative only; do not rely on it for the
> assertion. If it causes confusion, omit it — the real guarantees are the `Distinct`
> equality and the representative-type `Contains` checks. The type-node assertion
> (`AppClassTypeNode`) is the critical one: it proves dispatch rides `Accept`, not
> `Children`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter CompileCheckDriverTests`
Expected: FAIL — `CompileCheckDriver` does not exist.

- [ ] **Step 3: Implement the driver**

Read `PeopleCodeParser.SelfHosted/Visitors/IAstVisitor.cs` to get the **complete** list
of `VisitX` methods (~45). Override each with the uniform body. Skeleton:

```csharp
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// Runs all registered checks in a single AST traversal. Extends ScopedAstVisitor so
/// the variable registry is populated during the same pass. Dispatch is pre-order.
/// </summary>
public sealed class CompileCheckDriver : ScopedAstVisitor<object>
{
    private readonly IReadOnlyList<ICompileCheck> _checks;
    private readonly IDiagnosticSink _sink;

    public CompileCheckContext Context { get; set; } = null!;

    public CompileCheckDriver(IReadOnlyList<ICompileCheck> checks, IDiagnosticSink sink)
    {
        _checks = checks;
        _sink = sink;
    }

    public void Run(ProgramNode program)
    {
        program.Accept(this);           // OnNode dispatch + registry population
        foreach (var check in _checks)
        {
            try { check.Finish(Context, _sink); }
            catch (Exception ex) { LogCheckFailure(check, ex); }
        }
    }

    private void DispatchNode(AstNode node)
    {
        foreach (var check in _checks)
        {
            try { check.OnNode(node, Context, _sink); }
            catch (Exception ex) { LogCheckFailure(check, ex); }
        }
    }

    private static void LogCheckFailure(ICompileCheck check, Exception ex)
    {
        // Library has no Debug.Log; swallow to preserve the "one bad check can't abort
        // the pass" guarantee. Optionally expose a failures list for tests/telemetry.
    }

    // --- One override per node type; identical shape. Example subset: ---
    public override void VisitProgram(ProgramNode node) { DispatchNode(node); base.VisitProgram(node); }
    public override void VisitAppClass(AppClassNode node) { DispatchNode(node); base.VisitAppClass(node); }
    public override void VisitMethod(MethodNode node) { DispatchNode(node); base.VisitMethod(node); }
    public override void VisitAppClassType(AppClassTypeNode node) { DispatchNode(node); base.VisitAppClassType(node); }
    public override void VisitIf(IfStatementNode node) { DispatchNode(node); base.VisitIf(node); }
    public override void VisitBinaryOperation(BinaryOperationNode node) { DispatchNode(node); base.VisitBinaryOperation(node); }
    // ... continue for EVERY VisitX declared on IAstVisitor. Missing one silently
    // drops that node type from dispatch — the guard test's representative checks catch
    // the common ones; add assertions if a specific check needs an unusual node type.
}
```

**Important:** consider adding an internal `List<(ICompileCheck, Exception)> Failures`
that `LogCheckFailure` appends to, so tests can assert no check threw. Optional but
recommended.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter CompileCheckDriverTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Compilation/CompileCheckDriver.cs \
        PeopleCodeParser.SelfHosted.Tests/Compilation/CompileCheckDriverTests.cs
git commit -m "feat(compile): add single-traversal CompileCheckDriver with dispatch guard test"
```

### Task 1.4: `CompileChecker` — pipeline entry point

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Compilation/CompileChecker.cs`
- Test: `PeopleCodeParser.SelfHosted.Tests/Compilation/CompileCheckerTests.cs`

**Interfaces:**
- Consumes: `ParseError` (has `SourceSpan Location`, `string Message`), `TypeError`
  (has `AstNode Node`, `string Message`) via `program.GetAllTypeErrors()` /
  `GetAllTypeWarnings()`, `TypeCheckerVisitor.Run(program, resolver, resolver.Cache)`.
- Produces:
  `static IReadOnlyList<CompileDiagnostic> CompileChecker.Check(ProgramNode program, IReadOnlyList<ParseError> parserErrors, ITypeMetadataResolver? resolver, CompileCheckContextInput context)`
  where `CompileCheckContextInput` is a small input DTO `(string? ExpectedClassName)`.
  The registered check list is a private static readonly field, empty in Phase 1.

- [ ] **Step 1: Write failing tests**

```csharp
using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class CompileCheckerTests
{
    [Fact]
    public void Syntax_errors_become_diagnostics()
    {
        var (program, errors) = ParseTestHelper.Parse("Local number &n =;"); // malformed
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.SyntaxError);
    }

    [Fact]
    public void No_resolver_skips_type_checks_but_still_returns()
    {
        var (program, errors) = ParseTestHelper.Parse("Local number &n = 1;");
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.TypeError);
    }

    [Fact]
    public void Diagnostics_are_sorted_by_start_offset()
    {
        var (program, errors) = ParseTestHelper.Parse("Local number &n =;\nLocal string &s =;");
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));

        for (int i = 1; i < diags.Count; i++)
            Assert.True(diags[i - 1].Span.Start.ByteIndex <= diags[i].Span.Start.ByteIndex);
    }
}
```

> Verify `SourceSpan.Start.ByteIndex` is the correct property path by reading
> `Lexing/SourceLocation` (memory: byte index tracking exists). Adjust if the property
> is named differently (e.g. `ByteOffset`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter CompileCheckerTests`
Expected: FAIL — `CompileChecker` / `CompileCheckContextInput` do not exist.

- [ ] **Step 3: Implement `CompileChecker`**

```csharp
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;

namespace PeopleCodeParser.SelfHosted.Compilation;

public readonly record struct CompileCheckContextInput(string? ExpectedClassName);

public static class CompileChecker
{
    // Grows across phases. Order here is not significant; results are sorted at the end.
    private static readonly IReadOnlyList<ICompileCheck> Checks = new ICompileCheck[]
    {
        // Phase 2+: add check instances here (checks are stateless-per-run;
        // the driver calls Reset-equivalent by constructing fresh Context each run.
        // If a check holds per-run state, make it hold that state in fields cleared in
        // VisitProgram/Finish, OR construct checks per Check() call instead of static.)
    };

    public static IReadOnlyList<CompileDiagnostic> Check(
        ProgramNode program,
        IReadOnlyList<ParseError> parserErrors,
        ITypeMetadataResolver? resolver,
        CompileCheckContextInput context)
    {
        var sink = new ListDiagnosticSink();

        // 1. Parse-level diagnostics.
        foreach (var err in parserErrors)
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.SyntaxError, DiagnosticSeverity.Error, err.Location, err.Message));

        // 2. Type errors/warnings (requires inference already run + a resolver).
        if (resolver != null)
        {
            TypeCheckerVisitor.Run(program, resolver, resolver.Cache);
            foreach (var te in program.GetAllTypeErrors())
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.TypeError, DiagnosticSeverity.Error, te.Node.SourceSpan, te.Message));
            foreach (var tw in program.GetAllTypeWarnings())
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.TypeWarning, DiagnosticSeverity.Warning, tw.Node.SourceSpan, tw.Message));
        }

        // 3. Single dispatch traversal for AST/semantic checks.
        var active = Checks
            .Where(c => c.Requirement != CheckRequirement.Required || resolver != null)
            .ToList();
        if (active.Count > 0)
        {
            var driver = new CompileCheckDriver(active, sink);
            driver.Context = new CompileCheckContext(program, resolver, context.ExpectedClassName, driver);
            driver.Run(program);
        }

        // 4. Sort by start offset, then stable by code; dedupe exact repeats.
        return sink.Diagnostics
            .OrderBy(d => d.Span.Start.ByteIndex)
            .ThenBy(d => d.Code)
            .Distinct()
            .ToList();
    }

    private sealed class ListDiagnosticSink : IDiagnosticSink
    {
        public readonly List<CompileDiagnostic> Diagnostics = new();
        public void Report(CompileDiagnostic diagnostic) => Diagnostics.Add(diagnostic);
    }
}
```

> `Distinct()` on the record dedupes exact-equal diagnostics (same code/severity/span/
> message/fixcontext). `FixContext` reference-equality is fine — real duplicates from a
> single run share no FixContext anyway.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter CompileCheckerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Compilation/CompileChecker.cs \
        PeopleCodeParser.SelfHosted.Tests/Compilation/CompileCheckerTests.cs
git commit -m "feat(compile): add CompileChecker pipeline (parse + type diagnostics)"
```

### Task 1.5: `CompilerErrorsStyler` + quick-fix map (AppRefiner)

**Files:**
- Create: `AppRefiner/Stylers/CompilerErrorsStyler.cs`
- Create: `AppRefiner/Services/CompileDiagnosticQuickFixMap.cs`
- Delete: `AppRefiner/Stylers/SyntaxErrors.cs`, `AppRefiner/Stylers/TypeErrorStyler.cs`
- Test: manual (AppRefiner has no unit test harness; verify via the app — see checkpoint).

**Interfaces:**
- Consumes: `CompileChecker.Check(...)`, `Editor.ParserErrors`,
  `Editor.AppDesignerProcess.TypeResolver`, `Editor.ClassPath`.
- Produces: `class CompilerErrorsStyler : BaseStyler` (`Active = true` by default,
  `DatabaseRequirement = Optional`, `Description = "Compiler errors"`).

- [ ] **Step 1: Implement the quick-fix map**

Map each `DiagnosticCode` to quick-fix entries. Phase 1 wires only the codes that exist
now (`TypeError` → `AssignToNewVariable`, matching `TypeErrorStyler`'s current behavior).
Later phases extend this map. Read `AppRefiner/Stylers/TypeErrorStyler.cs` lines 72–92 to
replicate the `AssignToVariableContext` construction exactly.

```csharp
using AppRefiner.Stylers;
using PeopleCodeParser.SelfHosted.Compilation;

namespace AppRefiner.Services;

/// <summary>
/// Maps a library CompileDiagnostic to AppRefiner quick-fix entries. Keeps refactor
/// references out of the parser library. Some codes produce deferred resolvers
/// (DB-query-on-Ctrl+.), added in later phases.
/// </summary>
internal static class CompileDiagnosticQuickFixMap
{
    public static List<QuickFixEntry> GetQuickFixes(CompileDiagnostic d)
    {
        switch (d.Code)
        {
            case DiagnosticCode.TypeError when d.FixContext is Refactors.QuickFixes.AssignToVariableContext ctx:
                return new List<QuickFixEntry>
                {
                    new(typeof(Refactors.QuickFixes.AssignToNewVariable),
                        "Assign result to a new local variable", ctx)
                };
            default:
                return new List<QuickFixEntry>();
        }
    }
}
```

> The type-error `FixContext` must be produced inside a Phase-1 check or by the styler.
> Since Phase 1 has no checks, and type errors come straight from `CompileChecker`,
> attach the `AssignToVariableContext` in the styler when it sees a `TypeError` whose
> span belongs to an `ExpressionStatementNode` — OR (cleaner) defer the assign-to-var
> quick fix to when the TypeError check moves into a real `ICompileCheck`. **Decision for
> Phase 1:** preserve today's behavior — in the styler, for `TypeError` diagnostics,
> re-derive the `ExpressionStatementNode` case exactly as `TypeErrorStyler` did, since
> the diagnostic carries the node's span. Simplest: keep the assign-to-var mapping in the
> styler for now; generalize when checks own fix context.

- [ ] **Step 2: Implement the styler**

```csharp
using AppRefiner.Services;
using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers;

/// <summary>
/// Consolidated styler that surfaces every "won't compile" diagnostic from the shared
/// CompileChecker. Replaces the individual compile-error stylers.
/// </summary>
public class CompilerErrorsStyler : BaseStyler
{
    private const uint ERROR_COLOR = 0x0000FFA0;   // red squiggle (matches old stylers)
    private const uint WARNING_COLOR = 0x32FF32FF; // light green (matches TypeErrorStyler)

    public CompilerErrorsStyler() { Active = true; }

    public override string Description => "Compiler errors";
    public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        if (Editor == null) return;

        var resolver = Editor.AppDesignerProcess?.TypeResolver;
        var expectedClassName = Editor.ClassPath?.Split(':').LastOrDefault();

        var diagnostics = CompileChecker.Check(
            node,
            Editor.ParserErrors,
            resolver,
            new CompileCheckContextInput(expectedClassName));

        foreach (var d in diagnostics)
        {
            var color = d.Severity == DiagnosticSeverity.Warning ? WARNING_COLOR : ERROR_COLOR;
            var quickFixes = CompileDiagnosticQuickFixMap.GetQuickFixes(d);
            AddIndicator(d.Span, IndicatorType.SQUIGGLE, color, d.Message, quickFixes);
        }
        // Do NOT call base.VisitProgram — CompileChecker already traversed. This styler
        // is a thin adapter; a second traversal would be wasted work.
    }
}
```

> **Verify:** `AddIndicator(SourceSpan, ...)` exists on `BaseStyler` (it does — line 125
> of `BaseStyler.cs`). Deferred quick-fixes (unimported class) need
> `AddIndicatorWithDeferredQuickFix`; wire that in Phase 3 when that check lands, keyed
> off `DiagnosticCode.UnimportedClass` in the map.

- [ ] **Step 3: Delete the replaced stylers**

```bash
git rm AppRefiner/Stylers/SyntaxErrors.cs AppRefiner/Stylers/TypeErrorStyler.cs
```

- [ ] **Step 4: Checkpoint — reviewer builds and runs**

Reviewer rebuilds `PeopleCodeParser.SelfHosted`, `PeopleCodeParser.SelfHosted.Tests`,
`AppRefiner` on Windows, then opens Application Designer and confirms: syntax errors and
type errors squiggle under the single "Compiler errors" styler; the old two rows are
gone from the styler grid; assign-to-var quick fix still works on an unassigned
expression. Expected: parity with prior behavior for these two check families.

- [ ] **Step 5: Commit**

```bash
git add AppRefiner/Stylers/CompilerErrorsStyler.cs \
        AppRefiner/Services/CompileDiagnosticQuickFixMap.cs
git commit -m "feat(compile): consolidated CompilerErrorsStyler over CompileChecker; remove SyntaxErrors/TypeErrorStyler"
```

---

# Phase 2 — Pure-AST checks

Each task ports one styler's logic into an `ICompileCheck` (`Requirement =
NotRequired`), adds a unit test, deletes the old styler, and (if it had a quick fix)
extends `CompileDiagnosticQuickFixMap`. **The executor MUST read the cited source styler
and reproduce its logic** — these are behavioral ports, not rewrites.

**Registration:** add each new check instance to `CompileChecker.Checks`.

**Per-check test shape (reuse for every check task):**

```csharp
[Fact]
public void Flags_the_defect()
{
    var (program, errors) = ParseTestHelper.Parse(SOURCE_WITH_DEFECT);
    var diags = CompileChecker.Check(program, errors, resolver: RESOLVER_OR_NULL,
        new CompileCheckContextInput(EXPECTED_CLASS_NAME));
    Assert.Contains(diags, d => d.Code == DiagnosticCode.THE_CODE);
}

[Fact]
public void Clean_code_produces_no_diagnostic_of_this_code()
{
    var (program, errors) = ParseTestHelper.Parse(CLEAN_SOURCE);
    var diags = CompileChecker.Check(program, errors, resolver: RESOLVER_OR_NULL,
        new CompileCheckContextInput(EXPECTED_CLASS_NAME));
    Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.THE_CODE);
}
```

### Task 2.1: ClassNameMismatch check

- **Source to port:** `AppRefiner/Stylers/ClassNameMismatch.cs` (compares `AppClassNode.Name`
  to `Editor.ClassPath.Split(':').Last()`; library uses `ctx.ExpectedClassName`).
- **Hook:** `OnNode` on `AppClassNode` → if `ctx.ExpectedClassName` is non-null and
  `!string.Equals(node.Name, ctx.ExpectedClassName, OrdinalIgnoreCase)`, report
  `ClassNameMismatch` at `node.NameToken.SourceSpan` with `FixContext = node.NameToken`
  (or the expected name — whatever `CorrectClassName` needs).
- **Quick fix:** map `ClassNameMismatch` → `Refactors.QuickFixes.CorrectClassName`
  (read that refactor to learn its required context).
- **Test:** defect = class named `Baz` with `ExpectedClassName = "Bar"`; clean = names match.
- Create `Checks/ClassNameMismatchCheck.cs`; delete `Stylers/ClassNameMismatch.cs`.

### Task 2.2: UndefinedVariables — split into check + surviving styler

- **Source to port:** `AppRefiner/Stylers/UndefinedVariables.cs`. Read it fully; it uses
  the scope/variable registry to find references with no accessible declaration.
- **Check (class code only):** `Checks/UndefinedVariableCheck.cs`,
  `Requirement = NotRequired`. In `Finish`, use `ctx.ScopeData.VariableRegistry` to find
  undefined variable references, **but only when `ctx.Program.AppClass != null`** (report
  `UndefinedVariable`). Non-class programs are handled by the surviving styler.
- **Surviving styler:** keep a trimmed `UndefinedVariables` styler that flags undefined
  vars **only when `Editor` has no app class** (the code-smell case). Rename its
  `Description` to make the split obvious (e.g. "Undefined variables (non-class code)").
  It stays independently toggleable and is NOT deleted.
- **Test:** defect = app class method referencing `&undeclared`; clean = declared local.
  Add a second test: a non-class program with `&undeclared` produces NO
  `UndefinedVariable` diagnostic from `CompileChecker` (it belongs to the styler).

### Task 2.3: RedeclaredVariables check

- **Source to port:** `AppRefiner/Stylers/RedeclaredVariables.cs`.
- **Approach:** `Finish` over `ctx.ScopeData.VariableRegistry` — find variables declared
  more than once in the same scope; report `RedeclaredVariable` at each redeclaration span.
- **Quick fix:** none.
- **Test:** defect = two `Local number &n;` in one method; clean = one declaration.
- Create `Checks/RedeclaredVariableCheck.cs`; delete `Stylers/RedeclaredVariables.cs`.

### Task 2.4: MissingSemicolon check

- **Source to port:** `AppRefiner/Stylers/MissingSemicolon.cs`. Read it to learn how it
  detects missing statement terminators (token/AST based).
- **Hook:** whichever node/statement it inspects; report `MissingSemicolon`.
- **Quick fix:** if the styler offered one, map it; else none.
- **Test:** defect = statement missing `;`; clean = terminated statement.
- Create `Checks/MissingSemicolonCheck.cs`; delete `Stylers/MissingSemicolon.cs`.

- [ ] **Phase 2 checkpoint:** reviewer builds all three projects + runs tests
  (`dotnet test PeopleCodeParser.SelfHosted.Tests --filter Compilation`), opens the app,
  confirms the four defects squiggle under "Compiler errors", the split UndefinedVariables
  styler still flags non-class code, and the four old rows are gone. Commit per task.

---

# Phase 3 — Resolver-backed checks

`Requirement = Optional` (skip gracefully when `ctx.Resolver == null`). **These require
re-expressing `IDataManager`-based logic against `ITypeMetadataResolver`.** Use the
existing `TestTypeMetadataResolver` in `PeopleCodeTypeInfo.Tests` as the pattern for a
test resolver (or reference it if accessible from the parser test project; otherwise add
a minimal fake in the test file).

> Memory aids (verify against code): `resolver.GetTypeMetadata(qualifiedName)` →
> `TypeMetadata?` with case-insensitive `Methods`/`Properties`/`InstanceVariables`
> dictionaries; `null` return means "assume valid" (unloaded module). "App class exists"
> = `GetTypeMetadata(path) != null`. Builtin object members via
> `PeopleCodeTypeDatabase.GetMethod/GetProperty`.

### Task 3.1: InvalidAppClass check

- **Source to port:** `AppRefiner/Stylers/InvalidAppClass.cs` (currently
  `DataManager.CheckAppClassExists`). Re-express: exists =
  `ctx.Resolver.GetTypeMetadata(appClassPath) != null`. Preserve the inferred-type
  preference (lines 64–67) and the "skip if not fully-qualified (`!Contains(':')`)" guard.
- **Hook:** `OnNode` on `AppClassTypeNode` (and the `AppClassNode.BaseType` case).
- **Quick fix:** none.
- **Test:** defect = `PKG:Missing` unknown to the fake resolver; clean = a class the fake
  resolver returns metadata for.
- Create `Checks/InvalidAppClassCheck.cs`; delete `Stylers/InvalidAppClass.cs`.

### Task 3.2: UnimportedClass check (with deferred quick fix)

- **Source to port:** `AppRefiner/Stylers/UnimportedClassStyler.cs`. The check portion
  (collect imports from `ProgramNode.Imports`, flag `AppClassTypeNode` occurrences whose
  class name is not imported) moves to the library. **The DB-querying quick-fix resolver
  stays in AppRefiner** (it needs `IDataManager.GetPackagesForClass`).
- **Hook:** `OnNode` on `AppClassTypeNode` + object-creation/variable-decl type nodes, as
  the styler does. Report `UnimportedClass` with `FixContext = className` (string).
- **Quick fix:** in `CompileDiagnosticQuickFixMap`, for `UnimportedClass` the styler must
  use `AddIndicatorWithDeferredQuickFix` (not the static list). **This requires extending
  the map/styler to support deferred resolvers keyed by code.** Port
  `GetImportOptionsResolver` + `PrioritizeByExistingImports` from the old styler into the
  AppRefiner side (e.g. into `CompileDiagnosticQuickFixMap` or a helper), and have
  `CompilerErrorsStyler` call `AddIndicatorWithDeferredQuickFix` when the code is
  `UnimportedClass`, passing `d.FixContext` as the context.
- **Test (library):** defect = `Local PKG:Foo:Bar &x;` with no matching import; clean =
  same with `import PKG:Foo:Bar;` present. (Wildcard-import DB expansion is AppRefiner-side
  and out of the library test's scope — note this explicitly.)
- Create `Checks/UnimportedClassCheck.cs`; delete `Stylers/UnimportedClassStyler.cs`.

**Sub-step: extend the styler for deferred quick fixes.** In `CompilerErrorsStyler`,
branch: if `CompileDiagnosticQuickFixMap.HasDeferredResolver(d.Code)`, call
`AddIndicatorWithDeferredQuickFix(d.Span, ..., resolver, d.FixContext)`; else use the
static `AddIndicator(... GetQuickFixes(d))`. Add `HasDeferredResolver` +
`GetDeferredResolver` to the map.

### Task 3.3: AmbiguousClassReference check

- **Source to port:** `AppRefiner/Stylers/AmbiguousClassReferenceStyler.cs`.
- **Hook:** as in the styler; report `AmbiguousClassReference`.
- **Quick fix:** map → `Refactors.QuickFixes.ReplaceWithQualifiedClassNameQuickFix`
  (read it for required context; likely the qualified name — put it in `FixContext`).
- **Test:** defect = a short class name resolvable in multiple imported packages (fake
  resolver returns 2 packages); clean = unambiguous.
- Create `Checks/AmbiguousClassReferenceCheck.cs`; delete the styler.

### Task 3.4: InvalidMemberAccess check

- **Source to port:** `AppRefiner/Stylers/InvalidMemberAccess.cs` (largest/most nuanced;
  uses inferred types + resolver + `PeopleCodeTypeDatabase`; note the per-class member
  cache and `ClearMemberCache` hooks in `StylerManager`). Read fully. Honor the semantic
  rules in memory: never validate property names on `RecordTypeInfo`; method calls need
  `()`; property access must not fall back to methods; `null` metadata = assume valid.
- **Hook:** `OnNode` on `MemberAccessNode` (+ `FunctionCallNode` for method calls, as the
  styler does).
- **Quick fix:** none.
- **Cache:** the member cache moves into the check instance. `StylerManager.ClearMemberCache`
  / `ClearMemberCacheForClass` currently reach into the `InvalidMemberAccess` styler —
  **update those methods** to instead clear the cache on the check (expose a static
  `InvalidMemberAccessCheck.ClearCache()` / `ClearForClass(path)` and call it from the
  same StylerManager entry points). Verify callers of those StylerManager methods still
  compile.
- **Test:** defect = `&builtinObj.NoSuchMethod()`; clean = a real builtin method.
- Create `Checks/InvalidMemberAccessCheck.cs`; delete the styler.

### Task 3.5: UndeclaredFunction check

- **Source to port:** `AppRefiner/Stylers/UndeclaredFunctionStyler.cs`. Uses program-level
  function declarations (`ProgramNode.Functions`) + builtin function DB.
- **Hook:** `OnNode` on `FunctionCallNode`; report `UndeclaredFunction` when the callee is
  neither a declared function nor a known builtin.
- **Quick fix:** map → `DeclareFunctionQuickFix` and/or `OpenDeclareFunctionDialogQuickFix`
  (read both; they may both apply — return two entries).
- **Test:** defect = call to `DoesNotExist()`; clean = a declared local function call.
- Create `Checks/UndeclaredFunctionCheck.cs`; delete the styler.

- [ ] **Phase 3 checkpoint:** reviewer builds + runs tests + exercises each in the app
  with a live DB connection AND with none (confirm graceful skip). Confirm quick fixes
  (add import deferred, qualify ambiguous, declare function) still work. Commit per task.

---

# Phase 4 — Structural checks (Finish-phase)

These analyze a whole class after traversal. Implement primarily in `Finish` (or in
`OnNode` on `AppClassNode`, which contains all members).

### Task 4.1: MissingMethodImplementation check

- **Source to port:** `AppRefiner/Stylers/MissingMethodImplementation.cs`
  (**`Requirement = NotRequired`** — pure AST: declared methods vs implemented methods in
  the same program).
- **Hook:** `OnNode` on `AppClassNode` (or `Finish`); report `MissingMethodImplementation`
  per declared-but-unimplemented method.
- **Quick fix:** map → `Refactors.QuickFixes.ImplementMissingMethod` (read for context).
- **Test:** defect = class declares `method Foo();` with no `method Foo ... end-method`;
  clean = implemented.
- Create `Checks/MissingMethodImplementationCheck.cs`; delete the styler.

### Task 4.2: UnimplementedAbstractMembers check

- **Source to port:** `AppRefiner/Stylers/UnimplementedAbstractMembersStyler.cs`
  (`Requirement = Optional` — needs base/interface metadata via resolver).
- **Hook:** `Finish` / `AppClassNode`; report `UnimplementedAbstractMember`.
- **Quick fix:** map → `Refactors.QuickFixes.ImplementAbstractMembers`.
- **Test:** defect = class implements an interface (fake resolver) but omits a member;
  clean = all members present.
- Create `Checks/UnimplementedAbstractMemberCheck.cs`; delete the styler.

### Task 4.3: MissingConstructor check (heaviest resolver port)

- **Source to port:** `AppRefiner/Stylers/MissingConstructor.cs`. Currently
  `Requirement = Required` and uses `DataManager.GetAppClassSourceByPath(baseClassPath)`
  to fetch and inspect the **base class source** for a constructor. Re-express against
  `ITypeMetadataResolver`: a PeopleCode constructor is a method whose name equals the
  class name — check `resolver.GetTypeMetadata(baseClassPath).Methods` for a constructor
  entry rather than re-parsing source. If the resolver cannot express this, keep the check
  `Optional` and skip when metadata is insufficient (report nothing rather than
  false-positive). Document the decision in the check's XML doc comment.
- **Hook:** `Finish` / `AppClassNode` with a `BaseType`.
- **Quick fix:** map → `Refactors.QuickFixes.GenerateBaseConstructor`.
- **Test:** defect = subclass of a base that has a constructor, subclass lacks one; clean =
  subclass defines a constructor. Use the fake resolver to supply base metadata.
- Create `Checks/MissingConstructorCheck.cs`; delete the styler.

- [ ] **Phase 4 checkpoint + final cleanup:**
  - Reviewer builds + runs full test suite.
  - Confirm every deleted styler is gone and no dangling references remain
    (`grep` for each old class name across `AppRefiner/`).
  - Confirm the styler grid shows exactly one "Compiler errors" row plus the surviving
    non-compile stylers (incl. the split "Undefined variables (non-class code)").
  - Confirm `StylerManager.ClearMemberCache`/`ClearMemberCacheForClass` now target the
    check, and their call sites compile.
  - Settings: obsolete `StylerStates` entries for removed stylers are silently ignored
    (no migration code needed); the consolidated styler defaults on. Verify by launching
    with an existing settings blob.
  - Final commit.

---

## Self-Review

**Spec coverage** (against `2026-07-04-compile-checker-design.md`):
- §2 placement in parser library → Tasks 1.1–1.4 (Compilation folder). ✓
- §3 diagnostic model (code + severity + span + FixContext, no refactor refs) → 1.1. ✓
- §4 composite driver = ScopedAstVisitor, per-check try/catch, Finish phase → 1.3. ✓
- §5 pipeline (parse → inference precondition → type check → dispatch → sort/dedupe) → 1.4. ✓
- §6 all 14 checks → Phase 1 (syntax, type), Phase 2 (missing-semicolon, redeclared,
  undefined-in-class, class-name), Phase 3 (invalid-app-class, unimported, ambiguous,
  invalid-member, undeclared-fn), Phase 4 (missing-ctor, missing-method-impl,
  unimplemented-abstract). ✓
- §7 consolidated styler (default on, Optional, code→quickfix map incl. deferred) → 1.5,
  3.2. ✓
- §7 settings migration (obsolete keys ignored) → Phase 4 checkpoint. ✓
- §8 UndefinedVariables split + cosmetic stylers untouched → 2.2. ✓
- §9 phasing → the four phases. ✓
- §10 per-check unit tests + checker integration tests → per-task tests + 1.4. ✓
- §11 non-goals (MCP, TypeChecker folding) → explicitly deferred. ✓

**Placeholder scan:** Per-check tasks in Phases 2–4 intentionally cite source stylers to
port rather than paste final code — these are behavioral ports of existing, working logic,
and pasting reverse-engineered code would be less accurate than instructing the executor to
read the proven source. Every such task fixes the interface (hook, diagnostic code, quick-fix
class, concrete test). Phase 1 (novel framework) carries complete code.

**Type consistency:** `CompileChecker.Check(program, parserErrors, resolver, CompileCheckContextInput)`
used identically in 1.4 and all Phase 2–4 tests. `ICompileCheck.OnNode/Finish`,
`CompileCheckContext.{Program,Resolver,ExpectedClassName,ScopeData}`, `DiagnosticCode`
members, and `CompileDiagnosticQuickFixMap` are consistent across tasks. `CheckRequirement`
(library-local) is used rather than AppRefiner's `DataManagerRequirement` inside the library.

**Open items the executor must verify against code (flagged inline):**
- `SourceLocation` constructor arity and the byte-index property name (`ByteIndex` vs other).
- Whether `DataManagerRequirement` is AppRefiner-only (→ use library-local `CheckRequirement`).
- Exact context each quick-fix refactor requires (`CorrectClassName`, `AddImportQuickFix`,
  `ReplaceWithQualifiedClassNameQuickFix`, `DeclareFunctionQuickFix`,
  `ImplementMissingMethod`, `ImplementAbstractMembers`, `GenerateBaseConstructor`,
  `AssignToNewVariable`).
- Whether `ITypeMetadataResolver` can express "class has a constructor" for Task 4.3.
