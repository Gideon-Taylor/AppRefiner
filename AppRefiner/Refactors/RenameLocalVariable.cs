using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using AppRefiner.Services;
using System.Drawing;
using System.Windows.Forms;

namespace AppRefiner.Refactors
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
        
            // Store discovered variable information for post-traversal use
    private List<SourceSpan>? storedVariableReferences;
    private PeopleCodeParser.SelfHosted.Visitors.Models.ScopeInfo? storedTargetScope;
    private VariableType discoveredVariableType;
    private PeopleCodeParser.SelfHosted.Visitors.Models.ScopeInfo? lastExitedScope;

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

            /// <summary>
    /// Checks if the cursor position is on any variable identifier in the given scope
    /// and stores the variable information for later use
    /// </summary>
    private void CheckForTargetVariableInScope(PeopleCodeParser.SelfHosted.Visitors.Models.ScopeInfo scope)
    {
        // Skip if we already found our target variable
        if (variableToRename != null)
            return;

        // Check all variables in the current scope
        var currentVariableScope = GetCurrentVariableScope();
        foreach (var variableEntry in currentVariableScope)
        {
            string variableName = variableEntry.Key;
            var variableInfo = variableEntry.Value;

            // Check if cursor is on the variable's declaration
            if (variableInfo.VariableNameInfo.Token != null &&
                variableInfo.VariableNameInfo.Token.SourceSpan.ContainsPosition(CurrentPosition))
            {
                // Found our target variable!
                variableToRename = variableName;
                storedVariableReferences = new List<SourceSpan>();
                storedVariableReferences.Add(variableInfo.VariableNameInfo.Token.SourceSpan);
                storedTargetScope = scope;
                discoveredVariableType = variableInfo.VariableType;
                
                // Determine variable type based on scope
                DetermineIdentifierTypeFromScope(scope, variableInfo.VariableType);
                return;
            }
        }
        }

            /// <summary>
    /// Determines the type of identifier based on the scope hierarchy and variable type
    /// </summary>
    private void DetermineIdentifierTypeFromScope(PeopleCodeParser.SelfHosted.Visitors.Models.ScopeInfo scope, VariableType variableType)
    {
        // Reset flags
        isInstanceVariable = false;
        isParameter = false;
        isConstant = false;
        isPrivateMethod = false;

        // Set flags based on variable type
        switch (variableType)
        {
            case VariableType.Instance:
                isInstanceVariable = true;
                break;
            case VariableType.Parameter:
                isParameter = true;
                break;
            case VariableType.Constant:
                isConstant = true;
                break;
            case VariableType.Local:
            case VariableType.Global:
            case VariableType.Property:
                // These are handled by default
                break;
        }
        }

        /// <summary>
        /// Override VisitFunction to use post-traversal discovery
        /// </summary>
        public override void VisitFunction(FunctionNode node)
        {
            // Let the base class handle scope entry and complete traversal
            base.VisitFunction(node);
            
            // After traversal, check if cursor is on any identifier in this scope
            if (lastExitedScope != null && variableToRename == null)
            {
                CheckForTargetVariableInScope(lastExitedScope);
            }
        }

            protected override void OnExitScope(PeopleCodeParser.SelfHosted.Visitors.Models.ScopeInfo scopeInfo, Dictionary<string, VariableInfo> variableScope, Dictionary<string, object> customData)
    {
        lastExitedScope = scopeInfo;
    }

        /// <summary>
        /// Override VisitMethod to use post-traversal discovery
        /// </summary>
        public override void VisitMethod(MethodNode node)
        {
            // Let the base class handle scope entry and complete traversal
            base.VisitMethod(node);
            
            // After traversal, check if cursor is on any identifier in this scope
            if (CurrentScope != null && variableToRename == null)
            {
                CheckForTargetVariableInScope(CurrentScope);
            }
        }

        /// <summary>
        /// Override VisitProgram to handle global scope variables
        /// </summary>
        public override void VisitProgram(ProgramNode node)
        {
            // Let the base class handle scope entry and complete traversal
            base.VisitProgram(node);
            
            // After traversal, check if cursor is on any identifier in this scope
            if (CurrentScope != null && variableToRename == null)
            {
                CheckForTargetVariableInScope(CurrentScope);
            }
        }

        /// <summary>
        /// Override VisitBlock to handle block-level variables
        /// </summary>
        public override void VisitBlock(BlockNode node)
        {
            // Let the base class handle scope entry and complete traversal
            base.VisitBlock(node);
            
            // After traversal, check if cursor is on any identifier in this scope
            if (CurrentScope != null && variableToRename == null)
            {
                CheckForTargetVariableInScope(CurrentScope);
            }
        }


        private void GenerateRenameChanges()
        {
            if (string.IsNullOrEmpty(variableToRename) || string.IsNullOrEmpty(newVariableName))
            {
                return;
            }

            // Use the stored references instead of trying to find them again
            // (since scopes have been exited by the time this method runs)
            if (storedVariableReferences != null)
            {
                            // Sort by position in reverse order to maintain accuracy when making replacements
            var sortedReferences = storedVariableReferences.OrderByDescending(r => r.Start.ByteIndex).ToList();
            
            foreach (var span in sortedReferences)
            {
                EditText(span.Start.ByteIndex, span.End.ByteIndex, 
                         newVariableName, $"Rename {variableToRename} to {newVariableName}");
            }
            }
        }
    }
}