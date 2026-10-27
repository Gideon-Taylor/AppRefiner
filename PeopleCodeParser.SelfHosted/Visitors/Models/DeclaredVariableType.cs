using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Visitors.Models;

/// <summary>
/// The type a variable holds right after its declaration or an assignment to it, kept on that
/// node so a hover on the declared name (not an expression node) or on the assignment's target
/// (whose own stamp is the type before the assignment) can show it.
/// </summary>
/// <param name="Name">The declared variable name</param>
/// <param name="NameSpan">Where the name appears in the declaration</param>
/// <param name="Type">The variable's type after the declaration and any initial value</param>
/// <param name="Source">Where a refined type came from, if it was refined</param>
/// <param name="Candidates">The API types an undecided ApiObject variable's uses still allow</param>
public sealed record DeclaredVariableType(
    string Name,
    SourceSpan NameSpan,
    TypeInfo Type,
    RefinementSource? Source,
    IReadOnlyList<string>? Candidates);
