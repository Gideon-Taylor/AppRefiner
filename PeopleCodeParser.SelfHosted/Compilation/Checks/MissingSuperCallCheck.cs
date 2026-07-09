using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// When a subclass defines its own constructor, the compiler no longer emits the
/// auto-generated constructor that would set <c>%Super</c>. The authored constructor
/// must therefore assign <c>%Super</c> somewhere in its body (typically
/// <c>%Super = create BaseClass(...);</c>).
///
/// Complements <see cref="MissingConstructorCheck"/> (which requires a constructor to
/// exist when the base has a parameterized constructor). This check applies whenever
/// a constructor is present and the class has a base type — independent of base arity.
///
/// Pure AST — presence of a <c>%Super = …</c> assignment anywhere in the body is enough
/// (not path-sensitive, not first-statement-only).
/// </summary>
public sealed class MissingSuperCallCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not AppClassNode appClass)
            return;

        if (appClass.BaseType == null)
            return;

        foreach (var method in appClass.Methods)
        {
            if (!IsConstructor(method, appClass.Name))
                continue;

            var body = method.Body ?? method.Implementation?.Body;
            if (body == null)
                continue;

            if (BodySetsSuper(body))
                continue;

            sink.Report(new CompileDiagnostic(
                DiagnosticCode.MissingSuperCall,
                DiagnosticSeverity.Error,
                method.NameToken.SourceSpan,
                $"Constructor for '{appClass.Name}' must set %Super to initialize the base class."));
        }
    }

    private static bool IsConstructor(MethodNode method, string className) =>
        method.IsConstructor ||
        string.Equals(method.Name, className, StringComparison.OrdinalIgnoreCase);

    private static bool BodySetsSuper(BlockNode body)
    {
        foreach (var assignment in body.FindDescendants<AssignmentNode>())
        {
            if (assignment.Target is IdentifierNode id &&
                (id.IdentifierType == IdentifierType.Super ||
                 id.Name.Equals("%Super", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Also consider assignments that are direct children of expression statements
        // when FindDescendants already covers them via the block tree.
        return false;
    }
}
