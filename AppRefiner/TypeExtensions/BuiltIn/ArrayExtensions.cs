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
        private static void TransformForEach(ScintillaEditor editor, AstNode node, TypeInfo matchedType, VariableRegistry? variableRegistry)
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
        private static void TransformMap(ScintillaEditor editor, AstNode node, TypeInfo matchedType, VariableRegistry? variableRegistry)
        {
            // TODO: Implement Map transformation
            // For now, just a placeholder
        }

        /// <summary>
        /// Transforms .Filter() method call to a For loop that builds a filtered array
        /// Example: &result = &students.Filter(&item.GPA > 3.5)
        /// Becomes:
        ///   Local array of Student &result = CreateArray();
        ///   For &i = 1 To &students.Len
        ///     If &students[&i].GPA > 3.5 Then
        ///       &result.Push(&students[&i]);
        ///     End-If;
        ///   End-For;
        /// </summary>
        private static void TransformFilter(ScintillaEditor editor, AstNode node, TypeInfo matchedType, VariableRegistry? variableRegistry)
        {
            // TODO: Implement Filter transformation
            // For now, just a placeholder
        }

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
