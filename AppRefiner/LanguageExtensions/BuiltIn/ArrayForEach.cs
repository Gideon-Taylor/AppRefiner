using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.LanguageExtensions.BuiltIn
{
    public class ArrayForEach : BaseLanguageExtension
    {
        public override string Name => "ForEach";

        public override string Description => "Expands to a For loop that iterates the array.";

        public override LanguageExtensionType ExtensionType => LanguageExtensionType.Method;

        public override List<TypeInfo> TargetTypes => new() { new ArrayTypeInfo(), new ArrayTypeInfo(2)} ;

        public override TypeWithDimensionality ReturnType => new TypeWithDimensionality(PeopleCodeType.Void);
        public override FunctionInfo? FunctionInfo => new FunctionInfo()
        {
            Parameters = new()
            {
                new SingleParameter()
                {
                    Name = "iterator",
                    ParameterType = new TypeWithDimensionality(PeopleCodeType.Number,0)
                },
                new VariableParameter(
                    new SingleParameter()
                    {
                        Name = "item_variable",
                        ParameterType = new TypeWithDimensionality(PeopleCodeType.ElementOfObject,0)
                    }
                )
            },
            ReturnType = new TypeWithDimensionality(PeopleCodeType.Void)
        };

        public override void Transform(ScintillaEditor editor, AstNode node, TypeInfo matchedType, VariableRegistry? variableRegistry = null)
        {
            if (node is not FunctionCallNode fcn) return;
            if (fcn.Arguments.Count < 1) return;

            string iteratorVarName = ExtractVariableName(fcn.Arguments[0]);

            if (string.IsNullOrEmpty(iteratorVarName))
            {
                return;
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
