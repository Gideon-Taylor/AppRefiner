namespace PeopleCodeTypeInfo.Validation;

/// <summary>
/// Kinds of type warnings that can be issued during validation.
/// </summary>
public enum TypeWarningKind
{
    /// <summary>
    /// Implicit narrowing conversion from 'any' or 'object' to a specific AppClass type.
    /// This can fail at runtime if the actual value is not of the expected AppClass type.
    /// </summary>
    ImplicitNarrowingToAppClass
}

/// <summary>
/// Represents a type validation warning with structured metadata.
/// Allows calling applications to handle warnings programmatically.
/// </summary>
public class TypeWarning
{
    /// <summary>
    /// The kind of warning being reported.
    /// </summary>
    public TypeWarningKind Kind { get; set; }

    /// <summary>
    /// The zero-based index of the argument that triggered the warning.
    /// </summary>
    public int ArgumentIndex { get; set; }

    /// <summary>
    /// The expected type (e.g., "AUC_TREE:AbstractVisitor").
    /// </summary>
    public string ExpectedType { get; set; } = string.Empty;

    /// <summary>
    /// The actual type that was found (e.g., "any", "object").
    /// </summary>
    public string FoundType { get; set; } = string.Empty;

    /// <summary>
    /// The name of the function being called.
    /// </summary>
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets a default warning message for this warning.
    /// Applications can use this or generate their own custom messages.
    /// </summary>
    public string GetDefaultMessage()
    {
        return Kind switch
        {
            TypeWarningKind.ImplicitNarrowingToAppClass =>
                $"Argument {ArgumentIndex + 1}: Implicit narrowing from '{FoundType}' to '{ExpectedType}' " +
                $"can cause runtime errors. Consider explicit cast: cast({ExpectedType}, <value>)",

            _ => $"Unknown warning at argument {ArgumentIndex + 1}"
        };
    }

    public override string ToString() => GetDefaultMessage();
}
