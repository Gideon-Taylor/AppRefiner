using AppRefiner.Services;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using System.Drawing;
using System.Reflection.Metadata;
using System.Text;
using System.Windows.Forms;

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

    /// <summary>
    /// Collects all local variable declarations in the current scope and groups them at the top
    /// </summary>
    public class LocalVariableCollectorRefactor : BaseRefactor
    {
        public new static string RefactorName => "Collect Local Variables";
        public new static string RefactorDescription => "Collects all local variable declarations in the current scope and groups them at the top";

        public override bool RequiresUserInputDialog => true;
        public override bool DeferDialogUntilAfterVisitor => false;
        public new static bool RegisterKeyboardShortcut => false;


        private readonly Dictionary<string, int> scopeInsertionPoints = new();

        private VariableCollectionMode selectedMode = VariableCollectionMode.CollapseByType;
        private ScopeProcessingMode selectedScopeMode = ScopeProcessingMode.CurrentScopeOnly;

        public LocalVariableCollectorRefactor(AppRefiner.ScintillaEditor editor) : base(editor)
        {
        }

        public override bool ShowRefactorDialog()
        {
            using var dialog = new VariableCollectionModeDialog();
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
            private Button btnOk = new();
            private Button btnCancel = new();
            private GroupBox modeGroupBox = new();
            private RadioButton rbCollapseByType = new();
            private RadioButton rbExplicitDeclarations = new();
            private GroupBox scopeGroupBox = new();
            private RadioButton rbCurrentScope = new();
            private RadioButton rbAllScopes = new();
            private Panel headerPanel = new();
            private Label headerLabel = new();

            public VariableCollectionMode SelectedMode { get; private set; } = VariableCollectionMode.CollapseByType;
            public ScopeProcessingMode SelectedScopeMode { get; private set; } = ScopeProcessingMode.CurrentScopeOnly;

            public VariableCollectionModeDialog()
            {
                InitializeComponent();
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
                this.headerLabel.Text = "Collect Local Variables";
                this.headerLabel.ForeColor = Color.White;
                this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                this.headerLabel.Dock = DockStyle.Fill;
                this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;
                
                // modeGroupBox
                this.modeGroupBox.Text = "Collection Mode";
                this.modeGroupBox.Location = new Point(12, 40);
                this.modeGroupBox.Size = new Size(300, 80);
                this.modeGroupBox.Controls.Add(this.rbCollapseByType);
                this.modeGroupBox.Controls.Add(this.rbExplicitDeclarations);
                
                // rbCollapseByType
                this.rbCollapseByType.Text = "Collapse by type (combine same types)";
                this.rbCollapseByType.Location = new Point(10, 20);
                this.rbCollapseByType.Size = new Size(280, 20);
                this.rbCollapseByType.Checked = true;
                
                // rbExplicitDeclarations
                this.rbExplicitDeclarations.Text = "Keep explicit declarations separate";
                this.rbExplicitDeclarations.Location = new Point(10, 45);
                this.rbExplicitDeclarations.Size = new Size(280, 20);
                
                // scopeGroupBox
                this.scopeGroupBox.Text = "Scope Processing";
                this.scopeGroupBox.Location = new Point(12, 130);
                this.scopeGroupBox.Size = new Size(300, 80);
                this.scopeGroupBox.Controls.Add(this.rbCurrentScope);
                this.scopeGroupBox.Controls.Add(this.rbAllScopes);
                
                // rbCurrentScope
                this.rbCurrentScope.Text = "Current scope only";
                this.rbCurrentScope.Location = new Point(10, 20);
                this.rbCurrentScope.Size = new Size(280, 20);
                this.rbCurrentScope.Checked = true;
                
                // rbAllScopes
                this.rbAllScopes.Text = "All scopes";
                this.rbAllScopes.Location = new Point(10, 45);
                this.rbAllScopes.Size = new Size(280, 20);
                
                // btnOk
                this.btnOk.Text = "&OK";
                this.btnOk.Location = new Point(160, 220);
                this.btnOk.Size = new Size(75, 28);
                this.btnOk.DialogResult = DialogResult.OK;
                this.btnOk.Click += BtnOk_Click;
                
                // btnCancel
                this.btnCancel.Text = "&Cancel";
                this.btnCancel.Location = new Point(240, 220);
                this.btnCancel.Size = new Size(75, 28);
                this.btnCancel.DialogResult = DialogResult.Cancel;
                
                // Form
                this.Text = "Collect Local Variables";
                this.Size = new Size(340, 280);
                this.StartPosition = FormStartPosition.CenterParent;
                this.FormBorderStyle = FormBorderStyle.None;
                this.Controls.AddRange(new Control[] { this.headerPanel, this.modeGroupBox, this.scopeGroupBox, this.btnOk, this.btnCancel });
                
                this.ResumeLayout(false);
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                SelectedMode = rbCollapseByType.Checked ? VariableCollectionMode.CollapseByType : VariableCollectionMode.ExplicitDeclarations;
                SelectedScopeMode = rbCurrentScope.Checked ? ScopeProcessingMode.CurrentScopeOnly : ScopeProcessingMode.AllScopes;
            }
        }


        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);

            // Process based on selected mode
            //if (selectedScopeMode == ScopeProcessingMode.CurrentScopeOnly)

        }

        /* In practice AstNode here is a FunctionNode/ProgramNode/MethodNode/PropertyNode */
        private void CollectVariables(ScopeContext scope, AstNode node)
        {
            var localVariables = GetAccessibleVariables(scope).Where(v => v.Kind == VariableKind.Local);

            var groupedByType = localVariables.GroupBy(v => v.Type).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderBy(g => g.Name));

            var newDeclarations = new StringBuilder();

            foreach (var group in groupedByType)
            {
                if (!group.Any()) { continue; }

                if (selectedMode == VariableCollectionMode.CollapseByType)
                {
                    var names = string.Join(", ", group.Select(g => g.Name));
                    newDeclarations.AppendLine($"   Local {group.First().Type} {names};");
                }
                else
                {
                    /* make a new declaration for each one */
                    foreach (var variable in group)
                    {
                        newDeclarations.AppendLine($"   Local {variable.Type} {variable.Name};");
                    }
                }
            }

            var newString = newDeclarations.ToString();

            if (node is FunctionNode functionNode)
            {
                InsertAndCleanBody(functionNode.Body!, newString, localVariables);
            }

            if (node is MethodImplNode methodImplNode)
            {
                InsertAndCleanBody(methodImplNode.Body!, newString, localVariables);
            }

            if (node is PropertyNode propertyNode)
            {
                if (scope.Type == EnhancedScopeType.PropertyGetter)
                {
                    if (propertyNode.HasGet && propertyNode.GetterBody is not null)
                    {
                        InsertAndCleanBody(propertyNode.GetterBody, newString, localVariables);
                    }
                }

                if (scope.Type == EnhancedScopeType.PropertySetter)
                {
                    if (propertyNode.HasGet && propertyNode.GetterBody is not null)
                    {
                        InsertAndCleanBody(propertyNode.GetterBody, newString, localVariables);
                    }
                }

            }

            if (node is ProgramNode programNode && programNode.MainBlock is not null)
            {
                InsertAndCleanBody(programNode.MainBlock!, newString, localVariables);
            }

        }

        private void InsertAndCleanBody(BlockNode body, string newDeclarations, IEnumerable<VariableInfo> localVariables)
        {
            var insertLocation = body.SourceSpan.Start.ByteIndex;
            StatementNode? firstStatement = body.Statements.FirstOrDefault();
            if (firstStatement is not null)
            {
                insertLocation = firstStatement.SourceSpan.Start.ByteIndex - 3; /* minus 3 for the padding at the start of this statement */
            }

            InsertText(insertLocation, newDeclarations, "Insert collected variable names");

            /* Now we have to go cleanup all local variable declarations to be either empty or assignments */
            HashSet<AstNode> processedNodes = [];
            foreach(var variableDecl in localVariables.Select(v => v.DeclarationNode))
            {
                if (variableDecl is LocalVariableDeclarationNode localVariableDecl)
                {
                    var lineNum = localVariableDecl.SourceSpan.Start.Line;
                    if (processedNodes.Contains(localVariableDecl)) { continue; }

                    var lineStartIndex = ScintillaManager.GetLineStartIndex(Editor, lineNum);
                    var lineEndIndex = lineStartIndex + ScintillaManager.GetLineLength(Editor, lineNum);

                    if (localVariableDecl == firstStatement)
                    {
                        /* keep the padding and only delete the actual statement all the way to the end of the line */
                        /* This keeps the DeleteText and the InsertText from earlier with the same start index */
                        var startIndex = localVariableDecl.SourceSpan.Start.ByteIndex - 3; /* minus 3 for the padding at the start of this statement */
                        DeleteText(startIndex, lineEndIndex, "Remove local variable declaration");
                    }
                    else
                    {

                        /* We want to delete the entire line */
                        
                        DeleteText(lineStartIndex, lineEndIndex, "Remove local variable declaration");
                    }

                    processedNodes.Add(localVariableDecl);

                } else if (variableDecl is LocalVariableDeclarationWithAssignmentNode localVariableWithAssignmentNode)
                {
                    var localStart = localVariableWithAssignmentNode.SourceSpan.Start.ByteIndex;
                    var nameEnd = localVariableWithAssignmentNode.VariableNameInfo.Token!.SourceSpan.End.ByteIndex;

                    EditText(localStart, nameEnd, localVariableWithAssignmentNode.VariableName,"Replace declaration assignment with assignment.");
                }
            }

        }


        protected override void OnExitFunctionScope(ScopeContext scope, FunctionNode node, Dictionary<string, object> customData)
        {
            if (selectedScopeMode == ScopeProcessingMode.AllScopes ||
                (selectedScopeMode == ScopeProcessingMode.CurrentScopeOnly && node.SourceSpan.ContainsPosition(CurrentPosition)))
            {
                CollectVariables(scope, node);
            }
            base.OnExitFunctionScope(scope, node, customData);
        }

        protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, object> customData)
        {
            if (node.MainBlock is not null)
            {
                if (selectedScopeMode == ScopeProcessingMode.AllScopes ||
                    (selectedScopeMode == ScopeProcessingMode.CurrentScopeOnly && node.MainBlock.SourceSpan.ContainsPosition(CurrentPosition)))
                {
                    CollectVariables(scope, node);
                }
            }
            base.OnExitGlobalScope(scope, node, customData);
        }

        protected override void OnExitMethodScope(ScopeContext scope, MethodNode node, Dictionary<string, object> customData)
        {
            if (node.Implementation is not null)
            {
                if (selectedScopeMode == ScopeProcessingMode.AllScopes ||
                    (selectedScopeMode == ScopeProcessingMode.CurrentScopeOnly && node.Implementation.SourceSpan.ContainsPosition(CurrentPosition)))
                {
                    CollectVariables(scope, node.Implementation);
                }
            }
            base.OnExitMethodScope(scope, node, customData);
        }

        protected override void OnExitPropertyGetterScope(ScopeContext scope, PropertyNode node, Dictionary<string, object> customData)
        {
            if (node.GetterImplementation is not null)
            {
                if (selectedScopeMode == ScopeProcessingMode.AllScopes ||
                    (selectedScopeMode == ScopeProcessingMode.CurrentScopeOnly && node.GetterImplementation.SourceSpan.ContainsPosition(CurrentPosition)))
                {
                    CollectVariables(scope, node.GetterImplementation);
                }
            }
            base.OnExitPropertyGetterScope(scope, node, customData);
        }

        protected override void OnExitPropertySetterScope(ScopeContext scope, PropertyNode node, Dictionary<string, object> customData)
        {
            if (node.SetterImplementation is not null)
            {
                if (selectedScopeMode == ScopeProcessingMode.AllScopes ||
                    (selectedScopeMode == ScopeProcessingMode.CurrentScopeOnly && node.SetterImplementation.SourceSpan.ContainsPosition(CurrentPosition)))
                {
                    CollectVariables(scope, node.SetterImplementation);
                }
            }
            base.OnExitPropertySetterScope(scope, node, customData);
        }

    }
}