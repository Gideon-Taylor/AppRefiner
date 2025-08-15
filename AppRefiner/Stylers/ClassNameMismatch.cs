using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using AppRefiner.QuickFixes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class ClassNameMismatch : BaseStyler
    {
        public ClassNameMismatch()
        {
            Description = "Highlights class names that do not match the expected name.";
            Active = true;
        }


        public override void EnterClassDeclarationExtension([NotNull] ClassDeclarationExtensionContext context)
        {
            VerifyClassName(context.genericID());
        }

        public override void EnterClassDeclarationImplementation([NotNull] ClassDeclarationImplementationContext context)
        {
            VerifyClassName(context.genericID());
        }

        public override void EnterClassDeclarationPlain([NotNull] ClassDeclarationPlainContext context)
        {
            VerifyClassName(context.genericID());
        }


        private void VerifyClassName(GenericIDContext genericID)
        {
            if (Editor == null)
            {
                return; // No editor available
            }
            var className = genericID.GetText();
            var expectedName = Editor.ClassPath.Split(":").LastOrDefault() ?? string.Empty;
            if (!string.Equals(className, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                AddIndicator(
                    genericID.Start, 
                    genericID.Stop, 
                    IndicatorType.SQUIGGLE, 
                    0x0000FFFF, // Red color for mismatch
                    $"Class name '{className}' does not match expected name '{expectedName}'.",
                    [(typeof(CorrectClassName), "Correct class name.")]
                );
            }

        }
    }
}
