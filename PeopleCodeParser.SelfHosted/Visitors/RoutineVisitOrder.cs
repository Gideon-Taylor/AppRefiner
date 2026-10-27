using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// Orders an app class's routines so callees are visited before their callers, letting a
/// caller see a same-program refined return type (spec §9 "Same-program calls").
/// </summary>
public static class RoutineVisitOrder
{
    /// <summary>
    /// Methods and property getters in depth-first post-order over the syntactic call graph,
    /// starting from methods in header order and then getters in property order. Edges are
    /// <c>%This.Name(...)</c> naming a method of the class and a non-call <c>%This.Name</c>
    /// naming a property with a getter. A bare <c>Name(...)</c> is not an edge: AppRefiner
    /// treats it as a call to an undeclared function, not a method. Back edges (cycles) are
    /// ignored, so mutually recursive routines keep header order. Setters follow everything else.
    /// </summary>
    public static IEnumerable<AstNode> CalleesFirst(AppClassNode node)
    {
        var methods = new Dictionary<string, MethodNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in node.Methods)
            methods.TryAdd(method.Name, method);

        var getters = new Dictionary<string, PropertyImplNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in node.Properties)
        {
            if (property.Getter != null)
                getters.TryAdd(property.Name, property.Getter);
        }

        var starts = new List<AstNode>(node.Methods);
        starts.AddRange(node.Properties.Where(p => p.Getter != null).Select(p => (AstNode)p.Getter!));

        var order = new List<AstNode>();
        var done = new HashSet<AstNode>(ReferenceEqualityComparer.Instance);
        var onPath = new HashSet<AstNode>(ReferenceEqualityComparer.Instance);

        void Visit(AstNode routine)
        {
            if (done.Contains(routine) || !onPath.Add(routine))
                return;

            foreach (var callee in Callees(routine, methods, getters))
                Visit(callee);

            onPath.Remove(routine);
            done.Add(routine);
            order.Add(routine);
        }

        foreach (var start in starts)
            Visit(start);

        foreach (var property in node.Properties)
        {
            if (property.Setter != null)
                order.Add(property.Setter);
        }

        return order;
    }

    /// <summary>
    /// Groups of routines that call each other (strongly connected components of the same call
    /// graph), including a routine that calls itself. Callees-first order can't put one before
    /// the other, so their returns are settled together once the group has been visited.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<AstNode>> Cycles(AppClassNode node)
    {
        var methods = new Dictionary<string, MethodNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in node.Methods)
            methods.TryAdd(method.Name, method);

        var getters = new Dictionary<string, PropertyImplNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in node.Properties)
        {
            if (property.Getter != null)
                getters.TryAdd(property.Name, property.Getter);
        }

        var routines = methods.Values.Cast<AstNode>().Concat(getters.Values).ToList();
        var edges = routines.ToDictionary(r => r, r => Callees(r, methods, getters).Distinct().ToList(),
            ReferenceEqualityComparer.Instance);

        // Tarjan's algorithm
        var index = new Dictionary<AstNode, int>(ReferenceEqualityComparer.Instance);
        var low = new Dictionary<AstNode, int>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<AstNode>();
        var onStack = new HashSet<AstNode>(ReferenceEqualityComparer.Instance);
        var cycles = new List<IReadOnlyList<AstNode>>();

        void Connect(AstNode routine)
        {
            index[routine] = low[routine] = index.Count;
            stack.Push(routine);
            onStack.Add(routine);

            foreach (var callee in edges[routine])
            {
                if (!index.ContainsKey(callee))
                {
                    Connect(callee);
                    low[routine] = Math.Min(low[routine], low[callee]);
                }
                else if (onStack.Contains(callee))
                {
                    low[routine] = Math.Min(low[routine], index[callee]);
                }
            }

            if (low[routine] != index[routine])
                return;

            var component = new List<AstNode>();
            AstNode member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            } while (!ReferenceEquals(member, routine));

            if (component.Count > 1 || edges[routine].Contains(routine))
                cycles.Add(component);
        }

        foreach (var routine in routines)
        {
            if (!index.ContainsKey(routine))
                Connect(routine);
        }

        return cycles;
    }

    /// <summary>
    /// The routines <paramref name="routine"/>'s body references, in source order.
    /// </summary>
    private static IEnumerable<AstNode> Callees(
        AstNode routine,
        Dictionary<string, MethodNode> methods,
        Dictionary<string, PropertyImplNode> getters)
    {
        var body = routine switch
        {
            MethodNode method => method.Body,
            PropertyImplNode getter => getter.Body,
            _ => null
        };
        if (body == null)
            yield break;

        var references = body.FindDescendants<ExpressionNode>()
            .Where(e => e is FunctionCallNode or MemberAccessNode)
            .OrderBy(e => e.SourceSpan.Start.Index);

        foreach (var expression in references)
        {
            switch (expression)
            {
                case FunctionCallNode { Function: MemberAccessNode { Target: IdentifierNode target } access }
                    when IsThis(target) && methods.TryGetValue(access.MemberName, out var thisMethod):
                    yield return thisMethod;
                    break;

                case MemberAccessNode { Target: IdentifierNode target } access
                    when IsThis(target)
                        && !(access.Parent is FunctionCallNode call && ReferenceEquals(call.Function, access))
                        && getters.TryGetValue(access.MemberName, out var getter):
                    yield return getter;
                    break;
            }
        }
    }

    private static bool IsThis(IdentifierNode identifier) =>
        identifier.Name.Equals("%This", StringComparison.OrdinalIgnoreCase);
}
