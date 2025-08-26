using AppRefiner.Database;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppRefiner.Stylers
{

    /// <summary>
    /// Styler that checks if an Application Class implements all abstract members
    /// from its base class or interfaces.
    /// </summary>
    public class UnimplementedAbstractMembersStyler : BaseStyler
    {
        private const uint WARNING_COLOR = 0xFF00A5FF; // Orange (BGRA) for unimplemented members warning

        public override string Description => "Missing abstract implementations";

        /// <summary>
        /// Specifies that this styler requires a database connection to resolve class hierarchies.
        /// </summary>
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;

        /// <summary>
        /// Processes the entire program and resets state
        /// </summary>
        public override void VisitProgram(ProgramNode node)
        {
            Reset();
            base.VisitProgram(node);
        }

        /// <summary>
        /// Check application classes for unimplemented abstract members
        /// </summary>
        public override void VisitAppClass(AppClassNode node)
        {
            // Only proceed if we have a database connection and either a base class or interface
            if (DataManager == null || (node.BaseClass == null && node.ImplementedInterface == null))
            {
                base.VisitAppClass(node);
                return;
            }

            try
            {
                // Get all unimplemented abstract members
                var (unimplementedMethods, unimplementedProperties) = GetAllUnimplementedAbstractMembers(node);

                // If there are no unimplemented members, we are done
                if (unimplementedMethods.Count == 0 && unimplementedProperties.Count == 0)
                {
                    base.VisitAppClass(node);
                    return;
                }

                // Determine what to highlight based on what the class extends/implements
                TypeNode? targetToHighlight = null;
                if (node.BaseClass != null)
                {
                    targetToHighlight = node.BaseClass;
                }
                else if (node.ImplementedInterface != null)
                {
                    targetToHighlight = node.ImplementedInterface;
                }

                // If we have a target to highlight, add the indicator
                if (targetToHighlight != null)
                {
                    // Build the tooltip message
                    var tooltipBuilder = new StringBuilder("Missing implementations:");
                    foreach (var method in unimplementedMethods)
                    {
                        tooltipBuilder.Append($"\n - Method: {method.Name}");
                    }
                    foreach (var prop in unimplementedProperties)
                    {
                        tooltipBuilder.Append($"\n - Property: {prop.Name}");
                    }

                    AddIndicator(targetToHighlight.SourceSpan, IndicatorType.SQUIGGLE, WARNING_COLOR, tooltipBuilder.ToString());
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - stylers should be resilient
                Console.Error.WriteLine($"Error in UnimplementedAbstractMembersStyler: {ex.Message}");
            }

            base.VisitAppClass(node);
        }

        /// <summary>
        /// Gets all unimplemented abstract members from base classes and interfaces
        /// </summary>
        private (List<MethodNode> UnimplementedMethods, List<PropertyNode> UnimplementedProperties) 
            GetAllUnimplementedAbstractMembers(AppClassNode node)
        {
            var abstractMethods = new Dictionary<string, MethodNode>();
            var abstractProperties = new Dictionary<string, PropertyNode>();
            var implementedSignatures = GetImplementedSignatures(node);

            // Collect from base class hierarchy
            if (node.BaseClass != null)
            {
                CollectAbstractMembers(node.BaseClass.TypeName, implementedSignatures, abstractMethods, abstractProperties);
            }

            // Collect from implemented interface hierarchy  
            if (node.ImplementedInterface != null)
            {
                CollectAbstractMembers(node.ImplementedInterface.TypeName, implementedSignatures, abstractMethods, abstractProperties);
            }

            return (abstractMethods.Values.ToList(), abstractProperties.Values.ToList());
        }

        /// <summary>
        /// Gets signatures of all concrete members implemented in a class
        /// </summary>
        private static HashSet<string> GetImplementedSignatures(AppClassNode node)
        {
            var signatures = new HashSet<string>();
            
            // Add concrete methods (excluding constructors)
            foreach (var method in node.Methods.Where(m => !m.IsAbstract && !IsConstructor(m, node.Name)))
                signatures.Add($"M:{method.Name}({method.Parameters.Count})");
                
            // Add concrete properties
            foreach (var property in node.Properties.Where(p => !p.IsAbstract))
                signatures.Add($"P:{property.Name}");
                
            return signatures;
        }

        /// <summary>
        /// Recursively collects abstract members from a class or interface hierarchy
        /// </summary>
        private void CollectAbstractMembers(string typePath, HashSet<string> implementedSignatures, 
            Dictionary<string, MethodNode> abstractMethods, Dictionary<string, PropertyNode> abstractProperties)
        {
            try
            {
                var program = ParseClassAst(typePath);
                if (program == null) return;

                var isInterface = program.Interface != null;
                var methods = isInterface ? program.Interface!.Methods : program.AppClass?.Methods;
                var properties = isInterface ? program.Interface!.Properties : program.AppClass?.Properties;
                
                if (methods == null && properties == null) return;

                // Process methods - all interface methods are abstract, only abstract class methods
                if (methods != null)
                {
                    foreach (var method in methods.Where(m => isInterface || m.IsAbstract))
                    {
                        string signature = $"M:{method.Name}({method.Parameters.Count})";
                        if (!implementedSignatures.Contains(signature))
                            abstractMethods.TryAdd(signature, method);
                    }
                }

                // Process properties - all interface properties are abstract, only abstract class properties
                if (properties != null)
                {
                    foreach (var property in properties.Where(p => isInterface || p.IsAbstract))
                    {
                        string signature = $"P:{property.Name}";
                        if (!implementedSignatures.Contains(signature))
                            abstractProperties.TryAdd(signature, property);
                    }
                }

                // Add concrete implementations to prevent propagation from parents (classes only)
                if (!isInterface && program.AppClass != null)
                {
                    foreach (var method in program.AppClass.Methods.Where(m => !m.IsAbstract && !IsConstructor(m, program.AppClass.Name)))
                        implementedSignatures.Add($"M:{method.Name}({method.Parameters.Count})");
                    foreach (var property in program.AppClass.Properties.Where(p => !p.IsAbstract))
                        implementedSignatures.Add($"P:{property.Name}");
                }

                // Recurse to parent
                string? parentPath = isInterface ? program.Interface?.BaseInterface?.TypeName : program.AppClass?.BaseClass?.TypeName;
                if (parentPath != null)
                {
                    CollectAbstractMembers(parentPath, implementedSignatures, abstractMethods, abstractProperties);
                }
            }
            catch (Exception)
            {
                // Silently handle parsing errors
            }
        }

        /// <summary>
        /// Parses a class or interface AST from its path using the self-hosted parser
        /// </summary>
        private ProgramNode? ParseClassAst(string classPath)
        {
            if (DataManager == null || string.IsNullOrEmpty(classPath))
                return null;

            try
            {
                // Get the source code from the database
                string? sourceCode = DataManager.GetAppClassSourceByPath(classPath);
                
                if (string.IsNullOrEmpty(sourceCode))
                    return null; // Class not found in database
                
                // Parse using the self-hosted parser
                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(sourceCode);
                var tokens = lexer.TokenizeAll();
                
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                return parser.ParseProgram();
            }
            catch (Exception)
            {
                // Silently handle database or parsing errors
                return null;
            }
        }

        /// <summary>
        /// Helper method to determine if a method is a constructor
        /// </summary>
        private static bool IsConstructor(MethodNode method, string className)
        {
            return string.Equals(method.Name, className, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reset state for reuse
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            // No additional state to clear in this implementation
        }
    }
}