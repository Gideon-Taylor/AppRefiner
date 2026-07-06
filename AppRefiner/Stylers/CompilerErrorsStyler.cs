using AppRefiner.Database;
using AppRefiner.Services;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Inference;

namespace AppRefiner.Stylers;

/// <summary>
/// Consolidated styler that surfaces every "won't compile" diagnostic from the shared
/// <see cref="CompileChecker"/>. Replaces the individual compile-error stylers
/// (SyntaxErrors, TypeErrorStyler).
/// </summary>
public class CompilerErrorsStyler : BaseStyler
{
    private const uint ERROR_COLOR = 0x0000FFA0;   // red squiggle (matches old stylers)
    private const uint WARNING_COLOR = 0x32FF32FF; // light green (matches TypeErrorStyler)

    public CompilerErrorsStyler()
    {
        // Enable by default: compile checking is on out of the box.
        Active = true;
    }

    public override string Description => "Compiler errors";

    /// <summary>
    /// Type checking is richer with a resolver, but syntax errors still surface without
    /// one, so the database connection is optional.
    /// </summary>
    public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        if (Editor == null) return;

        var resolver = Editor.AppDesignerProcess?.TypeResolver;
        var expectedClassName = Editor.ClassPath?.Split(':').LastOrDefault();

        // Build metadata for the class currently open in the editor from the LIVE program,
        // so the member-access check recognizes members added but not yet saved (the
        // resolver only sees last-saved DB source). The qualified name is derived exactly
        // as TypeInferenceRunner does, so it matches the QualifiedName inference stamps onto
        // %This — that name equality is what lets the check pick the live metadata for the
        // self level of the inheritance walk. Only class/interface programs have a self type.
        TypeMetadata? selfMetadata = null;
        if (node.AppClass != null)
        {
            var qualifiedName = TypeInferenceRunner.DetermineQualifiedName(node, Editor);
            selfMetadata = TypeMetadataBuilder.ExtractMetadata(node, qualifiedName);
        }

        var diagnostics = CompileChecker.Check(
            node,
            Editor.ParserErrors,
            resolver,
            new CompileCheckContextInput(expectedClassName, selfMetadata));

        // CompileChecker returns diagnostics carrying only spans (no AST nodes) and does
        // not attach the assign-to-var FixContext in Phase 1. Re-derive it here to
        // preserve TypeErrorStyler's behavior: index every ExpressionStatementNode by its
        // full span, and enrich TypeError diagnostics whose span matches a statement
        // exactly (both start and end byte index — a full-span match avoids over-offering
        // the fix to narrower sub-expression type errors).
        var statementsBySpan = new Dictionary<(int Start, int End), ExpressionStatementNode>();
        foreach (var stmt in node.FindDescendants<ExpressionStatementNode>())
        {
            statementsBySpan[(stmt.SourceSpan.Start.ByteIndex, stmt.SourceSpan.End.ByteIndex)] = stmt;
        }

        foreach (var diagnostic in diagnostics)
        {
            var d = diagnostic;

            if (d.Code == DiagnosticCode.TypeError
                && d.FixContext is null
                && statementsBySpan.TryGetValue(
                    (d.Span.Start.ByteIndex, d.Span.End.ByteIndex), out var stmt))
            {
                // The declared type is rendered here because the quick fix's fresh
                // re-parse won't re-run type inference.
                d = d with
                {
                    FixContext = new Refactors.QuickFixes.AssignToVariableContext(
                        stmt.SourceSpan.Start.ByteIndex,
                        TypeInferenceRunner.RenderDeclaredType(stmt.Expression?.GetInferredType()))
                };
            }

            var color = d.Severity == DiagnosticSeverity.Warning ? WARNING_COLOR : ERROR_COLOR;
            if (CompileDiagnosticQuickFixMap.HasDeferredResolver(d))
            {
                // Quick fixes for this diagnostic need a DB/cache query — resolve them
                // lazily at Ctrl+. time, handing the resolver the diagnostic's FixContext.
                AddIndicatorWithDeferredQuickFix(
                    d.Span,
                    IndicatorType.SQUIGGLE,
                    color,
                    d.Message,
                    CompileDiagnosticQuickFixMap.GetDeferredResolver(d),
                    d.FixContext);
            }
            else
            {
                var quickFixes = CompileDiagnosticQuickFixMap.GetQuickFixes(d);
                AddIndicator(d.Span, IndicatorType.SQUIGGLE, color, d.Message, quickFixes);
            }
        }

        // Do NOT call base.VisitProgram — CompileChecker already traversed. This styler
        // is a thin adapter; a second traversal would be wasted work.
    }
}
