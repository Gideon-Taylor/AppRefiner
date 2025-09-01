namespace AppRefiner.Database.Models
{
    /// <summary>
    /// Represents metadata for a field within a PeopleSoft record definition.
    /// </summary>
    public class RecordFieldInfo
    {
        public string FieldName { get; }
        public int FieldNumber { get; }
        public int FieldType { get; }
        public int Length { get; }
        public int DecimalPosition { get; }
        public int UseEdit { get; } // Bitmask for key, required, etc.

        public bool IsKey => (UseEdit & 1) != 0; // Bit 0: Set if key field
        public bool IsRequired => (UseEdit & 256) != 0; // Bit 8: Set if required

        // Basic mapping of PSDBFIELD.FIELDTYPE to readable names
        public string FieldTypeName => FieldType switch
        {
            0 => "Char",
            1 => "Long Char",
            2 => "Number",
            3 => "Signed Nbr",
            4 => "Date",
            5 => "Time",
            6 => "DateTime",
            8 => "Image",
            9 => "Image Ref",
            _ => $"Unknown ({FieldType})"
        };

        // Formatted length/precision string
        public string LengthPrecision => FieldType switch
        {
            0 or 1 => $"({Length})", // Char types
            2 or 3 => $"({Length},{DecimalPosition})", // Number types
            _ => "" // Others don't typically show length/precision
        };

        public RecordFieldInfo(string fieldName, int fieldNumber, int fieldType, int length, int decimalPosition, int useEdit)
        {
            FieldName = fieldName;
            FieldNumber = fieldNumber;
            FieldType = fieldType;
            Length = length;
            DecimalPosition = decimalPosition;
            UseEdit = useEdit;
        }

        public override string ToString()
        {
            string keyIndicator = IsKey ? "*" : "";
            string requiredIndicator = IsRequired ? "!" : "";
            return $"{FieldName}{keyIndicator}{requiredIndicator} : {FieldTypeName}{LengthPrecision}";
        }
    }
}