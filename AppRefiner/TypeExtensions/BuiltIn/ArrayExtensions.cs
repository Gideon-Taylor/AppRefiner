using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

namespace AppRefiner.LanguageExtensions.BuiltIn
{
    /// <summary>
    /// Provides extensions for array types
    /// </summary>
    public class ArrayExtensions : BaseTypeExtension
    {
        // Use dimensions=0 as wildcard to match any array dimensionality (1D, 2D, 3D, etc.)
        public override TypeInfo TargetType => new ArrayTypeInfo(dimensions: 0);
        public override List<ExtensionTransform> Transforms => new()
        {
            new ExtensionTransform
            {
                Signature = "ForEach(iterator?: number, item_variable?: $element) -> void",
                Description = "Expands to a For loop that iterates the array.",
                TransformAction = TransformForEach
            },

            new ExtensionTransform
            {
                Signature = "Map(expression: any) -> array of any",
                Description = "Transforms each element using an expression. Use &item to reference the current element.",
                TransformAction = TransformMap,
                ImplicitParameters = new()
                {
                    new ImplicitParameter("&item", targetType =>
                    {
                        // For array of T, return T
                        if (targetType is ArrayTypeInfo arrayType)
                        {
                            return arrayType.ElementType ?? AnyTypeInfo.Instance;
                        }
                        return AnyTypeInfo.Instance;
                    })
                }
            },

            new ExtensionTransform
            {
                Signature = "Filter(predicate: any) -> $same",
                Description = "Filters elements using a predicate expression. Use &item to reference the current element.",
                TransformAction = TransformFilter,
                ImplicitParameters = new()
                {
                    new ImplicitParameter("&item", targetType =>
                    {
                        // For array of T, return T
                        if (targetType is ArrayTypeInfo arrayType)
                        {
                            return arrayType.ElementType ?? AnyTypeInfo.Instance;
                        }
                        return AnyTypeInfo.Instance;
                    })
                }
            }
        };

        /// <summary>
        /// Transforms .ForEach() method call to a For loop
        /// </summary>
        private void TransformForEach(ScintillaEditor editor, AstNode node, TypeInfo matchedType, VariableRegistry? variableRegistry)
        {
            if (node is not FunctionCallNode fcn) return;
            

            string iteratorVarName = "&iterator";
            if (fcn.Arguments.Count > 0)
            {
                iteratorVarName = ExtractVariableName(fcn.Arguments[0]);
            }

            string itemHolderVarName = "";
            if (fcn.Arguments.Count >= 2)
            {
                itemHolderVarName = ExtractVariableName(fcn.Arguments[1]);
            }

            string content = ScintillaManager.GetScintillaText(editor) ?? "";

            SourceSpan? expressionSpan = null;
            TypeInfo? elementType = null;
            int dimensions = 0;
            if (fcn.Function is MemberAccessNode memberAccess)
            {
                expressionSpan = memberAccess.Target.SourceSpan;
                if (memberAccess.Target.GetInferredType() is ArrayTypeInfo arrType)
                {
                    elementType = arrType.ElementType;
                    dimensions = arrType.Dimensions - 1;
                }
            }

            if (expressionSpan.HasValue)
            {
                var exprSpan = expressionSpan.Value;

                // Extract the original array expression text from source code
                string arrayExpressionText = content.Substring(
                    exprSpan.Start.ByteIndex,
                    exprSpan.End.ByteIndex - exprSpan.Start.ByteIndex
                );

                /* Get indentation of current line */
                string lineText = ScintillaManager.GetLineText(editor, node.SourceSpan.Start.Line);
                string indent = lineText.Replace(lineText.TrimStart(), "");

                // Get scope context for this node
                var scope = node.GetScopeContext();

                // Check if variables already exist in accessible scopes
                bool iteratorExists = IsVariableManuallyDeclared(variableRegistry, scope, iteratorVarName);
                bool itemHolderExists = IsVariableManuallyDeclared(variableRegistry, scope, itemHolderVarName);

                string defineIterator = !iteratorExists
                    ? $"Local number {iteratorVarName};\n{indent}"
                    : "";

                string forLoopText = $"{defineIterator}For {iteratorVarName} = 1 To {arrayExpressionText}.Len\n";

                string defineItemHolder = "";
                if (!itemHolderExists)
                {
                    string iteratorType = "any";
                    if (elementType != null)
                    {
                        iteratorType = "";
                        for (var x = 0; x < dimensions; x++)
                        {
                            iteratorType += "Array of ";
                        }
                        iteratorType += elementType.Name;
                    }

                    defineItemHolder = $"{indent}   Local {iteratorType} {itemHolderVarName} = {arrayExpressionText}[{iteratorVarName}];\n";
                }

                /* work out the array type */
                if (!string.IsNullOrEmpty(itemHolderVarName))
                {
                    if (!itemHolderExists)
                    {
                        forLoopText += defineItemHolder;
                    }
                    else
                    {
                        forLoopText += $"{defineItemHolder}{indent}   {itemHolderVarName} = {arrayExpressionText}[{iteratorVarName}]\n";
                    }
                }

                forLoopText += $"{indent}   ";

                var newCursorPosition = fcn.SourceSpan.Start.ByteIndex + forLoopText.Length;

                forLoopText += $"\n{indent}End-For";

                // Replace entire function call
                ScintillaManager.ReplaceTextRange(
                    editor,
                    fcn.SourceSpan.Start.ByteIndex,
                    fcn.SourceSpan.End.ByteIndex,
                    forLoopText
                );

                ScintillaManager.SetCursorPosition(editor, newCursorPosition);
            }
        }

        /// <summary>
        /// Transforms .Map() method call to a For loop that builds a new array
        /// Example: &result = &students.Map(&item.GPA)
        /// Becomes:
        ///   Local array of any &result = CreateArray();
        ///   For &i = 1 To &students.Len
        ///     &result.Push(&students[&i].GPA);
        ///   End-For;
        /// </summary>
        private void TransformMap(ScintillaEditor editor, AstNode node, TypeInfo matchedType, VariableRegistry? variableRegistry)
        {
            var usageInfo = ExtensionTransform.GetUsageInfo(node, variableRegistry);

            var implicitVars  = GetTransformByName("Map")?.GetImplicitVariables(node);
            if (implicitVars == null) return;

            // TODO: Implement Map transformation
            // For now, just a placeholder
        }

        /// <summary>
        /// Transforms .Filter() method call to a For loop that builds a filtered array
        /// Example: &result = &students.Filter(&item.GPA > 3.5)
        /// Becomes:
        ///   Local Student &item;
        ///   Local array of Student &result = createarrayrept(&item, 0);
        ///   Local number &iterator;
        ///   For &iterator = 1 To &students.Len
        ///     If &students[&iterator].GPA > 3.5 Then
        ///       &result.Push(&students[&iterator]);
        ///     End-If;
        ///   End-For;
        /// </summary>
        private static void TransformFilter(ScintillaEditor editor, AstNode node, TypeInfo matchedType, VariableRegistry? variableRegistry)
        {
            // 1. Validate node type
            if (node is not FunctionCallNode fcn) return;
            if (fcn.Function is not MemberAccessNode memberAccess) return;
            if (fcn.Arguments.Count == 0) return;

            // 2. Get usage context
            var usageInfo = ExtensionTransform.GetUsageInfo(node, variableRegistry);

            // 3. Extract array information
            string content = ScintillaManager.GetScintillaText(editor) ?? "";
            var arraySpan = memberAccess.Target.SourceSpan;
            string arrayExpr = content.Substring(arraySpan.Start.Index,
                                                arraySpan.End.Index - arraySpan.Start.Index);

            // 4. Get element type
            TypeInfo? elementType = null;
            int dimensions = 0;
            if (memberAccess.Target.GetInferredType() is ArrayTypeInfo arrType)
            {
                elementType = arrType.ElementType;
                dimensions = arrType.Dimensions;
            }

            // 5. Extract predicate
            var predicateNode = fcn.Arguments[0];
            var predicateSpan = predicateNode.SourceSpan;
            string predicateText = content.Substring(predicateSpan.Start.Index,
                                                    predicateSpan.End.Index - predicateSpan.Start.Index);

            // 6. Get scope context
            var scope = node.GetScopeContext();

            // 7. Determine iterator variable
            var iteratorInfo = DetermineIteratorVariable(variableRegistry, scope);

            // 8. Determine item variable
            var itemInfo = DetermineItemVariable(variableRegistry, scope, "&item", elementType);

            // 9. Determine result variable
            var resultInfo = DetermineResultVariable(usageInfo, elementType, variableRegistry, scope);

            // 10. Replace &item in predicate with array accessor
            string arrayAccessor = $"{arrayExpr}[{iteratorInfo.VariableName}]";
            string transformedPredicate = ReplaceImplicitParameter(
                predicateText, predicateNode, "&item", arrayAccessor);

            // 11. Get indentation
            string lineText = ScintillaManager.GetLineText(editor, node.SourceSpan.Start.Line);
            string indent = lineText.Replace(lineText.TrimStart(), "");

            // 12. Build generated code
            string generatedCode = BuildFilterCode(
                usageInfo,
                iteratorInfo, itemInfo, resultInfo,
                arrayExpr, transformedPredicate,
                elementType, dimensions, indent);

            // 13. Apply transformation based on usage type
            if (usageInfo.UsageType == ExtensionUsageType.FunctionParameter ||
                usageInfo.UsageType == ExtensionUsageType.Other)
            {
                // For function parameters and complex expressions:
                // 1. Insert the filter generation code before the containing statement
                // 2. Replace only the Filter call with the result variable name

                // Find the containing statement
                var containingStatement = node.FindAncestor<StatementNode>();
                if (containingStatement != null)
                {
                    int insertPos = containingStatement.SourceSpan.Start.Index;

                    // Insert the filter generation code before the statement
                    ScintillaManager.InsertTextAtLocation(editor, insertPos, generatedCode);

                    // Replace just the Filter call with the result variable name
                    // Need to recalculate positions after insertion
                    int filterCallStart = fcn.SourceSpan.Start.Index + generatedCode.Length;
                    int filterCallEnd = fcn.SourceSpan.End.Index + generatedCode.Length;
                    ScintillaManager.ReplaceTextRange(editor, filterCallStart, filterCallEnd, resultInfo.VariableName);

                    // Set cursor at the end of the replaced variable name in the function call
                    int cursorPos = filterCallStart + resultInfo.VariableName.Length;
                    ScintillaManager.SetCursorPosition(editor, cursorPos);
                }
                else
                {
                    // Fallback: just replace the filter call
                    ScintillaManager.ReplaceTextRange(editor, fcn.SourceSpan.Start.Index, fcn.SourceSpan.End.Index, generatedCode);
                }
            }
            else
            {
                // For other usage types (Declaration, Assignment, Statement):
                // Replace the entire statement/declaration with the generated code
                DetermineReplacementSpan(usageInfo, node, out int replaceStart, out int replaceEnd);
                ScintillaManager.ReplaceTextRange(editor, replaceStart, replaceEnd, generatedCode);

                // Set cursor position at the end
                int cursorPos = replaceStart + generatedCode.Length;
                ScintillaManager.SetCursorPosition(editor, cursorPos);
            }
        }

        #region Filter Transform Helper Methods

        /// <summary>
        /// Container for variable information
        /// </summary>
        private class VariableInfo
        {
            public required string VariableName { get; init; }
            public required bool NeedsDeclaration { get; init; }
            public string? TypeName { get; init; }
        }

        /// <summary>
        /// Common helper to determine if a variable can be reused or needs declaration
        /// </summary>
        private static VariableInfo DetermineVariableUsage(
            VariableRegistry? registry,
            ScopeContext? scope,
            string variableName,
            string typeName,
            TypeInfo? typeInfo = null,
            string? alternativeNameIfConflict = null)
        {
            // If no registry, assume we need to declare
            if (registry == null || scope == null)
            {
                return new VariableInfo
                {
                    VariableName = variableName,
                    NeedsDeclaration = true,
                    TypeName = typeName
                };
            }

            // Check if variable exists
            var existingVar = registry.FindVariableInScope(variableName, scope);

            if (existingVar == null || existingVar.IsAutoDeclared)
            {
                // Variable doesn't exist or is auto-declared, need to declare it
                return new VariableInfo
                {
                    VariableName = variableName,
                    NeedsDeclaration = true,
                    TypeName = typeName
                };
            }

            // Variable exists and is manually declared - check if type matches
            bool typeMatches = VariableTypeMatches(existingVar, typeInfo, typeName);

            if (typeMatches)
            {
                // Type matches, reuse the existing variable
                return new VariableInfo
                {
                    VariableName = variableName,
                    NeedsDeclaration = false,
                    TypeName = typeName
                };
            }

            // Type doesn't match
            if (alternativeNameIfConflict != null)
            {
                // Use alternative name with declaration
                return new VariableInfo
                {
                    VariableName = alternativeNameIfConflict,
                    NeedsDeclaration = true,
                    TypeName = typeName
                };
            }

            // No alternative provided, can't use this variable (shouldn't happen in practice)
            return new VariableInfo
            {
                VariableName = variableName,
                NeedsDeclaration = true,
                TypeName = typeName
            };
        }

        /// <summary>
        /// Determines the iterator variable to use and whether it needs declaration
        /// </summary>
        private static VariableInfo DetermineIteratorVariable(VariableRegistry? registry, ScopeContext? scope)
        {
            return DetermineVariableUsage(registry, scope, "&iterator", "number");
        }

        /// <summary>
        /// Determines the item variable to use and whether it needs declaration.
        /// Handles type conflicts by generating a new variable name if needed.
        /// </summary>
        private static VariableInfo DetermineItemVariable(
            VariableRegistry? registry,
            ScopeContext? scope,
            string implicitParamName,
            TypeInfo? expectedType)
        {
            string expectedTypeName = FormatTypeName(expectedType, 1);
            string elementTypeName = expectedType?.Name ?? "any";
            string alternativeName = $"&{ToPascalCase(elementTypeName)}Item";

            return DetermineVariableUsage(
                registry,
                scope,
                implicitParamName,
                expectedTypeName,
                expectedType,
                alternativeName);
        }

        /// <summary>
        /// Determines the result variable name and whether it needs declaration
        /// </summary>
        private static VariableInfo DetermineResultVariable(
            ExtensionUsageInfo usageInfo,
            TypeInfo? elementType,
            VariableRegistry? registry,
            ScopeContext? scope)
        {
            string arrayTypeName = $"array of {FormatTypeName(elementType, 1)}";

            switch (usageInfo.UsageType)
            {
                case ExtensionUsageType.VariableDeclaration:
                    // Use the declared variable name (will be part of generated declaration)
                    return new VariableInfo
                    {
                        VariableName = usageInfo.DeclaredVariableName ?? "&filtered",
                        NeedsDeclaration = true,
                        TypeName = arrayTypeName
                    };

                case ExtensionUsageType.VariableAssignment:
                    // Use the assignment target variable name (already declared)
                    return new VariableInfo
                    {
                        VariableName = usageInfo.VariableName ?? "&filtered",
                        NeedsDeclaration = false,
                        TypeName = arrayTypeName
                    };

                case ExtensionUsageType.Statement:
                case ExtensionUsageType.FunctionParameter:
                case ExtensionUsageType.Other:
                default:
                    // Generate variable name: &filtered<ElementType>s (plural)
                    string elementTypeName = elementType?.Name ?? "any";
                    string varName = $"&filtered{ToPascalCase(elementTypeName)}s";

                    // Use common method to check if variable exists and can be reused
                    return DetermineVariableUsage(
                        registry,
                        scope,
                        varName,
                        arrayTypeName,
                        typeInfo: null, // Will use string type comparison
                        alternativeNameIfConflict: null); // No alternative name for result variables
            }
        }

        /// <summary>
        /// Replaces all occurrences of the implicit parameter in the predicate expression
        /// with the array accessor pattern
        /// </summary>
        private static string ReplaceImplicitParameter(
            string predicateText,
            AstNode predicateNode,
            string implicitParamName,
            string arrayAccessorPattern)
        {
            // Find all identifier nodes that match the implicit parameter
            var identifiers = predicateNode.FindDescendants<IdentifierNode>()
                .Where(id => id.Name.Equals(implicitParamName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(id => id.SourceSpan.Start.ByteIndex)
                .ToList();

            if (identifiers.Count == 0)
                return predicateText;

            string result = predicateText;
            int baseOffset = predicateNode.SourceSpan.Start.ByteIndex;

            // Replace from end to start to maintain offsets
            foreach (var id in identifiers)
            {
                int relativeStart = id.SourceSpan.Start.ByteIndex - baseOffset;
                int relativeEnd = id.SourceSpan.End.ByteIndex - baseOffset;

                result = result.Substring(0, relativeStart)
                       + arrayAccessorPattern
                       + result.Substring(relativeEnd);
            }

            return result;
        }

        /// <summary>
        /// Builds the complete generated code for the Filter transformation
        /// </summary>
        private static string BuildFilterCode(
            ExtensionUsageInfo usageInfo,
            VariableInfo iteratorInfo,
            VariableInfo itemInfo,
            VariableInfo resultInfo,
            string arrayExpression,
            string transformedPredicate,
            TypeInfo? elementType,
            int dimensions,
            string indent)
        {
            var codeBuilder = new System.Text.StringBuilder();

            // 1. Declare item variable if needed (used for createarrayrept)
            if (itemInfo.NeedsDeclaration)
            {
                codeBuilder.AppendLine($"{indent}Local {itemInfo.TypeName} {itemInfo.VariableName};");
            }

            // 2. Result variable initialization
            switch (usageInfo.UsageType)
            {
                case ExtensionUsageType.VariableDeclaration:
                    // Replace the entire declaration with our initialization
                    codeBuilder.AppendLine($"{indent}Local {resultInfo.TypeName} {resultInfo.VariableName} = createarrayrept({itemInfo.VariableName}, 0);");
                    break;

                case ExtensionUsageType.VariableAssignment:
                    // Initialize the existing variable
                    codeBuilder.AppendLine($"{indent}{resultInfo.VariableName} = createarrayrept({itemInfo.VariableName}, 0);");
                    break;

                case ExtensionUsageType.Statement:
                case ExtensionUsageType.FunctionParameter:
                case ExtensionUsageType.Other:
                    // Check if variable needs declaration or already exists
                    if (resultInfo.NeedsDeclaration)
                    {
                        // Create a new variable
                        codeBuilder.AppendLine($"{indent}Local {resultInfo.TypeName} {resultInfo.VariableName} = createarrayrept({itemInfo.VariableName}, 0);");
                    }
                    else
                    {
                        // Reuse existing variable
                        codeBuilder.AppendLine($"{indent}{resultInfo.VariableName} = createarrayrept({itemInfo.VariableName}, 0);");
                    }
                    break;
            }

            // 3. Declare iterator variable if needed
            if (iteratorInfo.NeedsDeclaration)
            {
                codeBuilder.AppendLine($"{indent}Local {iteratorInfo.TypeName} {iteratorInfo.VariableName};");
            }

            // 4. Build the For loop
            codeBuilder.AppendLine($"{indent}");
            codeBuilder.AppendLine($"{indent}For {iteratorInfo.VariableName} = 1 To {arrayExpression}.Len");
            codeBuilder.AppendLine($"{indent}   If ({transformedPredicate}) Then");
            codeBuilder.AppendLine($"{indent}      {resultInfo.VariableName}.Push({arrayExpression}[{iteratorInfo.VariableName}]);");
            codeBuilder.AppendLine($"{indent}   End-If;");
            codeBuilder.AppendLine($"{indent}End-For;");
            codeBuilder.AppendLine(); // Add blank line at the end for cursor positioning

            return codeBuilder.ToString();
        }

        /// <summary>
        /// Determines what source span should be replaced based on usage type
        /// </summary>
        private static void DetermineReplacementSpan(
            ExtensionUsageInfo usageInfo,
            AstNode node,
            out int replaceStart,
            out int replaceEnd)
        {
            switch (usageInfo.UsageType)
            {
                case ExtensionUsageType.VariableDeclaration:
                    // Replace the entire declaration line
                    if (usageInfo.DeclarationNode != null)
                    {
                        replaceStart = usageInfo.DeclarationNode.SourceSpan.Start.ByteIndex;
                        replaceEnd = usageInfo.DeclarationNode.SourceSpan.End.ByteIndex;
                    }
                    else
                    {
                        // Fallback to node span
                        replaceStart = node.SourceSpan.Start.ByteIndex;
                        replaceEnd = node.SourceSpan.End.ByteIndex;
                    }
                    break;

                case ExtensionUsageType.VariableAssignment:
                case ExtensionUsageType.Statement:
                    // Find the parent ExpressionStatementNode to get the full statement including semicolon
                    var exprStmt = node.FindAncestor<ExpressionStatementNode>();
                    if (exprStmt != null)
                    {
                        replaceStart = exprStmt.SourceSpan.Start.ByteIndex;
                        replaceEnd = exprStmt.SourceSpan.End.ByteIndex;
                    }
                    else
                    {
                        // Fallback to node span
                        replaceStart = node.SourceSpan.Start.ByteIndex;
                        replaceEnd = node.SourceSpan.End.ByteIndex;
                    }
                    break;

                case ExtensionUsageType.FunctionParameter:
                case ExtensionUsageType.Other:
                    // For these cases, we would need to insert code before the containing statement
                    // and replace just the Filter call with the variable reference
                    // For now, just replace the node itself
                    replaceStart = node.SourceSpan.Start.ByteIndex;
                    replaceEnd = node.SourceSpan.End.ByteIndex;
                    break;

                default:
                    replaceStart = node.SourceSpan.Start.ByteIndex;
                    replaceEnd = node.SourceSpan.End.ByteIndex;
                    break;
            }
        }

        /// <summary>
        /// Formats a type name with optional array dimensions
        /// </summary>
        private static string FormatTypeName(TypeInfo? elementType, int dimensions)
        {
            if (elementType == null) return "any";

            string typeName = elementType.Name;

            // Don't add array dimensions here - handled separately
            return typeName;
        }

        /// <summary>
        /// Checks if two types match
        /// </summary>
        private static bool TypesMatch(TypeInfo? type1, TypeInfo? type2)
        {
            if (type1 == null && type2 == null) return true;
            if (type1 == null || type2 == null) return false;

            // Compare the full qualified names (handles AppClass types like "ADS:Common")
            string name1 = type1.Name ?? "";
            string name2 = type2.Name ?? "";

            return name1.Equals(name2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a variable's type matches the expected type, using both InferredType and string Type
        /// </summary>
        private static bool VariableTypeMatches(
            PeopleCodeParser.SelfHosted.Visitors.Models.VariableInfo variable,
            TypeInfo? expectedType,
            string expectedTypeName)
        {
            // First try InferredType comparison if available
            if (variable.InferredType != null && expectedType != null)
            {
                if (TypesMatch(variable.InferredType, expectedType))
                {
                    return true;
                }
            }

            // Fall back to string Type comparison
            // The Type property contains the declared type as a string
            if (!string.IsNullOrEmpty(variable.Type) && !string.IsNullOrEmpty(expectedTypeName))
            {
                // Normalize both type strings for comparison (remove whitespace, case-insensitive)
                string varType = variable.Type.Replace(" ", "").Trim();
                string expType = expectedTypeName.Replace(" ", "").Trim();

                return varType.Equals(expType, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Converts a string to PascalCase (first letter uppercase, rest lowercase)
        /// and removes invalid variable name characters (like colons from AppClass package paths)
        /// </summary>
        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Handle common cases
            input = input.Trim();
            if (input.Length == 0) return input;

            // Remove colons and other invalid variable name characters
            // Common in AppClass types like "PTAF_CORE:Navigation" -> "PTAF_CORENavigation"
            input = input.Replace(":", "");

            // Simple PascalCase: capitalize first letter, keep rest as-is
            return char.ToUpper(input[0]) + input.Substring(1);
        }

        #endregion

        /// <summary>
        /// Extracts a variable name from an AST node argument.
        /// Supports both LiteralNode (string literals) and IdentifierNode (variable references).
        /// </summary>
        /// <param name="argument">The AST node to extract the variable name from</param>
        /// <returns>The variable name, or empty string if extraction fails</returns>
        private static string ExtractVariableName(AstNode argument)
        {
            return argument switch
            {
                LiteralNode literal => literal.Value?.ToString() ?? "",
                IdentifierNode identifier => identifier.Name,
                _ => ""
            };
        }

        /// <summary>
        /// Checks if a variable is manually declared (not auto-declared) in accessible scopes.
        /// </summary>
        /// <param name="registry">The variable registry to search</param>
        /// <param name="scope">The current scope context</param>
        /// <param name="varName">The variable name to search for</param>
        /// <returns>True if variable is manually declared and accessible; false otherwise</returns>
        private static bool IsVariableManuallyDeclared(
            VariableRegistry? registry,
            ScopeContext? scope,
            string varName)
        {
            if (registry == null || scope == null || string.IsNullOrEmpty(varName))
                return false;

            var variable = registry.FindVariableInScope(varName, scope);
            return variable != null && !variable.IsAutoDeclared;
        }
    }
}
