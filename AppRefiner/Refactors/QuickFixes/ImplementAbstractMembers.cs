using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Text;

namespace AppRefiner.Refactors.QuickFixes
{
    public class ImplementAbstractMembers : BaseRefactor
    {
        public new static string RefactorName => "Implement Abstract Members";
        public new static string RefactorDescription => "Generates implementations for abstract methods and properties from base class/interface";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => false;

        private AppClassNode? targetClass;
        private List<MethodNode> abstractMethods = new();
        private List<PropertyNode> abstractProperties = new();
        private string? baseClassPath;

        /// <summary>
        /// Represents a group of members for a specific visibility level
        /// </summary>
        private class VisibilityMemberGroup
        {
            public List<MethodNode> Methods { get; } = new();
            public List<PropertyNode> Properties { get; } = new();
        }

        public ImplementAbstractMembers(ScintillaEditor editor) : base(editor)
        {
        }

        public override void VisitProgram(ProgramNode node)
        {
            Reset();
            base.VisitProgram(node);
        }

        public override void VisitAppClass(AppClassNode node)
        {
            base.VisitAppClass(node);

            if (targetClass != null)
                return;

            if (node.BaseClass == null && node.ImplementedInterface == null)
            {
                SetFailure("Class does not extend another class or implement an interface");
                return;
            }

            targetClass = node;

            if (Editor.DataManager != null)
            {
                // Check base class hierarchy first
                if (node.BaseClass != null)
                {
                    baseClassPath = node.BaseClass.TypeName;
                    AnalyzeBaseClassForAbstractMembers(baseClassPath);
                }

                // Also check implemented interface hierarchy
                if (node.ImplementedInterface != null)
                {
                    var interfacePath = node.ImplementedInterface.TypeName;
                    AnalyzeBaseClassForAbstractMembers(interfacePath);
                }

                if (abstractMethods.Count == 0 && abstractProperties.Count == 0)
                {
                    SetFailure("No abstract members found that need implementation");
                    return;
                }

                GenerateAbstractMemberImplementations();
            }
            else
            {
                SetFailure("No data manager available");
            }
        }

        private new void Reset()
        {
            targetClass = null;
            abstractMethods.Clear();
            abstractProperties.Clear();
            baseClassPath = null;
            base.Reset();
        }

        private void AnalyzeBaseClassForAbstractMembers(string baseClassPath)
        {
            if (Editor.DataManager == null || targetClass == null)
                return;

            try
            {
                // Use the same logic as UnimplementedAbstractMembersStyler for consistency
                var abstractMethodsDict = new Dictionary<string, MethodNode>();
                var abstractPropertiesDict = new Dictionary<string, PropertyNode>();
                var implementedSignatures = GetImplementedSignatures(targetClass);

                // Recursively collect abstract members from the hierarchy
                CollectAbstractMembers(baseClassPath, implementedSignatures, abstractMethodsDict, abstractPropertiesDict);

                // Convert dictionaries to lists
                abstractMethods.AddRange(abstractMethodsDict.Values);
                abstractProperties.AddRange(abstractPropertiesDict.Values);
            }
            catch (Exception ex)
            {
                SetFailure($"Error analyzing base class hierarchy: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets signatures of all concrete members implemented in a class (same logic as styler)
        /// </summary>
        private HashSet<string> GetImplementedSignatures(AppClassNode node)
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
        /// Recursively collects abstract members from a class or interface hierarchy (same logic as styler)
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
        /// Parses a class or interface AST from its path using the self-hosted parser (same as styler)
        /// </summary>
        private ProgramNode? ParseClassAst(string classPath)
        {
            if (Editor.DataManager == null || string.IsNullOrEmpty(classPath))
                return null;

            try
            {
                // Get the source code from the database
                string? sourceCode = Editor.DataManager.GetAppClassSourceByPath(classPath);

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
        /// Helper method to determine if a method is a constructor (same as styler)
        /// </summary>
        private bool IsConstructor(MethodNode method, string className)
        {
            return string.Equals(method.Name, className, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Groups abstract methods and properties by their visibility modifier
        /// </summary>
        private Dictionary<VisibilityModifier, VisibilityMemberGroup> GroupMembersByVisibility()
        {
            var groups = new Dictionary<VisibilityModifier, VisibilityMemberGroup>();

            // Group methods by visibility
            foreach (var method in abstractMethods)
            {
                // Debug: Log method visibility  
                Debug.Log($"Method {method.Name} has visibility: {method.Visibility}");
                
                if (!groups.ContainsKey(method.Visibility))
                    groups[method.Visibility] = new VisibilityMemberGroup();
                groups[method.Visibility].Methods.Add(method);
            }

            // Group properties by visibility
            foreach (var property in abstractProperties)
            {
                // Debug: Log property visibility
                Debug.Log($"Property {property.Name} has visibility: {property.Visibility}");
                
                if (!groups.ContainsKey(property.Visibility))
                    groups[property.Visibility] = new VisibilityMemberGroup();
                groups[property.Visibility].Properties.Add(property);
            }

            return groups;
        }

        /// <summary>
        /// Generates and inserts all headers for a specific visibility section
        /// </summary>
        private void GenerateVisibilitySectionEdits(VisibilityModifier visibility, VisibilityMemberGroup memberGroup)
        {
            if (targetClass == null)
            {
                SetFailure("No target class identified");
                return;
            }

            var (insertPosition, needsSectionHeader) = FindHeaderInsertionPositionForVisibility(visibility);
            if (insertPosition < 0)
            {
                SetFailure($"Could not determine where to insert {visibility.ToString().ToLower()} members");
                return;
            }

            var sectionText = new StringBuilder();

            // Add section header if needed
            if (needsSectionHeader)
            {
                sectionText.AppendLine(visibility.ToString().ToLower());
            }

            // Add all method headers
            foreach (var method in memberGroup.Methods)
            {
                var implementsComment = !string.IsNullOrEmpty(baseClassPath) ? $"{baseClassPath}.{method.Name}" : null;
                var methodHeader = method.GenerateHeader(implementsComment);
                sectionText.AppendLine(methodHeader);
            }

            // Add all property headers
            foreach (var property in memberGroup.Properties)
            {
                var propertyHeader = GeneratePropertyHeader(property);
                sectionText.AppendLine(propertyHeader);
            }

            // Insert the entire section as a single edit
            var description = $"Insert {visibility.ToString().ToLower()} abstract member headers";
            if (needsSectionHeader)
                description += $" with {visibility.ToString().ToLower()} section header";

            InsertText(insertPosition, sectionText.ToString(), description);
        }


        private void GenerateAbstractMemberImplementations()
        {
            if (targetClass == null)
            {
                SetFailure("No target class identified");
                return;
            }

            // Group members by visibility modifier
            var memberGroups = GroupMembersByVisibility();

            // Process visibility sections in PeopleCode order: Public → Protected → Private
            var visibilityOrder = new[] { VisibilityModifier.Public, VisibilityModifier.Protected, VisibilityModifier.Private };
            
            foreach (var visibility in visibilityOrder.Reverse<VisibilityModifier>())
            {
                if (memberGroups.ContainsKey(visibility) && 
                    (memberGroups[visibility].Methods.Count > 0 || memberGroups[visibility].Properties.Count > 0))
                {
                    GenerateVisibilitySectionEdits(visibility, memberGroups[visibility]);
                    if (!GetResult().Success)
                        return;
                }
            }

            // Generate method implementations (these go after end-class)
            foreach (var method in abstractMethods.Reverse<MethodNode>())
            {
                var options = new MethodImplementationOptions
                {
                    Type = ImplementationType.Abstract,
                    ImplementsComment = !string.IsNullOrEmpty(baseClassPath) ? $"{baseClassPath}.{method.Name}" : null,
                    BaseClassPath = baseClassPath,
                    TargetClassName = targetClass.Name
                };
                var implementation = method.GenerateDefaultImplementation(options);
                InsertMethodImplementation(implementation, method.Name);
                if (!GetResult().Success)
                    return;
            }
        }



        private string GeneratePropertyHeader(PropertyNode property)
        {
            var readonlyKeyword = property.IsReadOnly ? " readonly" : "";
            var overrideAnnotation = $" /* Implements {baseClassPath}.{property.Name} */";

            return $"   property {property.Type} {property.Name}{readonlyKeyword};{overrideAnnotation}";
        }




        private void InsertMethodImplementation(string implementation, string methodName)
        {
            var insertPosition = FindImplementationInsertionPosition();

            if (insertPosition >= 0)
            {
                InsertText(insertPosition, implementation,
                          $"Insert implementation for abstract method '{methodName}'");
            }
            else
            {
                SetFailure("Could not determine where to insert method implementation");
            }
        }

        private (int insertPosition,bool needsSectionHeader) FindHeaderInsertionPositionForVisibility(VisibilityModifier visibility)
        {
            if (targetClass == null)
                return (-1,false);

            var insertLine = 0;
            var needsHeader = false;

            // Check if we have an empty class (no members in any section)
            var hasAnyMembers = targetClass.VisibilitySections.Values.Any(section => section.Count > 0);

            switch (visibility)
            {
                case VisibilityModifier.Public:
                    if (targetClass.VisibilitySections[VisibilityModifier.Public].Count > 0)
                    {
                        // Insert after last public member
                        insertLine = targetClass.VisibilitySections[VisibilityModifier.Public].Last().SourceSpan.End.Line + 1;
                    }
                    else
                    {
                        // Insert before protected section, private section, or end-class
                        if (targetClass.ProtectedToken != null)
                        {
                            insertLine = targetClass.ProtectedToken.SourceSpan.Start.Line;
                        }
                        else if (targetClass.PrivateToken != null)
                        {
                            insertLine = targetClass.PrivateToken.SourceSpan.Start.Line;
                        }
                        else
                        {
                            insertLine = FindEndClassInsertionLine();
                        }
                    }
                    break;

                case VisibilityModifier.Protected:
                    needsHeader = (targetClass.ProtectedToken == null);

                    if (targetClass.VisibilitySections[VisibilityModifier.Protected].Count > 0)
                    {
                        // Insert after last protected member
                        insertLine = targetClass.VisibilitySections[VisibilityModifier.Protected].Last().SourceSpan.End.Line + 1;
                    }
                    else
                    {
                        // Insert before private section or end-class
                        if (targetClass.PrivateToken != null)
                        {
                            insertLine = targetClass.PrivateToken.SourceSpan.Start.Line;
                        }
                        else
                        {
                            insertLine = FindEndClassInsertionLine();
                            // we +1 here because the general rule is to insert above the line we locate
                            // but in an empty class this causes the "line above" end-class to be 'class' so we insert the method
                            // above the class by mistake.
                        }
                    }
                    break;

                case VisibilityModifier.Private:
                    needsHeader = (targetClass.PrivateToken == null);

                    if (targetClass.VisibilitySections[VisibilityModifier.Private].Count > 0)
                    {
                        // Insert after last private member
                        insertLine = targetClass.VisibilitySections[VisibilityModifier.Private].Last().SourceSpan.End.Line + 1;
                    }
                    else
                    {
                        // Insert before end-class
                        insertLine = FindEndClassInsertionLine();
                    }
                    break;
            }

            var insertPosition = ScintillaManager.GetLineStartIndex(Editor, insertLine - 1);
            return (insertPosition, needsHeader);
        }

        /// <summary>
        /// Finds the appropriate line number for inserting content before the end-class statement.
        /// Handles cases where LastToken might not be set correctly for empty classes.
        /// </summary>
        private int FindEndClassInsertionLine()
        {
            if (targetClass == null)
                return -1;

            // First try to use LastToken if it's available and seems valid
            if (targetClass.LastToken != null && targetClass.LastToken.SourceSpan.Start.Line > 0)
            {
                return targetClass.LastToken.SourceSpan.Start.Line;
            }

            // Fallback: Use the class's overall SourceSpan end
            if (targetClass.SourceSpan.End.Line > 0)
            {
                // Insert just before the end of the class span
                return targetClass.SourceSpan.End.Line;
            }

            // Final fallback: Use the class name token position + reasonable offset
            if (targetClass.NameToken != null)
            {
                // Assume end-class is a few lines after the class declaration
                return targetClass.NameToken.SourceSpan.Start.Line + 2;
            }

            // If all else fails, return an error
            return -1;
        }

        private int FindImplementationInsertionPosition()
        {
            if (targetClass == null)
                return -1;

            var lastImplementation = targetClass.Methods
                .Where(m => m.IsImplementation)
                .OrderBy(m => m.SourceSpan.End.ByteIndex)
                .LastOrDefault();

            if (lastImplementation != null && lastImplementation.Implementation != null)
            {
                return lastImplementation.Implementation.SourceSpan.End.ByteIndex + 1;
            }

            // For empty classes or classes without implementations, insert after the class definition
            // Use a more robust approach to find the end of the class
            if (targetClass.SourceSpan.End.ByteIndex > 0)
            {
                return targetClass.SourceSpan.End.ByteIndex + 1;
            }

            // Fallback: Use LastToken if available
            if (targetClass.LastToken != null)
            {
                return targetClass.LastToken.SourceSpan.End.ByteIndex + 1;
            }

            // Final fallback: Use a reasonable position after the class name
            if (targetClass.NameToken != null)
            {
                return targetClass.NameToken.SourceSpan.End.ByteIndex + 100; // Rough estimate
            }

            return -1;
        }
    }
}