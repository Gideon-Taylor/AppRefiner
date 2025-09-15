using System;
using System.Collections.Generic;
using System.Linq;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Extracts <see cref="ClassTypeInfo"/> metadata from parsed PeopleCode application classes.
/// </summary>
internal static class ClassMetadataBuilder
{
    public static ClassTypeInfo? Build(ProgramNode program, string? overrideQualifiedName = null)
    {
        if (program == null)
        {
            return null;
        }

        if (program.AppClass != null)
        {
            return Build(program.AppClass, overrideQualifiedName);
        }

        if (program.Interface != null)
        {
            return Build(program.Interface, overrideQualifiedName);
        }

        return null;
    }

    public static ClassTypeInfo Build(InterfaceNode interfaceNode, string? overrideQualifiedName = null)
    {
        if (interfaceNode == null)
        {
            throw new ArgumentNullException(nameof(interfaceNode));
        }

        var qualifiedName = overrideQualifiedName ?? interfaceNode.Name;
        var baseInterfaceName = GetAppClassName(interfaceNode.BaseInterface);

        var implementedInterfaces = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseInterfaceName))
        {
            implementedInterfaces.Add(baseInterfaceName!);
        }

        var properties = interfaceNode.Properties
            .Select(p => CreatePropertyInfo(p, new Dictionary<AstNode, MemberAccessibility>()))
            .Where(p => p != null)
            .Cast<ClassPropertyInfo>()
            .ToList();

        var methods = interfaceNode.Methods
            .Select(m => CreateMethodInfo(m, new Dictionary<AstNode, MemberAccessibility>()))
            .Where(m => m != null)
            .Cast<ClassMethodInfo>()
            .ToList();

        MergeInterfaceMembers(implementedInterfaces, properties, methods);

        return new ClassTypeInfo(
            qualifiedName,
            baseInterfaceName,
            implementedInterfaces,
            properties,
            methods,
            isInterface: true);
    }

    public static ClassTypeInfo Build(AppClassNode classNode, string? overrideQualifiedName = null)
    {
        if (classNode == null)
        {
            throw new ArgumentNullException(nameof(classNode));
        }

        var qualifiedName = overrideQualifiedName ?? classNode.NameToken?.Text ?? classNode.Name;
        var baseClassName = GetAppClassName(classNode.BaseClass);
        var interfaces = new List<string>();
        if (classNode.ImplementedInterface != null)
        {
            var interfaceName = GetAppClassName(classNode.ImplementedInterface);
            if (!string.IsNullOrWhiteSpace(interfaceName))
            {
                interfaces.Add(interfaceName!);
            }
        }

        var visibilityMap = BuildVisibilityMap(classNode);

        var properties = classNode.Properties
            .Select(p => CreatePropertyInfo(p, visibilityMap))
            .Where(p => p != null)
            .Cast<ClassPropertyInfo>()
            .ToList();

        var methods = classNode.Methods
            .Select(m => CreateMethodInfo(m, visibilityMap))
            .Where(m => m != null)
            .Cast<ClassMethodInfo>()
            .ToList();

        var constructor = classNode.Methods
            .Select(m => CreateConstructorInfo(m))
            .FirstOrDefault(c => c != null);

        MergeInterfaceMembers(interfaces, properties, methods);

        return new ClassTypeInfo(
            qualifiedName,
            baseClassName,
            interfaces,
            properties,
            methods,
            constructor);
    }

    private static ClassPropertyInfo? CreatePropertyInfo(PropertyNode property, IDictionary<AstNode, MemberAccessibility> visibilityMap)
    {
        if (property == null)
        {
            return null;
        }

        var accessibility = visibilityMap.TryGetValue(property, out var value)
            ? value
            : MemberAccessibility.Public;

        var typeInfo = ConvertTypeNode(property.Type) ?? AnyTypeInfo.Instance;

        return new ClassPropertyInfo(property.Name, accessibility, typeInfo);
    }

    private static ClassMethodInfo? CreateMethodInfo(MethodNode method, IDictionary<AstNode, MemberAccessibility> visibilityMap)
    {
        if (method == null)
        {
            return null;
        }

        var accessibility = visibilityMap.TryGetValue(method, out var value)
            ? value
            : MemberAccessibility.Public;

        var parameterInfos = new List<TypeInfo>();

        foreach (var parameter in method.Parameters)
        {
            var paramTypeInfo = ConvertTypeNode(parameter.Type) ?? AnyTypeInfo.Instance;
            parameterInfos.Add(paramTypeInfo);
        }

        var returnTypeInfo = method.ReturnType != null
            ? ConvertTypeNode(method.ReturnType) ?? AnyTypeInfo.Instance
            : VoidTypeInfo.Instance;

        // Function signature details will be handled by external function resolution system
        return new ClassMethodInfo(method.Name, accessibility, parameterInfos, returnTypeInfo);
    }

    private static ClassConstructorInfo? CreateConstructorInfo(MethodNode method)
    {
        if (method == null || !method.IsConstructor)
        {
            return null;
        }

        var parameterInfos = new List<TypeInfo>();

        foreach (var parameter in method.Parameters)
        {
            var paramTypeInfo = ConvertTypeNode(parameter.Type) ?? AnyTypeInfo.Instance;
            parameterInfos.Add(paramTypeInfo);
        }

        // Function signature details will be handled by external function resolution system
        return new ClassConstructorInfo(parameterInfos);
    }

    private static IDictionary<AstNode, MemberAccessibility> BuildVisibilityMap(AppClassNode classNode)
    {
        var result = new Dictionary<AstNode, MemberAccessibility>();

        foreach (var (modifier, members) in classNode.VisibilitySections)
        {
            var accessibility = modifier switch
            {
                VisibilityModifier.Protected => MemberAccessibility.Protected,
                VisibilityModifier.Private => MemberAccessibility.Private,
                _ => MemberAccessibility.Public
            };

            foreach (var member in members)
            {
                result[member] = accessibility;
            }
        }

        return result;
    }

    private static string? GetAppClassName(TypeNode? typeNode)
    {
        return typeNode switch
        {
            AppClassTypeNode appClass => appClass.QualifiedName,
            _ => typeNode?.TypeName
        };
    }

    private static void MergeInterfaceMembers(
        IEnumerable<string> interfaceNames,
        IList<ClassPropertyInfo> properties,
        IList<ClassMethodInfo> methods)
    {
        if (interfaceNames == null)
        {
            return;
        }

        var propertyNames = new HashSet<string>(properties.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        var methodNames = new HashSet<string>(methods.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var interfaceName in interfaceNames)
        {
            MergeInterfaceMembersRecursive(interfaceName, properties, methods, propertyNames, methodNames, visited);
        }
    }

    private static void MergeInterfaceMembersRecursive(
        string interfaceName,
        IList<ClassPropertyInfo> properties,
        IList<ClassMethodInfo> methods,
        HashSet<string> propertyNames,
        HashSet<string> methodNames,
        HashSet<string> visited)
    {
        if (string.IsNullOrWhiteSpace(interfaceName) || !visited.Add(interfaceName))
        {
            return;
        }

        if (!PeopleCodeTypeRegistry.TryGetClassInfo(interfaceName, out var interfaceInfo) || interfaceInfo == null)
        {
            return;
        }

        foreach (var property in interfaceInfo.Properties.Values)
        {
            if (propertyNames.Add(property.Name))
            {
                properties.Add(property);
            }
        }

        foreach (var method in interfaceInfo.Methods.Values)
        {
            if (methodNames.Add(method.Name))
            {
                methods.Add(method);
            }
        }

        foreach (var inheritedInterface in interfaceInfo.ImplementedInterfaces)
        {
            MergeInterfaceMembersRecursive(inheritedInterface, properties, methods, propertyNames, methodNames, visited);
        }
    }

    private static TypeInfo? ConvertTypeNode(TypeNode? typeNode)
    {
        if (typeNode == null)
        {
            return null;
        }

        return typeNode switch
        {
            BuiltInTypeNode builtin => TypeInfo.FromPeopleCodeType(builtin.Type),
            ArrayTypeNode array => new ArrayTypeInfo(array.Dimensions, ConvertTypeNode(array.ElementType)),
            AppClassTypeNode appClass => new AppClassTypeInfo(appClass.QualifiedName),
            _ => PeopleCodeTypeRegistry.GetTypeByName(typeNode.TypeName)
                ?? AnyTypeInfo.Instance
        };
    }

}
