using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted;
using AppRefiner.Services;
using AppRefiner;

namespace ParserPorting.Refactors.Impl
{
    /// <summary>
    /// Refactoring operation that provides auto-completion for create() statements based on variable types
    /// </summary>
    public class CreateAutoComplete : ScopedRefactor
    {
        public new static string RefactorName => "Create Auto Complete";
        public new static string RefactorDescription => "Auto-completes create() statements with appropriate class types";

        /// <summary>
        /// This refactor should not have a keyboard shortcut
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from refactoring lists
        /// </summary>
        public new static bool IsHidden => true;

        private bool isAppropriateContext = false;
        private string? detectedClassType = null;
        private MethodCallNode? targetCreateCall = null;
        private readonly bool autoPairingEnabled;

        // Track instance variables and their types
        private readonly Dictionary<string, string> instanceVariables = new();

        // Track parent class information for %Super usage
        private string? parentClassName = null;

        public CreateAutoComplete(ScintillaEditor editor, bool autoPairingEnabled = true) : base(editor)
        {
            this.autoPairingEnabled = autoPairingEnabled;
            Debug.Log($"CreateAutoComplete initialized with auto-pairing: {autoPairingEnabled}");
        }

        /// <summary>
        /// Check if this is a create() call and detect the appropriate type
        /// </summary>
        private bool IsCreateCallAtCursor(LocalVariableDeclarationWithAssignmentNode node)
        {
            // Check if the initializer is a method call to "create"
            if (node.InitialValue is MethodCallNode methodCall)
            {
                if (methodCall.MethodName.Equals("create", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if cursor is within the method call
                    if (methodCall.SourceSpan.IsValid)
                    {
                        var span = methodCall.SourceSpan;
                        if (CurrentPosition >= span.Start.Index && CurrentPosition <= span.End.Index + 1)
                        {
                            // Try to detect the class type from the variable type
                            if (node.Type is AppClassTypeNode appClassType)
                            {
                                detectedClassType = appClassType.ClassName;
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            if (IsCreateCallAtCursor(node) && node.InitialValue is MethodCallNode createCall)
            {
                isAppropriateContext = true;
                targetCreateCall = createCall;
                Debug.Log($"CreateAutoComplete: Found create() call for type {detectedClassType} at cursor position {CurrentPosition}");
            }

            base.VisitLocalVariableDeclarationWithAssignment(node);
        }

        public override void VisitAppClass(AppClassNode node)
        {
            // Track class extension information
            if (node.BaseClass != null)
            {
                parentClassName = node.BaseClass.ToString();
                Debug.Log($"Class extends {parentClassName}");
            }

            // Track instance variables for type detection
            foreach (var instanceVar in node.InstanceVariables)
            {
                if (instanceVar.Type is AppClassTypeNode appClassType)
                {
                    instanceVariables[instanceVar.Name] = appClassType.ClassName;
                }
            }

            base.VisitAppClass(node);
        }

        /// <summary>
        /// Complete the traversal and generate changes
        /// </summary>
        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);

            if (!isAppropriateContext)
            {
                Debug.Log("CreateAutoComplete: Not in appropriate context, skipping");
                return;
            }

            if (targetCreateCall == null || !targetCreateCall.SourceSpan.IsValid)
            {
                Debug.Log("CreateAutoComplete: Invalid target create call, skipping");
                return;
            }

            if (string.IsNullOrEmpty(detectedClassType))
            {
                Debug.Log("CreateAutoComplete: No class type detected, skipping");
                return;
            }

            // Generate appropriate create() replacement based on detected type
            string replacementText = GenerateCreateReplacement(detectedClassType);

            Debug.Log($"CreateAutoComplete: Replacing create() with {replacementText}");

            // Replace the create() call with the expanded version
            ReplaceNode(targetCreateCall, replacementText, RefactorDescription);
        }

        /// <summary>
        /// Generates the appropriate create() replacement based on the detected class type
        /// </summary>
        private string GenerateCreateReplacement(string classType)
        {
            // Handle special cases
            if (classType.Equals(parentClassName, StringComparison.OrdinalIgnoreCase))
            {
                // If creating parent class type, use %Super
                return "%Super";
            }

            // For regular app classes, use create with class name
            if (classType.Contains(":"))
            {
                // Fully qualified class name
                return $"create {classType}()";
            }
            else
            {
                // Simple class name - might need import
                return $"create {classType}()";
            }
        }
    }
}