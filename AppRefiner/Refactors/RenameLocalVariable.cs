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
        public new static string RefactorName => "Rename Variable or Method";

        /// <summary>
        /// Gets the description of this refactoring operation
        /// </summary>
        public new static string RefactorDescription => "Rename a local variable, parameter, private instance variable, private method, or private constant and all its references";

        private string? newVariableName;
        private string? variableToRename;
        private Dictionary<string, List<(int, int)>>? targetScope;
        private bool isInstanceVariable = false;
        private bool isParameter = false;
        private bool isConstant = false;
        private bool isPrivateMethod = false;
        
        // Track method parameters for later association with method scopes
        private readonly Dictionary<string, List<(string paramName, (int, int) span)>> pendingMethodParameters = new();
        private string? currentMethodName;
        
        // Track private methods in class declarations
        private readonly Dictionary<string, (int, int)> privateMethods = new();
        private readonly Dictionary<string, List<(int, int)>> methodCalls = new();
        private readonly Dictionary<string, List<(int, int)>> methodImplementations = new();
        
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
        public new static ModifierKeys ShortcutModifiers => ModifierKeys.Control | ModifierKeys.Shift;

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
            private RenameTokenType tokenType;

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
                    RenameTokenType.PrivateMethod => "Rename Method",
                    RenameTokenType.Parameter => "Rename Parameter",
                    RenameTokenType.InstanceVariable => "Rename Instance Variable",
                    RenameTokenType.Constant => "Rename Constant",
                    _ => "Rename Variable"
                };
            }

            private string GetPromptText()
            {
                return tokenType switch
                {
                    RenameTokenType.PrivateMethod => "Enter new method name:",
                    RenameTokenType.Parameter => "Enter new parameter name:",
                    RenameTokenType.InstanceVariable => "Enter new instance variable name:",
                    RenameTokenType.Constant => "Enter new constant name:",
                    _ => "Enter new variable name:"
                };
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
                    MessageBox.Show("Please enter a name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                    return;
                }

                // Ensure the variable name starts with & if it's not a method
                if (tokenType != RenameTokenType.PrivateMethod && !name.StartsWith('&'))
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
        /// Determines the type of token being renamed
        /// </summary>
        private RenameTokenType GetTokenType()
        {
            if (isPrivateMethod)
                return RenameTokenType.PrivateMethod;
            if (isInstanceVariable)
                return RenameTokenType.InstanceVariable;
            if (isParameter)
                return RenameTokenType.Parameter;
            if (isConstant)
                return RenameTokenType.Constant;
            return RenameTokenType.LocalVariable;
        }

        /// <summary>
        /// Shows the dialog to get the new variable name from the user
        /// </summary>
        /// <returns>True if the user confirmed, false if canceled</returns>
        public override bool ShowRefactorDialog()
        {
            using var dialog = new RenameVariableDialog(newVariableName ?? "", GetTokenType());
            
            // Show dialog with the specified owner
            var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
            DialogResult result = dialog.ShowDialog(wrapper);

            // If user confirmed, update the variable name
            if (result == DialogResult.OK)
            {
                newVariableName = dialog.NewVariableName;
                
                // Since we're using deferred dialog, generate changes now that we have user input
                GenerateChanges();
                
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
            
            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                var methodName = genericIdNode.GetText();
                currentMethodName = methodName;
                
                if (!pendingMethodParameters.ContainsKey(methodName))
                {
                    pendingMethodParameters[methodName] = new List<(string, (int, int))>();
                }
                
                var parentContext = context.Parent;
                if (parentContext is PrivateMethodHeaderContext)
                {
                    // Store just the span of the method name, not the entire header
                    var span = (genericIdNode.Start.StartIndex, genericIdNode.Stop.StopIndex);
                    privateMethods[methodName] = span;
                    
                    // Add to global scope for renaming
                    var globalScope = scopeStack.Last();
                    if (!globalScope.ContainsKey(methodName))
                    {
                        globalScope[methodName] = new List<(int, int)>();
                    }
                    globalScope[methodName].Add(span);
                    
                    // Check if cursor is within this method declaration
                    if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
                    {
                        variableToRename = methodName;
                        isPrivateMethod = true;
                        targetScope = globalScope;
                    }
                }
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
            
            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                var methodName = genericIdNode.GetText();
                
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
                
                if (privateMethods.ContainsKey(methodName))
                {
                    // Store just the span of the method name, not the entire method
                    var span = (genericIdNode.Start.StartIndex, genericIdNode.Stop.StopIndex);
                    
                    // Add to method implementations
                    if (!methodImplementations.ContainsKey(methodName))
                    {
                        methodImplementations[methodName] = new List<(int, int)>();
                    }
                    methodImplementations[methodName].Add(span);
                    
                    // Add to global scope for renaming
                    var globalScope = scopeStack.Last();
                    if (!globalScope.ContainsKey(methodName))
                    {
                        globalScope[methodName] = new List<(int, int)>();
                    }
                    globalScope[methodName].Add(span);
                    
                    // Check if cursor is within this method implementation
                    if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
                    {
                        variableToRename = methodName;
                        isPrivateMethod = true;
                        targetScope = globalScope;
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
        
        // Track method calls with %THIS
        public override void EnterDotAccessExpr(DotAccessExprContext context)
        {
            base.EnterDotAccessExpr(context);
            
            // Check if the expression is %THIS
            var expr = context.expression();
            if (expr != null && expr.GetText().Equals("%THIS", StringComparison.OrdinalIgnoreCase))
            {
                // Get the method name from the dot access
                var dotAccesses = context.dotAccess();
                if (dotAccesses != null && dotAccesses.Length > 0)
                {
                    var firstDotAccess = dotAccesses[0];
                    var methodName = firstDotAccess.genericID()?.GetText();
                    
                    if (methodName != null && privateMethods.ContainsKey(methodName))
                    {
                        // This is a call to a private method
                        // Get just the span of the method name, not the entire expression
                        var genericIdNode = firstDotAccess.genericID();
                        if (genericIdNode != null)
                        {
                            var span = (genericIdNode.Start.StartIndex, genericIdNode.Stop.StopIndex);
                            
                            // Track this method call
                            if (!methodCalls.ContainsKey(methodName))
                            {
                                methodCalls[methodName] = new List<(int, int)>();
                            }
                            methodCalls[methodName].Add(span);
                            
                            // Add to global scope for renaming
                            var globalScope = scopeStack.Last();
                            if (!globalScope.ContainsKey(methodName))
                            {
                                globalScope[methodName] = new List<(int, int)>();
                            }
                            globalScope[methodName].Add(span);
                            
                            // Check if cursor is within this method call
                            if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
                            {
                                variableToRename = methodName;
                                isPrivateMethod = true;
                                targetScope = globalScope;
                            }
                        }
                    }
                }
            }
        }
        
        // Track simple function calls that might be private methods
        public override void EnterFunctionCallExpr(FunctionCallExprContext context)
        {
            base.EnterFunctionCallExpr(context);
            
            var simpleFunctionCall = context.simpleFunctionCall();
            if (simpleFunctionCall != null)
            {
                var genericIdNode = simpleFunctionCall.genericID();
                if (genericIdNode != null)
                {
                    var methodName = genericIdNode.GetText();
                    
                    if (privateMethods.ContainsKey(methodName))
                    {
                        // This is a call to a private method without %THIS
                        // Get just the span of the method name, not the entire expression
                        var span = (genericIdNode.Start.StartIndex, genericIdNode.Stop.StopIndex);
                        
                        // Track this method call
                        if (!methodCalls.ContainsKey(methodName))
                        {
                            methodCalls[methodName] = new List<(int, int)>();
                        }
                        methodCalls[methodName].Add(span);
                        
                        // Add to global scope for renaming
                        var globalScope = scopeStack.Last();
                        if (!globalScope.ContainsKey(methodName))
                        {
                            globalScope[methodName] = new List<(int, int)>();
                        }
                        globalScope[methodName].Add(span);
                        
                        // Check if cursor is within this method call
                        if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
                        {
                            variableToRename = methodName;
                            isPrivateMethod = true;
                            targetScope = globalScope;
                        }
                    }
                }
            }
        }

        // Helper method to add an occurrence to the appropriate scope
        private void AddOccurrence(string varName, (int, int) span, bool mustExist = false)
        {
            // For instance variables, constants, or methods, check the global scope first
            if (!mustExist && (isInstanceVariable || isConstant || isPrivateMethod) && variableToRename == varName)
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
            // With deferred dialog, we don't generate changes here
            // The changes will be generated after the dialog is shown
            // We just validate that we found a variable to rename
            if (variableToRename == null || targetScope == null)
            {
                // No variable found at cursor position
                SetFailure("No variable or method found at cursor position. Please place cursor on a variable or method name.");
            }
        }
        
        // Generate the refactoring changes
        public void GenerateChanges()
        {
            if (variableToRename == null || targetScope == null || newVariableName == null)
            {
                // No variable found at cursor position
                SetFailure("No variable or method found at cursor position. Please place cursor on a variable or method name.");
                return;
            }

            targetScope.TryGetValue(variableToRename, out var allOccurrences);

            /* If newVariableName is already in the scope, report failure to the user, cannot rename to existing variable name */
            if (targetScope.ContainsKey(newVariableName))
            {
                SetFailure($"'{newVariableName}' already exists in the current scope. Please choose a different name.");
                return;
            }

            if (allOccurrences == null || allOccurrences.Count == 0)
            {
                // No occurrences found
                string errorVarType = isInstanceVariable ? "private instance variable" : 
                                      isParameter ? "parameter" : 
                                      isConstant ? "private constant" :
                                      isPrivateMethod ? "private method" : "local variable";
                SetFailure($"Target '{variableToRename}' is not a {errorVarType}. Only {errorVarType}s can be renamed.");
                return;
            }

            // Sort occurrences in reverse order to avoid position shifting
            allOccurrences.Sort((a, b) => b.Item1.CompareTo(a.Item1));

            string varTypeDescription = isInstanceVariable ? "private instance variable" : 
                                  isParameter ? "parameter" : 
                                  isConstant ? "private constant" :
                                  isPrivateMethod ? "private method" : "local variable";
            
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
