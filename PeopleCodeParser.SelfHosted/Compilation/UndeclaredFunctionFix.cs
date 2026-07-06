namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// FixContext payload for <see cref="DiagnosticCode.UndeclaredFunction"/> when the
/// function IS implemented in this program but only below the call site (PeopleCode is
/// single-pass, so forward references are compile errors). The fix is static: move the
/// implementation above its first use. <paramref name="MoveDescription"/> is computed
/// at check time (it names the calling function when the call site is inside one) so
/// the UI layer needs no AST access to build the quick-fix label.
/// </summary>
public sealed record UndeclaredFunctionForwardRefFix(string FunctionName, string ImplName, string MoveDescription);

/// <summary>
/// FixContext payload for <see cref="DiagnosticCode.UndeclaredFunction"/> when the
/// function is neither declared, implemented, nor a builtin. The fix is deferred: at
/// Ctrl+. time the UI layer queries its function cache / database for matching
/// functions to declare.
/// </summary>
public sealed record UndeclaredFunctionUnknownFix(string FunctionName);
