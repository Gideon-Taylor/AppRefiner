using System.Data;

namespace AppRefiner.Database.SqlProxy
{
    /// <summary>
    /// <see cref="IDbConnection"/> over the App Designer SAM SQL proxy.
    /// Result columns are discovered via <c>SamNumResultCols</c> / <c>SamDescribeCol</c>
    /// (same family as Query / psopnqry), not caller inference or SQL parsing for types/names.
    /// </summary>
    /// <remarks>
    /// <para><b>Bind types:</b> from each dictionary value's CLR type via
    /// <see cref="SqlProxyBind.FromObject"/> (<c>int</c> → <c>SamBindInt</c>, <c>string</c> → <c>SamBindStr</c>).
    /// Or pass explicit <see cref="SqlProxyBind"/> list.</para>
    /// <para><b>Dialect:</b> only affects parameter placeholder rewrite (<c>:name</c> vs <c>?</c>)
    /// and optional SqlParserCS helpers — not result typing.</para>
    /// </remarks>
    public sealed class SqlProxyDbConnection : IDbConnection
    {
        private readonly AppDesignerProcess process;
        private SqlProxy? proxy;
        private ConnectionState state = ConnectionState.Closed;

        /// <summary>
        /// SQL flavor for parameter placeholder rewriting only (Oracle <c>:name</c> vs SQL Server <c>?</c>).
        /// </summary>
        public SqlProxyDialect Dialect { get; set; } = SqlProxyDialect.Oracle;

        public int DefaultMaxRows { get; set; } = 5000;
        public uint DefaultStringColumnChars { get; set; } = 512;

        public SqlProxyDbConnection(AppDesignerProcess process, SqlProxyDialect dialect = SqlProxyDialect.Oracle)
        {
            this.process = process ?? throw new ArgumentNullException(nameof(process));
            Dialect = dialect;
        }

        public string ConnectionString { get; set; } = "SqlProxy";
        public ConnectionState State => state;
        public string ServerName => $"App Designer SAM proxy ({Dialect})";
        public SqlProxy? Proxy => proxy;

        public void Open()
        {
            if (state == ConnectionState.Open && proxy != null)
            {
                return;
            }

            proxy?.Dispose();
            proxy = SqlProxy.TryAttach(process);
            if (proxy == null)
            {
                state = ConnectionState.Closed;
                throw new InvalidOperationException(
                    "Failed to attach SQL proxy to App Designer (hooks installed and process signed on?)");
            }

            var pong = proxy.Ping();
            if (pong.Status != SqlProxyStatus.Ok)
            {
                proxy.Dispose();
                proxy = null;
                state = ConnectionState.Closed;
                throw new InvalidOperationException($"SQL proxy Ping failed: {pong.Status}");
            }

            state = ConnectionState.Open;
            Debug.Log($"SqlProxyDbConnection: Open succeeded (dialect={Dialect})");
        }

        public void Close()
        {
            proxy?.Dispose();
            proxy = null;
            state = ConnectionState.Closed;
        }

        public IDbCommand CreateCommand()
        {
            throw new NotSupportedException(
                "SqlProxyDbConnection does not expose ADO.NET IDbCommand; use ExecuteQuery.");
        }

        /// <summary>
        /// Execute SELECT. Output names/types come from SAM describe inside the hook.
        /// </summary>
        public DataTable ExecuteQuery(string sql, Dictionary<string, object>? parameters = null)
        {
            EnsureOpen();
            ValidateSelect(sql);

            var (positionalSql, binds) = SqlProxySqlAnalysis.RewriteParameters(sql, parameters, Dialect);
            var result = proxy!.ExecSql(
                positionalSql,
                binds,
                columnOverrides: null,
                DefaultMaxRows,
                DefaultStringColumnChars);

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"SqlProxy query failed: {result.Status} samRc={result.SamReturnCode} {result.ErrorMessage}");
            }

            return ToDataTable(result);
        }

        /// <summary>Explicit bind list (SAM order); SQL placeholders already positional or rewritten by caller.</summary>
        public DataTable ExecuteQuery(string sql, IReadOnlyList<SqlProxyBind> binds, int? maxRows = null)
        {
            EnsureOpen();
            ValidateSelect(sql);

            var result = proxy!.ExecSql(
                sql,
                binds,
                columnOverrides: null,
                maxRows ?? DefaultMaxRows,
                DefaultStringColumnChars);

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"SqlProxy query failed: {result.Status} samRc={result.SamReturnCode} {result.ErrorMessage}");
            }

            return ToDataTable(result);
        }

        public int ExecuteNonQuery(string sql, Dictionary<string, object>? parameters = null)
        {
            throw new NotSupportedException(
                "SqlProxyDbConnection does not support ExecuteNonQuery (SELECT-only SAM proxy).");
        }

        public void Dispose() => Close();

        private void EnsureOpen()
        {
            if (state != ConnectionState.Open || proxy == null)
            {
                throw new InvalidOperationException("SqlProxyDbConnection is not open");
            }
        }

        private static void ValidateSelect(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new ArgumentException("SQL is empty", nameof(sql));
            }

            if (!SqlProxy.IsSelectOnly(sql))
            {
                throw new InvalidOperationException("SqlProxyDbConnection only allows SELECT statements");
            }
        }

        private static DataTable ToDataTable(SqlProxyResult result)
        {
            var table = new DataTable();
            int width = result.ColumnCount;
            var columns = result.Columns;

            for (int c = 0; c < width; c++)
            {
                string name = (columns != null && c < columns.Count && !string.IsNullOrEmpty(columns[c].Name))
                    ? columns[c].Name
                    : $"Column{c + 1}";

                string unique = name;
                int n = 2;
                while (table.Columns.Contains(unique))
                {
                    unique = name + n;
                    n++;
                }

                var vtype = columns != null && c < columns.Count
                    ? columns[c].Type
                    : SqlProxyValueType.String;

                Type clrType = vtype switch
                {
                    SqlProxyValueType.Int32 => typeof(int),
                    SqlProxyValueType.LongRaw => typeof(byte[]),
                    _ => typeof(string),
                };
                table.Columns.Add(unique, clrType);
            }

            if (result.Cells == null || result.RowCount == 0)
            {
                return table;
            }

            for (int row = 0; row < result.RowCount; row++)
            {
                var dataRow = table.NewRow();
                for (int c = 0; c < width; c++)
                {
                    int idx = row * result.ColumnCount + c;
                    if (idx >= result.Cells.Count)
                    {
                        dataRow[c] = DBNull.Value;
                        continue;
                    }

                    object? val = result.Cells[idx].ToObject();
                    if (val == null || val is DBNull)
                    {
                        dataRow[c] = DBNull.Value;
                    }
                    else if (table.Columns[c].DataType == typeof(string) && val is int iv)
                    {
                        dataRow[c] = iv.ToString();
                    }
                    else if (table.Columns[c].DataType == typeof(int) && val is string s && int.TryParse(s, out int parsed))
                    {
                        dataRow[c] = parsed;
                    }
                    else if (table.Columns[c].DataType.IsInstanceOfType(val))
                    {
                        dataRow[c] = val;
                    }
                    else
                    {
                        dataRow[c] = Convert.ChangeType(val, table.Columns[c].DataType);
                    }
                }
                table.Rows.Add(dataRow);
            }

            return table;
        }
    }
}
