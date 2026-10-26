using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Visitors.Models;

/// <summary>
/// The type a declared variable holds right after its declaration, kept on the declaration so a
/// hover on the declared name can show it (the name itself isn't an expression node).
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
