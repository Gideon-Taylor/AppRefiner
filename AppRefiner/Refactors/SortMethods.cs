using PeopleCodeParser.SelfHosted.Nodes;

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

        // Track method declarations in the class header
        private readonly List<MethodInfo> methodDeclarations = new();

        // Track method implementations in the class body
        private readonly List<MethodInfo> methodImplementations = new();

        // Track getter/setter declarations in the class header
        private readonly List<PropertyInfo> propertyDeclarations = new();

        // Track getter/setter implementations in the class body
        private readonly List<PropertyInfo> propertyImplementations = new();

        // Flag to indicate if we're currently in a class program
        private bool isClassProgram = false;

        // Flag to indicate if implementations are already in the correct order
        private bool implementationsInOrder = true;

        public SortMethods(AppRefiner.ScintillaEditor editor) : base(editor)
        {
        }

        /// <summary>
        /// Information about a method declaration or implementation
        /// </summary>
        private class MethodInfo
        {
            public string Name { get; }
            public int StartIndex { get; }
            public int EndIndex { get; }
            public string OriginalText { get; }
            public string LeadingComments { get; set; } = string.Empty;

            public MethodInfo(string name, int startIndex, int endIndex, string originalText)
            {
                Name = name;
                StartIndex = startIndex;
                EndIndex = endIndex;
                OriginalText = originalText;
            }
        }

        /// <summary>
        /// Information about a property getter/setter declaration or implementation
        /// </summary>
        private class PropertyInfo
        {
            public string Name { get; }
            public bool IsGetter { get; }
            public int StartIndex { get; }
            public int EndIndex { get; }
            public string OriginalText { get; }
            public string LeadingComments { get; set; } = string.Empty;

            public PropertyInfo(string name, bool isGetter, int startIndex, int endIndex, string originalText)
            {
                Name = name;
                IsGetter = isGetter;
                StartIndex = startIndex;
                EndIndex = endIndex;
                OriginalText = originalText;
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

            public SortMethodsDialog(int methodCount, int propertyCount)
            {
                InitializeComponent(methodCount, propertyCount);
            }

            private void InitializeComponent(int methodCount, int propertyCount)
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
                this.lblPrompt.AutoSize = true;
                this.lblPrompt.Location = new System.Drawing.Point(12, 40);
                this.lblPrompt.Name = "lblPrompt";
                this.lblPrompt.Size = new System.Drawing.Size(260, 30);
                this.lblPrompt.TabIndex = 0;
                this.lblPrompt.Text = "This will reorder method and property implementations\nto match the order defined in the class declaration.";

                // lblMethodCount
                this.lblMethodCount.AutoSize = true;
                this.lblMethodCount.Location = new System.Drawing.Point(12, 80);
                this.lblMethodCount.Name = "lblMethodCount";
                this.lblMethodCount.Size = new System.Drawing.Size(260, 15);
                this.lblMethodCount.TabIndex = 1;

                string methodText = methodCount == 1 ? "method" : "methods";
                string propertyText = propertyCount == 1 ? "property" : "properties";
                this.lblMethodCount.Text = $"Found {methodCount} {methodText} and {propertyCount} {propertyText} to sort.";

                // btnOk
                this.btnOk.DialogResult = DialogResult.OK;
                this.btnOk.Location = new System.Drawing.Point(116, 110);
                this.btnOk.Name = "btnOk";
                this.btnOk.Size = new System.Drawing.Size(75, 28);
                this.btnOk.TabIndex = 2;
                this.btnOk.Text = "&Sort";
                this.btnOk.UseVisualStyleBackColor = true;

                // btnCancel
                this.btnCancel.DialogResult = DialogResult.Cancel;
                this.btnCancel.Location = new System.Drawing.Point(197, 110);
                this.btnCancel.Name = "btnCancel";
                this.btnCancel.Size = new System.Drawing.Size(75, 28);
                this.btnCancel.TabIndex = 3;
                this.btnCancel.Text = "&Cancel";
                this.btnCancel.UseVisualStyleBackColor = true;

                // SortMethodsDialog
                this.AcceptButton = this.btnOk;
                this.CancelButton = this.btnCancel;
                this.ClientSize = new System.Drawing.Size(284, 150);
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
            if (!isClassProgram)
            {
                SetFailure("This refactoring only works on class-style programs.");
                return false;
            }

            if (methodDeclarations.Count == 0 && propertyDeclarations.Count == 0)
            {
                SetFailure("No method or property declarations found in the class header.");
                return false;
            }

            if (methodImplementations.Count == 0 && propertyImplementations.Count == 0)
            {
                SetFailure("No method or property implementations found in the class body.");
                return false;
            }

            if (implementationsInOrder)
            {
                SetFailure("Method and property implementations are already in the correct order.");
                return false;
            }

            using var dialog = new SortMethodsDialog(methodImplementations.Count, propertyImplementations.Count);

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
            isClassProgram = true;

            // Track method declarations
            foreach (var method in node.Methods)
            {
                if (method.SourceSpan.IsValid)
                {
                    var originalText = GetOriginalText(method);
                    if (!string.IsNullOrEmpty(originalText))
                    {
                        methodDeclarations.Add(new MethodInfo(
                            method.Name,
                            method.SourceSpan.Start.Index,
                            method.SourceSpan.End.Index,
                            originalText
                        ));
                    }
                }
            }

            // Track property declarations
            foreach (var property in node.Properties)
            {
                if (property.SourceSpan.IsValid)
                {
                    var originalText = GetOriginalText(property);
                    if (!string.IsNullOrEmpty(originalText))
                    {
                        // Add getter
                        propertyDeclarations.Add(new PropertyInfo(
                            property.Name,
                            true,
                            property.SourceSpan.Start.Index,
                            property.SourceSpan.End.Index,
                            originalText
                        ));

                        // Add setter if it exists
                        if (property.HasSetter)
                        {
                            propertyDeclarations.Add(new PropertyInfo(
                                property.Name,
                                false,
                                property.SourceSpan.Start.Index,
                                property.SourceSpan.End.Index,
                                originalText
                            ));
                        }
                    }
                }
            }

            // Track method implementations
            foreach (var method in node.MethodImplementations)
            {
                if (method.SourceSpan.IsValid)
                {
                    var originalText = GetOriginalText(method);
                    if (!string.IsNullOrEmpty(originalText))
                    {
                        methodImplementations.Add(new MethodInfo(
                            method.Name,
                            method.SourceSpan.Start.Index,
                            method.SourceSpan.End.Index,
                            originalText
                        ));
                    }
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
            // Simple check: if method implementations are in the same order as declarations
            if (methodImplementations.Count != methodDeclarations.Count)
            {
                implementationsInOrder = false;
                return;
            }

            for (int i = 0; i < methodDeclarations.Count; i++)
            {
                if (i >= methodImplementations.Count ||
                    !methodDeclarations[i].Name.Equals(methodImplementations[i].Name, StringComparison.OrdinalIgnoreCase))
                {
                    implementationsInOrder = false;
                    return;
                }
            }
        }

        /// <summary>
        /// Applies the method sorting changes
        /// </summary>
        private void ApplyChanges()
        {
            // Create sorted implementation text based on declaration order
            var sortedImplementations = new List<string>();

            // Sort methods first
            foreach (var declaration in methodDeclarations)
            {
                var implementation = methodImplementations.FirstOrDefault(impl =>
                    impl.Name.Equals(declaration.Name, StringComparison.OrdinalIgnoreCase));
                if (implementation != null)
                {
                    sortedImplementations.Add(implementation.OriginalText);
                }
            }

            // Then sort properties
            foreach (var declaration in propertyDeclarations)
            {
                var implementation = propertyImplementations.FirstOrDefault(impl =>
                    impl.Name.Equals(declaration.Name, StringComparison.OrdinalIgnoreCase) &&
                    impl.IsGetter == declaration.IsGetter);
                if (implementation != null)
                {
                    sortedImplementations.Add(implementation.OriginalText);
                }
            }

            if (sortedImplementations.Count > 0 && methodImplementations.Count > 0)
            {
                // Replace the entire implementation section
                var firstImpl = methodImplementations.First();
                var lastImpl = methodImplementations.Last();

                string newImplementationsText = string.Join(Environment.NewLine + Environment.NewLine, sortedImplementations);

                EditText(firstImpl.StartIndex, lastImpl.EndIndex, newImplementationsText, "Sort methods and properties");
            }
        }
    }
}