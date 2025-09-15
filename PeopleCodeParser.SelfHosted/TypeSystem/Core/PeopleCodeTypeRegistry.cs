using System.Collections.Concurrent;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Central registry for all valid PeopleCode types
/// </summary>
public static class PeopleCodeTypeRegistry
{
    #region Class Type Cache

    private static readonly ConcurrentDictionary<string, ClassTypeInfo> _classTypeCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    #endregion

    #region Type Lookup Methods

    /// <summary>
    /// Gets a TypeInfo instance from a PeopleCodeType enum value (fast path)
    /// </summary>
    public static TypeInfo GetPeopleCodeType(PeopleCodeType peopleCodeType)
    {
        return TypeInfo.FromPeopleCodeType(peopleCodeType);
    }

    /// <summary>
    /// Gets a TypeInfo instance by name - unified method that handles all PeopleCode types
    /// This is the primary method for type lookup by name
    /// </summary>
    public static TypeInfo? GetTypeByName(string typeName, string? appClassContext = null)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        // Try to get the PeopleCodeType enum first
        if (TryGetPeopleCodeTypeEnum(typeName, out var peopleCodeType))
        {
            // Handle AppClass specially - requires context
            if (peopleCodeType == PeopleCodeType.AppClass && !string.IsNullOrEmpty(appClassContext))
            {
                return new AppClassTypeInfo(appClassContext);
            }

            // For all other types, use the enum directly via TypeInfo factory
            if (peopleCodeType != PeopleCodeType.AppClass)
            {
                return TypeInfo.FromPeopleCodeType(peopleCodeType);
            }
        }

        // If not found in enum, might be a qualified AppClass name
        if (typeName.Contains(':') || !string.IsNullOrEmpty(appClassContext))
        {
            var qualifiedName = !string.IsNullOrEmpty(appClassContext) ? appClassContext : typeName;
            return new AppClassTypeInfo(qualifiedName);
        }

        return null;
    }

    /// <summary>
    /// Checks if a type name refers to any valid PeopleCode type
    /// </summary>
    public static bool IsValidTypeName(string typeName)
    {
        return GetTypeByName(typeName) != null;
    }


    /// <summary>
    /// Tries to get a PeopleCodeType enum value from a string name (case-insensitive)
    /// </summary>
    public static bool TryGetPeopleCodeTypeEnum(string typeName, out PeopleCodeType peopleCodeType)
    {
        peopleCodeType = default;

        if (string.IsNullOrEmpty(typeName))
            return false;

        // First try direct enum parsing (case-insensitive) for builtin object types
        if (Enum.TryParse<PeopleCodeType>(typeName, true, out peopleCodeType))
        {
            return true;
        }

        // Then check primitive types and special types with explicit mapping
        peopleCodeType = typeName.ToLowerInvariant() switch
        {
            // Special types
            "any" => PeopleCodeType.Any,
            "void" => PeopleCodeType.Void,
            "unknown" => PeopleCodeType.Unknown,
            "reference" => PeopleCodeType.Reference,
            "appclass" => PeopleCodeType.AppClass,
            "object" => PeopleCodeType.Object,

            // Primitive types
            "string" => PeopleCodeType.String,
            "integer" => PeopleCodeType.Integer,
            "number" => PeopleCodeType.Number,
            "date" => PeopleCodeType.Date,
            "datetime" => PeopleCodeType.DateTime,
            "time" => PeopleCodeType.Time,
            "boolean" => PeopleCodeType.Boolean,

            _ => default
        };

        return peopleCodeType != default;
    }


    /// <summary>
    /// Attempts to retrieve cached class metadata for the specified fully qualified class name.
    /// </summary>
    public static bool TryGetClassInfo(string qualifiedClassName, out ClassTypeInfo? classInfo)
    {
        if (string.IsNullOrWhiteSpace(qualifiedClassName))
        {
            classInfo = null;
            return false;
        }

        return _classTypeCache.TryGetValue(qualifiedClassName, out classInfo);
    }

    /// <summary>
    /// Adds or replaces cached metadata for the specified class.
    /// </summary>
    public static void CacheClassInfo(ClassTypeInfo classInfo)
    {
        if (classInfo == null)
        {
            throw new ArgumentNullException(nameof(classInfo));
        }

        _classTypeCache[classInfo.QualifiedName] = classInfo;
    }

    /// <summary>
    /// Clears all cached application class metadata.
    /// </summary>
    public static void ClearClassInfoCache()
    {
        _classTypeCache.Clear();
    }



    /// <summary>
    /// Gets all PeopleCode type enum values (excluding AppClass which requires context)
    /// </summary>
    public static IEnumerable<PeopleCodeType> GetAllPeopleCodeTypes()
    {
        // Return all enum values except AppClass
        return Enum.GetValues<PeopleCodeType>().Where(bt => bt != PeopleCodeType.AppClass);
    }

    /// <summary>
    /// Gets all primitive type enum values
    /// </summary>
    public static IEnumerable<PeopleCodeType> GetAllPrimitiveTypes()
    {
        return Enum.GetValues<PeopleCodeType>().Where(bt => bt.IsPrimitive());
    }

    /// <summary>
    /// Gets all builtin object type enum values
    /// </summary>
    public static IEnumerable<PeopleCodeType> GetAllBuiltinObjectTypes()
    {
        return Enum.GetValues<PeopleCodeType>().Where(bt => bt.IsBuiltinObject());
    }

    #endregion

    #region Registry Management

    /// <summary>
    /// Gets all registered type names (for debugging/introspection)
    /// </summary>
    public static IEnumerable<string> GetAllBuiltinTypeNames()
    {
        // Return all PeopleCode type names except AppClass (which is dynamic)
        return Enum.GetValues<PeopleCodeType>()
            .Where(t => t != PeopleCodeType.AppClass)
            .Select(t => t.GetTypeName());
    }

    #endregion
}
