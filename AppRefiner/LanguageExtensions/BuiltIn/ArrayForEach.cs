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
            if (node is FunctionCallNode fcn)
            {
                if (fcn.Arguments.Count < 1)
                {
                    return;
                }
                string iteratorVarName = "";
                if (fcn.Arguments[0] is LiteralNode iteratorLiteral)
                {
                    iteratorVarName = iteratorLiteral.Value?.ToString() ?? "";
                } else if (fcn.Arguments[0] is IdentifierNode iteratorIdentifier)
                {
                    iteratorVarName = iteratorIdentifier.Name;
                }

                if (string.IsNullOrEmpty(iteratorVarName))
                {
                    return;
                }

                string itemHolderVarName = "";
                if (fcn.Arguments.Count >= 2)
                {
                    if (fcn.Arguments[1] is LiteralNode itemLiteral)
                    {
                        itemHolderVarName = itemLiteral.Value?.ToString() ?? "";
                    }
                    else if (fcn.Arguments[1] is IdentifierNode itemIdentifier)
                    {
                        itemHolderVarName = itemIdentifier.Name;
                    }
                }

                string content = ScintillaManager.GetScintillaText(editor) ?? "";

                SourceSpan? expressionSpan = null;
                TypeInfo? elementType = null;
                int dimensions = 0;
                if (fcn.Function is MemberAccessNode memberAccess) {
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
                    string arrayExpressionText = content.Substring(
                        exprSpan.Start.ByteIndex,
                        exprSpan.End.ByteIndex - exprSpan.Start.ByteIndex
                    );

                    /* Get indentation of current line */
                    string lineText = ScintillaManager.GetLineText(editor, node.SourceSpan.Start.Line);
                    string indent = lineText.Replace(lineText.TrimStart(), "");

                    // Get scope context for this node
                    var scope = node.GetScopeContext();

                    // Check if iterator var exists in accessible scopes
                    bool iteratorExists = false;
                    if (variableRegistry != null && scope != null && !string.IsNullOrEmpty(iteratorVarName))
                    {
                        var iteratorVar = variableRegistry.FindVariableInScope(iteratorVarName, scope);
                        iteratorExists = iteratorVar != null && iteratorVar.IsAutoDeclared == false;
                    }

                    // Check if item holder var exists in accessible scopes
                    bool itemHolderExists = false;
                    if (variableRegistry != null && scope != null && !string.IsNullOrEmpty(itemHolderVarName))
                    {
                        var itemVar = variableRegistry.FindVariableInScope(itemHolderVarName, scope);
                        itemHolderExists = itemVar != null && itemVar.IsAutoDeclared == false;
                    }

                    string defineIterator = "";
                    if (!iteratorExists)
                    {
                        defineIterator = $"Local number {iteratorVarName};\n{indent}";
                    }

                    string forLoopText = $"{defineIterator}For {iteratorVarName} = 1 To {arrayExpressionText}.Len\n";

                    string defineItemHolder = "";
                    if (!itemHolderExists)
                    {
                        string iteratorType = "any";
                        if (elementType != null)
                        {
                            iteratorType = "";
                            for(var x = 0; x < dimensions; x++)
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

                    ScintillaManager.SetCursorPosition(editor,newCursorPosition);

                }
            }

        }
    }
}
