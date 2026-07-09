using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports bare <c>Return;</c> (no expression) inside a function, method, or property
/// getter that declares a return type. PeopleCode requires a value on those paths.
/// Pure AST — no type inference or database.
/// </summary>
public sealed class MissingReturnValueCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not ReturnStatementNode ret || ret.Value != null)
            return;

        if (!EnclosingRoutineRequiresReturnValue(ret, out var kind, out var name))
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.MissingReturnValue,
            DiagnosticSeverity.Error,
            ret.SourceSpan,
            $"Return in {kind} '{name}' must include a value."));
    }

    private static bool EnclosingRoutineRequiresReturnValue(
        AstNode node,
        out string kind,
        out string name)
    {
        kind = "";
        name = "";

        var function = node.FindAncestor<FunctionNode>();
        if (function != null)
        {
            if (function.ReturnType == null)
                return false;
            kind = "function";
            name = function.Name;
            return true;
        }

        var methodImpl = node.FindAncestor<MethodImplNode>();
        if (methodImpl != null)
        {
            var returnType = methodImpl.Declaration?.ReturnType ?? methodImpl.ReturnTypeAnnotation;
            if (returnType == null)
                return false;
            kind = "method";
            name = methodImpl.Name;
            return true;
        }

        var method = node.FindAncestor<MethodNode>();
        if (method?.ReturnType != null)
        {
            kind = "method";
            name = method.Name;
            return true;
        }

        var propertyImpl = node.FindAncestor<PropertyImplNode>();
        if (propertyImpl is { IsGetter: true })
        {
            var prop = propertyImpl.Parent as PropertyNode
                ?? propertyImpl.FindAncestor<PropertyNode>();
            if (prop == null)
                return false;
            kind = "property getter";
            name = propertyImpl.Name;
            return true;
        }

        return false;
    }
}
