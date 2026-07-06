using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// Declares whether a check needs a database-backed type metadata resolver to run.
/// Library-local mirror of AppRefiner's DataManagerRequirement (the parser library
/// must not reference AppRefiner).
/// </summary>
public enum CheckRequirement { NotRequired, Optional, Required }

/// <summary>
/// One compile check. The driver calls OnNode for every AST node during a single
/// traversal, then Finish once after traversal completes (for whole-program or
/// whole-class analyses that need the full variable registry).
/// </summary>
public interface ICompileCheck
{
    CheckRequirement Requirement { get; }
    void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink);
    void Finish(CompileCheckContext ctx, IDiagnosticSink sink);
}

/// <summary>
/// Convenience base so checks override only the hook they use.
/// </summary>
public abstract class CompileCheckBase : ICompileCheck
{
    public virtual CheckRequirement Requirement => CheckRequirement.NotRequired;
    public virtual void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink) { }
    public virtual void Finish(CompileCheckContext ctx, IDiagnosticSink sink) { }
}
