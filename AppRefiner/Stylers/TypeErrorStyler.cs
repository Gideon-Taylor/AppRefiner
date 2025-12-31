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
                AddIndicator(
                    error.Node.SourceSpan,
                    IndicatorType.SQUIGGLE,
                    ERROR_COLOR,
                    error.Message
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
}
