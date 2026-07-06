using AppRefiner.Database;
using AppRefiner.Refactors;
using AppRefiner.Refactors.QuickFixes;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Database;

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Flags calls to functions PeopleCode cannot resolve at the call site: not builtin,
    /// not Declare-d, and not implemented above the call (PeopleCode is single-pass, so
    /// forward references to later implementations are compile errors). Quick fixes:
    /// declare from the local function cache (works without a live DB), open the Declare
    /// Function search dialog pre-filled (connected only), or move a below-defined local
    /// function above its first use.
    /// </summary>
    public class UndeclaredFunctionStyler : BaseStyler
    {
        public override string Description => "Undeclared functions";
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        private const uint SQUIGGLE_COLOR = 0x0000FFA0; // Red + alpha
        private const int MAX_IMPORT_OPTIONS = 10;

        private FunctionVisibilityIndex? _functionIndex;

        public UndeclaredFunctionStyler()
        {
            Active = true; // Ships enabled by default
        }

        public override void VisitProgram(ProgramNode node)
        {
            _functionIndex = FunctionVisibilityIndex.Build(node);
            base.VisitProgram(node);
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            base.VisitFunctionCall(node);

            // Only bare-identifier calls: method calls, create expressions, and
            // %This.X() never have a plain IdentifierNode callee. User-variable callees
            // are default-method calls (e.g. &rowset(1)), not function calls.
            if (node.Function is not IdentifierNode ident || ident.IdentifierType != IdentifierType.Generic)
                return;

            string name = ident.Name;

            // Declares must precede implementations and executable code, so existence
            // alone makes the name visible everywhere
            if (_functionIndex == null || _functionIndex.Declarations.ContainsKey(name))
                return;

            if (_functionIndex.Implementations.TryGetValue(name, out var impl))
            {
                if (impl.SourceSpan.Start.ByteIndex < node.SourceSpan.Start.ByteIndex)
                    return; // Defined above the call — valid

                // Forward reference: the implementation exists but below this call
                var caller = node.FindAncestor<FunctionNode>();
                string description = caller != null
                    ? $"Move Function '{impl.Name}' above '{caller.Name}'"
                    : $"Move Function '{impl.Name}' above this statement";

                AddIndicator(ident.SourceSpan, IndicatorType.SQUIGGLE, SQUIGGLE_COLOR,
                    $"Function '{name}' is defined below its first use",
                    new List<QuickFixEntry> { new(typeof(MoveFunctionAbove), description, impl.Name) });
                return;
            }

            if (PeopleCodeTypeDatabase.GetFunction(name) != null)
                return; // Builtin

            AddIndicatorWithDeferredQuickFix(
                ident.SourceSpan,
                IndicatorType.SQUIGGLE,
                SQUIGGLE_COLOR,
                $"Function '{name}' is not declared or defined",
                ResolveUnknownFunctionFixes,
                name);
        }

        /// <summary>
        /// Deferred resolver (runs at Ctrl+. time): import options from the local function
        /// cache — usable without a live DB connection — plus a pre-filled search dialog
        /// entry when connected. Empty result = squiggle only, no popup.
        /// </summary>
        private List<QuickFixEntry> ResolveUnknownFunctionFixes(ScintillaEditor editor, int position, object? context)
        {
            var fixes = new List<QuickFixEntry>();
            if (context is not string functionName || string.IsNullOrEmpty(functionName))
                return fixes;

            var cache = MainForm.FunctionCache;
            var process = editor.AppDesignerProcess;
            if (cache != null && process != null)
            {
                var matches = cache.SearchFunctionCache(process, functionName)
                    .Where(r => string.Equals(r.FunctionName, functionName, StringComparison.OrdinalIgnoreCase))
                    .Take(MAX_IMPORT_OPTIONS)
                    .ToList();

                // The event is omitted from descriptions (shared functions live almost
                // exclusively in FieldFormula, so it's noise) — EXCEPT when the same
                // REC.FIELD appears more than once in the result set, where the event is
                // the only disambiguator (and the description string is the selection key)
                var recFieldCounts = matches
                    .Select(RecFieldOf)
                    .GroupBy(rf => rf, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                foreach (var match in matches)
                {
                    var parts = match.FunctionPath.Split(':');
                    string recField = RecFieldOf(match);
                    string source = parts.Length >= 3 && recFieldCounts[recField] > 1
                        ? $"{recField} ({parts[2]})"
                        : recField;
                    fixes.Add(new(typeof(DeclareFunctionQuickFix), $"Import '{match.FunctionName}' from {source}", match));
                }
            }

            if (editor.DataManager != null && editor.DataManager.IsConnected)
            {
                fixes.Add(new(typeof(OpenDeclareFunctionDialogQuickFix), $"Search for function '{functionName}'...", functionName));
            }

            return fixes;
        }

        /// <summary>
        /// REC.FIELD portion of a cache result's path (format REC:FIELD:EVENT);
        /// falls back to the raw path when it doesn't have the expected shape.
        /// </summary>
        private static string RecFieldOf(FunctionSearchResult match)
        {
            var parts = match.FunctionPath.Split(':');
            return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : match.FunctionPath;
        }
    }
}
