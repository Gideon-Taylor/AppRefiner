using AppRefiner.Database;
using DiffPlex.Model;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace AppRefiner.Stylers;

/// <summary>
/// Highlights type errors in PeopleCode by running type inference and type checking.
/// </summary>
/// <remarks>
/// This styler:
/// 1. Extracts type metadata from the current program
/// 2. Runs TypeInferenceVisitor to infer types throughout the AST
/// 3. Runs TypeCheckerVisitor to validate type compatibility
/// 4. Highlights all nodes with type errors using red squiggly underlines
/// </remarks>
public class TypeErrorStyler : BaseStyler
{
    private const uint ERROR_COLOR = 0x0000FFA0; // Harsh red color with high alpha (matches UndefinedVariables)
    private const uint WARNING_COLOR = 0x32FF32FF; // Light green 
    public TypeErrorStyler()
    {
        // Enable by default
        Active = true;
    }

    public override string Description => "Type errors";

    /// <summary>
    /// Type checking requires database access for resolving custom types
    /// </summary>
    public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

    public override void VisitProgram(ProgramNode node)
    {
        Reset();

        // Get the AppDesigner process from the editor
        var appDesignerProcess = Editor?.AppDesignerProcess;
        if (appDesignerProcess == null)
        {
            Debug.Log("TypeErrorStyler: No AppDesigner process available");
            return;
        }

        // Get type system infrastructure
        var typeResolver = appDesignerProcess.TypeResolver;

        if (typeResolver == null)
        {
            Debug.Log("TypeErrorStyler: TypeResolver is null (database not connected?)");
            return;
        }

        try
        {
            // Type inference is now run by StylerManager BEFORE stylers execute
            // We only need to run type checking here
            TypeCheckerVisitor.Run(node, typeResolver, typeResolver.Cache);

            // Collect all type errors from the AST
            var typeErrors = node.GetAllTypeErrors();
            // Add squiggle indicators for each type error
            foreach (var error in typeErrors)
            {
                // "Return/Expression values must be assigned to a variable" errors are the
                // only ones recorded on an ExpressionStatementNode — offer to capture the
                // value in a new local. The declared type is rendered here because the
                // quick fix's fresh re-parse won't re-run type inference.
                List<QuickFixEntry>? quickFixes = null;
                if (error.Node is ExpressionStatementNode stmt)
                {
                    quickFixes = new List<QuickFixEntry>
                    {
                        new(typeof(Refactors.QuickFixes.AssignToNewVariable),
                            "Assign result to a new local variable",
                            new Refactors.QuickFixes.AssignToVariableContext(
                                stmt.SourceSpan.Start.ByteIndex,
                                RenderDeclaredType(stmt.Expression.GetInferredType())))
                    };
                }

                AddIndicator(
                    error.Node.SourceSpan,
                    IndicatorType.SQUIGGLE,
                    ERROR_COLOR,
                    error.Message,
                    quickFixes
                );
            }

            var typeWarnings = node.GetAllTypeWarnings();
            foreach (var warning in typeWarnings)
            {
                AddIndicator(
                    warning.Node.SourceSpan,
                    IndicatorType.SQUIGGLE,
                    WARNING_COLOR,
                    warning.Message
                );
            }

            if (typeErrors.Any())
            {
                Debug.Log($"TypeErrorStyler: Found {typeErrors.Count()} type errors and {typeWarnings.Count()} warnings");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, "TypeErrorStyler: Error during type checking");
        }
    }

    /// <summary>
    /// Renders an inferred type as a PeopleCode declared-type token. TypeInfo.Name is
    /// already source-legal for primitives, builtin objects, app classes (PKG:CLS) and
    /// arrays ("array of string"); anything that doesn't look like a legal type token
    /// (e.g. "Dynamic Reference") conservatively declares as "any".
    /// </summary>
    private static string RenderDeclaredType(PeopleCodeTypeInfo.Types.TypeInfo? typeInfo)
        => Services.TypeInferenceRunner.RenderDeclaredType(typeInfo);
}
