using PeopleCodeTypeInfo.Types;

namespace AppRefiner.Database
{
    /// <summary>
    /// Maps PSDBFIELD.FIELDTYPE values to PeopleCode types.
    /// </summary>
    public static class FieldTypes
    {
        /// <summary>
        /// 0/1 → String, 2/3 → Number, 4 → Date, 5 → Time, 6 → DateTime, 8/9 → Any, else Unknown.
        /// </summary>
        public static PeopleCodeType ToPeopleCodeType(int fieldType) => fieldType switch
        {
            0 => PeopleCodeType.String,
            1 => PeopleCodeType.String,
            2 => PeopleCodeType.Number,
            3 => PeopleCodeType.Number,
            4 => PeopleCodeType.Date,
            5 => PeopleCodeType.Time,
            6 => PeopleCodeType.DateTime,
            8 => PeopleCodeType.Any,
            9 => PeopleCodeType.Any,
            _ => PeopleCodeType.Unknown,
        };
    }
}
