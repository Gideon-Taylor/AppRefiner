using PeopleCodeTypeInfo.HashTable;
using PeopleCodeTypeInfo.HashTable.Strategies;
using PeopleCodeTypeInfo.Types;
using System.Text;

namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Strategy for reading FunctionInfo from hash table storage.
/// Uses the composable parameter system with hierarchical parameter encoding.
/// Supports NameTable for efficient parameter name resolution.
/// </summary>
public class FunctionReaderStrategy : VariableSizeReaderStrategy<FunctionInfo>
{
    private const int DATA_OFFSET_BASE = 4 + (2048 * 8); // header + hash table size for default table

    // No explicit format version; reader/writer evolve in lockstep
    private readonly INameTable? _nameTable;

    public FunctionReaderStrategy(Func<FunctionInfo, uint> stringHashFunc, INameTable? nameTable = null, int tableSize = 2048) : base(
        hashFunc: stringHashFunc,
        readDataFunc: (reader, offset, dataSection) => ReadFunction(reader, offset, dataSection, nameTable))
    {
        _nameTable = nameTable;
    }

    private static FunctionInfo ReadFunction(BinaryReader reader, uint offset, byte[] dataSection, INameTable? nameTable)
    {
        // Convert file offset to data array offset
        uint dataOffset = offset - DATA_OFFSET_BASE;
        var memoryStream = new MemoryStream(dataSection, (int)dataOffset, dataSection.Length - (int)dataOffset);
        var dataReader = new BinaryReader(memoryStream);

        return ReadFunctionData(dataReader, nameTable);
    }

    private static FunctionInfo ReadFunctionData(BinaryReader dataReader, INameTable? nameTable)
    {
        // Read flags byte
        byte flags = dataReader.ReadByte();
        bool hasUnionReturn = (flags & 0x01) != 0;
        bool isDefault = (flags & 0x02) != 0;
        bool isProperty = (flags & 0x04) != 0;

        var function = new FunctionInfo
        {
            Name = "", // Name will be set externally by hash table
            IsDefaultMethod = isDefault,
            IsProperty = isProperty
        };

        // Read return type information
        if (hasUnionReturn)
        {
            // Read union return types
            byte unionCount = dataReader.ReadByte();
            var unionTypes = new List<TypeWithDimensionality>();

            for (int i = 0; i < unionCount; i++)
            {
                var type = (PeopleCodeType)dataReader.ReadByte();
                var arrayDim = dataReader.ReadByte();
                var appClassPath = ReadString(dataReader);
                bool isRef = dataReader.ReadByte() != 0;

                unionTypes.Add(string.IsNullOrEmpty(appClassPath)
                    ? new TypeWithDimensionality(type, arrayDim, null, isRef)
                    : new TypeWithDimensionality(type, arrayDim, appClassPath, isReference: false));
            }

            function.ReturnUnionTypes = unionTypes;
            // Set primary return type to first union type for backward compatibility
            function.ReturnType = unionTypes[0];
        }
        else
        {
            // Read single return type
            var returnType = (PeopleCodeType)dataReader.ReadByte();
            var returnArrayDim = dataReader.ReadByte();
            bool retIsRef = dataReader.ReadByte() != 0;
            function.ReturnType = new TypeWithDimensionality(returnType, returnArrayDim, null, retIsRef);
        }

        // Read parameters using name table
        function.Parameters = ReadParameters(dataReader, nameTable);
        
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
            ParameterTag.Single => ReadSingleParameter(reader, nameTable),
            ParameterTag.Union => ReadUnionParameter(reader, nameTable),
            ParameterTag.Group => ReadParameterGroup(reader, nameTable),
            ParameterTag.Variable => ReadVariableParameter(reader, nameTable),
            ParameterTag.Reference => ReadReferenceParameter(reader, nameTable),
            _ => throw new InvalidDataException($"Unknown parameter tag: {tag}")
        };
    }

    public static SingleParameter ReadSingleParameter(BinaryReader reader, INameTable? nameTable)
    {
        var type = (PeopleCodeType)reader.ReadByte();
        var arrayDim = reader.ReadByte();
        var appClassPath = ReadString(reader);
        var (name, nameIndex) = ReadParameterName(reader, nameTable);

        var paramType = string.IsNullOrEmpty(appClassPath)
            ? new TypeWithDimensionality(type, arrayDim)
            : new TypeWithDimensionality(type, arrayDim, appClassPath);

        return new SingleParameter(paramType, name) { NameIndex = nameIndex };
    }

    public static UnionParameter ReadUnionParameter(BinaryReader reader, INameTable? nameTable)
    {
        var typeCount = reader.ReadByte();
        var allowedTypes = new List<TypeWithDimensionality>(typeCount);

        for (int i = 0; i < typeCount; i++)
        {
            var type = (PeopleCodeType)reader.ReadByte();
            var arrayDim = reader.ReadByte();
            var appClassPath = ReadString(reader);
            bool isRef = reader.ReadByte() != 0;

            var typeWithDim = string.IsNullOrEmpty(appClassPath)
                ? new TypeWithDimensionality(type, arrayDim, null, isRef)
                : new TypeWithDimensionality(type, arrayDim, appClassPath, isReference: false);

            allowedTypes.Add(typeWithDim);
        }

        var (name, nameIndex) = ReadParameterName(reader, nameTable);
        return new UnionParameter(allowedTypes, name) { NameIndex = nameIndex };
    }

    public static ParameterGroup ReadParameterGroup(BinaryReader reader, INameTable? nameTable)
    {
        var (name, nameIndex) = ReadParameterName(reader, nameTable);
        var parameters = ReadParameters(reader, nameTable);

        return new ParameterGroup(parameters, name) { NameIndex = nameIndex };
    }

    public static VariableParameter ReadVariableParameter(BinaryReader reader, INameTable? nameTable)
    {
        var minCount = reader.ReadInt32();
        var maxCount = reader.ReadInt32();
        if (maxCount == -1) maxCount = int.MaxValue; // Convert back from unlimited marker

        var (name, nameIndex) = ReadParameterName(reader, nameTable);
        var innerParameter = ReadParameter(reader, nameTable);

        return new VariableParameter(innerParameter, minCount, maxCount, name) { NameIndex = nameIndex };
    }

    public static ReferenceParameter ReadReferenceParameter(BinaryReader reader, INameTable? nameTable)
    {
        var cat = (PeopleCodeType)reader.ReadByte();
        var (name, nameIndex) = ReadParameterName(reader, nameTable);
        var p = new ReferenceParameter(cat);
        p.Name = name;
        p.NameIndex = nameIndex;
        return p;
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
    /// Read parameter name using space-efficient encoding for name indices
    /// </summary>
    private static (string name, int? nameIndex) ReadParameterName(BinaryReader reader, INameTable? nameTable)
    {
        // Check file format version to determine how to read parameter names
        if (nameTable != null)
        {
            // Read first byte to check encoding
            var firstByte = reader.ReadByte();

            if ((firstByte & 1) == 0)
            {
                // Lowest bit is 0 - no name index
                return ("", null);
            }
            else
            {
                // Lowest bit is 1 - read remaining 3 bytes and decode
                var byte2 = reader.ReadByte();
                var byte3 = reader.ReadByte();
                var byte4 = reader.ReadByte();

                // Reconstruct the 32-bit value and shift right to get actual index
                uint encodedValue = (uint)(firstByte | (byte2 << 8) | (byte3 << 16) | (byte4 << 24));
                int nameIndex = (int)(encodedValue >> 1);

                var resolvedName = nameTable.GetNameByIndex(nameIndex);
                return (resolvedName ?? "", nameIndex);
            }
        }
        else
        {
            // Legacy format: read the name string directly
            var name = ReadString(reader);
            return (name, null); // No name index in legacy format
        }
    }

    /// <summary>
    /// Create hash function for function names (case-insensitive)
    /// </summary>
    public static Func<FunctionInfo, uint> CreateHashFunction()
    {
        return func => HashUtilities.FNV1a32Hash(func.Name);
    }
}

/// <summary>
/// Extension methods for working with FunctionReaderStrategy
/// </summary>
public static class FunctionReaderStrategyExtensions
{
    /// <summary>
    /// Create a reader strategy with default hash function for function names
    /// </summary>
    public static FunctionReaderStrategy CreateDefault(INameTable? nameTable = null, int tableSize = 2048)
    {
        return new FunctionReaderStrategy(FunctionReaderStrategy.CreateHashFunction(), nameTable, tableSize);
    }

    /// <summary>
    /// Load a hash file of FunctionInfo
    /// </summary>
    public static HashTableReader<FunctionInfo> LoadFunctionHashFile(string filePath, INameTable? nameTable = null, int tableSize = 2048)
    {
        var strategy = CreateDefault(nameTable, tableSize);
        return HashTableReader<FunctionInfo>.LoadFromFile(filePath, strategy);
    }
}
