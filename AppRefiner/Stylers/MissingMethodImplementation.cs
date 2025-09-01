using AppRefiner.Database;
using AppRefiner.Refactors.QuickFixes;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers
{
    public class MissingMethodImplementation : BaseStyler
    {
        private const uint WARNING_COLOR = 0x0000FFAA; // Orange color for missing implementation warnings

        public override string Description => "Missing method implementations";

        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.NotRequired;

        public override void VisitProgram(ProgramNode node)
        {
            Reset();
            base.VisitProgram(node);
        }

        public override void VisitAppClass(AppClassNode node)
        {
            CheckForMissingImplementations(node);
            base.VisitAppClass(node);
        }

        private void CheckForMissingImplementations(AppClassNode classNode)
        {
            // Get all method declarations (methods without implementations)
            var declarations = classNode.Methods.Where(m => m.IsDeclaration).ToList();

            // Get all method implementations
            var implementations = classNode.Methods.Where(m => m.IsImplementation).ToList();

            foreach (var declaration in declarations)
            {
                // Skip constructors - they have their own handling
                if (declaration.IsConstructor)
                    continue;

                // Check if there's already an implementation for this method
                bool hasImplementation = implementations.Any(impl =>
                    string.Equals(impl.Name, declaration.Name, StringComparison.OrdinalIgnoreCase));

                if (!hasImplementation)
                {
                    FlagMissingImplementation(declaration);
                }
            }
        }

        private void FlagMissingImplementation(MethodNode methodDeclaration)
        {
            string tooltip = $"Method '{methodDeclaration.Name}' is declared but not implemented.";
            var quickFixes = new List<(Type RefactorClass, string Description)>
            {
                (typeof(ImplementMissingMethod), "Implement missing method")
            };

            AddIndicator((methodDeclaration.NameToken.SourceSpan.Start.ByteIndex, methodDeclaration.NameToken.SourceSpan.End.ByteIndex), IndicatorType.SQUIGGLE, WARNING_COLOR, tooltip, quickFixes);
        }
    }
}