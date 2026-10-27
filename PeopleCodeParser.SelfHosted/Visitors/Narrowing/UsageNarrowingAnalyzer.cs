using PeopleCodeParser.SelfHosted.Annotations;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Visitors.Narrowing;

/// <summary>
/// Usage narrowing (spec §8): works out which API type an <c>ApiObject</c> variable holds from the
/// members used directly on it. Uses are taken in source order; the first use that leaves one
/// candidate settles the type, later uses are checked against it, and a use that fits nothing is
/// reported and ignored. Pure analysis: nothing on the AST is changed.
/// </summary>
public sealed class UsageNarrowingAnalyzer : ScopedAstVisitor<object>
{
    // API types whose properties are resolved at runtime: a component interface's keys and fields,
    // and the fields of an item in one of its collections
    private static readonly HashSet<string> DynamicPropertyTypes =
        new(StringComparer.OrdinalIgnoreCase) { ComponentInterface, "DataItem" };

    private const string ComponentInterface = "ComponentInterface";

    // Every API subtype reachable through a PeopleCodeType value, in ordinal name order.
    // typeinfo.dat can't be enumerated by name, so the enum is the only source.
    private static readonly Lazy<IReadOnlyList<string>> AllCandidates = new(() =>
        Enum.GetValues<PeopleCodeType>()
            .Select(t => t.GetTypeName())
            .Where(PeopleCodeTypeDatabase.IsApiObjectSubtype)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList());

    private readonly record struct Use(MemberAccessNode Node, bool IsMethod)
    {
        public string Decider => IsMethod ? $"{Node.MemberName}()" : Node.MemberName;
        public int Line => Node.SourceSpan.Start.Line + 1;
    }

    private readonly ProgramNode _program;

    // Eligible variables, with their names as declared
    private readonly Dictionary<VariableKey, string> _eligible = new();
    private readonly HashSet<VariableKey> _annotated = new();
    private readonly Dictionary<VariableKey, List<Use>> _uses = new();
    private readonly HashSet<MemberAccessNode> _seen = new(ReferenceEqualityComparer.Instance);

    private UsageNarrowingAnalyzer(ProgramNode program)
    {
        _program = program;
    }

    public static NarrowingResults Analyze(ProgramNode program)
    {
        var analyzer = new UsageNarrowingAnalyzer(program);
        program.Accept(analyzer);
        return analyzer.BuildResults();
    }

    /// <summary>
    /// The <c>{phrase}</c> for a candidate list: <c>A or B</c>, <c>A, B or C</c>, or
    /// <c>one of {count} types (A, B, C, …)</c> for more than three.
    /// </summary>
    public static string CandidatePhrase(IReadOnlyList<string> candidates)
    {
        var sorted = candidates.OrderBy(name => name, StringComparer.Ordinal).ToList();
        return sorted.Count switch
        {
            0 => "any API object type",
            1 => sorted[0],
            2 or 3 => $"{string.Join(", ", sorted.Take(sorted.Count - 1))} or {sorted[^1]}",
            _ => $"one of {sorted.Count} types ({string.Join(", ", sorted.Take(3))}, …)"
        };
    }

    /// <summary>
    /// A deciding use as messages and hovers name it: <c>Name()</c> for a method use, <c>Name</c> for a property.
    /// </summary>
    public static string Decider(MemberAccessNode use) =>
        use.Parent is FunctionCallNode call && ReferenceEquals(call.Function, use) ? $"{use.MemberName}()" : use.MemberName;

    #region Collection

    protected override void OnVariableDeclared(VariableInfo variable)
    {
        if (variable.IsAutoDeclared || !variable.Type.Equals("ApiObject", StringComparison.OrdinalIgnoreCase))
            return;

        var key = new VariableKey(variable.DeclarationNode, variable.Name);
        _eligible.TryAdd(key, variable.Name);

        var annotations = _program.Annotations;
        bool annotated = variable.DeclarationNode switch
        {
            ParameterNode p => Types(annotations.ForType(p.Type)),
            ProgramVariableNode pv => Types(annotations.ForType(pv.Type)),
            PropertyNode prop => Types(annotations.ForType(prop.Type)),
            LocalVariableDeclarationNode local => Types(annotations.ForType(local.Type)),
            LocalVariableDeclarationWithAssignmentNode withValue =>
                Types(annotations.ForType(withValue.Type)) || Types(annotations.ForValue(withValue.InitialValue)),
            _ => false
        };
        if (annotated)
            _annotated.Add(key);
    }

    /// <summary>
    /// Whether an annotation types its variable. One naming an unknown type is only warned
    /// about, so the variable is still narrowed from its uses.
    /// </summary>
    private static bool Types(ParsedAnnotation? annotation) => annotation?.Target != null;

    public override void VisitAssignment(AssignmentNode node)
    {
        // An annotated assignment to the variable anywhere turns narrowing off for it
        if (node.Target is IdentifierNode target
            && Types(_program.Annotations.ForValue(node.Value))
            && FindVariable(target.Name) is { } variable)
        {
            _annotated.Add(new VariableKey(variable.DeclarationNode, variable.Name));
        }

        base.VisitAssignment(node);
    }

    public override void VisitMemberAccess(MemberAccessNode node)
    {
        RecordUse(node);
        base.VisitMemberAccess(node);
    }

    // Inside an assignment, the base walks these nodes' children with its skip flag still set, so
    // nested calls and operators below them are never visited. Their uses are recorded up front.
    public override void VisitObjectCreation(ObjectCreationNode node)
    {
        RecordUsesWithin(node);
        base.VisitObjectCreation(node);
    }

    public override void VisitObjectCreateShortHand(ObjectCreateShortHand node)
    {
        RecordUsesWithin(node);
        base.VisitObjectCreateShortHand(node);
    }

    public override void VisitPartialShortHandAssignment(PartialShortHandAssignmentNode node)
    {
        RecordUsesWithin(node);
        base.VisitPartialShortHandAssignment(node);
    }

    public override void VisitInterpolatedString(InterpolatedStringNode node)
    {
        RecordUsesWithin(node);
        base.VisitInterpolatedString(node);
    }

    private void RecordUsesWithin(AstNode node)
    {
        foreach (var member in node.FindDescendants<MemberAccessNode>())
            RecordUse(member);
    }

    /// <summary>
    /// Records a direct member access on an eligible variable. Only the first link of a chain
    /// has an identifier target, so later links never count.
    /// </summary>
    private void RecordUse(MemberAccessNode node)
    {
        if (node.IsDynamic || node.Target is not IdentifierNode target || !_seen.Add(node))
            return;

        var variable = FindVariable(target.Name);
        if (variable == null)
            return;

        var key = new VariableKey(variable.DeclarationNode, variable.Name);
        if (!_eligible.ContainsKey(key))
            return;

        bool isMethod = node.Parent is FunctionCallNode call && ReferenceEquals(call.Function, node);
        if (!_uses.TryGetValue(key, out var uses))
            _uses[key] = uses = new List<Use>();
        uses.Add(new Use(node, isMethod));
    }

    #endregion

    #region Narrowing

    private NarrowingResults BuildResults()
    {
        var results = new Dictionary<VariableKey, NarrowingResult>();
        var conflicts = new Dictionary<MemberAccessNode, NarrowingConflict>(ReferenceEqualityComparer.Instance);

        foreach (var (key, name) in _eligible)
        {
            if (_annotated.Contains(key))
                continue;

            var uses = _uses.TryGetValue(key, out var collected)
                ? collected.OrderBy(u => u.Node.SourceSpan.Start.Index).ToList()
                : new List<Use>();

            var result = Narrow(name, uses);
            results[key] = result;
            foreach (var conflict in result.Conflicts)
                conflicts[conflict.Use] = conflict;
        }

        return new NarrowingResults(results, conflicts);
    }

    private static NarrowingResult Narrow(string variableName, List<Use> uses)
    {
        IReadOnlyList<string> candidates = AllCandidates.Value;
        var counted = new List<Use>();
        var conflicts = new List<NarrowingConflict>();
        string? settled = null;
        Use? decider = null;

        foreach (var use in uses)
        {
            var member = use.Node.MemberName;

            if (settled != null)
            {
                // Settled types don't change; a use that doesn't fit is reported against the decider.
                // A CI's definition can add methods, so an unknown method on one isn't reported
                if (!HasMember(settled, use) && !(use.IsMethod && settled == ComponentInterface))
                {
                    var d = decider!.Value;
                    conflicts.Add(new NarrowingConflict(use.Node,
                        $"{variableName} was inferred as {settled} from {d.Decider} on line {d.Line}, but {settled} has no member '{member}'."));
                }
                continue;
            }

            bool declared = false;
            var remaining = use.IsMethod
                ? candidates.Where(type => HasMember(type, use)).ToList()
                : PropertyFits(candidates, member, out declared);

            // A property no candidate declares could be any CI or item field, or a typo; it doesn't
            // count until a method or declared property has narrowed the list
            if (!use.IsMethod && !declared && counted.Count == 0)
                continue;

            if (remaining.Count == 0)
            {
                var message = counted.Count == 0
                    ? $"'{member}' is not a member of any API object type."
                    : $"'{member}' is not a member of {CandidatePhrase(candidates)}. " +
                      $"Earlier uses of {variableName} ({string.Join(", ", counted.Select(u => $"{u.Decider} on line {u.Line}"))}) suggested one of those types.";
                conflicts.Add(new NarrowingConflict(use.Node, message));
                continue;
            }

            candidates = remaining;
            counted.Add(use);

            if (remaining.Count == 1)
            {
                settled = remaining[0];
                decider = use;
            }
        }

        return new NarrowingResult(
            variableName,
            settled,
            decider?.Node,
            counted.Count == 0 ? Array.Empty<string>() : candidates,
            conflicts);
    }

    /// <summary>
    /// The candidates a property use leaves: those that declare it, or, when none does, the ones
    /// with dynamic properties (ComponentInterface and DataItem), where it may be a field. A CI
    /// field named like a declared property (NAME, COUNT) narrows away from ComponentInterface;
    /// CI variables are nearly always typed by GetCompIntfc() or a CI method first, and an
    /// annotation covers the rest.
    /// </summary>
    private static List<string> PropertyFits(IReadOnlyList<string> candidates, string member, out bool declared)
    {
        var declaring = candidates.Where(type => PeopleCodeTypeDatabase.GetProperty(type, member) != null).ToList();
        declared = declaring.Count > 0;
        return declared ? declaring : candidates.Where(DynamicPropertyTypes.Contains).ToList();
    }

    /// <summary>
    /// Whether <paramref name="type"/> has the used member. Properties always fit
    /// ComponentInterface and DataItem, whose fields are dynamic properties.
    /// </summary>
    private static bool HasMember(string type, Use use) =>
        use.IsMethod
            ? PeopleCodeTypeDatabase.GetMethod(type, use.Node.MemberName) != null
            : PeopleCodeTypeDatabase.GetProperty(type, use.Node.MemberName) != null || DynamicPropertyTypes.Contains(type);

    #endregion
}
