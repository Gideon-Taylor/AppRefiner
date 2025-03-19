using Antlr4.Runtime.Misc;
using AppRefiner.Linters.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RenameLocalVariable"/> class
    /// </summary>
    /// <param name="editor">The Scintilla editor instance</param>
    public class RenameLocalVariable(ScintillaEditor editor) : ScopedRefactor<List<(int, int)>>(editor)
    {
        /// <summary>
        /// Gets the display name of this refactoring operation
        /// </summary>
        public new static string RefactorName => "Rename Variable";

        /// <summary>
        /// Gets the description of this refactoring operation
        /// </summary>
        public new static string RefactorDescription => "Rename a local variable and all its references";

        private string? newVariableName;
        private string? variableToRename;
        private Dictionary<string, List<(int, int)>>? targetScope;
        
        /// <summary>
        /// Indicates that this refactor requires a user input dialog
        /// </summary>
        public override bool RequiresUserInputDialog => true;

        /// <summary>
        /// Indicates that this refactor should have a keyboard shortcut registered
        /// </summary>
        public new static bool RegisterKeyboardShortcut => true;

        /// <summary>
        /// Gets the keyboard shortcut modifier keys for this refactor
        /// </summary>
        public new static ModifierKeys ShortcutModifiers => ModifierKeys.Control | ModifierKeys.Shift;

        /// <summary>
        /// Gets the keyboard shortcut key for this refactor
        /// </summary>
        public new static Keys ShortcutKey => Keys.R;

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

            public RenameVariableDialog(string initialName = "")
            {
                NewVariableName = initialName;
                InitializeComponent();
                txtNewName.Text = initialName.TrimStart('&');
                
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
                this.headerLabel.Text = "Rename Variable";
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
                this.lblPrompt.Text = "Enter new variable name:";
                
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
                this.Text = "Rename Variable";
                this.ShowInTaskbar = false;
                this.ResumeLayout(false);
                this.PerformLayout();
            }

            private void TxtNewName_KeyDown(object? sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    ValidateAndAccept();
                    e.Handled = true;
                }
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                ValidateAndAccept();
            }
            
            private void ValidateAndAccept()
            {
                string name = txtNewName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show("Please enter a variable name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                    return;
                }

                // Ensure the variable name starts with &
                if (!name.StartsWith('&'))
                {
                    name = $"&{name}";
                }

                NewVariableName = name;
                DialogResult = DialogResult.OK;
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
        /// Shows the dialog to get the new variable name from the user
        /// </summary>
        /// <returns>True if the user confirmed, false if canceled</returns>
        public override bool ShowRefactorDialog()
        {
            using var dialog = new RenameVariableDialog(newVariableName ?? "");
            
            // Show dialog with the specified owner
            var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
            DialogResult result = dialog.ShowDialog(wrapper);

            // If user confirmed, update the variable name
            if (result == DialogResult.OK)
            {
                newVariableName = dialog.NewVariableName;
                return true;
            }

            return false;
        }

        // Called when a variable is declared
        protected override void OnVariableDeclared(VariableInfo varInfo)
        {
            // Add this declaration to our tracking
            AddOccurrence(varInfo.Name, varInfo.Span);

            // Check if cursor is within this variable declaration
            if (varInfo.Span.Item1 <= CurrentPosition && CurrentPosition <= varInfo.Span.Item2 + 1)
            {
                variableToRename = varInfo.Name;
                targetScope = GetCurrentScope();
            }
        }

        // Override the base method for tracking variable usage
        public override void EnterIdentUserVariable(IdentUserVariableContext context)
        {
            base.EnterIdentUserVariable(context);

            string varName = context.GetText();
            var span = (context.Start.StartIndex, context.Stop.StopIndex);

            AddOccurrence(varName, span, true);

            // Check if cursor is within this variable reference
            if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
            {
                variableToRename = varName;
                targetScope = GetCurrentScope();
            }
        }

        // Helper method to add an occurrence to the appropriate scope
        private void AddOccurrence(string varName, (int, int) span, bool mustExist = false)
        {

            // If not found, add to current scope
            var currentScope = GetCurrentScope();

            if (mustExist && !currentScope.ContainsKey(varName))
            {
                return;
            }

            if (!currentScope.ContainsKey(varName))
            {
                currentScope[varName] = new List<(int, int)>();
            }
            currentScope[varName].Add(span);
        }

        public override void ExitProgram([NotNull] ProgramContext context)
        {
            GenerateChanges();
        }
        // Generate the refactoring changes
        public void GenerateChanges()
        {
            if (variableToRename == null || targetScope == null || newVariableName == null)
            {
                // No variable found at cursor position
                SetFailure("No variable found at cursor position. Please place cursor on a variable name.");
                return;
            }

            targetScope.TryGetValue(variableToRename, out var allOccurrences);

            /* If newVariableName is already in the scope, report failure to the user, cannot rename to existing variable name */
            if (targetScope.ContainsKey(newVariableName))
            {
                SetFailure($"Variable '{newVariableName}' already exists in the current scope. Please choose a different name.");
                return;
            }

            if (allOccurrences == null || allOccurrences.Count == 0)
            {
                // No occurrences found
                SetFailure($"Target '{variableToRename}' is not a local variable. Only local variables can be renamed.");
                return;
            }

            // Sort occurrences in reverse order to avoid position shifting
            allOccurrences.Sort((a, b) => b.Item1.CompareTo(a.Item1));

            // Generate replacement changes for each occurrence
            foreach (var (start, end) in allOccurrences)
            {
                ReplaceText(
                    start,
                    end,
                    newVariableName,
                    $"Rename variable '{variableToRename}' to '{newVariableName}'"
                );
            }
        }
    }
}
