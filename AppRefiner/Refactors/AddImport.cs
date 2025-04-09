using Antlr4.Runtime;
using AppRefiner.PeopleCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using System.Windows.Forms; // Required for Form, Button, TextBox, etc.
using System.Drawing; // Required for Color, Font, Point, Size, etc.
using Antlr4.Runtime.Tree; // Required for ITerminalNode


namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that adds a specific application class import if it's not already covered by an existing explicit or wildcard import.
    /// </summary>
    public class AddImport : BaseRefactor
    {
        public new static string RefactorName => "Add Import";
        public new static string RefactorDescription => "Adds a specific application class import if not already covered.";

        /// <summary>
        /// This refactor should not have a keyboard shortcut.
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        private string? _appClassPathToAdd;
        private readonly bool _requiresInput;
        private ImportsBlockContext? _importsBlockContext;
        // Stores the full text of each existing import statement (e.g., "import PKG:CLASS;")
        private readonly List<string> _existingImportStatements = [];
        // Stores just the path part of existing imports (e.g., "PKG:CLASS", "PKG:*") for coverage check
        private readonly List<string> _existingImportPaths = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="AddImport"/> class with a specific path.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        /// <param name="appClassPathToAdd">The application class path to add.</param>
        public AddImport(ScintillaEditor editor, string appClassPathToAdd) : base(editor)
        {
            _appClassPathToAdd = appClassPathToAdd?.Trim();
            _requiresInput = string.IsNullOrWhiteSpace(_appClassPathToAdd);
            if (!_requiresInput && !IsValidAppClassPath(_appClassPathToAdd!))
            {
                 // If an invalid path is provided directly, treat it as needing input
                 _requiresInput = true;
                 _appClassPathToAdd = null;
                 // We won't set failure here, let ShowRefactorDialog handle it or ExitProgram if dialog not shown.
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddImport"/> class, requiring user input for the path.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        public AddImport(ScintillaEditor editor) : base(editor)
        {
            _appClassPathToAdd = null;
            _requiresInput = true;
        }

        /// <summary>
        /// Gets a value indicating whether this refactor requires user input via a dialog.
        /// </summary>
        public override bool RequiresUserInputDialog => _requiresInput;

        /// <summary>
        /// Dialog form for adding an import
        /// </summary>
        private class AddImportDialog : Form
        {
            private TextBox txtAppClassPath = new();
            private Button btnOk = new();
            private Button btnCancel = new();
            private Label lblPrompt = new();
            private Panel headerPanel = new();
            private Label headerLabel = new();

            public string AppClassPath { get; private set; } = "";

            public AddImportDialog()
            {
                InitializeComponent();
                // Set focus to the text box
                this.ActiveControl = txtAppClassPath;
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
                this.headerLabel.Text = "Add Import";
                this.headerLabel.ForeColor = Color.White;
                this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                this.headerLabel.Dock = DockStyle.Fill;
                this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

                // lblPrompt
                this.lblPrompt.AutoSize = true;
                this.lblPrompt.Location = new System.Drawing.Point(12, 40);
                this.lblPrompt.Name = "lblPrompt";
                this.lblPrompt.Size = new System.Drawing.Size(240, 15);
                this.lblPrompt.TabIndex = 0;
                this.lblPrompt.Text = "Enter Application Class Path (e.g., PKG:SUB:Class):";

                // txtAppClassPath
                this.txtAppClassPath.BorderStyle = BorderStyle.FixedSingle;
                this.txtAppClassPath.Location = new System.Drawing.Point(12, 60);
                this.txtAppClassPath.Name = "txtAppClassPath";
                this.txtAppClassPath.Size = new System.Drawing.Size(360, 23); // Wider for class paths
                this.txtAppClassPath.TabIndex = 1;
                this.txtAppClassPath.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point); // Monospaced font
                this.txtAppClassPath.KeyDown += TxtAppClassPath_KeyDown;

                // btnOk
                this.btnOk.DialogResult = DialogResult.OK;
                this.btnOk.Location = new System.Drawing.Point(216, 95);
                this.btnOk.Name = "btnOk";
                this.btnOk.Size = new System.Drawing.Size(75, 28);
                this.btnOk.TabIndex = 2;
                this.btnOk.Text = "&OK";
                this.btnOk.UseVisualStyleBackColor = true;
                this.btnOk.Click += BtnOk_Click;

                // btnCancel
                this.btnCancel.DialogResult = DialogResult.Cancel;
                this.btnCancel.Location = new System.Drawing.Point(297, 95);
                this.btnCancel.Name = "btnCancel";
                this.btnCancel.Size = new System.Drawing.Size(75, 28);
                this.btnCancel.TabIndex = 3;
                this.btnCancel.Text = "&Cancel";
                this.btnCancel.UseVisualStyleBackColor = true;

                // AddImportDialog
                this.AcceptButton = this.btnOk;
                this.CancelButton = this.btnCancel;
                this.ClientSize = new System.Drawing.Size(384, 135); // Wider client size
                this.Controls.Add(this.btnCancel);
                this.Controls.Add(this.btnOk);
                this.Controls.Add(this.txtAppClassPath);
                this.Controls.Add(this.lblPrompt);
                this.Controls.Add(this.headerPanel);
                this.FormBorderStyle = FormBorderStyle.None; // Use None for custom border/header
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.Name = "AddImportDialog";
                this.StartPosition = FormStartPosition.CenterParent; // Center relative to parent
                this.Text = "Add Import"; // Title bar text (though not visible with None border)
                this.ShowInTaskbar = false; // Don't show in taskbar
                this.ResumeLayout(false);
                this.PerformLayout();
            }

            private void TxtAppClassPath_KeyDown(object? sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    BtnOk_Click(sender, e); // Trigger OK button logic
                    e.Handled = true;
                }
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                // Basic check for non-empty input - more specific validation happens after dialog closes
                if (!string.IsNullOrWhiteSpace(txtAppClassPath.Text))
                {
                    AppClassPath = txtAppClassPath.Text.Trim();
                    this.DialogResult = DialogResult.OK;
                }
                else
                {
                    // Prevent closing if empty
                    MessageBox.Show("Please enter an Application Class Path.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None; // Keeps the dialog open
                }
            }

            // Optional: Handle Escape key at the form level too
            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    return true; // Handled
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        /// <summary>
        /// Shows a dialog to get the application class path from the user if it wasn't provided initially.
        /// </summary>
        /// <returns><c>true</c> if valid input was received or no input was required; <c>false</c> otherwise.</returns>
        public override bool ShowRefactorDialog()
        {
            if (!RequiresUserInputDialog) return true; // No input needed

            string input = string.Empty;
            // Create wrapper once for both dialog and potential message box
            var wrapper = new WindowWrapper(GetEditorMainWindowHandle());

            using (var dialog = new AddImportDialog())
            {
                DialogResult result = dialog.ShowDialog(wrapper);

                if (result == DialogResult.OK)
                {
                    input = dialog.AppClassPath; // Get path from dialog property
                }
                else
                {
                     SetFailure("Operation cancelled by user.");
                     return false; // User cancelled
                }
            }

            // Now validate the input obtained from the dialog
            if (string.IsNullOrWhiteSpace(input)) // Should be caught by dialog, but double-check
            {
                SetFailure("No application class path provided.");
                return false;
            }

            input = input.Trim(); // Ensure no leading/trailing whitespace
            if (!IsValidAppClassPath(input))
            {
                // Provide feedback about the invalid format using the correct MessageBox overload
                 MessageBox.Show(
                    wrapper, // owner (IWin32Window)
                    $"The entered path '{input}' is not a valid Application Class Path format (must contain ':', cannot contain '*').", // text
                    "Invalid Input", // caption
                    MessageBoxButtons.OK, // buttons
                    MessageBoxIcon.Warning); // icon

                SetFailure($"Invalid Application Class Path format: {input}");
                return false;
            }

            _appClassPathToAdd = input;
            return true; // User provided valid input
        }

        /// <summary>
        /// Basic validation for Application Class Path format.
        /// Ensures it contains a colon and does not contain a wildcard.
        /// </summary>
        private bool IsValidAppClassPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.Contains(':') && !path.Contains('*');
        }

        /// <summary>
        /// Called when entering the imports block. Collects existing import statements and paths.
        /// </summary>
        public override void EnterImportsBlock(ImportsBlockContext context)
        {
            _importsBlockContext = context;
            _existingImportPaths.Clear();
            _existingImportStatements.Clear();

            foreach (var importDecl in context.importDeclaration())
            {
                string? importText = GetOriginalText(importDecl)?.Trim();
                 if (!string.IsNullOrEmpty(importText))
                 {
                     // Ensure it ends with a semicolon for consistency if missing (though parser should enforce)
                     if (!importText.EndsWith(";")) importText += ";";
                     _existingImportStatements.Add(importText);
                 }

                // Extract the path part for coverage checking
                var appClassPathCtx = importDecl.appClassPath();
                if (appClassPathCtx != null)
                {
                    _existingImportPaths.Add(appClassPathCtx.GetText());
                }
                else
                {
                    var appPkgAllCtx = importDecl.appPackageAll();
                    if (appPkgAllCtx != null)
                    {
                        // Store wildcard path (e.g., "APP_PKG:SUB_PKG:*")
                        _existingImportPaths.Add(appPkgAllCtx.GetText());
                    }
                }
            }
        }

        /// <summary>
        /// Called when exiting the program. Performs the import check and modification logic.
        /// </summary>
        public override void ExitProgram(ProgramContext context)
        {
            // If input was required but the dialog failed/was cancelled, GetResult() will be Failed.
            // If input wasn't required but was invalid in constructor, _appClassPathToAdd is null.
            if (string.IsNullOrWhiteSpace(_appClassPathToAdd))
            {
                 // If failure wasn't already set by ShowRefactorDialog, set it now.
                 if (!GetResult().Success) return; // Already failed
                 SetFailure("No valid application class path specified for import.");
                 return;
            }

            // Check if the class path is already covered by existing imports
            if (IsCovered(_appClassPathToAdd, _existingImportPaths))
            {
                // No changes needed. Set success message to inform user.
                if (_requiresInput)
                {
                    SetFailure($"Import '{_appClassPathToAdd}' is already covered by existing imports."); // Using SetFailure to show message without making changes. Consider a dedicated Info status later.
                }
                return;
            }

            // Prepare the new import statement
            string newImportStatement = $"import {_appClassPathToAdd};";

            // Combine existing and new imports
            var allImportStatements = new List<string>(_existingImportStatements);
            allImportStatements.Add(newImportStatement);

            // Sort the imports alphabetically by the path part, case-insensitively
            allImportStatements.Sort((s1, s2) => {
                // Extract path by removing "import " prefix and trailing ";"
                string path1 = s1.Length > 7 ? s1.Substring(7, s1.Length - (s1.EndsWith(";") ? 8 : 7)).Trim() : s1;
                string path2 = s2.Length > 7 ? s2.Substring(7, s2.Length - (s2.EndsWith(";") ? 8 : 7)).Trim() : s2;
                return string.Compare(path1, path2, StringComparison.OrdinalIgnoreCase);
            });

            // Generate the new imports block text, joined by newlines
            string newImportsBlockText = string.Join(Environment.NewLine, allImportStatements);

            // Apply the change: Replace existing block or insert new one
            if (_importsBlockContext != null)
            {
                 // Ensure the original context includes the trailing whitespace/newlines if possible,
                 // otherwise, the replacement might change formatting significantly.
                 // However, ReplaceNode replaces based on token indices, so it should be okay.
                ReplaceNode(_importsBlockContext, newImportsBlockText, $"Add import for {_appClassPathToAdd}");
            }
            else // No existing imports block
            {
                 // Insert the new imports at the beginning of the program
                var firstChild = context.GetChild(0);
                if (firstChild is ITerminalNode || firstChild is ParserRuleContext) // Check if there's anything to insert before
                {
                    var insertionPoint = context.Start.StartIndex;
                    if (firstChild is ParserRuleContext firstChildContext) {
                         insertionPoint = firstChildContext.Start.StartIndex;
                    } else if (firstChild is ITerminalNode firstTerminalNode) {
                         insertionPoint = firstTerminalNode.Symbol.StartIndex;
                    }

                    // Add standard spacing: imports block, blank line, then the rest
                    string insertText = newImportsBlockText + Environment.NewLine + Environment.NewLine;

                    // Check if the first *significant* node is already an import (unlikely if _importsBlockContext is null)
                    // This check might be less reliable without a full token stream analysis,
                    // but InsertBefore should handle placement correctly.
                    // We primarily want to ensure correct spacing.

                    InsertText(insertionPoint, insertText, $"Add import for {_appClassPathToAdd}");
                }
                else // Empty program? Insert at the very beginning.
                {
                    InsertText(context.Start?.StartIndex ?? 0,
                        newImportsBlockText + Environment.NewLine + Environment.NewLine,
                        $"Add import for {_appClassPathToAdd}");
                }
            }
        }

        /// <summary>
        /// Checks if a target application class path is covered by a list of existing import paths,
        /// considering both exact matches and wildcard imports.
        /// </summary>
        /// <param name="targetPath">The application class path to check (e.g., "PKG:SUB:CLASS").</param>
        /// <param name="existingImportPaths">A collection of existing import paths (e.g., "PKG:SUB:CLASS", "PKG:SUB:*", "PKG:*").</param>
        /// <returns><c>true</c> if the target path is covered; <c>false</c> otherwise.</returns>
        private bool IsCovered(string targetPath, IEnumerable<string> existingImportPaths)
        {
            // Extract the package part of the target path (e.g., "PKG:SUB" from "PKG:SUB:CLASS")
            string targetPackage = GetPackagePath(targetPath);
            // A valid class path must contain ':' and thus have a non-empty package path.
            if (string.IsNullOrEmpty(targetPackage) || targetPackage == targetPath) return false;

            foreach (string existingPath in existingImportPaths)
            {
                if (string.IsNullOrWhiteSpace(existingPath)) continue; // Skip empty paths

                if (existingPath.EndsWith(":*")) // It's a wildcard import
                {
                    // Get the package part of the wildcard (e.g., "PKG:SUB" from "PKG:SUB:*" or "PKG" from "PKG:*")
                    string wildcardPackage = GetPackagePath(existingPath); // Reuse helper

                    // Check if the target package IS the wildcard package (e.g., target PKG:SUB covered by PKG:SUB:*)
                    // OR if the target package starts with the wildcard package followed by a colon (e.g., target PKG:SUB:SUB2 covered by PKG:SUB:*)
                    if (targetPackage.Equals(wildcardPackage, StringComparison.OrdinalIgnoreCase) ||
                        targetPackage.StartsWith(wildcardPackage + ":", StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // Covered by wildcard
                    }
                }
                else // It's an explicit import
                {
                    if (existingPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // Exact match
                    }
                }
            }
            return false; // Not covered by any existing import
        }

         /// <summary>
         /// Helper to get the package part of a full application path (class or wildcard).
         /// Returns the part before the last colon. For "PKG:*", returns "PKG".
         /// </summary>
         /// <param name="fullPath">The full path (e.g., "PKG:SUB:CLASS" or "PKG:SUB:*").</param>
         /// <returns>The package path (e.g., "PKG:SUB"), or empty string if no colon exists or path is invalid.</returns>
         private string GetPackagePath(string fullPath)
         {
             if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;

             int lastColon = fullPath.LastIndexOf(':');
             if (lastColon > 0) // Ensure colon is not the first character
             {
                 return fullPath.Substring(0, lastColon);
             }
             // Handle cases like "PKG" (invalid class/wildcard path) - should not match anything
             return string.Empty;
         }
    }
} 