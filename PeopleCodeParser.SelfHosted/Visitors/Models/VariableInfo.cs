using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted.Visitors.Models;

public class VariableInfo
{
    public string Name { get; }
    public string Type { get; }
    public int Line { get; }
    public (int Start, int Stop) Span { get; }
    public bool Used { get; set; }
    public SourceSpan? SourceSpan { get; }
    public VariableType VariableType { get; }

    /// <summary>
    /// Creates a new VariableInfo with the specified name, type, line, and span.
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <param name="type">Variable type</param>
    /// <param name="line">Line number where the variable is declared</param>
    /// <param name="span">Byte span (start and stop indexes) in the source code</param>
    /// <param name="variableType">Type of variable (local, global, instance, etc.)</param>
    public VariableInfo(string name, string type, int line, (int Start, int Stop) span, VariableType variableType = VariableType.Local)
    {
        Name = name;
        Type = type;
        Line = line;
        Span = span;
        VariableType = variableType;
        Used = false;
    }

    /// <summary>
    /// Creates a new VariableInfo with the specified name, type, and source span.
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <param name="type">Variable type</param>
    /// <param name="sourceSpan">Source span containing position information</param>
    /// <param name="variableType">Type of variable (local, global, instance, etc.)</param>
    public VariableInfo(string name, string type, SourceSpan sourceSpan, VariableType variableType = VariableType.Local)
    {
        Name = name;
        Type = type;
        Line = sourceSpan.Start.Line;
        Span = (sourceSpan.Start.ByteIndex, sourceSpan.End.ByteIndex - 1); // End is exclusive, so -1 for inclusive stop
        SourceSpan = sourceSpan;
        VariableType = variableType;
        Used = false;
    }

    public override string ToString() => $"{Type} {Name} (Line {Line})";
}