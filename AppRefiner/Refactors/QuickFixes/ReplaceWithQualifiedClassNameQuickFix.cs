using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Refactors.QuickFixes
{
    /// <summary>
    /// QuickFix that replaces an unqualified class reference with a fully qualified name.
    /// Extracts the qualified name from the editor's QuickFixContext.
    /// </summary>
    /// <remarks>
    /// Used when multiple imports provide the same class name, creating ambiguity.
    /// Example:
    /// <code>
    /// import APP_PKG:UI:FormRenderer;
    /// import APP_PKG:DATA:FormRenderer;
    ///
    /// Local FormRenderer &r;  // Ambiguous
    /// </code>
    ///
    /// This quick fix replaces "FormRenderer" with the selected qualified name
    /// (e.g., "APP_PKG:UI:FormRenderer").
    ///
    /// Special case: When used on an object creation in a variable declaration:
    /// <code>
    /// Local RequestHandler &c = create RequestHandler();
    /// // Selecting "Use OPT_CALL:RequestHandler" results in:
    /// Local OPT_CALL:RequestHandler &c = create OPT_CALL:RequestHandler();
    /// </code>
    /// Both the variable type and the object creation type are updated.
    /// </remarks>
    public class ReplaceWithQualifiedClassNameQuickFix : BaseRefactor
    {
        public new static string RefactorName => "Replace with Qualified Class Name";
        public new static string RefactorDescription => "Replaces an unqualified class reference with a fully qualified name";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => true;  // Hidden from refactor menu (only appears via quick fix)

        private readonly string _qualifiedName;
        private readonly int _cursorPosition;

        public ReplaceWithQualifiedClassNameQuickFix(ScintillaEditor editor) : base(editor)
        {
            // Read qualified name from editor context (set by AutoCompleteService.HandleQuickFixSelection)
            // Expected format: "Use APP_PKG:UI:FormRenderer"
            string? contextValue = editor.QuickFixContext as string;

            if (string.IsNullOrEmpty(contextValue))
                throw new InvalidOperationException("QuickFix context does not contain qualified class name");

            // Extract qualified name from description (remove "Use " prefix)
            _qualifiedName = ExtractQualifiedName(contextValue);
            _cursorPosition = ScintillaManager.GetCursorPosition(editor);

            Debug.Log($"ReplaceWithQualifiedClassNameQuickFix: Replacing with '{_qualifiedName}' at position {_cursorPosition}");
        }

        /// <summary>
        /// Extracts the qualified name from the quick fix description.
        /// Expected format: "Use APP_PKG:UI:FormRenderer"
        /// </summary>
        private string ExtractQualifiedName(string description)
        {
            const string prefix = "Use ";
            if (description.StartsWith(prefix))
            {
                return description.Substring(prefix.Length).Trim();
            }
            return description.Trim();  // Fallback
        }

        public override void VisitAppClassType(AppClassTypeNode node)
        {
            // Check if this is the node at the cursor position
            if (!node.SourceSpan.ContainsPosition(_cursorPosition))
            {
                base.VisitAppClassType(node);
                return;
            }

            // Only process unqualified references (no package path)
            if (node.PackagePath.Count > 0)
            {
                Debug.Log("ReplaceWithQualifiedClassNameQuickFix: Reference is already qualified, skipping");
                base.VisitAppClassType(node);
                return;
            }

            // Replace the unqualified reference with the qualified name
            EditText(
                node.SourceSpan,
                _qualifiedName,
                $"Replace '{node.ClassName}' with '{_qualifiedName}'"
            );

            Debug.Log($"ReplaceWithQualifiedClassNameQuickFix: Replaced '{node.ClassName}' with '{_qualifiedName}'");

            // Check if this type node is part of an ObjectCreationNode that's being assigned to a variable
            // Example: Local RequestHandler &c = create RequestHandler();
            // In this case, we should also update the variable's declared type
            CheckAndUpdateVariableDeclarationType(node);

            base.VisitAppClassType(node);
        }

        /// <summary>
        /// Checks if the AppClassTypeNode is part of an ObjectCreationNode used in a variable declaration,
        /// and if so, updates the variable's declared type as well.
        /// </summary>
        private void CheckAndUpdateVariableDeclarationType(AppClassTypeNode objectCreationType)
        {
            // Check if this type is part of an ObjectCreationNode
            var objectCreation = objectCreationType.Parent as ObjectCreationNode;
            if (objectCreation == null)
                return;

            // Find if this ObjectCreationNode is the initial value in a LocalVariableDeclarationWithAssignmentNode
            var varDeclaration = objectCreation.FindAncestor<LocalVariableDeclarationWithAssignmentNode>();
            if (varDeclaration == null)
                return;

            // Verify that the ObjectCreationNode is indeed the InitialValue (not some nested expression)
            if (varDeclaration.InitialValue != objectCreation)
                return;

            // Check if the variable's declared type is also an unqualified AppClassTypeNode with the same class name
            if (varDeclaration.Type is not AppClassTypeNode varType)
                return;

            // Skip if the variable type is already qualified
            if (varType.PackagePath.Count > 0)
                return;

            // Check if the variable type has the same class name (case-insensitive)
            if (!string.Equals(varType.ClassName, objectCreationType.ClassName, StringComparison.OrdinalIgnoreCase))
                return;

            // Replace the variable's type with the qualified name
            EditText(
                varType.SourceSpan,
                _qualifiedName,
                $"Replace variable type '{varType.ClassName}' with '{_qualifiedName}'"
            );

            Debug.Log($"ReplaceWithQualifiedClassNameQuickFix: Also replaced variable type '{varType.ClassName}' with '{_qualifiedName}'");
        }
    }
}
