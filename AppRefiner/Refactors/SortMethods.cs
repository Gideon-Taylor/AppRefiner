using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static AppRefiner.PeopleCode.PeopleCodeParser;

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
        public new static ModifierKeys ShortcutModifiers => ModifierKeys.Control | ModifierKeys.Shift;

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
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SortMethods"/> class
        /// </summary>
        /// <param name="editor">The Scintilla editor instance</param>
        public SortMethods(ScintillaEditor editor) : base(editor)
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
        /// <returns>True if the user confirmed, false if canceled</returns>
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

        /// <summary>
        /// Detect if we're in a class program
        /// </summary>
        public override void EnterAppClassProgram([NotNull] AppClassProgramContext context)
        {
            isClassProgram = true;
        }

        /// <summary>
        /// Track method header declarations in the class header
        /// </summary>
        public override void EnterMethodHeader([NotNull] MethodHeaderContext context)
        {            
            if (!isClassProgram) return;
            
            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                string methodName = genericIdNode.GetText();
                
                // Add to method declarations
                methodDeclarations.Add(new MethodInfo(
                    methodName,
                    context.Start.StartIndex,
                    context.Stop.StopIndex,
                    GetOriginalText(context)!
                ));
            }
        }
        
        /// <summary>
        /// Track property declarations in the class header
        /// </summary>
        public override void EnterPropertyGetSet([NotNull] PropertyGetSetContext context)
        {
            
            if (!isClassProgram) return;
            
            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                string propertyName = genericIdNode.GetText();
                
                // Check if this property has GET and/or SET
                bool hasGetter = true;
                bool hasSetter = context.SET() != null;
                
                // Add getter if present
                if (hasGetter)
                {
                    propertyDeclarations.Add(new PropertyInfo(
                        propertyName,
                        true,
                        context.Start.StartIndex,
                        context.Stop.StopIndex,
                        GetOriginalText(context)!
                    ));
                }
                
                // Add setter if present
                if (hasSetter)
                {
                    propertyDeclarations.Add(new PropertyInfo(
                        propertyName,
                        false,
                        context.Start.StartIndex,
                        context.Stop.StopIndex,
                        GetOriginalText(context)!
                    ));
                }
            }
        }
        
        /// <summary>
        /// Track method implementations in the class body
        /// </summary>
        public override void EnterMethod([NotNull] MethodContext context)
        {            
            if (!isClassProgram) return;
            
            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                string methodName = genericIdNode.GetText();
                
                // Add to method implementations
                methodImplementations.Add(new MethodInfo(
                    methodName,
                    context.Start.StartIndex,
                    context.Stop.StopIndex,
                    GetOriginalText(context, true)!
                ));
            }
        }
        
        /// <summary>
        /// Track getter implementations in the class body
        /// </summary>
        public override void EnterGetter([NotNull] GetterContext context)
        {            
            if (!isClassProgram) return;
            
            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                string propertyName = genericIdNode.GetText();
                
                // Add to property implementations
                propertyImplementations.Add(new PropertyInfo(
                    propertyName,
                    true,
                    context.Start.StartIndex,
                    context.Stop.StopIndex,
                    GetOriginalText(context, true)!
                ));
            }
        }
        
        /// <summary>
        /// Track setter implementations in the class body
        /// </summary>
        public override void EnterSetter([NotNull] SetterContext context)
        {            
            if (!isClassProgram) return;
            
            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                string propertyName = genericIdNode.GetText();
                
                // Add to property implementations
                propertyImplementations.Add(new PropertyInfo(
                    propertyName,
                    false,
                    context.Start.StartIndex,
                    context.Stop.StopIndex,
                    GetOriginalText(context, true)!
                ));
            }
        }
        
        /// <summary>
        /// Generate the refactoring changes when we reach the end of the program
        /// </summary>
        public override void ExitProgram([NotNull] ProgramContext context)
        {            
            if (!isClassProgram || 
                methodDeclarations.Count == 0 && propertyDeclarations.Count == 0 ||
                methodImplementations.Count == 0 && propertyImplementations.Count == 0)
            {
                return; // Will be handled in ShowRefactorDialog
            }
            
            // Create a combined list of all declarations in the order they appear in the class header
            var orderedDeclarations = new List<(string Name, bool IsMethod, bool IsGetter)>();
            
            foreach (var methodDecl in methodDeclarations)
            {
                orderedDeclarations.Add((methodDecl.Name, true, false));
            }
            
            foreach (var propertyDecl in propertyDeclarations)
            {
                orderedDeclarations.Add((propertyDecl.Name, false, propertyDecl.IsGetter));
            }
            
            // Create a map of method and property implementations
            var methodMap = methodImplementations.ToDictionary(m => m.Name, m => m);
            var getterMap = propertyImplementations.Where(p => p.IsGetter).ToDictionary(p => p.Name, p => p);
            var setterMap = propertyImplementations.Where(p => !p.IsGetter).ToDictionary(p => p.Name, p => p);
            
            // Sort implementations by their order in the class header
            var sortedImplementations = new List<(int StartIndex, int EndIndex, string Text)>();
            
            foreach (var decl in orderedDeclarations)
            {
                if (decl.IsMethod)
                {
                    if (methodMap.TryGetValue(decl.Name, out var methodImpl))
                    {
                        sortedImplementations.Add((methodImpl.StartIndex, methodImpl.EndIndex, methodImpl.OriginalText));
                        methodMap.Remove(decl.Name);
                    }
                }
                else if (decl.IsGetter)
                {
                    if (getterMap.TryGetValue(decl.Name, out var getterImpl))
                    {
                        sortedImplementations.Add((getterImpl.StartIndex, getterImpl.EndIndex, getterImpl.OriginalText));
                        getterMap.Remove(decl.Name);
                    }
                }
                else
                {
                    if (setterMap.TryGetValue(decl.Name, out var setterImpl))
                    {
                        sortedImplementations.Add((setterImpl.StartIndex, setterImpl.EndIndex, setterImpl.OriginalText));
                        setterMap.Remove(decl.Name);
                    }
                }
            }
            
            // Add any remaining implementations that weren't in the class header
            foreach (var methodImpl in methodMap.Values)
            {
                sortedImplementations.Add((methodImpl.StartIndex, methodImpl.EndIndex, methodImpl.OriginalText));
            }
            
            foreach (var getterImpl in getterMap.Values)
            {
                sortedImplementations.Add((getterImpl.StartIndex, getterImpl.EndIndex, getterImpl.OriginalText));
            }
            
            foreach (var setterImpl in setterMap.Values)
            {
                sortedImplementations.Add((setterImpl.StartIndex, setterImpl.EndIndex, setterImpl.OriginalText));
            }
            
            // Sort by original position
            sortedImplementations.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
            
            // If implementations are already in the correct order, no changes needed
            implementationsInOrder = true;
            for (int i = 0; i < sortedImplementations.Count; i++)
            {
                var impl = sortedImplementations[i];
                if (i < orderedDeclarations.Count)
                {
                    var decl = orderedDeclarations[i];
                    
                    string implName = "";
                    bool implIsMethod = false;
                    bool implIsGetter = false;
                    
                    // Determine the name and type of the implementation
                    if (methodImplementations.Any(m => m.StartIndex == impl.StartIndex))
                    {
                        var methodImpl = methodImplementations.First(m => m.StartIndex == impl.StartIndex);
                        implName = methodImpl.Name;
                        implIsMethod = true;
                    }
                    else if (propertyImplementations.Any(p => p.StartIndex == impl.StartIndex))
                    {
                        var propImpl = propertyImplementations.First(p => p.StartIndex == impl.StartIndex);
                        implName = propImpl.Name;
                        implIsMethod = false;
                        implIsGetter = propImpl.IsGetter;
                    }
                    
                    // Check if this implementation matches the declaration at the same position
                    if (implName != decl.Name || implIsMethod != decl.IsMethod || (!implIsMethod && implIsGetter != decl.IsGetter))
                    {
                        implementationsInOrder = false;
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Apply the refactoring changes
        /// </summary>
        private void ApplyChanges()
        {
            // Get the source code from the editor
            if (Editor == null)
            {
                SetFailure("Could not apply changes, editor was null.");
                return;
            }
            
            string source = Editor.ContentString!;

            // Create a combined list of all declarations in the order they appear in the class header
            var orderedDeclarations = new List<(string Name, bool IsMethod, bool IsGetter)>();
            
            foreach (var methodDecl in methodDeclarations)
            {
                orderedDeclarations.Add((methodDecl.Name, true, false));
            }
            
            foreach (var propertyDecl in propertyDeclarations)
            {
                orderedDeclarations.Add((propertyDecl.Name, false, propertyDecl.IsGetter));
            }
            
            // Extract leading comments for each method and property implementation
            ExtractLeadingComments(source);
            
            // Generate the new implementation order based on the class header order
            var newImplementations = new List<(int StartIndex, int EndIndex, string Text, string LeadingComments)>();
            
            // First add implementations that match declarations in the class header
            foreach (var decl in orderedDeclarations)
            {
                if (decl.IsMethod)
                {
                    var methodImpl = methodImplementations.FirstOrDefault(m => m.Name == decl.Name);
                    if (methodImpl != null)
                    {
                        newImplementations.Add((methodImpl.StartIndex, methodImpl.EndIndex, methodImpl.OriginalText, methodImpl.LeadingComments));
                    }
                }
                else if (decl.IsGetter)
                {
                    var getterImpl = propertyImplementations.FirstOrDefault(p => p.Name == decl.Name && p.IsGetter);
                    if (getterImpl != null)
                    {
                        newImplementations.Add((getterImpl.StartIndex, getterImpl.EndIndex, getterImpl.OriginalText, getterImpl.LeadingComments));
                    }
                }
                else
                {
                    var setterImpl = propertyImplementations.FirstOrDefault(p => p.Name == decl.Name && !p.IsGetter);
                    if (setterImpl != null)
                    {
                        newImplementations.Add((setterImpl.StartIndex, setterImpl.EndIndex, setterImpl.OriginalText, setterImpl.LeadingComments));
                    }
                }
            }
            
            // Then add any implementations that weren't in the class header
            foreach (var methodImpl in methodImplementations)
            {
                if (!orderedDeclarations.Any(d => d.IsMethod && d.Name == methodImpl.Name))
                {
                    newImplementations.Add((methodImpl.StartIndex, methodImpl.EndIndex, methodImpl.OriginalText, methodImpl.LeadingComments));
                }
            }
            
            foreach (var propImpl in propertyImplementations)
            {
                if (!orderedDeclarations.Any(d => !d.IsMethod && d.Name == propImpl.Name && d.IsGetter == propImpl.IsGetter))
                {
                    newImplementations.Add((propImpl.StartIndex, propImpl.EndIndex, propImpl.OriginalText, propImpl.LeadingComments));
                }
            }
            
            // Get original implementations in the order they appear
            var sortedImplementations = new List<(int StartIndex, int EndIndex, string Text, string LeadingComments)>();
            
            foreach (var methodImpl in methodImplementations)
            {
                sortedImplementations.Add((methodImpl.StartIndex, methodImpl.EndIndex, methodImpl.OriginalText, methodImpl.LeadingComments));
            }
            
            foreach (var propImpl in propertyImplementations)
            {
                sortedImplementations.Add((propImpl.StartIndex, propImpl.EndIndex, propImpl.OriginalText, propImpl.LeadingComments));
            }
            
            // Sort by original position
            sortedImplementations.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
            
            // Find the class body start and end
            var classBodyStart = int.MaxValue;
            var classBodyEnd = int.MinValue;
            
            foreach (var impl in sortedImplementations)
            {
                classBodyStart = Math.Min(classBodyStart, impl.StartIndex - impl.LeadingComments.Length);
                classBodyEnd = Math.Max(classBodyEnd, impl.EndIndex);
            }
            
            if (classBodyStart == int.MaxValue || classBodyEnd == int.MinValue)
            {
                SetFailure("Could not determine class body boundaries.");
                return;
            }
            
            // Build the new class body content
            var newClassBodyContent = new System.Text.StringBuilder();
            
            // Add the first implementation with any leading whitespace/semicolons
            if (newImplementations.Count > 0)
            {
                var firstImpl = sortedImplementations[0];
                var firstNewImpl = newImplementations[0];
                
                // Preserve any leading whitespace or semicolons before the first implementation
                // but exclude the leading comments that we'll add with each method
                var leadingText = source[classBodyStart..(firstImpl.StartIndex - firstImpl.LeadingComments.Length)];
                newClassBodyContent.Append(leadingText);
                
                // Add the first implementation with its leading comments
                newClassBodyContent.Append(firstNewImpl.LeadingComments);
                newClassBodyContent.Append(firstNewImpl.Text);
                
                // Add the rest of the implementations with appropriate separators
                for (int i = 1; i < newImplementations.Count; i++)
                {
                    var prevImpl = sortedImplementations[i - 1];
                    var currImpl = sortedImplementations[i];
                    var newImpl = newImplementations[i];
                    
                    // Add a consistent separator between implementations (double newline)
                    // We don't use the original separator since we want to ensure proper spacing
                    newClassBodyContent.Append("\n\n");
                    
                    // Add the implementation with its leading comments
                    newClassBodyContent.Append(newImpl.LeadingComments);
                    newClassBodyContent.Append(newImpl.Text);
                }
                
                // Preserve any trailing content after the last implementation
                var lastImpl = sortedImplementations[sortedImplementations.Count - 1];
                /* check if there is trailing text before appending it */
                if (lastImpl.EndIndex + 1 < classBodyEnd)
                {
                    var trailingText = source[(lastImpl.EndIndex + 1)..classBodyEnd];
                    newClassBodyContent.Append(trailingText);
                }
                
                // Replace the entire class body with the new content
                ReplaceText(
                    classBodyStart,
                    classBodyEnd,
                    newClassBodyContent.ToString(),
                    "Reordered method and property implementations to match class declaration order"
                );
            }
        }
        
        /// <summary>
        /// Extracts leading comments for each method and property implementation
        /// </summary>
        /// <param name="source">The source code</param>
        private void ExtractLeadingComments(string source)
        {
            // Process method implementations
            foreach (var methodImpl in methodImplementations)
            {
                int commentStart = FindCommentStart(source, methodImpl.StartIndex);
                if (commentStart < methodImpl.StartIndex)
                {
                    methodImpl.LeadingComments = source[commentStart..methodImpl.StartIndex];
                }
            }
            
            // Process property implementations
            foreach (var propImpl in propertyImplementations)
            {
                int commentStart = FindCommentStart(source, propImpl.StartIndex);
                if (commentStart < propImpl.StartIndex)
                {
                    propImpl.LeadingComments = source[commentStart..propImpl.StartIndex];
                }
            }
        }
        
        /// <summary>
        /// Finds the start index of comments that precede a method or property
        /// </summary>
        /// <param name="source">The source code</param>
        /// <param name="methodStart">The start index of the method or property</param>
        /// <returns>The start index of the comments</returns>
        private int FindCommentStart(string source, int methodStart)
        {
            // Start from the method start and go backwards
            int pos = methodStart - 1;
            
            // Skip whitespace immediately before the method
            while (pos >= 0 && char.IsWhiteSpace(source[pos]))
            {
                pos--;
            }
            
            // If we didn't find any non-whitespace, there's no comment
            if (pos < 0 || !IsCommentChar(source[pos]))
            {
                return methodStart;
            }
            
            // We found a comment character, now find the start of the comment block
            int commentEnd = pos + 1;
            
            // Handle block comments /* ... */
            if (pos >= 1 && source[pos] == '/' && source[pos - 1] == '*')
            {
                // Find the start of the block comment
                while (pos >= 1 && !(source[pos] == '*' && source[pos - 1] == '/'))
                {
                    pos--;
                }
                
                if (pos >= 1)
                {
                    // Include the /* at the start
                    pos -= 1;
                }
            }
            // Handle line comments //
            else if (source[pos] == '/' && pos >= 1 && source[pos - 1] == '/')
            {
                // Find the start of the line
                while (pos >= 0 && source[pos] != '\n')
                {
                    pos--;
                }
                
                // Move past the newline
                pos++;
            }
            
            // Find the start of the entire comment block (including multiple comment lines)
            int commentBlockStart = pos;
            
            // Go backwards to find any preceding comments or blank lines that should be included
            pos--;
            while (pos >= 0)
            {
                // Skip whitespace
                while (pos >= 0 && char.IsWhiteSpace(source[pos]))
                {
                    pos--;
                }
                
                // If we hit non-whitespace that's not a comment, we're done
                if (pos < 0 || !IsCommentChar(source[pos]))
                {
                    break;
                }
                
                // We found another comment, find its start
                if (pos >= 1 && source[pos] == '/' && source[pos - 1] == '*')
                {
                    // Find the start of the block comment
                    while (pos >= 1 && !(source[pos] == '*' && source[pos - 1] == '/'))
                    {
                        pos--;
                    }
                    
                    if (pos >= 1)
                    {
                        // Include the /* at the start
                        pos -= 1;
                    }
                }
                else if (source[pos] == '/' && pos >= 1 && source[pos - 1] == '/')
                {
                    // Find the start of the line
                    while (pos >= 0 && source[pos] != '\n')
                    {
                        pos--;
                    }
                    
                    // Move past the newline
                    pos++;
                }
                
                // Update the comment block start
                commentBlockStart = pos;
                
                // Move to the character before this comment
                pos--;
            }
            
            return commentBlockStart;
        }
        
        /// <summary>
        /// Determines if a character is part of a comment
        /// </summary>
        /// <param name="c">The character to check</param>
        /// <returns>True if the character is part of a comment</returns>
        private bool IsCommentChar(char c)
        {
            return c == '/' || c == '*';
        }
    }
}
