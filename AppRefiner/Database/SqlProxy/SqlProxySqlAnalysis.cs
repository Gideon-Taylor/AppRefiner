using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using System.Text.RegularExpressions;

namespace AppRefiner.Database.SqlProxy
{
    /// <summary>
    /// PeopleSoft-oriented generic dialect (same idea as <c>SQLHelper.PeopleSoftSQLDialect</c>):
    /// allows # and % in identifiers used by some PS SQL.
    /// </summary>
    internal sealed class PeopleSoftSqlDialect : GenericDialect
    {
        public override bool IsIdentifierStart(char character)
        {
            return char.IsLetter(character) ||
                   character is '_' or '#' or '@' or '%';
        }
    }

    /// <summary>
    /// SQL analysis via SqlParserCS — column names and validation for the proxy connection.
    /// </summary>
    public static class SqlProxySqlAnalysis
    {
        public static Dialect CreateDialect(SqlProxyDialect dialect) => dialect switch
        {
            SqlProxyDialect.SqlServer => new MsSqlDialect(),
            SqlProxyDialect.Oracle => new PeopleSoftSqlDialect(),
            _ => new PeopleSoftSqlDialect(),
        };

        /// <summary>
        /// Parse SQL and return output column names from the outermost SELECT projection.
        /// Throws if the statement is not a SELECT, uses wildcards, or cannot be parsed.
        /// </summary>
        public static IReadOnlyList<string> GetOutputColumnNames(string sql, SqlProxyDialect dialect)
        {
            Statement statement = ParseFirstStatement(sql, dialect);
            if (statement is not Statement.Select selectStmt)
            {
                throw new ArgumentException("SQL must be a single SELECT statement", nameof(sql));
            }

            var projection = GetProjection(selectStmt.Query.Body);
            if (projection.Count == 0)
            {
                throw new ArgumentException("SELECT projection is empty", nameof(sql));
            }

            var names = new List<string>(projection.Count);
            for (int i = 0; i < projection.Count; i++)
            {
                names.Add(GetSelectItemName(projection[i], i));
            }

            return names;
        }

        public static int GetOutputColumnCount(string sql, SqlProxyDialect dialect)
            => GetOutputColumnNames(sql, dialect).Count;

        public static Statement ParseFirstStatement(string sql, SqlProxyDialect dialect)
        {
            try
            {
                var statements = new SqlQueryParser().Parse(sql, CreateDialect(dialect));
                var first = statements.FirstOrDefault()
                    ?? throw new ArgumentException("SQL produced no statements", nameof(sql));
                return first;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Failed to parse SQL with dialect {dialect}: {ex.Message}", nameof(sql), ex);
            }
        }

        private static Sequence<SelectItem> GetProjection(SetExpression body)
        {
            if (body is SetExpression.SetOperation setOp)
            {
                // UNION/INTERSECT: column count/names follow the left side
                return GetProjection(setOp.Left);
            }

            var selectExpr = body.AsSelectExpression();
            return selectExpr.Select.Projection;
        }

        private static string GetSelectItemName(SelectItem item, int index)
        {
            switch (item)
            {
                case SelectItem.Wildcard:
                case SelectItem.QualifiedWildcard:
                    throw new ArgumentException(
                        "SELECT * / table.* is not supported by SqlProxyDbConnection; " +
                        "list columns explicitly so names and types can be specified.");

                case SelectItem.ExpressionWithAlias aliased:
                    return aliased.Alias.Value;

                case SelectItem.UnnamedExpression unnamed:
                    return NameFromExpression(unnamed.Expression, index);

                default:
                    throw new ArgumentException(
                        $"Unsupported SELECT item type: {item.GetType().Name}");
            }
        }

        private static string NameFromExpression(Expression expression, int index)
        {
            return expression switch
            {
                Expression.Identifier id => id.Ident.Value,
                Expression.CompoundIdentifier compound when compound.Idents.Count > 0
                    => compound.Idents[^1].Value,
                // Function calls, casts, etc. without AS — no reliable name
                _ => throw new ArgumentException(
                    $"SELECT expression at index {index} has no column name/alias " +
                    $"(got {expression.GetType().Name}). Add an AS alias so the result can be named."),
            };
        }

        /// <summary>
        /// Rewrite parameter placeholders to SAM positional order and build typed binds.
        /// <list type="bullet">
        /// <item><see cref="SqlProxyDialect.SqlServer"/>: <c>?</c> in order of dictionary insertion.</item>
        /// <item><see cref="SqlProxyDialect.Oracle"/>: <c>:name</c> in left-to-right first appearance order.</item>
        /// </list>
        /// Bind <em>types</em> come from each value's CLR type via <see cref="SqlProxyBind.FromObject"/> —
        /// not from SQL. Pass an <see cref="int"/> for <c>SamBindInt</c>, a <see cref="string"/> for <c>SamBindStr</c>.
        /// </summary>
        public static (string Sql, List<SqlProxyBind> Binds) RewriteParameters(
            string sql,
            Dictionary<string, object>? parameters,
            SqlProxyDialect dialect)
        {
            var binds = new List<SqlProxyBind>();
            if (parameters == null || parameters.Count == 0)
            {
                return (sql, binds);
            }

            if (dialect == SqlProxyDialect.SqlServer)
            {
                var values = parameters.Values.ToList();
                int index = 0;
                string rewritten = Regex.Replace(sql, @"\?", _ =>
                {
                    if (index >= values.Count)
                    {
                        throw new ArgumentException("More ? placeholders than parameters");
                    }

                    binds.Add(SqlProxyBind.FromObject(values[index]));
                    index++;
                    return $":{index}";
                });

                if (index != values.Count)
                {
                    Debug.Log($"SqlProxySqlAnalysis: {values.Count - index} unused parameter(s) for ? SQL");
                }

                return (rewritten, binds);
            }

            // Oracle / named :param
            var pattern = new Regex(@":([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
            string rewrittenNamed = pattern.Replace(sql, m =>
            {
                string name = m.Groups[1].Value;
                if (int.TryParse(name, out _))
                {
                    // Already positional :1 — leave as-is; caller must have pre-built binds
                    return m.Value;
                }

                string keyWithColon = ":" + name;
                if (!parameters.TryGetValue(keyWithColon, out var value) &&
                    !parameters.TryGetValue(name, out value))
                {
                    throw new ArgumentException($"Missing parameter for {keyWithColon}");
                }

                binds.Add(SqlProxyBind.FromObject(value));
                return $":{binds.Count}";
            });

            return (rewrittenNamed, binds);
        }
    }
}
