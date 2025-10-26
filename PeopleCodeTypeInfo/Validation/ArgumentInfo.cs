using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Validation;

/// <summary>
/// Represents an argument passed to a function, including both its type and whether it's a variable reference.
/// This is necessary for validating parameters that require variable arguments (marked with &amp; prefix).
/// </summary>
public class ArgumentInfo
{
    /// <summary>
    /// The type of the argument
    /// </summary>
    public TypeInfo Type { get; }

    /// <summary>
    /// True if this argument is a variable reference (can be assigned to).
    /// False for literals, expressions, function calls, and other non-variable values.
    /// </summary>
    public bool IsVariable { get; }

    /// <summary>
    /// Create an ArgumentInfo with explicit type and variable flag
    /// </summary>
    public ArgumentInfo(TypeInfo type, bool isVariable)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsVariable = isVariable;
    }

    /// <summary>
    /// Create an ArgumentInfo for a variable reference
    /// </summary>
    public static ArgumentInfo Variable(TypeInfo type) => new ArgumentInfo(type, true);

    /// <summary>
    /// Create an ArgumentInfo for a non-variable (literal, expression, function call, etc.)
    /// </summary>
    public static ArgumentInfo NonVariable(TypeInfo type) => new ArgumentInfo(type, false);

    public override string ToString()
    {
        var varPrefix = IsVariable ? "&" : "";
        return $"{varPrefix}{Type.Name}";
    }
}
