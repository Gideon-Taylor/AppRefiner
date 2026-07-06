using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors.QuickFixes
{
    /// <summary>
    /// Context payload attached by CompilerErrorsStyler for the assign-result quick fix.
    /// The declared type is rendered at styler time because the refactor's fresh
    /// re-parse does not re-run type inference.
    /// </summary>
    public sealed record AssignToVariableContext(int StatementStartByteIndex, string TypeName);

    /// <summary>
    /// QuickFix for "Return values must be assigned to a variable" / "Expression values
    /// must be assigned to a variable" type errors: prefixes the offending expression
    /// statement with a local variable declaration that captures the value, e.g.
    /// <c>Local string &amp;_ = Func();</c>. The variable name is the shortest of
    /// &amp;_, &amp;__, &amp;___, ... not visible in the statement's scope (checked
    /// against the scope chain via the variable registry, after the full traversal so
    /// declarations later in the same scope also count).
    /// </summary>
    public class AssignToNewVariable : BaseRefactor
    {
        public new static string RefactorName => "Assign Result To Variable (QuickFix)";
        public new static string RefactorDescription => "Captures an ignored expression value in a new local variable";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => true;

        private readonly AssignToVariableContext _context;
        private ScopeContext? _statementScope;

        public AssignToNewVariable(ScintillaEditor editor) : base(editor)
        {
            if (editor.QuickFixContext is not AssignToVariableContext context)
                throw new InvalidOperationException("QuickFix context does not contain an AssignToVariableContext");

            _context = context;
        }

        public override void VisitExpressionStatement(ExpressionStatementNode node)
        {
            base.VisitExpressionStatement(node);

            if (node.SourceSpan.Start.ByteIndex == _context.StatementStartByteIndex)
            {
                _statementScope = GetCurrentScope();
            }
        }

        public override void VisitProgram(ProgramNode node)
        {
            // Full traversal first: locates the statement's scope AND completes the
            // variable registry, so the name check below sees every declaration in the
            // scope chain — including ones below the statement
            base.VisitProgram(node);

            if (_statementScope == null)
            {
                SetFailure("The statement has moved since the quick fix was offered — re-run the styler and try again");
                return;
            }

            int length = 1;
            while (IsNameVisibleInScope("&" + new string('_', length)))
            {
                length++;
            }
            string variableName = "&" + new string('_', length);

            InsertText(_context.StatementStartByteIndex,
                $"Local {_context.TypeName} {variableName} = ",
                "Capture expression value in a local variable");
        }

        /// <summary>
        /// True when the candidate collides with any variable visible from the
        /// statement's scope. The registry stores names both with and without the
        /// leading &amp; depending on declaration kind, so check both forms (mirrors
        /// ScopedAstVisitor.FindVariable).
        /// </summary>
        private bool IsNameVisibleInScope(string name)
        {
            return VariableRegistry.FindVariableInScope(name, _statementScope!) != null
                || VariableRegistry.FindVariableInScope(name.Substring(1), _statementScope!) != null;
        }
    }
}
