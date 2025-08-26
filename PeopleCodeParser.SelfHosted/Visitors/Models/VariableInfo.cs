using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Visitors.Models;

public class VariableInfo
{
    public string Name { get; }
    public string Type { get; }
    public int Line { get; }
    public bool Used { get; set; }
    public VariableType VariableType { get; }
    public VariableNameInfo VariableNameInfo { get; }

    /// <summary>
    /// Creates a new VariableInfo with a VariableNameInfo reference, preserving rich source information.
    /// </summary>
    /// <param name="variableNameInfo">Variable name information from the parser</param>
    /// <param name="type">Variable type</param>
    /// <param name="variableType">Type of variable (local, global, instance, etc.)</param>
    public VariableInfo(VariableNameInfo variableNameInfo, string type, VariableType variableType = VariableType.Local)
    {
        Name = variableNameInfo.Name;
        Type = type;
        Line = variableNameInfo.Token?.SourceSpan.Start.Line ?? 0;
        VariableType = variableType;
        VariableNameInfo = variableNameInfo;
        Used = false;
    }

    public override string ToString() => $"{Type} {Name} (Line {Line})";
}