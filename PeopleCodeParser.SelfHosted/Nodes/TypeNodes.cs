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
            var arrayName = "ARRAY";
            for(var x = 2; x <= Dimensions; x++)
            {
                arrayName += " OF ARRAY";
            }

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
/// Built-in PeopleCode types including primitive types and object types
/// </summary>
public enum BuiltInType
{
    // Primitive types
    Any,
    Boolean,
    Date,
    DateTime,
    Exception,
    Float,
    Integer,
    Number,
    String,
    Time,
    
    // Core PeopleSoft objects
    Field,
    Record,
    Page,
    Component,
    Menu,
    MenuGroup,
    
    // Data and XML objects
    XmlDoc,
    XmlNode,
    JsonObject,
    JsonArray,
    Rowset,
    Row,
    DataSource,
    Query,
    Tree,
    TreeNode,
    
    // UI and graphics objects
    Chart,
    Image,
    Grid,
    GridColumn,
    TreeControl,
    PushButton,
    CheckBox,
    RadioButton,
    DropDownList,
    EditBox,
    LongEditBox,
    RichTextBox,
    ScrollArea,
    SubPage,
    GroupBox,
    Static,
    Frame,
    TabPage,
    
    // File and I/O objects
    File,
    FileLayout,
    Message,
    MsgGet,
    MsgGetText,
    
    // Security and session objects
    Session,
    Request,
    Response,
    Portal,
    Node,
    ProcessRequest,
    ContentReference,
    Url,
    
    // Analytics and reporting objects
    AnalyticGrid,
    PivotGrid,
    Cube,
    Dimension,
    
    // Integration objects
    SoapDoc,
    HttpRequest,
    HttpResponse,
    FtpClient,
    LdapEntry,
    SmtpClient,
    EmailMessage,
    
    // Workflow and approval objects
    WorklistEntry,
    ApprovalInstance,
    WorkflowInstance,
    
    // Meta-data objects
    MetaField,
    MetaRecord,
    MetaPage,
    MetaComponent,
    MetaMenu,
    
    // Array and collection objects
    Array,
    Collection,
    Stack,
    Queue,
    Dictionary,
    
    // Database objects
    DbField,
    Sql,
    GetLevel0,
    CreateLevel0,
    
    // PeopleTools objects
    ProcessScheduler,
    Application,
    PeopleCodeEvent,
    Transform,
    Channel,
    Publication,
    Subscription,
    
    // Financial objects (if using Financials)
    Voucher,
    Journal,
    Ledger,
    ChartField,
    SetId,
    
    // HCM objects (if using HCM)
    Job,
    Position,
    Employee,
    Person,
    Compensation,
    
    // Campus objects (if using Campus Solutions)
    Student,
    Course,
    Class,
    Term,
    Institution
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
        // For built-in object types, just return the enum name as-is
        // For primitive types, return the uppercase keyword
        return type switch
        {
            // Primitive types (uppercase keywords)
            BuiltInType.Any => "ANY",
            BuiltInType.Boolean => "BOOLEAN",
            BuiltInType.Date => "DATE",
            BuiltInType.DateTime => "DATETIME",
            BuiltInType.Exception => "EXCEPTION",
            BuiltInType.Float => "FLOAT",
            BuiltInType.Integer => "INTEGER",
            BuiltInType.Number => "NUMBER",
            BuiltInType.String => "STRING",
            BuiltInType.Time => "TIME",
            
            // Object types (use enum name directly)
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Parse a PeopleCode keyword into a BuiltInType
    /// </summary>
    public static BuiltInType? TryParseKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return null;

        // First try primitive type keywords (case-insensitive)
        var result = keyword.ToUpperInvariant() switch
        {
            "ANY" => BuiltInType.Any,
            "BOOLEAN" => BuiltInType.Boolean,
            "DATE" => BuiltInType.Date,
            "DATETIME" => BuiltInType.DateTime,
            "EXCEPTION" => BuiltInType.Exception,
            "FLOAT" => BuiltInType.Float,
            "INTEGER" => BuiltInType.Integer,
            "NUMBER" => BuiltInType.Number,
            "STRING" => BuiltInType.String,
            "TIME" => BuiltInType.Time,
            _ => (BuiltInType?)null
        };
        
        if (result.HasValue)
            return result;

        // Try to parse as an object type enum value (case-insensitive)
        if (Enum.TryParse<BuiltInType>(keyword, ignoreCase: true, out var objectType))
        {
            return objectType;
        }

        return null;
    }

    /// <summary>
    /// True if this type is a primitive built-in type (not an object type)
    /// </summary>
    public static bool IsPrimitiveType(this BuiltInType type)
    {
        return type is BuiltInType.Any or BuiltInType.Boolean or BuiltInType.Date or 
               BuiltInType.DateTime or BuiltInType.Exception or BuiltInType.Float or 
               BuiltInType.Integer or BuiltInType.Number or BuiltInType.String or BuiltInType.Time;
    }

    /// <summary>
    /// True if this type is a built-in object type (not a primitive)
    /// </summary>
    public static bool IsObjectType(this BuiltInType type)
    {
        return !IsPrimitiveType(type);
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

    /// <summary>
    /// True if this type represents an exception object
    /// </summary>
    public static bool IsException(this BuiltInType type)
    {
        return type is BuiltInType.Exception;
    }

    /// <summary>
    /// True if this type can be used as a base class/interface
    /// </summary>
    public static bool CanBeBaseType(this BuiltInType type)
    {
        return type is BuiltInType.Exception or BuiltInType.Any;
    }

    /// <summary>
    /// True if this type is nullable (can hold null values)
    /// </summary>
    public static bool IsNullable(this BuiltInType type)
    {
        // In PeopleCode, most types can be null, but primitives typically default to zero/empty
        return type is not (BuiltInType.Integer or BuiltInType.Float or BuiltInType.Number or BuiltInType.Boolean);
    }
}

