using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Indexes a program's top-level functions and answers PeopleCode's single-pass
/// visibility rule: a Declare Function is visible everywhere in the program, while a
/// function implementation is only visible at positions textually below its start.
/// Shared by UndeclaredFunctionStyler (per-call-site checks) and function-name
/// autocomplete (enumeration at the cursor).
/// </summary>
public sealed class FunctionVisibilityIndex
{
    private readonly Dictionary<string, FunctionNode> _declarations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FunctionNode> _implementations = new(StringComparer.OrdinalIgnoreCase);

    private FunctionVisibilityIndex() { }

    /// <summary>Declare Function nodes by name. Visible program-wide.</summary>
    public IReadOnlyDictionary<string, FunctionNode> Declarations => _declarations;

    /// <summary>
    /// First implementation per name — visibility is judged against the earliest
    /// definition of the name.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionNode> Implementations => _implementations;

    public static FunctionVisibilityIndex Build(ProgramNode program)
    {
        var index = new FunctionVisibilityIndex();
        foreach (var fn in program.Functions)
        {
            if (fn.IsDeclaration)
            {
                index._declarations[fn.Name] = fn;
            }
            else if (fn.IsImplementation)
            {
                index._implementations.TryAdd(fn.Name, fn);
            }
        }
        return index;
    }

    /// <summary>
    /// All functions callable at the given UTF-8 byte position: every declare, plus
    /// implementations that start above the position. Deduped case-insensitively;
    /// a declare shadows a same-named implementation.
    /// </summary>
    public List<FunctionNode> GetVisibleAt(int byteIndex)
    {
        var result = new List<FunctionNode>(_declarations.Count + _implementations.Count);
        result.AddRange(_declarations.Values);
        foreach (var impl in _implementations.Values)
        {
            if (impl.SourceSpan.Start.ByteIndex < byteIndex && !_declarations.ContainsKey(impl.Name))
            {
                result.Add(impl);
            }
        }
        return result;
    }
}
