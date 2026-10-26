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
    /// Indicates whether this expression represents a variable or other assignable location
    /// that can receive an 'out' parameter. This property does NOT propagate through expressions.
    /// For example: &myVar has IsAssignable=true, but &myVar | " cat" has IsAssignable=false.
    /// </summary>
    public bool IsAssignable { get; init; } = false;

    /// <summary>
    /// Indicates that this type info came from an auto-declar inferal. So we can warn instead of error
    /// on type errors.
    /// </summary>
    public bool IsAutoDeclared { get; init; } = false;

    /// <summary>
    /// Set when this expression is a buffer field reference (bare REC.FIELD, or a bare FIELD in
    /// record PeopleCode). The type itself stays the field's data type; this marker additionally
    /// lets the expression satisfy field-name parameters (signature prefix '*'). Like IsAssignable,
    /// it does NOT propagate through expressions, variables, or call results.
    /// </summary>
    public BufferFieldReference? BufferField { get; private set; }

    /// <summary>
    /// Returns a new TypeInfo instance carrying the given buffer field reference (or none).
    /// If the value is the same as the current value, returns this instance.
    /// </summary>
    public TypeInfo WithBufferField(BufferFieldReference? bufferField)
    {
        if (BufferField == bufferField) return this;
        var clone = CloneWithState(isAssignable: IsAssignable, isAutoDeclared: IsAutoDeclared);
        clone.BufferField = bufferField;
        clone.Identity = Identity;
        return clone;
    }

    /// <summary>
    /// The definition names this value is known to belong to (record, field, component interface),
    /// if any. Unlike BufferField, identity travels with the value through variables and call
    /// results. It is NOT part of Equals; compare with TypeIdentity.IsRefinementOf / ConflictsWith.
    /// </summary>
    public TypeIdentity? Identity { get; private set; }

    /// <summary>
    /// The identity to compare against. RecordTypeInfo and FieldTypeInfo expose their names here,
    /// so records, fields and identity-carrying types compare uniformly.
    /// </summary>
    public virtual TypeIdentity? EffectiveIdentity => Identity;

    /// <summary>
    /// Returns a new TypeInfo instance carrying the given identity (or none).
    /// If the value is the same as the current value, returns this instance.
    /// </summary>
    public TypeInfo WithIdentity(TypeIdentity? identity)
    {
        if (Identity == identity) return this;
        var clone = CloneWithState(isAssignable: IsAssignable, isAutoDeclared: IsAutoDeclared);
        clone.BufferField = BufferField;
        clone.Identity = identity;
        return clone;
    }

    /// <summary>
    /// Creates a shallow copy of this TypeInfo instance with optional state modifications.
    /// Subclasses must implement this to support the immutable builder pattern.
    /// </summary>
    protected abstract TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null);

    /// <summary>
    /// Returns a new TypeInfo instance with the specified IsAssignable value.
    /// If the value is the same as the current value, returns this instance.
    /// </summary>
    public TypeInfo WithAssignable(bool isAssignable)
    {
        if (IsAssignable == isAssignable) return this;
        var clone = CloneWithState(isAssignable: isAssignable, isAutoDeclared: IsAutoDeclared);
        clone.BufferField = BufferField;
        clone.Identity = Identity;
        return clone;
    }

    /// <summary>
    /// Returns a new TypeInfo instance with the specified IsAutoDeclared value.
    /// If the value is the same as the current value, returns this instance.
    /// </summary>
    public TypeInfo WithAutoDeclared(bool isAutoDeclared)
    {
        if (IsAutoDeclared == isAutoDeclared) return this;
        var clone = CloneWithState(isAssignable: IsAssignable, isAutoDeclared: isAutoDeclared);
        clone.BufferField = BufferField;
        clone.Identity = Identity;
        return clone;
    }

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
        if (Kind == TypeKind.Any) return AnyTypeInfo.Instance;
        if (other.Kind == TypeKind.Any) return AnyTypeInfo.Instance;

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

        // Page and Panel are bidirectionally compatible, common type is Page
        if ((type1 == Types.PeopleCodeType.Page && type2 == Types.PeopleCodeType.Panel) ||
            (type1 == Types.PeopleCodeType.Panel && type2 == Types.PeopleCodeType.Page))
        {
            return Types.PeopleCodeType.Page;
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
    public static TypeInfo FromPeopleCodeType(PeopleCodeType peopleCodeType, bool assignable = false)
    {
        return peopleCodeType switch
        {
            // Special types
            Types.PeopleCodeType.Any => AnyTypeInfo.Instance,
            Types.PeopleCodeType.Binary => AnyTypeInfo.Instance,
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
    Binary = 27,

    // Builtin object types (30-255)
    AeSection = 30,
    AnalyticGrid = 31,
    AnalyticGridColumn = 32,
    AnalyticInstance = 33,
    AnalyticModel = 34,
    ApiObject = 35,
    BIDocs = 36,
    Chart = 37,
    Collection = 38,
    CompositeQuery = 39,
    Compound = 40,
    Cookie = 41,
    CQChunkedProcessor = 42,
    CQRuntime = 43,
    Crypt = 44,
    CubeCollection = 45,
    DialGauge = 46,
    Document = 47,
    DocumentKey = 48,
    Field = 49,
    FieldDefn = 50,
    File = 51,
    FileDefn = 52,
    FileDefnElement = 53,
    FileDefnElementCollection = 54,
    FileDefnManager = 55,
    FileDefnMap = 56,
    FileMetadata = 57,
    Gantt = 58,
    GaugeChart = 59,
    GaugeThreshold = 60,
    Grid = 61,
    GridColumn = 62,
    IBConnectorInfo = 63,
    IBInfo = 64,
    IntBroker = 65,
    Interlink = 66,
    JavaObject = 67,
    JsonArray = 68,
    JsonBuilder = 69,
    JsonGenerator = 70,
    JsonNode = 71,
    JsonObject = 72,
    JsonParser = 73,
    JsonValue = 74,
    LEDGauge = 75,
    Map = 76,
    MapElement = 77,
    MapPage = 78,
    MCFIMInfo = 79,
    Message = 80,
    MFileMetadata = 81,
    OptEngine = 82,
    OptInterface = 83,
    OrgChart = 84,
    Page = 85,
    Panel = 86,
    PostReport = 87,
    PPMClass = 88,
    Primitive = 89,
    ProcessRequest = 90,
    PSEvent = 91,
    PSOCIDataScience = 92,
    PSSpreadsheet = 93,
    PTAppSysVar = 94,
    PTBatchSysVar = 95,
    PTDbSysVar = 96,
    PTDirectTransferObject = 97,
    PTRpsSysVar = 98,
    PTSysVar = 99,
    PTWebSysVar = 100,
    PVGEngine = 101,
    QuadrantSchema = 102,
    RatingBoxChart = 103,
    RatingGaugeChart = 104,
    RatingGaugeState = 105,
    Record = 106,
    RecordDefn = 107,
    RecordFieldDefn = 108,
    ReferenceArea = 109,
    ReferenceLine = 110,
    Request = 111,
    Response = 112,
    Row = 113,
    Rowset = 114,
    RowsetCache = 115,
    RunControlManager = 116,
    SchemaLevel = 117,
    Series = 118,
    SoapDoc = 119,
    SparkChart = 120,
    SparkChartItem = 121,
    SQL = 122,
    StatusMeterGauge = 123,
    SyncServer = 124,
    Threshold = 125,
    Timeline = 126,
    ToolTipLabel = 127,
    TransformData = 128,
    XmlDoc = 129,
    XmlDocFactory = 130,
    XmlLink = 131,
    XmlNode = 132,

    // Additional reference type keywords (133-148)
    BarName = 133,
    BusActivity = 134,
    BusEvent = 135,
    BusProcess = 136,
    CompIntfc = 137,
    Component = 138,
    FileLayout = 139,
    Html = 140,
    Image = 141,
    ItemName = 142,
    MenuName = 143,
    Node = 144,
    Package = 145,
    PanelGroup = 146,
    StyleSheet = 147,
    Url = 148,
    Portal = 149,
    Market = 150,
    
    Hash = 152,
    Exception = 153,

    // PeopleSoft API object types (ApiObject subtypes; declared as ApiObject in PeopleCode)
    // Session / CI / Portal / Query / Search / Tree and related collections
    Session = 154,
    ComponentInterface = 155,
    CompIntfcCollection = 156,
    PSMessage = 157,
    PSMessageCollection = 158,
    RegionalSettings = 159,
    TraceSettings = 160,
    Repository = 161,
    PropertyInfo = 162,
    PropertyInfoCollection = 163,
    DataCollection = 164,
    PortalRegistry = 165,
    PortalRegistryCollection = 166,
    NodeCollection = 167,
    ContentReference = 168,
    Folder = 169,
    Query = 170,
    QueryCollection = 171,
    QueryRecord = 172,
    QueryField = 173,
    QueryCriteria = 174,
    QueryExpression = 175,
    QueryDBRecord = 176,
    QueryDBRecordCollection = 177,
    QuerySecurityProfile = 178,
    SearchIndexCollection = 179,
    SearchQuery = 180,
    Tree = 181,
    TreeStructure = 182,
    TreeNode = 183,
    TreeLevel = 184,
    TreeNodeCollection = 185,

    // Portal Registry API types (continued)
    RemoteNodeCollection = 186,
    PortalCollection = 187,
    NodeTemplate = 188,
    NodeTemplateCollection = 189,
    FolderCollection = 190,
    ContentReferenceCollection = 191,
    AttributeValue = 192,
    AttributeCollection = 193,
    PermissionValue = 194,
    PermissionValueCollection = 195,
    RolePermissionValueCollection = 196,
    Link = 197,
    LinkCollection = 198,
    TabDefinition = 199,
    TabDefinitionCollection = 200,
    AssignedPagelet = 201,
    AssignedPageletCollection = 202,
    AvailableCategory = 203,
    AvailableCategoryCollection = 204,
    AvailablePagelet = 205,
    AvailablePageletCollection = 206,
    DynamicCategoryCollection = 207,
    PageletCategory = 208,
    PageletCategoryCollection = 209,
    Pagelet = 210,
    PageletCollection = 211,
    UserHomepage = 212,
    UserTab = 213,
    UserTabCollection = 214,
    SelectedPagelet = 215,
    SelectedPageletCollection = 216,
    Favorite = 217,
    FavoriteCollection = 218,
    QueryPrompt = 219,
    QuerySelect = 220,
    QueryPromptCollection = 221,
    QueryStatistics = 222,
    QueryMetadataCollection = 223,
    QueryList = 224,
    QueryExpressionCollection = 225,
    QueryDBRecordField = 226,
    QueryListValue = 227,
    QueryCriteriaCollection = 228,
    QueryFieldCollection = 229,
    QueryRecordCollection = 230,
    QuerySelectCollection = 231,
    QueryDBRecordFieldCollection = 232,
    QueryRecordHierarchy = 233,
    QueryRecordHierarchyCollection = 234,
    QueryMetadata = 235,
    Bindings = 236,
    BindingsCollection = 237,
    Namespaces = 238,
    NamespacesCollection = 239,
    ClassInfo = 240,
    ClassInfoCollection = 241,
    MethodInfo = 242,
    MethodInfoCollection = 243,
    DataItem = 244,

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
            "binary" => PeopleCodeType.Binary,
            "hash" => PeopleCodeType.Hash,
            "void" => PeopleCodeType.Void,
            "appclass" => PeopleCodeType.AppClass,
            "exception" => PeopleCodeType.Exception,
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
            "float" => PeopleCodeType.Number,
            "integer" => PeopleCodeType.Integer,
            "number" => PeopleCodeType.Number,
            "date" => PeopleCodeType.Date,
            "datetime" => PeopleCodeType.DateTime,
            "time" => PeopleCodeType.Time,
            "boolean" => PeopleCodeType.Boolean,
            "aesection" => PeopleCodeType.AeSection,
            "analyticgrid" => PeopleCodeType.AnalyticGrid,
            "analyticgridcolumn" => PeopleCodeType.AnalyticGridColumn,
            "analyticinstance" => PeopleCodeType.AnalyticInstance,
            "analyticmodel" => PeopleCodeType.AnalyticModel,
            "apiobject" => PeopleCodeType.ApiObject,
            "bidocs" => PeopleCodeType.BIDocs,
            "chart" => PeopleCodeType.Chart,
            "collection" => PeopleCodeType.Collection,
            "compositequery" => PeopleCodeType.CompositeQuery,
            "compound" => PeopleCodeType.Compound,
            "cookie" => PeopleCodeType.Cookie,
            "cqchunkedprocessor" => PeopleCodeType.CQChunkedProcessor,
            "cqruntime" => PeopleCodeType.CQRuntime,
            "crypt" => PeopleCodeType.Crypt,
            "cubecollection" => PeopleCodeType.CubeCollection,
            "dialgauge" => PeopleCodeType.DialGauge,
            "document" => PeopleCodeType.Document,
            "documentkey" => PeopleCodeType.DocumentKey,
            "field" => PeopleCodeType.Field,
            "fielddefn" => PeopleCodeType.FieldDefn,
            "file" => PeopleCodeType.File,
            "filedefn" => PeopleCodeType.FileDefn,
            "filedefnelement" => PeopleCodeType.FileDefnElement,
            "filedefnelementcollection" => PeopleCodeType.FileDefnElementCollection,
            "filedefnmanager" => PeopleCodeType.FileDefnManager,
            "filedefnmap" => PeopleCodeType.FileDefnMap,
            "filemetadata" => PeopleCodeType.FileMetadata,
            "gantt" => PeopleCodeType.Gantt,
            "gaugechart" => PeopleCodeType.GaugeChart,
            "gaugethreshold" => PeopleCodeType.GaugeThreshold,
            "grid" => PeopleCodeType.Grid,
            "gridcolumn" => PeopleCodeType.GridColumn,
            "ibconnectorinfo" => PeopleCodeType.IBConnectorInfo,
            "ibinfo" => PeopleCodeType.IBInfo,
            "intbroker" => PeopleCodeType.IntBroker,
            "interlink" => PeopleCodeType.Interlink,
            "javaobject" => PeopleCodeType.JavaObject,
            "jsonarray" => PeopleCodeType.JsonArray,
            "jsonbuilder" => PeopleCodeType.JsonBuilder,
            "jsongenerator" => PeopleCodeType.JsonGenerator,
            "jsonnode" => PeopleCodeType.JsonNode,
            "jsonobject" => PeopleCodeType.JsonObject,
            "jsonparser" => PeopleCodeType.JsonParser,
            "jsonvalue" => PeopleCodeType.JsonValue,
            "ledgauge" => PeopleCodeType.LEDGauge,
            "market" => PeopleCodeType.Market,
            "map" => PeopleCodeType.Map,
            "mapelement" => PeopleCodeType.MapElement,
            "mappage" => PeopleCodeType.MapPage,
            "mcfiminfo" => PeopleCodeType.MCFIMInfo,
            "message" => PeopleCodeType.Message,
            "mfilemetadata" => PeopleCodeType.MFileMetadata,
            "optengine" => PeopleCodeType.OptEngine,
            "optinterface" => PeopleCodeType.OptInterface,
            "orgchart" => PeopleCodeType.OrgChart,
            "page" => PeopleCodeType.Page,
            "panel" => PeopleCodeType.Panel,
            "portal" => PeopleCodeType.Portal,
            "postreport" => PeopleCodeType.PostReport,
            "ppmclass" => PeopleCodeType.PPMClass,
            "primitive" => PeopleCodeType.Primitive,
            "processrequest" => PeopleCodeType.ProcessRequest,
            "psevent" => PeopleCodeType.PSEvent,
            "psocidatascience" => PeopleCodeType.PSOCIDataScience,
            "psspreadsheet" => PeopleCodeType.PSSpreadsheet,
            "ptappsysvar" => PeopleCodeType.PTAppSysVar,
            "ptbatchsysvar" => PeopleCodeType.PTBatchSysVar,
            "ptdbsysvar" => PeopleCodeType.PTDbSysVar,
            "ptdirecttransferobject" => PeopleCodeType.PTDirectTransferObject,
            "ptrpssysvar" => PeopleCodeType.PTRpsSysVar,
            "ptsysvar" => PeopleCodeType.PTSysVar,
            "ptwebsysvar" => PeopleCodeType.PTWebSysVar,
            "pvgengine" => PeopleCodeType.PVGEngine,
            "quadrantschema" => PeopleCodeType.QuadrantSchema,
            "ratingboxchart" => PeopleCodeType.RatingBoxChart,
            "ratinggaugechart" => PeopleCodeType.RatingGaugeChart,
            "ratinggaugestate" => PeopleCodeType.RatingGaugeState,
            "record" => PeopleCodeType.Record,
            "recorddefn" => PeopleCodeType.RecordDefn,
            "recordfielddefn" => PeopleCodeType.RecordFieldDefn,
            "referencearea" => PeopleCodeType.ReferenceArea,
            "referenceline" => PeopleCodeType.ReferenceLine,
            "request" => PeopleCodeType.Request,
            "response" => PeopleCodeType.Response,
            "row" => PeopleCodeType.Row,
            "rowset" => PeopleCodeType.Rowset,
            "rowsetcache" => PeopleCodeType.RowsetCache,
            "runcontrolmanager" => PeopleCodeType.RunControlManager,
            "schemalevel" => PeopleCodeType.SchemaLevel,
            "series" => PeopleCodeType.Series,
            "soapdoc" => PeopleCodeType.SoapDoc,
            "sparkchart" => PeopleCodeType.SparkChart,
            "sparkchartitem" => PeopleCodeType.SparkChartItem,
            "sql" => PeopleCodeType.SQL,
            "statusmetergauge" => PeopleCodeType.StatusMeterGauge,
            "syncserver" => PeopleCodeType.SyncServer,
            "threshold" => PeopleCodeType.Threshold,
            "timeline" => PeopleCodeType.Timeline,
            "tooltiplabel" => PeopleCodeType.ToolTipLabel,
            "transformdata" => PeopleCodeType.TransformData,
            "xmldoc" => PeopleCodeType.XmlDoc,
            "xmldocfactory" => PeopleCodeType.XmlDocFactory,
            "xmllink" => PeopleCodeType.XmlLink,
            "xmlnode" => PeopleCodeType.XmlNode,
            "barname" => PeopleCodeType.BarName,
            "busactivity" => PeopleCodeType.BusActivity,
            "busevent" => PeopleCodeType.BusEvent,
            "busprocess" => PeopleCodeType.BusProcess,
            "compintfc" => PeopleCodeType.CompIntfc,
            "component" => PeopleCodeType.Component,
            "filelayout" => PeopleCodeType.FileLayout,
            "html" => PeopleCodeType.Html,
            "image" => PeopleCodeType.Image,
            "itemname" => PeopleCodeType.ItemName,
            "menuname" => PeopleCodeType.MenuName,
            "node" => PeopleCodeType.Node,
            "package" => PeopleCodeType.Package,
            "panelgroup" => PeopleCodeType.PanelGroup,
            "stylesheet" => PeopleCodeType.StyleSheet,
            "url" => PeopleCodeType.Url,

            // ApiObject subtypes
            "session" => PeopleCodeType.Session,
            "componentinterface" => PeopleCodeType.ComponentInterface,
            "compintfccollection" => PeopleCodeType.CompIntfcCollection,
            "psmessage" => PeopleCodeType.PSMessage,
            "psmessagecollection" => PeopleCodeType.PSMessageCollection,
            "regionalsettings" => PeopleCodeType.RegionalSettings,
            "tracesettings" => PeopleCodeType.TraceSettings,
            "repository" => PeopleCodeType.Repository,
            "propertyinfo" => PeopleCodeType.PropertyInfo,
            "propertyinfocollection" => PeopleCodeType.PropertyInfoCollection,
            "datacollection" => PeopleCodeType.DataCollection,
            "portalregistry" => PeopleCodeType.PortalRegistry,
            "portalregistrycollection" => PeopleCodeType.PortalRegistryCollection,
            "nodecollection" => PeopleCodeType.NodeCollection,
            "contentreference" => PeopleCodeType.ContentReference,
            "cref" => PeopleCodeType.ContentReference, // alias
            "folder" => PeopleCodeType.Folder,
            "query" => PeopleCodeType.Query,
            "querycollection" => PeopleCodeType.QueryCollection,
            "queryrecord" => PeopleCodeType.QueryRecord,
            "queryfield" => PeopleCodeType.QueryField,
            "querycriteria" => PeopleCodeType.QueryCriteria,
            "queryexpression" => PeopleCodeType.QueryExpression,
            "querydbrecord" => PeopleCodeType.QueryDBRecord,
            "querydbrecordcollection" => PeopleCodeType.QueryDBRecordCollection,
            "querysecurityprofile" => PeopleCodeType.QuerySecurityProfile,
            "searchindexcollection" => PeopleCodeType.SearchIndexCollection,
            "searchquery" => PeopleCodeType.SearchQuery,
            "tree" => PeopleCodeType.Tree,
            "treestructure" => PeopleCodeType.TreeStructure,
            "treenode" => PeopleCodeType.TreeNode,
            "treelevel" => PeopleCodeType.TreeLevel,
            "treenodecollection" => PeopleCodeType.TreeNodeCollection,

            // Portal Registry API types (continued)
            "remotenodecollection" => PeopleCodeType.RemoteNodeCollection,
            "portalcollection" => PeopleCodeType.PortalCollection,
            "nodetemplate" => PeopleCodeType.NodeTemplate,
            "nodetemplatecollection" => PeopleCodeType.NodeTemplateCollection,
            "foldercollection" => PeopleCodeType.FolderCollection,
            "contentreferencecollection" => PeopleCodeType.ContentReferenceCollection,
            "attributevalue" => PeopleCodeType.AttributeValue,
            "attributecollection" => PeopleCodeType.AttributeCollection,
            "permissionvalue" => PeopleCodeType.PermissionValue,
            "permissionvaluecollection" => PeopleCodeType.PermissionValueCollection,
            "rolepermissionvaluecollection" => PeopleCodeType.RolePermissionValueCollection,
            "link" => PeopleCodeType.Link,
            "linkcollection" => PeopleCodeType.LinkCollection,
            "tabdefinition" => PeopleCodeType.TabDefinition,
            "tabdefinitioncollection" => PeopleCodeType.TabDefinitionCollection,
            "assignedpagelet" => PeopleCodeType.AssignedPagelet,
            "assignedpageletcollection" => PeopleCodeType.AssignedPageletCollection,
            "availablecategory" => PeopleCodeType.AvailableCategory,
            "availablecategorycollection" => PeopleCodeType.AvailableCategoryCollection,
            "availablepagelet" => PeopleCodeType.AvailablePagelet,
            "availablepageletcollection" => PeopleCodeType.AvailablePageletCollection,
            "dynamiccategorycollection" => PeopleCodeType.DynamicCategoryCollection,
            "pageletcategory" => PeopleCodeType.PageletCategory,
            "pageletcategorycollection" => PeopleCodeType.PageletCategoryCollection,
            "pagelet" => PeopleCodeType.Pagelet,
            "pageletcollection" => PeopleCodeType.PageletCollection,
            "userhomepage" => PeopleCodeType.UserHomepage,
            "usertab" => PeopleCodeType.UserTab,
            "usertabcollection" => PeopleCodeType.UserTabCollection,
            "selectedpagelet" => PeopleCodeType.SelectedPagelet,
            "selectedpageletcollection" => PeopleCodeType.SelectedPageletCollection,
            "favorite" => PeopleCodeType.Favorite,
            "favoritecollection" => PeopleCodeType.FavoriteCollection,
            "queryprompt" => PeopleCodeType.QueryPrompt,
            "queryselect" => PeopleCodeType.QuerySelect,
            "querypromptcollection" => PeopleCodeType.QueryPromptCollection,
            "querystatistics" => PeopleCodeType.QueryStatistics,
            "querymetadatacollection" => PeopleCodeType.QueryMetadataCollection,
            "querylist" => PeopleCodeType.QueryList,
            "queryexpressioncollection" => PeopleCodeType.QueryExpressionCollection,
            "querydbrecordfield" => PeopleCodeType.QueryDBRecordField,
            "querylistvalue" => PeopleCodeType.QueryListValue,
            "querycriteriacollection" => PeopleCodeType.QueryCriteriaCollection,
            "queryfieldcollection" => PeopleCodeType.QueryFieldCollection,
            "queryrecordcollection" => PeopleCodeType.QueryRecordCollection,
            "queryselectcollection" => PeopleCodeType.QuerySelectCollection,
            "querydbrecordfieldcollection" => PeopleCodeType.QueryDBRecordFieldCollection,
            "queryrecordhierarchy" => PeopleCodeType.QueryRecordHierarchy,
            "queryrecordhierarchycollection" => PeopleCodeType.QueryRecordHierarchyCollection,
            "querymetadata" => PeopleCodeType.QueryMetadata,
            "bindings" => PeopleCodeType.Bindings,
            "bindingscollection" => PeopleCodeType.BindingsCollection,
            "namespaces" => PeopleCodeType.Namespaces,
            "namespacescollection" => PeopleCodeType.NamespacesCollection,
            "classinfo" => PeopleCodeType.ClassInfo,
            "classinfocollection" => PeopleCodeType.ClassInfoCollection,
            "methodinfo" => PeopleCodeType.MethodInfo,
            "methodinfocollection" => PeopleCodeType.MethodInfoCollection,
            "dataitem" => PeopleCodeType.DataItem,

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

            // Builtin object types: PascalCase enum names (PeopleCode is case-insensitive)
            _ => type.ToString()
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
        return type >= PeopleCodeType.AeSection && type <= PeopleCodeType.DataItem;
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

        // Field objects are not scalar-compatible (need explicit .Value).
        // Buffer REC.FIELD is collapsed to its data type at inference time.

        // Any can be assigned to any primitive
        if (other.Kind == TypeKind.Any) return true;

        // @ANY references (dynamic references) can resolve to any type at runtime
        // Example: @("RECORD." | &FIELD) where we can't statically determine the target
        if (other is ReferenceTypeInfo refType && refType.ReferenceCategory == Types.PeopleCodeType.Any)
        {
            return true;
        }

        /* We can pass in references to string parameters */
        if (PeopleCodeType.HasValue && PeopleCodeType == Types.PeopleCodeType.String && other is ReferenceTypeInfo)
        {
            return true;
        }

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

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new PrimitiveTypeInfo(Name, PeopleCodeType)
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    /// <summary>
    /// True when this type is the declarable ApiObject base (not a Session/CI/… subtype).
    /// </summary>
    public bool IsApiObjectBase =>
        PeopleCodeType == Types.PeopleCodeType.ApiObject
        || Name.Equals("ApiObject", StringComparison.OrdinalIgnoreCase);

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Field objects only assign to Field (and Any/Object paths below), not to
        // other builtins via implicit .Value. Buffer REC.FIELD is already a data type.

        // Any can be assigned to any builtin object
        if (other.Kind == TypeKind.Any) return true;

        // Object can be assigned to any builtin object (implicit widening)
        // This allows &variable As object to be passed where specific types like ApiObject are expected
        if (other is ObjectTypeInfo) return true;

        // @ANY references (dynamic references) can resolve to any type at runtime
        // Example: @("RECORD." | &FIELD) where we can't statically determine the target
        if (other is ReferenceTypeInfo refType && refType.ReferenceCategory == Types.PeopleCodeType.Any)
        {
            return true;
        }

        // ApiObject accepts any API subtype (Session, ComponentInterface, …) and itself
        if (IsApiObjectBase)
        {
            if (other is BuiltinObjectTypeInfo otherApi)
            {
                if (otherApi.IsApiObjectBase || otherApi.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (Database.PeopleCodeTypeDatabase.IsApiObjectSubtype(otherApi.Name))
                    return true;
            }
        }

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

            // Special compatibility: PAGE and PANEL are bidirectionally compatible
            if ((PeopleCodeType.Value == Types.PeopleCodeType.Page && other.PeopleCodeType.Value == Types.PeopleCodeType.Panel) ||
                (PeopleCodeType.Value == Types.PeopleCodeType.Panel && other.PeopleCodeType.Value == Types.PeopleCodeType.Page))
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

            // Name-based compatibility for PAGE and PANEL
            if ((thisName == "page" && otherName == "panel") ||
                (thisName == "panel" && otherName == "page"))
            {
                return true;
            }
        }

        // Check if other is an AppClass that extends this builtin type
        if (other is AppClassTypeInfo appClass)
        {
            return appClass.InheritanceChain.Any(c => c.QualifiedName.Equals(Name, StringComparison.InvariantCultureIgnoreCase));
        }

        return false;
    }

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new BuiltinObjectTypeInfo(Name, PeopleCodeType)
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
    }
}

/// <summary>
/// Represents an entry in an application class's inheritance chain
/// </summary>
public readonly struct InheritanceChainEntry
{
    /// <summary>
    /// Qualified name of the class or interface (e.g., "PKG:MyClass" or "Record")
    /// </summary>
    public string QualifiedName { get; }

    /// <summary>
    /// True if this is a builtin type (no package - determined by absence of ':')
    /// </summary>
    public bool IsBuiltin { get; }

    public InheritanceChainEntry(string qualifiedName, bool isBuiltin)
    {
        QualifiedName = qualifiedName ?? throw new ArgumentNullException(nameof(qualifiedName));
        IsBuiltin = isBuiltin;
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

    /// <summary>
    /// True if this AppClass extends a builtin type (like Record, Field, etc.)
    /// </summary>
    public bool ExtendsBuiltinType { get; }

    /// <summary>
    /// The PeopleCodeType of the builtin base class, if ExtendsBuiltinType is true
    /// </summary>
    public PeopleCodeType? BuiltinBaseType { get; }

    /// <summary>
    /// The complete inheritance hierarchy chain from this class to its ultimate base.
    /// First entry is this class, subsequent entries are base classes/interfaces (extends or implements).
    /// Empty list if no inheritance information is available.
    /// </summary>
    public IReadOnlyList<InheritanceChainEntry> InheritanceChain { get; }

    public AppClassTypeInfo(string qualifiedName, bool extendsBuiltinType = false, PeopleCodeType? builtinBaseType = null, IReadOnlyList<InheritanceChainEntry>? inheritanceChain = null)
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

        ExtendsBuiltinType = extendsBuiltinType;
        BuiltinBaseType = builtinBaseType;
        InheritanceChain = inheritanceChain ?? Array.Empty<InheritanceChainEntry>();
    }

    /// <summary>
    /// Create an AppClassTypeInfo with complete inheritance chain and builtin base class information if available.
    /// Traverses the inheritance chain (checking both BaseClassName and InterfaceName) to build the complete hierarchy.
    /// </summary>
    /// <param name="qualifiedName">The fully qualified name of the AppClass (e.g., "PKG:MyClass")</param>
    /// <param name="resolver">Type metadata resolver to resolve base class metadata</param>
    /// <param name="cache">Type cache for performance optimization</param>
    /// <returns>AppClassTypeInfo with complete inheritance chain information</returns>
    public static AppClassTypeInfo CreateWithInheritanceChain(
        string qualifiedName,
        Contracts.ITypeMetadataResolver resolver,
        Inference.TypeCache cache)
    {
        // Build the complete inheritance chain
        var chain = new List<InheritanceChainEntry>();
        var currentClassName = qualifiedName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentClassName };

        // Track builtin base information for backward compatibility
        bool extendsBuiltinType = false;
        PeopleCodeType? builtinBaseType = null;

        // Add the starting class to the chain
        chain.Add(new InheritanceChainEntry(
            qualifiedName,
            isBuiltin: !qualifiedName.Contains(':')
        ));

        while (!string.IsNullOrEmpty(currentClassName))
        {
            // Try to get metadata - check cache first, then resolver
            Inference.TypeMetadata? metadata = cache.Get(currentClassName);
            if (metadata == null && resolver != null)
            {
                metadata = resolver.GetTypeMetadata(currentClassName);
                if (metadata != null)
                    cache.Set(currentClassName, metadata);
            }

            if (metadata == null)
            {
                break; // Can't resolve metadata
            }

            // Check if this class directly extends a builtin type
            if (metadata.IsBaseClassBuiltin && metadata.BuiltinBaseType.HasValue)
            {
                extendsBuiltinType = true;
                builtinBaseType = metadata.BuiltinBaseType.Value;

                // Add builtin to chain
                var builtinTypeName = metadata.BuiltinBaseType.Value.GetTypeName();
                chain.Add(new InheritanceChainEntry(
                    builtinTypeName,
                    isBuiltin: true
                ));

                break; // Builtin is terminal
            }

            // Check for next class in hierarchy - check both BaseClassName and InterfaceName
            // (PeopleCode allows either extends or implements, but not both, and they're interchangeable)
            string? nextClassName = null;
            if (!string.IsNullOrEmpty(metadata.BaseClassName))
            {
                nextClassName = metadata.BaseClassName;
            }
            else if (!string.IsNullOrEmpty(metadata.InterfaceName))
            {
                nextClassName = metadata.InterfaceName;
            }

            if (nextClassName == null)
            {
                break; // No more base classes/interfaces
            }

            // Circular inheritance detection
            if (!visited.Add(nextClassName))
            {
                break; // Detected circular inheritance
            }

            // Add base class/interface to chain
            chain.Add(new InheritanceChainEntry(
                nextClassName,
                isBuiltin: !nextClassName.Contains(':')
            ));

            // Move to the next class in hierarchy
            currentClassName = nextClassName;
        }

        return new AppClassTypeInfo(qualifiedName, extendsBuiltinType, builtinBaseType, chain);
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can be assigned to any app class
        if (other.Kind == TypeKind.Any) return true;

        // Object can be assigned to any AppClass (implicit widening)
        // This allows &variable As object to be passed where specific AppClass types are expected
        if (other is ObjectTypeInfo) return true;

        // Same app class type
        if (other is AppClassTypeInfo appClass &&
            appClass.QualifiedName.Equals(QualifiedName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Metadata keeps types as written ("Config") while inference qualifies values
        // ("PKG:Config"), so a bare type matches on the class name. Weak-spots §3 approximation:
        // two classes with the same short name in different packages are treated as the same.
        if (PackagePath.Count == 0 && other is AppClassTypeInfo o &&
            o.ClassName.Equals(ClassName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new AppClassTypeInfo(QualifiedName, ExtendsBuiltinType, BuiltinBaseType, InheritanceChain)
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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
        if (dimensions < 0 || dimensions > 9)
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Array dimensions must be between 0 and 9 (0 = wildcard for any dimensionality)");

        Dimensions = dimensions;
        ElementType = elementType;
    }

    public override bool IsAssignableFrom(TypeInfo other)
    {
        // Null safety - should never happen, but protect against edge cases
        if (other == null) return false;

        // Any can be assigned to any array
        if (other.Kind == TypeKind.Any) return true;

        // Special case: dimensions==0 means "match any array dimensionality"
        if (other is ArrayTypeInfo array)
        {
            // If this is a wildcard (0 dimensions), match any array
            if (Dimensions == 0) return true;

            // Otherwise require exact dimension match
            if (array.Dimensions == Dimensions)
            {
                // Untyped arrays are compatible
                if (ElementType == null || array.ElementType == null) return true;

                // Check element type compatibility (with null safety)
                return ElementType?.IsAssignableFrom(array.ElementType) ?? true;
            }
        }

        if (other is ObjectTypeInfo obj && obj.PeopleCodeType == Types.PeopleCodeType.Object) return true;

        return false;
    }

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new ArrayTypeInfo(Dimensions, ElementType)
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    internal ObjectTypeInfo() { }

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

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new ObjectTypeInfo()
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    internal AnyTypeInfo() { }

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

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new AnyTypeInfo()
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    internal VoidTypeInfo() { }

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

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new VoidTypeInfo()
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new UnknownTypeInfo(Name)
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new InvalidTypeInfo(Reason)
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    public override string ToString()
    {
        if (ReferenceCategory == Types.PeopleCodeType.Any)
            return "Dynamic Reference";

        return Name;
    }

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

        /* Support the ability to do @("RECORD.FIELD") = &value */
        /* if left side is an @Any() we will always accept any primitive value */
        if (ReferenceCategory == Types.PeopleCodeType.Any && other is PrimitiveTypeInfo)
        {
            return true;
        }

        // Buffer REC.FIELD is typed as its data type (primitive); Field objects can
        // also be stored through @Any at runtime.
        if (ReferenceCategory == Types.PeopleCodeType.Any && other is FieldTypeInfo)
        {
            return true;
        }

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

            if (ReferenceCategory == Types.PeopleCodeType.Page && otherRef.ReferenceCategory == Types.PeopleCodeType.Panel)
                return true; /* You can pass a @Page into a @Panel */

            if (ReferenceCategory == Types.PeopleCodeType.Panel && otherRef.ReferenceCategory == Types.PeopleCodeType.Page)
                return true; /* You can pass a @Panel into a @Page */

            if (otherRef.ReferenceCategory == Types.PeopleCodeType.Any)
            {
                /* An Any reference category means this was an @() expression, we can't statically know the type) */
                return true;
            }

            return ReferenceCategory == otherRef.ReferenceCategory;
        }

        /* Support the ability to do @("RECORD.FIELD") = &value */


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

    // MARKET is deliberately absent: the compiler parses Market.GBL as REC.FIELD (use "GBL" / %Market).
    private static readonly HashSet<string> SpecialReferenceKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ANALYTICMODEL", "BARNAME", "BUSACTIVITY", "BUSEVENT", "BUSPROCESS", "COMPINTFC", "COMPONENT",
        "FIELD", "FILELAYOUT", "HTML", "IMAGE", "INTERLINK", "ITEMNAME", "MENUNAME",
        "MESSAGE", "NODE", "OPERATION", "PACKAGE", "PAGE", "PANEL", "PANELGROUP", "PORTAL",
        "RECORD", "SCROLL", "SQL", "STYLESHEET", "URL"
    };

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new ReferenceTypeInfo(ReferenceCategory, ReferencedName, FullReference)
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
    }
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

        // Field objects need explicit .Value for string contexts.
        // Buffer REC.FIELD is collapsed to its data type at inference time.

        // Any can be assigned to string
        if (other.Kind == TypeKind.Any) return true;

        // Reference types can be assigned to string (Record.NAME, SQL.NAME, etc.)
        // Example: &str = Record.FOO; or ScrollSelectNew(..., "", Record.FOO, ...)
        if (other is ReferenceTypeInfo)
        {
            return true;
        }

        // Same type (string)
        if (other.PeopleCodeType.HasValue && other.PeopleCodeType.Value == Types.PeopleCodeType.String) return true;
        if (other is PrimitiveTypeInfo primitive && primitive.Name.Equals("string", StringComparison.OrdinalIgnoreCase)) return true;

        // String can ONLY accept string values - no implicit conversions (except references handled above)
        return false;
    }

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new StringTypeInfo()
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

        // Field objects need explicit .Value for number contexts.
        // Buffer REC.FIELD is collapsed to its data type at inference time.

        // Any can be assigned to number
        if (other.Kind == TypeKind.Any) return true;

        // @ANY references (dynamic references) can resolve to any type at runtime
        // Example: UpdateValue(@("FIELD_" | &NAME), ...) where we can't statically determine the field
        if (other is ReferenceTypeInfo refType && refType.ReferenceCategory == Types.PeopleCodeType.Any)
        {
            return true;
        }

        // Same type (number)
        if (other.PeopleCodeType.HasValue && other.PeopleCodeType.Value == Types.PeopleCodeType.Number) return true;
        if (other is PrimitiveTypeInfo primitive && primitive.Name.Equals("number", StringComparison.OrdinalIgnoreCase)) return true;

        // Number accepts integer (bidirectional compatibility)
        if (other.PeopleCodeType.HasValue && other.PeopleCodeType.Value == Types.PeopleCodeType.Integer) return true;
        if (other is PrimitiveTypeInfo intPrimitive && intPrimitive.Name.Equals("integer", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new NumberTypeInfo()
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

        var resolvedType = Resolve();
        return resolvedType.IsAssignableFrom(other);

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

    internal SameAsObjectTypeInfo() { }

    public override TypeInfo Resolve(TypeInfo? objectType = null, TypeInfo[]? parameterTypes = null)
    {
        if (objectType == null)
            throw new InvalidOperationException("SameAsObject polymorphic type requires object context");

        return objectType;
    }

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new SameAsObjectTypeInfo()
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    internal ElementOfObjectTypeInfo() { }

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

        return AnyTypeInfo.Instance;
    }

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new ElementOfObjectTypeInfo()
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    internal SameAsFirstParameterTypeInfo() { }

    public override TypeInfo Resolve(TypeInfo? objectType = null, TypeInfo[]? parameterTypes = null)
    {
        if (parameterTypes == null || parameterTypes.Length == 0)
            throw new InvalidOperationException("SameAsFirstParameter polymorphic type requires parameter context");

        // Field objects stay Field; buffer REC.FIELD is already a data type on the arg node.
        return parameterTypes[0];
    }

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new SameAsFirstParameterTypeInfo()
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    internal ArrayOfFirstParameterTypeInfo() { }

    public override TypeInfo Resolve(TypeInfo? objectType = null, TypeInfo[]? parameterTypes = null)
    {
        if (parameterTypes == null || parameterTypes.Length == 0)
            throw new InvalidOperationException("ArrayOfFirstParameter polymorphic type requires parameter context");

        var firstParamType = parameterTypes[0];
        if (firstParamType is ArrayTypeInfo ati)
        {
            return new ArrayTypeInfo(ati.Dimensions + 1, ati.ElementType);
        }

        // Create an array of the first parameter's type (Field stays Field)
        return new ArrayTypeInfo(1, firstParamType);
    }

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new ArrayOfFirstParameterTypeInfo()
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
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

    protected override TypeInfo CloneWithState(bool? isAssignable = null, bool? isAutoDeclared = null)
    {
        return new UnionReturnTypeInfo(PossibleTypes)
        {
            IsAssignable = isAssignable ?? IsAssignable,
            IsAutoDeclared = isAutoDeclared ?? IsAutoDeclared
        };
    }
}
