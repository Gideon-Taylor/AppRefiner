using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Extension methods for AstNode to provide convenient access to semantic analysis attributes.
/// These methods provide a clean API for working with type information, function metadata, and type errors
/// that are stored in the node's Attributes dictionary.
/// </summary>
public static class AstNodeExtensions
{
    #region TypeInfo Extensions

    /// <summary>
    /// Gets the inferred type information for this node, if available.
    /// </summary>
    /// <param name="node">The AST node to query</param>
    /// <returns>The TypeInfo if type inference has been performed, null otherwise</returns>
    public static TypeInfo? GetInferredType(this AstNode node)
    {
        if (node.Attributes.TryGetValue(AstNode.TypeInfoAttributeKey, out var typeInfo))
        {
            return typeInfo as TypeInfo;
        }
        return null;
    }

    /// <summary>
    /// Sets the inferred type information for this node.
    /// </summary>
    /// <param name="node">The AST node to update</param>
    /// <param name="typeInfo">The type information to store</param>
    public static void SetInferredType(this AstNode node, TypeInfo typeInfo)
    {
        node.Attributes[AstNode.TypeInfoAttributeKey] = typeInfo;
    }

    #endregion

    #region FunctionInfo Extensions

    /// <summary>
    /// Gets the function metadata for this node, if available.
    /// Typically used on FunctionCallNode to get information about the called function.
    /// </summary>
    /// <param name="node">The AST node to query</param>
    /// <returns>The FunctionInfo if function metadata has been stored, null otherwise</returns>
    public static FunctionInfo? GetFunctionInfo(this AstNode node)
    {
        if (node.Attributes.TryGetValue(AstNode.FunctionInfoAttributeKey, out var functionInfo))
        {
            return functionInfo as FunctionInfo;
        }
        return null;
    }

    /// <summary>
    /// Sets the function metadata for this node.
    /// </summary>
    /// <param name="node">The AST node to update</param>
    /// <param name="functionInfo">The function information to store</param>
    public static void SetFunctionInfo(this AstNode node, FunctionInfo functionInfo)
    {
        node.Attributes[AstNode.FunctionInfoAttributeKey] = functionInfo;
    }

    #endregion

    #region TypeError Extensions

    /// <summary>
    /// Gets the type error for this node, if one has been recorded.
    /// </summary>
    /// <param name="node">The AST node to query</param>
    /// <returns>The TypeError if a type checking error was found, null otherwise</returns>
    public static TypeError? GetTypeError(this AstNode node)
    {
        if (node.Attributes.TryGetValue(AstNode.TypeErrorAttributeKey, out var error))
        {
            return error as TypeError;
        }
        return null;
    }

    /// <summary>
    /// Gets the type error for this node, if one has been recorded.
    /// </summary>
    /// <param name="node">The AST node to query</param>
    /// <returns>The TypeError if a type checking error was found, null otherwise</returns>
    public static TypeError? GetTypeWarning(this AstNode node)
    {
        if (node.Attributes.TryGetValue(AstNode.TypeWarningAttributeKey, out var error))
        {
            return error as TypeError;
        }
        return null;
    }

    /// <summary>
    /// Sets the type error for this node.
    /// </summary>
    /// <param name="node">The AST node to update</param>
    /// <param name="error">The type error to record</param>
    public static void SetTypeError(this AstNode node, TypeError error)
    {
        node.Attributes[AstNode.TypeErrorAttributeKey] = error;
    }

    /// <summary>
    /// Sets the type warning for this node.
    /// </summary>
    /// <param name="node">The AST node to update</param>
    /// <param name="error">The type error to record</param>
    public static void SetTypeWarning(this AstNode node, TypeError error)
    {
        node.Attributes[AstNode.TypeWarningAttributeKey] = error;
    }

    /// <summary>
    /// Checks if this node has a type error.
    /// </summary>
    /// <param name="node">The AST node to check</param>
    /// <returns>True if the node has a type error, false otherwise</returns>
    public static bool HasTypeError(this AstNode node)
    {
        return node.Attributes.ContainsKey(AstNode.TypeErrorAttributeKey);
    }

    /// <summary>
    /// Checks if this node has a type warning.
    /// </summary>
    /// <param name="node">The AST node to check</param>
    /// <returns>True if the node has a type error, false otherwise</returns>
    public static bool HasTypeWarning(this AstNode node)
    {
        return node.Attributes.ContainsKey(AstNode.TypeWarningAttributeKey);
    }

    /// <summary>
    /// Gets all type errors from this node and all its descendants.
    /// Useful for collecting all type errors from a program after type checking.
    /// </summary>
    /// <param name="root">The root node to start searching from (typically a ProgramNode)</param>
    /// <returns>All type errors found in the tree</returns>
    public static IEnumerable<TypeError> GetAllTypeErrors(this AstNode root)
    {
        // Check if this node has an error
        var error = root.GetTypeError();
        if (error != null)
        {
            yield return error;
        }

        // Recursively check all children
        foreach (var child in root.Children)
        {
            foreach (var childError in child.GetAllTypeErrors())
            {
                yield return childError;
            }
        }
    }

    /// <summary>
    /// Gets all type warnings from this node and all its descendants.
    /// Useful for collecting all type errors from a program after type checking.
    /// </summary>
    /// <param name="root">The root node to start searching from (typically a ProgramNode)</param>
    /// <returns>All type errors found in the tree</returns>
    public static IEnumerable<TypeError> GetAllTypeWarnings(this AstNode root)
    {
        // Check if this node has an error
        var error = root.GetTypeWarning();
        if (error != null)
        {
            yield return error;
        }

        // Recursively check all children
        foreach (var child in root.Children)
        {
            foreach (var childError in child.GetAllTypeWarnings())
            {
                yield return childError;
            }
        }
    }

    #endregion
}
