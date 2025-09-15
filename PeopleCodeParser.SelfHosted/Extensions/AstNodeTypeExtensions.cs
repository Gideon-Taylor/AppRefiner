using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.TypeSystem;

namespace PeopleCodeParser.SelfHosted.Extensions;

/// <summary>
/// Constants for type system attributes stored in AstNode.Attributes dictionary
/// </summary>
public static class TypeSystemAttributes
{
    /// <summary>
    /// Key for storing the inferred TypeInfo on an AST node
    /// </summary>
    public const string INFERRED_TYPE = "TypeSystem.InferredType";

    /// <summary>
    /// Key for storing type errors associated with an AST node
    /// </summary>
    public const string TYPE_ERRORS = "TypeSystem.Errors";

    /// <summary>
    /// Key for storing type warnings associated with an AST node
    /// </summary>
    public const string TYPE_WARNINGS = "TypeSystem.Warnings";

    /// <summary>
    /// Key for storing additional type resolution context
    /// </summary>
    public const string RESOLUTION_CONTEXT = "TypeSystem.ResolutionContext";

    /// <summary>
    /// Key for storing variable binding information
    /// </summary>
    public const string VARIABLE_BINDING = "TypeSystem.VariableBinding";
}

/// <summary>
/// Extension methods for AstNode to support type system operations
/// </summary>
public static class AstNodeTypeExtensions
{
    #region Type Information

    /// <summary>
    /// Gets the inferred type for this AST node, or null if no type has been inferred
    /// </summary>
    public static TypeInfo? GetInferredType(this AstNode node)
    {
        if (node == null) return null;

        return node.Attributes.TryGetValue(TypeSystemAttributes.INFERRED_TYPE, out var type)
            ? type as TypeInfo
            : null;
    }

    /// <summary>
    /// Sets the inferred type for this AST node
    /// </summary>
    public static void SetInferredType(this AstNode node, TypeInfo typeInfo)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (typeInfo == null) throw new ArgumentNullException(nameof(typeInfo));

        node.Attributes[TypeSystemAttributes.INFERRED_TYPE] = typeInfo;
    }

    /// <summary>
    /// Checks if this AST node has type information attached
    /// </summary>
    public static bool HasInferredType(this AstNode node)
    {
        return node?.GetInferredType() != null;
    }

    /// <summary>
    /// Clears any type information from this AST node
    /// </summary>
    public static void ClearInferredType(this AstNode node)
    {
        if (node == null) return;

        node.Attributes.Remove(TypeSystemAttributes.INFERRED_TYPE);
    }

    #endregion

    #region Type Errors

    /// <summary>
    /// Gets all type errors associated with this AST node
    /// </summary>
    public static IEnumerable<TypeError> GetTypeErrors(this AstNode node)
    {
        if (node == null) return Enumerable.Empty<TypeError>();

        return node.Attributes.TryGetValue(TypeSystemAttributes.TYPE_ERRORS, out var errors)
            ? (errors as List<TypeError>) ?? Enumerable.Empty<TypeError>()
            : Enumerable.Empty<TypeError>();
    }

    /// <summary>
    /// Adds a type error to this AST node
    /// </summary>
    public static void AddTypeError(this AstNode node, TypeError error)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (error == null) throw new ArgumentNullException(nameof(error));

        if (!node.Attributes.TryGetValue(TypeSystemAttributes.TYPE_ERRORS, out var existingErrors))
        {
            existingErrors = new List<TypeError>();
            node.Attributes[TypeSystemAttributes.TYPE_ERRORS] = existingErrors;
        }

        if (existingErrors is List<TypeError> errorList)
        {
            errorList.Add(error);
        }
    }

    /// <summary>
    /// Adds a type error to this AST node with a simple message
    /// </summary>
    public static void AddTypeError(this AstNode node, string message, TypeErrorKind kind = TypeErrorKind.General,
                                   TypeInfo? expectedType = null, TypeInfo? actualType = null)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (string.IsNullOrEmpty(message)) throw new ArgumentNullException(nameof(message));

        var error = new TypeError(message, node.SourceSpan, kind, expectedType, actualType);
        node.AddTypeError(error);
    }

    /// <summary>
    /// Checks if this AST node has any type errors
    /// </summary>
    public static bool HasTypeErrors(this AstNode node)
    {
        return node?.GetTypeErrors().Any() == true;
    }

    /// <summary>
    /// Clears all type errors from this AST node
    /// </summary>
    public static void ClearTypeErrors(this AstNode node)
    {
        if (node == null) return;

        node.Attributes.Remove(TypeSystemAttributes.TYPE_ERRORS);
    }

    #endregion

    #region Type Warnings

    /// <summary>
    /// Gets all type warnings associated with this AST node
    /// </summary>
    public static IEnumerable<TypeWarning> GetTypeWarnings(this AstNode node)
    {
        if (node == null) return Enumerable.Empty<TypeWarning>();

        return node.Attributes.TryGetValue(TypeSystemAttributes.TYPE_WARNINGS, out var warnings)
            ? (warnings as List<TypeWarning>) ?? Enumerable.Empty<TypeWarning>()
            : Enumerable.Empty<TypeWarning>();
    }

    /// <summary>
    /// Adds a type warning to this AST node
    /// </summary>
    public static void AddTypeWarning(this AstNode node, TypeWarning warning)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (warning == null) throw new ArgumentNullException(nameof(warning));

        if (!node.Attributes.TryGetValue(TypeSystemAttributes.TYPE_WARNINGS, out var existingWarnings))
        {
            existingWarnings = new List<TypeWarning>();
            node.Attributes[TypeSystemAttributes.TYPE_WARNINGS] = existingWarnings;
        }

        if (existingWarnings is List<TypeWarning> warningList)
        {
            warningList.Add(warning);
        }
    }

    /// <summary>
    /// Adds a type warning to this AST node with a simple message
    /// </summary>
    public static void AddTypeWarning(this AstNode node, string message)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (string.IsNullOrEmpty(message)) throw new ArgumentNullException(nameof(message));

        var warning = new TypeWarning(message, node.SourceSpan);
        node.AddTypeWarning(warning);
    }

    /// <summary>
    /// Checks if this AST node has any type warnings
    /// </summary>
    public static bool HasTypeWarnings(this AstNode node)
    {
        return node?.GetTypeWarnings().Any() == true;
    }

    /// <summary>
    /// Clears all type warnings from this AST node
    /// </summary>
    public static void ClearTypeWarnings(this AstNode node)
    {
        if (node == null) return;

        node.Attributes.Remove(TypeSystemAttributes.TYPE_WARNINGS);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Clears all type system information from this AST node
    /// </summary>
    public static void ClearAllTypeInformation(this AstNode node)
    {
        if (node == null) return;

        node.ClearInferredType();
        node.ClearTypeErrors();
        node.ClearTypeWarnings();
        node.Attributes.Remove(TypeSystemAttributes.RESOLUTION_CONTEXT);
        node.Attributes.Remove(TypeSystemAttributes.VARIABLE_BINDING);
    }

    /// <summary>
    /// Sets a value in the resolution context dictionary for this node.
    /// </summary>
    public static void SetResolutionContextValue(this AstNode node, string key, object value)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

        if (!node.Attributes.TryGetValue(TypeSystemAttributes.RESOLUTION_CONTEXT, out var contextObj) ||
            contextObj is not Dictionary<string, object> context)
        {
            context = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            node.Attributes[TypeSystemAttributes.RESOLUTION_CONTEXT] = context;
        }

        context[key] = value;
    }

    /// <summary>
    /// Retrieves a value from the resolution context dictionary for this node.
    /// </summary>
    public static T? GetResolutionContextValue<T>(this AstNode node, string key) where T : class
    {
        if (node == null || string.IsNullOrEmpty(key)) return null;

        if (!node.Attributes.TryGetValue(TypeSystemAttributes.RESOLUTION_CONTEXT, out var contextObj) ||
            contextObj is not Dictionary<string, object> context)
        {
            return null;
        }

        return context.TryGetValue(key, out var value) ? value as T : null;
    }

    /// <summary>
    /// Gets a summary of type information for debugging/logging
    /// </summary>
    public static string GetTypeInformationSummary(this AstNode node)
    {
        if (node == null) return "null";

        var summary = new List<string>();

        var type = node.GetInferredType();
        if (type != null)
        {
            summary.Add($"Type: {type.Name}");
        }

        var errorCount = node.GetTypeErrors().Count();
        if (errorCount > 0)
        {
            summary.Add($"Errors: {errorCount}");
        }

        var warningCount = node.GetTypeWarnings().Count();
        if (warningCount > 0)
        {
            summary.Add($"Warnings: {warningCount}");
        }

        return summary.Count > 0 ? string.Join(", ", summary) : "No type information";
    }

    /// <summary>
    /// Recursively clears type information from this node and all its children
    /// Useful for clearing type information before re-running type inference
    /// </summary>
    public static void ClearTypeInformationRecursively(this AstNode node)
    {
        if (node == null) return;

        // Clear type information from this node
        node.ClearAllTypeInformation();

        // Recursively clear from all children
        foreach (var child in node.Children)
        {
            child.ClearTypeInformationRecursively();
        }
    }

    #endregion

    #region Type Checking Helpers

    /// <summary>
    /// Checks if the inferred type of this node is compatible with the expected type
    /// </summary>
    public static bool IsTypeCompatibleWith(this AstNode node, TypeInfo expectedType)
    {
        var actualType = node.GetInferredType();
        if (actualType == null) return false;

        return expectedType.IsAssignableFrom(actualType);
    }

    /// <summary>
    /// Gets the effective type of this node, defaulting to Any if no type is inferred
    /// </summary>
    public static TypeInfo GetEffectiveType(this AstNode node)
    {
        return node.GetInferredType() ?? AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Checks if this node represents a value of a specific type kind
    /// </summary>
    public static bool IsOfTypeKind(this AstNode node, TypeKind kind)
    {
        var type = node.GetInferredType();
        return type?.Kind == kind;
    }

    /// <summary>
    /// Checks if this node represents a primitive type value
    /// </summary>
    public static bool IsPrimitiveType(this AstNode node)
    {
        return node.IsOfTypeKind(TypeKind.Primitive);
    }

    /// <summary>
    /// Checks if this node represents a builtin object type value
    /// </summary>
    public static bool IsBuiltinObjectType(this AstNode node)
    {
        return node.IsOfTypeKind(TypeKind.BuiltinObject);
    }

    /// <summary>
    /// Checks if this node represents an application class type value
    /// </summary>
    public static bool IsAppClassType(this AstNode node)
    {
        return node.IsOfTypeKind(TypeKind.AppClass);
    }

    /// <summary>
    /// Checks if this node represents a void type (no return value)
    /// </summary>
    public static bool IsVoidType(this AstNode node)
    {
        return node.IsOfTypeKind(TypeKind.Void);
    }

    /// <summary>
    /// Checks if this node can be used in an assignment context
    /// (i.e., it's not void and not unknown)
    /// </summary>
    public static bool IsAssignable(this AstNode node)
    {
        var type = node.GetInferredType();
        return type != null &&
               type.Kind != TypeKind.Void &&
               type.Kind != TypeKind.Unknown;
    }

    /// <summary>
    /// Gets a user-friendly description of why a node cannot be assigned
    /// </summary>
    public static string? GetAssignmentErrorReason(this AstNode node)
    {
        var type = node.GetInferredType();
        if (type == null)
        {
            return "Type could not be determined";
        }

        return type.Kind switch
        {
            TypeKind.Void => "Function does not return a value",
            TypeKind.Unknown => "Type is unknown or unresolved",
            _ => null // No error, assignable
        };
    }

    #endregion
}
