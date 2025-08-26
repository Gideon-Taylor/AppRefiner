using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using AppRefiner.PeopleCode;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips showing information about variables in the code.
    /// This is a sample implementation showing how to use ParseTreeTooltipProvider.
    /// </summary>
    public class VariableInfoTooltipProvider : ParseTreeTooltipProvider
    {
        private Dictionary<string, VariableInfo> variableData = new Dictionary<string, VariableInfo>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> propertyNameToVariableName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public override string Name => "Variable Info";

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public override string Description => "Shows information about variables in the code";

        /// <summary>
        /// Medium priority
        /// </summary>
        public override int Priority => 50;

        /// <summary>
        /// Specifies which token types this provider is interested in.
        /// </summary>
        public override int[]? TokenTypes => new int[] 
        { 
            PeopleCodeLexer.USER_VARIABLE,
//            PeopleCodeLexer.LOCAL,
//            PeopleCodeLexer.CONSTANT,
//            PeopleCodeLexer.INSTANCE,
//            PeopleCodeLexer.PRIVATE,
//            PeopleCodeLexer.PROTECTED,
//            PeopleCodeLexer.PROPERTY,
//            PeopleCodeLexer.GLOBAL,
//            PeopleCodeLexer.COMPONENT,
            PeopleCodeLexer.GENERIC_ID_LIMITED // For property names
        };

        /// <summary>
        /// Structure to store variable information
        /// </summary>
        private class VariableInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Scope { get; set; } = "Unknown";
            public string AccessModifier { get; set; } = string.Empty;
            public string InitialValue { get; set; } = string.Empty;
            public string Direction { get; set; } = string.Empty;
            public bool IsReadOnly { get; set; } = false;
            public bool IsProperty { get; set; } = false;
            public bool IsConstant { get; set; } = false;
            
            public override string ToString()
            {
                var sb = new System.Text.StringBuilder();
                
                sb.AppendLine("Variable Info:");
                
                // Show scope
                sb.Append("   Scope: ");
                if (IsProperty)
                {
                    sb.Append("Property");
                    if (IsReadOnly)
                        sb.Append(" (ReadOnly)");
                }
                else if (IsConstant)
                {
                    sb.Append("Constant");
                }
                else
                {
                    sb.Append(Scope);
                }
                sb.AppendLine();
                
                // Access modifier if present
                if (!string.IsNullOrEmpty(AccessModifier))
                {
                    sb.AppendLine($"   Access: {AccessModifier}");
                }
                
                // Type information with formatted array types
                if (!string.IsNullOrEmpty(Type))
                {
                    string formattedType = FormatArrayType(Type);
                    sb.AppendLine($"   Type: {formattedType}");
                }
                
                // Parameter direction if applicable
                if (!string.IsNullOrEmpty(Direction))
                {
                    sb.AppendLine($"   Direction: {Direction}");
                }
                
                // Initial value if present
                if (!string.IsNullOrEmpty(InitialValue))
                {
                    sb.AppendLine($"   Initial Value: {InitialValue}");
                }
                
                return sb.ToString().TrimEnd();
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
            int index = 0;
            
            // Use regex to count array of occurrences
            string pattern = @"(array\s*of\s*)";
            Match match = Regex.Match(lowerType, pattern, RegexOptions.IgnoreCase);
            
            while (match.Success)
            {
                arrayCount++;
                index = match.Index + match.Length;
                match = match.NextMatch();
            }
            
            if (arrayCount == 0)
            {
                // Fallback for when the regex doesn't match
                arrayCount = Regex.Matches(lowerType, "array").Count;
                
                // Check if the type follows the pattern "arrayofarrayof..."
                if (Regex.IsMatch(lowerType, @"^array(\s*of\s*array)*"))
                {
                    // Extract the base type by removing all "array of" parts
                    string baseType = Regex.Replace(type, @"^(array\s*of\s*)*", "", RegexOptions.IgnoreCase);
                    
                    // Format the array declaration
                    if (arrayCount == 1)
                        return $"Array of {baseType}";
                    else
                        return $"Array{arrayCount} of {baseType}";
                }
            }
            else
            {
                // Extract the base type (everything after the last "of")
                string baseType = lowerType.Substring(index).Trim();
                if (string.IsNullOrEmpty(baseType))
                    baseType = "Any"; // Default if no base type specified
                    
                // Keep original casing for base type if possible
                if (index < type.Length)
                    baseType = type.Substring(index).Trim();
                
                // Format the array declaration
                if (arrayCount == 1)
                    return $"Array of {baseType}";
                else
                    return $"Array{arrayCount} of {baseType}";
            }
            
            // If we can't parse it properly, return the original
            return type;
        }

        /// <summary>
        /// Resets the internal state of the tooltip provider.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            variableData.Clear();
            propertyNameToVariableName.Clear();
        }

        // Track current access modifier context (for class members)
        private string currentAccessModifier = string.Empty;
        
        /// <summary>
        /// Captures access modifier for public section
        /// </summary>
        public override void EnterPublicHeader([NotNull] PeopleCode.PeopleCodeParser.PublicHeaderContext context)
        {
            currentAccessModifier = "Public";
        }
        
        /// <summary>
        /// Captures access modifier for protected section
        /// </summary>
        public override void EnterProtectedHeader([NotNull] PeopleCode.PeopleCodeParser.ProtectedHeaderContext context)
        {
            currentAccessModifier = "Protected";
        }
        
        /// <summary>
        /// Captures access modifier for private section
        /// </summary>
        public override void EnterPrivateHeader([NotNull] PeopleCode.PeopleCodeParser.PrivateHeaderContext context)
        {
            currentAccessModifier = "Private";
        }

        /// <summary>
        /// Handles local variable definitions
        /// </summary>
        public override void EnterLocalVariableDefinition([NotNull] PeopleCode.PeopleCodeParser.LocalVariableDefinitionContext context)
        {
            if (context.typeT() != null && context.USER_VARIABLE() != null)
            {
                var typeName = context.typeT().GetText();
                
                foreach (var userVariable in context.USER_VARIABLE())
                {
                    var variableName = userVariable.GetText();
                    
                    var varInfo = new VariableInfo
                    {
                        Name = variableName,
                        Type = typeName,
                        Scope = "Local Variable"
                    };
                    
                    variableData[variableName] = varInfo;
                    RegisterTooltip(userVariable.Symbol, $"{varInfo}");
                }
            }
        }
        
        /// <summary>
        /// Handles local variable declarations with assignment
        /// </summary>
        public override void EnterLocalVariableDeclAssignment([NotNull] PeopleCode.PeopleCodeParser.LocalVariableDeclAssignmentContext context)
        {
            if (context.typeT() != null && context.USER_VARIABLE() != null && context.expression() != null)
            {
                var typeName = context.typeT().GetText();
                var variableName = context.USER_VARIABLE().GetText();
                var initialValue = context.expression().GetText();
                
                var varInfo = new VariableInfo
                {
                    Name = variableName,
                    Type = typeName,
                    Scope = "Local Variable",
                    InitialValue = initialValue
                };
                
                variableData[variableName] = varInfo;
                RegisterTooltip(context.USER_VARIABLE().Symbol, $"{varInfo}");
            }
        }
        
        /// <summary>
        /// Handles instance variable declarations
        /// </summary>
        public override void EnterInstanceDecl([NotNull] PeopleCode.PeopleCodeParser.InstanceDeclContext context)
        {
            if (context.typeT() != null && context.USER_VARIABLE() != null)
            {
                var typeName = context.typeT().GetText();
                
                foreach (var userVariable in context.USER_VARIABLE())
                {
                    var variableName = userVariable.GetText();
                    
                    var varInfo = new VariableInfo
                    {
                        Name = variableName,
                        Type = typeName,
                        Scope = "Instance Variable",
                        AccessModifier = currentAccessModifier
                    };
                    
                    variableData[variableName] = varInfo;
                    RegisterTooltip(userVariable.Symbol, $"{varInfo}");
                }
            }
        }
        
        /// <summary>
        /// Handles constant declarations
        /// </summary>
        public override void EnterConstantDeclaration([NotNull] PeopleCode.PeopleCodeParser.ConstantDeclarationContext context)
        {
            if (context.USER_VARIABLE() != null && context.literal() != null)
            {
                var variableName = context.USER_VARIABLE().GetText();
                var value = context.literal().GetText();
                
                var varInfo = new VariableInfo
                {
                    Name = variableName,
                    InitialValue = value,
                    Scope = "Constant",
                    IsConstant = true,
                    AccessModifier = currentAccessModifier
                };
                
                variableData[variableName] = varInfo;
                RegisterTooltip(context.USER_VARIABLE().Symbol, $"{varInfo}");
            }
        }
        
        /// <summary>
        /// Handles properties with get/set
        /// </summary>
        public override void EnterPropertyGetSet([NotNull] PeopleCode.PeopleCodeParser.PropertyGetSetContext context)
        {
            if (context.genericID() != null && context.typeT() != null)
            {
                var propertyName = context.genericID().GetText();
                var typeName = context.typeT().GetText();
                bool hasGet = true; // Always has GET
                bool hasSet = context.SET() != null;
                
                var varInfo = new VariableInfo
                {
                    Name = propertyName,
                    Type = typeName,
                    Scope = "Property",
                    IsProperty = true,
                    IsReadOnly = !hasSet,
                    AccessModifier = currentAccessModifier
                };
                
                // Store property info under both access patterns
                string thisPropertyKey = "%This." + propertyName;
                string varPropertyKey = "&" + propertyName;
                
                variableData[thisPropertyKey] = varInfo;
                variableData[varPropertyKey] = varInfo;
                
                // Map property name to variable patterns for dotAccess resolution
                propertyNameToVariableName[propertyName.ToLowerInvariant()] = thisPropertyKey;
                
                // Register tooltip for the property name in the declaration
                // Use token indexes to register the tooltip
                int start = context.genericID().Start.ByteStartIndex();
                int length = context.genericID().Stop.ByteStopIndex() - start + 1;
                RegisterTooltip(start, length, $"{varInfo}");
            }
        }
        
        /// <summary>
        /// Handles properties with direct access
        /// </summary>
        public override void EnterPropertyDirect([NotNull] PeopleCode.PeopleCodeParser.PropertyDirectContext context)
        {
            if (context.genericID() != null && context.typeT() != null)
            {
                var propertyName = context.genericID().GetText();
                var typeName = context.typeT().GetText();
                bool isReadOnly = context.READONLY() != null;
                bool isAbstract = context.ABSTRACT() != null;
                
                var varInfo = new VariableInfo
                {
                    Name = propertyName,
                    Type = typeName,
                    Scope = isAbstract ? "Abstract Property" : "Property",
                    IsProperty = true,
                    IsReadOnly = isReadOnly,
                    AccessModifier = currentAccessModifier
                };
                
                // Store property info under both access patterns
                string thisPropertyKey = "%This." + propertyName;
                string varPropertyKey = "&" + propertyName;
                
                variableData[thisPropertyKey] = varInfo;
                variableData[varPropertyKey] = varInfo;
                
                // Map property name to variable patterns for dotAccess resolution
                propertyNameToVariableName[propertyName.ToLowerInvariant()] = thisPropertyKey;
                
                // Register tooltip for the property name in the declaration
                // Use token indexes to register the tooltip
                int start = context.genericID().Start.ByteStartIndex();
                int length = context.genericID().Stop.ByteStopIndex() - start + 1;
                RegisterTooltip(start, length, $"{varInfo}");
            }
        }
        
        /// <summary>
        /// Handles global variable declarations
        /// </summary>
        public override void EnterNonLocalVarDeclaration([NotNull] PeopleCode.PeopleCodeParser.NonLocalVarDeclarationContext context)
        {
            if (context.typeT() != null && context.USER_VARIABLE() != null)
            {
                var typeName = context.typeT().GetText();
                string scope = context.COMPONENT() != null ? "Component Variable" : "Global Variable";
                
                foreach (var userVariable in context.USER_VARIABLE())
                {
                    var variableName = userVariable.GetText();
                    
                    var varInfo = new VariableInfo
                    {
                        Name = variableName,
                        Type = typeName,
                        Scope = scope
                    };
                    
                    variableData[variableName] = varInfo;
                    RegisterTooltip(userVariable.Symbol, $"{varInfo}");
                }
            }
        }
        
        /// <summary>
        /// Handles method arguments for extracted type information
        /// </summary>
        public override void EnterMethodArgument([NotNull] PeopleCode.PeopleCodeParser.MethodArgumentContext context)
        {
            if (context.USER_VARIABLE() != null && context.typeT() != null)
            {
                var variableName = context.USER_VARIABLE().GetText();
                var typeName = context.typeT().GetText();
                var direction = context.OUT() != null ? "out" : "in";
                
                var varInfo = new VariableInfo
                {
                    Name = variableName,
                    Type = typeName,
                    Scope = "Method Parameter",
                    Direction = direction
                };
                
                variableData[variableName] = varInfo;
                RegisterTooltip(context.USER_VARIABLE().Symbol, $"{varInfo}");
            }
        }
        
        /// <summary>
        /// Handles function arguments
        /// </summary>
        public override void EnterFunctionArgument([NotNull] PeopleCode.PeopleCodeParser.FunctionArgumentContext context)
        {
            if (context.USER_VARIABLE() != null)
            {
                var variableName = context.USER_VARIABLE().GetText();
                var typeName = context.typeT() != null ? context.typeT().GetText() : "any";
                
                var varInfo = new VariableInfo
                {
                    Name = variableName,
                    Type = typeName,
                    Scope = "Function Parameter"
                };
                
                variableData[variableName] = varInfo;
                RegisterTooltip(context.USER_VARIABLE().Symbol, $"{varInfo}");
            }
        }
        
        /// <summary>
        /// Handles user variable references in expressions
        /// </summary>
        public override void EnterIdentUserVariable([NotNull] PeopleCode.PeopleCodeParser.IdentUserVariableContext context)
        {
            var variableName = context.USER_VARIABLE().GetText();
            
            // Check if we know the type of this variable
            if (variableData.TryGetValue(variableName, out var varInfo))
            {
                RegisterTooltip(context.USER_VARIABLE().Symbol, $"{varInfo}");
            }
        }
        
        /// <summary>
        /// Handles dot access expressions like %This.PropertyName
        /// </summary>
        public override void EnterDotAccessExpr([NotNull] PeopleCode.PeopleCodeParser.DotAccessExprContext context)
        {
            var dotAccessList = context.dotAccess();
            if (dotAccessList != null && dotAccessList.Length > 0)
            {
                foreach (var dotAccess in dotAccessList)
                {
                    if (dotAccess.genericID() != null)
                    {
                        string propertyName = dotAccess.genericID().GetText();
                        
                        // Check if we know this as a property
                        if (propertyNameToVariableName.TryGetValue(propertyName.ToLowerInvariant(), out var varKey) &&
                            variableData.TryGetValue(varKey, out var varInfo))
                        {
                            // Use token indexes to register the tooltip
                            int start = dotAccess.genericID().Start.ByteStartIndex();
                            int length = dotAccess.genericID().Stop.ByteStopIndex() - start + 1;
                            RegisterTooltip(start, length, $"{varInfo}");
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Process generic ID tokens which might be property references
        /// </summary>
        public override void EnterIdentGenericID([NotNull] PeopleCode.PeopleCodeParser.IdentGenericIDContext context)
        {
            if (context.genericID() != null)
            {
                string idName = context.genericID().GetText();
                
                // Check if we know this as a property
                if (propertyNameToVariableName.TryGetValue(idName.ToLowerInvariant(), out var varKey) &&
                    variableData.TryGetValue(varKey, out var varInfo))
                {
                    // Use token indexes to register the tooltip
                    int start = context.genericID().Start.ByteStartIndex();
                    int length = context.genericID().Stop.ByteStopIndex() - start + 1;
                    RegisterTooltip(start, length, $"{varInfo}");
                }
            }
        }
    }
} 