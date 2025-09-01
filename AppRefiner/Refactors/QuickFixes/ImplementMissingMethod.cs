using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AppRefiner.Refactors.QuickFixes
{
    public class ImplementMissingMethod : BaseRefactor
    {
        public new static string RefactorName => "Implement Missing Method";
        public new static string RefactorDescription => "Generates a basic implementation for a method declared in the header";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => false;

        private AppClassNode? targetClass;
        private MethodNode? targetMethod;
        private MethodNode? baseMethodToOverride;
        private string? baseClassPath;
        
        public ImplementMissingMethod(ScintillaEditor editor) : base(editor)
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

            targetClass = node;
            
            // Find method declarations that don't have implementations
            var methodsNeedingImplementation = FindMethodsNeedingImplementation(node);
            
            if (methodsNeedingImplementation.Count == 0)
            {
                SetFailure("No methods found that need implementation");
                return;
            }

            // Find the method that contains the current cursor position
            targetMethod = FindTargetMethodByCursorPosition(methodsNeedingImplementation);
            
            if (targetMethod == null)
            {
                // If no method found at cursor, take the first one
                targetMethod = methodsNeedingImplementation.First();
            }
            
            // Check if this method exists in base class/interface for override annotation
            if (node.BaseClass != null && Editor.DataManager != null)
            {
                baseClassPath = node.BaseClass.TypeName;
                AnalyzeBaseClassForOverride(baseClassPath, targetMethod.Name);
            }
            
            // Generate the implementation now that we have everything we need
            GenerateMethodImplementation();
        }

        private void Reset()
        {
            targetClass = null;
            targetMethod = null;
            baseMethodToOverride = null;
            baseClassPath = null;
        }

        private MethodNode? FindTargetMethodByCursorPosition(List<MethodNode> methodsNeedingImplementation)
        {
            var currentPosition = CurrentPosition;
            
            foreach (var method in methodsNeedingImplementation)
            {
                if (method.SourceSpan.ContainsPosition(currentPosition))
                {
                    return method;
                }
            }
            
            return null;
        }

        private List<MethodNode> FindMethodsNeedingImplementation(AppClassNode classNode)
        {
            var methodsNeedingImpl = new List<MethodNode>();
            
            // Get all method declarations (methods without implementations)
            var declarations = classNode.Methods.Where(m => m.IsDeclaration).ToList();
            
            // Get all method implementations
            var implementations = classNode.Methods.Where(m => m.IsImplementation).ToList();
            
            foreach (var declaration in declarations)
            {
                // Skip constructors
                if (declaration.IsConstructor)
                    continue;
                
                // Check if there's already an implementation for this method
                bool hasImplementation = implementations.Any(impl => 
                    string.Equals(impl.Name, declaration.Name, StringComparison.OrdinalIgnoreCase));
                
                if (!hasImplementation)
                {
                    methodsNeedingImpl.Add(declaration);
                }
            }
            
            return methodsNeedingImpl;
        }

        private void AnalyzeBaseClassForOverride(string baseClassPath, string methodName)
        {
            if (Editor.DataManager == null)
                return;

            try
            {
                var baseClassSource = Editor.DataManager.GetAppClassSourceByPath(baseClassPath);
                if (string.IsNullOrEmpty(baseClassSource))
                    return;

                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(baseClassSource);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var baseProgram = parser.ParseProgram();

                if (baseProgram == null)
                    return;

                FindMethodInBaseClass(baseProgram, methodName);
            }
            catch (Exception)
            {
                // Silently handle errors - override annotation is optional
            }
        }

        private void FindMethodInBaseClass(ProgramNode baseProgram, string methodName)
        {
            MethodNode? baseMethod = null;

            if (baseProgram.AppClass != null)
            {
                baseMethod = baseProgram.AppClass.Methods.FirstOrDefault(m => 
                    string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase));
            }
            else if (baseProgram.Interface != null)
            {
                baseMethod = baseProgram.Interface.Methods.FirstOrDefault(m => 
                    string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase));
            }

            if (baseMethod != null)
            {
                baseMethodToOverride = baseMethod;
            }
        }


        private void GenerateMethodImplementation()
        {
            if (targetMethod == null || targetClass == null)
            {
                SetFailure("No target method or class identified");
                return;
            }

            var methodName = targetMethod.Name;
            var parameters = GenerateParameterInfo(targetMethod.Parameters);
            var overrideAnnotation = GenerateOverrideAnnotation();
            var parameterAnnotations = GenerateParameterAnnotations(parameters);
            var methodBody = GenerateMethodBody(targetMethod);

            var implementation = GenerateFullImplementation(methodName, overrideAnnotation, parameterAnnotations, methodBody);
            
            var insertPosition = FindImplementationInsertionPosition();
            if (insertPosition >= 0)
            {
                InsertText(insertPosition, implementation, $"Insert implementation for method '{methodName}'");
            }
            else
            {
                SetFailure("Could not determine where to insert method implementation");
            }
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

        private string GenerateOverrideAnnotation()
        {
            if (baseMethodToOverride != null && !string.IsNullOrEmpty(baseClassPath))
            {
                return $"   /+ Extends/implements {baseClassPath}.{baseMethodToOverride.Name} +/" + Environment.NewLine;
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
            var methodBody = $"{indent}throw CreateException(0, 0, \"Method '{method.Name}' not implemented.\");" + Environment.NewLine;
            
            // Add return statement if method has a return type
            if (method.ReturnType != null)
            {
                var defaultValue = GetDefaultValueForType(method.ReturnType.ToString());
                methodBody += $"{indent}Return {defaultValue};" + Environment.NewLine;
            }
            
            return methodBody;
        }

        private string GenerateFullImplementation(string methodName, string overrideAnnotation, string parameterAnnotations, string methodBody)
        {
            var implementation = $"method {methodName}" + Environment.NewLine +
                                overrideAnnotation +
                                parameterAnnotations +
                                methodBody +
                                "end-method;";
            
            return Environment.NewLine + Environment.NewLine + implementation;
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