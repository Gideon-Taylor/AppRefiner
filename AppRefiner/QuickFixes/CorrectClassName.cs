using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using AppRefiner.Refactors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.QuickFixes
{
    public class CorrectClassName : BaseRefactor
    {
        private string expectedName = String.Empty;
        private string currentClassName = String.Empty;
        private bool neededToReplace = false;
        public CorrectClassName(ScintillaEditor editor) : base(editor)
        {
            expectedName = editor.ClassPath.Split(":").LastOrDefault() ?? string.Empty;
        }

        public new static string RefactorName => "QuickFix: Correct Class Name";
        public new static string RefactorDescription => "Corrects the class name to match the expected name.";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => true;

        public override void EnterClassDeclarationExtension([NotNull] PeopleCodeParser.ClassDeclarationExtensionContext context)
        {
            currentClassName = context.genericID().GetText();
            if (!string.Equals(currentClassName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                neededToReplace = true;
                // Logic to replace the class name
                ReplaceNode(context.genericID(), expectedName, ""); // Implement this method to perform the replacement
            }
        }

        public override void EnterClassDeclarationImplementation([NotNull] PeopleCodeParser.ClassDeclarationImplementationContext context)
        {
            currentClassName = context.genericID().GetText();
            if (!string.Equals(currentClassName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                neededToReplace = true;
                // Logic to replace the class name
                ReplaceNode(context.genericID(), expectedName, ""); // Implement this method to perform the replacement
            }
        }

        public override void EnterClassDeclarationPlain([NotNull] PeopleCodeParser.ClassDeclarationPlainContext context)
        {
            currentClassName = context.genericID().GetText();
            if (!string.Equals(currentClassName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                neededToReplace = true;
                // Logic to replace the class name
                ReplaceNode(context.genericID(), expectedName, ""); // Implement this method to perform the replacement
            }
        }

        public override void EnterMethodHeader([NotNull] PeopleCodeParser.MethodHeaderContext context)
        {
            var methodName = context.genericID().GetText();
            if (methodName.Equals(currentClassName) && neededToReplace)
            {
                // Logic to replace the method name
                ReplaceNode(context.genericID(), expectedName, ""); // Implement this method to perform the replacement
            }

        }

        public override void EnterMethod([NotNull] PeopleCodeParser.MethodContext context)
        {
            var methodName = context.genericID().GetText();
            if (methodName.Equals(currentClassName) && neededToReplace)
            {
                // Logic to replace the method name
                ReplaceNode(context.genericID(), expectedName, ""); // Implement this method to perform the replacement
            }
        }
    }
}
