using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using System;
using System.Collections.Generic;
using System.Linq;

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

        private void Reset()
        {
            targetClass = null;
            abstractMethods.Clear();
            abstractProperties.Clear();
            baseClassPath = null;
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


        private void GenerateAbstractMemberImplementations()
        {
            if (targetClass == null)
            {
                SetFailure("No target class identified");
                return;
            }

            // Generate method implementations
            foreach (var method in abstractMethods)
            {
                GenerateMethodImplementation(method);
                if (!GetResult().Success)
                    return;
            }

            // Generate property implementations
            foreach (var property in abstractProperties)
            {
                GeneratePropertyImplementation(property);
                if (!GetResult().Success)
                    return;
            }
        }

        private void GenerateMethodImplementation(MethodNode abstractMethod)
        {
            var methodHeader = GenerateMethodHeader(abstractMethod);
            var methodImplementation = GenerateMethodImplementationCode(abstractMethod);

            // Insert header in appropriate scope section
            InsertMethodHeader(methodHeader, abstractMethod.Visibility);
            
            if (GetResult().Success)
            {
                // Insert implementation after end-class
                InsertMethodImplementation(methodImplementation, abstractMethod.Name);
            }
        }

        private void GeneratePropertyImplementation(PropertyNode abstractProperty)
        {
            var propertyHeader = GeneratePropertyHeader(abstractProperty);

            // Insert header in appropriate scope section
            InsertPropertyHeader(propertyHeader, abstractProperty.Visibility);
        }

        private string GenerateMethodHeader(MethodNode method)
        {
            var parameters = GenerateParameterInfo(method.Parameters);
            var parameterList = string.Join(", ", parameters.Select(p => 
                $"{p.Name} As {p.Type}{(p.IsOut ? " out" : "")}"));
            
            var returnType = method.ReturnType != null ? $" returns {method.ReturnType}" : "";
            var overrideAnnotation = $" /* Implements {baseClassPath}.{method.Name} */";
            
            return $"   method {method.Name}({parameterList}){returnType};{overrideAnnotation}";
        }

        private string GeneratePropertyHeader(PropertyNode property)
        {
            var readonlyKeyword = property.IsReadOnly ? " readonly" : "";
            var overrideAnnotation = $" /* Implements {baseClassPath}.{property.Name} */";
            
            return $"   property {property.Type} {property.Name}{readonlyKeyword};{overrideAnnotation}";
        }

        private string GenerateMethodImplementationCode(MethodNode method)
        {
            var parameters = GenerateParameterInfo(method.Parameters);
            var overrideAnnotation = GenerateOverrideAnnotation(method.Name);
            var parameterAnnotations = GenerateParameterAnnotations(parameters);
            var methodBody = GenerateMethodBody(method);

            var implementation = $"method {method.Name}" + Environment.NewLine +
                                overrideAnnotation +
                                parameterAnnotations +
                                methodBody +
                                "end-method;";
            
            return Environment.NewLine + Environment.NewLine + implementation;
        }

        private List<(string Name, string Type, bool IsOut)> GenerateParameterInfo(List<ParameterNode> parameters)
        {
            var paramInfo = new List<(string Name, string Type, bool IsOut)>();
            
            foreach (var param in parameters)
            {
                var paramType = param.Type?.ToString() ?? "any";
                var isOut = param.IsOut;
                paramInfo.Add((param.Name, paramType, isOut));
            }
            
            return paramInfo;
        }

        private string GenerateOverrideAnnotation(string methodName)
        {
            if (!string.IsNullOrEmpty(baseClassPath))
            {
                return $"   /+ Extends/implements {baseClassPath}.{methodName} +/" + Environment.NewLine;
            }
            
            return string.Empty;
        }

        private string GenerateParameterAnnotations(List<(string Name, string Type, bool IsOut)> parameters)
        {
            if (parameters.Count == 0)
                return string.Empty;

            var annotations = new List<string>();
            foreach (var param in parameters)
            {
                var outModifier = param.IsOut ? " out" : "";
                annotations.Add($"   /+ &{param.Name} as {param.Type}{outModifier} +/");
            }
            
            return string.Join(Environment.NewLine, annotations) + Environment.NewLine;
        }

        private string GenerateMethodBody(MethodNode method)
        {
            var indent = "   ";
            var declaringType = !string.IsNullOrEmpty(baseClassPath) ? baseClassPath : "base class";
            var errorMessage = $"{declaringType}.{method.Name} not implemented for {targetClass!.Name}";
            
            var methodBody = $"{indent}throw CreateException(0, 0, \"{errorMessage}\");" + Environment.NewLine;
            
            // Add return statement if method has a return type
            if (method.ReturnType != null)
            {
                var defaultValue = GetDefaultValueForType(method.ReturnType.ToString());
                methodBody += $"{indent}Return {defaultValue};" + Environment.NewLine;
            }
            
            return methodBody;
        }

        private string GetDefaultValueForType(string typeName)
        {
            switch (typeName.ToLower())
            {
                case "boolean":
                    return "False";
                case "integer":
                case "number":
                case "float":
                    return "0";
                case "string":
                    return "\"\"";
                case "date":
                case "time":
                case "datetime":
                    return "Null";
                default:
                    return "Null";
            }
        }

        private void InsertMethodHeader(string methodHeader, VisibilityModifier visibility)
        {
            var insertPosition = FindHeaderInsertionPosition(visibility);
            
            if (insertPosition >= 0)
            {
                var headerWithNewline = methodHeader + Environment.NewLine;
                InsertText(insertPosition, headerWithNewline, 
                          $"Insert abstract method header");
            }
            else
            {
                SetFailure("Could not determine where to insert method header");
            }
        }

        private void InsertPropertyHeader(string propertyHeader, VisibilityModifier visibility)
        {
            var insertPosition = FindHeaderInsertionPosition(visibility);
            
            if (insertPosition >= 0)
            {
                var headerWithNewline = propertyHeader + Environment.NewLine;
                InsertText(insertPosition, headerWithNewline, 
                          $"Insert abstract property header");
            }
            else
            {
                SetFailure("Could not determine where to insert property header");
            }
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

        private int FindHeaderInsertionPosition(VisibilityModifier visibility)
        {
            if (targetClass == null) 
                return -1;

            // For now, use a simple approach: find the first method/property and insert there
            // or insert before end-class if no members exist
            var firstMember = targetClass.Methods.Concat<AstNode>(targetClass.Properties)
                .OrderBy(m => m.SourceSpan.Start.ByteIndex)
                .FirstOrDefault();
            
            if (firstMember != null)
            {
                return firstMember.SourceSpan.Start.ByteIndex;
            }

            // Fallback: insert before end-class
            return targetClass.SourceSpan.End.ByteIndex;
        }

        private int FindImplementationInsertionPosition()
        {
            if (targetClass == null) 
                return -1;

            var lastImplementation = targetClass.Methods
                .Where(m => m.IsImplementation)
                .OrderBy(m => m.SourceSpan.End.ByteIndex)
                .LastOrDefault();

            if (lastImplementation != null)
            {
                return lastImplementation.SourceSpan.End.ByteIndex + 1;
            }

            return targetClass.SourceSpan.End.ByteIndex + 1;
        }
    }
}