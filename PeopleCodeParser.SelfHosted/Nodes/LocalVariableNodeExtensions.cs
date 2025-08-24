using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Extensions to the LocalVariableDeclarationNode class to add support for tracking variable name tokens
/// </summary>
public static class LocalVariableNodeExtensions
{
    /// <summary>
    /// Adds a variable name with its token information to a LocalVariableDeclarationNode
    /// </summary>
    public static void AddVariableNameWithToken(this LocalVariableDeclarationNode node, string name, Token token)
    {
        node.VariableNameInfos.Add(new VariableNameInfo(name, token));
        node.VariableNames.Add(name);
    }
    
    /// <summary>
    /// Adds a variable name with its token information to a LocalVariableDeclarationWithAssignmentNode
    /// </summary>
    public static void SetVariableNameWithToken(this LocalVariableDeclarationWithAssignmentNode node, string name, Token token)
    {
        node.VariableNameInfo = new VariableNameInfo(name, token);
    }
}
