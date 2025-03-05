using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Common helper methods for SQL-related linting operations
    /// </summary>
    public static class SQLHelper
    {
        private static readonly Regex PlaceHolderRegex = new Regex("Placeholder { Value = (:[0-9]+) }");
        
        /// <summary>
        /// Parses SQL text and returns the first statement
        /// </summary>
        /// <param name="sqlText">The SQL text to parse</param>
        /// <returns>The first SQL statement or null if parsing failed</returns>
        public static Statement? ParseSQL(string sqlText)
        {
            try
            {
                var ast = new SqlQueryParser().Parse(sqlText, new PeopleSoftSQLDialect());
                return ast.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Counts the number of bind variables in an SQL statement
        /// </summary>
        /// <param name="statement">The SQL statement to analyze</param>
        /// <returns>The number of unique bind variables</returns>
        public static int GetBindCount(Statement statement)
        {
            var matches = PlaceHolderRegex.Matches(statement.ToString());
            HashSet<string> placeHolders = new HashSet<string>();
            
            foreach (Match match in matches)
            {
                placeHolders.Add(match.Groups[1].Value);
            }
            
            return placeHolders.Count;
        }

        /// <summary>
        /// Counts the number of output columns in a SELECT statement
        /// </summary>
        /// <param name="statement">The SELECT statement to analyze</param>
        /// <returns>The number of output columns</returns>
        public static int GetOutputCount(Statement.Select statement)
        {
            return statement.Query.Body.AsSelectExpression().Select.Projection.Count;
        }

        /// <summary>
        /// Checks if a SELECT statement uses wildcards
        /// </summary>
        /// <param name="statement">The SELECT statement to analyze</param>
        /// <returns>True if wildcards are used, false otherwise</returns>
        public static bool HasWildcard(Statement.Select statement)
        {
            try
            {
                if (statement.Query.Body is SetExpression.SetOperation set)
                {
                    return set.Left.AsSelectExpression().Select.Projection.Any(x => x is SelectItem.Wildcard || x is SelectItem.QualifiedWildcard) ||
                        set.Right.AsSelectExpression().Select.Projection.Any(x => x is SelectItem.Wildcard || x is SelectItem.QualifiedWildcard);
                }

                var columns = statement.Query.Body.AsSelectExpression().Select.Projection;
                return columns.Any(x => x is SelectItem.Wildcard || x is SelectItem.QualifiedWildcard);

            }catch(Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Extracts SQL text from a string literal (removes quotes)
        /// </summary>
        /// <param name="literalText">The string literal text</param>
        /// <returns>The SQL text without quotes</returns>
        public static string ExtractSQLFromLiteral(string literalText)
        {
            if (string.IsNullOrEmpty(literalText) || literalText.Length < 2)
                return literalText;
                
            return literalText.Substring(1, literalText.Length - 2);
        }
    }
}
