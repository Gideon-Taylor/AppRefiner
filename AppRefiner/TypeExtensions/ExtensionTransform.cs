using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using TypeInfo = PeopleCodeTypeInfo.Types.TypeInfo;

namespace AppRefiner.LanguageExtensions
{
    /// <summary>
    /// Represents a single transform provided by a language extension.
    /// Multiple transforms can be grouped in a single BaseLanguageExtension class.
    /// </summary>
    public class ExtensionTransform
    {
        // Private fields for parsed signature information
        private FunctionInfo? _functionInfo;
        private PropertyInfo? _propertyInfo;
        private string _signature = string.Empty;
        public string Name { get
            {
                if (_functionInfo != null)
                    return _functionInfo.Name;
                else if (_propertyInfo != null)
                    return _propertyInfo.Name;
                else
                    return string.Empty;
            } 
        }
        /// <summary>
        /// The signature string defining this extension (e.g., "Len -> number" or "IndexOf(search_string: string) -> number")
        /// </summary>
        public required string Signature
        {
            get => _signature;
            init
            {
                _signature = value;
                ParseSignature();
            }
        }

        /// <summary>
        /// Description of what this transform does
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// Whether this is a property or method extension (auto-derived from signature)
        /// </summary>
        public LanguageExtensionType ExtensionType { get; private set; }

        /// <summary>
        /// The actual transformation logic
        /// </summary>
        public required Action<ScintillaEditor, AstNode, TypeInfo, VariableRegistry?> TransformAction { get; init; }

        /// <summary>
        /// Implicit parameters introduced by this extension (e.g., &item in .Map(&item.Prop)).
        /// These parameters are phantom identifiers that exist only for type inference and are
        /// eliminated during transformation. NOT registered in variable registry.
        /// </summary>
        public List<ImplicitParameter>? ImplicitParameters { get; init; }

        /// <summary>
        /// Reference to parent extension (set during discovery)
        /// </summary>
        internal BaseTypeExtension? ParentExtension { get; set; }

        /// <summary>
        /// Whether this transform is currently active (delegates to parent)
        /// </summary>
        public bool Active => ParentExtension?.Active ?? false;

        /// <summary>
        /// Database requirement (delegates to parent)
        /// </summary>
        public DataManagerRequirement DatabaseRequirement => ParentExtension?.DatabaseRequirement ?? DataManagerRequirement.NotRequired;

        /// <summary>
        /// Data manager instance (delegates to parent)
        /// </summary>
        public IDataManager? DataManager => ParentExtension?.DataManager;

        #region Signature Parsing

        /// <summary>
        /// Parse the signature string to determine extension type and create FunctionInfo or PropertyInfo
        /// </summary>
        private void ParseSignature()
        {
            if (SignatureParser.IsPropertySignature(_signature))
            {
                ExtensionType = LanguageExtensionType.Property;
                _propertyInfo = SignatureParser.ParsePropertySignature(_signature);
                _functionInfo = null;
            }
            else
            {
                ExtensionType = LanguageExtensionType.Method;
                _functionInfo = SignatureParser.ParseFunctionSignature(_signature);
                _propertyInfo = null;
            }
        }

        /// <summary>
        /// Get the name of this extension (e.g., "Len", "IndexOf")
        /// </summary>
        public string GetName()
        {
            return _propertyInfo?.Name ?? $"{_functionInfo?.Name}()" ?? string.Empty;
        }

        /// <summary>
        /// Get the return type of this extension
        /// </summary>
        public TypeWithDimensionality GetReturnType()
        {
            if (_propertyInfo != null)
                return new TypeWithDimensionality(_propertyInfo.Type, _propertyInfo.ArrayDimensionality);

            if (_functionInfo != null)
                return _functionInfo.ReturnType;

            return new TypeWithDimensionality(PeopleCodeType.Unknown);
        }

        /// <summary>
        /// Get the FunctionInfo for method extensions (null for property extensions)
        /// </summary>
        public FunctionInfo? GetFunctionInfo() => _functionInfo;

        /// <summary>
        /// Get the PropertyInfo for property extensions (null for method extensions)
        /// </summary>
        public PropertyInfo? GetPropertyInfo() => _propertyInfo;

        #endregion

        #region Simple Transform Factory

        /// <summary>
        /// Creates a simple pattern-based transform for common string replacement cases.
        /// Pattern syntax:
        ///   %1 = target expression (the object before the dot)
        ///   %2, %3, %4, etc. = function arguments (for method extensions)
        ///
        /// Examples:
        ///   Property signature: "Len -> number" with pattern "Len(%1)" transforms &foo.Len → Len(&foo)
        ///   Method signature: "IndexOf(search_string: string) -> number" with pattern "Find(%2,%1)"
        ///     transforms &foo.IndexOf("x") → Find("x",&foo)
        /// </summary>
        /// <param name="signature">The signature string (e.g., "Len -> number" or "IndexOf(search_string: string) -> number")</param>
        /// <param name="description">Description of what this does</param>
        /// <param name="transformPattern">Pattern string with %1, %2, %3 placeholders</param>
        /// <returns>ExtensionTransform configured with pattern-based logic</returns>
        public static ExtensionTransform CreateSimple(
            string signature,
            string description,
            string transformPattern)
        {
            // Pre-parse to determine type for the transform action
            bool isProperty = SignatureParser.IsPropertySignature(signature);

            return new ExtensionTransform
            {
                Signature = signature,  // This triggers parsing via init setter
                Description = description,
                TransformAction = (editor, node, matchedType, variableRegistry) =>
                {
                    if (isProperty)
                    {
                        ExecuteSimplePropertyTransform(editor, node, transformPattern);
                    }
                    else // Method
                    {
                        ExecuteSimpleMethodTransform(editor, node, transformPattern);
                    }
                }
            };
        }

        /// <summary>
        /// Executes a simple property transform using pattern replacement
        /// </summary>
        private static void ExecuteSimplePropertyTransform(ScintillaEditor editor, AstNode node, string pattern)
        {
            if (node is not MemberAccessNode memberAccess) return;

            string content = ScintillaManager.GetScintillaText(editor) ?? "";
            var targetSpan = memberAccess.Target.SourceSpan;

            // Extract %1 (target expression)
            string targetText = content.Substring(
                targetSpan.Start.ByteIndex,
                targetSpan.End.ByteIndex - targetSpan.Start.ByteIndex
            );

            // Replace pattern
            string newText = pattern.Replace("%1", targetText);

            // Replace entire member access expression
            ScintillaManager.ReplaceTextRange(
                editor,
                memberAccess.SourceSpan.Start.ByteIndex,
                memberAccess.SourceSpan.End.ByteIndex,
                newText
            );
        }

        /// <summary>
        /// Executes a simple method transform using pattern replacement
        /// </summary>
        private static void ExecuteSimpleMethodTransform(ScintillaEditor editor, AstNode node, string pattern)
        {
            if (node is not FunctionCallNode funcCall) return;
            if (funcCall.Function is not MemberAccessNode memberAccess) return;

            string content = ScintillaManager.GetScintillaText(editor) ?? "";

            // Extract %1 (target expression)
            var targetSpan = memberAccess.Target.SourceSpan;
            string targetText = content.Substring(
                targetSpan.Start.ByteIndex,
                targetSpan.End.ByteIndex - targetSpan.Start.ByteIndex
            );

            // Extract %2, %3, %4, etc. (function arguments)
            var argumentTexts = new List<string>();
            foreach (var arg in funcCall.Arguments)
            {
                var span = arg.SourceSpan;
                string argText = content.Substring(
                    span.Start.ByteIndex,
                    span.End.ByteIndex - span.Start.ByteIndex
                );
                argumentTexts.Add(argText);
            }

            // Replace pattern
            string newText = pattern.Replace("%1", targetText);

            // Replace argument placeholders
            for (int i = 0; i < argumentTexts.Count; i++)
            {
                newText = newText.Replace($"%{i + 2}", argumentTexts[i]);
            }

            // Remove any unreplaced placeholders and their surrounding commas/whitespace
            // This handles optional arguments that weren't provided
            // Examples: "Find(%2, %1, %3)" with 1 arg → "Find(arg1, target)" (removes ", %3")
            newText = System.Text.RegularExpressions.Regex.Replace(newText, @",\s*%\d+", "");
            newText = System.Text.RegularExpressions.Regex.Replace(newText, @"%\d+\s*,\s*", "");
            newText = System.Text.RegularExpressions.Regex.Replace(newText, @"%\d+", "");

            // Replace entire function call
            ScintillaManager.ReplaceTextRange(
                editor,
                funcCall.SourceSpan.Start.ByteIndex,
                funcCall.SourceSpan.End.ByteIndex,
                newText
            );
        }

        #endregion

        #region Usage Context Analysis

        /// <summary>
        /// Analyzes how this extension is being used in code by walking the AST parent chain.
        /// Detects whether the extension is used as a statement, in an assignment, in a declaration,
        /// as a function parameter, or in some other context.
        /// </summary>
        /// <param name="node">The AST node where the extension is used (MemberAccessNode for properties, FunctionCallNode for methods)</param>
        /// <param name="variableRegistry">Optional variable registry for type resolution</param>
        /// <returns>ExtensionUsageInfo containing the usage type and relevant contextual information</returns>
        public static ExtensionUsageInfo GetUsageInfo(AstNode node, VariableRegistry? variableRegistry = null)
        {
            var info = new ExtensionUsageInfo();
            AstNode? current = node;

            // Walk up the parent chain to detect usage context
            while (current != null)
            {
                // Priority 1: Variable Declaration
                if (current.Parent is LocalVariableDeclarationWithAssignmentNode declNode)
                {
                    // Verify the extension is part of the initial value
                    if (IsDescendantOf(node, declNode.InitialValue))
                    {
                        info.UsageType = ExtensionUsageType.VariableDeclaration;
                        info.DeclaredVariableName = declNode.VariableName;
                        info.DeclarationNode = declNode;
                        info.DeclaredVariableType = ConvertTypeNodeToTypeInfo(declNode.Type);
                        return info;
                    }
                }

                // Priority 2: Variable Assignment
                if (current.Parent is AssignmentNode assignNode)
                {
                    // Verify the extension is part of the value (right-hand side)
                    if (IsDescendantOf(node, assignNode.Value))
                    {
                        info.UsageType = ExtensionUsageType.VariableAssignment;
                        info.AssignmentNode = assignNode;
                        info.VariableName = ExtractVariableName(assignNode.Target);

                        // Note: Type resolution requires scope context which we don't have in a static method
                        // Users can resolve the type themselves if needed using the AssignmentNode
                        info.VariableType = null;

                        return info;
                    }
                }

                // Priority 3: Function Parameter
                if (current.Parent is FunctionCallNode funcCall)
                {
                    // Check if the node is in the arguments list
                    var paramIndex = GetParameterIndex(funcCall, node);
                    if (paramIndex >= 0)
                    {
                        info.UsageType = ExtensionUsageType.FunctionParameter;
                        info.ParameterIndex = paramIndex;
                        info.FunctionCallNode = funcCall;
                        info.FunctionName = ExtractFunctionName(funcCall);
                        return info;
                    }
                }

                // Priority 4: Statement Usage
                if (current.Parent is ExpressionStatementNode)
                {
                    info.UsageType = ExtensionUsageType.Statement;
                    return info;
                }

                current = current.Parent;
            }

            // Default: Other
            info.UsageType = ExtensionUsageType.Other;
            return info;
        }

        /// <summary>
        /// Check if a node is a descendant of a potential ancestor node
        /// </summary>
        private static bool IsDescendantOf(AstNode node, AstNode? potentialAncestor)
        {
            if (potentialAncestor == null) return false;

            var current = node;
            while (current != null)
            {
                if (current == potentialAncestor)
                    return true;
                current = current.Parent;
            }
            return false;
        }

        /// <summary>
        /// Extract variable name from assignment target
        /// </summary>
        private static string? ExtractVariableName(ExpressionNode target)
        {
            return target switch
            {
                IdentifierNode id => id.Name,
                MemberAccessNode ma => ExtractMemberAccessPath(ma),
                _ => null
            };
        }

        /// <summary>
        /// Extract full path from member access (e.g., "record.field")
        /// </summary>
        private static string? ExtractMemberAccessPath(MemberAccessNode ma)
        {
            if (ma.Target is IdentifierNode id)
                return $"{id.Name}.{ma.MemberName}";

            if (ma.Target is MemberAccessNode nested)
                return $"{ExtractMemberAccessPath(nested)}.{ma.MemberName}";

            return ma.MemberName;
        }

        /// <summary>
        /// Find the parameter index of the node within a function call's arguments
        /// </summary>
        private static int GetParameterIndex(FunctionCallNode funcCall, AstNode node)
        {
            for (int i = 0; i < funcCall.Arguments.Count; i++)
            {
                if (IsDescendantOf(node, funcCall.Arguments[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Extract function name from function call node
        /// </summary>
        private static string? ExtractFunctionName(FunctionCallNode funcCall)
        {
            return funcCall.Function switch
            {
                IdentifierNode id => id.Name,
                MemberAccessNode ma => ma.MemberName,
                _ => null
            };
        }

        /// <summary>
        /// Convert TypeNode to TypeInfo for type resolution
        /// </summary>
        private static TypeInfo? ConvertTypeNodeToTypeInfo(TypeNode? typeNode)
        {
            if (typeNode == null) return null;

            try
            {
                return typeNode switch
                {
                    BuiltInTypeNode builtin => TypeInfo.FromPeopleCodeType(builtin.Type),
                    ArrayTypeNode array => new PeopleCodeTypeInfo.Types.ArrayTypeInfo(
                        array.Dimensions,
                        ConvertTypeNodeToTypeInfo(array.ElementType)),
                    AppClassTypeNode appClass => new PeopleCodeTypeInfo.Types.AppClassTypeInfo(
                        string.Join(":", appClass.PackagePath.Concat(new[] { appClass.ClassName }))),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Implicit Variable Discovery

        /// <summary>
        /// Finds all implicit variables (from language extensions) within an AST subtree.
        /// Returns a distinct dictionary mapping variable names to their resolved types.
        /// </summary>
        /// <param name="node">The AST node to search within</param>
        /// <param name="implicitParameters">List of implicit parameters defined by the extension</param>
        /// <param name="targetType">The type of the object the extension is called on (used for type resolution)</param>
        /// <returns>Dictionary mapping implicit variable names to their types</returns>
        public Dictionary<string, TypeInfo> GetImplicitVariables(
            AstNode node)
        {
            var result = new Dictionary<string, TypeInfo>(StringComparer.OrdinalIgnoreCase);

            // Early exit if no implicit parameters defined
            if (ImplicitParameters == null || ImplicitParameters .Count == 0)
                return result;

            // Find all identifier nodes in the subtree
            var identifiers = node.FindDescendants<IdentifierNode>();

            foreach (var identifier in identifiers)
            {
                // Check if this identifier matches an implicit parameter
                if (ImplicitParameters.Any(i => i.ParameterName == identifier.Name))
                {
                    // Only add if not already present (distinct)
                    if (!result.ContainsKey(identifier.Name))
                    {
                        // Resolve the type using the target type
                        var resolvedType = identifier.GetInferredType() ?? AnyTypeInfo.Instance;
                        result[identifier.Name] = resolvedType;
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
