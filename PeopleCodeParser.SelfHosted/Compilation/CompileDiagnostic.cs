namespace PeopleCodeParser.SelfHosted.Compilation;

public enum DiagnosticSeverity { Error, Warning }

/// <summary>
/// One compile finding. FixContext is an opaque payload the check attaches for the
/// UI layer's quick-fix mapping; the library never interprets it.
/// <see cref="IsSecondary"/> marks related "path locus" findings (e.g. NotAllPathsReturn
/// incomplete branch) so the UI can style them differently from the primary signature hit.
/// </summary>
public sealed record CompileDiagnostic(
    DiagnosticCode Code,
    DiagnosticSeverity Severity,
    SourceSpan Span,
    string Message,
    object? FixContext = null,
    bool IsSecondary = false);
