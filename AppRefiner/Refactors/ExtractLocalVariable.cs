using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Extracts the selected expression into a new local variable declared
    /// immediately before the containing statement. Requires a selection that
    /// covers exactly one complete expression — cursor position alone is
    /// ambiguous for nested expressions.
    /// </summary>
    public class ExtractLocalVariable : BaseRefactor
    {
        public new static string RefactorName => "Extract Local Variable";
        public new static string RefactorDescription => "Extracts the selected expression into a new local variable";
        public new static bool RegisterKeyboardShortcut => false;

        public override bool RequiresUserInputDialog => true;
        public override bool DeferDialogUntilAfterVisitor => true;
        public override bool RequiresTypeInference => true;
        public override bool RunOnIncompleteParse => false;

        private ExpressionNode? targetExpression;
        private StatementNode? containingStatement;
        private ScopeContext? containingScope;
        private readonly List<ExpressionNode> occurrences = new();

        public ExtractLocalVariable(ScintillaEditor editor) : base(editor) { }

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
            LocateTargetExpression(node);
        }

        protected override void OnReset()
        {
            targetExpression = null;
            containingStatement = null;
            containingScope = null;
            occurrences.Clear();
        }

        private void LocateTargetExpression(ProgramNode program)
        {
            if (!HasSelection)
            {
                SetFailure("Select the expression to extract. Extract Local Variable needs a selection because the cursor alone cannot identify which enclosing expression you mean.");
                return;
            }

            int selStart = SelectionStart;
            int selEnd = SelectionEnd;
            TrimWhitespace(ref selStart, ref selEnd);

            // Exact-span match; if none, strip one paren layer and retry
            while (true)
            {
                // FindDescendants is a pre-order walk, so the first exact-span match is
                // the outermost node when wrappers share the span
                targetExpression = program.FindDescendants<ExpressionNode>()
                    .FirstOrDefault(e => e.SourceSpan.Start.ByteIndex == selStart
                                      && e.SourceSpan.End.ByteIndex == selEnd);
                if (targetExpression != null)
                    break;

                if (selEnd - selStart >= 2
                    && SourceBytes[selStart] == (byte)'('
                    && SourceBytes[selEnd - 1] == (byte)')')
                {
                    selStart++;
                    selEnd--;
                    TrimWhitespace(ref selStart, ref selEnd);
                    continue;
                }

                SetFailure("Selection must cover exactly one complete expression.");
                return;
            }

            if (targetExpression is IdentifierNode)
            {
                SetFailure("The selection is already a single identifier — there is nothing to extract.");
                return;
            }

            if (targetExpression.Parent is AssignmentNode assignment
                && ReferenceEquals(assignment.Target, targetExpression))
            {
                SetFailure("Cannot extract an assignment target.");
                return;
            }

            containingStatement = FindContainingStatement(targetExpression);
            if (containingStatement == null || containingScope == null)
            {
                SetFailure("Extract Local Variable only works inside a code block (method, function, getter/setter, or event body).");
                return;
            }

            // Re-evaluated contexts: hoisting out of these changes semantics
            for (AstNode? cur = targetExpression; cur != null && !ReferenceEquals(cur, containingStatement); cur = cur.Parent)
            {
                switch (cur.Parent)
                {
                    case WhileStatementNode w when ReferenceEquals(cur, w.Condition):
                        SetFailure("Cannot extract from a While condition — it is re-evaluated every iteration.");
                        return;
                    case RepeatStatementNode r when ReferenceEquals(cur, r.Condition):
                        SetFailure("Cannot extract from a Repeat-Until condition — it is re-evaluated every iteration.");
                        return;
                    case ForStatementNode f when ReferenceEquals(cur, f.FromValue)
                                              || ReferenceEquals(cur, f.ToValue)
                                              || (f.StepValue != null && ReferenceEquals(cur, f.StepValue)):
                        SetFailure("Cannot extract from a For loop header.");
                        return;
                    case EvaluateStatementNode ev when ev.WhenClauses.Any(wc => ReferenceEquals(cur, wc.Condition)):
                        SetFailure("Cannot extract from a When condition.");
                        return;
                }
            }

            CollectOccurrences();
        }

        private void TrimWhitespace(ref int start, ref int end)
        {
            while (start < end && IsWhitespaceByte(SourceBytes[start])) start++;
            while (end > start && IsWhitespaceByte(SourceBytes[end - 1])) end--;
        }

        private static bool IsWhitespaceByte(byte b)
            => b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';

        private static StatementNode? FindContainingStatement(AstNode node)
        {
            for (AstNode? cur = node.Parent; cur != null; cur = cur.Parent)
            {
                if (cur is StatementNode stmt && stmt.Parent is BlockNode)
                    return stmt;
            }
            return null;
        }

        /// <summary>
        /// Finds expressions identical to the target (normalized text) within the
        /// containing block subtree, at or after the insertion point, so the new
        /// declaration dominates every replacement. The target itself is included.
        /// </summary>
        private void CollectOccurrences()
        {
            occurrences.Clear();
            var block = (BlockNode)containingStatement!.Parent!;
            string normTarget = Normalize(GetSourceText(targetExpression!.SourceSpan));

            foreach (var expr in block.FindDescendants<ExpressionNode>())
            {
                if (expr.SourceSpan.Start.ByteIndex < containingStatement.SourceSpan.Start.ByteIndex)
                    continue;
                if (expr.Parent is AssignmentNode a && ReferenceEquals(a.Target, expr))
                    continue;
                if (Normalize(GetSourceText(expr.SourceSpan)) == normTarget)
                    occurrences.Add(expr);
            }

            if (!occurrences.Contains(targetExpression))
                occurrences.Add(targetExpression);
        }

        /// <summary>
        /// Comparison text for occurrence matching. When the text contains a double
        /// quote it may hold a string literal whose contents are whitespace- and
        /// case-sensitive, so the trimmed raw text is compared ordinally (no collapsing,
        /// no lowercasing) to avoid falsely matching e.g. Foo("a  b") with Foo("a b").
        /// Only quote-free text — pure identifiers/operators, which PeopleCode treats
        /// case-insensitively — gets whitespace-collapsed and lowercased.
        /// Conservative: may miss a match, never produces a wrong one.
        /// </summary>
        private static string Normalize(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.Contains('"'))
                return trimmed; // literal content must match exactly; conservative: may miss, never wrong
            return System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ").ToLowerInvariant();
        }

        private string SuggestName()
        {
            string baseName = targetExpression switch
            {
                FunctionCallNode fc when fc.Function is IdentifierNode fn => fn.Name,
                FunctionCallNode fc when fc.Function is MemberAccessNode ma => ma.MemberName,
                MemberAccessNode member => member.MemberName,
                _ => "value"
            };

            baseName = new string(baseName.Where(char.IsLetterOrDigit).ToArray());
            if (baseName.Length == 0 || !char.IsLetter(baseName[0]))
                baseName = "value";
            baseName = "&" + char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);

            string candidate = baseName;
            int suffix = 2;
            while (IsNameVisibleInScope(candidate))
                candidate = baseName + suffix++;
            return candidate;
        }

        /// <summary>
        /// The registry stores names both with and without the leading ampersand
        /// depending on declaration kind, so check both forms (mirrors AssignToNewVariable).
        /// </summary>
        private bool IsNameVisibleInScope(string name)
        {
            if (VariableRegistry.FindVariableInScope(name, containingScope!) != null)
                return true;
            if (name.StartsWith('&') && name.Length > 1
                && VariableRegistry.FindVariableInScope(name.Substring(1), containingScope!) != null)
                return true;
            return false;
        }

        public override bool ShowRefactorDialog()
        {
            if (targetExpression == null)
            {
                // LocateTargetExpression already called SetFailure with the reason
                return false;
            }

            string typeName = Services.TypeInferenceRunner.RenderDeclaredType(targetExpression.GetInferredType());
            using var dialog = new ExtractVariableDialog(SuggestName(), typeName, occurrences.Count, IsNameVisibleInScope);
            var wrapper = new WindowWrapper(GetEditorMainWindowHandle());

            if (dialog.ShowDialog(wrapper) != DialogResult.OK)
                return false;

            GenerateChanges(dialog.VariableName, dialog.ReplaceAll);
            return true;
        }

        private void GenerateChanges(string variableName, bool replaceAll)
        {
            string exprText = GetSourceText(targetExpression!.SourceSpan);
            string typeName = Services.TypeInferenceRunner.RenderDeclaredType(targetExpression.GetInferredType());
            string indent = GetLineIndent(containingStatement!.SourceSpan.Start.ByteIndex);

            // Replacements MUST be added before the insertion: when an occurrence starts
            // at the same byte as the insertion point (extracting a whole expression
            // statement), ApplyEdits' stable descending sort applies same-position edits
            // in add order, and the replacement must consume the original span first.
            var targets = replaceAll ? occurrences : new List<ExpressionNode> { targetExpression };
            foreach (var expr in targets)
            {
                EditText(expr.SourceSpan.Start.ByteIndex, expr.SourceSpan.End.ByteIndex,
                    variableName, $"Replace expression with {variableName}");
            }

            InsertText(containingStatement.SourceSpan.Start.ByteIndex,
                $"Local {typeName} {variableName} = {exprText};{NewLine}{indent}",
                $"Declare {variableName}");
        }

        private class ExtractVariableDialog : Form
        {
            private readonly TextBox txtName = new();
            private readonly Button btnOk = new();
            private readonly Button btnCancel = new();
            private readonly Label lblPrompt = new();
            private readonly Label lblType = new();
            private readonly CheckBox chkReplaceAll = new();
            private readonly Label lblError = new();
            private readonly Panel headerPanel = new();
            private readonly Label headerLabel = new();

            private readonly Func<string, bool> isNameTaken;

            // Captured at construction: Control.Visible reports *effective* visibility,
            // so once the modal form is hidden on close it reads false regardless of the
            // checkbox's own state. ReplaceAll is read after ShowDialog returns, so it
            // must gate on this field (Checked survives form hide; Visible does not).
            private readonly bool showReplaceAll;

            public string VariableName { get; private set; }
            public bool ReplaceAll => showReplaceAll && chkReplaceAll.Checked;

            public ExtractVariableDialog(string suggestedName, string typeName,
                int occurrenceCount, Func<string, bool> isNameTaken)
            {
                this.isNameTaken = isNameTaken;
                showReplaceAll = occurrenceCount > 1;
                VariableName = suggestedName;
                InitializeComponent(typeName, occurrenceCount);
                txtName.Text = suggestedName.StartsWith('&') ? suggestedName.Substring(1) : suggestedName;
                ActiveControl = txtName;
                txtName.SelectAll();
            }

            private void InitializeComponent(string typeName, int occurrenceCount)
            {
                SuspendLayout();

                headerPanel.BackColor = Color.FromArgb(50, 50, 60);
                headerPanel.Dock = DockStyle.Top;
                headerPanel.Height = 30;
                headerPanel.Controls.Add(headerLabel);

                headerLabel.Text = "Extract Local Variable";
                headerLabel.ForeColor = Color.White;
                headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                headerLabel.Dock = DockStyle.Fill;
                headerLabel.TextAlign = ContentAlignment.MiddleCenter;

                lblPrompt.AutoSize = true;
                lblPrompt.Location = new Point(12, 40);
                lblPrompt.Text = "Enter variable name:";

                txtName.BorderStyle = BorderStyle.FixedSingle;
                txtName.Location = new Point(12, 60);
                txtName.Size = new Size(260, 23);
                txtName.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
                txtName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnOk_Click(s, e); };

                lblType.AutoSize = true;
                lblType.Location = new Point(12, 90);
                lblType.ForeColor = Color.FromArgb(90, 90, 100);
                lblType.Text = $"Type: {typeName}";

                chkReplaceAll.AutoSize = true;
                chkReplaceAll.Location = new Point(12, 112);
                chkReplaceAll.Text = $"Replace all {occurrenceCount} identical occurrences";
                chkReplaceAll.Visible = showReplaceAll; // layout only; ReplaceAll gates on the field

                lblError.AutoSize = true;
                lblError.Location = new Point(12, 136);
                lblError.ForeColor = Color.Firebrick;
                lblError.Text = "";

                btnOk.DialogResult = DialogResult.None;
                btnOk.Location = new Point(116, 158);
                btnOk.Size = new Size(75, 28);
                btnOk.Text = "&OK";
                btnOk.UseVisualStyleBackColor = true;
                btnOk.Click += BtnOk_Click;

                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Location = new Point(197, 158);
                btnCancel.Size = new Size(75, 28);
                btnCancel.Text = "&Cancel";
                btnCancel.UseVisualStyleBackColor = true;

                AcceptButton = btnOk;
                CancelButton = btnCancel;
                ClientSize = new Size(284, 198);
                Controls.AddRange(new Control[] { btnCancel, btnOk, lblError, chkReplaceAll, lblType, txtName, lblPrompt, headerPanel });
                FormBorderStyle = FormBorderStyle.None;
                MaximizeBox = false;
                MinimizeBox = false;
                Name = "ExtractVariableDialog";
                StartPosition = FormStartPosition.CenterParent;
                Text = "Extract Local Variable";
                ShowInTaskbar = false;
                ResumeLayout(false);
                PerformLayout();
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                var name = txtName.Text.Trim();
                if (!name.StartsWith("&"))
                    name = "&" + name;

                if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^&[A-Za-z][A-Za-z0-9_]*$"))
                {
                    lblError.Text = "Not a valid variable name.";
                    return;
                }
                if (isNameTaken(name))
                {
                    lblError.Text = $"{name} is already in use in this scope.";
                    return;
                }

                VariableName = name;
                DialogResult = DialogResult.OK;
                Close();
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }
        }
    }
}
