using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;

namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// Read-only context handed to every check. ScopeData is the driver itself, exposing
/// the completed VariableRegistry and scope queries once traversal has finished (safe
/// to query from Finish; during OnNode the registry is still being populated).
/// </summary>
public sealed class CompileCheckContext
{
    public ProgramNode Program { get; }
    public ITypeMetadataResolver? Resolver { get; }
    public string? ExpectedClassName { get; }
    public ScopedAstVisitor<object> ScopeData { get; }

    /// <summary>
    /// Metadata for the class currently open in the editor, built from the LIVE in-editor
    /// <see cref="Program"/> rather than the DB-backed resolver. Lets checks recognize
    /// members that have been added but not yet saved (the resolver only sees last-saved
    /// source). Null for non-class programs or when no live metadata was supplied.
    /// Mirrors the "self" special-case in TypeInferenceVisitor's inheritance walk.
    /// </summary>
    public TypeMetadata? SelfMetadata { get; }

    /// <summary>
    /// Record definition name for record-field PeopleCode (e.g. WEBLIB_TS_TEST when editing
    /// WEBLIB_TS_TEST.ISCRIPT1 FieldChange). Bare identifiers are buffer fields on this
    /// record. Null outside record PeopleCode.
    /// </summary>
    public string? DefaultRecordName { get; }

    public CompileCheckContext(
        ProgramNode program,
        ITypeMetadataResolver? resolver,
        string? expectedClassName,
        ScopedAstVisitor<object> scopeData,
        TypeMetadata? selfMetadata = null,
        string? defaultRecordName = null)
    {
        Program = program;
        Resolver = resolver;
        ExpectedClassName = expectedClassName;
        ScopeData = scopeData;
        SelfMetadata = selfMetadata;
        DefaultRecordName = defaultRecordName;
    }
}
