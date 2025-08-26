using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using AppRefiner.Ast; // Assuming AST classes are here
using AppRefiner.Database;
using AppRefiner.PeopleCode;
using AppRefiner.Services; // For AstService

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips for Application Class paths, showing public/protected members.
    /// </summary>
    public class AppClassTooltipProvider : ParseTreeTooltipProvider
    {
        private AstService _astService;

        public override string Name => "Application Class Details";
        public override string Description => "Shows public and protected members of an Application Class when hovering over its path.";
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;
        // We don't rely on specific token types, but rather the parser rule context.
        public override int[]? TokenTypes => null; 

        private string extendedClassPath = string.Empty;

        public AppClassTooltipProvider()
        {
        }

        
        public override void Reset()
        {
            base.Reset();
        }

        public override void EnterProgram([NotNull] PeopleCode.PeopleCodeParser.ProgramContext context)
        {
            _astService = new AstService(DataManager);
            base.EnterProgram(context);

            var parsedProgram = AppRefiner.Ast.Program.Parse(context, "", DataManager);
            if (parsedProgram.ContainedInterface != null)
            {
                if (parsedProgram.ContainedInterface.ExtendedInterface != null)
                {
                    extendedClassPath = parsedProgram.ContainedInterface.ExtendedInterface.FullPath;
                }
            }
            else if (parsedProgram.ContainedAppClass != null)
            {
                if (parsedProgram.ContainedAppClass.ExtendedClass != null)
                {
                    extendedClassPath = parsedProgram.ContainedAppClass.ExtendedClass.FullPath;
                }
            }
        }


        private Dictionary<string, Ast.Program> _programPathToAst = new Dictionary<string, Ast.Program>();

        /// <summary>
        /// Called when the parser enters an appClassPath rule.
        /// Generates a tooltip with class member information.
        /// </summary>
        public override void EnterAppClassPath(PeopleCode.PeopleCodeParser.AppClassPathContext context)
        {
            base.EnterAppClassPath(context);

            if (context == null || DataManager == null) return;

            // Reconstruct the full class path from the parse tree context.
            string hoveredClassPath = context.GetText();

            Ast.Program? appClassProgram = null;
            if (_programPathToAst.TryGetValue(hoveredClassPath, out Ast.Program? programAst)){
                appClassProgram = programAst;
            }
            else{
                var hoveredProgramAst = _astService.GetProgramAst(hoveredClassPath);

                
                _programPathToAst[hoveredClassPath] = hoveredProgramAst;
                appClassProgram = hoveredProgramAst;
            }

            bool showProtected = (hoveredClassPath == extendedClassPath);

            // Build the tooltip string
            string tooltipText = string.Empty;
            
            if (appClassProgram?.ContainedInterface != null)
            {
                tooltipText = BuildTooltipForInterface(appClassProgram.ContainedInterface, showProtected);
            }
            else if (appClassProgram?.ContainedAppClass != null)
            {
                tooltipText = BuildTooltipForClass(appClassProgram.ContainedAppClass, showProtected);
            }

            // Register the tooltip for the range covered by the appClassPath context
            RegisterTooltip(context, tooltipText);

        }


        private string BuildTooltipForInterface(Interface intfcAst, bool includeProtected)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Interface: {intfcAst.FullPath}");
            if (!string.IsNullOrEmpty(intfcAst.ExtendedInterface?.FullPath))
            {
                sb.AppendLine($"Extends: {intfcAst.ExtendedInterface.FullPath}");
            }

            sb.AppendLine("---");

            // Combine methods and properties for display
            var methodsToShow = intfcAst.Methods
                .Where(m => m.Scope == Ast.Scope.Public || (includeProtected && m.Scope == Ast.Scope.Protected));

            var propertiesToShow = intfcAst.Properties
                .Where(p => p.Scope == Ast.Scope.Public || (includeProtected && p.Scope == Ast.Scope.Protected));

            bool addedMembers = false;

            foreach (var method in methodsToShow.OrderBy(m => m.Name))
            {
                addedMembers = true;
                string args = string.Join(", ", method.Parameters.Select(p => $"{p.Type.Name} {p.Name}")); // Use Type.Name
                string returns = method.ReturnType != null ? $": {method.ReturnType.Name}" : ""; // Use Type.Name
                sb.AppendLine($"({method.Scope}) {(method.IsAbstract? "Abstract ": "")}Method: {method.Name}({args}){returns}");
            }

            foreach (var prop in propertiesToShow.OrderBy(p => p.Name))
            {
                addedMembers = true;
                string accessors = (prop.HasGetter ? "Get" : "") + (prop.HasSetter ? (prop.HasGetter ? "/Set" : "Set") : "");
                if (prop.IsReadonly) accessors = "ReadOnly"; // Corrected property name
                sb.AppendLine($"({prop.Scope}) Property: {prop.Type.Name} {prop.Name} {{{accessors}}}"); // Use Type.Name
            }

            if (!addedMembers)
            {
                sb.AppendLine(includeProtected ? "(No public or protected members found)" : "(No public members found)");
            }

            return sb.ToString().Trim();
        }

        private string BuildTooltipForClass(AppClass classAst, bool includeProtected)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Class: {classAst.FullPath}");
            if (!string.IsNullOrEmpty(classAst.ExtendedClass?.FullPath))
            {
                sb.AppendLine($"Extends: {classAst.ExtendedClass.FullPath}");
            }
            if (!string.IsNullOrEmpty(classAst.ImplementedInterface?.FullPath))
            {
                sb.AppendLine($"Implements: {classAst.ImplementedInterface.FullPath}");
            }
            sb.AppendLine("---");

            // Combine methods and properties for display
            var methodsToShow = classAst.Methods
                .Where(m => m.Scope == Ast.Scope.Public || (includeProtected && m.Scope == Ast.Scope.Protected));
                
            var propertiesToShow = classAst.Properties
                .Where(p => p.Scope == Ast.Scope.Public || (includeProtected && p.Scope == Ast.Scope.Protected));

            bool addedMembers = false;

            foreach (var method in methodsToShow.OrderBy(m => m.Name))
            {
                addedMembers = true;
                string args = string.Join(", ", method.Parameters.Select(p => $"{p.Type.Name} {p.Name}")); // Use Type.Name
                string returns = method.ReturnType != null ? $": {method.ReturnType.Name}" : ""; // Use Type.Name
                sb.AppendLine($"({method.Scope}) {(method.IsAbstract ? "Abstract " : "")}Method: {method.Name}({args}){returns}");
            }

            foreach (var prop in propertiesToShow.OrderBy(p => p.Name))
            {
                addedMembers = true;
                string accessors = (prop.HasGetter ? "Get" : "") + (prop.HasSetter ? (prop.HasGetter ? "/Set" : "Set") : "");
                if (prop.IsReadonly) accessors = "ReadOnly"; // Corrected property name
                sb.AppendLine($"({prop.Scope}) Property: {prop.Type.Name} {prop.Name} {{{accessors}}}"); // Use Type.Name
            }

            if (!addedMembers)
            {
                 sb.AppendLine(includeProtected ? "(No public or protected members found)" : "(No public members found)");
            }

            return sb.ToString().Trim();
        }
    }
} 