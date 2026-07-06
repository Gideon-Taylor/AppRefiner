namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// FixContext payload for a <see cref="DiagnosticCode.UndefinedVariable"/> diagnostic that
/// specifically flags an undefined for-loop iterator. Its presence tells the AppRefiner
/// quick-fix map to offer the "Declare iterator" fix (matching the surviving non-class
/// UndefinedVariables styler). Plain undefined-variable diagnostics carry a null FixContext.
/// </summary>
public sealed record UndefinedForLoopIteratorFix(string IteratorName);
