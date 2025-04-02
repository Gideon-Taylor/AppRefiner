using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips showing method parameter information when hovering over method calls.
    /// Specifically focuses on %This.Method() calls and attempts to find the method definition
    /// within the current class.
    /// </summary>
    public class MethodParametersTooltipProvider : ParseTreeTooltipProvider
    {
        private Dictionary<string, MethodInfo> methodData = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);

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
        /// Specifies which token types this provider is interested in.
        /// </summary>
        public override int[]? TokenTypes => new int[] 
        { 
            PeopleCodeLexer.GENERIC_ID_LIMITED, // For method names in %This.Method() calls
            PeopleCodeLexer.SYSTEM_VARIABLE     // For %This
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
        }

        // Track current access modifier context (for class members)
        private string currentAccessModifier = string.Empty;
        
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
        /// Handles dot access expressions like %This.MethodName() to display parameter information
        /// </summary>
        public override void EnterDotAccessExpr([NotNull] PeopleCodeParser.DotAccessExprContext context)
        {
            var expr = context.expression();
            if (expr != null && expr.GetText().Equals("%This", StringComparison.OrdinalIgnoreCase))
            {
                var dotAccessList = context.dotAccess();
                if (dotAccessList != null && dotAccessList.Length > 0)
                {
                    foreach (var dotAccess in dotAccessList)
                    {
                        if (dotAccess.genericID() != null)
                        {
                            string methodName = dotAccess.genericID().GetText();
                            
                            // Check if this is a method call (has parentheses)
                            bool isMethodCall = dotAccess.LPAREN() != null;
                            
                            if (isMethodCall)
                            {
                                // Check if we know this method
                                if (methodData.TryGetValue(methodName.ToLowerInvariant(), out var methodInfo))
                                {
                                    // Register tooltip for the method name in the call
                                    int start = dotAccess.genericID().Start.StartIndex;
                                    int length = dotAccess.genericID().Stop.StopIndex - start + 1;
                                    RegisterTooltip(start, length, $"{methodInfo}");
                                }
                                else
                                {
                                    // Method not found in current class - provide stub message
                                    string stubMessage = $"Method: {methodName}\n(Method definition not found in current class)";
                                    int start = dotAccess.genericID().Start.StartIndex;
                                    int length = dotAccess.genericID().Stop.StopIndex - start + 1;
                                    RegisterTooltip(start, length, stubMessage);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
} 