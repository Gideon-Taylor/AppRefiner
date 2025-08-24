using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Holds information about a variable name, including its token
/// </summary>
public class VariableNameInfo
{
    /// <summary>
    /// The name of the variable
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// The token representing this variable name
    /// </summary>
    public Token? Token { get; }
    
    /// <summary>
    /// Creates a new VariableNameInfo with the specified name and token
    /// </summary>
    public VariableNameInfo(string name, Token? token = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Token = token;
    }
    
    /// <summary>
    /// Gets the source span for this variable name
    /// </summary>
    public SourceSpan? SourceSpan => Token?.SourceSpan;
    
    /// <summary>
    /// Implicit conversion from string to VariableNameInfo
    /// </summary>
    public static implicit operator VariableNameInfo(string name) => new(name);
    
    /// <summary>
    /// Implicit conversion from VariableNameInfo to string
    /// </summary>
    public static implicit operator string(VariableNameInfo info) => info.Name;
    
    /// <summary>
    /// Returns the name of the variable
    /// </summary>
    public override string ToString() => Name;
}
