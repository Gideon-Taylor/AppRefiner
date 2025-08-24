namespace PeopleCodeParser.SelfHosted.Scoped.Models;

public enum ScopeType
{
    Global,
    Method,
    Function,
    Property,
    Getter,
    Setter,
    Block
}

public class ScopeInfo
{
    public ScopeType Type { get; }
    public string Name { get; }
    public ScopeInfo? Parent { get; }
    public int Depth { get; }

    public ScopeInfo(ScopeType type, string name, ScopeInfo? parent = null)
    {
        Type = type;
        Name = name;
        Parent = parent;
        Depth = parent?.Depth + 1 ?? 0;
    }

    public string FullPath => Parent != null ? $"{Parent.FullPath}.{Name}" : Name;

    public override string ToString() => $"{Type}: {FullPath} (Depth {Depth})";
}