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
        public new static string RefactorDescription => "Rename a local variable, parameter, private instance variable, or private constant and all its references";

        private string? newVariableName;
        private string? variableToRename;
        private Dictionary<string, List<(int, int)>>? targetScope;
        private bool isInstanceVariable = false;
        private bool isParameter = false;
        private bool isConstant = false;
        
        // Track method parameters for later association with method scopes
        private readonly Dictionary<string, List<(string paramName, (int, int) span)>> pendingMethodParameters = new();
        private string? currentMethodName;
        
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
        
        // Track method header declarations to associate parameters later
        public override void EnterMethodHeader(MethodHeaderContext context)
        {
            base.EnterMethodHeader(context);
            
            // Store the method name for later use with parameters
            var methodName = context.genericID().GetText();
            currentMethodName = methodName;
            
            // Initialize the parameter list for this method if it doesn't exist
            if (!pendingMethodParameters.ContainsKey(methodName))
            {
                pendingMethodParameters[methodName] = new List<(string, (int, int))>();
            }
        }
        
        // Handle method parameters - store them for later association with method scope
        public override void EnterMethodArgument(MethodArgumentContext context)
        {
            base.EnterMethodArgument(context);
            
            if (currentMethodName == null)
            {
                return; // Safety check
            }
            
            var varNode = context.USER_VARIABLE();
            if (varNode != null)
            {
                string varName = varNode.GetText();
                var span = (varNode.Symbol.StartIndex, varNode.Symbol.StopIndex);
                
                // Store the parameter for later association with method scope
                pendingMethodParameters[currentMethodName].Add((varName, span));
                
                // Check if cursor is within this parameter
                if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
                {
                    variableToRename = varName;
                    isParameter = true;
                    // We'll set targetScope when we enter the method implementation
                }
            }
        }
        
        // When entering a method implementation, associate any pending parameters with this scope
        public override void EnterMethod(MethodContext context)
        {
            base.EnterMethod(context);
            
            var methodName = context.genericID().GetText();
            
            // Check if we have pending parameters for this method
            if (pendingMethodParameters.TryGetValue(methodName, out var parameters))
            {
                var currentScope = GetCurrentScope();
                
                // Add each parameter to the current method scope
                foreach (var (paramName, span) in parameters)
                {
                    if (!currentScope.ContainsKey(paramName))
                    {
                        currentScope[paramName] = new List<(int, int)>();
                    }
                    currentScope[paramName].Add(span);
                    
                    // If this is the parameter we want to rename, update targetScope
                    if (isParameter && variableToRename == paramName && targetScope == null)
                    {
                        targetScope = currentScope;
                    }
                }
            }
        }
        
        // Track function definitions to associate parameters
        public override void EnterFunctionDefinition(FunctionDefinitionContext context)
        {
            base.EnterFunctionDefinition(context);
            
            // Process function arguments if the cursor is on a parameter
            if (isParameter && variableToRename != null && targetScope == null)
            {
                var currentScope = GetCurrentScope();
                
                // If we have a parameter to rename but no target scope yet,
                // this is the right scope for function parameters
                targetScope = currentScope;
            }
        }
        
        // Handle function parameters directly in the function scope
        public override void EnterFunctionArgument(FunctionArgumentContext context)
        {
            base.EnterFunctionArgument(context);
            
            var varNode = context.USER_VARIABLE();
            if (varNode != null)
            {
                string varName = varNode.GetText();
                var span = (varNode.Symbol.StartIndex, varNode.Symbol.StopIndex);
                
                // Add to current scope (which should be the function scope)
                var currentScope = GetCurrentScope();
                if (!currentScope.ContainsKey(varName))
                {
                    currentScope[varName] = new List<(int, int)>();
                }
                currentScope[varName].Add(span);
                
                // Check if cursor is within this parameter
                if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
                {
                    variableToRename = varName;
                    targetScope = currentScope;
                    isParameter = true;
                }
            }
        }
        
        // Handle private instance variables
        public override void EnterPrivateProperty(PrivatePropertyContext context)
        {
            base.EnterPrivateProperty(context);
            
            var instanceDeclContext = context.instanceDeclaration();
            if (instanceDeclContext is InstanceDeclContext instanceDecl)
            {
                // Process each variable in the instance declaration
                foreach (var varNode in instanceDecl.USER_VARIABLE())
                {
                    string varName = varNode.GetText();
                    var span = (varNode.Symbol.StartIndex, varNode.Symbol.StopIndex);
                    
                    // Add to global scope (first scope in the stack)
                    var globalScope = scopeStack.Last();
                    if (!globalScope.ContainsKey(varName))
                    {
                        globalScope[varName] = new List<(int, int)>();
                    }
                    globalScope[varName].Add(span);
                    
                    // Check if cursor is within this instance variable
                    if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
                    {
                        variableToRename = varName;
                        targetScope = globalScope;
                        isInstanceVariable = true;
                    }
                }
            }
        }
        
        // Handle private constant declarations
        public override void EnterPrivateConstant(PrivateConstantContext context)
        {
            base.EnterPrivateConstant(context);
            
            var constDeclContext = context.constantDeclaration();
            if (constDeclContext != null)
            {
                var varNode = constDeclContext.USER_VARIABLE();
                if (varNode != null)
                {
                    string varName = varNode.GetText();
                    var span = (varNode.Symbol.StartIndex, varNode.Symbol.StopIndex);
                    
                    // Add to global scope (first scope in the stack)
                    var globalScope = scopeStack.Last();
                    if (!globalScope.ContainsKey(varName))
                    {
                        globalScope[varName] = new List<(int, int)>();
                    }
                    globalScope[varName].Add(span);
                    
                    // Check if cursor is within this constant variable
                    if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
                    {
                        variableToRename = varName;
                        targetScope = globalScope;
                        isConstant = true;
                    }
                }
            }
        }
        
        // Handle top-level constants in non-class programs
        public override void EnterConstantDeclaration(ConstantDeclarationContext context)
        {
            base.EnterConstantDeclaration(context);
            
            var varNode = context.USER_VARIABLE();
            if (varNode != null)
            {
                string varName = varNode.GetText();
                var span = (varNode.Symbol.StartIndex, varNode.Symbol.StopIndex);
                
                // Add to current scope
                var currentScope = GetCurrentScope();
                if (!currentScope.ContainsKey(varName))
                {
                    currentScope[varName] = new List<(int, int)>();
                }
                currentScope[varName].Add(span);
                
                // Check if cursor is within this constant variable
                if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
                {
                    variableToRename = varName;
                    targetScope = currentScope;
                    isConstant = true;
                }
            }
        }
        
        // Handle method parameter annotations that appear after the method header
        public override void EnterMethodParameterAnnotation(MethodParameterAnnotationContext context)
        {
            base.EnterMethodParameterAnnotation(context);
            
            var methodArgCtx = context.methodArgument();
            if (methodArgCtx != null)
            {
                var varNode = methodArgCtx.USER_VARIABLE();
                if (varNode != null)
                {
                    string varName = varNode.GetText();
                    var span = (varNode.Symbol.StartIndex, varNode.Symbol.StopIndex);
                    
                    // Add this parameter annotation to the current scope
                    AddOccurrence(varName, span, true);
                    
                    // Check if cursor is within this parameter annotation
                    if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
                    {
                        variableToRename = varName;
                        targetScope = GetCurrentScope();
                        isParameter = true;
                    }
                }
            }
        }

        // Helper method to add an occurrence to the appropriate scope
        private void AddOccurrence(string varName, (int, int) span, bool mustExist = false)
        {
            // For instance variables or constants, check the global scope first
            if (!mustExist && (isInstanceVariable || isConstant) && variableToRename == varName)
            {
                var globalScope = scopeStack.Last();
                if (!globalScope.ContainsKey(varName))
                {
                    globalScope[varName] = new List<(int, int)>();
                }
                globalScope[varName].Add(span);
                return;
            }
            
            // For regular variables, try to find in any scope
            if (mustExist)
            {
                foreach (var scope in scopeStack)
                {
                    if (scope.ContainsKey(varName))
                    {
                        scope[varName].Add(span);
                        return;
                    }
                }
                return;
            } 

            // If not found, add to current scope
            var currentScope = GetCurrentScope();

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
                string errorVarType = isInstanceVariable ? "private instance variable" : 
                                      isParameter ? "parameter" : 
                                      isConstant ? "private constant" : "local variable";
                SetFailure($"Target '{variableToRename}' is not a {errorVarType}. Only {errorVarType}s can be renamed.");
                return;
            }

            // Sort occurrences in reverse order to avoid position shifting
            allOccurrences.Sort((a, b) => b.Item1.CompareTo(a.Item1));

            string varTypeDescription = isInstanceVariable ? "private instance variable" : 
                                  isParameter ? "parameter" : 
                                  isConstant ? "private constant" : "local variable";
            
            // Generate replacement changes for each occurrence
            foreach (var (start, end) in allOccurrences)
            {
                ReplaceText(
                    start,
                    end,
                    newVariableName,
                    $"Rename {varTypeDescription} '{variableToRename}' to '{newVariableName}'"
                );
            }
        }
    }
}
