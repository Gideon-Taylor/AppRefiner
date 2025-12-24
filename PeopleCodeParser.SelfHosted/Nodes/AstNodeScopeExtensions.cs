using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Extension methods for accessing scope context information from AST nodes
/// </summary>
public static class AstNodeScopeExtensions
{
    /// <summary>
    /// Gets the scope context for this AST node.
    /// If the node itself doesn't have scope information, walks up the parent chain
    /// to find the nearest ancestor with scope context.
    /// </summary>
    /// <param name="node">The AST node to get scope context for</param>
    /// <returns>The scope context, or null if no scope information is found</returns>
    public static ScopeContext? GetScopeContext(this AstNode node)
    {
        // Try to get scope from current node
        if (node.Attributes.TryGetValue(
            ScopeAnnotationVisitor.ScopeContextAttributeKey,
            out var scope))
        {
            return scope as ScopeContext;
        }

        // Fallback: search up the tree for an annotated ancestor
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent.Attributes.TryGetValue(
                ScopeAnnotationVisitor.ScopeContextAttributeKey,
                out var parentScope))
            {
                return parentScope as ScopeContext;
            }
            parent = parent.Parent;
        }

        return null;
    }
}
