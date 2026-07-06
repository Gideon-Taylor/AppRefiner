namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// Receives diagnostics produced by compile checks during a traversal.
/// </summary>
public interface IDiagnosticSink
{
    void Report(CompileDiagnostic diagnostic);
}
