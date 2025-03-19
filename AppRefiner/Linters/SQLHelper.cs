using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using System.Text.RegularExpressions;

namespace AppRefiner.Linters
{
    class PeopleSoftSQLDialect : GenericDialect
    {
        public override bool IsIdentifierStart(char character)
        {
            return char.IsLetter(character) ||
                   character is Symbols.Underscore
                       or Symbols.Num
                       or Symbols.At
                       or Symbols.Percent;
        }
    }
    /* 
     * Helper methods for SQL-related linting operations
     */
    /// <summary>
    /// Common helper methods for SQL-related linting operations
    /// </summary>
    public static class SQLHelper
    {
        private static readonly Regex PlaceHolderRegex = new("Placeholder { Value = (:[0-9]+) }");

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
            HashSet<string> placeHolders = new();

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
            return GetOutputCount(statement.Query.Body);
        }

        /// <summary>
        /// Recursively counts the number of output columns in a query expression
        /// </summary>
        /// <param name="expression">The query expression to analyze</param>
        /// <returns>The number of output columns</returns>
        private static int GetOutputCount(SetExpression expression)
        {
            if (expression is SetExpression.SetOperation setOp)
            {
                // For set operations like UNION, INTERSECT, etc.
                // We can use either left or right side as they must have the same number of columns
                return GetOutputCount(setOp.Left);
            }
            else
            {
                // Base case: we have a select expression
                return expression.AsSelectExpression().Select.Projection.Count;
            }
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
                return HasWildcard(statement.Query.Body);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Recursively checks if a query expression uses wildcards
        /// </summary>
        /// <param name="expression">The query expression to analyze</param>
        /// <returns>True if wildcards are used, false otherwise</returns>
        private static bool HasWildcard(SetExpression expression)
        {
            if (expression is SetExpression.SetOperation setOp)
            {
                // Recursively check both sides of the set operation
                return HasWildcard(setOp.Left) || HasWildcard(setOp.Right);
            }
            else
            {
                // Base case: check if any projection item is a wildcard
                var columns = expression.AsSelectExpression().Select.Projection;
                return columns.Any(x => x is SelectItem.Wildcard || x is SelectItem.QualifiedWildcard);
            }
        }

        /// <summary>
        /// Extracts SQL text from a string literal (removes quotes)
        /// </summary>
        /// <param name="literalText">The string literal text</param>
        /// <returns>The SQL text without quotes</returns>
        public static string ExtractSQLFromLiteral(string literalText)
        {
            return string.IsNullOrEmpty(literalText) || literalText.Length < 2 ? literalText : literalText.Substring(1, literalText.Length - 2);
        }
    }
}
