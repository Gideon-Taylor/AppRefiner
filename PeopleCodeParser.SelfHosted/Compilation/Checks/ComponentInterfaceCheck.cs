using PeopleCodeParser.SelfHosted.ComponentInterfaces;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Checks uses of a component interface against its definition: assignments to read-only
/// properties, calls to standard methods the CI doesn't enable, and CompIntfc.X references to a
/// CI that doesn't exist. Unknown members are InvalidMemberAccessCheck's.
///
/// Nothing is reported unless the definition lookup answers: a named CI or known item needs a
/// Found definition, and a reference is only "doesn't exist" when the lookup says Missing
/// (never Unknown, e.g. without a database).
/// </summary>
public sealed class ComponentInterfaceCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.Optional;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (ctx.Resolver == null)
            return;

        switch (node)
        {
            case AssignmentNode { Target: MemberAccessNode { IsDynamic: false } target }:
                CheckReadOnlyAssignment(target, ctx, sink);
                break;
            case FunctionCallNode { Function: MemberAccessNode { IsDynamic: false } method }:
                CheckStandardMethod(method, ctx, sink);
                break;
        }

        CheckReference(node, ctx, sink);
    }

    private static void CheckReadOnlyAssignment(MemberAccessNode target, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (target.Target.GetInferredType() is not { } targetType
            || CompIntfcTarget.Find(targetType, ctx.Resolver) is not { } ci
            || ci.FindProperty(target.MemberName) is not { IsReadOnly: true } property)
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.ReadOnlyCompIntfcProperty,
            DiagnosticSeverity.Error,
            target.MemberNameSpan,
            $"'{property.Name}' is read-only on {ci.DisplayName}"));
    }

    private static void CheckStandardMethod(MemberAccessNode method, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (method.Target.GetInferredType() is not { PeopleCodeType: PeopleCodeType.ComponentInterface } targetType
            || CompIntfcTarget.Find(targetType, ctx.Resolver) is not { } ci)
            return;

        // A custom method of the same name is the CI's own
        if (ci.Definition.CustomMethods.ContainsKey(method.MemberName)
            || ci.Definition.IsStandardMethodEnabled(method.MemberName)
            || !Enum.TryParse<CompIntfcStandardMethods>(method.MemberName, ignoreCase: true, out var standard))
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.DisabledCompIntfcMethod,
            DiagnosticSeverity.Error,
            method.MemberNameSpan,
            $"{ci.DisplayName} doesn't enable {standard}()"));
    }

    /// <summary>
    /// CompIntfc.X (or @("CompIntfc.X")) wherever it appears. Variable uses and parentheses can
    /// carry the same reference type on; only the reference itself is reported.
    /// </summary>
    private static void CheckReference(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is IdentifierNode or ParenthesizedExpressionNode)
            return;

        if (node.GetInferredType() is not ReferenceTypeInfo { ReferenceCategory: PeopleCodeType.CompIntfc } reference
            || string.IsNullOrEmpty(reference.ReferencedName)
            || string.Equals(reference.ReferencedName, "Dynamic Reference", StringComparison.OrdinalIgnoreCase))
            return;

        if (ctx.Resolver!.GetComponentInterface(reference.ReferencedName).Status != CompIntfcLookupStatus.Missing)
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.UnknownComponentInterface,
            DiagnosticSeverity.Error,
            node.SourceSpan,
            $"Component interface '{reference.ReferencedName}' doesn't exist"));
    }
}
