using DiffPlex.Model;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that provides auto-completion for create() statements based on variable types
    /// </summary>
    public class CreateAutoComplete : BaseRefactor
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
        private ObjectCreateShortHand? targetCreateCall = null;

        // Track parent class information for %Super usage
        private string parentClassName = "";

        public CreateAutoComplete(ScintillaEditor editor) : base(editor){}

        public override void VisitAssignment(AssignmentNode node)
        {
            if ( node.Value is ObjectCreateShortHand createCall && createCall.SourceSpan.ContainsPosition(CurrentPosition))
            {
                isAppropriateContext = true;
                targetCreateCall = createCall;

                if (node.Target is IdentifierNode identifierNode)
                {
                    /* Look up variable */
                    var variableInfo = FindVariable(identifierNode.Name);
                    if (variableInfo != null)
                    {
                        detectedClassType = variableInfo.Type;
                    } else
                    {
                        if (identifierNode.Name.Equals("%Super", StringComparison.CurrentCultureIgnoreCase))
                        {
                            detectedClassType = parentClassName;
                        }
                        else
                        {

                            detectedClassType = identifierNode.Name;
                        }
                    }
                }
                else
                {
                    SetFailure("Create() expansion is not supported on expression that need an infered type.");
                }
                Debug.Log($"CreateAutoComplete: Found create() call for type {detectedClassType} at cursor position {CurrentPosition}");
            }

            base.VisitAssignment(node);
        }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            if (node.InitialValue is ObjectCreateShortHand createCall && createCall.SourceSpan.ContainsPosition(CurrentPosition))
            {
                isAppropriateContext = true;
                targetCreateCall = createCall;
                detectedClassType = node.Type.ToString();
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
            EditText(targetCreateCall.SourceSpan, replacementText, RefactorDescription);

            Task.Delay(250).ContinueWith((_) =>
            {
                var newPosition = ScintillaManager.GetCursorPosition(Editor);
                WinApi.SendMessage(AppDesignerProcess.CallbackWindow, MainForm.AR_FUNCTION_CALL_TIP, newPosition, '(');
            });
        }

        /// <summary>
        /// Generates the appropriate create() replacement based on the detected class type
        /// </summary>
        private string GenerateCreateReplacement(string classType)
        {
            if (Editor.AppDesignerProcess.Settings.AutoPair)
            {
                return $"create {classType}()";
            } else
            {
                return $"create {classType}(";
            }
        }
    }
}