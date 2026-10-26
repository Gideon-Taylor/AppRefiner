using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// Refines the ApiObject, Record and Field return, property and instance-variable types of a
/// program a resolver has just parsed (spec §9 "Cross-program return types"), so callers in other
/// programs see e.g. Session instead of ApiObject. Resolvers call it right after
/// <see cref="TypeMetadataBuilder.ExtractMetadata"/>.
/// </summary>
/// <remarks>
/// Running inference on the program can fetch, and so refine, other programs through the resolver.
/// A per-thread stack of the programs being refined stops cycles and caps the depth at
/// <see cref="MaxDepth"/>; metadata whose refinement was cut short, directly or by a nested one, is
/// marked <see cref="TypeMetadata.IsProvisional"/>. Everything is cached, so a cycle's members are
/// parsed a bounded number of times, but the nested provisional entries are evicted when the
/// top-level refinement finishes; only the top-level result stays cached.
/// </remarks>
public static class ReturnTypeRefiner
{
    /// <summary>
    /// The most programs refined at once on one thread (spec §9 "Rules and guards").
    /// </summary>
    public const int MaxDepth = 3;

    // Qualified names of the programs being refined on this thread, innermost on top. Resolution
    // is synchronous within one Refine, so nested refinements run on the same thread.
    [ThreadStatic] private static Stack<string>? _refining;

    // How many refinements on this thread were cut short; a frame compares it before and after
    [ThreadStatic] private static int _cutShortCount;

    // Qualified names of nested metadata left provisional during the current top-level refinement,
    // evicted from the resolver's cache when it finishes
    [ThreadStatic] private static List<string>? _provisional;

    /// <summary>
    /// Refines <paramref name="metadata"/> (extracted from <paramref name="program"/>) in place.
    /// Never throws: on any failure the metadata stays as extracted.
    /// </summary>
    public static void Refine(ProgramNode program, TypeMetadata metadata, ITypeMetadataResolver resolver)
    {
        bool outermost = (_refining?.Count ?? 0) == 0;
        List<Action>? restore = null;
        try
        {
            if (!HasUnrefinedMember(program, metadata))
                return;

            var refining = _refining ??= new Stack<string>();

            // The program being refined fetched itself. Classes don't (inference uses their own
            // metadata for %This and their own type), so this is e.g. a library that Declares one of
            // its own functions: that call sees this unrefined copy and stays unrefined. Accepted
            // as rare. The copy is evicted with the cycle's other provisional entries, but the
            // refinement in progress doesn't otherwise depend on it (own members come from its own
            // metadata), so it isn't cut short.
            if (refining.Count > 0 && refining.Peek().Equals(metadata.QualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                MarkProvisional(metadata);
                return;
            }

            if (refining.Count >= MaxDepth
                || refining.Contains(metadata.QualifiedName, StringComparer.OrdinalIgnoreCase))
            {
                _cutShortCount++;
                MarkProvisional(metadata);
                return;
            }

            restore = Snapshot(metadata);
            int cutShortBefore = _cutShortCount;

            // Collects what this frame looks up, cache hits included; merged into the enclosing
            // frame on exit, so its dependencies cover what nested refinements were inferred from
            var parentDependencies = RefinementDependencyCollector.Current;
            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RefinementDependencyCollector.Current = dependencies;
            refining.Push(metadata.QualifiedName);
            try
            {
                // Writes same-program return and getter refinements into metadata (spec §9)
                var visitor = TypeInferenceVisitor.Run(program, metadata, resolver);
                RefineMembers(program, metadata, visitor);
            }
            finally
            {
                refining.Pop();
                RefinementDependencyCollector.Current = parentDependencies;
                parentDependencies?.UnionWith(dependencies);
            }

            dependencies.Remove(metadata.QualifiedName);
            metadata.RefinementDependencies = dependencies;

            if (_cutShortCount != cutShortBefore)
            {
                // The outermost result is cached by the resolver after this returns: it's the best
                // the cycle guard allows. Nested ones are evicted when the outermost finishes.
                if (outermost)
                    metadata.IsProvisional = true;
                else
                    MarkProvisional(metadata);
            }
        }
        catch (Exception)
        {
            // A refiner fault must never cost the caller its metadata: put back what was
            // extracted and treat it as final. The library has no Debug facility, so the
            // exception is intentionally swallowed (as CompileChecker does).
            if (restore != null)
                foreach (var undo in restore)
                    undo();
            metadata.IsProvisional = false;
        }
        finally
        {
            if (outermost)
                EvictProvisional(resolver);
        }
    }

    /// <summary>
    /// Marks nested metadata provisional and records it for eviction when the outermost
    /// refinement finishes. Until then it's cached, so repeated fetches during one top-level
    /// refinement are cache hits rather than re-parses.
    /// </summary>
    private static void MarkProvisional(TypeMetadata metadata)
    {
        metadata.IsProvisional = true;
        (_provisional ??= new List<string>()).Add(metadata.QualifiedName);
    }

    /// <summary>
    /// Removes the provisional metadata recorded during this top-level refinement from the
    /// resolver's cache, and resets the per-thread state.
    /// </summary>
    private static void EvictProvisional(ITypeMetadataResolver resolver)
    {
        try
        {
            if (_provisional != null)
                foreach (var name in _provisional)
                    resolver.Cache.Remove(name);
        }
        catch (Exception)
        {
            // Swallowed like the refiner's own faults; the worst case is a stale provisional entry
        }
        finally
        {
            _provisional?.Clear();
            _cutShortCount = 0;
        }
    }

    /// <summary>
    /// Opt-in (spec §9): true when a method or function, property or instance variable is a
    /// scalar ApiObject, Record or Field that is still unrefined.
    /// </summary>
    private static bool HasUnrefinedMember(ProgramNode program, TypeMetadata metadata)
    {
        // Interface methods have no bodies; they rely on header annotations
        if (metadata.Kind == ProgramKind.Interface)
            return false;

        return metadata.Methods.Values.Any(m =>
                !m.HasReturnAnnotation && m.RefinedReturnType == null && IsRefinable(m.ReturnType))
            || RefinableGetters(program, metadata).Any()
            || RefinableMembers(program, metadata).Any();
    }

    /// <summary>
    /// Properties with a getter that inference may refine from the getter's returns: the same
    /// condition as <c>TypeInferenceVisitor.VisitPropertyImpl</c>. Opt-in only; assignments don't
    /// refine these (<see cref="RefinableMembers"/>).
    /// </summary>
    private static IEnumerable<PropertyInfo> RefinableGetters(ProgramNode program, TypeMetadata metadata)
    {
        if (program.AppClass is not { IsInterface: false } appClass)
            yield break;

        foreach (var property in appClass.Properties)
        {
            if (property.HasGet
                && program.Annotations.ForType(property.Type) == null
                && metadata.Properties.TryGetValue(property.Name, out var info)
                && IsUnrefined(info))
                yield return info;
        }
    }

    /// <summary>
    /// The class's scalar ApiObject, Record and Field properties and instance variables that are
    /// still unrefined and unannotated. Properties with a getter are left to the getter's returns.
    /// </summary>
    private static IEnumerable<PropertyInfo> RefinableMembers(ProgramNode program, TypeMetadata metadata)
    {
        if (program.AppClass is not { IsInterface: false } appClass)
            yield break;

        foreach (var property in appClass.Properties)
        {
            if (!property.HasGet
                && program.Annotations.ForType(property.Type) == null
                && metadata.Properties.TryGetValue(property.Name, out var info)
                && IsUnrefined(info))
                yield return info;
        }

        // One multi-name instance declaration shares a PropertyInfo across its names
        var seen = new HashSet<PropertyInfo>(ReferenceEqualityComparer.Instance);
        foreach (var instance in appClass.InstanceVariables)
        {
            if (program.Annotations.ForType(instance.Type) != null)
                continue;
            foreach (var nameInfo in instance.NameInfos)
            {
                if (metadata.InstanceVariables.TryGetValue(nameInfo.Name, out var info)
                    && IsUnrefined(info)
                    && seen.Add(info))
                    yield return info;
            }
        }
    }

    /// <summary>
    /// Refines properties and instance variables from the values assigned to them anywhere in the
    /// class, with the same agreement rule as returns; disagreement leaves them unrefined, silently.
    /// </summary>
    private static void RefineMembers(ProgramNode program, TypeMetadata metadata, TypeInferenceVisitor visitor)
    {
        foreach (var member in RefinableMembers(program, metadata).ToList())
        {
            if (visitor.MemberAssignments.TryGetValue(member, out var values)
                && TypeInferenceVisitor.AgreedType(member.Type, values) is { } refined)
                member.RefinedType = refined;
        }
    }

    private static bool IsUnrefined(PropertyInfo info) =>
        info.RefinedType == null && IsRefinable(info.ToTypeWithDimensionality());

    private static bool IsRefinable(TypeWithDimensionality type) =>
        type.IsScalar && type.Type is PeopleCodeType.ApiObject or PeopleCodeType.Record or PeopleCodeType.Field;

    /// <summary>
    /// Undo actions that put every refinement slot back to its extracted value.
    /// </summary>
    private static List<Action> Snapshot(TypeMetadata metadata)
    {
        var restore = new List<Action>();
        foreach (var method in metadata.Methods.Values)
        {
            var extracted = method.RefinedReturnType;
            restore.Add(() => method.RefinedReturnType = extracted);
        }
        foreach (var member in metadata.Properties.Values.Concat(metadata.InstanceVariables.Values))
        {
            var extracted = member.RefinedType;
            restore.Add(() => member.RefinedType = extracted);
        }
        return restore;
    }
}
