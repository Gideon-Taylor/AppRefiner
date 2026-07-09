using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports duplicate member names within a single application class or interface header
/// (methods, properties, instance variables, constants). Case-insensitive; same name is
/// illegal regardless of member kind. Pure AST — no type inference or database.
/// </summary>
public sealed class DuplicateMemberCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not AppClassNode appClass)
            return;

        // First occurrence of each name (case-insensitive). Subsequent hits are errors.
        var first = new Dictionary<string, (string Kind, SourceSpan Span)>(StringComparer.OrdinalIgnoreCase);

        void Consider(string name, string kind, SourceSpan span)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (first.TryGetValue(name, out var prior))
            {
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.DuplicateMember,
                    DiagnosticSeverity.Error,
                    span,
                    $"Duplicate member '{name}': {kind} conflicts with earlier {prior.Kind}."));
                return;
            }

            first[name] = (kind, span);
        }

        foreach (var method in appClass.Methods)
            Consider(method.Name, "method", method.NameToken.SourceSpan);

        foreach (var property in appClass.Properties)
            Consider(property.Name, "property", property.NameToken.SourceSpan);

        foreach (var instance in appClass.InstanceVariables)
        {
            if (instance.NameInfos.Count > 0)
            {
                foreach (var nameInfo in instance.NameInfos)
                    Consider(nameInfo.Name, "instance variable", nameInfo.SourceSpan);
            }
            else
            {
                // Fallback if NameInfos was not populated.
                foreach (var name in instance.AllNames)
                    Consider(name, "instance variable", instance.NameToken.SourceSpan);
            }
        }

        foreach (var constant in appClass.Constants)
            Consider(constant.Name, "constant", constant.NameToken.SourceSpan);
    }
}
