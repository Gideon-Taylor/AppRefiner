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

        /// <summary>
        /// The open program's qualified name: PKG:Class for an app class, the dotted object path
        /// (REC.FIELD.Event, …) otherwise. Falls back to the class name, or "Program", when the
        /// caption doesn't parse.
        /// </summary>
        public static string DetermineQualifiedName(ProgramNode node, ScintillaEditor editor) =>
            QualifiedNameFromCaption(editor, isAppClass: node.AppClass != null)
            ?? node.AppClass?.Name
            ?? "Program";

        /// <summary>
        /// The open program's qualified name without a parsed program: an Application Package
        /// caption is taken to be an app class.
        /// </summary>
        public static string DetermineQualifiedName(ScintillaEditor editor) =>
            QualifiedNameFromCaption(editor, isAppClass: editor.Caption?.Contains("Application Package") == true)
            ?? "Program";

        private static string? QualifiedNameFromCaption(ScintillaEditor? editor, bool isAppClass)
        {
            if (string.IsNullOrWhiteSpace(editor?.Caption))
                return null;

            var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
            if (openTarget == null)
                return null;

            if (!isAppClass)
                return string.Join(".", openTarget.ObjectValues);

            // The caption names the method being edited; the class is the path without it
            var methodIndex = Array.IndexOf(openTarget.ObjectIDs, PSCLASSID.METHOD);
            if (methodIndex >= 0)
            {
                openTarget.ObjectIDs[methodIndex] = PSCLASSID.NONE;
                openTarget.ObjectValues[methodIndex] = null;
            }
            return openTarget.Path;
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
