using PeopleCodeParser.SelfHosted.Analysis;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
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

    /// <summary>
    /// Checks if this AST node has type information attached
    /// </summary>
    public static bool HasInferredType(this AstNode node)
    {
        return node?.Attributes.ContainsKey(AstNode.TypeInfoAttributeKey) ?? false;
    }

    /// <summary>
    /// Clears any type information from this AST node
    /// </summary>
    public static void ClearInferredType(this AstNode node)
    {
        if (node == null) return;

        node.Attributes.Remove(AstNode.TypeInfoAttributeKey);
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

    /// <summary>
    /// Gets the effective <c>/* as T */</c> parameter annotations of an app-class method call:
    /// for each parameter, its own annotation or the one inherited from a base declaration.
    /// </summary>
    /// <param name="node">The AST node to query (a FunctionCallNode)</param>
    /// <returns>One entry per parameter (null where unannotated), or null if none were stored</returns>
    public static IReadOnlyList<TypeWithDimensionality?>? GetParameterAnnotations(this AstNode node)
    {
        if (node.Attributes.TryGetValue(AstNode.ParameterAnnotationsAttributeKey, out var annotations))
        {
            return annotations as IReadOnlyList<TypeWithDimensionality?>;
        }
        return null;
    }

    /// <summary>
    /// Sets the effective parameter annotations of an app-class method call.
    /// </summary>
    /// <param name="node">The AST node to update</param>
    /// <param name="annotations">One entry per parameter, null where unannotated</param>
    public static void SetParameterAnnotations(this AstNode node, IReadOnlyList<TypeWithDimensionality?> annotations)
    {
        node.Attributes[AstNode.ParameterAnnotationsAttributeKey] = annotations;
    }

    #endregion

    #region RefinementSource Extensions

    /// <summary>
    /// Gets where this variable use's refined type came from, if it was refined.
    /// </summary>
    /// <param name="node">The AST node to query</param>
    /// <returns>The RefinementSource if one has been stored, null otherwise</returns>
    public static RefinementSource? GetRefinementSource(this AstNode node)
    {
        if (node.Attributes.TryGetValue(AstNode.RefinementSourceAttributeKey, out var source))
        {
            return source as RefinementSource;
        }
        return null;
    }

    /// <summary>
    /// Sets where this variable use's refined type came from.
    /// </summary>
    /// <param name="node">The AST node to update</param>
    /// <param name="source">The refinement source to store</param>
    public static void SetRefinementSource(this AstNode node, RefinementSource source)
    {
        node.Attributes[AstNode.RefinementSourceAttributeKey] = source;
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
    /// Gets the type warnings recorded on this node, in the order they were added.
    /// </summary>
    /// <param name="node">The AST node to query</param>
    /// <returns>The node's warnings; empty when there are none</returns>
    public static IReadOnlyList<TypeError> GetTypeWarnings(this AstNode node)
    {
        if (node.Attributes.TryGetValue(AstNode.TypeWarningAttributeKey, out var value)
            && value is List<TypeError> warnings)
        {
            return warnings;
        }
        return Array.Empty<TypeError>();
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
    /// Adds a type warning to this node. A warning whose message the node already holds
    /// (ordinal comparison) is ignored, so re-running a pass doesn't double its warnings.
    /// </summary>
    /// <param name="node">The AST node to update</param>
    /// <param name="warning">The type warning to record</param>
    public static void AddTypeWarning(this AstNode node, TypeError warning)
    {
        if (!node.Attributes.TryGetValue(AstNode.TypeWarningAttributeKey, out var value)
            || value is not List<TypeError> warnings)
        {
            warnings = new List<TypeError>();
            node.Attributes[AstNode.TypeWarningAttributeKey] = warnings;
        }

        if (warnings.Any(w => string.Equals(w.Message, warning.Message, StringComparison.Ordinal)))
            return;

        warnings.Add(warning);
    }

    /// <summary>
    /// Removes the type error and type warnings from this node and all its descendants, so a
    /// re-run of inference and checking on a cached tree starts from a clean slate.
    /// </summary>
    /// <param name="root">The root node to clear (typically a ProgramNode)</param>
    public static void ClearTypeDiagnostics(this AstNode root)
    {
        root.Attributes.Remove(AstNode.TypeErrorAttributeKey);
        root.Attributes.Remove(AstNode.TypeWarningAttributeKey);

        foreach (var child in root.Children)
        {
            child.ClearTypeDiagnostics();
        }
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
    /// <returns>True if the node has a type warning, false otherwise</returns>
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
    /// Gets all type warnings from this node and all its descendants, each node's
    /// warnings in the order they were added.
    /// </summary>
    /// <param name="root">The root node to start searching from (typically a ProgramNode)</param>
    /// <returns>All type warnings found in the tree</returns>
    public static IEnumerable<TypeError> GetAllTypeWarnings(this AstNode root)
    {
        foreach (var warning in root.GetTypeWarnings())
        {
            yield return warning;
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

    #region ExitMode Extensions

    /// <summary>
    /// Gets the completion analysis result for this node, if
    /// <see cref="Analysis.CompletionAnalyzer"/> has been run over it.
    /// </summary>
    /// <returns>The ExitMode set, or null if completion analysis has not been performed.</returns>
    public static ExitMode? GetExitMode(this AstNode node)
    {
        if (node.Attributes.TryGetValue(AstNode.ExitModeAttributeKey, out var mode)
            && mode is ExitMode exitMode)
        {
            return exitMode;
        }
        return null;
    }

    /// <summary>
    /// Stores the completion analysis result for this node.
    /// </summary>
    public static void SetExitMode(this AstNode node, ExitMode mode)
    {
        node.Attributes[AstNode.ExitModeAttributeKey] = mode;
    }

    #endregion
}
