using PeopleCodeParser.SelfHosted.Nodes;
using System.Text;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that sorts method implementations to match the order defined in the class declaration
    /// </summary>
    public class SortMethods : BaseRefactor
    {
        /// <summary>
        /// Gets the display name of this refactoring operation
        /// </summary>
        public new static string RefactorName => "Sort Methods";

        /// <summary>
        /// Gets the description of this refactoring operation
        /// </summary>
        public new static string RefactorDescription => "Reorders method implementations to match the order defined in the class declaration";

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
        public new static Keys ShortcutKey => Keys.M;

        /// <summary>
        /// Indicates that this refactor requires a user input dialog
        /// </summary>
        public override bool RequiresUserInputDialog => true;

        /// <summary>
        /// Indicates that this refactor should defer showing the dialog until after the visitor has run
        /// </summary>
        public override bool DeferDialogUntilAfterVisitor => true;

        // Track the app class node for analysis
        private AppClassNode? appClassNode;

        // Track method declaration order 
        private readonly List<MethodNode> methodDeclarations = new();

        // Track method implementations that need to be sorted
        private readonly List<MethodImplNode> methodImplementations = new();

        // Flag to indicate if implementations are already in the correct order
        private bool implementationsInOrder = true;

        public SortMethods(AppRefiner.ScintillaEditor editor) : base(editor)
        {
        }

        /// <summary>
        /// Helper class to track method implementations with their original text
        /// </summary>
        private class MethodImplementationInfo
        {
            public MethodImplNode Node { get; }
            public string OriginalText { get; }
            public int OriginalIndex { get; }

            public MethodImplementationInfo(MethodImplNode node, string originalText, int originalIndex)
            {
                Node = node;
                OriginalText = originalText;
                OriginalIndex = originalIndex;
            }
        }

        /// <summary>
        /// Dialog form for confirming method sorting
        /// </summary>
        private class SortMethodsDialog : Form
        {
            private Button btnOk = new();
            private Button btnCancel = new();
            private Label lblPrompt = new();
            private Panel headerPanel = new();
            private Label headerLabel = new();
            private Label lblMethodCount = new();

            public SortMethodsDialog(int methodCount)
            {
                InitializeComponent(methodCount);
            }

            private void InitializeComponent(int methodCount)
            {
                this.SuspendLayout();

                // headerPanel
                this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
                this.headerPanel.Dock = DockStyle.Top;
                this.headerPanel.Height = 30;
                this.headerPanel.Controls.Add(this.headerLabel);

                // headerLabel
                this.headerLabel.Text = "Sort Methods";
                this.headerLabel.ForeColor = Color.White;
                this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                this.headerLabel.Dock = DockStyle.Fill;
                this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

                // lblPrompt
                this.lblPrompt.AutoSize = false;
                this.lblPrompt.Location = new System.Drawing.Point(12, 40);
                this.lblPrompt.Name = "lblPrompt";
                this.lblPrompt.Size = new System.Drawing.Size(336, 30);
                this.lblPrompt.TabIndex = 0;
                this.lblPrompt.Text = "This will reorder method implementations to match the order\ndefined in the class declaration.";

                // lblMethodCount
                this.lblMethodCount.AutoSize = false;
                this.lblMethodCount.Location = new System.Drawing.Point(12, 80);
                this.lblMethodCount.Name = "lblMethodCount";
                this.lblMethodCount.Size = new System.Drawing.Size(336, 15);
                this.lblMethodCount.TabIndex = 1;

                string methodText = methodCount == 1 ? "method" : "methods";
                this.lblMethodCount.Text = $"Found {methodCount} {methodText} to sort.";

                // btnOk
                this.btnOk.DialogResult = DialogResult.OK;
                this.btnOk.Location = new System.Drawing.Point(192, 110);
                this.btnOk.Name = "btnOk";
                this.btnOk.Size = new System.Drawing.Size(75, 28);
                this.btnOk.TabIndex = 2;
                this.btnOk.Text = "&Sort";
                this.btnOk.UseVisualStyleBackColor = true;

                // btnCancel
                this.btnCancel.DialogResult = DialogResult.Cancel;
                this.btnCancel.Location = new System.Drawing.Point(273, 110);
                this.btnCancel.Name = "btnCancel";
                this.btnCancel.Size = new System.Drawing.Size(75, 28);
                this.btnCancel.TabIndex = 3;
                this.btnCancel.Text = "&Cancel";
                this.btnCancel.UseVisualStyleBackColor = true;

                // SortMethodsDialog
                this.AcceptButton = this.btnOk;
                this.CancelButton = this.btnCancel;
                this.ClientSize = new System.Drawing.Size(360, 150);
                this.Controls.Add(this.btnCancel);
                this.Controls.Add(this.btnOk);
                this.Controls.Add(this.lblMethodCount);
                this.Controls.Add(this.lblPrompt);
                this.Controls.Add(this.headerPanel);
                this.FormBorderStyle = FormBorderStyle.None;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.Name = "SortMethodsDialog";
                this.StartPosition = FormStartPosition.CenterParent;
                this.Text = "Sort Methods";
                this.ShowInTaskbar = false;
                this.ResumeLayout(false);
                this.PerformLayout();
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
        /// Shows the dialog to confirm sorting methods
        /// </summary>
        public override bool ShowRefactorDialog()
        {
            if (appClassNode == null)
            {
                SetFailure("This refactoring only works on class-style programs.");
                return false;
            }

            if (methodDeclarations.Count == 0)
            {
                SetFailure("No method declarations found in the class header.");
                return false;
            }

            if (methodImplementations.Count == 0)
            {
                SetFailure("No method implementations found in the class body.");
                return false;
            }

            if (implementationsInOrder)
            {
                SetFailure("Method implementations are already in the correct order.");
                return false;
            }

            using var dialog = new SortMethodsDialog(methodImplementations.Count);

            // Show dialog with the specified owner
            var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
            DialogResult result = dialog.ShowDialog(wrapper);

            if (result == DialogResult.OK)
            {
                // Since we're using deferred dialog, generate changes now that we have user input
                ApplyChanges();
                return true;
            }

            return false;
        }

        public override void VisitAppClass(AppClassNode node)
        {
            appClassNode = node;

            // Collect method declarations in order
            methodDeclarations.AddRange(node.Methods);

            // Collect method implementations in order (only those that have implementations)
            foreach (var method in node.Methods)
            {
                if (method.Implementation != null && method.Implementation.SourceSpan.IsValid)
                {
                    methodImplementations.Add(method.Implementation);
                }
            }

            // Check if implementations are in correct order
            CheckImplementationOrder();

            base.VisitAppClass(node);
        }

        /// <summary>
        /// Checks if the implementations are already in the correct order
        /// </summary>
        private void CheckImplementationOrder()
        {
            // Check if implementations are in the same order as declarations
            var declarationOrder = methodDeclarations
                .Where(m => m.Implementation != null)
                .Select(m => m.Name.ToLowerInvariant())
                .ToList();

            var implementationOrder = methodImplementations
                .Select(impl => impl.Name.ToLowerInvariant())
                .ToList();

            // Compare the orders - they should match exactly
            implementationsInOrder = declarationOrder.SequenceEqual(implementationOrder);
        }

        /// <summary>
        /// Applies the method sorting changes
        /// </summary>
        private void ApplyChanges()
        {
            if (methodImplementations.Count == 0) return;

            // Create a list to track implementation info with original text
            var implementationInfos = new List<MethodImplementationInfo>();

            // Extract original text for each implementation
            for (int i = 0; i < methodImplementations.Count; i++)
            {
                var impl = methodImplementations[i];
                var originalText = ExtractOriginalText(impl);
                implementationInfos.Add(new MethodImplementationInfo(impl, originalText, i));
            }

            // Sort implementations by declaration order
            var sortedImplementations = new List<MethodImplementationInfo>();
            foreach (var declaration in methodDeclarations.Where(m => m.Implementation != null))
            {
                var matchingImpl = implementationInfos.FirstOrDefault(info =>
                    info.Node.Name.Equals(declaration.Name, StringComparison.OrdinalIgnoreCase));
                if (matchingImpl != null)
                {
                    sortedImplementations.Add(matchingImpl);
                }
            }

            // Apply the changes by replacing each implementation in reverse order (to preserve positions)
            for (int i = implementationInfos.Count - 1; i >= 0; i--)
            {
                var originalImpl = implementationInfos[i];
                var sortedImpl = sortedImplementations[i];
                
                // Only replace if the implementation actually changed position
                if (originalImpl.Node.Name != sortedImpl.Node.Name)
                {
                    EditText(originalImpl.Node.SourceSpan.Start.ByteIndex,
                           originalImpl.Node.SourceSpan.End.ByteIndex,
                           sortedImpl.OriginalText,
                           $"Sort {sortedImpl.Node.Name} implementation");
                }
            }
        }

        /// <summary>
        /// Extracts the original source text for a method implementation
        /// </summary>
        private string ExtractOriginalText(MethodImplNode implementation)
        {
            // For now, use the node's ToString() method to reconstruct the method
            // This provides a reasonable fallback until we can access the actual source text
            try
            {
                return implementation.ToString() ?? $"// Method {implementation.Name} - unable to reconstruct";
            }
            catch (Exception ex)
            {
                return $"// Method {implementation.Name} - error reconstructing: {ex.Message}";
            }
        }

        /// <summary>
        /// Resets the refactor state for a new analysis
        /// </summary>
        protected override void OnReset()
        {
            appClassNode = null;
            methodDeclarations.Clear();
            methodImplementations.Clear();
            implementationsInOrder = true;
        }
    }
}