using AppRefiner.Database;
using AppRefiner.LanguageExtensions;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;

namespace AppRefiner.Services
{
    /// <summary>
    /// Shared type-inference pipeline used by stylers, refactors with RequiresTypeInference,
    /// tooltips, autocomplete and the type error reporter. Populates node.Attributes["TypeInfo"]
    /// throughout the AST.
    /// </summary>
    public static class TypeInferenceRunner
    {
        /// <summary>
        /// Runs inference on <paramref name="program"/> as the program open in <paramref name="editor"/>.
        /// Without a resolver (no database) only the program's own types are known.
        /// </summary>
        public static void Run(ProgramNode program, ScintillaEditor editor,
            ITypeMetadataResolver? typeResolver, TypeExtensionManager? typeExtensionManager)
        {
            string qualifiedName = DetermineQualifiedName(program, editor);
            var programMetadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);

            string? defaultRecord = null;
            string? defaultField = null;
            if (editor.Caption?.EndsWith("(Record PeopleCode)") == true)
            {
                var parts = qualifiedName.Split('.');
                if (parts.Length >= 2)
                {
                    defaultRecord = parts[0];
                    defaultField = parts[1];
                }
            }

            TypeInferenceVisitor.Run(
                program,
                programMetadata,
                typeResolver ?? PeopleCodeTypeInfo.Contracts.NullTypeMetadataResolver.Instance,
                defaultRecord,
                defaultField,
                inferAutoDeclaredTypes: false,
                onUndefinedVariable: typeExtensionManager != null ? typeExtensionManager.HandleUndefinedVariable : null);
        }

        public static string DetermineQualifiedName(ProgramNode node, ScintillaEditor editor)
        {
            // Try to extract from AST structure first
            if (node.AppClass != null)
            {
                var className = node.AppClass.Name;

                if (editor?.Caption != null && !string.IsNullOrWhiteSpace(editor.Caption))
                {
                    var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
                    if (openTarget != null)
                    {
                        var methodIndex = Array.IndexOf(openTarget.ObjectIDs, PSCLASSID.METHOD);
                        if (methodIndex >= 0)
                        {
                            openTarget.ObjectIDs[methodIndex] = PSCLASSID.NONE;
                            openTarget.ObjectValues[methodIndex] = null;
                        }
                        return openTarget.Path;
                    }
                    else
                    {
                        return className;
                    }
                }
                else
                {
                    return className;
                }
            }
            else
            {
                // For function libraries or other programs
                if (editor?.Caption != null && !string.IsNullOrWhiteSpace(editor.Caption))
                {
                    var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
                    if (openTarget != null)
                    {
                        return string.Join(".", openTarget.ObjectValues);
                    }
                }

                return "Program";
            }
        }

        /// <summary>
        /// Renders a TypeInfo as declared-type syntax; anything that doesn't look like
        /// a legal type token conservatively declares as "any".
        /// </summary>
        public static string RenderDeclaredType(PeopleCodeTypeInfo.Types.TypeInfo? typeInfo)
        {
            var name = typeInfo?.Name;
            if (string.IsNullOrEmpty(name))
            {
                return "any";
            }

            return System.Text.RegularExpressions.Regex.IsMatch(
                name, @"^(array of )*[A-Za-z][A-Za-z0-9_]*(:[A-Za-z0-9_]+)*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                ? name
                : "any";
        }
    }
}
