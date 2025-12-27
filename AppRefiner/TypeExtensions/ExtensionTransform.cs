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
    }
}
