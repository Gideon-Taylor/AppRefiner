using AppRefiner.Database;
using PeopleCodeParser.SelfHosted.Nodes;
using System;
using System.Linq;

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Styler that highlights methods declared in class headers but not implemented.
    /// Detects method declarations where Body == null, indicating missing implementation.
    /// </summary>
    public class MissingMethodImplementationStyler : BaseStyler
    {
        private const uint WARNING_COLOR = 0xFF00A5FF; // Orange (BGRA) for missing implementation warning

        public override string Description => "Methods declared but not implemented";

        /// <summary>
        /// This styler doesn't require database access - works purely with AST
        /// </summary>
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.NotRequired;

        /// <summary>
        /// Processes the entire program and resets state
        /// </summary>
        public override void VisitProgram(ProgramNode node)
        {
            Reset();
            base.VisitProgram(node);
        }

        /// <summary>
        /// Check application classes for method declarations without implementations
        /// </summary>
        public override void VisitAppClass(AppClassNode node)
        {
            // Find declared methods without implementations (excluding constructors)
            var unimplementedMethods = node.Methods
                .Where(m => m.Body == null && !IsConstructor(m, node.Name));

            // Add indicators for missing implementations
            foreach (var method in unimplementedMethods)
            {
                string tooltip = $"Method '{method.Name}' is declared but not implemented.";
                AddIndicator(method.SourceSpan, IndicatorType.SQUIGGLE, WARNING_COLOR, tooltip);
            }

            base.VisitAppClass(node);
        }

        /// <summary>
        /// Helper method to determine if a method is a constructor
        /// </summary>
        private static bool IsConstructor(MethodNode method, string className)
        {
            return string.Equals(method.Name, className, StringComparison.OrdinalIgnoreCase);
        }
    }
}