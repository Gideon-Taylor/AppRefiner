using PeopleCodeTypeInfo.HashTable;
using PeopleCodeTypeInfo.HashTable.Strategies;
using PeopleCodeTypeInfo.Types;
using System.Text;

namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Strategy for reading BuiltinObjectInfo from hash table storage.
/// Handles both linear storage (for small objects) and hash table storage (for large objects like System).
/// </summary>
public class BuiltinObjectReaderStrategy : VariableSizeReaderStrategy<BuiltinObjectInfo>
{
    private const int DATA_OFFSET_BASE = 4 + (2048 * 8); // header + hash table size for default table

    private readonly INameTable? _nameTable;

    public BuiltinObjectReaderStrategy(Func<BuiltinObjectInfo, uint> stringHashFunc, INameTable? nameTable = null, int tableSize = 2048) : base(
        hashFunc: stringHashFunc,
        readDataFunc: (reader, offset, dataSection) => ReadObject(reader, offset, dataSection, nameTable))
    {
        _nameTable = nameTable;
    }

    private static BuiltinObjectInfo ReadObject(BinaryReader reader, uint offset, byte[] dataSection, INameTable? nameTable)
    {
        // Calculate the data section base offset: header(4) + names table size + hash table size
        int namesTableSize = nameTable?.NamesTableSize ?? 0;

        uint dataOffsetBase = (uint)(4 + namesTableSize + (2048 * 8)); // header + names + hash table
        uint dataOffset = offset - dataOffsetBase;
        var memoryStream = new MemoryStream(dataSection, (int)dataOffset, dataSection.Length - (int)dataOffset);
        var dataReader = new BinaryReader(memoryStream);

        var defaultMethodHash = dataReader.ReadUInt32();

        // Read reserved flags (ignore for now)
        var flags = dataReader.ReadByte();

        var obj = new BuiltinObjectInfo("") { DefaultMethodHash = defaultMethodHash };

        // Read methods
        ReadMethods(dataReader, obj, nameTable);

        // Read properties
        ReadProperties(dataReader, obj, nameTable);

        return obj;
    }

    private static void ReadMethods(BinaryReader reader, BuiltinObjectInfo obj, INameTable? nameTable)
    {
        var methodCount = reader.ReadInt32();

        obj.Methods = new Dictionary<uint, FunctionInfo>();

        for (int i = 0; i < methodCount; i++)
        {
            var hash = reader.ReadUInt32();
            var method = ReadFunctionInfo(reader, nameTable);
            obj.Methods[hash] = method;
        }
    }

    private static void ReadProperties(BinaryReader reader, BuiltinObjectInfo obj, INameTable? nameTable)
    {
        var propertyCount = reader.ReadInt32();

        obj.Properties = new Dictionary<uint, PropertyInfo>();

        for (int i = 0; i < propertyCount; i++)
        {
            var hash = reader.ReadUInt32();
            var property = reader.ReadPropertyInfo();
            obj.Properties[hash] = property;
        }
    }

    private static FunctionInfo ReadFunctionInfo(BinaryReader reader, INameTable? nameTable)
    {
        // Read flags byte
        byte flags = reader.ReadByte();
        bool hasUnionReturn = (flags & 0x01) != 0;
        bool isDefault = (flags & 0x02) != 0;
        bool isProperty = (flags & 0x04) != 0;
        bool isOptionalReturn = (flags & 0x08) != 0;

        var function = new FunctionInfo
        {
            Name = "", // Name will be set externally or derived from hash
            IsDefaultMethod = isDefault,
            IsProperty = isProperty,
            IsOptionalReturn = isOptionalReturn
        };

        // Read return type information
        if (hasUnionReturn)
        {
            // Read union return types
            byte unionCount = reader.ReadByte();
            var unionTypes = new List<TypeWithDimensionality>();

            for (int i = 0; i < unionCount; i++)
            {
                var type = (PeopleCodeType)reader.ReadByte();
                var arrayDim = reader.ReadByte();
                var appClassPath = ReadString(reader);
                bool isRef = reader.ReadByte() != 0;

                unionTypes.Add(string.IsNullOrEmpty(appClassPath)
                    ? new TypeWithDimensionality(type, arrayDim, null, isRef)
                    : new TypeWithDimensionality(type, arrayDim, appClassPath, isReference: false));
            }

            function.ReturnUnionTypes = unionTypes;
        }
        else
        {
            // Read single return type
            var returnType = (PeopleCodeType)reader.ReadByte();
            var returnArrayDim = reader.ReadByte();
            bool retIsRef = reader.ReadByte() != 0;
            function.ReturnType = new TypeWithDimensionality(returnType, returnArrayDim, null, retIsRef);
        }

        // Read number of parameter overload variants
        byte overloadCount = reader.ReadByte();
        function.ParameterOverloads = new List<List<Parameter>>();

        // Read each parameter list variant
        for (int i = 0; i < overloadCount; i++)
        {
            var paramList = ReadParameters(reader, nameTable);
            function.ParameterOverloads.Add(paramList);
        }

        return function;
    }

    private static List<Parameter> ReadParameters(BinaryReader reader, INameTable? nameTable)
    {
        var paramCount = reader.ReadByte();
        var parameters = new List<Parameter>(paramCount);

        for (int i = 0; i < paramCount; i++)
        {
            var parameter = ReadParameter(reader, nameTable);
            parameters.Add(parameter);
        }

        return parameters;
    }

    private static Parameter ReadParameter(BinaryReader reader, INameTable? nameTable)
    {
        var tag = (ParameterTag)reader.ReadByte();

        return tag switch
        {
            ParameterTag.Single => FunctionReaderStrategy.ReadSingleParameter(reader, nameTable),
            ParameterTag.Union => FunctionReaderStrategy.ReadUnionParameter(reader, nameTable),
            ParameterTag.Group => FunctionReaderStrategy.ReadParameterGroup(reader, nameTable),
            ParameterTag.Variable => FunctionReaderStrategy.ReadVariableParameter(reader, nameTable),
            ParameterTag.Reference => FunctionReaderStrategy.ReadReferenceParameter(reader,nameTable),
            _ => throw new InvalidDataException($"Unknown parameter tag: {tag}")
        };
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadByte();
        if (length == 0)
            return "";

        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Create hash function for object names (case-insensitive)
    /// </summary>
    public static Func<BuiltinObjectInfo, uint> CreateHashFunction()
    {
        return obj => HashUtilities.FNV1a32Hash(obj.Name);
    }
}

/// <summary>
/// Extension methods for working with BuiltinObjectReaderStrategy
/// </summary>
public static class BuiltinObjectReaderStrategyExtensions
{
    /// <summary>
    /// Create a reader strategy with default hash function for object names
    /// </summary>
    public static BuiltinObjectReaderStrategy CreateDefault(int tableSize = 2048)
    {
        return new BuiltinObjectReaderStrategy(BuiltinObjectReaderStrategy.CreateHashFunction(), null, tableSize);
    }

    /// <summary>
    /// Load a hash file of BuiltinObjectInfo
    /// </summary>
    public static HashTableReader<BuiltinObjectInfo> LoadBuiltinObjectHashFile(string filePath, int tableSize = 2048)
    {
        // First load to get the name table
        using var file = new BinaryReader(File.OpenRead(filePath));
        var header = file.ReadUInt32();

        INameTable? nameTable = null;
        if (file.BaseStream.Position + 4 <= file.BaseStream.Length)
        {
            var currentPos = file.BaseStream.Position;
            var possibleNamesCount = file.ReadInt32();

            if (possibleNamesCount >= 0 && possibleNamesCount <= 10000)
            {
                file.BaseStream.Position = currentPos;
                nameTable = NameTableReader.ReadAsReadOnlyTable(file, buildReverseIndex: false);
            }
            else
            {
                file.BaseStream.Position = currentPos;
            }
        }

        // Now create strategy with the name table
        var strategy = new BuiltinObjectReaderStrategy(BuiltinObjectReaderStrategy.CreateHashFunction(), nameTable, tableSize);

        // Reset and load the full file with the name table-aware strategy
        file.BaseStream.Position = 0;
        return HashTableReader<BuiltinObjectInfo>.LoadFromFile(filePath, strategy);
    }

    /// <summary>
    /// Lookup an object by name
    /// </summary>
    public static BuiltinObjectInfo? LookupObject(this HashTableReader<BuiltinObjectInfo> reader, string objectName)
    {
        var hash = HashUtilities.FNV1a32Hash(objectName);
        var obj = reader.Lookup(hash);
        if (obj != null)
        {
            obj.Name = objectName; // Set name from lookup parameter (name not stored in data for efficiency)
        }
        return obj;
    }
}
