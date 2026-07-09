using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports <c>Return &lt;expr&gt;;</c> inside a routine that has no declared return type
/// (procedure function/method) or inside a property setter. Mirror of
/// <see cref="MissingReturnValueCheck"/>. Pure AST — no type inference or database.
/// </summary>
public sealed class UnexpectedReturnValueCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not ReturnStatementNode ret || ret.Value == null)
            return;

        if (!EnclosingRoutineForbidsReturnValue(ret, out var kind, out var name))
            return;

        var span = ret.Value.SourceSpan;
        sink.Report(new CompileDiagnostic(
            DiagnosticCode.UnexpectedReturnValue,
            DiagnosticSeverity.Error,
            span,
            $"Return value is not allowed here; {kind} '{name}' does not declare a return type."));
    }

    private static bool EnclosingRoutineForbidsReturnValue(
        AstNode node,
        out string kind,
        out string name)
    {
        kind = "";
        name = "";

        var function = node.FindAncestor<FunctionNode>();
        if (function != null)
        {
            if (function.ReturnType != null)
                return false;
            kind = "function";
            name = function.Name;
            return true;
        }

        var methodImpl = node.FindAncestor<MethodImplNode>();
        if (methodImpl != null)
        {
            var returnType = methodImpl.Declaration?.ReturnType ?? methodImpl.ReturnTypeAnnotation;
            if (returnType != null)
                return false;
            kind = "method";
            name = methodImpl.Name;
            return true;
        }

        var method = node.FindAncestor<MethodNode>();
        if (method != null)
        {
            if (method.ReturnType != null)
                return false;
            kind = "method";
            name = method.Name;
            return true;
        }

        var propertyImpl = node.FindAncestor<PropertyImplNode>();
        if (propertyImpl is { IsSetter: true })
        {
            kind = "property setter";
            name = propertyImpl.Name;
            return true;
        }

        // Property getters always return a value of the property type — never forbid here.
        return false;
    }
}
