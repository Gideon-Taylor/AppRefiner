using Antlr4.Runtime.Misc;
using AppRefiner.Dialogs;
using AppRefiner.Linters.Models;
using AppRefiner.PeopleCode;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Refactors
{
    public enum VariableCollectionMode
    {
        CollapseByType,
        ExplicitDeclarations
    }

    public enum ScopeProcessingMode
    {
        CurrentScopeOnly,
        AllScopes
    }

    public class LocalVariableCollectorRefactor(ScintillaEditor editor) : ScopedRefactor<List<VariableDeclarationInfo>>(editor)
    {
        public new static string RefactorName => "Collect Local Variables";

        public new static string RefactorDescription => "Collects all local variable declarations in the current scope and groups them at the top";

        public override bool RequiresUserInputDialog => true;

        public override bool DeferDialogUntilAfterVisitor => false;

        public new static bool RegisterKeyboardShortcut => false;

        private readonly List<VariableDeclarationInfo> allVariableDeclarations = new();
        private readonly Dictionary<string, List<VariableDeclarationInfo>> scopeVariables = new();
        private readonly Dictionary<string, int> scopeInsertionPoints = new();
        private VariableCollectionMode selectedMode = VariableCollectionMode.CollapseByType;
        private ScopeProcessingMode selectedScopeMode = ScopeProcessingMode.CurrentScopeOnly;
        private int scopeStartPosition = -1;
        private string? currentScopeName;

        public override bool ShowRefactorDialog()
        {
            using var dialog = new VariableCollectionModeDialog(GetEditorMainWindowHandle());
            var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
            DialogResult result = dialog.ShowDialog(wrapper);

            if (result == DialogResult.OK)
            {
                selectedMode = dialog.SelectedMode;
                selectedScopeMode = dialog.SelectedScopeMode;
                return true;
            }

            return false;
        }

        private class VariableCollectionModeDialog : Form
        {
            private readonly Panel headerPanel;
            private readonly Label headerLabel;
            private readonly Label messageLabel;
            private readonly GroupBox collectionModeGroupBox;
            private readonly RadioButton collapseRadioButton;
            private readonly RadioButton explicitRadioButton;
            private readonly Label collapseDescriptionLabel;
            private readonly Label explicitDescriptionLabel;
            private readonly GroupBox scopeModeGroupBox;
            private readonly RadioButton currentScopeRadioButton;
            private readonly RadioButton allScopesRadioButton;
            private readonly Label currentScopeDescriptionLabel;
            private readonly Label allScopesDescriptionLabel;
            private readonly FlowLayoutPanel buttonsPanel;
            private readonly IntPtr owner;
            private DialogHelper.ModalDialogMouseHandler? mouseHandler;

            public VariableCollectionMode SelectedMode { get; private set; } = VariableCollectionMode.CollapseByType;
            public ScopeProcessingMode SelectedScopeMode { get; private set; } = ScopeProcessingMode.CurrentScopeOnly;

            public VariableCollectionModeDialog(IntPtr owner)
            {
                this.headerPanel = new Panel();
                this.headerLabel = new Label();
                this.messageLabel = new Label();
                this.collectionModeGroupBox = new GroupBox();
                this.collapseRadioButton = new RadioButton();
                this.explicitRadioButton = new RadioButton();
                this.collapseDescriptionLabel = new Label();
                this.explicitDescriptionLabel = new Label();
                this.scopeModeGroupBox = new GroupBox();
                this.currentScopeRadioButton = new RadioButton();
                this.allScopesRadioButton = new RadioButton();
                this.currentScopeDescriptionLabel = new Label();
                this.allScopesDescriptionLabel = new Label();
                this.buttonsPanel = new FlowLayoutPanel();
                this.owner = owner;

                InitializeComponent();
            }

            private void InitializeComponent()
            {
                this.headerPanel.SuspendLayout();
                this.collectionModeGroupBox.SuspendLayout();
                this.scopeModeGroupBox.SuspendLayout();
                this.buttonsPanel.SuspendLayout();
                this.SuspendLayout();

                // headerPanel
                this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
                this.headerPanel.Dock = DockStyle.Top;
                this.headerPanel.Height = 30;
                this.headerPanel.Controls.Add(this.headerLabel);

                // headerLabel
                this.headerLabel.Text = "AppRefiner - Local Variable Collection Mode";
                this.headerLabel.ForeColor = Color.White;
                this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                this.headerLabel.Dock = DockStyle.Fill;
                this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

                // messageLabel
                this.messageLabel.Text = "Configure local variable collection options:";
                this.messageLabel.Location = new Point(20, 45);
                this.messageLabel.Size = new Size(400, 20);
                this.messageLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

                // collectionModeGroupBox
                this.collectionModeGroupBox.Text = "Variable Grouping Mode";
                this.collectionModeGroupBox.Location = new Point(20, 75);
                this.collectionModeGroupBox.Size = new Size(410, 140);
                this.collectionModeGroupBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

                // collapseRadioButton
                this.collapseRadioButton.Text = "Collapse by Type";
                this.collapseRadioButton.Location = new Point(15, 25);
                this.collapseRadioButton.Size = new Size(150, 20);
                this.collapseRadioButton.Checked = true;
                this.collapseRadioButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);

                // collapseDescriptionLabel
                this.collapseDescriptionLabel.Text = "Groups variables of the same type together:\n  local integer &x, &y, &z;";
                this.collapseDescriptionLabel.Location = new Point(35, 45);
                this.collapseDescriptionLabel.Size = new Size(350, 35);
                this.collapseDescriptionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point);
                this.collapseDescriptionLabel.ForeColor = Color.DarkGray;

                // explicitRadioButton
                this.explicitRadioButton.Text = "Explicit Declarations";
                this.explicitRadioButton.Location = new Point(15, 85);
                this.explicitRadioButton.Size = new Size(150, 20);
                this.explicitRadioButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);

                // explicitDescriptionLabel
                this.explicitDescriptionLabel.Text = "Keeps each variable on separate lines:\n  local integer &x;\n  local integer &y;";
                this.explicitDescriptionLabel.Location = new Point(35, 105);
                this.explicitDescriptionLabel.Size = new Size(350, 30);
                this.explicitDescriptionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point);
                this.explicitDescriptionLabel.ForeColor = Color.DarkGray;

                // Add radio buttons to collection mode group
                this.collectionModeGroupBox.Controls.Add(this.collapseRadioButton);
                this.collectionModeGroupBox.Controls.Add(this.collapseDescriptionLabel);
                this.collectionModeGroupBox.Controls.Add(this.explicitRadioButton);
                this.collectionModeGroupBox.Controls.Add(this.explicitDescriptionLabel);

                // scopeModeGroupBox
                this.scopeModeGroupBox.Text = "Scope Processing Mode";
                this.scopeModeGroupBox.Location = new Point(20, 225);
                this.scopeModeGroupBox.Size = new Size(410, 115);
                this.scopeModeGroupBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

                // currentScopeRadioButton
                this.currentScopeRadioButton.Text = "Current Scope Only";
                this.currentScopeRadioButton.Location = new Point(15, 25);
                this.currentScopeRadioButton.Size = new Size(150, 20);
                this.currentScopeRadioButton.Checked = true;
                this.currentScopeRadioButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);

                // currentScopeDescriptionLabel
                this.currentScopeDescriptionLabel.Text = "Process only the scope containing the cursor";
                this.currentScopeDescriptionLabel.Location = new Point(35, 45);
                this.currentScopeDescriptionLabel.Size = new Size(350, 20);
                this.currentScopeDescriptionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point);
                this.currentScopeDescriptionLabel.ForeColor = Color.DarkGray;

                // allScopesRadioButton
                this.allScopesRadioButton.Text = "All Scopes";
                this.allScopesRadioButton.Location = new Point(15, 70);
                this.allScopesRadioButton.Size = new Size(100, 20);
                this.allScopesRadioButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);

                // allScopesDescriptionLabel
                this.allScopesDescriptionLabel.Text = "Process all methods, getters, setters, and program scope";
                this.allScopesDescriptionLabel.Location = new Point(35, 90);
                this.allScopesDescriptionLabel.Size = new Size(350, 20);
                this.allScopesDescriptionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point);
                this.allScopesDescriptionLabel.ForeColor = Color.DarkGray;

                // Add radio buttons to scope mode group
                this.scopeModeGroupBox.Controls.Add(this.currentScopeRadioButton);
                this.scopeModeGroupBox.Controls.Add(this.currentScopeDescriptionLabel);
                this.scopeModeGroupBox.Controls.Add(this.allScopesRadioButton);
                this.scopeModeGroupBox.Controls.Add(this.allScopesDescriptionLabel);

                // buttonsPanel
                this.buttonsPanel.Dock = DockStyle.Bottom;
                this.buttonsPanel.FlowDirection = FlowDirection.RightToLeft;
                this.buttonsPanel.Height = 50;
                this.buttonsPanel.Padding = new Padding(10);

                // Add buttons
                var cancelButton = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Size = new Size(75, 30)
                };
                cancelButton.Click += (s, e) => this.Close();

                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new Size(75, 30)
                };
                okButton.Click += OkButton_Click;

                this.buttonsPanel.Controls.Add(cancelButton);
                this.buttonsPanel.Controls.Add(okButton);

                // VariableCollectionModeDialog
                this.ClientSize = new Size(450, 395);
                this.Controls.Add(this.messageLabel);
                this.Controls.Add(this.collectionModeGroupBox);
                this.Controls.Add(this.scopeModeGroupBox);
                this.Controls.Add(this.buttonsPanel);
                this.Controls.Add(this.headerPanel);
                this.FormBorderStyle = FormBorderStyle.None;
                this.StartPosition = FormStartPosition.Manual;
                this.Text = "Variable Collection Mode";
                this.ShowInTaskbar = false;
                this.BackColor = Color.FromArgb(240, 240, 245);
                this.AcceptButton = okButton;
                this.CancelButton = cancelButton;

                this.headerPanel.ResumeLayout(false);
                this.collectionModeGroupBox.ResumeLayout(false);
                this.scopeModeGroupBox.ResumeLayout(false);
                this.buttonsPanel.ResumeLayout(false);
                this.ResumeLayout(false);
            }

            private void OkButton_Click(object? sender, EventArgs e)
            {
                SelectedMode = collapseRadioButton.Checked ? VariableCollectionMode.CollapseByType : VariableCollectionMode.ExplicitDeclarations;
                SelectedScopeMode = currentScopeRadioButton.Checked ? ScopeProcessingMode.CurrentScopeOnly : ScopeProcessingMode.AllScopes;
                this.DialogResult = DialogResult.OK;
                this.Close();
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

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                
                // Draw a border around the form
                ControlPaint.DrawBorder(e.Graphics, ClientRectangle, 
                    Color.FromArgb(100, 100, 120), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(100, 100, 120), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(100, 100, 120), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(100, 100, 120), 1, ButtonBorderStyle.Solid);
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);

                // Center on owner window
                if (owner != IntPtr.Zero)
                {
                    WindowHelper.CenterFormOnWindow(this, owner);
                }
                else
                {
                    this.CenterToScreen();
                }

                if (this.Modal && owner != IntPtr.Zero)
                {
                    mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, owner);
                }
            }

            protected override void OnFormClosed(FormClosedEventArgs e)
            {
                base.OnFormClosed(e);
                mouseHandler?.Dispose();
                mouseHandler = null;
            }
        }

        public override void EnterMethod(MethodContext context)
        {
            base.EnterMethod(context);
            ProcessScope(context, "method");
        }

        public override void EnterGetter(GetterContext context)
        {
            base.EnterGetter(context);
            ProcessScope(context, "getter");
        }

        public override void EnterSetter(SetterContext context)
        {
            base.EnterSetter(context);
            ProcessScope(context, "setter");
        }

        public override void EnterProgram(ProgramContext context)
        {
            base.EnterProgram(context);
            
            // For non-app class programs, the entire program is the scope
            if (!IsAppClass(context))
            {
                ProcessScope(context, "program");
            }
        }

        private bool IsAppClass(ProgramContext context)
        {
            return context.appClass() != null;
        }

        private void ProcessScope(Antlr4.Runtime.ParserRuleContext context, string scopeType)
        {
            int contextStart = context.Start.ByteStartIndex();
            int contextEnd = (context.Stop ?? context.Start).ByteStopIndex();
            
            // For "All Scopes" mode, process every scope we encounter
            // For "Current Scope Only" mode, only process if cursor is within this scope
            bool shouldProcess = selectedScopeMode == ScopeProcessingMode.AllScopes || 
                               (CurrentPosition >= contextStart && CurrentPosition <= contextEnd);

            if (shouldProcess)
            {
                // Find the position after the scope declaration to insert variables
                int insertionPoint = FindScopeInsertionPoint(context, scopeType);
                string scopeKey = $"{scopeType}_{contextStart}_{contextEnd}";
                
                // Initialize collections for this scope
                if (!scopeVariables.ContainsKey(scopeKey))
                {
                    scopeVariables[scopeKey] = new List<VariableDeclarationInfo>();
                }
                scopeInsertionPoints[scopeKey] = insertionPoint;
                
                // If this is the current scope (cursor-based), also set legacy fields for compatibility
                if (CurrentPosition >= contextStart && CurrentPosition <= contextEnd)
                {
                    scopeStartPosition = insertionPoint;
                    currentScopeName = scopeType;
                }
            }
        }

        private int FindScopeInsertionPoint(Antlr4.Runtime.ParserRuleContext context, string scopeType)
        {
            switch (scopeType)
            {
                case "method":
                    if (context is MethodContext methodCtx)
                    {
                        return FindMethodInsertionPoint(methodCtx);
                    }
                    break;
                    
                case "getter":
                    if (context is GetterContext getterCtx)
                    {
                        return FindGetterSetterInsertionPoint(getterCtx);
                    }
                    break;
                    
                case "setter":
                    if (context is SetterContext setterCtx)
                    {
                        return FindGetterSetterInsertionPoint(setterCtx);
                    }
                    break;
                    
                case "program":
                    if (context is ProgramContext programCtx)
                    {
                        // For programs, insert after imports and preambles
                        return FindProgramInsertionPoint(programCtx);
                    }
                    break;
            }

            return context.Start.ByteStartIndex();
        }

        private int FindMethodInsertionPoint(MethodContext methodCtx)
        {
            // Start after the method signature
            int insertionPoint = methodCtx.Start.ByteStartIndex();
            
            // If there are method annotations, find the end of the last one
            var annotations = methodCtx.methodAnnotations();
            if (annotations != null && annotations.Stop != null)
            {
                insertionPoint = annotations.Stop.ByteStopIndex() + 1;
            }
            else
            {
                // No annotations, find the first line break after the method signature
                // This handles: method MethodName() followed by a newline
                var methodHeader = methodCtx.genericID();
                if (methodHeader != null)
                {
                    insertionPoint = FindFirstLineBreakAfter(methodHeader.Stop.ByteStopIndex());
                }
            }
            
            return insertionPoint;
        }

        private int FindGetterSetterInsertionPoint(Antlr4.Runtime.ParserRuleContext context)
        {
            // For getters and setters, find the first line break after the signature
            return FindFirstLineBreakAfter(context.Start.ByteStartIndex());
        }

        private int FindFirstLineBreakAfter(int startIndex)
        {
            string? source = Editor?.ContentString;
            if (source == null) return startIndex;

            for (int i = startIndex; i < source.Length; i++)
            {
                if (source[i] == '\n')
                {
                    return i + 1;
                }
            }

            return startIndex;
        }

        private int FindFirstSemicolonAfter(int startIndex)
        {
            string? source = Editor?.ContentString;
            if (source == null) return startIndex;

            for (int i = startIndex; i < source.Length; i++)
            {
                if (source[i] == ';')
                {
                    return i + 1;
                }
            }

            return startIndex;
        }

        private int FindProgramInsertionPoint(ProgramContext context)
        {
            // Look for the end of imports and preambles
            var preambles = context.programPreambles();
            if (preambles != null && preambles.Stop != null)
            {
                return preambles.Stop.ByteStopIndex() + 1;
            }

            var imports = context.importsBlock();
            if (imports != null && imports.Stop != null)
            {
                return imports.Stop.ByteStopIndex() + 1;
            }

            return context.Start.ByteStartIndex();
        }

        public override void EnterLocalVariableDefinition(LocalVariableDefinitionContext context)
        {
            base.EnterLocalVariableDefinition(context);

            var typeContext = context.typeT();
            string typeName = GetTypeFromContext(typeContext);

            foreach (var varNode in context.USER_VARIABLE())
            {
                string varName = varNode.GetText();
                
                var variableInfo = new VariableDeclarationInfo
                {
                    Name = varName,
                    Type = typeName,
                    Context = context,
                    AssignmentExpression = null,
                    StartIndex = context.Start.ByteStartIndex(),
                    StopIndex = context.Stop?.ByteStopIndex() ?? context.Start.ByteStartIndex()
                };

                // Add to the appropriate scope(s)
                AddVariableToRelevantScopes(variableInfo);
            }
        }

        public override void EnterLocalVariableDeclAssignment(LocalVariableDeclAssignmentContext context)
        {
            base.EnterLocalVariableDeclAssignment(context);

            var typeContext = context.typeT();
            string typeName = GetTypeFromContext(typeContext);
            var varNode = context.USER_VARIABLE();
            string varName = varNode.GetText();

            // Extract the assignment expression with original formatting
            var expression = context.expression();
            string assignmentExpr = GetOriginalExpressionText(expression);

            var variableInfo = new VariableDeclarationInfo
            {
                Name = varName,
                Type = typeName,
                Context = context,
                AssignmentExpression = assignmentExpr,
                StartIndex = context.Start.ByteStartIndex(),
                StopIndex = context.Stop?.ByteStopIndex() ?? context.Start.ByteStartIndex()
            };

            // Add to the appropriate scope(s)
            AddVariableToRelevantScopes(variableInfo);
        }

        private void AddVariableToRelevantScopes(VariableDeclarationInfo variableInfo)
        {
            bool addedToRelevantScope = false;

            // For "All Scopes" mode, find which scope this variable belongs to
            if (selectedScopeMode == ScopeProcessingMode.AllScopes)
            {
                foreach (var scopeKey in scopeVariables.Keys.ToList())
                {
                    // Parse scope key to get bounds
                    var parts = scopeKey.Split('_');
                    if (parts.Length >= 3 && 
                        int.TryParse(parts[1], out int scopeStart) && 
                        int.TryParse(parts[2], out int scopeEnd))
                    {
                        // Check if variable is within this scope
                        if (variableInfo.StartIndex >= scopeStart && variableInfo.StartIndex <= scopeEnd)
                        {
                            scopeVariables[scopeKey].Add(variableInfo);
                            addedToRelevantScope = true;
                        }
                    }
                }
                
                // Add to legacy collection for "All Scopes" mode
                if (addedToRelevantScope)
                {
                    allVariableDeclarations.Add(variableInfo);
                }
            }
            // For "Current Scope Only" mode, only add if in target scope
            else if (scopeStartPosition != -1)
            {
                // Check if this variable is within the current target scope
                bool isInCurrentScope = false;
                
                // Find the matching scope key for the current scope
                foreach (var kvp in scopeInsertionPoints)
                {
                    if (kvp.Value == scopeStartPosition)
                    {
                        // Parse scope key to get bounds
                        var parts = kvp.Key.Split('_');
                        if (parts.Length >= 3 && 
                            int.TryParse(parts[1], out int scopeStart) && 
                            int.TryParse(parts[2], out int scopeEnd))
                        {
                            // Check if variable is within this scope
                            if (variableInfo.StartIndex >= scopeStart && variableInfo.StartIndex <= scopeEnd)
                            {
                                scopeVariables[kvp.Key].Add(variableInfo);
                                allVariableDeclarations.Add(variableInfo);
                                isInCurrentScope = true;
                            }
                        }
                        break;
                    }
                }
            }
        }

        public override void ExitProgram([NotNull] ProgramContext context)
        {
            if (selectedScopeMode == ScopeProcessingMode.AllScopes)
            {
                // For "All Scopes" mode, check if we found any variables in any scope
                bool hasVariables = scopeVariables.Values.Any(list => list.Count > 0);
                if (!hasVariables)
                {
                    SetFailure("No local variable declarations found in any scope.");
                    return;
                }
            }
            else
            {
                // For "Current Scope Only" mode, use legacy validation
                if (allVariableDeclarations.Count == 0)
                {
                    SetFailure("No local variable declarations found in the current scope.");
                    return;
                }

                if (scopeStartPosition == -1)
                {
                    SetFailure("Could not determine scope for variable collection. Please place cursor within a method, getter, setter, or program.");
                    return;
                }
            }

            // Generate the refactoring changes now that AST processing is complete
            GenerateChanges();
        }

        private void GenerateChanges()
        {
            if (selectedScopeMode == ScopeProcessingMode.AllScopes)
            {
                GenerateChangesForAllScopes();
            }
            else
            {
                GenerateChangesForCurrentScope();
            }
        }

        private void GenerateChangesForAllScopes()
        {
            if (scopeVariables.Count == 0)
            {
                SetFailure("No local variable declarations found to collect.");
                return;
            }

            // Process each scope separately
            foreach (var scopeEntry in scopeVariables)
            {
                string scopeKey = scopeEntry.Key;
                var variables = scopeEntry.Value;
                
                if (variables.Count == 0) continue;

                // Get insertion point for this scope
                int insertionPoint = scopeInsertionPoints[scopeKey];

                // Group variables by type
                var groupedVariables = variables
                    .GroupBy(v => v.Type)
                    .OrderBy(g => g.Key)
                    .ToList();

                // Generate the new declarations at the top of scope
                var declarationText = GenerateDeclarationText(groupedVariables);

                // Insert the new declarations
                InsertText(insertionPoint, declarationText, $"Insert collected variable declarations for scope {scopeKey}");

                // Remove original declarations and replace with assignments where needed
                ProcessVariableReplacements(variables);
            }
        }

        private void GenerateChangesForCurrentScope()
        {
            if (allVariableDeclarations.Count == 0)
            {
                SetFailure("No local variable declarations found to collect.");
                return;
            }

            // Group variables by type
            var groupedVariables = allVariableDeclarations
                .GroupBy(v => v.Type)
                .OrderBy(g => g.Key)
                .ToList();

            // Generate the new declarations at the top of scope
            var declarationText = GenerateDeclarationText(groupedVariables);

            // Insert the new declarations
            InsertText(scopeStartPosition, declarationText, "Insert collected variable declarations");

            // Remove original declarations and replace with assignments where needed
            ProcessVariableReplacements(allVariableDeclarations);
        }

        private void ProcessVariableReplacements(List<VariableDeclarationInfo> variables)
        {
            // Group variables by their declaration context to handle multi-variable declarations
            var variablesByContext = variables.GroupBy(v => v.Context).ToList();
            
            // Sort contexts by position in reverse order to avoid index shifting
            var sortedContexts = variablesByContext
                .OrderByDescending(g => g.Key.Start.ByteStartIndex())
                .ToList();

            foreach (var contextGroup in sortedContexts)
            {
                var contextVariables = contextGroup.ToList();
                
                // Check if any variables in this context have assignments
                var variablesWithAssignments = contextVariables.Where(v => v.AssignmentExpression != null).ToList();
                var variablesWithoutAssignments = contextVariables.Where(v => v.AssignmentExpression == null).ToList();
                
                if (variablesWithAssignments.Any())
                {
                    // Handle each assignment separately
                    foreach (var variable in variablesWithAssignments)
                    {
                        string assignmentText = $"{variable.Name} = {variable.AssignmentExpression}";
                        ReplaceText(
                            variable.StartIndex,
                            variable.StopIndex,
                            assignmentText,
                            $"Replace declaration of {variable.Name} with assignment"
                        );
                    }
                }
                
                if (variablesWithoutAssignments.Any())
                {
                    // For declaration-only variables, remove the entire context (line) once
                    var firstVariable = variablesWithoutAssignments.First();
                    string? source = Editor?.ContentString;
                    
                    if (source != null)
                    {
                        var lineRange = FindEntireLineRange(source, firstVariable.Context.Start.ByteStartIndex(), 
                                                          firstVariable.Context.Stop?.ByteStopIndex() ?? firstVariable.Context.Start.ByteStartIndex());
                        DeleteText(
                            lineRange.Start,
                            lineRange.End,
                            $"Remove entire line containing declarations: {string.Join(", ", variablesWithoutAssignments.Select(v => v.Name))}"
                        );
                    }
                    else
                    {
                        // Fallback: remove the entire context
                        var context = firstVariable.Context;
                        int endIndex = context.Stop?.ByteStopIndex() ?? context.Start.ByteStartIndex();
                        
                        DeleteText(
                            context.Start.ByteStartIndex(),
                            endIndex,
                            $"Remove original declarations: {string.Join(", ", variablesWithoutAssignments.Select(v => v.Name))}"
                        );
                    }
                }
            }
        }

        private (int Start, int End) FindEntireLineRange(string source, int variableStart, int variableEnd)
        {
            // Find the start of the line (including any leading whitespace)
            int lineStart = variableStart;
            while (lineStart > 0 && source[lineStart - 1] != '\n')
            {
                lineStart--;
            }

            // Find the end of the line (including the trailing newline if present)
            int lineEnd = variableEnd;
            
            // First, include any semicolon if present
            if (lineEnd + 1 < source.Length && source[lineEnd + 1] == ';')
            {
                lineEnd++;
            }
            
            // Then find the end of the line (newline or end of file)
            while (lineEnd < source.Length - 1 && source[lineEnd + 1] != '\n')
            {
                lineEnd++;
            }
            
            // Include the newline character if present
            if (lineEnd < source.Length - 1 && source[lineEnd + 1] == '\n')
            {
                lineEnd++;
            }

            return (lineStart, lineEnd);
        }

        private string GetOriginalExpressionText(Antlr4.Runtime.Tree.IParseTree? expression)
        {
            if (expression == null) return "";

            string? source = Editor?.ContentString;
            if (source == null) return expression.GetText();

            // Get the start and stop positions from the expression
            if (expression is Antlr4.Runtime.ParserRuleContext ruleContext &&
                ruleContext.Start != null && ruleContext.Stop != null)
            {
                int startIndex = ruleContext.Start.ByteStartIndex();
                int stopIndex = ruleContext.Stop.ByteStopIndex();
                
                if (startIndex >= 0 && stopIndex >= startIndex && stopIndex < source.Length)
                {
                    return source.Substring(startIndex, stopIndex - startIndex + 1);
                }
            }

            // Fallback to GetText() if we can't extract from source
            return expression.GetText();
        }

        private string GenerateDeclarationText(List<IGrouping<string, VariableDeclarationInfo>> groupedVariables)
        {
            var sb = new StringBuilder();
            sb.AppendLine();

            if (selectedMode == VariableCollectionMode.CollapseByType)
            {
                // Collapse variables of the same type
                foreach (var group in groupedVariables)
                {
                    var variableNames = group.Select(v => v.Name).ToList();
                    sb.AppendLine($"   Local {group.Key} {string.Join(", ", variableNames)};");
                }
            }
            else
            {
                // Explicit declarations - each variable on its own line
                foreach (var group in groupedVariables)
                {
                    foreach (var variable in group)
                    {
                        sb.AppendLine($"   Local {variable.Type} {variable.Name};");
                    }
                }
            }

            return sb.ToString();
        }
    }

    public class VariableDeclarationInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Antlr4.Runtime.ParserRuleContext Context { get; set; } = null!;
        public string? AssignmentExpression { get; set; }
        public int StartIndex { get; set; }
        public int StopIndex { get; set; }
    }
}