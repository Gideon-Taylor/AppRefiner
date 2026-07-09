using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;

namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// Small input DTO carried into <see cref="CompileChecker.Check"/> and forwarded to the
/// per-run <see cref="CompileCheckContext"/>. Kept separate from the context so callers
/// don't construct the driver-coupled context themselves.
/// </summary>
/// <param name="ExpectedClassName">
/// Final path segment of the class being compiled (used by ClassNameMismatchCheck).
/// </param>
/// <param name="SelfMetadata">
/// Optional live in-editor metadata for the class being compiled. When supplied, checks
/// that resolve members on the current ("self") class use it in preference to the
/// DB-backed resolver, so members added but not yet saved are recognized. Must carry the
/// FULL qualified class path so it matches the QualifiedName inference stamps onto %This.
/// </param>
public readonly record struct CompileCheckContextInput(
    string? ExpectedClassName,
    TypeMetadata? SelfMetadata = null);

/// <summary>
/// Public entry point for the compile checker pipeline. Collects parser errors, runs the
/// type checker (when a resolver is supplied), runs the single-traversal check driver,
/// then returns one sorted, deduplicated diagnostic list.
/// </summary>
public static class CompileChecker
{
    // Grows across phases. Order here is not significant; results are sorted at the end.
    // Constructed per Check() call: several checks are stateful (they accumulate per-run
    // data, resetting on the ProgramNode), and shared static instances raced when
    // Check() ran concurrently (e.g. xUnit test classes in parallel).
    private static ICompileCheck[] CreateChecks() => new ICompileCheck[]
    {
        new Checks.ClassNameMismatchCheck(),
        new Checks.UndefinedVariableCheck(),
        new Checks.RedeclaredVariableCheck(),
        new Checks.MissingSemicolonCheck(),
        new Checks.InvalidAppClassCheck(),
        new Checks.UnimportedClassCheck(),
        new Checks.AmbiguousClassReferenceCheck(),
        new Checks.InvalidMemberAccessCheck(),
        new Checks.UndeclaredFunctionCheck(),
        new Checks.MissingMethodImplementationCheck(),
        new Checks.MissingConstructorCheck(),
        // NOTE: unimplemented abstract members are intentionally NOT a compile check.
        // They are an advisory code smell (they do not stop compilation) and are surfaced
        // separately by AppRefiner's UnimplementedAbstractMembers styler, so they never trip
        // the unified "Compiler errors" indicator.
    };

    public static IReadOnlyList<CompileDiagnostic> Check(
        ProgramNode program,
        IReadOnlyList<ParseError> parserErrors,
        ITypeMetadataResolver? resolver,
        CompileCheckContextInput context)
    {
        var sink = new ListDiagnosticSink();

        // NullTypeMetadataResolver is the stand-in used when no DB is connected — every
        // GetTypeMetadata returns null. Treating it as "no resolver" prevents
        // existence checks (InvalidAppClass, etc.) from flagging every app class path.
        if (resolver is NullTypeMetadataResolver)
            resolver = null;

        // 1. Parse-level diagnostics.
        foreach (var err in parserErrors)
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.SyntaxError, DiagnosticSeverity.Error, err.Location, err.Message));

        // 2. Type errors/warnings (requires inference already run + a resolver).
        if (resolver != null)
        {
            // Isolate the whole type-check step. A single fault in TypeCheckerVisitor (or
            // while collecting its results) must not abort Check(): the parse-level
            // diagnostics already in the sink and the driver's AST checks (phase 3) must
            // both survive. This mirrors the driver's per-check isolation. The library has
            // no Debug facility, so the exception is intentionally swallowed.
            try
            {
                TypeCheckerVisitor.Run(program, resolver, resolver.Cache);
                foreach (var te in program.GetAllTypeErrors())
                    sink.Report(new CompileDiagnostic(
                        DiagnosticCode.TypeError, DiagnosticSeverity.Error, te.Node.SourceSpan, te.Message));
                foreach (var tw in program.GetAllTypeWarnings())
                    sink.Report(new CompileDiagnostic(
                        DiagnosticCode.TypeWarning, DiagnosticSeverity.Warning, tw.Node.SourceSpan, tw.Message));
            }
            catch (Exception)
            {
                // Type-checker fault: keep already-collected diagnostics; fall through to
                // the driver so AST/semantic checks still run.
            }
        }

        // 3. Single dispatch traversal for AST/semantic checks.
        var active = CreateChecks()
            .Where(c => c.Requirement != CheckRequirement.Required || resolver != null)
            .ToList();
        if (active.Count > 0)
        {
            var driver = new CompileCheckDriver(active, sink);
            driver.Context = new CompileCheckContext(program, resolver, context.ExpectedClassName, driver, context.SelfMetadata);
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
