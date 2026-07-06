using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Extracts a selected run of statements into a new function (non-app-class
    /// program / main block) or private method (app class). Live-in variables
    /// become parameters; live-out variables are returned via a single Returns
    /// value and/or `out` parameters. Requires a selection covering whole
    /// statements — cursor position alone cannot delimit the range.
    /// </summary>
    public class ExtractFunctionMethod : BaseRefactor
    {
        public new static string RefactorName => "Extract Function/Method";
        public new static string RefactorDescription => "Extracts the selected statements into a new function or method";
        public new static bool RegisterKeyboardShortcut => false;

        public override bool RequiresUserInputDialog => true;
        public override bool DeferDialogUntilAfterVisitor => true;
        public override bool RequiresTypeInference => true;
        public override bool RunOnIncompleteParse => false;

        private ScopeContext? containingScope;
        private BlockNode? containingBlock;
        private readonly List<StatementNode> selectedStatements = new();

        private enum ParamRole { Value, InOut, Out }

        private sealed class ParamPlan
        {
            public required VariableInfo Var;
            public required ParamRole Role;
            public int OrderKey;
            public bool DeclaredInside;
            public string Name => Var.Name.StartsWith('&') ? Var.Name : "&" + Var.Name;
            public string TypeName => string.IsNullOrWhiteSpace(Var.Type) ? "any" : Var.Type;
        }

        private readonly List<ParamPlan> paramPlans = new();
        private readonly List<ParamPlan> returnCandidates = new();
        // Variables declared BEFORE the range but used only INSIDE it (their incoming value
        // is dead — first in-range use is a write — and they are not read after the range).
        // The canonical case is a For-loop iterator. These are neither parameters nor returns:
        // they need a fresh `Local <type> &x;` at the top of the extracted body so the routine
        // is self-contained. No caller-side or signature change.
        private readonly List<VariableInfo> internalLocalsToDeclare = new();
        private ParamPlan? returnChoice;
        private string routineName = "ExtractedFunction";

        // App-class method extraction (Task 5). When the program is an app class the
        // refactor emits a method (declaration + implementation + %This. call) instead
        // of a free function.
        private bool isAppClass;
        private AppClassNode? appClass;
        private VisibilityModifier chosenVisibility = VisibilityModifier.Private;

        public ExtractFunctionMethod(ScintillaEditor editor) : base(editor) { }

        private int RangeStart => selectedStatements[0].SourceSpan.Start.ByteIndex;
        private int RangeEnd => IncludeTrailingSemicolon(selectedStatements[^1].SourceSpan.End.ByteIndex);

        // A statement's SourceSpan.End stops before its terminating ';' — the parser
        // consumes the semicolon separately (tracked only via HasSemicolon). Extend the
        // range end to cover a trailing ';' so the extracted body keeps it AND the
        // call-site replacement consumes the original ';' (otherwise the original ';'
        // is left behind, doubling it at the call site). Conservative: if the span
        // already ends past a ';' the next non-blank byte isn't ';', so nothing changes.
        private int IncludeTrailingSemicolon(int end)
        {
            int i = end;
            while (i < SourceBytes.Length && (SourceBytes[i] == (byte)' ' || SourceBytes[i] == (byte)'\t'))
                i++;
            return (i < SourceBytes.Length && SourceBytes[i] == (byte)';') ? i + 1 : end;
        }

        private bool InRange(SourceSpan s) => s.Start.ByteIndex >= RangeStart && s.End.ByteIndex <= RangeEnd;
        private bool BeforeRange(SourceSpan s) => s.Start.ByteIndex < RangeStart;
        private bool AfterRange(SourceSpan s) => s.Start.ByteIndex >= RangeEnd;

        protected override void OnReset()
        {
            containingScope = null;
            containingBlock = null;
            selectedStatements.Clear();
            paramPlans.Clear();
            returnCandidates.Clear();
            internalLocalsToDeclare.Clear();
            returnChoice = null;
            routineName = "ExtractedFunction";
            isAppClass = false;
            appClass = null;
            chosenVisibility = VisibilityModifier.Private;
        }

        public override void VisitBlock(BlockNode node)
        {
            // Deepest block containing the selection wins. Blocks don't introduce
            // scopes, so GetCurrentScope() is the enclosing method/function/getter/
            // setter scope — captured here because scope contexts are gone by the
            // time OnExitGlobalScope runs the location logic.
            if (HasSelection && node.SourceSpan.ContainsPosition(SelectionStart))
            {
                containingScope = GetCurrentScope();
            }
            base.VisitBlock(node);
        }

        protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, object> customData)
        {
            // Top-level program statements run in the global scope, which VisitBlock's
            // GetCurrentScope() doesn't capture (it only yields enclosing method/function/
            // getter/setter scopes). Fall back to the global scope handed to us here so
            // extraction works for a block of top-level statements too.
            containingScope ??= scope;
            LocateStatementRange(node);
            if (selectedStatements.Count == 0) return; // SetFailure already called
            if (!PassesSafetyGuards()) return;         // SetFailure already called

            // App-class context: extract a method rather than a free function.
            appClass = node.AppClass;
            isAppClass = appClass != null;
            if (isAppClass) routineName = "ExtractedMethod";

            AnalyzeDataFlow();
        }

        public override bool ShowRefactorDialog()
        {
            if (selectedStatements.Count == 0) return false; // failure already set

            using var dialog = new ExtractRoutineDialog(routineName, returnCandidates, returnChoice,
                p => BuildPreviewTail(p), IsRoutineNameTaken);
            dialog.ShowVisibility = isAppClass;
            var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
            if (dialog.ShowDialog(wrapper) != DialogResult.OK) return false;

            routineName = dialog.RoutineName;
            returnChoice = dialog.ReturnChoice;
            chosenVisibility = dialog.Visibility;

            MoveOutputsIntoParams();
            if (isAppClass)
            {
                GenerateMethod();
            }
            else
            {
                if (!PassesFunctionRenameCollisionGuard()) return false; // SetFailure already called
                GenerateFunction();
            }
            return true;
        }

        // Live signature-tail preview for the dialog. `preview` is the tentative
        // return choice; the dialog prepends "Function "/"method " + the name.
        private string BuildPreviewTail(ParamPlan? preview)
        {
            // Recompute params for the tentative choice without mutating state.
            var tentative = paramPlans.ToList();
            foreach (var o in returnCandidates.Where(c => c != preview))
                tentative.Add(new ParamPlan { Var = o.Var, Role = ParamRole.Out, OrderKey = o.OrderKey, DeclaredInside = o.DeclaredInside });
            string prms = string.Join(", ", tentative.Select(RenderSigParam));
            string ret = preview != null ? $" Returns {preview.TypeName}" : "";
            return $"({prms}){ret}";
        }

        private bool IsRoutineNameTaken(string name)
        {
            var program = selectedStatements[0].GetRoot() as ProgramNode;
            if (program == null) return false;
            bool fnClash = program.Functions.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            bool mClash = program.AppClass?.Methods.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false;
            return fnClash || mClash;
        }

        // Every output that isn't the chosen Return becomes an `out` parameter,
        // appended after the value/in-out params in first-use order.
        private void MoveOutputsIntoParams()
        {
            foreach (var outVar in returnCandidates.Where(c => c != returnChoice).OrderBy(c => c.OrderKey))
                paramPlans.Add(new ParamPlan
                {
                    Var = outVar.Var,
                    Role = ParamRole.Out,
                    OrderKey = outVar.OrderKey,
                    DeclaredInside = outVar.DeclaredInside
                });
        }

        // Function only: non-value params are renamed with an `Out` suffix (functions have
        // no `out` keyword). If that renamed name already exists as another variable in
        // scope — or two params would collapse to the same name — the rename would silently
        // merge two distinct variables or duplicate a parameter. Refuse rather than emit
        // broken code. (Methods keep `out` + original names, so this can't happen there.)
        private bool PassesFunctionRenameCollisionGuard()
        {
            var reserved = new HashSet<string>(
                VariableRegistry.GetAccessibleVariables(containingScope!)
                    .Select(v => v.Name.StartsWith('&') ? v.Name : "&" + v.Name),
                StringComparer.OrdinalIgnoreCase);

            var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in paramPlans.Where(p => p.Role != ParamRole.Value))
            {
                string eff = EffectiveParamName(p); // &nameOut
                if (reserved.Contains(eff) || !assigned.Add(eff))
                {
                    SetFailure($"Cannot extract to a function: output '{p.Name}' would be renamed to '{eff}' " +
                        "(functions can't use 'out', so outputs take an 'Out' suffix), but that name is already in use. " +
                        $"Rename the existing '{eff}' first.");
                    return false;
                }
            }
            return true;
        }

        private void AnalyzeDataFlow()
        {
            var candidates = VariableRegistry.GetAccessibleVariables(containingScope!)
                .Where(v => v.Kind is VariableKind.Local or VariableKind.Parameter or VariableKind.Exception);

            foreach (var v in candidates)
            {
                var reads = v.References.Where(r => r.ReferenceType == ReferenceType.Read).ToList();
                var writes = v.References.Where(r => r.ReferenceType == ReferenceType.Write).ToList();

                bool writeInside = writes.Any(r => InRange(r.SourceSpan));
                bool readAfter = reads.Any(r => AfterRange(r.SourceSpan));

                int firstWriteInside = writes.Where(r => InRange(r.SourceSpan))
                                             .Select(r => r.SourceSpan.Start.ByteIndex)
                                             .DefaultIfEmpty(int.MaxValue).Min();
                bool readInsideBeforeFirstWrite = reads.Any(r => InRange(r.SourceSpan)
                                             && r.SourceSpan.Start.ByteIndex < firstWriteInside);

                var declSpan = v.DeclarationReference?.SourceSpan;
                bool declaredInside = declSpan.HasValue && InRange(declSpan.Value);
                bool definedBefore = v.Kind == VariableKind.Parameter
                                     || (declSpan.HasValue && declSpan.Value.Start.ByteIndex < RangeStart)
                                     || writes.Any(r => BeforeRange(r.SourceSpan))
                                     || reads.Any(r => BeforeRange(r.SourceSpan));

                // The byte heuristic (readInsideBeforeFirstWrite) misses a self-referential
                // first write like `&total = &total + &a;`: the parser records the LHS Write
                // at the target identifier's span, which precedes the RHS Read, so the Read
                // never counts as "before the first write". When the FIRST in-range write
                // reads the same variable to compute its new value, the incoming value IS live
                // at range entry, so the variable is an input (in-out) — recover that here.
                bool selfRefFirstWrite = FirstInRangeWriteIsSelfReferential(v);
                bool needsInput = definedBefore && (readInsideBeforeFirstWrite || selfRefFirstWrite);
                // A variable whose value is produced inside the range and read after it
                // escapes as an output. "Produced inside" = an assignment (Write) inside,
                // OR a declaration inside — the parser records `Local T &x = expr;` as a
                // Declaration reference (not a Write), so a decl-with-initializer inside the
                // range would otherwise be missed as an output (deviation from brief; the
                // brief's own Scenario B requires this to become a Returns value).
                bool isOutput = (writeInside || declaredInside) && readAfter;

                int order = v.References.Where(r => InRange(r.SourceSpan))
                             .Select(r => r.SourceSpan.Start.ByteIndex)
                             .DefaultIfEmpty(int.MaxValue).Min();

                if (needsInput && isOutput)
                    paramPlans.Add(new ParamPlan { Var = v, Role = ParamRole.InOut, OrderKey = order, DeclaredInside = declaredInside });
                else if (needsInput)
                    paramPlans.Add(new ParamPlan { Var = v, Role = ParamRole.Value, OrderKey = order, DeclaredInside = declaredInside });
                else if (isOutput)
                    returnCandidates.Add(new ParamPlan { Var = v, Role = ParamRole.Out, OrderKey = order, DeclaredInside = declaredInside });
                else
                {
                    // Internal-only. If it was declared BEFORE the range (not declaredInside)
                    // yet is actually used inside it — its first in-range use is a write (dead
                    // incoming value) and it does not escape — the extracted body references a
                    // variable with no declaration. Give it a fresh local at the top of the
                    // routine body. The explicit "used inside" guard prevents declaring a
                    // variable that is declared-before but never touched inside the range.
                    bool usedInside = writeInside || reads.Any(r => InRange(r.SourceSpan));
                    if (!declaredInside && usedInside)
                        internalLocalsToDeclare.Add(v);
                }
            }

            // Deterministic order: value/in-out params by first use inside the range.
            paramPlans.Sort((x, y) => x.OrderKey.CompareTo(y.OrderKey));
            returnCandidates.Sort((x, y) => x.OrderKey.CompareTo(y.OrderKey));

            // Default return: prefer a candidate declared inside the range, else the first.
            returnChoice = returnCandidates.FirstOrDefault(c => c.DeclaredInside) ?? returnCandidates.FirstOrDefault();
        }

        // Name/type for a fresh `Local <type> &x;` declaration, using the same normalization
        // conventions as ParamPlan (single leading `&`, "any" fallback for a blank type).
        private static string LocalDeclName(VariableInfo v)
            => v.Name.StartsWith('&') ? v.Name : "&" + v.Name;
        private static string LocalDeclType(VariableInfo v)
            => string.IsNullOrWhiteSpace(v.Type) ? "any" : v.Type;

        // Case-insensitive PeopleCode identifier key, ignoring the optional leading `&`
        // (VariableInfo.Name and IdentifierNode.Name may or may not carry it).
        private static string NormalizeVarName(string name)
            => (name.StartsWith('&') ? name.Substring(1) : name).ToLowerInvariant();

        // True when the variable's FIRST in-range write assignment is self-referential —
        // its right-hand side reads the same variable (accumulator pattern). Qualifies on
        // the FIRST write only: for `&x = 1; &x = &x + 1;` the first write (`&x = 1`) is not
        // self-referential, so this stays false and the incoming value is correctly treated
        // as dead.
        private bool FirstInRangeWriteIsSelfReferential(VariableInfo v)
        {
            string key = NormalizeVarName(v.Name);
            AssignmentNode? firstWrite = null;
            int firstWritePos = int.MaxValue;

            foreach (var stmt in selectedStatements)
            {
                foreach (var asn in DescendantsAndSelf(stmt).OfType<AssignmentNode>())
                {
                    if (asn.Target is IdentifierNode target
                        && NormalizeVarName(target.Name) == key
                        && InRange(target.SourceSpan))
                    {
                        int pos = target.SourceSpan.Start.ByteIndex;
                        if (pos < firstWritePos)
                        {
                            firstWritePos = pos;
                            firstWrite = asn;
                        }
                    }
                }
            }

            if (firstWrite == null) return false;

            return DescendantsAndSelf(firstWrite.Value)
                .OfType<IdentifierNode>()
                .Any(id => NormalizeVarName(id.Name) == key);
        }

        private string ReindentBody(string raw, string blockIndent, string bodyIndent)
        {
            var lines = raw.Replace("\r\n", "\n").Split('\n');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (i == 0)
                {
                    sb.Append(bodyIndent).Append(line.TrimStart());
                }
                else if (line.Length == 0)
                {
                    // preserve blank lines
                }
                else
                {
                    string stripped = line.StartsWith(blockIndent) ? line.Substring(blockIndent.Length) : line.TrimStart();
                    sb.Append(bodyIndent).Append(stripped);
                }
                if (i < lines.Length - 1) sb.Append(NewLine);
            }
            return sb.ToString();
        }

        // Renders the parenthesised parameter list for a signature.
        //  - Method: "&a As number, &acc As string out" — out/in-out params get ` out`.
        //  - Function: "&a As number, &accOut As string" — PeopleCode functions pass ALL
        //    parameters by reference and forbid the `out` keyword, so out/in-out params
        //    instead carry an `Out` name suffix to signal they return a value. The caller
        //    still passes the original variable (see BuildCallArgs), so only the signature
        //    and the function body use the renamed name.
        private string BuildSignatureParams()
            => string.Join(", ", paramPlans.Select(RenderSigParam));

        private string RenderSigParam(ParamPlan p)
            => isAppClass
                ? $"{p.Name} As {p.TypeName}{(p.Role == ParamRole.Value ? "" : " out")}"
                : $"{EffectiveParamName(p)} As {p.TypeName}";

        // The name a parameter appears under inside the routine. For a function, non-value
        // (by-reference output) params get an `Out` suffix; methods and value params keep
        // their original name.
        private string EffectiveParamName(ParamPlan p)
            => (!isAppClass && p.Role != ParamRole.Value) ? p.Name + "Out" : p.Name;

        // Comma-joined argument names for the call site, in the same order. Always the
        // ORIGINAL variable name — a function's renamed `Out` param binds by reference to
        // the caller's original variable positionally, so the argument is unchanged.
        private string BuildCallArgs()
            => string.Join(", ", paramPlans.Select(p => p.Name));

        // Shared body builder used by both function and method generation. Returns the
        // fully re-indented body text (incl. any Local decl for a before-range Return var
        // and the trailing Return), and reports the out-params that were declared inside
        // the range (so the caller can re-declare them at the call site).
        private string BuildRoutineBody(string bodyIndent, out string blockIndent, out List<ParamPlan> outParamsDeclaredInside)
        {
            blockIndent = GetLineIndent(RangeStart);

            // Out-params declared inside the range become parameters, so their in-body `Local`
            // declaration must go (the caller re-declares them). Reported back for the call prefix.
            outParamsDeclaredInside = paramPlans
                .Where(p => p.Role != ParamRole.Value && p.DeclaredInside && p != returnChoice)
                .ToList();

            // Build the body by applying TARGETED span edits (from AST node spans) to the
            // extracted source, not string/regex passes — so declaration relocation, the
            // combined-declaration split, and the function `Out`-rename can never touch a
            // `&name` inside a string literal or comment. Edits are non-overlapping byte ranges
            // over [RangeStart, RangeEnd); the transformed body is stitched from decoded segments.
            var edits = new List<(int Start, int End, string Replacement)>();
            var removedDeclSpans = new List<(int Start, int End)>();

            // (1) Relocate each out-param-declared-inside declaration, grouped by decl node so a
            //     combined `Local number &x, &y;` is handled exactly once.
            foreach (var group in outParamsDeclaredInside.GroupBy(p => p.Var.DeclarationNode))
            {
                if (group.Key is LocalVariableDeclarationWithAssignmentNode initDecl)
                {
                    // `Local <type> &x = expr;` -> `&x = expr;` (strip only the `Local <type> ` prefix)
                    edits.Add((initDecl.SourceSpan.Start.ByteIndex,
                        initDecl.VariableNameInfo.SourceSpan.Start.ByteIndex, ""));
                }
                else if (group.Key is LocalVariableDeclarationNode bareDecl)
                {
                    var removed = new HashSet<string>(group.Select(p => NormalizeVarName(p.Name)),
                        StringComparer.OrdinalIgnoreCase);
                    var kept = bareDecl.VariableNameInfos
                        .Where(n => !removed.Contains(NormalizeVarName(n.Name)))
                        .ToList();
                    int declStart = bareDecl.SourceSpan.Start.ByteIndex;
                    int declEnd = bareDecl.SourceSpan.End.ByteIndex;
                    if (kept.Count == 0)
                    {
                        // No names remain — drop the whole declaration line.
                        var (ls, le) = WholeLineByteRange(declStart, declEnd);
                        edits.Add((ls, le, ""));
                    }
                    else
                    {
                        // Split: rebuild the declaration with only the kept names (original `;` stays).
                        string typeText = GetSourceText(bareDecl.Type.SourceSpan);
                        string keptText = string.Join(", ", kept.Select(n => GetSourceText(n.SourceSpan)));
                        edits.Add((declStart, declEnd, $"Local {typeText} {keptText}"));
                    }
                    removedDeclSpans.Add((declStart, declEnd));
                }
            }

            // (2) Function-only: rename every in-range reference of a non-value param to its
            //     `Out` name — except references inside a removed/rebuilt declaration span (those
            //     names are gone or handled by the rebuild). Methods keep `out` + original names.
            if (!isAppClass)
            {
                foreach (var p in paramPlans.Where(p => p.Role != ParamRole.Value))
                {
                    string newName = EffectiveParamName(p);
                    foreach (var r in p.Var.References)
                    {
                        int s = r.SourceSpan.Start.ByteIndex, e = r.SourceSpan.End.ByteIndex;
                        if (s < RangeStart || e > RangeEnd) continue;
                        if (removedDeclSpans.Any(d => s >= d.Start && e <= d.End)) continue;
                        edits.Add((s, e, newName));
                    }
                }
            }

            // (3) Stitch the transformed body from decoded source segments + replacements.
            edits.Sort((a, b) => a.Start.CompareTo(b.Start));
            var stitched = new System.Text.StringBuilder();
            int cursor = RangeStart;
            foreach (var (start, end, replacement) in edits)
            {
                if (start < cursor) continue; // defensive: ignore any accidental overlap
                if (start > cursor) stitched.Append(GetSourceText(cursor, start));
                stitched.Append(replacement);
                cursor = end;
            }
            if (cursor < RangeEnd) stitched.Append(GetSourceText(cursor, RangeEnd));
            string rawBody = stitched.ToString();

            // (4) Prepend fresh locals, re-indent the body, append the Return.
            var body = new System.Text.StringBuilder();
            // Internal-only variables declared BEFORE the range (e.g. a For-loop iterator) need a
            // fresh local so the routine is self-contained — not params, not returns.
            foreach (var v in internalLocalsToDeclare)
                body.Append(bodyIndent).Append($"Local {LocalDeclType(v)} {LocalDeclName(v)};").Append(NewLine);
            // A pure-output Return var declared BEFORE the range needs a fresh local inside.
            if (returnChoice != null && !returnChoice.DeclaredInside)
                body.Append(bodyIndent).Append($"Local {returnChoice.TypeName} {returnChoice.Name};").Append(NewLine);

            body.Append(ReindentBody(rawBody, blockIndent, bodyIndent));

            if (returnChoice != null)
                body.Append(NewLine).Append(bodyIndent).Append($"Return {returnChoice.Name};");

            return body.ToString();
        }

        // Byte range to delete for a whole bare declaration statement. Removes the statement and
        // its terminating ';'; extends to swallow the leading indent and trailing newline ONLY
        // when the declaration is alone on its physical line. If another statement follows on the
        // same line it stops at the ';' so a sibling is never deleted. Clamped to the extracted range.
        private (int Start, int End) WholeLineByteRange(int stmtStart, int stmtEnd)
        {
            // Advance to just past this statement's terminating ';'.
            int end = stmtEnd;
            while (end < RangeEnd && SourceBytes[end] != (byte)';' && SourceBytes[end] != (byte)'\n') end++;
            if (end < RangeEnd && SourceBytes[end] == (byte)';') end++;

            // Peek past trailing spaces/tabs: is the rest of the physical line blank?
            int probe = end;
            while (probe < RangeEnd && (SourceBytes[probe] == (byte)' ' || SourceBytes[probe] == (byte)'\t')) probe++;
            bool restBlank = probe >= RangeEnd || SourceBytes[probe] == (byte)'\r' || SourceBytes[probe] == (byte)'\n';

            if (!restBlank)
                return (stmtStart, end); // a sibling statement follows — remove only this declaration

            // Decl is alone on its line: consume the trailing newline and the leading indent (but
            // stop the back-scan at any non-whitespace, so a preceding statement is left intact).
            if (probe < RangeEnd && SourceBytes[probe] == (byte)'\r') probe++;
            if (probe < RangeEnd && SourceBytes[probe] == (byte)'\n') probe++;
            end = probe;
            int start = stmtStart;
            while (start > RangeStart && (SourceBytes[start - 1] == (byte)' ' || SourceBytes[start - 1] == (byte)'\t')) start--;
            return (start, end);
        }

        private void GenerateFunction()
        {
            // functions live at column 0; body is one level in
            string body = BuildRoutineBody("   ", out string blockIndent, out var outParamsDeclaredInside);
            string? retType = returnChoice != null ? returnChoice.TypeName : null;

            // --- Function block ---
            string returnsClause = retType != null ? $" Returns {retType}" : "";
            string funcText =
                $"Function {routineName}({BuildSignatureParams()}){returnsClause}{NewLine}" +
                body + NewLine +
                $"End-Function;{NewLine}{NewLine}";

            // --- Call site ---
            string call = $"{routineName}({BuildCallArgs()})";
            string callStmt = returnChoice == null
                ? $"{call};"
                : returnChoice.DeclaredInside
                    ? $"Local {returnChoice.TypeName} {returnChoice.Name} = {call};"
                    : $"{returnChoice.Name} = {call};";

            // Caller must declare out-params that were originally declared inside the range
            // (their body declaration lost its `Local <type>` prefix).
            var prefix = new System.Text.StringBuilder();
            foreach (var p in outParamsDeclaredInside)
                prefix.Append($"Local {p.TypeName} {p.Name};{NewLine}{blockIndent}");

            // Replace the selected statements with the call, then insert the function above.
            EditText(RangeStart, RangeEnd, prefix + callStmt, "Replace statements with function call");
            int insertAt = FunctionInsertionIndex();
            InsertText(insertAt, funcText, $"Insert function '{routineName}'");
        }

        // App-class variant: emits a method declaration into the class header, an
        // implementation after the last method impl, and a `%This.` call at the range.
        private void GenerateMethod()
        {
            string body = BuildRoutineBody("   ", out string blockIndent, out var outParamsDeclaredInside);
            string? retType = returnChoice != null ? returnChoice.TypeName : null;

            // --- Declaration line for the class header ---
            // PeopleCode groups members under public/protected/private section headers, not
            // per-member keywords; visibility is decided by which section the decl sits in.
            string returnsDecl = retType != null ? $" returns {retType}" : "";
            string declLine = $"   method {routineName}({BuildSignatureParams()}){returnsDecl};{NewLine}";

            // If the chosen visibility section has no members yet we must create the section
            // header keyword line. `public` is the default section and needs no keyword. The
            // keyword sits at the class's own indent (like `class`/`end-class`), not the
            // member indent — matching how existing section keywords are written.
            string sectionHeader = "";
            if (chosenVisibility != VisibilityModifier.Public
                && appClass!.VisibilitySections[chosenVisibility].Count == 0)
            {
                string classIndent = GetLineIndent(appClass.SourceSpan.Start.ByteIndex);
                sectionHeader = $"{classIndent}{chosenVisibility.ToString().ToLowerInvariant()}{NewLine}";
            }

            // --- Implementation (after the last method impl) ---
            var annotations = new System.Text.StringBuilder();
            for (int i = 0; i < paramPlans.Count; i++)
            {
                var p = paramPlans[i];
                string outMod = p.Role == ParamRole.Value ? "" : " out";
                string comma = i < paramPlans.Count - 1 ? "," : "";
                annotations.Append($"   /+ {p.Name} as {p.TypeName}{outMod}{comma} +/").Append(NewLine);
            }
            if (retType != null) annotations.Append($"   /+ Returns {retType} +/").Append(NewLine);

            string impl =
                $"{NewLine}method {routineName}{NewLine}" +
                annotations +
                body + NewLine +
                $"end-method;{NewLine}";

            // --- Call site (%This.) ---
            string call = $"%This.{routineName}({BuildCallArgs()})";
            string callStmt = returnChoice == null
                ? $"{call};"
                : returnChoice.DeclaredInside
                    ? $"Local {returnChoice.TypeName} {returnChoice.Name} = {call};"
                    : $"{returnChoice.Name} = {call};";

            // Caller must declare out-params that were originally declared inside the range.
            var prefix = new System.Text.StringBuilder();
            foreach (var p in outParamsDeclaredInside)
                prefix.Append($"Local {p.TypeName} {p.Name};{NewLine}{blockIndent}");

            // Three edits at distinct byte positions (decl in header < range < impl after
            // impls); BaseRefactor applies them high-to-low so lower targets aren't shifted.
            EditText(RangeStart, RangeEnd, prefix + callStmt, "Replace statements with method call");
            InsertText(MethodDeclInsertionIndex(), sectionHeader + declLine, $"Insert method declaration '{routineName}'");
            InsertText(MethodImplInsertionIndex(), impl, $"Insert method implementation '{routineName}'");
        }

        // Byte index at which to insert the new method declaration within the class header.
        // End of the chosen visibility section if it has members; otherwise a fresh spot
        // (top of the header for public, just before `end-class;` for private/protected —
        // the caller prepends the section keyword in the latter case).
        private int MethodDeclInsertionIndex()
        {
            var section = appClass!.VisibilitySections[chosenVisibility];
            if (section.Count > 0)
            {
                var last = section.OrderBy(m => m.SourceSpan.End.ByteIndex).Last();
                return ScintillaManager.GetLineStartIndex(Editor, last.SourceSpan.End.Line + 1);
            }
            if (chosenVisibility == VisibilityModifier.Public)
            {
                // Public is the default section: place a fresh decl at the top of the header,
                // just after the `class ... [extends ...]` line.
                int headerLine = appClass.BaseType != null
                    ? appClass.BaseType.SourceSpan.End.Line + 1
                    : appClass.SourceSpan.Start.Line + 1;
                return ScintillaManager.GetLineStartIndex(Editor, headerLine);
            }
            // No existing section of the chosen visibility — create one at a position that
            // keeps PeopleCode's section order: public (default) < protected < private. A new
            // `protected` section must precede an existing `private` section; otherwise
            // (private, or protected with no private section) it goes just before `end-class;`.
            if (chosenVisibility == VisibilityModifier.Protected && appClass.PrivateToken != null)
                return ScintillaManager.GetLineStartIndex(Editor, appClass.PrivateToken.SourceSpan.Start.Line);
            return ScintillaManager.GetLineStartIndex(Editor, appClass.SourceSpan.End.Line);
        }

        // Byte index after the last existing method implementation (or class end).
        private int MethodImplInsertionIndex()
        {
            var lastImpl = appClass!.Methods
                .Where(m => m.IsImplementation && m.Implementation != null)
                .OrderBy(m => m.Implementation!.SourceSpan.End.ByteIndex)
                .LastOrDefault();
            if (lastImpl?.Implementation != null)
                return lastImpl.Implementation.SourceSpan.End.ByteIndex + 1;
            return appClass.SourceSpan.End.ByteIndex + 1;
        }

        // Byte index of the line where the new Function block should be inserted.
        //
        // If the selection sits inside an existing function's body, the new function
        // goes right above that enclosing function (grouped with it).
        //
        // Otherwise (selection is in the program's main flow) a non-class program's
        // declarations always appear in a fixed order before the executable main
        // block: Imports, Functions, ComponentAndGlobalVariables, LocalVariables,
        // Constants, MainBlock. Walk that order from the bottom up, starting at
        // MainBlock and letting each non-empty section override the candidate with
        // its own first member — so the new function lands at the top of the
        // Functions section if one exists, else right above whichever declaration
        // section comes first, else (nothing at all) right after the last Import,
        // else at the very top of the file.
        private int FunctionInsertionIndex()
        {
            var program = selectedStatements[0].GetRoot() as ProgramNode;
            var enclosingFunc = program?.Functions.FirstOrDefault(f => f.IsImplementation
                && f.SourceSpan.Start.ByteIndex <= RangeStart
                && f.SourceSpan.End.ByteIndex >= RangeEnd);

            if (enclosingFunc != null)
                return InsertionIndexBefore(enclosingFunc);

            if (program == null || program.MainBlock == null)
                return ScintillaManager.GetLineStartIndex(Editor, selectedStatements[0].SourceSpan.Start.Line);

            AstNode anchor = program.MainBlock;
            if (program.Constants.Count > 0) anchor = FirstByPosition(program.Constants);
            if (program.LocalVariables.Count > 0) anchor = FirstByPosition(program.LocalVariables);
            if (program.ComponentAndGlobalVariables.Count > 0) anchor = FirstByPosition(program.ComponentAndGlobalVariables);
            if (program.Functions.Count > 0) anchor = FirstByPosition(program.Functions);

            return InsertionIndexBefore(anchor);
        }

        // Byte index of `node`'s own line, pulled back to cover any comment block
        // directly attached above it (e.g. a flowerbox change-tracking header) so the
        // new insertion doesn't get sandwiched between the comment and the node it
        // documents.
        private int InsertionIndexBefore(AstNode node)
        {
            var firstComment = node.GetLeadingComments().FirstOrDefault();
            int line = firstComment != null
                ? Math.Min(firstComment.SourceSpan.Start.Line, node.SourceSpan.Start.Line)
                : node.SourceSpan.Start.Line;
            return ScintillaManager.GetLineStartIndex(Editor, line);
        }

        private static T FirstByPosition<T>(IEnumerable<T> nodes) where T : AstNode
            => nodes.OrderBy(n => n.SourceSpan.Start.ByteIndex).First();

        private void LocateStatementRange(ProgramNode program)
        {
            if (!HasSelection)
            {
                SetFailure("Select the statements to extract. Extract Function/Method needs a selection because the cursor alone cannot delimit the range.");
                return;
            }

            int selStart = SelectionStart, selEnd = SelectionEnd;
            TrimWhitespace(ref selStart, ref selEnd);

            // Deepest block whose span covers the trimmed selection.
            BlockNode? best = null;
            foreach (var block in program.FindDescendants<BlockNode>())
            {
                if (block.SourceSpan.Start.ByteIndex <= selStart && block.SourceSpan.End.ByteIndex >= selEnd)
                {
                    if (best == null || block.SourceSpan.Start.ByteIndex >= best.SourceSpan.Start.ByteIndex)
                        best = block;
                }
            }
            if (best == null)
            {
                SetFailure("Selection must lie within a single statement block.");
                return;
            }

            // Sibling statements the selection overlaps, in source order.
            var touched = best.Statements
                .Where(s => s.SourceSpan.End.ByteIndex > selStart && s.SourceSpan.Start.ByteIndex < selEnd)
                .OrderBy(s => s.SourceSpan.Start.ByteIndex)
                .ToList();
            if (touched.Count == 0)
            {
                SetFailure("Selection must cover at least one whole statement.");
                return;
            }

            // Trimmed selection must align to the outer boundary of the touched statements.
            if (touched[0].SourceSpan.Start.ByteIndex < selStart || touched[^1].SourceSpan.End.ByteIndex > selEnd)
            {
                SetFailure("Selection must cover whole statements — it currently starts or ends inside a statement.");
                return;
            }

            containingBlock = best;
            selectedStatements.AddRange(touched);
        }

        private void TrimWhitespace(ref int start, ref int end)
        {
            while (start < end && IsWhitespaceByte(SourceBytes[start])) start++;
            while (end > start && IsWhitespaceByte(SourceBytes[end - 1])) end--;
        }

        private static bool IsWhitespaceByte(byte b)
            => b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';

        private bool PassesSafetyGuards()
        {
            foreach (var stmt in selectedStatements)
            {
                if (stmt.FindDescendants<ReturnStatementNode>().Any() || stmt is ReturnStatementNode)
                {
                    SetFailure("Cannot extract a selection that contains a Return — it would return from the new routine, not the original.");
                    return false;
                }

                foreach (var brk in DescendantsAndSelf(stmt).OfType<BreakStatementNode>())
                    if (!LoopEnclosesWithinSelection(brk))
                    { SetFailure("Cannot extract a Break that targets a loop outside the selection."); return false; }

                foreach (var cont in DescendantsAndSelf(stmt).OfType<ContinueStatementNode>())
                    if (!LoopEnclosesWithinSelection(cont))
                    { SetFailure("Cannot extract a Continue that targets a loop outside the selection."); return false; }
            }
            return true;
        }

        private static IEnumerable<AstNode> DescendantsAndSelf(AstNode node)
            => new[] { node }.Concat(node.FindDescendants<AstNode>());

        // True when the nearest enclosing loop of `node` is inside the selected range.
        private bool LoopEnclosesWithinSelection(AstNode node)
        {
            for (AstNode? cur = node.Parent; cur != null; cur = cur.Parent)
            {
                if (cur is ForStatementNode or WhileStatementNode or RepeatStatementNode)
                    return cur.SourceSpan.Start.ByteIndex >= RangeStart && cur.SourceSpan.End.ByteIndex <= RangeEnd;
            }
            return false; // no enclosing loop at all
        }

        private sealed class ExtractRoutineDialog : Form
        {
            private readonly TextBox txtName = new();
            private readonly ComboBox cboReturn = new();
            private readonly ComboBox cboVisibility = new();
            private readonly Label lblPreview = new();
            private readonly Label lblError = new();
            private readonly Button btnOk = new();
            private readonly Button btnCancel = new();
            private readonly Panel headerPanel = new();
            private readonly Label headerLabel = new();

            private readonly List<ParamPlan> candidates;
            private readonly Func<string, bool> isNameTaken;
            private readonly Func<ParamPlan?, string> buildTail; // returns "(<params>) Returns <T>"

            public string RoutineName { get; private set; }
            public ParamPlan? ReturnChoice { get; private set; }
            public bool ShowVisibility { get; set; }               // set true by Task 5
            public PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier Visibility { get; private set; }
                = PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier.Private;

            public ExtractRoutineDialog(string suggestedName, List<ParamPlan> returnCandidates,
                ParamPlan? defaultReturn, Func<ParamPlan?, string> buildTail, Func<string, bool> isNameTaken)
            {
                this.candidates = returnCandidates;
                this.isNameTaken = isNameTaken;
                this.buildTail = buildTail;
                RoutineName = suggestedName;
                ReturnChoice = defaultReturn;
                InitializeComponent();
                txtName.Text = suggestedName;
                PopulateReturn(defaultReturn);
                ActiveControl = txtName;
                txtName.SelectAll();
                UpdatePreview();
            }

            private void PopulateReturn(ParamPlan? defaultReturn)
            {
                cboReturn.Items.Add("(none — all via out params)");
                foreach (var c in candidates) cboReturn.Items.Add(c.Name + " As " + c.TypeName);
                cboReturn.SelectedIndex = defaultReturn == null ? 0 : candidates.IndexOf(defaultReturn) + 1;
            }

            private void UpdatePreview()
            {
                ReturnChoice = cboReturn.SelectedIndex <= 0 ? null : candidates[cboReturn.SelectedIndex - 1];
                string kind = ShowVisibility
                    ? $"{cboVisibility.SelectedItem} method" : "Function";
                lblPreview.Text = $"{kind} {txtName.Text}{buildTail(ReturnChoice)}";
            }

            private void InitializeComponent()
            {
                SuspendLayout();
                headerPanel.BackColor = Color.FromArgb(50, 50, 60);
                headerPanel.Dock = DockStyle.Top; headerPanel.Height = 30; headerPanel.Controls.Add(headerLabel);
                headerLabel.Text = "Extract Function/Method"; headerLabel.ForeColor = Color.White;
                headerLabel.Dock = DockStyle.Fill; headerLabel.TextAlign = ContentAlignment.MiddleCenter;
                headerLabel.Font = new Font("Segoe UI", 9F);

                var lblName = new Label { AutoSize = true, Location = new Point(12, 40), Text = "Function/method name:" };
                txtName.BorderStyle = BorderStyle.FixedSingle; txtName.Location = new Point(12, 60);
                txtName.Size = new Size(320, 23); txtName.Font = new Font("Segoe UI", 11F);
                txtName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnOk_Click(s, e); };
                txtName.TextChanged += (s, e) => UpdatePreview();

                var lblVis = new Label { AutoSize = true, Location = new Point(12, 90), Text = "Visibility:" };
                cboVisibility.DropDownStyle = ComboBoxStyle.DropDownList; cboVisibility.Location = new Point(80, 87);
                cboVisibility.Size = new Size(120, 23);
                cboVisibility.Items.AddRange(new object[] { "Private", "Protected", "Public" });
                cboVisibility.SelectedIndex = 0;
                cboVisibility.SelectedIndexChanged += (s, e) =>
                {
                    Visibility = cboVisibility.SelectedIndex switch
                    {
                        1 => PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier.Protected,
                        2 => PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier.Public,
                        _ => PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier.Private,
                    };
                    UpdatePreview();
                };

                var lblRet = new Label { AutoSize = true, Location = new Point(12, 120), Text = "Return value:" };
                cboReturn.DropDownStyle = ComboBoxStyle.DropDownList; cboReturn.Location = new Point(90, 117);
                cboReturn.Size = new Size(242, 23);
                cboReturn.SelectedIndexChanged += (s, e) => UpdatePreview();

                lblPreview.AutoSize = false; lblPreview.Location = new Point(12, 150); lblPreview.Size = new Size(320, 40);
                lblPreview.ForeColor = Color.FromArgb(90, 90, 100); lblPreview.Font = new Font("Consolas", 8.5F);
                // Show '&' literally — by default a Label treats '&' as a mnemonic marker and
                // hides it, so "&x As number" would render as "x As number".
                lblPreview.UseMnemonic = false;

                lblError.AutoSize = true; lblError.Location = new Point(12, 195); lblError.ForeColor = Color.Firebrick;

                btnOk.Text = "&OK"; btnOk.Location = new Point(176, 216); btnOk.Size = new Size(75, 28);
                btnOk.Click += BtnOk_Click;
                btnCancel.Text = "&Cancel"; btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Location = new Point(257, 216); btnCancel.Size = new Size(75, 28);

                AcceptButton = btnOk; CancelButton = btnCancel;
                ClientSize = new Size(344, 256);
                Controls.AddRange(new Control[] { headerPanel, lblName, txtName, lblVis, cboVisibility,
                    lblRet, cboReturn, lblPreview, lblError, btnOk, btnCancel });
                FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent;
                ShowInTaskbar = false; MaximizeBox = false; MinimizeBox = false;

                // Visibility row hidden by default (functions); Task 5 sets ShowVisibility=true for methods.
                lblVis.Visible = cboVisibility.Visible = false;
                Load += (s, e) => { lblVis.Visible = cboVisibility.Visible = ShowVisibility; UpdatePreview(); };

                ResumeLayout(false); PerformLayout();
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                var name = txtName.Text.Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]*$"))
                { lblError.Text = "Not a valid function/method name."; return; }
                if (isNameTaken(name))
                { lblError.Text = $"'{name}' is already declared in this program."; return; }
                RoutineName = name;
                ReturnChoice = cboReturn.SelectedIndex <= 0 ? null : candidates[cboReturn.SelectedIndex - 1];
                DialogResult = DialogResult.OK; Close();
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }
        }
    }
}
