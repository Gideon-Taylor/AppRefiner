namespace AppRefiner.Database.SqlProxy
{
    public sealed class SqlProxyPong
    {
        public SqlProxyStatus Status { get; init; }
        public string? DbType { get; init; }
    }

    public sealed class SqlProxyBind
    {
        public SqlProxyValueType Type { get; init; }
        public string? StringValue { get; init; }
        public int IntValue { get; init; }

        public static SqlProxyBind String(string? value) => new()
        {
            Type = SqlProxyValueType.String,
            StringValue = value ?? string.Empty,
        };

        public static SqlProxyBind Int32(int value) => new()
        {
            Type = SqlProxyValueType.Int32,
            IntValue = value,
        };

        /// <summary>
        /// Map a boxed parameter value to a SAM bind from its CLR type
        /// (not from SQL): int/short/byte/bool → Int32, string → String.
        /// </summary>
        public static SqlProxyBind FromObject(object? value)
        {
            if (value == null || value is DBNull)
            {
                return String(string.Empty);
            }

            return value switch
            {
                int i => Int32(i),
                short s => Int32(s),
                long l when l >= int.MinValue && l <= int.MaxValue => Int32((int)l),
                byte b => Int32(b),
                bool bo => Int32(bo ? 1 : 0),
                string str => String(str),
                _ => String(Convert.ToString(value) ?? string.Empty),
            };
        }
    }

    /// <summary>
    /// Optional override for output buffers. Prefer leaving column list empty so the hook
    /// uses <c>SamNumResultCols</c> / <c>SamDescribeCol</c> (Query-style ad-hoc path).
    /// </summary>
    public sealed class SqlProxyColumnSpec
    {
        public SqlProxyValueType Type { get; init; } = SqlProxyValueType.String;
        public uint MaxSize { get; init; } = 256;

        public static SqlProxyColumnSpec String(uint maxChars = 256) => new()
        {
            Type = SqlProxyValueType.String,
            MaxSize = maxChars,
        };

        public static SqlProxyColumnSpec Int32() => new()
        {
            Type = SqlProxyValueType.Int32,
            MaxSize = 0,
        };

        public static SqlProxyColumnSpec LongRaw() => new()
        {
            Type = SqlProxyValueType.LongRaw,
            MaxSize = 0,
        };

        public static SqlProxyColumnSpec LongText() => new()
        {
            Type = SqlProxyValueType.LongText,
            MaxSize = 0,
        };
    }

    /// <summary>Result-set column metadata returned by the hook (usually from SamDescribeCol).</summary>
    public sealed class SqlProxyColumnInfo
    {
        public string Name { get; init; } = string.Empty;
        public SqlProxyValueType Type { get; init; }
        public int SamTypeId { get; init; }
    }

    public sealed class SqlProxyCell
    {
        public SqlProxyValueType Type { get; init; }
        public string? StringValue { get; init; }
        public int IntValue { get; init; }
        public byte[]? Bytes { get; init; }

        public object? ToObject()
        {
            return Type switch
            {
                SqlProxyValueType.Null => DBNull.Value,
                SqlProxyValueType.String => StringValue ?? string.Empty,
                SqlProxyValueType.Int32 => IntValue,
                SqlProxyValueType.LongRaw => Bytes ?? Array.Empty<byte>(),
                SqlProxyValueType.LongText => !string.IsNullOrEmpty(StringValue)
                    ? StringValue
                    : Bytes ?? Array.Empty<byte>(),
                _ => DBNull.Value,
            };
        }
    }

    public sealed class SqlProxyResult
    {
        public SqlProxyStatus Status { get; init; }
        public int SamReturnCode { get; init; }
        public string? ErrorMessage { get; init; }
        public IReadOnlyList<SqlProxyColumnInfo>? Columns { get; init; }
        public int ColumnCount { get; init; }
        public int RowCount { get; init; }
        public IReadOnlyList<SqlProxyCell>? Cells { get; init; }

        public IReadOnlyList<string>? ColumnValues
        {
            get
            {
                if (Cells == null || ColumnCount <= 0 || RowCount <= 0)
                {
                    return null;
                }

                var list = new List<string>(ColumnCount);
                for (int c = 0; c < ColumnCount; c++)
                {
                    var cell = Cells[c];
                    list.Add(cell.Type switch
                    {
                        SqlProxyValueType.Int32 => cell.IntValue.ToString(),
                        SqlProxyValueType.String or SqlProxyValueType.LongText => cell.StringValue ?? string.Empty,
                        _ => string.Empty,
                    });
                }
                return list;
            }
        }

        public bool IsSuccess => Status == SqlProxyStatus.Ok || Status == SqlProxyStatus.Truncated;
    }
}
