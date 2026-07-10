using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Validates that buffer field references name a field that exists on the record.
///
/// Covers:
/// <list type="bullet">
/// <item>Bare identifiers in record PeopleCode (default record context), e.g. <c>ACCTLOCK</c>
/// when editing <c>WEBLIB_TS_TEST.ISCRIPT1</c> FieldChange — inferred as a buffer field
/// on the default record even before App Designer expands them to <c>REC.FIELD</c>.</item>
/// <item>Direct buffer access <c>REC.FIELD</c> (<see cref="RecordTypeInfo.DirectRecordAccess"/>).</item>
/// <item>Named record instance field access <c>&amp;r.FIELD</c> / <c>Record.REC.FIELD</c>
/// when the record name is known (optional consistency with the compiler after save).</item>
/// </list>
///
/// Skips when the resolver cannot answer (<c>RecordHasField</c> returns null) so missing
/// DB connectivity never false-positives.
/// </summary>
public sealed class UnknownRecordFieldCheck : CompileCheckBase
{
    /// <summary>
    /// Needs a resolver that can answer record field membership (DB-backed in production).
    /// </summary>
    public override CheckRequirement Requirement => CheckRequirement.Optional;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (ctx.Resolver == null)
            return;

        switch (node)
        {
            case IdentifierNode id:
                CheckBareBufferField(id, ctx, sink);
                break;
            case MemberAccessNode m when !m.IsDynamic:
                CheckRecordMemberField(m, ctx, sink);
                break;
        }
    }

    /// <summary>
    /// Bare NAME in default-record PeopleCode → field on default record.
    /// NAME in NAME.x is a record name (not a field) and is skipped.
    /// </summary>
    private static void CheckBareBufferField(IdentifierNode id, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (string.IsNullOrEmpty(ctx.DefaultRecordName))
            return;

        // Only bare generic names (not &var, %sys, Super, ...)
        if (id.IdentifierType != IdentifierType.Generic)
            return;

        if (id.Name.StartsWith('&') || id.Name.StartsWith('%'))
            return;

        if (ReferenceTypeInfo.IsSpecialReferenceKeyword(id.Name))
            return;

        // NAME.something → NAME is a record, not a field on the default record
        if (id.Parent is MemberAccessNode ma && ReferenceEquals(ma.Target, id))
            return;

        // Declared variables / constants with this bare name should not occur for Generic
        // identifiers, but if the registry somehow has one, leave it alone.
        var scope = ctx.ScopeData.GetCurrentScope();
        if (ctx.ScopeData.GetVariablesInScope(scope)
            .Any(v => v.Name.Equals(id.Name, StringComparison.OrdinalIgnoreCase)
                   || v.Name.Equals("&" + id.Name, StringComparison.OrdinalIgnoreCase)))
            return;

        ReportIfMissing(ctx, sink, ctx.DefaultRecordName, id.Name, id.SourceSpan);
    }

    /// <summary>
    /// REC.FIELD / &amp;r.FIELD when target is a named RecordTypeInfo and the member
    /// is not a real Record property or method call.
    /// </summary>
    private static void CheckRecordMemberField(MemberAccessNode m, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        var targetType = m.Target.GetInferredType();
        if (targetType is not RecordTypeInfo recordType || string.IsNullOrEmpty(recordType.RecordName))
            return;

        // Method call on record — validated by InvalidMemberAccessCheck
        if (m.Parent is FunctionCallNode fc && ReferenceEquals(fc.Function, m))
            return;

        // Real Record object properties (IsChanged, …) are not fields
        if (!recordType.DirectRecordAccess)
        {
            var realProp = PeopleCodeTypeDatabase.GetProperty("record", m.MemberName);
            if (realProp != null)
                return;
        }

        ReportIfMissing(ctx, sink, recordType.RecordName, m.MemberName, m.MemberNameSpan);
    }

    private static void ReportIfMissing(
        CompileCheckContext ctx,
        IDiagnosticSink sink,
        string recordName,
        string fieldName,
        SourceSpan span)
    {
        var hasField = ctx.Resolver!.RecordHasField(recordName, fieldName);
        if (hasField != false)
            return; // true or null (unknown) — no diagnostic

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.UnknownRecordField,
            DiagnosticSeverity.Error,
            span,
            $"Field '{fieldName}' is not defined on record '{recordName}'"));
    }
}
