namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// Stable, machine-readable identity for each compile diagnostic. Doubles as the
/// quick-fix routing key on the AppRefiner side and the MCP payload key later.
/// Do not renumber or repurpose existing members.
/// </summary>
public enum DiagnosticCode
{
    // Explicit values are load-bearing: these codes are a stable wire/routing contract
    // (quick-fix map keys, future MCP payload keys). Assigning them pins each member so a
    // future mid-list insertion cannot silently renumber the rest. Append new members with
    // the next unused value; never renumber or repurpose existing ones.
    SyntaxError = 0,
    TypeError = 1,
    TypeWarning = 2,
    MissingSemicolon = 3,
    RedeclaredVariable = 4,
    UndefinedVariable = 5,
    ClassNameMismatch = 6,
    InvalidAppClass = 7,
    UnimportedClass = 8,
    AmbiguousClassReference = 9,
    InvalidMemberAccess = 10,
    MissingConstructor = 11,
    MissingMethodImplementation = 12,
    UnimplementedAbstractMember = 13,
    UndeclaredFunction = 14,
    NotAllPathsReturn = 15,
    InvalidBreakContinue = 16,
    MissingReturnValue = 17,
}
