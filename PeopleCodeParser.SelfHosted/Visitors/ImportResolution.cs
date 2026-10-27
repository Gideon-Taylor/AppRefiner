using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// Expands short app class names the way App Designer does on save. Only the program being
/// edited can hold short names: saved programs, and so all fetched metadata, are qualified.
/// </summary>
public static class ImportResolution
{
    /// <summary>
    /// Maps each short class name the program can use to its qualified name: explicit imports,
    /// wildcard imports the resolver can list, and a class's own name (which needs no import).
    /// The first import of a name wins; App Designer reports the ambiguity anyway.
    /// </summary>
    public static Dictionary<string, string> BuildImportMap(
        ProgramNode program, string? ownQualifiedName, ITypeMetadataResolver? resolver)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var import in program.Imports)
        {
            if (import.IsWildcard)
            {
                if (resolver == null)
                    continue;

                var packagePath = string.Join(":", import.PackagePath);
                foreach (var className in resolver.GetClassesInPackage(packagePath))
                    map.TryAdd(className, string.Join(":", import.PackagePath.Append(className)));
            }
            else
            {
                map.TryAdd(import.ClassName!, import.FullPath);
            }
        }

        // An explicit import of the same short name wins (App Designer reports that conflict anyway)
        if (program.AppClass != null && !string.IsNullOrEmpty(ownQualifiedName))
            map.TryAdd(program.AppClass.Name, ownQualifiedName);

        return map;
    }

    /// <summary>
    /// The qualified name of the class an <c>extends</c>/<c>implements</c> clause names in the
    /// program being edited (a builtin base keeps its type name), or null without one.
    /// </summary>
    public static string? BaseTypeName(AppClassNode appClass, ITypeMetadataResolver? resolver) =>
        appClass.BaseType switch
        {
            null => null,
            AppClassTypeNode baseClass => QualifiedName(baseClass, resolver),
            var baseType => baseType.TypeName
        };

    /// <summary>
    /// The qualified name of one class reference in the program being edited (an
    /// <c>extends</c> clause, say), without building the whole import map: imports are tried
    /// in order, and a wildcard package is only listed when no earlier import named the class.
    /// A qualified or unresolvable reference is returned as written.
    /// </summary>
    public static string QualifiedName(AppClassTypeNode type, ITypeMetadataResolver? resolver)
    {
        AstNode? node = type;
        while (node != null && node is not ProgramNode)
            node = node.Parent;
        if (type.PackagePath.Count > 0 || node is not ProgramNode program)
            return type.QualifiedName;

        foreach (var import in program.Imports)
        {
            if (!import.IsWildcard)
            {
                if (import.ClassName!.Equals(type.ClassName, StringComparison.OrdinalIgnoreCase))
                    return import.FullPath;
            }
            else if (resolver != null
                && resolver.GetClassesInPackage(string.Join(":", import.PackagePath))
                    .Any(c => c.Equals(type.ClassName, StringComparison.OrdinalIgnoreCase)))
            {
                return string.Join(":", import.PackagePath.Append(type.ClassName));
            }
        }

        return type.QualifiedName;
    }

    /// <summary>
    /// Qualifies a bare class name through the import map. Qualified or unmapped names are
    /// returned unchanged.
    /// </summary>
    public static string Qualify(string path, IReadOnlyDictionary<string, string> importMap) =>
        !path.Contains(':') && importMap.TryGetValue(path, out var qualified) ? qualified : path;

    /// <summary>
    /// Qualifies the bare class names in a program's own metadata (signatures, properties,
    /// instance variables, extends/implements) in place. Idempotent.
    /// </summary>
    public static void QualifyClassNames(TypeMetadata metadata, IReadOnlyDictionary<string, string> importMap)
    {
        if (!string.IsNullOrEmpty(metadata.BaseClassName) && !metadata.IsBaseClassBuiltin)
            metadata.BaseClassName = Qualify(metadata.BaseClassName, importMap);
        if (!string.IsNullOrEmpty(metadata.InterfaceName))
            metadata.InterfaceName = Qualify(metadata.InterfaceName, importMap);

        var functions = metadata.Methods.Values.AsEnumerable();
        if (metadata.Constructor != null)
            functions = functions.Append(metadata.Constructor);
        foreach (var function in functions)
        {
            function.ReturnType = Qualify(function.ReturnType, importMap);
            if (function.RefinedReturnType is { } refinedReturn)
                function.RefinedReturnType = Qualify(refinedReturn, importMap);
            foreach (var parameter in function.Parameters)
                QualifyParameter(parameter, importMap);
        }

        foreach (var property in metadata.Properties.Values.Concat(metadata.InstanceVariables.Values))
        {
            if (property.AppClassPath != null)
                property.AppClassPath = Qualify(property.AppClassPath, importMap);
            if (property.UnionTypes != null)
                for (var i = 0; i < property.UnionTypes.Count; i++)
                    property.UnionTypes[i] = Qualify(property.UnionTypes[i], importMap);
            if (property.RefinedType is { } refined)
                property.RefinedType = Qualify(refined, importMap);
        }
    }

    private static void QualifyParameter(Parameter parameter, IReadOnlyDictionary<string, string> importMap)
    {
        switch (parameter)
        {
            case SingleParameter single:
                single.ParameterType = Qualify(single.ParameterType, importMap);
                if (single.RefinedParameterType is { } refined)
                    single.RefinedParameterType = Qualify(refined, importMap);
                break;
            case UnionParameter union:
                for (var i = 0; i < union.AllowedTypes.Count; i++)
                    union.AllowedTypes[i] = Qualify(union.AllowedTypes[i], importMap);
                break;
            case ParameterGroup group:
                foreach (var inner in group.Parameters)
                    QualifyParameter(inner, importMap);
                break;
            case VariableParameter variable:
                QualifyParameter(variable.InnerParameter, importMap);
                break;
        }
    }

    private static TypeWithDimensionality Qualify(TypeWithDimensionality type, IReadOnlyDictionary<string, string> importMap)
    {
        if (type.IsAppClass)
            type.AppClassPath = Qualify(type.AppClassPath!, importMap);
        return type;
    }
}
