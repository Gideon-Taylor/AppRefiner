namespace AppRefiner.Linters.Models
{
    /// <summary>
    /// Tracks information about SQL statements created with CreateSQL
    /// </summary>
    public class SQLStatementInfo
    {
        public bool UsesSQLDefn { get; set; }
        public string? SqlText { get; set; }
        public int BindCount { get; set; }
        public int OutputColumnCount { get; set; }
        public int CreateLine { get; set; }
        public (int Start, int Stop) CreateSpan { get; set; }
        public string VariableName { get; set; }

        public bool InVarsBound { get; set; }
        public bool NeedsOpenOrExec { get; set; }
        public bool ParseFailed { get; set; }

        public SQLStatementInfo(string? sqlText, int bindCount, int outputColumnCount, int line, (int Start, int Stop) span, string varName)
        {
            SqlText = sqlText;
            BindCount = bindCount;
            OutputColumnCount = outputColumnCount;
            CreateLine = line;
            CreateSpan = span;
            VariableName = varName;
            ParseFailed = false;
        }
    }
}
