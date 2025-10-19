using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;

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
}

/// <summary>
/// Extension methods for AstNode to support type system operations.
/// </summary>
public static class AstNodeTypeExtensions
{
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
}
