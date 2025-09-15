namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Enumeration of all valid variable types in PeopleCode for efficient storage and comparison.
/// Each value fits in a single byte (0-255) for compact data format storage.
/// Includes primitive types, builtin objects, special types, and user-defined AppClass types.
/// </summary>
public enum PeopleCodeType : byte
{
    // Special types (0-9)
    /// <summary>
    /// The special "Any" type that can hold any value
    /// </summary>
    Any = 0,

    /// <summary>
    /// No return value (like void in other languages)
    /// </summary>
    Void = 1,

    /// <summary>
    /// User-defined application class - triggers deeper type resolution
    /// </summary>
    AppClass = 2,

    /// <summary>
    /// Unknown/unresolved type
    /// </summary>
    Unknown = 3,

    /// <summary>
    /// Reference type for named references like HTML.OBJECT_NAME, SQL.FOO, RECORD.FOO
    /// Also includes dynamic references via @ expressions
    /// </summary>
    Reference = 4,

    /// <summary>
    /// Generic Object reference, can hold anything but primitives/strings
    /// </summary>
    Object = 5,
    // Primitive types (10-19)
    /// <summary>
    /// String primitive type
    /// </summary>
    String = 10,

    /// <summary>
    /// Integer primitive type
    /// </summary>
    Integer = 11,

    /// <summary>
    /// Number primitive type
    /// </summary>
    Number = 12,

    /// <summary>
    /// Date primitive type
    /// </summary>
    Date = 13,

    /// <summary>
    /// DateTime primitive type
    /// </summary>
    DateTime = 14,

    /// <summary>
    /// Time primitive type
    /// </summary>
    Time = 15,

    /// <summary>
    /// Boolean primitive type
    /// </summary>
    Boolean = 16,

    // Builtin object types (20-255)
    /// <summary>
    /// AES encryption section object
    /// </summary>
    Aesection = 20,

    /// <summary>
    /// Analytic grid object
    /// </summary>
    Analyticgrid = 21,

    /// <summary>
    /// Analytic grid column object
    /// </summary>
    Analyticgridcolumn = 22,

    /// <summary>
    /// Analytic instance object
    /// </summary>
    Analyticinstance = 23,

    /// <summary>
    /// Analytic model object
    /// </summary>
    Analyticmodel = 24,

    /// <summary>
    /// API object
    /// </summary>
    Apiobject = 25,

    /// <summary>
    /// BI docs object
    /// </summary>
    Bidocs = 26,

    /// <summary>
    /// Chart object
    /// </summary>
    Chart = 27,

    /// <summary>
    /// Collection object
    /// </summary>
    Collection = 28,

    /// <summary>
    /// Composite query object
    /// </summary>
    Compositequery = 29,

    /// <summary>
    /// Compound object
    /// </summary>
    Compound = 30,

    /// <summary>
    /// Cookie object
    /// </summary>
    Cookie = 31,

    /// <summary>
    /// CQ chunked processor object
    /// </summary>
    Cqchunkedprocessor = 32,

    /// <summary>
    /// CQ runtime object
    /// </summary>
    Cqruntime = 33,

    /// <summary>
    /// Cryptography object
    /// </summary>
    Crypt = 34,

    /// <summary>
    /// Cube collection object
    /// </summary>
    Cubecollection = 35,

    /// <summary>
    /// Dial gauge object
    /// </summary>
    Dialgauge = 36,

    /// <summary>
    /// Document object
    /// </summary>
    Document = 37,

    /// <summary>
    /// Document key object
    /// </summary>
    Documentkey = 38,

    /// <summary>
    /// Field object
    /// </summary>
    Field = 39,

    /// <summary>
    /// Field definition object
    /// </summary>
    Fielddefn = 40,

    /// <summary>
    /// File object
    /// </summary>
    File = 41,

    /// <summary>
    /// File definition object
    /// </summary>
    Filedefn = 42,

    /// <summary>
    /// File definition element object
    /// </summary>
    Filedefnelement = 43,

    /// <summary>
    /// File definition element collection object
    /// </summary>
    Filedefnelementcollection = 44,

    /// <summary>
    /// File definition manager object
    /// </summary>
    Filedefnmanager = 45,

    /// <summary>
    /// File definition map object
    /// </summary>
    Filedefnmap = 46,

    /// <summary>
    /// File metadata object
    /// </summary>
    Filemetadata = 47,

    /// <summary>
    /// Gantt chart object
    /// </summary>
    Gantt = 48,

    /// <summary>
    /// Gauge chart object
    /// </summary>
    Gaugechart = 49,

    /// <summary>
    /// Gauge threshold object
    /// </summary>
    Gaugethreshold = 50,

    /// <summary>
    /// Grid object
    /// </summary>
    Grid = 51,

    /// <summary>
    /// Grid column object
    /// </summary>
    Gridcolumn = 52,

    /// <summary>
    /// IB connector info object
    /// </summary>
    Ibconnectorinfo = 53,

    /// <summary>
    /// IB info object
    /// </summary>
    Ibinfo = 54,

    /// <summary>
    /// Integration broker object
    /// </summary>
    Intbroker = 55,

    /// <summary>
    /// Interlink object
    /// </summary>
    Interlink = 56,

    /// <summary>
    /// Java object
    /// </summary>
    Javaobject = 57,

    /// <summary>
    /// JSON array object
    /// </summary>
    Jsonarray = 58,

    /// <summary>
    /// JSON builder object
    /// </summary>
    Jsonbuilder = 59,

    /// <summary>
    /// JSON generator object
    /// </summary>
    Jsongenerator = 60,

    /// <summary>
    /// JSON node object
    /// </summary>
    Jsonnode = 61,

    /// <summary>
    /// JSON object
    /// </summary>
    Jsonobject = 62,

    /// <summary>
    /// JSON parser object
    /// </summary>
    Jsonparser = 63,

    /// <summary>
    /// JSON value object
    /// </summary>
    Jsonvalue = 64,

    /// <summary>
    /// LED gauge object
    /// </summary>
    Ledgauge = 65,

    /// <summary>
    /// Map object
    /// </summary>
    Map = 66,

    /// <summary>
    /// Map element object
    /// </summary>
    Mapelement = 67,

    /// <summary>
    /// Map page object
    /// </summary>
    Mappage = 68,

    /// <summary>
    /// MCF IM info object
    /// </summary>
    Mcfiminfo = 69,

    /// <summary>
    /// Message object
    /// </summary>
    Message = 70,

    /// <summary>
    /// M file metadata object
    /// </summary>
    Mfilemetadata = 71,

    /// <summary>
    /// Optimization engine object
    /// </summary>
    Optengine = 72,

    /// <summary>
    /// Optimization interface object
    /// </summary>
    Optinterface = 73,

    /// <summary>
    /// Organization chart object
    /// </summary>
    Orgchart = 74,

    /// <summary>
    /// Page object
    /// </summary>
    Page = 75,

    /// <summary>
    /// Panel object
    /// </summary>
    Panel = 76,

    /// <summary>
    /// Post report object
    /// </summary>
    Postreport = 77,

    /// <summary>
    /// PPM class object
    /// </summary>
    Ppmclass = 78,

    /// <summary>
    /// Primitive object
    /// </summary>
    Primitive = 79,

    /// <summary>
    /// Process request object
    /// </summary>
    Processrequest = 80,

    /// <summary>
    /// PS event object
    /// </summary>
    Psevent = 81,

    /// <summary>
    /// PSOCI data science object
    /// </summary>
    Psocidatascience = 82,

    /// <summary>
    /// PS spreadsheet object
    /// </summary>
    Psspreadsheet = 83,

    /// <summary>
    /// PT application system variable object
    /// </summary>
    Ptappsysvar = 84,

    /// <summary>
    /// PT batch system variable object
    /// </summary>
    Ptbatchsysvar = 85,

    /// <summary>
    /// PT database system variable object
    /// </summary>
    Ptdbsysvar = 86,

    /// <summary>
    /// PT direct transfer object
    /// </summary>
    Ptdirecttransferobject = 87,

    /// <summary>
    /// PT RPS system variable object
    /// </summary>
    Ptrpssysvar = 88,

    /// <summary>
    /// PT system variable object
    /// </summary>
    Ptsysvar = 89,

    /// <summary>
    /// PT web system variable object
    /// </summary>
    Ptwebsysvar = 90,

    /// <summary>
    /// PVG engine object
    /// </summary>
    Pvgengine = 91,

    /// <summary>
    /// Quadrant schema object
    /// </summary>
    Quadrantschema = 92,

    /// <summary>
    /// Rating box chart object
    /// </summary>
    Ratingboxchart = 93,

    /// <summary>
    /// Rating gauge chart object
    /// </summary>
    Ratinggaugechart = 94,

    /// <summary>
    /// Rating gauge state object
    /// </summary>
    Ratinggaugestate = 95,

    /// <summary>
    /// Record object
    /// </summary>
    Record = 96,

    /// <summary>
    /// Record definition object
    /// </summary>
    Recorddefn = 97,

    /// <summary>
    /// Record field definition object
    /// </summary>
    Recordfielddefn = 98,

    /// <summary>
    /// Reference area object
    /// </summary>
    Referencearea = 99,

    /// <summary>
    /// Reference line object
    /// </summary>
    Referenceline = 100,

    /// <summary>
    /// Request object
    /// </summary>
    Request = 101,

    /// <summary>
    /// Response object
    /// </summary>
    Response = 102,

    /// <summary>
    /// Row object
    /// </summary>
    Row = 103,

    /// <summary>
    /// Rowset object
    /// </summary>
    Rowset = 104,

    /// <summary>
    /// Rowset cache object
    /// </summary>
    Rowsetcache = 105,

    /// <summary>
    /// Run control manager object
    /// </summary>
    Runcontrolmanager = 106,

    /// <summary>
    /// Schema level object
    /// </summary>
    Schemalevel = 107,

    /// <summary>
    /// Series object
    /// </summary>
    Series = 108,

    /// <summary>
    /// SOAP document object
    /// </summary>
    Soapdoc = 109,

    /// <summary>
    /// Spark chart object
    /// </summary>
    Sparkchart = 110,

    /// <summary>
    /// Spark chart item object
    /// </summary>
    Sparkchartitem = 111,

    /// <summary>
    /// SQL object
    /// </summary>
    Sql = 112,

    /// <summary>
    /// Status meter gauge object
    /// </summary>
    Statusmetergauge = 113,

    /// <summary>
    /// Sync server object
    /// </summary>
    Syncserver = 114,

    /// <summary>
    /// Threshold object
    /// </summary>
    Threshold = 115,

    /// <summary>
    /// Timeline object
    /// </summary>
    Timeline = 116,

    /// <summary>
    /// Tooltip label object
    /// </summary>
    Tooltiplabel = 117,

    /// <summary>
    /// Transform data object
    /// </summary>
    Transformdata = 118,

    /// <summary>
    /// XML document object
    /// </summary>
    Xmldoc = 119,

    /// <summary>
    /// XML document factory object
    /// </summary>
    Xmldocfactory = 120,

    /// <summary>
    /// XML link object
    /// </summary>
    Xmllink = 121,

    /// <summary>
    /// XML node object
    /// </summary>
    Xmlnode = 122
}

/// <summary>
/// Extension methods for PeopleCodeType enum
/// </summary>
public static class PeopleCodeTypeExtensions
{
    /// <summary>
    /// Gets the string name of the type as it appears in PeopleCode
    /// </summary>
    public static string GetTypeName(this PeopleCodeType type)
    {
        return type switch
        {
            // Special types
            PeopleCodeType.Any => "any",
            PeopleCodeType.Void => "void",
            PeopleCodeType.AppClass => "appclass",
            PeopleCodeType.Unknown => "unknown",
            PeopleCodeType.Reference => "reference",

            // Primitive types
            PeopleCodeType.String => "string",
            PeopleCodeType.Integer => "integer",
            PeopleCodeType.Number => "number",
            PeopleCodeType.Date => "date",
            PeopleCodeType.DateTime => "datetime",
            PeopleCodeType.Time => "time",
            PeopleCodeType.Boolean => "boolean",

            // Builtin object types (lowercase as they appear in PeopleCode)
            _ => type.ToString().ToLowerInvariant()
        };
    }

    /// <summary>
    /// Determines if this is a primitive type
    /// </summary>
    public static bool IsPrimitive(this PeopleCodeType type)
    {
        return type >= PeopleCodeType.String && type <= PeopleCodeType.Boolean;
    }

    /// <summary>
    /// Determines if this is a builtin object type
    /// </summary>
    public static bool IsBuiltinObject(this PeopleCodeType type)
    {
        return type >= PeopleCodeType.Aesection && type <= PeopleCodeType.Xmlnode;
    }

    /// <summary>
    /// Determines if this is a special type (Any, Void, AppClass, Unknown, Reference)
    /// </summary>
    public static bool IsSpecial(this PeopleCodeType type)
    {
        return type >= PeopleCodeType.Any && type <= PeopleCodeType.Reference;
    }

    /// <summary>
    /// Gets the TypeKind corresponding to this PeopleCodeType
    /// </summary>
    public static TypeKind GetTypeKind(this PeopleCodeType type)
    {
        return type switch
        {
            PeopleCodeType.Any => TypeKind.Any,
            PeopleCodeType.Void => TypeKind.Void,
            PeopleCodeType.AppClass => TypeKind.AppClass,
            PeopleCodeType.Unknown => TypeKind.Unknown,
            PeopleCodeType.Reference => TypeKind.Reference,
            var t when t.IsPrimitive() => TypeKind.Primitive,
            var t when t.IsBuiltinObject() => TypeKind.BuiltinObject,
            _ => TypeKind.Unknown
        };
    }
}