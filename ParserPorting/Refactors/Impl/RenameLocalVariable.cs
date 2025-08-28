using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted;
using AppRefiner.Services;
using AppRefiner;
using System.Drawing;
using System.Windows.Forms;

namespace ParserPorting.Refactors.Impl
{
    /// <summary>
    /// Renames a local variable, parameter, private instance variable, private method, or private constant and all its references
    /// </summary>
    public class RenameLocalVariable : ScopedRefactor
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
        private string? variableToRename;
        private bool isInstanceVariable = false;
        private bool isParameter = false;
        private bool isConstant = false;
        private bool isPrivateMethod = false;
        
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

        public RenameLocalVariable(AppRefiner.ScintillaEditor editor) : base(editor)
        {
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
            private readonly RenameTokenType tokenType;

            public RenameVariableDialog(string initialName = "", RenameTokenType tokenType = RenameTokenType.LocalVariable)
            {
                NewVariableName = initialName;
                this.tokenType = tokenType;
                InitializeComponent();
                
                // For methods, don't add the & prefix
                txtNewName.Text = tokenType == RenameTokenType.PrivateMethod ? initialName : initialName.TrimStart('&');
                
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
                return tokenType switch
                {
                    RenameTokenType.InstanceVariable => "Rename Instance Variable",
                    RenameTokenType.Parameter => "Rename Parameter",
                    RenameTokenType.Constant => "Rename Constant",
                    RenameTokenType.PrivateMethod => "Rename Private Method",
                    _ => "Rename Variable"
                };
            }

            private string GetPromptText()
            {
                return tokenType switch
                {
                    RenameTokenType.InstanceVariable => "Enter new instance variable name:",
                    RenameTokenType.Parameter => "Enter new parameter name:",
                    RenameTokenType.Constant => "Enter new constant name:",
                    RenameTokenType.PrivateMethod => "Enter new method name:",
                    _ => "Enter new variable name:"
                };
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                NewVariableName = txtNewName.Text.Trim();
                
                if (tokenType != RenameTokenType.PrivateMethod && !NewVariableName.StartsWith("&"))
                {
                    NewVariableName = "&" + NewVariableName;
                }
                
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
            if (string.IsNullOrEmpty(variableToRename))
            {
                SetFailure("No variable, parameter, or method found at cursor position.");
                return false;
            }

            var tokenType = DetermineTokenType();
            using var dialog = new RenameVariableDialog(variableToRename, tokenType);
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

        private RenameTokenType DetermineTokenType()
        {
            if (isPrivateMethod) return RenameTokenType.PrivateMethod;
            if (isInstanceVariable) return RenameTokenType.InstanceVariable;
            if (isParameter) return RenameTokenType.Parameter;
            if (isConstant) return RenameTokenType.Constant;
            return RenameTokenType.LocalVariable;
        }

        public override void VisitIdentifier(IdentifierNode node)
        {
            // Check if this identifier is at the cursor position
            if (node.SourceSpan.IsValid && 
                node.SourceSpan.ContainsPosition(CurrentPosition))
            {
                variableToRename = node.Name;
                
                // Determine the type of identifier based on context
                DetermineIdentifierType(node);
            }

            base.VisitIdentifier(node);
        }

        private void DetermineIdentifierType(IdentifierNode node)
        {
            // Find variable references in current scope chain
            var references = FindVariableReferences(node.Name);
            
            if (references != null && references.Count > 0)
            {
                var firstRef = references.First();
                if (firstRef.Type == VariableReferenceType.Declaration)
                {
                    // Check the scope level to determine variable type
                    var containingScopes = GetScopesContaining(CurrentPosition).ToList();
                    
                    foreach (var scope in containingScopes)
                    {
                        if (scope.Type == ScopeType.Class)
                        {
                            isInstanceVariable = true;
                            break;
                        }
                        else if (scope.Type == ScopeType.Method || scope.Type == ScopeType.Function)
                        {
                            // Could be a parameter or local variable
                            // Additional logic would be needed to distinguish
                            break;
                        }
                    }
                }
            }
        }

        private void GenerateRenameChanges()
        {
            if (string.IsNullOrEmpty(variableToRename) || string.IsNullOrEmpty(newVariableName))
            {
                return;
            }

            // Find all references to the variable in the current scope chain
            var references = FindVariableReferences(variableToRename);
            
            if (references != null)
            {
                // Sort by position in reverse order to maintain accuracy when making replacements
                var sortedReferences = references.OrderByDescending(r => r.Location.Start.Index).ToList();
                
                foreach (var reference in sortedReferences)
                {
                    EditText(reference.Location.Start.Index, reference.Location.End.Index, 
                             newVariableName, $"Rename {variableToRename} to {newVariableName}");
                }
            }
        }
    }
}