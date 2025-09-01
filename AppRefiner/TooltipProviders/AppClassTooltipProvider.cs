using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips for Application Class paths, showing public/protected members.
    /// This is the self-hosted equivalent of the ANTLR-based AppClassTooltipProvider.
    /// </summary>
    public class AppClassTooltipProvider : AstTooltipProvider
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

        private string extendedClassPath = string.Empty;
        private Dictionary<string, ProgramNode> _programPathToAst = new Dictionary<string, ProgramNode>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Resets the internal state of the tooltip provider.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _programPathToAst.Clear();
            extendedClassPath = string.Empty;
        }

        /// <summary>
        /// Processes the AST to find app class references and register tooltips
        /// </summary>
        public override void ProcessProgram(ProgramNode program)
        {
            // Capture the extended class path if present
            if (program.Interface != null)
            {
                if (program.Interface.BaseInterface != null)
                {
                    extendedClassPath = program.Interface.BaseInterface.TypeName;
                }
            }
            else if (program.AppClass != null)
            {
                if (program.AppClass.BaseClass != null)
                {
                    extendedClassPath = program.AppClass.BaseClass.TypeName;
                }
            }

            base.ProcessProgram(program);
        }

        /// <summary>
        /// Override to process identifiers that might be app class paths
        /// </summary>
        public override void VisitIdentifier(IdentifierNode node)
        {
            // Check if this identifier represents an app class path (contains colons)
            if (node.IdentifierType == IdentifierType.Generic && IsAppClassPath(node.Name))
            {
                ProcessAppClassPath(node);
            }

            base.VisitIdentifier(node);
        }

        /// <summary>
        /// Checks if a string represents an Application Class path (contains colon characters)
        /// </summary>
        private static bool IsAppClassPath(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Contains(':');
        }

        /// <summary>
        /// Processes an app class path identifier to generate tooltip information
        /// </summary>
        private void ProcessAppClassPath(IdentifierNode node)
        {
            if (DataManager == null) return;

            string hoveredClassPath = node.Name;

            ProgramNode? appClassProgram = null;
            if (_programPathToAst.TryGetValue(hoveredClassPath, out ProgramNode? programAst))
            {
                appClassProgram = programAst;
            }
            else
            {
                // Parse the external class
                appClassProgram = ParseExternalClass(hoveredClassPath);
                if (appClassProgram != null)
                {
                    _programPathToAst[hoveredClassPath] = appClassProgram;
                }
            }

            bool showProtected = string.Equals(hoveredClassPath, extendedClassPath, StringComparison.OrdinalIgnoreCase);

            // Build the tooltip string
            string tooltipText = string.Empty;

            if (appClassProgram?.Interface != null)
            {
                tooltipText = BuildTooltipForInterface(appClassProgram.Interface, showProtected);
            }
            else if (appClassProgram?.AppClass != null)
            {
                tooltipText = BuildTooltipForClass(appClassProgram.AppClass, showProtected);
            }

            // Register the tooltip for the identifier
            RegisterTooltip(node.SourceSpan, tooltipText);
        }

        private string BuildTooltipForInterface(InterfaceNode intfc, bool includeProtected)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Interface: {intfc.Name}");
            if (intfc.BaseInterface != null)
            {
                sb.AppendLine($"Extends: {intfc.BaseInterface.TypeName}");
            }

            sb.AppendLine("---");

            // Combine methods and properties for display
            var methodsToShow = intfc.Methods
                .Where(m => m.Visibility == VisibilityModifier.Public ||
                           includeProtected && m.Visibility == VisibilityModifier.Protected);

            var propertiesToShow = intfc.Properties
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
                string accessors = (prop.HasGet ? "Get" : "") + (prop.HasSetter ? prop.HasGet ? "/Set" : "Set" : "");
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

        private string BuildTooltipForClass(AppClassNode classNode, bool includeProtected)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Class: {classNode.Name}");
            if (classNode.BaseClass != null)
            {
                sb.AppendLine($"Extends: {classNode.BaseClass.TypeName}");
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
                string accessors = (prop.HasGet ? "Get" : "") + (prop.HasSetter ? prop.HasGet ? "/Set" : "Set" : "");
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
