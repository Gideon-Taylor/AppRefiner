using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;

namespace ParserPorting.Shared.SQL.Models
{
    /// <summary>
    /// Tracks information about SQL statements created with CreateSQL
    /// Updated for self-hosted parser with SourceSpan support
    /// </summary>
    public class SQLStatementInfo
    {
        public bool UsesSQLDefn { get; set; }
        public string? SqlText { get; set; }
        public int BindCount { get; set; }
        public int OutputColumnCount { get; set; }
        public int CreateLine { get; set; }
        
        /// <summary>
        /// Source span for the SQL variable creation (using SourceSpan instead of byte indices)
        /// </summary>
        public PeopleCodeParser.SelfHosted.SourceSpan CreateSpan { get; set; }
        
        public string VariableName { get; set; }

        public bool InVarsBound { get; set; }
        public bool NeedsOpenOrExec { get; set; }
        public bool ParseFailed { get; set; }
        public bool HasValidSqlText => !string.IsNullOrWhiteSpace(SqlText);

        public SQLStatementInfo(string? sqlText, int bindCount, int outputColumnCount, int line, PeopleCodeParser.SelfHosted.SourceSpan span, string varName)
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