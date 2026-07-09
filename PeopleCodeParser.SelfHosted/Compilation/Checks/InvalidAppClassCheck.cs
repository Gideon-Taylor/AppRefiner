using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports application class type references that do not resolve to an existing class.
/// Ported from AppRefiner's InvalidAppClass styler, re-expressed against
/// <see cref="PeopleCodeTypeInfo.Contracts.ITypeMetadataResolver"/>: "class exists" =
/// <c>GetTypeMetadata(path) != null</c>.
///
/// Requirement is <see cref="CheckRequirement.Required"/>: without a live (DB-backed)
/// resolver every path would look missing. <see cref="CompileChecker"/> also treats
/// <c>NullTypeMetadataResolver</c> as no resolver for the same reason.
///
/// Divergences from the styler:
/// - The DB-backed resolver returns null both when the class does not exist (the old
///   <c>CheckAppClassExists == false</c>) AND when the class exists but its source is
///   empty or fails to parse — so an existing-but-unbuildable class may be flagged.
///   Acceptable/rare; the not-fully-qualified guard below still skips malformed paths.
/// - No per-check validity cache: the resolver's own TypeCache covers repeat lookups.
/// - No explicit VisitAppClass → BaseType bridge: the driver dispatches OnNode for
///   every AppClassTypeNode, including a class declaration's BaseType.
/// </summary>
public sealed class InvalidAppClassCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.Required;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not AppClassTypeNode appClassType)
            return;

        if (ctx.Resolver == null)
            return;

        // Prefer the inferred type name when type inference has run (for object creation,
        // the inferred type lives on the parent ObjectCreationNode); otherwise fall back
        // to the qualified name as written in source.
        AstNode nodeForTypeInfo = appClassType.Parent is ObjectCreationNode creation
            ? creation
            : appClassType;

        string appClassPath = nodeForTypeInfo.HasInferredType()
            ? nodeForTypeInfo.GetInferredType()!.Name
            : appClassType.QualifiedName;

        // Not a fully qualified app class path — skip validation.
        if (!appClassPath.Contains(':'))
            return;

        if (ctx.Resolver.GetTypeMetadata(appClassPath) == null)
        {
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.InvalidAppClass,
                DiagnosticSeverity.Error,
                appClassType.SourceSpan,
                $"Application class '{appClassPath}' does not exist in the database."));
        }
    }
}
