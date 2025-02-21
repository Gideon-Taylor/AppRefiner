using Antlr4.Runtime.Misc;
using static global::AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    class PropertyAsVariable : BaseStyler
    {
        private HashSet<string> publicProperties = new();
        private bool inPublicProtected = false;
        private bool inConstructor = false;
        private string? currentClassName;

        public PropertyAsVariable()
        {
            Description = "Highlights properties used as variables outside constructors";
            Active = true;
        }
        public override void EnterPublicHeader([NotNull] PublicHeaderContext context)
        {
            inPublicProtected = true;
        }
        public override void ExitPublicHeader([NotNull] PublicHeaderContext context)
        {
            inPublicProtected = false;
        }

        public override void EnterProtectedHeader([NotNull] ProtectedHeaderContext context)
        {
            inPublicProtected = true;
        }

        public override void ExitProtectedHeader([NotNull] ProtectedHeaderContext context)
        {
            inPublicProtected = false;
        }

        public override void EnterClassDeclarationExtension([NotNull] ClassDeclarationExtensionContext context)
        {
            currentClassName = context.genericID().GetText();
        }

        public override void EnterClassDeclarationImplementation([NotNull] ClassDeclarationImplementationContext context)
        {
            currentClassName = context.genericID().GetText();
        }

        public override void EnterClassDeclarationPlain([NotNull] ClassDeclarationPlainContext context)
        {
            currentClassName = context.genericID().GetText();
        }

        public override void EnterMethod([NotNull] MethodContext context)
        {
            var methodName = context.genericID().GetText();
            inConstructor = methodName == currentClassName;
        }

        public override void ExitMethod([NotNull] MethodContext context)
        {
            inConstructor = false;
        }

        public override void EnterPropertyDirect([NotNull] PropertyDirectContext context)
        {
            if (inPublicProtected)
            {

                string propertyName = context.genericID().GetText();
                publicProperties.Add(propertyName);
            }
        }

        public override void EnterIdentUserVariable(IdentUserVariableContext context)
        {
            var userVariable = context.USER_VARIABLE();
            string varName = userVariable.GetText().TrimStart('&');
            if (!inConstructor && publicProperties.Contains(varName))
            {
                if (Highlights != null)
                {
                    Highlights.Add(new CodeHighlight
                    {
                        Start = (uint)userVariable.Symbol.StartIndex,
                        Length = (uint)userVariable.Symbol.Text.Length,
                        Color = HighlightColor.Salmon
                    });
                }
            }
        }

        public override void Reset()
        {
            publicProperties.Clear();
        }
    }
}
