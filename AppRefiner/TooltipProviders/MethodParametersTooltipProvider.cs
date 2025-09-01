using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using System.Text;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips showing method parameter information when hovering over method calls.
    /// Specifically focuses on %This.Method() calls and &variable.Method() calls with application class types,
    /// and attempts to find the method definition within the class or its inheritance chain.
    /// This is the self-hosted equivalent of the ANTLR-based MethodParametersTooltipProvider.
    /// </summary>
    public class MethodParametersTooltipProvider : ScopedAstTooltipProvider
    {
        private Dictionary<string, MethodInfo> methodData = new(StringComparer.OrdinalIgnoreCase);
        private string? superClassName;
        private Dictionary<string, Dictionary<string, MethodInfo>> inheritanceChainMethods =
            new(StringComparer.OrdinalIgnoreCase);
        private bool hasProcessedInheritanceChain = false;

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
        /// Structure to store method information
        /// </summary>
        private class MethodInfo
        {
            public string Name { get; set; } = string.Empty;
            public string ReturnType { get; set; } = string.Empty;
            public bool IsAbstract { get; set; } = false;
            public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
            public VisibilityModifier Visibility { get; set; } = VisibilityModifier.Private;

            public override string ToString()
            {
                var sb = new StringBuilder();

                // Method name
                sb.AppendLine($"Method: {Name}");

                // Access modifier
                string visibilityText = Visibility switch
                {
                    VisibilityModifier.Public => "Public",
                    VisibilityModifier.Protected => "Protected",
                    VisibilityModifier.Private => "Private",
                    _ => "Private"
                };
                sb.AppendLine($"Access: {visibilityText}");

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
        /// Processes the AST to collect method information and register tooltips
        /// </summary>
        public override void ProcessProgram(ProgramNode program)
        {
            // Capture the superclass name if present
            if (program.AppClass != null && program.AppClass.BaseClass != null)
            {
                superClassName = program.AppClass.BaseClass.TypeName;
            }

            // Reset state
            methodData.Clear();
            inheritanceChainMethods.Clear();
            hasProcessedInheritanceChain = false;

            base.ProcessProgram(program);
        }

        /// <summary>
        /// Override to collect method information from method declarations
        /// </summary>
        public override void VisitMethod(MethodNode node)
        {
            // Store method information
            var methodInfo = new MethodInfo
            {
                Name = node.Name,
                ReturnType = node.ReturnType?.TypeName ?? string.Empty,
                IsAbstract = node.IsAbstract,
                Visibility = node.Visibility,
                Parameters = node.Parameters.Select(p => new ParameterInfo
                {
                    Name = p.Name,
                    Type = p.Type?.TypeName ?? "any",
                    IsOut = p.IsOut
                }).ToList()
            };

            methodData[node.Name.ToLowerInvariant()] = methodInfo;

            base.VisitMethod(node);
        }

        /// <summary>
        /// Override to process function calls that might be method calls
        /// </summary>
        public override void VisitFunctionCall(FunctionCallNode node)
        {
            // Check if this is a method call (function is a member access)
            if (node.Function is MemberAccessNode memberAccess)
            {
                ProcessMethodCall(memberAccess, node);
            }
            else if (node.Function is IdentifierNode identifier &&
                     identifier.Name.Equals("%This", StringComparison.OrdinalIgnoreCase))
            {
                // This might be a special case - %This() as a function call
                // Handle if needed
            }

            base.VisitFunctionCall(node);
        }

        /// <summary>
        /// Override to process member access for property references
        /// </summary>
        public override void VisitMemberAccess(MemberAccessNode node)
        {
            // Check if this member access is part of a method call
            if (node.Parent is FunctionCallNode functionCall && functionCall.Function == node)
            {
                // This is handled in VisitFunctionCall, so skip here
                return;
            }

            // Handle property access for tooltips (if needed in future)
            base.VisitMemberAccess(node);
        }

        /// <summary>
        /// Processes a method call to register tooltips
        /// </summary>
        private void ProcessMethodCall(MemberAccessNode memberAccess, FunctionCallNode functionCall)
        {
            string methodName = memberAccess.MemberName;

            // Check if target is an identifier (%This, &variable, etc.)
            if (memberAccess.Target is IdentifierNode targetIdentifier)
            {
                string objectText = targetIdentifier.Name;
                bool isThis = objectText.Equals("%This", StringComparison.OrdinalIgnoreCase);
                bool isVariable = objectText.StartsWith("&");

                if (isThis)
                {
                    // Handle %This.Method()
                    HandleThisMethodCall(methodName, memberAccess.SourceSpan);
                }
                else if (isVariable)
                {
                    // Handle &variable.Method()
                    string variableName = objectText;
                    HandleVariableMethodCall(variableName, methodName, memberAccess.SourceSpan);
                }
            }
            else
            {
                // Handle more complex expressions like (someExpr).Method()
                // For now, we could potentially handle these by analyzing the expression
                // but this would be more complex and may not be necessary for basic functionality
            }
        }

        /// <summary>
        /// Handle %This.MethodName() calls to find method parameters
        /// </summary>
        private void HandleThisMethodCall(string methodName, SourceSpan span)
        {
            // Check if we know this method in the current class
            if (methodData.TryGetValue(methodName.ToLowerInvariant(), out var methodInfo))
            {
                // Found in current class
                RegisterTooltip(span, $"{methodInfo}");
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
                                                        $"Access: {parentMethodInfo.Visibility}\n";

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

                        RegisterTooltip(span, tooltipWithInheritance.TrimEnd());
                        foundInParent = true;
                        break;
                    }
                }

                if (!foundInParent)
                {
                    // Not found in any parent class
                    string stubMessage = $"Method: {methodName}\n(Method definition not found in class hierarchy)";
                    RegisterTooltip(span, stubMessage);
                }
            }
            else
            {
                // No parent class or not found
                string stubMessage = $"Method: {methodName}\n(Method definition not found in current class)";
                RegisterTooltip(span, stubMessage);
            }
        }

        /// <summary>
        /// Handle &variable.MethodName() calls to find method parameters
        /// </summary>
        private void HandleVariableMethodCall(string variableName, string methodName, SourceSpan span)
        {
            // First, check if we know this variable's type using the ScopedAstTooltipProvider functionality
            if (!TryGetVariableInfo(variableName, out var varInfo) || varInfo == null)
            {
                // Unknown variable
                string stubMessage = $"Method: {methodName}\n(Variable type unknown)";
                RegisterTooltip(span, stubMessage);
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
                            HashSet<string> processedParents = new(StringComparer.OrdinalIgnoreCase);
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
                        RegisterTooltip(span, $"{methodInfo}");
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
                                                            $"Access: {parentMethodInfo.Visibility}\n";

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

                            RegisterTooltip(span, tooltipWithInheritance.TrimEnd());
                            foundInParent = true;
                            break;
                        }
                    }

                    if (!foundInParent)
                    {
                        // Method not found in class or its hierarchy
                        string stubMessage = $"Method: {methodName}\n(Method definition not found in {classType} or its parent classes)";
                        RegisterTooltip(span, stubMessage);
                    }
                }
                else
                {
                    // No DataManager available
                    string stubMessage = $"Method: {methodName}\n(Cannot access class definition for {varInfo.Type})";
                    RegisterTooltip(span, stubMessage);
                }
            }
            else
            {
                // For built-in types, no information available
                string stubMessage = $"Method: {methodName}\nType: {varInfo.Type}\n(Built-in method information not available)";
                RegisterTooltip(span, stubMessage);
            }
        }

        /// <summary>
        /// Processes the inheritance chain to extract methods from all parent classes
        /// </summary>
        private void ProcessInheritanceChain()
        {
            if (hasProcessedInheritanceChain || string.IsNullOrEmpty(superClassName) || DataManager == null)
                return;

            // Track the inheritance chain to avoid circular references
            HashSet<string> processedClasses = new(StringComparer.OrdinalIgnoreCase);
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
                // Parse the parent class using the self-hosted parser
                var externalProgram = ParseExternalClass(parentClassPath);
                if (externalProgram == null) return methods;

                // Extract superclass name
                if (externalProgram.AppClass != null && externalProgram.AppClass.BaseClass != null)
                {
                    parentSuperClassName = externalProgram.AppClass.BaseClass.TypeName;
                }

                // Extract methods from the parsed program
                if (externalProgram.AppClass != null)
                {
                    foreach (var method in externalProgram.AppClass.Methods)
                    {
                        var methodInfo = new MethodInfo
                        {
                            Name = method.Name,
                            ReturnType = method.ReturnType?.TypeName ?? string.Empty,
                            IsAbstract = method.IsAbstract,
                            Visibility = method.Visibility,
                            Parameters = method.Parameters.Select(p => new ParameterInfo
                            {
                                Name = p.Name,
                                Type = p.Type?.TypeName ?? "any",
                                IsOut = p.IsOut
                            }).ToList()
                        };

                        methods[method.Name.ToLowerInvariant()] = methodInfo;
                    }
                }
            }
            catch (Exception)
            {
                // Silently handle parsing errors
            }

            return methods;
        }

        /// <summary>
        /// Attempts to find variable information in the current scope
        /// </summary>
        private bool TryGetVariableInfo(string name, out VariableInfo? info)
        {
            info = GetVariablesAtPosition().FirstOrDefault(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return info != null;
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
            var matches = System.Text.RegularExpressions.Regex.Matches(lowerType, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            arrayCount = matches.Count;

            if (arrayCount == 0)
            {
                // If no "array of" patterns were found, it's just a simple array
                arrayCount = 1;

                // Extract the base type by removing the array part
                string baseType = System.Text.RegularExpressions.Regex.Replace(type, @"^array\s*of\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    lastIndex = match.Index + match.Length;
                }

                string baseType = type.Substring(lastIndex);

                // Use Array1, Array2, Array3 notation
                return $"Array{arrayCount + 1} of {baseType}";
            }
        }

        /// <summary>
        /// Determines if a type is an Application Class (contains colon characters)
        /// </summary>
        private static bool IsAppClassType(string typeName)
        {
            return !string.IsNullOrEmpty(typeName) && typeName.Contains(':');
        }
    }
}
