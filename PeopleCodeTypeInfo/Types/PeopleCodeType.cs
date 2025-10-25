namespace PeopleCodeTypeInfo.Types;

/// <summary>
/// Represents the kind/category of a type in the PeopleCode type system
/// </summary>
public enum TypeKind
{
    /// <summary>
    /// Built-in primitive types (string, integer, number, date, boolean, etc.)
    /// </summary>
    Primitive,

    /// <summary>
    /// Built-in object types (Record, Rowset, Field, Component, etc.)
    /// </summary>
    BuiltinObject,

    /// <summary>
    /// User-defined application classes
    /// </summary>
    AppClass,

    /// <summary>
    /// Interface types
    /// </summary>
    Interface,

    /// <summary>
    /// Array types (ARRAY OF type)
    /// </summary>
    Array,

    /// <summary>
    /// The special "Any" type that can hold any value
    /// </summary>
    Any,

    /// <summary>
    /// No return value (like void in other languages)
    /// Used for functions/methods that don't return a value
    /// </summary>
    Void,

    /// <summary>
    /// Unknown/unresolved type
    /// </summary>
    Unknown,

    /// <summary>
    /// Invalid type resulting from semantically impossible operations
    /// </summary>
    Invalid,

    /// <summary>
    /// Reference type for named references like HTML.OBJECT_NAME, SQL.FOO, RECORD.FOO
    /// Also includes dynamic references via @ expressions
    /// </summary>
    Reference
}

/// <summary>
/// Base class for all type information in the PeopleCode type system
/// </summary>
public abstract class TypeInfo
{
    /// <summary>
    /// The name of this type as it appears in code
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The kind/category of this type
    /// </summary>
    public abstract TypeKind Kind { get; }

    /// <summary>
    /// The PeopleCode type enum value, if this type corresponds to a PeopleCode type
    /// </summary>
    public virtual PeopleCodeType? PeopleCodeType => null;

    /// <summary>
    /// True if this type can be assigned null values (most types can in PeopleCode)
    /// </summary>
    public virtual bool IsNullable => true;

    /// <summary>
    /// Determines if a value of 'other' type can be assigned to this type
    /// </summary>
    public abstract bool IsAssignableFrom(TypeInfo other);

    /// <summary>
    /// Gets the most specific common type between this and another type
    /// </summary>
    public virtual TypeInfo GetCommonType(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return AnyTypeInfo.Instance;

        // Fast path: if both have the same PeopleCodeType, return this
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue && PeopleCodeType.Value == other.PeopleCodeType.Value)
        {
            return this;
        }

        // Fast path: check for common primitive promotions
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue &&
            PeopleCodeType.Value.IsPrimitive() && other.PeopleCodeType.Value.IsPrimitive())
        {
            var commonType = GetCommonPrimitiveType(PeopleCodeType.Value, other.PeopleCodeType.Value);
            if (commonType.HasValue)
            {
                return TypeInfo.FromPeopleCodeType(commonType.Value);
            }
        }

        // Standard logic - prioritize more general types
        // If one is Any, return Any
        if (Kind == TypeKind.Any) return this;
        if (other.Kind == TypeKind.Any) return other;

        // Check assignability
        if (IsAssignableFrom(other)) return this;
        if (other.IsAssignableFrom(this)) return other;
        return AnyTypeInfo.Instance; // Default to Any if no common type
    }

    /// <summary>
    /// Gets the common type for primitive type promotions
    /// </summary>
    private static Types.PeopleCodeType? GetCommonPrimitiveType(Types.PeopleCodeType type1, Types.PeopleCodeType type2)
    {
        // Same type
        if (type1 == type2) return type1;

        // Integer and Number are bidirectionally compatible, common type is Number
        if ((type1 == Types.PeopleCodeType.Integer && type2 == Types.PeopleCodeType.Number) ||
            (type1 == Types.PeopleCodeType.Number && type2 == Types.PeopleCodeType.Integer))
        {
            return Types.PeopleCodeType.Number;
        }

        // Record and Scroll are bidirectionally compatible, common type is Record
        if ((type1 == Types.PeopleCodeType.Record && type2 == Types.PeopleCodeType.Scroll) ||
            (type1 == Types.PeopleCodeType.Scroll && type2 == Types.PeopleCodeType.Record))
        {
            return Types.PeopleCodeType.Record;
        }

        // Date, DateTime, and Time are NOT compatible with each other - no common type
        var dateTimeTypes = new[] { Types.PeopleCodeType.Date, Types.PeopleCodeType.DateTime, Types.PeopleCodeType.Time };
        if (Array.IndexOf(dateTimeTypes, type1) >= 0 && Array.IndexOf(dateTimeTypes, type2) >= 0)
        {
            return null; // No common type for date/datetime/time
        }

        // No other common primitive types
        return null;
    }

    public override string ToString() => Name;

    public override bool Equals(object? obj)
    {
        if (obj is not TypeInfo other) return false;

        // Fast path: if both have PeopleCodeType values, compare those first
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue)
        {
            return PeopleCodeType.Value == other.PeopleCodeType.Value;
        }

        // Fallback to name comparison
        return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        // Use PeopleCodeType for hash if available, otherwise use name
        return PeopleCodeType?.GetHashCode() ?? Name.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a TypeInfo instance from a PeopleCodeType enum value
    /// </summary>
    public static TypeInfo FromPeopleCodeType(PeopleCodeType peopleCodeType)
    {
        return peopleCodeType switch
        {
            // Special types
            Types.PeopleCodeType.Any => AnyTypeInfo.Instance,
            Types.PeopleCodeType.Void => VoidTypeInfo.Instance,
            Types.PeopleCodeType.Unknown => UnknownTypeInfo.Instance,
            Types.PeopleCodeType.Reference => ReferenceTypeInfo.Instance,
            Types.PeopleCodeType.AppClass => throw new InvalidOperationException("AppClass types require additional context"),

            // Polymorphic types - return instances that can be resolved later
            Types.PeopleCodeType.SameAsObject => SameAsObjectTypeInfo.Instance,
            Types.PeopleCodeType.ElementOfObject => ElementOfObjectTypeInfo.Instance,
            Types.PeopleCodeType.SameAsFirstParameter => SameAsFirstParameterTypeInfo.Instance,
            Types.PeopleCodeType.ArrayOfFirstParameter => ArrayOfFirstParameterTypeInfo.Instance,

            // Special reference types (Object uses specialized ObjectTypeInfo, Scroll is a builtin object)
            Types.PeopleCodeType.Object => ObjectTypeInfo.Instance,
            Types.PeopleCodeType.Scroll => new BuiltinObjectTypeInfo("scroll", peopleCodeType),

            // Primitive types
            Types.PeopleCodeType.String => PrimitiveTypeInfo.String,
            Types.PeopleCodeType.Integer => PrimitiveTypeInfo.Integer,
            Types.PeopleCodeType.Number => PrimitiveTypeInfo.Number,
            Types.PeopleCodeType.Date => PrimitiveTypeInfo.Date,
            Types.PeopleCodeType.DateTime => PrimitiveTypeInfo.DateTime,
            Types.PeopleCodeType.Time => PrimitiveTypeInfo.Time,
            Types.PeopleCodeType.Boolean => PrimitiveTypeInfo.Boolean,

            // Builtin object types - create new instances with the enum type
            _ when peopleCodeType.IsBuiltinObject() => new BuiltinObjectTypeInfo(peopleCodeType.GetTypeName(), peopleCodeType),

            _ => throw new ArgumentException($"Unknown PeopleCode type: {peopleCodeType}", nameof(peopleCodeType))
        };
    }
}

public enum PeopleCodeType : byte
{
    // Special types (0-9)
    Any = 0,
    Void = 1,
    AppClass = 2,
    Unknown = 3,
    Reference = 4,
    Object = 5,
    Scroll = 6, // special reference type that isn't an actual type.
    Operation = 7,

    // Polymorphic types for runtime-determined return types (8-9)
    SameAsObject = 8,       // Returns same type as the object instance
    ElementOfObject = 9,    // Returns element type (object type minus 1 dimension)
    SameAsFirstParameter = 10,   // Returns same type as first parameter
    ArrayOfFirstParameter = 11,  // Returns array of first parameter's type

    // Primitive types (20-29)
    String = 20,
    Integer = 21,
    Number = 22,
    Date = 23,
    DateTime = 24,
    Time = 25,
    Boolean = 26,

    // Builtin object types (30-255)
    Aesection = 30,
    Analyticgrid = 31,
    Analyticgridcolumn = 32,
    Analyticinstance = 33,
    Analyticmodel = 34,
    Apiobject = 35,
    Bidocs = 36,
    Chart = 37,
    Collection = 38,
    Compositequery = 39,
    Compound = 40,
    Cookie = 41,
    Cqchunkedprocessor = 42,
    Cqruntime = 43,
    Crypt = 44,
    Cubecollection = 45,
    Dialgauge = 46,
    Document = 47,
    Documentkey = 48,
    Field = 49,
    Fielddefn = 50,
    File = 51,
    Filedefn = 52,
    Filedefnelement = 53,
    Filedefnelementcollection = 54,
    Filedefnmanager = 55,
    Filedefnmap = 56,
    Filemetadata = 57,
    Gantt = 58,
    Gaugechart = 59,
    Gaugethreshold = 60,
    Grid = 61,
    Gridcolumn = 62,
    Ibconnectorinfo = 63,
    Ibinfo = 64,
    Intbroker = 65,
    Interlink = 66,
    Javaobject = 67,
    Jsonarray = 68,
    Jsonbuilder = 69,
    Jsongenerator = 70,
    Jsonnode = 71,
    Jsonobject = 72,
    Jsonparser = 73,
    Jsonvalue = 74,
    Ledgauge = 75,
    Map = 76,
    Mapelement = 77,
    Mappage = 78,
    Mcfiminfo = 79,
    Message = 80,
    Mfilemetadata = 81,
    Optengine = 82,
    Optinterface = 83,
    Orgchart = 84,
    Page = 85,
    Panel = 86,
    Postreport = 87,
    Ppmclass = 88,
    Primitive = 89,
    Processrequest = 90,
    Psevent = 91,
    Psocidatascience = 92,
    Psspreadsheet = 93,
    Ptappsysvar = 94,
    Ptbatchsysvar = 95,
    Ptdbsysvar = 96,
    Ptdirecttransferobject = 97,
    Ptrpssysvar = 98,
    Ptsysvar = 99,
    Ptwebsysvar = 100,
    Pvgengine = 101,
    Quadrantschema = 102,
    Ratingboxchart = 103,
    Ratinggaugechart = 104,
    Ratinggaugestate = 105,
    Record = 106,
    Recorddefn = 107,
    Recordfielddefn = 108,
    Referencearea = 109,
    Referenceline = 110,
    Request = 111,
    Response = 112,
    Row = 113,
    Rowset = 114,
    Rowsetcache = 115,
    Runcontrolmanager = 116,
    Schemalevel = 117,
    Series = 118,
    Soapdoc = 119,
    Sparkchart = 120,
    Sparkchartitem = 121,
    Sql = 122,
    Statusmetergauge = 123,
    Syncserver = 124,
    Threshold = 125,
    Timeline = 126,
    Tooltiplabel = 127,
    Transformdata = 128,
    Xmldoc = 129,
    Xmldocfactory = 130,
    Xmllink = 131,
    Xmlnode = 132,

    // Additional reference type keywords (133-148)
    Barname = 133,
    Busactivity = 134,
    Busevent = 135,
    Busprocess = 136,
    Compintfc = 137,
    Component = 138,
    Filelayout = 139,
    Html = 140,
    Image = 141,
    Itemname = 142,
    Menuname = 143,
    Node = 144,
    Package = 145,
    Panelgroup = 146,
    Stylesheet = 147,
    Url = 148
}

public static class BuiltinTypeExtensions
{
    public static PeopleCodeType FromString(string typeStr)
    {
        // Convert to lowercase for comparison
        var cleanType = typeStr.ToLowerInvariant();

        return cleanType switch
        {
            "any" => PeopleCodeType.Any,
            "void" => PeopleCodeType.Void,
            "appclass" => PeopleCodeType.AppClass,
            "unknown" => PeopleCodeType.Unknown,
            "reference" => PeopleCodeType.Reference,
            "object" => PeopleCodeType.Object,
            "scroll" => PeopleCodeType.Scroll,
            "operation" => PeopleCodeType.Operation,
            "$same" => PeopleCodeType.SameAsObject,
            "$element" => PeopleCodeType.ElementOfObject,
            "$same_as_first" => PeopleCodeType.SameAsFirstParameter,
            "$array_of_first" => PeopleCodeType.ArrayOfFirstParameter,
            "string" => PeopleCodeType.String,
            "integer" => PeopleCodeType.Integer,
            "number" => PeopleCodeType.Number,
            "date" => PeopleCodeType.Date,
            "datetime" => PeopleCodeType.DateTime,
            "time" => PeopleCodeType.Time,
            "boolean" => PeopleCodeType.Boolean,
            "aesection" => PeopleCodeType.Aesection,
            "analyticgrid" => PeopleCodeType.Analyticgrid,
            "analyticgridcolumn" => PeopleCodeType.Analyticgridcolumn,
            "analyticinstance" => PeopleCodeType.Analyticinstance,
            "analyticmodel" => PeopleCodeType.Analyticmodel,
            "apiobject" => PeopleCodeType.Apiobject,
            "bidocs" => PeopleCodeType.Bidocs,
            "chart" => PeopleCodeType.Chart,
            "collection" => PeopleCodeType.Collection,
            "compositequery" => PeopleCodeType.Compositequery,
            "compound" => PeopleCodeType.Compound,
            "cookie" => PeopleCodeType.Cookie,
            "cqchunkedprocessor" => PeopleCodeType.Cqchunkedprocessor,
            "cqruntime" => PeopleCodeType.Cqruntime,
            "crypt" => PeopleCodeType.Crypt,
            "cubecollection" => PeopleCodeType.Cubecollection,
            "dialgauge" => PeopleCodeType.Dialgauge,
            "document" => PeopleCodeType.Document,
            "documentkey" => PeopleCodeType.Documentkey,
            "field" => PeopleCodeType.Field,
            "fielddefn" => PeopleCodeType.Fielddefn,
            "file" => PeopleCodeType.File,
            "filedefn" => PeopleCodeType.Filedefn,
            "filedefnelement" => PeopleCodeType.Filedefnelement,
            "filedefnelementcollection" => PeopleCodeType.Filedefnelementcollection,
            "filedefnmanager" => PeopleCodeType.Filedefnmanager,
            "filedefnmap" => PeopleCodeType.Filedefnmap,
            "filemetadata" => PeopleCodeType.Filemetadata,
            "gantt" => PeopleCodeType.Gantt,
            "gaugechart" => PeopleCodeType.Gaugechart,
            "gaugethreshold" => PeopleCodeType.Gaugethreshold,
            "grid" => PeopleCodeType.Grid,
            "gridcolumn" => PeopleCodeType.Gridcolumn,
            "ibconnectorinfo" => PeopleCodeType.Ibconnectorinfo,
            "ibinfo" => PeopleCodeType.Ibinfo,
            "intbroker" => PeopleCodeType.Intbroker,
            "interlink" => PeopleCodeType.Interlink,
            "javaobject" => PeopleCodeType.Javaobject,
            "jsonarray" => PeopleCodeType.Jsonarray,
            "jsonbuilder" => PeopleCodeType.Jsonbuilder,
            "jsongenerator" => PeopleCodeType.Jsongenerator,
            "jsonnode" => PeopleCodeType.Jsonnode,
            "jsonobject" => PeopleCodeType.Jsonobject,
            "jsonparser" => PeopleCodeType.Jsonparser,
            "jsonvalue" => PeopleCodeType.Jsonvalue,
            "ledgauge" => PeopleCodeType.Ledgauge,
            "map" => PeopleCodeType.Map,
            "mapelement" => PeopleCodeType.Mapelement,
            "mappage" => PeopleCodeType.Mappage,
            "mcfiminfo" => PeopleCodeType.Mcfiminfo,
            "message" => PeopleCodeType.Message,
            "mfilemetadata" => PeopleCodeType.Mfilemetadata,
            "optengine" => PeopleCodeType.Optengine,
            "optinterface" => PeopleCodeType.Optinterface,
            "orgchart" => PeopleCodeType.Orgchart,
            "page" => PeopleCodeType.Page,
            "panel" => PeopleCodeType.Panel,
            "postreport" => PeopleCodeType.Postreport,
            "ppmclass" => PeopleCodeType.Ppmclass,
            "primitive" => PeopleCodeType.Primitive,
            "processrequest" => PeopleCodeType.Processrequest,
            "psevent" => PeopleCodeType.Psevent,
            "psocidatascience" => PeopleCodeType.Psocidatascience,
            "psspreadsheet" => PeopleCodeType.Psspreadsheet,
            "ptappsysvar" => PeopleCodeType.Ptappsysvar,
            "ptbatchsysvar" => PeopleCodeType.Ptbatchsysvar,
            "ptdbsysvar" => PeopleCodeType.Ptdbsysvar,
            "ptdirecttransferobject" => PeopleCodeType.Ptdirecttransferobject,
            "ptrpssysvar" => PeopleCodeType.Ptrpssysvar,
            "ptsysvar" => PeopleCodeType.Ptsysvar,
            "ptwebsysvar" => PeopleCodeType.Ptwebsysvar,
            "pvgengine" => PeopleCodeType.Pvgengine,
            "quadrantschema" => PeopleCodeType.Quadrantschema,
            "ratingboxchart" => PeopleCodeType.Ratingboxchart,
            "ratinggaugechart" => PeopleCodeType.Ratinggaugechart,
            "ratinggaugestate" => PeopleCodeType.Ratinggaugestate,
            "record" => PeopleCodeType.Record,
            "recorddefn" => PeopleCodeType.Recorddefn,
            "recordfielddefn" => PeopleCodeType.Recordfielddefn,
            "referencearea" => PeopleCodeType.Referencearea,
            "referenceline" => PeopleCodeType.Referenceline,
            "request" => PeopleCodeType.Request,
            "response" => PeopleCodeType.Response,
            "row" => PeopleCodeType.Row,
            "rowset" => PeopleCodeType.Rowset,
            "rowsetcache" => PeopleCodeType.Rowsetcache,
            "runcontrolmanager" => PeopleCodeType.Runcontrolmanager,
            "schemalevel" => PeopleCodeType.Schemalevel,
            "series" => PeopleCodeType.Series,
            "soapdoc" => PeopleCodeType.Soapdoc,
            "sparkchart" => PeopleCodeType.Sparkchart,
            "sparkchartitem" => PeopleCodeType.Sparkchartitem,
            "sql" => PeopleCodeType.Sql,
            "statusmetergauge" => PeopleCodeType.Statusmetergauge,
            "syncserver" => PeopleCodeType.Syncserver,
            "threshold" => PeopleCodeType.Threshold,
            "timeline" => PeopleCodeType.Timeline,
            "tooltiplabel" => PeopleCodeType.Tooltiplabel,
            "transformdata" => PeopleCodeType.Transformdata,
            "xmldoc" => PeopleCodeType.Xmldoc,
            "xmldocfactory" => PeopleCodeType.Xmldocfactory,
            "xmllink" => PeopleCodeType.Xmllink,
            "xmlnode" => PeopleCodeType.Xmlnode,
            "barname" => PeopleCodeType.Barname,
            "busactivity" => PeopleCodeType.Busactivity,
            "busevent" => PeopleCodeType.Busevent,
            "busprocess" => PeopleCodeType.Busprocess,
            "compintfc" => PeopleCodeType.Compintfc,
            "component" => PeopleCodeType.Component,
            "filelayout" => PeopleCodeType.Filelayout,
            "html" => PeopleCodeType.Html,
            "image" => PeopleCodeType.Image,
            "itemname" => PeopleCodeType.Itemname,
            "menuname" => PeopleCodeType.Menuname,
            "node" => PeopleCodeType.Node,
            "package" => PeopleCodeType.Package,
            "panelgroup" => PeopleCodeType.Panelgroup,
            "stylesheet" => PeopleCodeType.Stylesheet,
            "url" => PeopleCodeType.Url,
            _ => PeopleCodeType.Unknown
        };
    }

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
            PeopleCodeType.Object => "object",
            PeopleCodeType.Scroll => "scroll",
            PeopleCodeType.Operation => "operation",

            // Polymorphic types
            PeopleCodeType.SameAsObject => "$same",
            PeopleCodeType.ElementOfObject => "$element",
            PeopleCodeType.SameAsFirstParameter => "$same_as_first",
            PeopleCodeType.ArrayOfFirstParameter => "$array_of_first",

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
        return type >= PeopleCodeType.Aesection && type <= PeopleCodeType.Url;
    }

    /// <summary>
    /// Determines if this is a special type (Any, Void, AppClass, Unknown, Reference, Object, Scroll, Operation)
    /// </summary>
    public static bool IsSpecial(this PeopleCodeType type)
    {
        return type >= PeopleCodeType.Any && type <= PeopleCodeType.Operation;
    }

    /// <summary>
    /// Determines if this is a polymorphic type that requires context resolution
    /// </summary>
    public static bool IsPolymorphic(this PeopleCodeType type)
    {
        return type >= PeopleCodeType.SameAsObject && type <= PeopleCodeType.ArrayOfFirstParameter;
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
            var t when t.IsPolymorphic() => TypeKind.Unknown, // Polymorphic types need context resolution
            var t when t.IsPrimitive() => TypeKind.Primitive,
            var t when t.IsBuiltinObject() => TypeKind.BuiltinObject,
            _ => TypeKind.Unknown
        };
    }
}

/// <summary>
/// Represents a built-in primitive type in PeopleCode (string, integer, number, etc.)
/// </summary>
public class PrimitiveTypeInfo : TypeInfo
{
    public override string Name { get; }
    public override TypeKind Kind => TypeKind.Primitive;
    public override PeopleCodeType? PeopleCodeType { get; }

    // Common primitive type instances
    public static readonly PrimitiveTypeInfo String = new StringTypeInfo();
    public static readonly PrimitiveTypeInfo Integer = new("integer", Types.PeopleCodeType.Integer);
    public static readonly PrimitiveTypeInfo Number = new NumberTypeInfo();
    public static readonly PrimitiveTypeInfo Date = new("date", Types.PeopleCodeType.Date);
    public static readonly PrimitiveTypeInfo DateTime = new("datetime", Types.PeopleCodeType.DateTime);
    public static readonly PrimitiveTypeInfo Time = new("time", Types.PeopleCodeType.Time);
    public static readonly PrimitiveTypeInfo Boolean = new("boolean", Types.PeopleCodeType.Boolean);

    public PrimitiveTypeInfo(string name, PeopleCodeType? peopleCodeType = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        PeopleCodeType = peopleCodeType;
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can be assigned to any primitive
        if (other.Kind == TypeKind.Any) return true;

        // Fast path: if both have PeopleCodeType values, compare those first
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue)
        {
            // Same type
            if (PeopleCodeType.Value == other.PeopleCodeType.Value) return true;

            // PeopleCode implicit conversions using enum values
            return CanImplicitlyConvert(PeopleCodeType.Value, other.PeopleCodeType.Value);
        }

        // Fallback: Same primitive type by name
        if (other is PrimitiveTypeInfo primitive && primitive.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))
            return true;

        // PeopleCode has some implicit conversions
        return CanImplicitlyConvert(other);
    }

    private bool CanImplicitlyConvert(PeopleCodeType thisType, PeopleCodeType otherType)
    {
        // Both must be primitive types
        if (!thisType.IsPrimitive() || !otherType.IsPrimitive()) return false;

        // Bidirectional number/integer compatibility
        if ((thisType == Types.PeopleCodeType.Number && otherType == Types.PeopleCodeType.Integer) ||
            (thisType == Types.PeopleCodeType.Integer && otherType == Types.PeopleCodeType.Number))
        {
            return true;
        }

        return false;
    }

    private bool CanImplicitlyConvert(TypeInfo other)
    {
        if (other.Kind != TypeKind.Primitive) return false;

        var otherName = other.Name.ToLowerInvariant();
        var thisName = Name.ToLowerInvariant();

        // Bidirectional number/integer compatibility
        if ((thisName == "number" && otherName == "integer") ||
            (thisName == "integer" && otherName == "number"))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Represents a built-in object type in PeopleCode (Record, Rowset, Field, etc.)
/// </summary>
public class BuiltinObjectTypeInfo : TypeInfo
{
    public override string Name { get; }
    public override TypeKind Kind => TypeKind.BuiltinObject;
    public override PeopleCodeType? PeopleCodeType { get; }

    // Common builtin object instances
    public static readonly BuiltinObjectTypeInfo Record = new("Record", Types.PeopleCodeType.Record);
    public static readonly BuiltinObjectTypeInfo Rowset = new("Rowset", Types.PeopleCodeType.Rowset);

    public BuiltinObjectTypeInfo(string name, PeopleCodeType? peopleCodeType = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        PeopleCodeType = peopleCodeType;
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can be assigned to any builtin object
        if (other.Kind == TypeKind.Any) return true;

        // Fast path: if both have PeopleCodeType values, compare those first
        if (PeopleCodeType.HasValue && other.PeopleCodeType.HasValue)
        {
            // Same type
            if (PeopleCodeType.Value == other.PeopleCodeType.Value) return true;

            // Special compatibility: RECORD and SCROLL are bidirectionally compatible
            if ((PeopleCodeType.Value == Types.PeopleCodeType.Record && other.PeopleCodeType.Value == Types.PeopleCodeType.Scroll) ||
                (PeopleCodeType.Value == Types.PeopleCodeType.Scroll && other.PeopleCodeType.Value == Types.PeopleCodeType.Record))
            {
                return true;
            }

            return false;
        }

        // Fallback: Same builtin object type by name
        if (other is BuiltinObjectTypeInfo builtin && builtin.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))
            return true;

        // Name-based compatibility for RECORD and SCROLL
        if (other is BuiltinObjectTypeInfo otherBuiltin)
        {
            var thisName = Name.ToLowerInvariant();
            var otherName = otherBuiltin.Name.ToLowerInvariant();

            if ((thisName == "record" && otherName == "scroll") ||
                (thisName == "scroll" && otherName == "record"))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Represents a user-defined application class type
/// </summary>
public class AppClassTypeInfo : TypeInfo
{
    public override string Name => QualifiedName;
    public override TypeKind Kind => TypeKind.AppClass;

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

    public AppClassTypeInfo(string qualifiedName)
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
            ClassName = parts[parts.Length - 1];
        }
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can be assigned to any app class
        if (other.Kind == TypeKind.Any) return true;

        // Same app class type
        if (other is AppClassTypeInfo appClass &&
            appClass.QualifiedName.Equals(QualifiedName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Represents an array type (ARRAY OF type)
/// </summary>
public class ArrayTypeInfo : TypeInfo
{
    public override string Name { 
        get {

            string prefix = "";
            for(var x = 0; x < Dimensions; x++)
            {
                prefix += "array of ";
            }

            return ElementType != null ? $"{prefix}{ElementType.Name}" : $"{prefix} any";
        }
    }
    public override TypeKind Kind => TypeKind.Array;

    /// <summary>
    /// Number of dimensions (1 for ARRAY, 2 for ARRAY2, etc.)
    /// </summary>
    public int Dimensions { get; }

    /// <summary>
    /// Element type, null for untyped arrays
    /// </summary>
    public TypeInfo? ElementType { get; }

    public ArrayTypeInfo(int dimensions = 1, TypeInfo? elementType = null)
    {
        if (dimensions < 1 || dimensions > 9)
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Array dimensions must be between 1 and 9");

        Dimensions = dimensions;
        ElementType = elementType;
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can be assigned to any array
        if (other.Kind == TypeKind.Any) return true;

        // Same array type with compatible element types
        if (other is ArrayTypeInfo array && array.Dimensions == Dimensions)
        {
            // Untyped arrays are compatible
            if (ElementType == null || array.ElementType == null) return true;

            // Check element type compatibility (with null safety)
            return ElementType?.IsAssignableFrom(array.ElementType) ?? true;
        }

        return false;
    }
}

/// <summary>
/// Represents the special "Object" type in PeopleCode.
/// The "object" type can hold any non-primitive type (builtin objects and AppClasses),
/// but cannot hold primitive types (string, integer, number, date, datetime, time, boolean).
/// This is different from "any" which can hold everything including primitives.
/// </summary>
public class ObjectTypeInfo : TypeInfo
{
    public override string Name => "object";
    public override TypeKind Kind => TypeKind.BuiltinObject;
    public override PeopleCodeType? PeopleCodeType => Types.PeopleCodeType.Object;

    // Singleton instance
    public static readonly ObjectTypeInfo Instance = new();

    private ObjectTypeInfo() { }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can be assigned to object
        if (other.Kind == TypeKind.Any) return true;

        // Object can accept any builtin object type
        if (other.Kind == TypeKind.BuiltinObject) return true;

        // Object can accept any AppClass type
        if (other.Kind == TypeKind.AppClass) return true;

        // Object can accept array types
        if (other.Kind == TypeKind.Array) return true;

        // Object can accept interface types
        if (other.Kind == TypeKind.Interface) return true;

        // Object CANNOT accept primitive types
        if (other.Kind == TypeKind.Primitive) return false;

        // Unknown types - allow for now since we can't verify at compile time
        if (other.Kind == TypeKind.Unknown) return true;

        // Reject void, invalid, and other types
        return false;
    }

    public override TypeInfo GetCommonType(TypeInfo other)
    {
        // If the other type is assignable to object, return object
        if (IsAssignableFrom(other)) return this;

        // If this is assignable to other, return other
        if (other.IsAssignableFrom(this)) return other;

        // Otherwise, the common type is any
        return AnyTypeInfo.Instance;
    }
}

/// <summary>
/// Represents the special "Any" type that can hold any value in PeopleCode
/// </summary>
public class AnyTypeInfo : TypeInfo
{
    public override string Name => "any";
    public override TypeKind Kind => TypeKind.Any;
    public override PeopleCodeType? PeopleCodeType => Types.PeopleCodeType.Any;

    // Singleton instance
    public static readonly AnyTypeInfo Instance = new();

    private AnyTypeInfo() { }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can accept any type
        return true;
    }

    public override TypeInfo GetCommonType(TypeInfo other)
    {
        // Any is the ultimate common type
        return this;
    }
}

/// <summary>
/// Represents a "void" type for functions/methods that don't return a value
/// </summary>
public class VoidTypeInfo : TypeInfo
{
    public override string Name => "void";
    public override TypeKind Kind => TypeKind.Void;
    public override PeopleCodeType? PeopleCodeType => Types.PeopleCodeType.Void;
    public override bool IsNullable => false; // Void cannot be assigned or be null

    // Singleton instance
    public static readonly VoidTypeInfo Instance = new();

    private VoidTypeInfo() { }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Void cannot accept any assignment
        return false;
    }

    public override TypeInfo GetCommonType(TypeInfo other)
    {
        // Void has no meaningful common type with anything
        return UnknownTypeInfo.Instance;
    }
}

/// <summary>
/// Represents an unknown or unresolved type
/// </summary>
public class UnknownTypeInfo : TypeInfo
{
    public override string Name { get; }
    public override TypeKind Kind => TypeKind.Unknown;
    public override PeopleCodeType? PeopleCodeType => Name.Equals("unknown", StringComparison.OrdinalIgnoreCase) ? Types.PeopleCodeType.Unknown : null;

    // Common instance for truly unknown types
    public static readonly UnknownTypeInfo Instance = new("unknown");

    public UnknownTypeInfo(string name = "unknown")
    {
        Name = name ?? "unknown";
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Unknown types are not assignable (except from Any)
        return other.Kind == TypeKind.Any;
    }

    public override TypeInfo GetCommonType(TypeInfo other)
    {
        // If we can't resolve this type, default to Any
        return AnyTypeInfo.Instance;
    }
}

/// <summary>
/// Represents a type that is semantically invalid due to an impossible operation
/// (e.g., adding a number to an object, concatenating non-strings, indexing a non-array)
/// </summary>
public class InvalidTypeInfo : TypeInfo
{
    public override string Name => "invalid";
    public override TypeKind Kind => TypeKind.Invalid;

    /// <summary>
    /// The reason why this type is invalid
    /// </summary>
    public string Reason { get; }

    public InvalidTypeInfo(string reason)
    {
        Reason = reason ?? "Invalid operation";
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Invalid types cannot accept any assignment
        return false;
    }

    public override TypeInfo GetCommonType(TypeInfo other)
    {
        // Invalid propagates through expressions
        return this;
    }
}

/// <summary>
/// Represents a PeopleCode reference (e.g., Record.MY_RECORD, Field.MY_FIELD, MY_RECORD.MY_FIELD)
/// References are NOT instances - they are references to definitions that can be passed to functions.
/// Example: CreateRecord(Record.FOO) - Record.FOO is a reference, the return value is an instance
/// </summary>
public class ReferenceTypeInfo : TypeInfo
{
    public override string Name => $"@{ReferenceCategory.GetTypeName().ToUpperInvariant()}";
    public override TypeKind Kind => TypeKind.Reference;
    public override PeopleCodeType? PeopleCodeType => Types.PeopleCodeType.Reference;

    /// <summary>
    /// The category of reference (e.g., Record, Field, SQL)
    /// </summary>
    public Types.PeopleCodeType ReferenceCategory { get; }

    /// <summary>
    /// The name of the referenced item (e.g., "MY_RECORD", "MY_FIELD")
    /// </summary>
    public string ReferencedName { get; }

    /// <summary>
    /// The fully qualified reference (e.g., "Record.MY_RECORD", "MY_RECORD.MY_FIELD")
    /// </summary>
    public string FullReference { get; }

    // Singleton instance for generic reference type (backward compatibility)
    public static readonly ReferenceTypeInfo Instance = new(Types.PeopleCodeType.Any, "", "");

    public ReferenceTypeInfo(Types.PeopleCodeType category, string referencedName, string fullReference)
    {
        ReferenceCategory = category;
        ReferencedName = referencedName;
        FullReference = fullReference;
    }

    // Private constructor for singleton
    private ReferenceTypeInfo() : this(Types.PeopleCodeType.Any, "", "") { }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        if (other.Kind == TypeKind.Any) return true;

        // References are only assignable from same category references
        if (other is ReferenceTypeInfo otherRef)
        {
            // Any reference category accepts any other reference
            if (ReferenceCategory == Types.PeopleCodeType.Any || otherRef.ReferenceCategory == Types.PeopleCodeType.Any)
                return true;

            if (ReferenceCategory == Types.PeopleCodeType.Scroll && otherRef.ReferenceCategory == Types.PeopleCodeType.Record)
                return true; /* You can pass a @Scroll into a @Record */

            if (ReferenceCategory == Types.PeopleCodeType.Record && otherRef.ReferenceCategory == Types.PeopleCodeType.Scroll)
                return true; /* You can pass a @Record into a @Scroll */

            if (otherRef.ReferenceCategory == Types.PeopleCodeType.Any)
            {
                /* An Any reference category means this was an @() expression, we can't statically know the type) */
                return true;
            }

            return ReferenceCategory == otherRef.ReferenceCategory;
        }


        return false;
    }

    /// <summary>
    /// Check if a name is a special reference keyword
    /// </summary>
    public static bool IsSpecialReferenceKeyword(string name)
    {
        return SpecialReferenceKeywords.Contains(name);
    }

    /// <summary>
    /// Get the PeopleCodeType for a reference category name
    /// </summary>
    public static Types.PeopleCodeType GetReferenceCategoryType(string categoryName)
    {
        // Try to parse as existing PeopleCodeType
        var type = BuiltinTypeExtensions.FromString(categoryName.ToLowerInvariant());

        // If not found or unknown, default to Field for non-keyword identifiers
        if (type == Types.PeopleCodeType.Unknown)
        {
            return Types.PeopleCodeType.Field; // Default for record.field pattern
        }

        return type;
    }

    private static readonly HashSet<string> SpecialReferenceKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "BARNAME", "BUSACTIVITY", "BUSEVENT", "BUSPROCESS", "COMPINTFC", "COMPONENT",
        "FIELD", "FILELAYOUT", "HTML", "IMAGE", "INTERLINK", "ITEMNAME", "MENUNAME",
        "MESSAGE", "NODE", "OPERATION", "PACKAGE", "PAGE", "PANEL", "PANELGROUP",
        "RECORD", "SCROLL", "SQL", "STYLESHEET", "URL"
    };
}

/// <summary>
/// Specialized string type that cannot be assigned null values
/// </summary>
public class StringTypeInfo : PrimitiveTypeInfo
{
    public StringTypeInfo() : base("string", Types.PeopleCodeType.String)
    {
    }

    public override bool IsNullable => false; // String cannot be assigned null in PeopleCode

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can be assigned to string
        if (other.Kind == TypeKind.Any) return true;

        // Same type (string)
        if (other.PeopleCodeType.HasValue && other.PeopleCodeType.Value == Types.PeopleCodeType.String) return true;
        if (other is PrimitiveTypeInfo primitive && primitive.Name.Equals("string", StringComparison.OrdinalIgnoreCase)) return true;

        // String can ONLY accept string values - no implicit conversions
        return false;
    }
}

/// <summary>
/// Specialized number type that accepts both number and integer values
/// </summary>
public class NumberTypeInfo : PrimitiveTypeInfo
{
    public NumberTypeInfo() : base("number", Types.PeopleCodeType.Number)
    {
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can be assigned to number
        if (other.Kind == TypeKind.Any) return true;

        // Same type (number)
        if (other.PeopleCodeType.HasValue && other.PeopleCodeType.Value == Types.PeopleCodeType.Number) return true;
        if (other is PrimitiveTypeInfo primitive && primitive.Name.Equals("number", StringComparison.OrdinalIgnoreCase)) return true;

        // Number accepts integer (bidirectional compatibility)
        if (other.PeopleCodeType.HasValue && other.PeopleCodeType.Value == Types.PeopleCodeType.Integer) return true;
        if (other is PrimitiveTypeInfo intPrimitive && intPrimitive.Name.Equals("integer", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}

/// <summary>
/// Base class for polymorphic types that require context resolution
/// </summary>
public abstract class PolymorphicTypeInfo : TypeInfo
{
    public override TypeKind Kind => TypeKind.Unknown; // Until resolved

    /// <summary>
    /// The polymorphic type that this represents
    /// </summary>
    public abstract PeopleCodeType PolymorphicType { get; }

    /// <summary>
    /// Resolve this polymorphic type given the context
    /// </summary>
    /// <param name="objectType">The type of the object this method is called on (for $same, $element)</param>
    /// <param name="parameterTypes">The types of parameters passed to the function (for $same_as_first, $array_of_first)</param>
    /// <returns>The resolved concrete type</returns>
    public abstract TypeInfo Resolve(TypeInfo? objectType = null, TypeInfo[]? parameterTypes = null);

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Polymorphic types can't be assigned to directly - they need to be resolved first
        return false;
    }
}

/// <summary>
/// Polymorphic type that returns the same type as the object instance ($same)
/// </summary>
public class SameAsObjectTypeInfo : PolymorphicTypeInfo
{
    public override string Name => "$same";
    public override PeopleCodeType? PeopleCodeType => Types.PeopleCodeType.SameAsObject;
    public override PeopleCodeType PolymorphicType => Types.PeopleCodeType.SameAsObject;

    // Singleton instance
    public static readonly SameAsObjectTypeInfo Instance = new();

    private SameAsObjectTypeInfo() { }

    public override TypeInfo Resolve(TypeInfo? objectType = null, TypeInfo[]? parameterTypes = null)
    {
        if (objectType == null)
            throw new InvalidOperationException("SameAsObject polymorphic type requires object context");

        return objectType;
    }
}

/// <summary>
/// Polymorphic type that returns the element type of the object (object type minus 1 dimension) ($element)
/// </summary>
public class ElementOfObjectTypeInfo : PolymorphicTypeInfo
{
    public override string Name => "$element";
    public override PeopleCodeType? PeopleCodeType => Types.PeopleCodeType.ElementOfObject;
    public override PeopleCodeType PolymorphicType => Types.PeopleCodeType.ElementOfObject;

    // Singleton instance
    public static readonly ElementOfObjectTypeInfo Instance = new();

    private ElementOfObjectTypeInfo() { }

    public override TypeInfo Resolve(TypeInfo? objectType = null, TypeInfo[]? parameterTypes = null)
    {
        if (objectType == null)
            throw new InvalidOperationException("ElementOfObject polymorphic type requires object context");

        // If the object is an array type, return its element type
        if (objectType is ArrayTypeInfo arrayType)
        {
            if (arrayType.Dimensions == 1)
            {
                // 1D array -> element type
                return arrayType.ElementType ?? AnyTypeInfo.Instance;
            }
            else
            {
                // Multi-dimensional array -> reduce dimensionality by 1
                return new ArrayTypeInfo(arrayType.Dimensions - 1, arrayType.ElementType);
            }
        }

        // For non-array types, we need to check if objectType represents an array via other means
        // This is a placeholder - in practice, we'd need more context about how arrays are represented
        // For now, return Any as fallback
        return AnyTypeInfo.Instance;
    }
}

/// <summary>
/// Polymorphic type that returns the same type as the first parameter ($same_as_first)
/// </summary>
public class SameAsFirstParameterTypeInfo : PolymorphicTypeInfo
{
    public override string Name => "$same_as_first";
    public override PeopleCodeType? PeopleCodeType => Types.PeopleCodeType.SameAsFirstParameter;
    public override PeopleCodeType PolymorphicType => Types.PeopleCodeType.SameAsFirstParameter;

    // Singleton instance
    public static readonly SameAsFirstParameterTypeInfo Instance = new();

    private SameAsFirstParameterTypeInfo() { }

    public override TypeInfo Resolve(TypeInfo? objectType = null, TypeInfo[]? parameterTypes = null)
    {
        if (parameterTypes == null || parameterTypes.Length == 0)
            throw new InvalidOperationException("SameAsFirstParameter polymorphic type requires parameter context");

        return parameterTypes[0];
    }
}

/// <summary>
/// Polymorphic type that returns an array of the first parameter's type ($array_of_first)
/// </summary>
public class ArrayOfFirstParameterTypeInfo : PolymorphicTypeInfo
{
    public override string Name => "$array_of_first";
    public override PeopleCodeType? PeopleCodeType => Types.PeopleCodeType.ArrayOfFirstParameter;
    public override PeopleCodeType PolymorphicType => Types.PeopleCodeType.ArrayOfFirstParameter;

    // Singleton instance
    public static readonly ArrayOfFirstParameterTypeInfo Instance = new();

    private ArrayOfFirstParameterTypeInfo() { }

    public override TypeInfo Resolve(TypeInfo? objectType = null, TypeInfo[]? parameterTypes = null)
    {
        if (parameterTypes == null || parameterTypes.Length == 0)
            throw new InvalidOperationException("ArrayOfFirstParameter polymorphic type requires parameter context");

        var firstParamType = parameterTypes[0];
        if (firstParamType is ArrayTypeInfo ati)
        {
            return new ArrayTypeInfo(ati.Dimensions + 1, ati.ElementType);
        }

        // Create an array of the first parameter's type
        return new ArrayTypeInfo(1, firstParamType);
    }
}

/// <summary>
/// Represents a union return type that can be one of several possible types
/// </summary>
public class UnionReturnTypeInfo : TypeInfo
{
    public override string Name { get; }
    public override TypeKind Kind => TypeKind.Any; // Union types are treated as Any until resolved

    /// <summary>
    /// List of possible return types in this union
    /// </summary>
    public IReadOnlyList<TypeInfo> PossibleTypes { get; }

    public UnionReturnTypeInfo(IEnumerable<TypeInfo> possibleTypes)
    {
        var types = possibleTypes.ToList();
        if (types.Count == 0)
            throw new ArgumentException("Union must have at least one type", nameof(possibleTypes));

        PossibleTypes = types.AsReadOnly();
        Name = string.Join("|", types.Select(t => t.Name));
    }

    /// <summary>
    /// Create from TypeWithDimensionality list
    /// </summary>
    public static UnionReturnTypeInfo FromTypeWithDimensionality(IEnumerable<Functions.TypeWithDimensionality> types)
    {
        var typeInfos = types.Select(t =>
        {
            TypeInfo baseType = t.IsAppClass ?
                new AppClassTypeInfo(t.AppClassPath!) :
                TypeInfo.FromPeopleCodeType(t.Type);

            return t.IsArray ?
                new ArrayTypeInfo(t.ArrayDimensionality, baseType) :
                baseType;
        });

        return new UnionReturnTypeInfo(typeInfos);
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can be assigned to union
        if (other.Kind == TypeKind.Any) return true;

        // Check if the other type is assignable to any of the union types
        return PossibleTypes.Any(t => t.IsAssignableFrom(other));
    }

    /// <summary>
    /// Check if a type is one of the possible types in this union
    /// </summary>
    public bool Contains(TypeInfo type)
    {
        return PossibleTypes.Any(t => t.Equals(type));
    }

    /// <summary>
    /// Get the most specific type that can represent a value assignment
    /// </summary>
    public TypeInfo GetMostSpecificType(TypeInfo assignedType)
    {
        // Find the first union type that can accept the assigned type
        return PossibleTypes.FirstOrDefault(t => t.IsAssignableFrom(assignedType)) ?? AnyTypeInfo.Instance;
    }

    public override TypeInfo GetCommonType(TypeInfo other)
    {
        if (other is UnionReturnTypeInfo otherUnion)
        {
            // Union of unions - combine all possible types
            var combinedTypes = PossibleTypes.Concat(otherUnion.PossibleTypes).Distinct();
            return new UnionReturnTypeInfo(combinedTypes);
        }

        // Check if the other type is already in our union
        if (Contains(other))
        {
            return this;
        }

        // Add the other type to our union
        var expandedTypes = PossibleTypes.Concat(new[] { other });
        return new UnionReturnTypeInfo(expandedTypes);
    }
}
