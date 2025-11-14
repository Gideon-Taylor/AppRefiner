using AppRefiner.Database;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Text;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips for Application Class paths, showing public/protected members.
    /// This is the self-hosted equivalent of the ANTLR-based AppClassTooltipProvider.
    /// </summary>
    public class AppClassTooltipProvider : BaseTooltipProvider
    {
        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public override string Name => "Application Class Details";

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public override string Description => "Shows public and protected members of an Application Class when hovering over its path.";

        /// <summary>
        /// Database connection is required to look up parent classes
        /// </summary>
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;

        private string extendedClassPath;

        /// <summary>
        /// Resets the internal state of the tooltip provider.
        /// </summary>
        /// 
        public override void Reset()
        {
            base.Reset();
        }

        public override void VisitAppClass(AppClassNode node)
        {
            if (node.BaseType != null)
            {
                if (node.BaseType is AppClassTypeNode classPath)
                {
                    extendedClassPath = classPath.QualifiedName;
                }
                else
                {
                    extendedClassPath = node.BaseType.ToString();
                }
            }
            base.VisitAppClass(node);
        }

        public override void VisitAppClassType(AppClassTypeNode node)
        {
            if (node.SourceSpan.ContainsPosition(CurrentPosition))
            {
                ProcessAppClassPath(node);
            }
            base.VisitAppClassType(node);
        }

        public override void VisitImport(ImportNode node)
        {
            base.VisitImport(node);
        }
        
        /// <summary>
        /// Processes an app class path identifier to generate tooltip information
        /// </summary>
        private void ProcessAppClassPath(AppClassTypeNode node)
        {
            if (DataManager == null) return;

            string hoveredClassPath = node.QualifiedName;

            ProgramNode? appClassProgram = null;
            
            // Parse the external class
            appClassProgram = ParseExternalClass(hoveredClassPath);
              
            

            bool showProtected = string.Equals(hoveredClassPath, extendedClassPath, StringComparison.OrdinalIgnoreCase);

            // Build the tooltip string
            string tooltipText = string.Empty;

            if (appClassProgram?.AppClass != null)
            {
                tooltipText = BuildTooltipForAppClass(appClassProgram.AppClass, showProtected);
            }

            // Register the tooltip for the identifier
            RegisterTooltip(node.SourceSpan, tooltipText);
        }

        private string BuildTooltipForAppClass(AppClassNode classNode, bool includeProtected)
        {
            var sb = new StringBuilder();
            var typeKind = classNode.IsInterface ? "Interface" : "Class";
            sb.AppendLine($"{typeKind}: {classNode.Name}");
            if (classNode.BaseType != null)
            {
                sb.AppendLine($"Extends: {classNode.BaseType.TypeName}");
            }
            sb.AppendLine("---");

            // Combine methods and properties for display
            var methodsToShow = classNode.Methods
                .Where(m => m.Visibility == VisibilityModifier.Public ||
                           includeProtected && m.Visibility == VisibilityModifier.Protected);

            var propertiesToShow = classNode.Properties
                .Where(p => p.Visibility == VisibilityModifier.Public ||
                           includeProtected && p.Visibility == VisibilityModifier.Protected);

            bool addedMembers = false;

            foreach (var method in methodsToShow.OrderBy(m => m.Name))
            {
                addedMembers = true;
                string args = string.Join(", ", method.Parameters.Select(p => $"{p.Type?.TypeName ?? "any"} {p.Name}"));
                string returns = method.ReturnType != null ? $": {method.ReturnType.TypeName}" : "";
                string abstractText = method.IsAbstract ? "Abstract " : "";
                sb.AppendLine($"({method.Visibility}) {abstractText}Method: {method.Name}({args}){returns}");
            }

            foreach (var prop in propertiesToShow.OrderBy(p => p.Name))
            {
                addedMembers = true;
                string accessors = (prop.HasGet ? "Get" : "") + (prop.HasSet ? prop.HasGet ? "/Set" : "Set" : "");
                if (prop.IsReadOnly) accessors = "ReadOnly";
                string typeName = prop.Type?.TypeName ?? "any";
                sb.AppendLine($"({prop.Visibility}) Property: {typeName} {prop.Name} {{{accessors}}}");
            }

            if (!addedMembers)
            {
                sb.AppendLine(includeProtected ? "(No public or protected members found)" : "(No public members found)");
            }

            return sb.ToString().Trim();
        }
    }
}
