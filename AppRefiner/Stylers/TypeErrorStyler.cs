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

        // Determine the qualified name for the current program
        string qualifiedName = DetermineQualifiedName(node);

        try
        {
            // Extract metadata from the program
            var programMetadata = TypeMetadataBuilder.ExtractMetadata(node, qualifiedName);

            string? defaultRecord = null;
            string? defaultField = null;
            if (Editor.Caption.EndsWith("(Record PeopleCode)"))
            {
                var parts = qualifiedName.Split('.');
                defaultRecord = parts[0];
                defaultField = parts[1];
            }

            // Run type inference
            TypeInferenceVisitor.Run(node, programMetadata, typeResolver, defaultRecord, defaultField);

            // Run type checking
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
            foreach ( var warning in typeWarnings)
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
                Debug.Log($"TypeErrorStyler: Found {typeErrors.Count()} type errors and {typeWarnings.Count()} warnings in '{qualifiedName}'");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, "TypeErrorStyler: Error during type checking");
        }
    }

    /// <summary>
    /// Determines the qualified name for the current program.
    /// For app classes, extracts from editor caption or AST.
    /// For function libraries and other programs, uses a generic name.
    /// </summary>
    private string DetermineQualifiedName(ProgramNode node)
    {
        // Try to extract from AST structure first
        if (node.AppClass != null || node.Interface != null)
        {
            // For app classes, try to build qualified name from imports or use simple name
            var className = node.AppClass != null ? node.AppClass.Name : node.Interface!.Name;

            if (Editor?.Caption != null && !string.IsNullOrWhiteSpace(Editor.Caption))
            {
                // Parse caption to get program identifier
                var openTarget = OpenTargetBuilder.CreateFromCaption(Editor.Caption);
                if (openTarget != null)
                {
                    var methodIndex = Array.IndexOf(openTarget.ObjectIDs, PSCLASSID.METHOD);
                    openTarget.ObjectIDs[methodIndex] = PSCLASSID.NONE;
                    openTarget.ObjectValues[methodIndex] = null;
                    return openTarget.Path;
                }
                else
                {
                    /* probably never what you want but we have to return something? */
                    return className;
                }
            } else
            {
                /* probably never what you want but we have to return something? */
                return className;
            }
        }
        else
        {
            // For function libraries or other programs, use a generic name
            // Try to extract from editor caption if available
            if (Editor?.Caption != null && !string.IsNullOrWhiteSpace(Editor.Caption))
            {
                // Parse caption to get program identifier
                var openTarget = OpenTargetBuilder.CreateFromCaption(Editor.Caption);
                if (openTarget != null)
                {
                    return string.Join(".", openTarget.ObjectValues);
                }
            }

            // Fallback to generic name
            return "Program";
        }
    }
}
