using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Base class for all type reference nodes
/// </summary>
public abstract class TypeNode : AstNode
{
    /// <summary>
    /// The name of the type as it appears in source code
    /// </summary>
    public abstract string TypeName { get; }

    /// <summary>
    /// True if this type can be assigned null values
    /// </summary>
    public virtual bool IsNullable => true;

    /// <summary>
    /// True if this is a built-in PeopleCode type
    /// </summary>
    public virtual bool IsBuiltIn => false;
}

/// <summary>
/// Built-in PeopleCode type (ANY, BOOLEAN, DATE, etc.)
/// </summary>
public class BuiltInTypeNode : TypeNode
{
    public PeopleCodeType Type { get; }

    public override string TypeName => Type.GetTypeName();
    public override bool IsBuiltIn => true;

    public BuiltInTypeNode(PeopleCodeType type)
    {
        Type = type;
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitBuiltInType(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitBuiltInType(this);
    }

    public override string ToString()
    {
        return TypeName;
    }
}

/// <summary>
/// Array type reference (ARRAY, ARRAY OF type, ARRAY2 OF type, etc.)
/// </summary>
public class ArrayTypeNode : TypeNode
{
    /// <summary>
    /// Number of dimensions (1 for ARRAY, 2 for ARRAY2, etc.)
    /// </summary>
    public int Dimensions { get; }

    /// <summary>
    /// Element type, null for untyped arrays
    /// </summary>
    public TypeNode? ElementType { get; set; }

    public override string TypeName
    {
        get
        {
            var arrayName = "array";
            for (var x = 2; x <= Dimensions; x++)
            {
                arrayName += " of array";
            }

            return ElementType != null ? $"{arrayName} of {ElementType.TypeName}" : arrayName;
        }
    }

    public ArrayTypeNode(int dimensions, TypeNode? elementType = null)
    {
        if (dimensions < 1 || dimensions > 9)
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Array dimensions must be between 1 and 9");

        Dimensions = dimensions;
        ElementType = elementType;

        if (elementType != null)
        {
            AddChild(elementType);
        }
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitArrayType(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitArrayType(this);
    }

    public override string ToString()
    {
        return TypeName;
    }
}

/// <summary>
/// Application class type reference (MyPackage:MyClass)
/// </summary>
public class AppClassTypeNode : TypeNode
{
    /// <summary>
    /// Fully qualified class name (e.g., "MyPackage:MyClass")
    /// </summary>
    public string QualifiedName { get; }

    /// <summary>
    /// Package path components (e.g., ["MyPackage"] for "MyPackage:MyClass")
    /// </summary>
    public IReadOnlyList<string> PackagePath { get; }

    /// <summary>
    /// Simple class name (e.g., "MyClass" from "MyPackage:MyClass")
    /// </summary>
    public string ClassName { get; }

    public override string TypeName => QualifiedName;
    public override bool IsNullable => true;

    public AppClassTypeNode(string qualifiedName)
    {
        QualifiedName = qualifiedName ?? throw new ArgumentNullException(nameof(qualifiedName));

        var parts = qualifiedName.Split(':');
        if (parts.Length == 1)
        {
            PackagePath = Array.Empty<string>();
            ClassName = parts[0];
        }
        else
        {
            PackagePath = parts.Take(parts.Length - 1).ToArray();
            ClassName = parts[^1];
        }
    }

    public AppClassTypeNode(IEnumerable<string> packagePath, string className)
    {
        var packageList = packagePath?.ToList() ?? throw new ArgumentNullException(nameof(packagePath));
        ClassName = className ?? throw new ArgumentNullException(nameof(className));

        PackagePath = packageList.AsReadOnly();
        QualifiedName = packageList.Count > 0
            ? string.Join(":", packageList) + ":" + className
            : className;
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitAppClassType(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitAppClassType(this);
    }

    public override string ToString()
    {
        return QualifiedName;
    }
}

/// <summary>
/// Application package wildcard import type (MyPackage:*)
/// </summary>
public class AppPackageWildcardTypeNode : TypeNode
{
    /// <summary>
    /// Package path components (e.g., ["MyPackage", "SubPackage"] for "MyPackage:SubPackage:*")
    /// </summary>
    public IReadOnlyList<string> PackagePath { get; }

    /// <summary>
    /// Full wildcard import path (e.g., "MyPackage:SubPackage:*")
    /// </summary>
    public string WildcardPath { get; }

    public override string TypeName => WildcardPath;
    public override bool IsNullable => false; // Package wildcards are not nullable types

    public AppPackageWildcardTypeNode(IEnumerable<string> packagePath)
    {
        var packageList = packagePath?.ToList() ?? throw new ArgumentNullException(nameof(packagePath));
        if (packageList.Count == 0)
            throw new ArgumentException("Package path cannot be empty", nameof(packagePath));

        PackagePath = packageList.AsReadOnly();
        WildcardPath = string.Join(":", packageList) + ":*";
    }

    public AppPackageWildcardTypeNode(string wildcardPath)
    {
        if (string.IsNullOrWhiteSpace(wildcardPath))
            throw new ArgumentException("Wildcard path cannot be empty", nameof(wildcardPath));

        if (!wildcardPath.EndsWith(":*"))
            throw new ArgumentException("Wildcard path must end with ':*'", nameof(wildcardPath));

        WildcardPath = wildcardPath;

        var parts = wildcardPath.Split(':');
        if (parts.Length < 2)
            throw new ArgumentException("Invalid wildcard path format", nameof(wildcardPath));

        // Remove the '*' part to get package path
        PackagePath = parts.Take(parts.Length - 1).ToArray();
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitAppPackageWildcardType(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitAppPackageWildcardType(this);
    }

    public override string ToString()
    {
        return WildcardPath;
    }
}


