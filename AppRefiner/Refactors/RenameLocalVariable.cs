using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Renames a local variable, parameter, private instance variable, private method, or private constant and all its references
    /// </summary>
    public class RenameLocalVariable(ScintillaEditor editor) : BaseRefactor(editor)
    {
        /// <summary>
        /// Gets the display name of this refactoring operation
        /// </summary>
        public new static string RefactorName => "Rename Variable or Method";

        /// <summary>
        /// Gets the description of this refactoring operation
        /// </summary>
        public new static string RefactorDescription => "Rename a local variable, parameter, private instance variable, private method, or private constant and all its references";

        private string? newVariableName;
        private VariableInfo? variableToRename;
        private ScopeContext? targetScope;

        /// <summary>
        /// Indicates that this refactor requires a user input dialog
        /// </summary>
        public override bool RequiresUserInputDialog => true;

        /// <summary>
        /// Indicates that this refactor should defer showing the dialog until after the visitor has run
        /// </summary>
        public override bool DeferDialogUntilAfterVisitor => true;

        /// <summary>
        /// Indicates that this refactor should have a keyboard shortcut registered
        /// </summary>
        public new static bool RegisterKeyboardShortcut => true;

        /// <summary>
        /// Gets the keyboard shortcut modifier keys for this refactor
        /// </summary>
        public new static AppRefiner.ModifierKeys ShortcutModifiers => AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Shift;

        /// <summary>
        /// Gets the keyboard shortcut key for this refactor
        /// </summary>
        public new static Keys ShortcutKey => Keys.R;

        /// <summary>
        /// Enum to represent the type of token being renamed
        /// </summary>
        private enum RenameTokenType
        {
            LocalVariable,
            InstanceVariable,
            Parameter,
            Constant,
            PrivateMethod
        }

        /// <summary>
        /// Dialog form for renaming variables
        /// </summary>
        private class RenameVariableDialog : Form
        {
            private TextBox txtNewName = new();
            private Button btnOk = new();
            private Button btnCancel = new();
            private Label lblPrompt = new();
            private Panel headerPanel = new();
            private Label headerLabel = new();

            public string NewVariableName { get; private set; }
            private readonly VariableKind variableKind;

            public RenameVariableDialog(string initialName = "", VariableKind varKind = VariableKind.Local)
            {
                NewVariableName = initialName.StartsWith('&') ? initialName.Substring(1) : initialName;
                this.variableKind = varKind;



                InitializeComponent();

                txtNewName.Text = NewVariableName;

                // Set focus to the text box
                this.ActiveControl = txtNewName;
                txtNewName.SelectAll();
            }

            private void InitializeComponent()
            {
                this.SuspendLayout();

                // headerPanel
                this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
                this.headerPanel.Dock = DockStyle.Top;
                this.headerPanel.Height = 30;
                this.headerPanel.Controls.Add(this.headerLabel);

                // headerLabel
                this.headerLabel.Text = GetHeaderText();
                this.headerLabel.ForeColor = Color.White;
                this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                this.headerLabel.Dock = DockStyle.Fill;
                this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

                // lblPrompt
                this.lblPrompt.AutoSize = true;
                this.lblPrompt.Location = new System.Drawing.Point(12, 40);
                this.lblPrompt.Name = "lblPrompt";
                this.lblPrompt.Size = new System.Drawing.Size(116, 15);
                this.lblPrompt.TabIndex = 0;
                this.lblPrompt.Text = GetPromptText();

                // txtNewName
                this.txtNewName.BorderStyle = BorderStyle.FixedSingle;
                this.txtNewName.Location = new System.Drawing.Point(12, 60);
                this.txtNewName.Name = "txtNewName";
                this.txtNewName.Size = new System.Drawing.Size(260, 23);
                this.txtNewName.TabIndex = 1;
                this.txtNewName.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
                this.txtNewName.KeyDown += TxtNewName_KeyDown;

                // btnOk
                this.btnOk.DialogResult = DialogResult.OK;
                this.btnOk.Location = new System.Drawing.Point(116, 95);
                this.btnOk.Name = "btnOk";
                this.btnOk.Size = new System.Drawing.Size(75, 28);
                this.btnOk.TabIndex = 2;
                this.btnOk.Text = "&OK";
                this.btnOk.UseVisualStyleBackColor = true;
                this.btnOk.Click += BtnOk_Click;

                // btnCancel
                this.btnCancel.DialogResult = DialogResult.Cancel;
                this.btnCancel.Location = new System.Drawing.Point(197, 95);
                this.btnCancel.Name = "btnCancel";
                this.btnCancel.Size = new System.Drawing.Size(75, 28);
                this.btnCancel.TabIndex = 3;
                this.btnCancel.Text = "&Cancel";
                this.btnCancel.UseVisualStyleBackColor = true;

                // RenameVariableDialog
                this.AcceptButton = this.btnOk;
                this.CancelButton = this.btnCancel;
                this.ClientSize = new System.Drawing.Size(284, 135);
                this.Controls.Add(this.btnCancel);
                this.Controls.Add(this.btnOk);
                this.Controls.Add(this.txtNewName);
                this.Controls.Add(this.lblPrompt);
                this.Controls.Add(this.headerPanel);
                this.FormBorderStyle = FormBorderStyle.None;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.Name = "RenameVariableDialog";
                this.StartPosition = FormStartPosition.CenterParent;
                this.Text = GetHeaderText();
                this.ShowInTaskbar = false;
                this.ResumeLayout(false);
                this.PerformLayout();
            }

            private string GetHeaderText()
            {
                return variableKind switch
                {
                    VariableKind.Instance => "Rename Instance Variable",
                    VariableKind.Parameter => "Rename Parameter",
                    _ => "Rename Variable"
                };
            }

            private string GetPromptText()
            {
                return variableKind switch
                {
                    VariableKind.Instance => "Enter new instance variable name:",
                    VariableKind.Parameter => "Enter new parameter name:",
                    _ => "Enter new variable name:"
                };
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                NewVariableName = txtNewName.Text.Trim();
                if (!NewVariableName.StartsWith("&") && variableKind != VariableKind.Property)
                    NewVariableName = "&" + NewVariableName;

                DialogResult = DialogResult.OK;
            }

            private void TxtNewName_KeyDown(object? sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    BtnOk_Click(sender, e);
                }
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        /// <summary>
        /// Shows the dialog for this refactor
        /// </summary>
        public override bool ShowRefactorDialog()
        {
            if (variableToRename is null)
            {
                SetFailure("No variable, parameter, or method found at cursor position.");
                return false;
            }

            using var dialog = new RenameVariableDialog(variableToRename.Name, variableToRename.Kind);
            var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
            DialogResult result = dialog.ShowDialog(wrapper);

            if (result == DialogResult.OK)
            {
                newVariableName = dialog.NewVariableName;
                GenerateRenameChanges();
                return true;
            }

            return false;
        }

        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);
        }
        protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, object> customData)
        {

            foreach (var testScope in GetAllScopes())
            {
                foreach (var variable in GetVariablesInScope(testScope))
                {
                    if (variable.References.Any(r => r.SourceSpan.ContainsPosition(CurrentPosition)))
                    {
                        variableToRename = variable;
                        targetScope = testScope;
                        return;
                    }
                }
            }

        }


        /// <summary>
        /// Handles function calls and tracks variable usage in member access scenarios
        /// </summary>


        /// <summary>
        /// Resets the refactor state for a new analysis
        /// </summary>
        protected override void OnReset()
        {
            variableToRename = null;
            newVariableName = null;
            targetScope = null;
        }

        private void GenerateRenameChanges()
        {
            if (variableToRename == null || targetScope == null || string.IsNullOrWhiteSpace(newVariableName))
            {
                SetFailure("No variable, or parameter found at cursor position.");
                return;
            }

            // Sort by position in reverse order to maintain accuracy when making replacements
            var targetReferences = GetVariableReferences(variableToRename.Name, targetScope);

            //targetReferences = targetReferences.OrderByDescending(r => r.Start.ByteIndex).ToList();
            foreach (var varRef in targetReferences)
            {
                // start and end are inclusive, so subtract 1 from the end, because SourceSpan has the upper bound as exclusive
                EditText(varRef.SourceSpan.Start.ByteIndex, varRef.SourceSpan.End.ByteIndex - 1,
                            newVariableName ?? variableToRename.Name, $"Rename {variableToRename} to {newVariableName}");
            }
        }
    }
}