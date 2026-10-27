using PeopleCodeParser.SelfHosted.Annotations;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Tooltip text for types that carry more than their declarable name. API subtypes show
    /// as "ApiObject (Session)" because Session itself can't be used in a declaration, and
    /// records, fields and component interfaces add the definition they are known to be,
    /// e.g. "record (Record.PSOPRDEFN)", and CI items and collections their path from the CI root,
    /// e.g. "ApiObject (CompIntfc.USER_PROFILE.Roles[])".
    /// </summary>
    internal static class TypeDisplay
    {
        /// <summary>The type's name, with its specific API type or definition in parentheses when known.</summary>
        public static string Format(TypeInfo type) =>
            Detail(type) is { } detail
                ? $"{(IsApiSubtype(type.Name) ? "ApiObject" : type.Name)} ({detail})"
                : type.Name;

        /// <summary>The specific API type or definition name, or null when the type has none.</summary>
        public static string? Detail(TypeInfo type) => type switch
        {
            RecordTypeInfo { RecordName.Length: > 0 } record => $"Record.{record.RecordName}",
            FieldTypeInfo { FieldName.Length: > 0 } field => FieldName(field.RecordName, field.FieldName),
            BuiltinObjectTypeInfo { IsApiObjectBase: false } api when IsApiSubtype(api.Name) =>
                TypeAnnotationParser.FormatCompIntfc(api.PeopleCodeType, api.Identity) ?? api.Name,
            _ => null
        };

        /// <summary>The specific API type or definition named by a <c>/* as T */</c> annotation.</summary>
        public static string Detail(TypeWithDimensionality annotation)
        {
            var identity = annotation.Identity;
            if (TypeAnnotationParser.FormatCompIntfc(annotation.Type, identity) is { } compIntfc)
                return compIntfc;
            if (!string.IsNullOrEmpty(identity?.FieldName))
                return FieldName(identity.RecordName, identity.FieldName);
            if (!string.IsNullOrEmpty(identity?.RecordName))
                return $"Record.{identity.RecordName}";
            return annotation.Type.GetTypeName();
        }

        private static string FieldName(string? recordName, string fieldName) =>
            string.IsNullOrEmpty(recordName) ? $"Field.{fieldName}" : $"Record.{recordName}.{fieldName}";

        private static bool IsApiSubtype(string name) => PeopleCodeTypeDatabase.IsApiObjectSubtype(name);
    }
}
