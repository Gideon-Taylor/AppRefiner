using System;
using System.Collections.Generic;
using System.Linq;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Analysis;

/// <summary>
/// Visibility for class members that the type system tracks.
/// </summary>
public enum MemberAccessibility
{
    Public,
    Protected,
    Private
}

/// <summary>
/// Describes a property on an application class, including its type and visibility.
/// </summary>
public sealed class ClassPropertyInfo
{
    public string Name { get; }
    public MemberAccessibility Accessibility { get; }
    public TypeInfo Type { get; }

    public ClassPropertyInfo(string name, MemberAccessibility accessibility, TypeInfo type)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Accessibility = accessibility;
    }
}

/// <summary>
/// Describes a method on an application class, including parameters and return type.
/// Note: Function signature details will be provided by external function resolution system.
/// </summary>
public sealed class ClassMethodInfo
{
    public string Name { get; }
    public MemberAccessibility Accessibility { get; }
    public IReadOnlyList<TypeInfo> ParameterTypes { get; }
    public TypeInfo ReturnType { get; }

    public ClassMethodInfo(string name, MemberAccessibility accessibility, IEnumerable<TypeInfo>? parameterTypes = null, TypeInfo? returnType = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Accessibility = accessibility;
        ParameterTypes = parameterTypes?.ToArray() ?? Array.Empty<TypeInfo>();
        ReturnType = returnType ?? AnyTypeInfo.Instance;
    }
}

/// <summary>
/// Describes a constructor for an application class.
/// Note: Function signature details will be provided by external function resolution system.
/// </summary>
public sealed class ClassConstructorInfo
{
    public IReadOnlyList<TypeInfo> ParameterTypes { get; }

    public ClassConstructorInfo(IEnumerable<TypeInfo>? parameterTypes = null)
    {
        ParameterTypes = parameterTypes?.ToArray() ?? Array.Empty<TypeInfo>();
    }
}

/// <summary>
/// Aggregated metadata about an application class required for member access inference.
/// </summary>
public sealed class ClassTypeInfo
{
    public string QualifiedName { get; }
    public string? BaseClassName { get; }
    public IReadOnlyList<string> ImplementedInterfaces { get; }
    public IReadOnlyDictionary<string, ClassPropertyInfo> Properties { get; }
    public IReadOnlyDictionary<string, ClassMethodInfo> Methods { get; }
    public ClassConstructorInfo? Constructor { get; }
    public bool IsInterface { get; }

    public ClassTypeInfo(
        string qualifiedName,
        string? baseClassName = null,
        IEnumerable<string>? implementedInterfaces = null,
        IEnumerable<ClassPropertyInfo>? properties = null,
        IEnumerable<ClassMethodInfo>? methods = null,
        ClassConstructorInfo? constructor = null,
        bool isInterface = false)
    {
        QualifiedName = qualifiedName ?? throw new ArgumentNullException(nameof(qualifiedName));
        BaseClassName = baseClassName;

        ImplementedInterfaces = implementedInterfaces?.ToArray() ?? Array.Empty<string>();

        Properties = BuildPropertyDictionary(properties);
        Methods = BuildMethodDictionary(methods);
        Constructor = constructor;
        IsInterface = isInterface;
    }

    private static IReadOnlyDictionary<string, ClassPropertyInfo> BuildPropertyDictionary(IEnumerable<ClassPropertyInfo>? properties)
    {
        if (properties == null)
        {
            return new Dictionary<string, ClassPropertyInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, ClassPropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties)
        {
            result[property.Name] = property;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ClassMethodInfo> BuildMethodDictionary(IEnumerable<ClassMethodInfo>? methods)
    {
        if (methods == null)
        {
            return new Dictionary<string, ClassMethodInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, ClassMethodInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in methods)
        {
            result[method.Name] = method;
        }

        return result;
    }
}
