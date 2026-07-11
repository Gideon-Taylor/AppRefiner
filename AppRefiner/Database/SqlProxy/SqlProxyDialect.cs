namespace AppRefiner.Database.SqlProxy
{
    /// <summary>
    /// SQL flavor for parsing and parameter placeholder style.
    /// Must match the SQL text emitted by the corresponding PeopleSoft DataManager.
    /// </summary>
    public enum SqlProxyDialect
    {
        /// <summary>
        /// Oracle-style SQL: named binds <c>:name</c>. Uses a PeopleSoft-friendly generic dialect.
        /// </summary>
        Oracle,

        /// <summary>
        /// SQL Server / ODBC-style SQL: positional <c>?</c> binds. Uses MsSql dialect.
        /// </summary>
        SqlServer,
    }
}
