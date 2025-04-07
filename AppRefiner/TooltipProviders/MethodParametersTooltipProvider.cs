using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using AppRefiner.Database;
using AppRefiner.Linters.Models;
using AppRefiner.PeopleCode;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips showing method parameter information when hovering over method calls.
    /// Specifically focuses on %This.Method() calls and &variable.Method() calls with application class types,
    /// and attempts to find the method definition within the class or its inheritance chain.
    /// </summary>
    public class MethodParametersTooltipProvider : ScopedTooltipProvider
    {
        private Dictionary<string, MethodInfo> methodData = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
        private string? superClassName;
        private Dictionary<string, Dictionary<string, MethodInfo>> inheritanceChainMethods = 
            new Dictionary<string, Dictionary<string, MethodInfo>>(StringComparer.OrdinalIgnoreCase);
        private bool hasProcessedInheritanceChain = false;
        
        // Track current access modifier context (for class members)
        private string currentAccessModifier = string.Empty;

        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public override string Name => "Method Parameters";

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public override string Description => "Shows method parameter information on method calls";

        /// <summary>
        /// Medium priority
        /// </summary>
        public override int Priority => 50;

        /// <summary>
        /// Database connection is required to look up parent classes
        /// </summary>
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        /// <summary>
        /// Specifies which token types this provider is interested in.
        /// </summary>
        public override int[]? TokenTypes => new int[] 
        { 
            PeopleCodeLexer.GENERIC_ID_LIMITED, // For method names in %This.Method() calls
            PeopleCodeLexer.SYSTEM_VARIABLE,    // For %This
            PeopleCodeLexer.USER_VARIABLE       // For &variable in &variable.Method() calls
        };

        /// <summary>
        /// Structure to store method information
        /// </summary>
        private class MethodInfo
        {
            public string Name { get; set; } = string.Empty;
            public string ReturnType { get; set; } = string.Empty;
            public bool IsAbstract { get; set; } = false;
            public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
            public string AccessModifier { get; set; } = string.Empty;
            
            public override string ToString()
            {
                var sb = new StringBuilder();
                
                // Method name
                sb.AppendLine($"Method: {Name}");
                
                // Access modifier
                if (!string.IsNullOrEmpty(AccessModifier))
                {
                    sb.AppendLine($"Access: {AccessModifier}");
                }
                
                // Abstract indicator
                if (IsAbstract)
                {
                    sb.AppendLine("Abstract Method");
                }
                
                // Return type
                if (!string.IsNullOrEmpty(ReturnType))
                {
                    sb.AppendLine($"Returns: {FormatArrayType(ReturnType)}");
                }
                
                // Parameters
                if (Parameters.Count > 0)
                {
                    sb.AppendLine("Parameters:");
                    foreach (var param in Parameters)
                    {
                        sb.AppendLine($"   {param}");
                    }
                }
                else
                {
                    sb.AppendLine("Parameters: None");
                }
                
                return sb.ToString().TrimEnd();
            }
        }
        
        /// <summary>
        /// Structure to store parameter information
        /// </summary>
        private class ParameterInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public bool IsOut { get; set; } = false;
            
            public override string ToString()
            {
                var result = Name;
                
                if (!string.IsNullOrEmpty(Type))
                {
                    result += " as " + FormatArrayType(Type);
                }
                
                if (IsOut)
                {
                    result += " out";
                }
                
                return result;
            }
        }

        /// <summary>
        /// Formats array type strings to be more readable.
        /// Converts "arrayofarrayofstring" to "Array2 of String".
        /// </summary>
        /// <param name="type">The original type string</param>
        /// <returns>A formatted type string</returns>
        private static string FormatArrayType(string type)
        {
            if (string.IsNullOrEmpty(type))
                return type;
                
            // Check if it's an array type
            string lowerType = type.ToLowerInvariant();
            if (!lowerType.StartsWith("array"))
                return type; // Not an array type, return as is
                
            // Count occurrences of "arrayof"
            int arrayCount = 0;
            
            // Use regex to count array of occurrences
            string pattern = @"(array\s*of\s*)";
            var matches = Regex.Matches(lowerType, pattern, RegexOptions.IgnoreCase);
            arrayCount = matches.Count;
            
            if (arrayCount == 0)
            {
                // If no "array of" patterns were found, it's just a simple array
                arrayCount = 1;
                
                // Extract the base type by removing the array part
                string baseType = Regex.Replace(type, @"^array\s*of\s*", "", RegexOptions.IgnoreCase);
                if (string.IsNullOrEmpty(baseType))
                    return "Array";
                    
                // Check if there are additional arrays
                if (baseType.ToLowerInvariant().StartsWith("array"))
                {
                    // This is a nested array, format it recursively
                    string nestedFormat = FormatArrayType(baseType);
                    return "Array of " + nestedFormat;
                }
                
                return "Array of " + baseType;
            }
            else
            {
                // Find the base type after all the array patterns
                int lastIndex = 0;
                foreach (Match match in matches)
                {
                    lastIndex = match.Index + match.Length;
                }
                
                string baseType = type.Substring(lastIndex);
                
                // Use Array1, Array2, Array3 notation
                return $"Array{arrayCount + 1} of {baseType}";
            }
        }

        /// <summary>
        /// Resets the internal state of the tooltip provider.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            methodData.Clear();
            inheritanceChainMethods.Clear();
            hasProcessedInheritanceChain = false;
            superClassName = null;
            currentAccessModifier = string.Empty;
        }
        
        /// <summary>
        /// Captures access modifier for public section
        /// </summary>
        public override void EnterPublicHeader([NotNull] PeopleCodeParser.PublicHeaderContext context)
        {
            currentAccessModifier = "Public";
        }
        
        /// <summary>
        /// Captures access modifier for protected section
        /// </summary>
        public override void EnterProtectedHeader([NotNull] PeopleCodeParser.ProtectedHeaderContext context)
        {
            currentAccessModifier = "Protected";
        }
        
        /// <summary>
        /// Captures access modifier for private section
        /// </summary>
        public override void EnterPrivateHeader([NotNull] PeopleCodeParser.PrivateHeaderContext context)
        {
            currentAccessModifier = "Private";
        }
        
        /// <summary>
        /// Captures the superclass name if present
        /// </summary>
        public override void EnterClassDeclarationExtension([NotNull] PeopleCodeParser.ClassDeclarationExtensionContext context)
        {
            if (context.superclass() is PeopleCodeParser.AppClassSuperClassContext appClassContext)
            {
                superClassName = appClassContext.appClassPath().GetText();
            }
        }
        
        /// <summary>
        /// Process method headers to capture method information
        /// </summary>
        public override void EnterMethodHeader([NotNull] PeopleCodeParser.MethodHeaderContext context)
        {
            if (context.genericID() != null)
            {
                var methodName = context.genericID().GetText();
                var returnType = context.typeT() != null ? context.typeT().GetText() : string.Empty;
                bool isAbstract = context.ABSTRACT() != null;
                
                var methodInfo = new MethodInfo
                {
                    Name = methodName,
                    ReturnType = returnType,
                    IsAbstract = isAbstract,
                    AccessModifier = currentAccessModifier
                };
                
                // Process method parameters if any
                if (context.methodArguments() != null)
                {
                    foreach (var argContext in context.methodArguments().methodArgument())
                    {
                        if (argContext.USER_VARIABLE() != null && argContext.typeT() != null)
                        {
                            var paramName = argContext.USER_VARIABLE().GetText();
                            var paramType = argContext.typeT().GetText();
                            var isOut = argContext.OUT() != null;
                            
                            methodInfo.Parameters.Add(new ParameterInfo
                            {
                                Name = paramName,
                                Type = paramType,
                                IsOut = isOut
                            });
                        }
                    }
                }
                
                // Store the method info
                methodData[methodName.ToLowerInvariant()] = methodInfo;
            }
        }
        
        /// <summary>
        /// Process the inheritance chain to extract methods from all parent classes
        /// </summary>
        private void ProcessInheritanceChain()
        {
            if (hasProcessedInheritanceChain || string.IsNullOrEmpty(superClassName) || DataManager == null)
                return;
            
            // Track the inheritance chain to avoid circular references
            HashSet<string> processedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? currentParent = superClassName;
            
            while (!string.IsNullOrEmpty(currentParent) && !processedClasses.Contains(currentParent))
            {
                processedClasses.Add(currentParent);
                
                // Get the parent class methods
                Dictionary<string, MethodInfo> parentMethods = GetParentClassMethods(currentParent, out string? nextParent);
                
                // Store methods with their parent class
                inheritanceChainMethods[currentParent] = parentMethods;
                
                // Move up the chain
                currentParent = nextParent;
            }
            
            hasProcessedInheritanceChain = true;
        }
        
        /// <summary>
        /// Extract methods from a parent class and identify its parent
        /// </summary>
        private Dictionary<string, MethodInfo> GetParentClassMethods(string parentClassPath, out string? parentSuperClassName)
        {
            var methods = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
            parentSuperClassName = null;
            
            // Use the DataManager to get the parent class source
            string? parentClassSource = DataManager?.GetAppClassSourceByPath(parentClassPath);
            
            if (string.IsNullOrEmpty(parentClassSource))
                return methods;
            
            try
            {
                // Create a lexer, token stream, and parser for the parent class
                var lexer = new PeopleCodeLexer(new AntlrInputStream(parentClassSource));
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new PeopleCodeParser(tokenStream);
                
                // Parse the program
                var program = parser.program();
                
                // Create a specialized listener to extract just the methods and inheritance
                var methodExtractor = new MethodExtractorListener();
                
                // Walk the parse tree to extract methods
                var walker = new ParseTreeWalker();
                walker.Walk(methodExtractor, program);
                
                // Get the super class name if it exists
                parentSuperClassName = methodExtractor.SuperClassName;
                
                // Get the methods
                methods = methodExtractor.MethodData;
                
                // Clean up
                parser.Interpreter.ClearDFA();
                GC.Collect();
            }
            catch (Exception ex)
            {
                // Log the error
                Debug.LogError($"Error parsing parent class {parentClassPath}: {ex.Message}");
            }
            
            return methods;
        }
        
        /// <summary>
        /// Provides parameters for built-in types
        /// </summary>
        private MethodInfo? GetParametersForBuiltinType(string typeName, string methodName)
        {
            // This method would be implemented to provide parameter information for built-in types
            // For now, it's a stub that returns null
            return null;
        }
        
        /// <summary>
        /// Helper listener class to extract method info and superclass from a parsed class
        /// </summary>
        private class MethodExtractorListener : PeopleCodeParserBaseListener
        {
            public Dictionary<string, MethodInfo> MethodData { get; } = 
                new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
            
            public string? SuperClassName { get; private set; }
            
            private string currentAccessModifier = string.Empty;
            
            // Track inheritance
            public override void EnterClassDeclarationExtension([NotNull] PeopleCodeParser.ClassDeclarationExtensionContext context)
            {
                if (context.superclass() is PeopleCodeParser.AppClassSuperClassContext appClassContext)
                {
                    SuperClassName = appClassContext.appClassPath().GetText();
                }
            }
            
            // Track access modifiers
            public override void EnterPublicHeader([NotNull] PeopleCodeParser.PublicHeaderContext context)
            {
                currentAccessModifier = "Public";
            }
            
            public override void EnterProtectedHeader([NotNull] PeopleCodeParser.ProtectedHeaderContext context)
            {
                currentAccessModifier = "Protected";
            }
            
            public override void EnterPrivateHeader([NotNull] PeopleCodeParser.PrivateHeaderContext context)
            {
                currentAccessModifier = "Private";
            }
            
            // Extract method definitions
            public override void EnterMethodHeader([NotNull] PeopleCodeParser.MethodHeaderContext context)
            {
                if (context.genericID() != null)
                {
                    var methodName = context.genericID().GetText();
                    var returnType = context.typeT() != null ? context.typeT().GetText() : string.Empty;
                    bool isAbstract = context.ABSTRACT() != null;
                    
                    var methodInfo = new MethodInfo
                    {
                        Name = methodName,
                        ReturnType = returnType,
                        IsAbstract = isAbstract,
                        AccessModifier = currentAccessModifier
                    };
                    
                    // Process method parameters if any
                    if (context.methodArguments() != null)
                    {
                        foreach (var argContext in context.methodArguments().methodArgument())
                        {
                            if (argContext.USER_VARIABLE() != null && argContext.typeT() != null)
                            {
                                var paramName = argContext.USER_VARIABLE().GetText();
                                var paramType = argContext.typeT().GetText();
                                var isOut = argContext.OUT() != null;
                                
                                methodInfo.Parameters.Add(new ParameterInfo
                                {
                                    Name = paramName,
                                    Type = paramType,
                                    IsOut = isOut
                                });
                            }
                        }
                    }
                    
                    // Store the method info
                    MethodData[methodName.ToLowerInvariant()] = methodInfo;
                }
            }
        }
        
        /// <summary>
        /// Handles dot access expressions like %This.MethodName() or &variable.MethodName() to display parameter information
        /// </summary>
        public override void EnterDotAccessExpr([NotNull] PeopleCodeParser.DotAccessExprContext context)
        {
            var expr = context.expression();
            if (expr == null)
                return;
                
            string objectText = expr.GetText();
            bool isThis = objectText.Equals("%This", StringComparison.OrdinalIgnoreCase);
            bool isVariable = objectText.StartsWith("&");
            
            // Handle both %This and &variable cases
            if (isThis || isVariable)
            {
                var dotAccessList = context.dotAccess();
                if (dotAccessList == null || dotAccessList.Length == 0)
                    return;
                    
                foreach (var dotAccess in dotAccessList)
                {
                    if (dotAccess.genericID() == null)
                        continue;
                        
                    string methodName = dotAccess.genericID().GetText();
                    
                    // Check if this is a method call (has parentheses)
                    bool isMethodCall = dotAccess.LPAREN() != null;
                    
                    if (!isMethodCall)
                        continue;
                    
                    // Get tooltip position information
                    int start = dotAccess.genericID().Start.StartIndex;
                    int length = dotAccess.genericID().Stop.StopIndex - start + 1;
                    
                    if (isThis)
                    {
                        // Handle %This.Method() - existing code
                        HandleThisMethodCall(methodName, start, length);
                    }
                    else if (isVariable)
                    {
                        // Handle &variable.Method() - new code with scoped variable tracking
                        string variableName = objectText;
                        HandleVariableMethodCall(variableName, methodName, start, length);
                    }
                }
            }
        }
        
        /// <summary>
        /// Handle %This.MethodName() calls to find method parameters
        /// </summary>
        private void HandleThisMethodCall(string methodName, int start, int length)
        {
            // Check if we know this method in the current class
            if (methodData.TryGetValue(methodName.ToLowerInvariant(), out var methodInfo))
            {
                // Found in current class
                RegisterTooltip(start, length, $"{methodInfo}");
            }
            else if (!string.IsNullOrEmpty(superClassName) && DataManager != null)
            {
                // Not found in current class, process the full inheritance chain
                if (!hasProcessedInheritanceChain)
                {
                    ProcessInheritanceChain();
                }
                
                bool foundInParent = false;
                
                // Try to find the method in the inheritance chain
                foreach (var entry in inheritanceChainMethods)
                {
                    string parentClassName = entry.Key;
                    Dictionary<string, MethodInfo> parentMethods = entry.Value;
                    
                    if (parentMethods.TryGetValue(methodName.ToLowerInvariant(), out var parentMethodInfo))
                    {
                        // Found in a parent class - add the class name to the tooltip
                        string tooltipWithInheritance = $"Method: {parentMethodInfo.Name} (inherited from {parentClassName})\n" +
                                                        $"Access: {parentMethodInfo.AccessModifier}\n";
                                                      
                        // Add the rest of the method info
                        if (parentMethodInfo.IsAbstract)
                            tooltipWithInheritance += "Abstract Method\n";
                            
                        if (!string.IsNullOrEmpty(parentMethodInfo.ReturnType))
                            tooltipWithInheritance += $"Returns: {FormatArrayType(parentMethodInfo.ReturnType)}\n";
                            
                        // Parameters
                        if (parentMethodInfo.Parameters.Count > 0)
                        {
                            tooltipWithInheritance += "Parameters:\n";
                            foreach (var param in parentMethodInfo.Parameters)
                            {
                                tooltipWithInheritance += $"   {param}\n";
                            }
                        }
                        else
                        {
                            tooltipWithInheritance += "Parameters: None\n";
                        }
                        
                        RegisterTooltip(start, length, tooltipWithInheritance.TrimEnd());
                        foundInParent = true;
                        break; // Exit after finding in the inheritance chain
                    }
                }
                
                if (!foundInParent)
                {
                    // Not found in any parent class
                    string stubMessage = $"Method: {methodName}\n(Method definition not found in class hierarchy)";
                    RegisterTooltip(start, length, stubMessage);
                }
            }
            else
            {
                // No parent class or not found
                string stubMessage = $"Method: {methodName}\n(Method definition not found in current class)";
                RegisterTooltip(start, length, stubMessage);
            }
        }
        
        /// <summary>
        /// Handle &variable.MethodName() calls to find method parameters
        /// </summary>
        private void HandleVariableMethodCall(string variableName, string methodName, int start, int length)
        {
            // First, check if we know this variable's type using the ScopedTooltipProvider functionality
            if (!TryGetVariableInfo(variableName, out var varInfo) || varInfo == null)
            {
                // Unknown variable
                string stubMessage = $"Method: {methodName}\n(Variable type unknown)";
                RegisterTooltip(start, length, stubMessage);
                return;
            }
            
            // Determine if we're dealing with an application class type
            if (IsAppClassType(varInfo.Type))
            {
                // For app class types, we need to look up the method in that class
                string classType = varInfo.Type;
                
                // Use DataManager to process the class and find method
                if (DataManager != null)
                {
                    // Create a dictionary for the class if we haven't processed it yet
                    if (!inheritanceChainMethods.ContainsKey(classType))
                    {
                        Dictionary<string, MethodInfo> classMethods = GetParentClassMethods(classType, out string? nextParent);
                        inheritanceChainMethods[classType] = classMethods;
                        
                        // If this class also has a parent, process that hierarchy if needed
                        if (!string.IsNullOrEmpty(nextParent) && !inheritanceChainMethods.ContainsKey(nextParent))
                        {
                            HashSet<string> processedParents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            processedParents.Add(classType);
                            
                            string? currentParent = nextParent;
                            while (!string.IsNullOrEmpty(currentParent) && !processedParents.Contains(currentParent))
                            {
                                processedParents.Add(currentParent);
                                
                                Dictionary<string, MethodInfo> parentMethods = GetParentClassMethods(currentParent, out string? nextNextParent);
                                inheritanceChainMethods[currentParent] = parentMethods;
                                
                                currentParent = nextNextParent;
                            }
                        }
                    }
                    
                    // Check if method exists in the variable's class
                    if (inheritanceChainMethods.TryGetValue(classType, out var variableClassMethods) && 
                        variableClassMethods.TryGetValue(methodName.ToLowerInvariant(), out var methodInfo))
                    {
                        // Found in the variable's class
                        RegisterTooltip(start, length, $"{methodInfo}");
                        return;
                    }
                    
                    // If not found in the class, check its parent classes
                    bool foundInParent = false;
                    foreach (var entry in inheritanceChainMethods)
                    {
                        string parentClassName = entry.Key;
                        Dictionary<string, MethodInfo> parentMethods = entry.Value;
                        
                        // Skip the current class, we already checked it
                        if (string.Equals(parentClassName, classType, StringComparison.OrdinalIgnoreCase))
                            continue;
                            
                        if (parentMethods.TryGetValue(methodName.ToLowerInvariant(), out var parentMethodInfo))
                        {
                            // Found in a parent class - add the class name to the tooltip
                            string tooltipWithInheritance = $"Method: {parentMethodInfo.Name} (inherited from {parentClassName})\n" +
                                                            $"Access: {parentMethodInfo.AccessModifier}\n";
                                                          
                            // Add the rest of the method info
                            if (parentMethodInfo.IsAbstract)
                                tooltipWithInheritance += "Abstract Method\n";
                                
                            if (!string.IsNullOrEmpty(parentMethodInfo.ReturnType))
                                tooltipWithInheritance += $"Returns: {FormatArrayType(parentMethodInfo.ReturnType)}\n";
                                
                            // Parameters
                            if (parentMethodInfo.Parameters.Count > 0)
                            {
                                tooltipWithInheritance += "Parameters:\n";
                                foreach (var param in parentMethodInfo.Parameters)
                                {
                                    tooltipWithInheritance += $"   {param}\n";
                                }
                            }
                            else
                            {
                                tooltipWithInheritance += "Parameters: None\n";
                            }
                            
                            RegisterTooltip(start, length, tooltipWithInheritance.TrimEnd());
                            foundInParent = true;
                            break; // Exit after finding in the inheritance chain
                        }
                    }
                    
                    if (!foundInParent)
                    {
                        // Method not found in class or its hierarchy
                        string stubMessage = $"Method: {methodName}\n(Method definition not found in {classType} or its parent classes)";
                        RegisterTooltip(start, length, stubMessage);
                    }
                }
                else
                {
                    // No DataManager available
                    string stubMessage = $"Method: {methodName}\n(Cannot access class definition for {varInfo.Type})";
                    RegisterTooltip(start, length, stubMessage);
                }
            }
            else
            {
                // For built-in types, use the stub method
                MethodInfo? builtinMethod = GetParametersForBuiltinType(varInfo.Type, methodName);
                
                if (builtinMethod != null)
                {
                    // Found information for the built-in type
                    RegisterTooltip(start, length, $"{builtinMethod}");
                }
                else
                {
                    // No information found for built-in type method
                    string stubMessage = $"Method: {methodName}\nType: {varInfo.Type}\n(Built-in method information not available)";
                    RegisterTooltip(start, length, stubMessage);
                }
            }
        }
    }
} 