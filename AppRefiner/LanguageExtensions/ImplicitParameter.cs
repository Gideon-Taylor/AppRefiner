using PeopleCodeTypeInfo.Types;
using System;

namespace AppRefiner.LanguageExtensions;

/// <summary>
/// Describes an implicit parameter introduced by a language extension.
/// Implicit parameters are phantom identifiers that exist only during the syntactic sugar phase
/// and are eliminated during transformation (e.g., &item in &array.Map(&item.Prop)).
/// </summary>
/// <remarks>
/// These parameters are NOT registered in the variable registry - they only receive type annotations
/// on their AST nodes to support autocomplete and type checking before transformation.
/// </remarks>
public class ImplicitParameter
{
    /// <summary>
    /// The name of the implicit parameter (e.g., "&item").
    /// Should include the & prefix for consistency with PeopleCode variable naming.
    /// </summary>
    public string ParameterName { get; set; }

    /// <summary>
    /// Function that resolves the type of this implicit parameter based on the target type.
    /// For example, for Map on "array of Student", this would return "Student".
    /// </summary>
    /// <remarks>
    /// Input: The type of the object on which the extension method is being called.
    /// Output: The type that should be assigned to the implicit parameter.
    /// </remarks>
    public Func<TypeInfo, TypeInfo> TypeResolver { get; set; }

    public ImplicitParameter(string parameterName, Func<TypeInfo, TypeInfo> typeResolver)
    {
        ParameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
        TypeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
    }
}
