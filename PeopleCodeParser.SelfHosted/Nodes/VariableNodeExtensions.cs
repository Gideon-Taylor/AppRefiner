using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Extensions to the VariableNode class to add support for tracking variable name tokens
/// </summary>
public static class VariableNodeExtensions
{
    /// <summary>
    /// Updates the VariableNode class to track variable name tokens
    /// </summary>
    public static void UpdateVariableNode(this VariableNode node, string name, Token token)
    {
        // Replace the main name with a VariableNameInfo
        if (node.NameInfos.Count > 0)
        {
            node.NameInfos[0] = new VariableNameInfo(name, token);
        }
        else
        {
            node.NameInfos.Add(new VariableNameInfo(name, token));
        }
    }

    /// <summary>
    /// Adds a variable name with its token information
    /// </summary>
    public static void AddNameWithToken(this VariableNode node, string name, Token token)
    {
        node.NameInfos.Add(new VariableNameInfo(name, token));
        node.AdditionalNames.Add(name);
    }
}

