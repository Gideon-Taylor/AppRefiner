using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Validation;

/// <summary>
/// Direct, non-graph validator that operates on FunctionInfo and Parameter trees.
/// Produces detailed failure info similar to the graph-based validator but is simpler to maintain.
/// </summary>
public class FunctionCallValidator
{
    private readonly ITypeMetadataResolver _typeResolver;

    /// <summary>
    /// Categorizes why validation failed at a given position. We distinguish between
    /// - MissingArgument: ran out of arguments while more parameter tokens were required (e.g., incomplete group)
    /// - TypeMismatch: an argument was present but didn't match any acceptable type at that position
    /// </summary>
    public enum FailureKind
    {
        None,
        TypeMismatch,
        MissingArgument
    }

    public FunctionCallValidator(ITypeMetadataResolver typeResolver)
    {
        _typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
    }

    /// <summary>
    /// Entry point for validating a concrete <see cref="FunctionInfo"/> using the direct (non-graph) strategy.
    /// </summary>
    public ValidationResult Validate(FunctionInfo functionInfo, TypeInfo[] argumentTypes)
    {
        return Validate(functionInfo.Parameters, argumentTypes, functionInfo.Name);
    }

    /// <summary>
    /// Validates a list of composable <see cref="Parameter"/>s against a provided argument vector.
    /// Implementation notes:
    /// - Walks parameters left-to-right and uses backtracking only where necessary:
    ///   - VariableParameter: tries repetition counts from maxPossible down to min
    ///   - UnionParameter: tries each allowed type branch
    /// - Produces rich diagnostics via MatchContext (furthest failure index, expected types, failure kind).
    /// - If traversal succeeds and we consumed all arguments, returns Success; otherwise builds a focused failure.
    /// </summary>
    public ValidationResult Validate(List<Parameter> parameters, TypeInfo[] argumentTypes, string functionName = "")
    {
        var ctx = new MatchContext(functionName, parameters, argumentTypes);
        var ok = MatchSequence(ctx, parameters, 0, 0, out int consumedIndex);
        if (ok && consumedIndex == argumentTypes.Length)
        {
            return ValidationResult.Success(consumedIndex);
        }

        // Build failure info from context
        var failedIndex = ctx.BestFailureArgIndex >= 0 ? ctx.BestFailureArgIndex : Math.Min(consumedIndex, argumentTypes.Length);
        var expected = ctx.GetExpectedTypesAtFailure();
        if (expected.Count == 0)
        {
            expected = BuildExpectedFromParameters(parameters, argumentTypes, 0, 0);
        }
        var found = failedIndex >= 0 && failedIndex < argumentTypes.Length
            ? FormatTypeInfoForDisplay(argumentTypes[failedIndex])
            : "";
        var defaultKind = failedIndex >= argumentTypes.Length ? FailureKind.MissingArgument : FailureKind.TypeMismatch;
        var kind = ctx.FailureKind == FailureKind.None ? defaultKind : ctx.FailureKind;
        var errors = ctx.Errors.Count > 0 ? ctx.Errors : new List<string> { kind == FailureKind.MissingArgument ? "Unexpected end of arguments" : "No valid parameter match" };
        return ValidationResult.Failure(failedIndex, expected, errors, found, kind);
    }

    /// <summary>
    /// Computes the set of allowed types for the next argument, given an existing prefix of argument types.
    /// - If the existing argument prefix is a valid prefix of the signature (including incomplete/missing args),
    ///   returns the allowed next token types at the next position.
    /// - If the existing arguments contain an invalid type mismatch, returns an empty list.
    /// </summary>
    public List<string> GetAllowedNextTypes(FunctionInfo functionInfo, TypeInfo[] argumentTypes)
    {
        return GetAllowedNextTypes(functionInfo.Parameters, argumentTypes);
    }

    /// <summary>
    /// Core implementation for computing allowed next types over a parameter sequence.
    /// </summary>
    public List<string> GetAllowedNextTypes(List<Parameter> parameters, TypeInfo[] argumentTypes)
    {
        var result = Validate(parameters, argumentTypes, "");
        if (!result.IsValid && result.FailureKind == FailureKind.MissingArgument)
        {
            return result.ExpectedTypesAtFailure.Distinct().ToList();
        }
        return new List<string>();
    }

    /// <summary>
    /// Recursively walks the parameter sequence to (a) verify the provided arguments form a valid prefix,
    /// and (b) when the prefix boundary is reached, collect the FIRST set of allowed next token types.
    /// </summary>
    private bool CollectNextTypesForPrefix(MatchContext ctx, List<Parameter> parameters, int paramIdx, int argIndex, HashSet<string> outTypes)
    {
        // If we've exhausted provided arguments, collect the FIRST set starting from the current parameter index.
        if (argIndex >= ctx.Arguments.Length)
        {
            AddStartTypesForSequence(parameters, paramIdx, outTypes);
            return true; // prefix is valid up to this point
        }

        // If we still have arguments but no parameters remain, the prefix is invalid (extra args)
        if (paramIdx >= parameters.Count)
        {
            return false;
        }

        var param = parameters[paramIdx];

        switch (param)
        {
            case SingleParameter single:
                {
                    var res = TryMatchOnePrefix(ctx, single, argIndex, out int used, out List<string> needed);
                    switch (res)
                    {
                        case PrefixMatchResult.Completed:
                            return CollectNextTypesForPrefix(ctx, parameters, paramIdx + 1, argIndex + used, outTypes);
                        case PrefixMatchResult.NeedsMoreArgs:
                            foreach (var t in needed) outTypes.Add(t);
                            return true;
                        default:
                            return false;
                    }
                }
            case UnionParameter union:
                {
                var res = TryMatchOnePrefix(ctx, union, argIndex, out int used, out List<string> needed);
                    switch (res)
                    {
                        case PrefixMatchResult.Completed:
                            return CollectNextTypesForPrefix(ctx, parameters, paramIdx + 1, argIndex + used, outTypes);
                        case PrefixMatchResult.NeedsMoreArgs:
                            foreach (var t in needed) outTypes.Add(t);
                            return true;
                        default:
                            return false;
                    }
                }
            case ParameterGroup group:
                {
                    var res = TryMatchOnePrefix(ctx, group, argIndex, out int used, out List<string> needed);
                    switch (res)
                    {
                        case PrefixMatchResult.Completed:
                            return CollectNextTypesForPrefix(ctx, parameters, paramIdx + 1, argIndex + used, outTypes);
                        case PrefixMatchResult.NeedsMoreArgs:
                            foreach (var t in needed) outTypes.Add(t);
                            return true;
                        default:
                            return false;
                    }
                }
            case VariableParameter variable:
                {
                    int idx = argIndex;
                    int count = 0;
                    while (count < variable.MaxCount && idx < ctx.Arguments.Length)
                    {
                        var res = TryMatchOnePrefix(ctx, variable.InnerParameter, idx, out int used, out List<string> needed);
                        if (res == PrefixMatchResult.Completed)
                        {
                            idx += used;
                            count++;
                            continue;
                        }
                        if (res == PrefixMatchResult.NeedsMoreArgs)
                        {
                            // We're at the boundary inside the inner repetition; report what comes next within the group
                            foreach (var t in needed) outTypes.Add(t);
                            return true;
                        }
                        // Type mismatch starting a new repetition
                        break;
                    }

                    // Reached boundary (no more provided args)
                    if (idx >= ctx.Arguments.Length)
                    {
                        if (count < Math.Max(0, variable.MinCount))
                        {
                            // Need at least one inner element next
                            AddStartTypesForParameter(variable.InnerParameter, outTypes);
                            return true;
                        }
                        // Min met: we can either continue the repetition or move on
                        if (count < variable.MaxCount)
                        {
                            AddStartTypesForParameter(variable.InnerParameter, outTypes);
                        }
                        AddStartTypesForSequence(parameters, paramIdx + 1, outTypes);
                        return true;
                    }

                    // Still have args and failed to match another repetition. If min not met → invalid; else continue to next parameter
                    if (count < Math.Max(0, variable.MinCount)) return false;
                    return CollectNextTypesForPrefix(ctx, parameters, paramIdx + 1, idx, outTypes);
                }
            default:
                return false;
        }
    }

    private enum PrefixMatchResult
    {
        Completed,
        NeedsMoreArgs,
        TypeMismatch
    }

    /// <summary>
    /// Attempts to match exactly one instance of <paramref name="parameter"/> starting at <paramref name="argIndex"/>,
    /// but treats running out of provided args as a valid prefix and returns the next expected types.
    /// </summary>
    private PrefixMatchResult TryMatchOnePrefix(MatchContext ctx, Parameter parameter, int argIndex, out int consumed, out List<string> nextTypes)
    {
        nextTypes = new List<string>();
        consumed = 0;
        switch (parameter)
        {
            case SingleParameter single:
                {
                    if (argIndex >= ctx.Arguments.Length)
                    {
                        nextTypes.Add(FormatTypePublic(single.ParameterType));
                        return PrefixMatchResult.NeedsMoreArgs;
                    }
                    if (MatchSingle(ctx, single, argIndex, out int used))
                    {
                        consumed = used;
                        return PrefixMatchResult.Completed;
                    }
                    return PrefixMatchResult.TypeMismatch;
                }
            case UnionParameter union:
            {
                if (argIndex >= ctx.Arguments.Length)
                {
                    nextTypes.AddRange(union.AllowedTypes.Select(FormatTypePublic));
                    return PrefixMatchResult.NeedsMoreArgs;
                }
                var arg = ctx.Arguments[argIndex];
                foreach (var t in union.AllowedTypes)
                {
                    // Handle reference types
                    if (t.IsReference)
                    {
                        if (arg is ReferenceTypeInfo refType)
                        {
                            if (t.Type == PeopleCodeType.Any || refType.ReferenceCategory == t.Type)
                            {
                                consumed = 1;
                                return PrefixMatchResult.Completed;
                            }
                        }
                        continue;
                    }

                    TypeInfo expectedTypeInfo = t.IsAppClass ? new AppClassTypeInfo(t.AppClassPath!) : TypeInfo.FromPeopleCodeType(t.Type);
                    if (t.IsArray)
                    {
                        expectedTypeInfo = new ArrayTypeInfo(t.ArrayDimensionality, expectedTypeInfo);
                    }
                    if (expectedTypeInfo.IsAssignableFrom(arg))
                    {
                        consumed = 1;
                        return PrefixMatchResult.Completed;
                    }
                }
                return PrefixMatchResult.TypeMismatch;
            }
            case ParameterGroup group:
                {
                    int idx = argIndex;
                    foreach (var child in group.Parameters)
                    {
                        if (idx >= ctx.Arguments.Length)
                        {
                            var tmp = new HashSet<string>();
                            AddStartTypesForParameter(child, tmp);
                            nextTypes = tmp.ToList();
                            return PrefixMatchResult.NeedsMoreArgs;
                        }
                        var res = TryMatchOnePrefix(ctx, child, idx, out int used, out List<string> needed);
                        switch (res)
                        {
                            case PrefixMatchResult.Completed:
                                idx += used;
                                break;
                            case PrefixMatchResult.NeedsMoreArgs:
                                nextTypes = needed;
                                return PrefixMatchResult.NeedsMoreArgs;
                            default:
                                return PrefixMatchResult.TypeMismatch;
                        }
                    }
                    consumed = idx - argIndex;
                    return PrefixMatchResult.Completed;
                }
            case VariableParameter variable:
                {
                    // Treat a single instance as its inner parameter's single instance
                    return TryMatchOnePrefix(ctx, variable.InnerParameter, argIndex, out consumed, out nextTypes);
                }
            case ReferenceParameter reference:
                {
                    if (argIndex >= ctx.Arguments.Length)
                    {
                        var refTypeName = reference.ReferenceCategory == PeopleCodeType.Any
                            ? "@ANY"
                            : $"@{reference.ReferenceCategory.GetTypeName().ToUpperInvariant()}";
                        nextTypes.Add(refTypeName);
                        return PrefixMatchResult.NeedsMoreArgs;
                    }
                    if (MatchReference(ctx, reference, argIndex, out int used))
                    {
                        consumed = used;
                        return PrefixMatchResult.Completed;
                    }
                    return PrefixMatchResult.TypeMismatch;
                }
            default:
                return PrefixMatchResult.TypeMismatch;
        }
    }

    /// <summary>
    /// Adds the FIRST set for a single parameter to the output set.
    /// </summary>
    private void AddStartTypesForParameter(Parameter parameter, HashSet<string> set)
    {
        switch (parameter)
        {
            case SingleParameter s:
                set.Add(FormatTypePublic(s.ParameterType));
                break;
            case UnionParameter u:
                foreach (var t in u.AllowedTypes) set.Add(FormatTypePublic(t));
                break;
            case ParameterGroup g:
                if (g.Parameters.Count > 0) AddStartTypesForParameter(g.Parameters[0], set);
                break;
            case VariableParameter v:
                AddStartTypesForParameter(v.InnerParameter, set);
                break;
            case ReferenceParameter r:
                var refTypeName = r.ReferenceCategory == PeopleCodeType.Any
                    ? "@ANY"
                    : $"@{r.ReferenceCategory.GetTypeName().ToUpperInvariant()}";
                set.Add(refTypeName);
                break;
        }
    }

    /// <summary>
    /// Adds the FIRST set for a parameter sequence starting at <paramref name="startParamIdx"/>,
    /// including epsilon-closures over optional/zero-min variable parameters.
    /// </summary>
    private void AddStartTypesForSequence(List<Parameter> parameters, int startParamIdx, HashSet<string> set)
    {
        int idx = startParamIdx;
        while (idx < parameters.Count)
        {
            var p = parameters[idx];
            AddStartTypesForParameter(p, set);

            if (p is VariableParameter v && v.MinCount == 0)
            {
                // Optional/zero-minimal variable: we can also start at the next parameter
                idx++;
                continue;
            }
            break;
        }
    }

    /// <summary>
    /// Derives a best-effort list of expected types when no contextual expectation was recorded.
    /// This is a fallback to hint at likely first tokens from the current and next parameter
    /// (especially when optional/zero-min varargs are present).
    /// </summary>
    private List<string> BuildExpectedFromParameters(List<Parameter> parameters, TypeInfo[] args, int paramIdx, int argIdx)
    {
        var set = new HashSet<string>();
        if (paramIdx >= parameters.Count) return set.ToList();

        void AddFrom(Parameter p)
        {
            switch (p)
            {
                case SingleParameter s:
                    set.Add(FormatTypePublic(s.ParameterType));
                    break;
                case UnionParameter u:
                    foreach (var t in u.AllowedTypes) set.Add(FormatTypePublic(t));
                    break;
                case ParameterGroup g:
                    if (g.Parameters.Count > 0) AddFrom(g.Parameters[0]);
                    break;
                case VariableParameter v:
                    AddFrom(v.InnerParameter);
                    break;
                case ReferenceParameter r:
                    var refTypeName = r.ReferenceCategory == PeopleCodeType.Any
                        ? "@ANY"
                        : $"@{r.ReferenceCategory.GetTypeName().ToUpperInvariant()}";
                    set.Add(refTypeName);
                    break;
            }
        }

        // From current parameter
        AddFrom(parameters[paramIdx]);
        // If current is optional/vararg with min 0, also include from the next parameter
        if (parameters[paramIdx] is VariableParameter vparam && vparam.MinCount == 0 && paramIdx + 1 < parameters.Count)
        {
            AddFrom(parameters[paramIdx + 1]);
        }
        return set.ToList();
    }

    /// <summary>
    /// Formats a <see cref="TypeWithDimensionality"/> to the display tokens used in tests and errors.
    /// Reference-like types are shown with '@' (upper-cased), others with their literal (e.g., "&string").
    /// </summary>
    private static string FormatTypePublic(TypeWithDimensionality t)
    {
        var s = t.ToString();
        if (s.StartsWith("@")) return "@" + s.Substring(1).ToUpperInvariant();
        return s;
    }

    /// <summary>
    /// Formats a runtime <see cref="TypeInfo"/> as the 'Found:' token at failure sites.
    /// Mirrors the display style used for expected types.
    /// </summary>
    private static string FormatTypeInfoForDisplay(TypeInfo typeInfo)
    {
        // Handle reference types first - they have their own Name formatting
        if (typeInfo is ReferenceTypeInfo)
        {
            return typeInfo.Name;
        }

        if (typeInfo.PeopleCodeType.HasValue)
        {
            var pct = typeInfo.PeopleCodeType.Value;
            // Reference-like types use '@'
            switch (pct)
            {
                case PeopleCodeType.Field:
                case PeopleCodeType.Record:
                case PeopleCodeType.Scroll:
                case PeopleCodeType.Row:
                case PeopleCodeType.Rowset:
                case PeopleCodeType.Page:
                case PeopleCodeType.Grid:
                case PeopleCodeType.Chart:
                case PeopleCodeType.Panel:
                    return "@" + pct.ToString().ToUpperInvariant();
                default:
                    return pct.ToString().ToLowerInvariant();
            }
        }
        return typeInfo.Name;
    }

    /// <summary>
    /// Core recursive matcher for a parameter sequence starting at <paramref name="paramIdx"/>,
    /// consuming arguments from <paramref name="argIndex"/>. Returns the next argument index if successful.
    ///
    /// Failure policy: Instead of failing fast, we record expectations and keep the deepest failure index,
    /// which yields more actionable diagnostics.
    /// </summary>
    private bool MatchSequence(MatchContext ctx, List<Parameter> parameters, int paramIdx, int argIndex, out int nextArgIndex)
    {
        // Base case: no more parameters to match
        if (paramIdx >= parameters.Count)
        {
            nextArgIndex = argIndex;
            return true;
        }

        var param = parameters[paramIdx];

        // Handle variable parameter with backtracking on repetition count
        if (param is VariableParameter variable)
        {
            // Compute the maximum repetitions we can attempt given remaining arguments
            int minCount = Math.Max(0, variable.MinCount);
            int maxCount = variable.MaxCount;

            // Heuristic: try higher counts first to reduce backtracking depth
            // Also cap by available args assuming inner consumes at least 1 arg when non-empty
            int maxPossible = maxCount;
            if (variable.InnerParameter.MinArgumentCount > 0)
            {
                var remainingArgs = ctx.Arguments.Length - argIndex;
                maxPossible = Math.Min(maxPossible, remainingArgs / variable.InnerParameter.MinArgumentCount);
            }

            // If even the maximum possible repetitions cannot meet the minimum, record the next expected inner token
            if (maxPossible < minCount)
            {
                RecordInnerExpectationForShortfall(ctx, variable.InnerParameter, argIndex);
                ctx.MarkMissingArgument(argIndex);
                nextArgIndex = argIndex;
                return false;
            }

            for (int count = maxPossible; count >= minCount; count--)
            {
                int innerIndex = argIndex;
                bool innerOk = true;
                for (int rep = 0; rep < count; rep++)
                {
                    if (!MatchParameter(ctx, variable.InnerParameter, innerIndex, out int used))
                    {
                        innerOk = false;
                        break;
                    }
                    innerIndex += used;
                }

                if (!innerOk)
                {
                    continue; // try fewer repetitions
                }

                // After committing 'count' repetitions, continue with the rest of the sequence
                if (MatchSequence(ctx, parameters, paramIdx + 1, innerIndex, out nextArgIndex))
                {
                    return true;
                }

                // ENHANCEMENT: Record expectations for both continuation paths when we've met minimum requirements
                if (count >= minCount)
                {
                    // If we could accept more repetitions of this variable and we've run out of arguments, record that expectation
                    if (count < variable.MaxCount && innerIndex >= ctx.Arguments.Length)
                    {
                        ctx.RecordExpectation(innerIndex, variable.InnerParameter);
                    }
                }
            }

            // Could not make variable parameter fit. If this variable has a required minimum,
            // project the next expected inner token at the current index for clarity. For optional (min=0),
            // defer to the expectations recorded by the remainder of the sequence.
            if (variable.MinCount > 0)
            {
                RecordInnerExpectationForShortfall(ctx, variable.InnerParameter, argIndex);
            }
            nextArgIndex = argIndex;
            return false;
        }

        // Non-variable: attempt to match and advance, with backtracking handled by recursion on the rest
        if (MatchParameter(ctx, param, argIndex, out int consumed))
        {
            if (MatchSequence(ctx, parameters, paramIdx + 1, argIndex + consumed, out nextArgIndex))
            {
                return true;
            }
        }

        // Failed here; record expectation for diagnostics and return
        ctx.RecordExpectation(argIndex, param);
        nextArgIndex = argIndex;
        return false;
    }

    /// <summary>
    /// Dispatch for a single <see cref="Parameter"/> node.
    /// Performs bounds checking and funnels error recording via the specialized matchers.
    /// </summary>
    private bool MatchParameter(MatchContext ctx, Parameter parameter, int argIndex, out int consumed)
    {
        consumed = 0;
        if (argIndex > ctx.Arguments.Length)
        {
            ctx.RecordError(argIndex, "Out of bounds");
            return false;
        }

        switch (parameter)
        {
            case SingleParameter single:
                return MatchSingle(ctx, single, argIndex, out consumed);
            case UnionParameter union:
                return MatchUnion(ctx, union, argIndex, out consumed);
            case ParameterGroup group:
                return MatchGroup(ctx, group, argIndex, out consumed);
            case VariableParameter variable:
                return MatchVariable(ctx, variable, argIndex, out consumed);
            case ReferenceParameter reference:
                return MatchReference(ctx, reference, argIndex, out consumed);
            default:
                ctx.RecordError(argIndex, $"Unknown parameter type: {parameter.GetType()}");
                return false;
        }
    }

    /// <summary>
    /// Matches a concrete single token (e.g., &string, @RECORD).
    /// Classifies end-of-args as MissingArgument; mismatches as TypeMismatch.
    /// </summary>
    private bool MatchSingle(MatchContext ctx, SingleParameter single, int argIndex, out int consumed)
    {
        consumed = 0;
        if (argIndex >= ctx.Arguments.Length)
        {
            ctx.RecordMissingArgument(argIndex, single);
            return false;
        }

        var arg = ctx.Arguments[argIndex];
        // Reuse TypeNode acceptance logic equivalently
        var expected = single.ParameterType;
        TypeInfo expectedTypeInfo = expected.IsAppClass
            ? new AppClassTypeInfo(expected.AppClassPath!)
            : TypeInfo.FromPeopleCodeType(expected.Type);
        if (expected.IsArray)
        {
            expectedTypeInfo = new ArrayTypeInfo(expected.ArrayDimensionality, expectedTypeInfo);
        }

        // Check AppClass inheritance if both are AppClass types
        if (expectedTypeInfo is AppClassTypeInfo expectedAppClass && arg is AppClassTypeInfo argAppClass)
        {
            if (IsAppClassCompatible(expectedAppClass, argAppClass))
            {
                consumed = 1;
                return true;
            }
        }
        else if (expectedTypeInfo.IsAssignableFrom(arg))
        {
            consumed = 1;
            return true;
        }

        /* We don't know what type it is. it'll be figured out at runtime */
        if (arg.PeopleCodeType == PeopleCodeType.Unknown)
        {
            consumed = 1;
            return true;
        }

        ctx.RecordTypeMismatch(argIndex, single);
        return false;
    }

    /// <summary>
    /// Matches a union token by trying each allowed type. End-of-args → MissingArgument, otherwise TypeMismatch.
    /// </summary>
    private bool MatchUnion(MatchContext ctx, UnionParameter union, int argIndex, out int consumed)
    {
        consumed = 0;
        if (argIndex >= ctx.Arguments.Length)
        {
            ctx.RecordMissingArgument(argIndex, union);
            return false;
        }

        var arg = ctx.Arguments[argIndex];

        // Backtracking across union alternatives
        foreach (var t in union.AllowedTypes)
        {
            // Handle reference types
            if (t.IsReference)
            {
                if (arg is ReferenceTypeInfo refType)
                {
                    // Check if reference category matches (or if ANY is allowed)
                    if (t.Type == PeopleCodeType.Any || refType.ReferenceCategory == t.Type || refType.ReferenceCategory == PeopleCodeType.Any)
                    {
                        consumed = 1;
                        return true;
                    }
                }
                continue; // Reference type expected but not matched
            }

            // Handle regular types (instances, not references)
            TypeInfo expectedTypeInfo = t.IsAppClass ? new AppClassTypeInfo(t.AppClassPath!) : TypeInfo.FromPeopleCodeType(t.Type);
            if (t.IsArray)
            {
                expectedTypeInfo = new ArrayTypeInfo(t.ArrayDimensionality, expectedTypeInfo);
            }

            // Check AppClass inheritance if both are AppClass types
            bool isMatch = false;
            if (expectedTypeInfo is AppClassTypeInfo expectedAppClass && arg is AppClassTypeInfo argAppClass)
            {
                isMatch = IsAppClassCompatible(expectedAppClass, argAppClass);
            }
            else
            {
                isMatch = expectedTypeInfo.IsAssignableFrom(arg);
            }

            if (!isMatch)
            {
                continue;
            }

            // If this union consumes 1 and the remainder of the outer sequence matches, accept this branch.
            consumed = 1;
            return true;
        }

        ctx.RecordTypeMismatch(argIndex, union);
        return false;
    }

    /// <summary>
    /// Matches a parameter group by consuming its children in order. If we run out of args midway,
    /// we classify as MissingArgument at the first unmet child.
    /// </summary>
    private bool MatchGroup(MatchContext ctx, ParameterGroup group, int argIndex, out int consumed)
    {
        int idx = argIndex;
        foreach (var child in group.Parameters)
        {
            if (idx >= ctx.Arguments.Length)
            {
                ctx.RecordMissingArgument(idx, child);
                consumed = 0;
                return false;
            }
            if (!MatchParameter(ctx, child, idx, out int used))
            {
                consumed = 0;
                return false;
            }
            idx += used;
        }
        consumed = idx - argIndex;
        return true;
    }

    /// <summary>
    /// Matches a variable parameter (repetition) by greedily trying the highest feasible repetition count first,
    /// then backtracking downward until the remainder of the sequence matches. When the minimum cannot be met,
    /// we project the next expected token and mark MissingArgument.
    /// </summary>
    private bool MatchVariable(MatchContext ctx, VariableParameter variable, int argIndex, out int consumed)
    {
        consumed = 0;
        int index = argIndex;
        int count = 0;

        // Try to match as many repetitions as possible up to MaxCount
        while (count < variable.MaxCount)
        {
            if (!MatchParameter(ctx, variable.InnerParameter, index, out int used))
            {
                break;
            }
            index += used;
            count++;
        }

        if (count < variable.MinCount)
        {
            // If minimum not met, try to project the next expected token within the inner parameter at the furthest index we could reach
            RecordInnerExpectationForShortfall(ctx, variable.InnerParameter, index);
            ctx.MarkMissingArgument(index);
            return false;
        }

        consumed = index - argIndex;
        return true;
    }

    /// <summary>
    /// Matches a reference parameter (e.g., @RECORD, @FIELD, @ANY).
    /// References must be ReferenceTypeInfo with matching category.
    /// </summary>
    private bool MatchReference(MatchContext ctx, ReferenceParameter reference, int argIndex, out int consumed)
    {
        consumed = 0;
        if (argIndex >= ctx.Arguments.Length)
        {
            ctx.RecordMissingArgument(argIndex, reference);
            return false;
        }

        var arg = ctx.Arguments[argIndex];

        // Must be a reference type
        if (arg is not ReferenceTypeInfo refType)
        {
            ctx.RecordTypeMismatch(argIndex, reference);
            return false;
        }

        // Check category match

        /* If the actual type is an @ANY, this means @() was used in the code. we cannot know it statically
         * so just say its OK 
         
         if the argument accepts any ref type, then we just say its OK
         */
        if (refType.ReferenceCategory == PeopleCodeType.Any || reference.ReferenceCategory == PeopleCodeType.Any)
        {
            consumed = 1;
            return true;
        }

        if (reference.ReferenceCategory != PeopleCodeType.Any &&
            refType.ReferenceCategory != reference.ReferenceCategory)
        {

            ctx.RecordTypeMismatch(argIndex, reference);
            return false;
        }

        consumed = 1;
        return true;
    }

    /// <summary>
    /// Projects the next expected type(s) of <paramref name="inner"/> at the furthest point we could reach with
    /// the available arguments. This helps produce precise expected types when we have a shortfall inside a group.
    /// </summary>
    private void RecordInnerExpectationForShortfall(MatchContext ctx, Parameter inner, int startIndex)
    {
        int idx = startIndex;
        // Walk as far as possible inside 'inner' consuming simple elements, then record expectation for the next element
        if (inner is ParameterGroup group)
        {
            foreach (var child in group.Parameters)
            {
                if (idx >= ctx.Arguments.Length)
                {
                    ctx.RecordExpectation(idx, child);
                    return;
                }

                var currentArg = ctx.Arguments[idx];
                if (Accepts(child, currentArg))
                {
                    idx += 1; // progress one argument
                    continue;
                }
                else
                {
                    ctx.RecordExpectation(idx, child);
                    return;
                }
            }
            // If we fully matched inner, fall back to recording at start
            ctx.RecordExpectation(startIndex, inner);
            return;
        }

        // For non-group, if we have no arg left or it doesn't fit, record at current index
        ctx.RecordExpectation(idx, inner);
    }

    /// <summary>
    /// Lightweight predicate to check whether a parameter would accept a given runtime type.
    /// Used for expectation projection; does not mutate traversal state.
    /// </summary>
    private bool Accepts(Parameter parameter, TypeInfo arg)
    {
        switch (parameter)
        {
            case SingleParameter single:
                {
                    var expected = single.ParameterType;

                    // Handle reference types
                    if (expected.IsReference)
                    {
                        if (arg is not ReferenceTypeInfo refType)
                            return false;
                        if (expected.Type == PeopleCodeType.Any)
                            return true;
                        return refType.ReferenceCategory == expected.Type;
                    }

                    TypeInfo expectedTypeInfo = expected.IsAppClass
                        ? new AppClassTypeInfo(expected.AppClassPath!)
                        : TypeInfo.FromPeopleCodeType(expected.Type);
                    if (expected.IsArray)
                    {
                        expectedTypeInfo = new ArrayTypeInfo(expected.ArrayDimensionality, expectedTypeInfo);
                    }

                    // Check AppClass inheritance if both are AppClass types
                    if (expectedTypeInfo is AppClassTypeInfo expectedAppClass && arg is AppClassTypeInfo argAppClass)
                    {
                        return IsAppClassCompatible(expectedAppClass, argAppClass);
                    }

                    return expectedTypeInfo.IsAssignableFrom(arg);
                }
            case UnionParameter union:
                {
                    foreach (var t in union.AllowedTypes)
                    {
                        // Handle reference types
                        if (t.IsReference)
                        {
                            if (arg is ReferenceTypeInfo refType)
                            {
                                if (t.Type == PeopleCodeType.Any || refType.ReferenceCategory == t.Type)
                                    return true;
                            }
                            continue;
                        }

                        TypeInfo expectedTypeInfo = t.IsAppClass ? new AppClassTypeInfo(t.AppClassPath!) : TypeInfo.FromPeopleCodeType(t.Type);
                        if (t.IsArray)
                        {
                            expectedTypeInfo = new ArrayTypeInfo(t.ArrayDimensionality, expectedTypeInfo);
                        }

                        // Check AppClass inheritance if both are AppClass types
                        if (expectedTypeInfo is AppClassTypeInfo expectedAppClass && arg is AppClassTypeInfo argAppClass)
                        {
                            if (IsAppClassCompatible(expectedAppClass, argAppClass))
                                return true;
                        }
                        else if (expectedTypeInfo.IsAssignableFrom(arg))
                        {
                            return true;
                        }
                    }
                    return false;
                }
            case ParameterGroup group:
                return group.Parameters.Count > 0 && Accepts(group.Parameters[0], arg);
            case VariableParameter variable:
                return Accepts(variable.InnerParameter, arg);
            case ReferenceParameter reference:
                {
                    if (arg is not ReferenceTypeInfo refType)
                        return false;

                    if (reference.ReferenceCategory == PeopleCodeType.Any)
                        return true;

                    return refType.ReferenceCategory == reference.ReferenceCategory;
                }
            default:
                return false;
        }
    }

    /// <summary>
    /// Check if a value of valueType can be assigned to a variable of targetType,
    /// considering the AppClass inheritance hierarchy.
    /// </summary>
    /// <param name="targetType">The target type (e.g., parameter type)</param>
    /// <param name="valueType">The value type being passed</param>
    /// <returns>True if valueType or any of its base classes match targetType</returns>
    private bool IsAppClassCompatible(AppClassTypeInfo targetType, AppClassTypeInfo valueType)
    {
        // Direct match
        if (valueType.QualifiedName.Equals(targetType.QualifiedName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Walk up the inheritance chain of valueType to see if we find targetType
        var currentClassName = valueType.QualifiedName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentClassName };

        while (!string.IsNullOrEmpty(currentClassName))
        {
            // Try to get metadata for the current class
            var metadata = _typeResolver.GetTypeMetadata(currentClassName);
            if (metadata == null)
            {
                // Can't resolve metadata, assume incompatible
                break;
            }

            // Check if this class has a base class
            if (string.IsNullOrEmpty(metadata.BaseClassName))
            {
                // Reached the top of the hierarchy without finding targetType
                break;
            }

            // Check if the base class matches our target
            if (metadata.BaseClassName.Equals(targetType.QualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Move to the base class for next iteration
            currentClassName = metadata.BaseClassName;

            // Circular inheritance detection
            if (!visited.Add(currentClassName))
            {
                // Detected circular inheritance, bail out
                break;
            }
        }

        return false;
    }

    /// <summary>
    /// Collects traversal state for diagnostics: furthest failure index, expected types by index,
    /// and failure kind hints. All recorders update BestFailureArgIndex so we can report the most
    /// useful failing position.
    /// </summary>
    private class MatchContext
    {
        public string FunctionName { get; }
        public List<Parameter> Parameters { get; }
        public TypeInfo[] Arguments { get; }
        public int BestFailureArgIndex { get; private set; } = -1;
        private readonly Dictionary<int, HashSet<string>> _expectedAtIndex = new();
        public List<string> Errors { get; } = new();
        public FailureKind FailureKind { get; private set; } = FailureKind.None;

        public MatchContext(string functionName, List<Parameter> parameters, TypeInfo[] arguments)
        {
            FunctionName = functionName;
            Parameters = parameters;
            Arguments = arguments;
        }

        public void RecordError(int argIndex, string message)
        {
            if (argIndex > BestFailureArgIndex) BestFailureArgIndex = argIndex;
            Errors.Add(message);
        }

        public void RecordExpectation(int argIndex, Parameter parameter)
        {
            if (argIndex > BestFailureArgIndex) BestFailureArgIndex = argIndex;
            var set = GetOrCreate(argIndex);
            foreach (var t in GetAcceptableTypes(parameter)) set.Add(t);
        }

        public void RecordUnionExpectation(int argIndex, UnionParameter union)
        {
            if (argIndex > BestFailureArgIndex) BestFailureArgIndex = argIndex;
            var set = GetOrCreate(argIndex);
            foreach (var t in union.AllowedTypes) set.Add(FormatType(t));
        }

        /// <summary>
        /// Records a missing argument at a position where a token is required but none was supplied.
        /// Also stores the expected type(s) for that token.
        /// </summary>
        public void RecordMissingArgument(int argIndex, Parameter parameter)
        {
            FailureKind = FailureKind.MissingArgument;
            RecordExpectation(argIndex, parameter);
            Errors.Add("Unexpected end of arguments");
        }

        /// <summary>
        /// Records a type mismatch at a position where an argument exists but doesn't match any allowed type.
        /// </summary>
        public void RecordTypeMismatch(int argIndex, Parameter parameter)
        {
            if (FailureKind == FailureKind.None)
            {
                FailureKind = FailureKind.TypeMismatch;
            }
            RecordExpectation(argIndex, parameter);
        }

        /// <summary>
        /// Marks the failure kind as MissingArgument and advances BestFailureArgIndex if appropriate.
        /// Use this when a higher-level construct (e.g., variable) determines an argument shortfall.
        /// </summary>
        public void MarkMissingArgument(int argIndex)
        {
            FailureKind = FailureKind.MissingArgument;
            if (argIndex > BestFailureArgIndex) BestFailureArgIndex = argIndex;
        }

        public List<string> GetExpectedTypesAtFailure()
        {
            if (BestFailureArgIndex < 0) return new List<string>();
            if (_expectedAtIndex.TryGetValue(BestFailureArgIndex, out var set))
            {
                return set.ToList();
            }
            return new List<string>();
        }

        private HashSet<string> GetOrCreate(int idx)
        {
            if (!_expectedAtIndex.TryGetValue(idx, out var set))
            {
                set = new HashSet<string>();
                _expectedAtIndex[idx] = set;
            }
            return set;
        }

        private static IEnumerable<string> GetAcceptableTypes(Parameter parameter)
        {
            switch (parameter)
            {
                case SingleParameter single:
                    return new[] { FormatType(single.ParameterType) };
                case UnionParameter union:
                    return union.AllowedTypes.Select(FormatType);
                case ParameterGroup group:
                    // Expected types come from the first element of the group
                    if (group.Parameters.Count == 0) return Array.Empty<string>();
                    return GetAcceptableTypes(group.Parameters[0]);
                case VariableParameter variable:
                    // Expected types come from the inner parameter's first token
                    return GetAcceptableTypes(variable.InnerParameter);
                case ReferenceParameter reference:
                    // Format reference parameter as @TYPE
                    var refTypeName = reference.ReferenceCategory == PeopleCodeType.Any
                        ? "@ANY"
                        : $"@{reference.ReferenceCategory.GetTypeName().ToUpperInvariant()}";
                    return new[] { refTypeName };
                default:
                    return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Converts a <see cref="TypeWithDimensionality"/> to the externally-visible token used in errors.
        /// </summary>
        private static string FormatType(TypeWithDimensionality t)
        {
            var s = t.ToString();
            if (s.StartsWith("@"))
            {
                return "@" + s.Substring(1).ToUpperInvariant();
            }
            return s;
        }
    }
}

/// <summary>
/// Result container for direct validation. Mirrors the fields returned by the graph validator,
/// with additional structured information about why validation failed.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public int FailedAtArgumentIndex { get; set; } = -1;
    public List<string> ExpectedTypesAtFailure { get; set; } = new();
    public List<string> ErrorMessages { get; set; } = new();
    public string FoundTypeAtFailure { get; set; } = "";
    public FunctionCallValidator.FailureKind FailureKind { get; set; } = FunctionCallValidator.FailureKind.None;

    /// <summary>
    /// Builds a human-friendly error message that includes position, expected types, and found type (if any).
    /// </summary>
    public string GetDetailedError()
    {
        if (IsValid) return "Validation successful";
        var parts = new List<string>();
        if (FailedAtArgumentIndex >= 0)
        {
            parts.Add($"Parameter validation failed at argument {FailedAtArgumentIndex + 1}");
            if (ExpectedTypesAtFailure.Any())
            {
                parts.Add($"Expected: {string.Join(" | ", ExpectedTypesAtFailure)}");
            }
            if (!string.IsNullOrEmpty(FoundTypeAtFailure))
            {
                parts.Add($"Found: {FoundTypeAtFailure}");
            }
        }
        parts.AddRange(ErrorMessages);
        return string.Join(". ", parts);
    }

    public static ValidationResult Success(int consumed)
    {
        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// Factory for a failure result with all contextual information.
    /// </summary>
    public static ValidationResult Failure(int failedAtIndex, List<string> expected, List<string> errors, string found, FunctionCallValidator.FailureKind kind)
    {
        return new ValidationResult
        {
            IsValid = false,
            FailedAtArgumentIndex = failedAtIndex,
            ExpectedTypesAtFailure = expected,
            ErrorMessages = errors,
            FoundTypeAtFailure = found,
            FailureKind = kind
        };
    }
}

/// <summary>
/// Convenience extensions for invoking the direct validator from existing <see cref="FunctionInfo"/> objects.
/// </summary>
public static class DirectParameterNextTypesExtensions
{
    public static ValidationResult ValidateDirect(this FunctionInfo functionInfo, TypeInfo[] argumentTypes, ITypeMetadataResolver typeResolver)
    {
        var validator = new FunctionCallValidator(typeResolver);
        return validator.Validate(functionInfo, argumentTypes);
    }

    public static bool IsValidCallDirect(this FunctionInfo functionInfo, TypeInfo[] argumentTypes, ITypeMetadataResolver typeResolver)
    {
        return functionInfo.ValidateDirect(argumentTypes, typeResolver).IsValid;
    }

    /// <summary>
    /// Convenience extension: returns the allowed next types for this function given an argument prefix.
    /// Empty list indicates the prefix is invalid.
    /// </summary>
    public static List<string> GetAllowedNextTypes(this FunctionInfo functionInfo, TypeInfo[] argumentTypes, ITypeMetadataResolver typeResolver)
    {
        var validator = new FunctionCallValidator(typeResolver);
        return validator.GetAllowedNextTypes(functionInfo, argumentTypes);
    }
}


