using PeopleCodeParser.SelfHosted;

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
    public BuiltInType Type { get; }

    public override string TypeName => Type.ToString().ToUpper();
    public override bool IsBuiltIn => true;

    public BuiltInTypeNode(BuiltInType type)
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
            var arrayName = Dimensions == 1 ? "ARRAY" : $"ARRAY{Dimensions}";
            return ElementType != null ? $"{arrayName} OF {ElementType.TypeName}" : arrayName;
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
/// Built-in PeopleCode types
/// </summary>
public enum BuiltInType
{
    Any,
    Boolean,
    Date,
    DateTime,
    Float,
    Integer,
    Number,
    String,
    Time
}

/// <summary>
/// Extension methods for BuiltInType
/// </summary>
public static class BuiltInTypeExtensions
{
    /// <summary>
    /// Get the PeopleCode keyword for this built-in type
    /// </summary>
    public static string ToKeyword(this BuiltInType type)
    {
        return type switch
        {
            BuiltInType.Any => "ANY",
            BuiltInType.Boolean => "BOOLEAN",
            BuiltInType.Date => "DATE",
            BuiltInType.DateTime => "DATETIME",
            BuiltInType.Float => "FLOAT",
            BuiltInType.Integer => "INTEGER",
            BuiltInType.Number => "NUMBER",
            BuiltInType.String => "STRING",
            BuiltInType.Time => "TIME",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    /// <summary>
    /// Parse a PeopleCode keyword into a BuiltInType
    /// </summary>
    public static BuiltInType? TryParseKeyword(string keyword)
    {
        return keyword?.ToUpperInvariant() switch
        {
            "ANY" => BuiltInType.Any,
            "BOOLEAN" => BuiltInType.Boolean,
            "DATE" => BuiltInType.Date,
            "DATETIME" => BuiltInType.DateTime,
            "FLOAT" => BuiltInType.Float,
            "INTEGER" => BuiltInType.Integer,
            "NUMBER" => BuiltInType.Number,
            "STRING" => BuiltInType.String,
            "TIME" => BuiltInType.Time,
            _ => null
        };
    }

    /// <summary>
    /// True if this type is numeric (can participate in arithmetic operations)
    /// </summary>
    public static bool IsNumeric(this BuiltInType type)
    {
        return type is BuiltInType.Integer or BuiltInType.Float or BuiltInType.Number;
    }

    /// <summary>
    /// True if this type represents a date/time value
    /// </summary>
    public static bool IsDateTime(this BuiltInType type)
    {
        return type is BuiltInType.Date or BuiltInType.DateTime or BuiltInType.Time;
    }

    /// <summary>
    /// True if this type can be implicitly converted from string
    /// </summary>
    public static bool IsStringConvertible(this BuiltInType type)
    {
        return type != BuiltInType.Boolean; // All types except boolean can be converted from string
    }
}