using AppRefiner.Database.Models;
using PeopleCodeParser.SelfHosted.ComponentInterfaces;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Types;
using System.Collections.Concurrent;
using System.Data;

namespace AppRefiner.Database
{
    /// <summary>
    /// Per-connection cache of component interface definitions, keyed by uppercase CI name and
    /// invalidated by PSBCDEFN.VERSION. Data managers supply only the query execution; the
    /// lookup, row mapping and caching rules live here so every manager behaves the same.
    /// Failures from the supplied queries propagate: a failed lookup must never read as
    /// "doesn't exist".
    /// </summary>
    internal sealed class ComponentInterfaceCache
    {
        private readonly ConcurrentDictionary<string, CompIntfcDefinition> definitions = new();

        public IReadOnlyCollection<string> Names => definitions.Keys.ToList();

        public void Clear() => definitions.Clear();

        public void Invalidate(string? ciName)
        {
            if (ciName == null)
            {
                definitions.Clear();
            }
            else
            {
                definitions.TryRemove(ciName.ToUpperInvariant(), out _);
            }
        }

        /// <summary>
        /// Returns the CI's definition, or null when PSBCDEFN has no row for it.
        /// </summary>
        /// <param name="ciName">CI name (any case)</param>
        /// <param name="readDefn">Runs the PSBCDEFN query (VERSION, BCSTDMETHODS) for the uppercase name</param>
        /// <param name="readItems">Runs the PSBCITEM/PSDBFIELD items query for the uppercase name</param>
        /// <param name="loadMethods">Loads the PSPCMPROG content of the item; false when there is no program. Must throw on failure.</param>
        public CompIntfcDefinition? Get(
            string ciName,
            Func<string, DataTable> readDefn,
            Func<string, DataTable> readItems,
            Func<PeopleCodeItem, bool> loadMethods)
        {
            ciName = ciName.ToUpperInvariant();

            DataTable defn = readDefn(ciName);
            if (defn.Rows.Count == 0)
            {
                definitions.TryRemove(ciName, out _);
                return null;
            }

            int version = Number(defn.Rows[0]["VERSION"]) ?? 0;
            int standardMethodBits = Number(defn.Rows[0]["BCSTDMETHODS"]) ?? 0;

            if (definitions.TryGetValue(ciName, out CompIntfcDefinition? cached) && cached.Version == version)
            {
                return cached;
            }

            IReadOnlyList<CompIntfcItemRow> rows = ReadItemRows(readItems(ciName));

            PeopleCodeItem methodsItem = MethodsProgramItem(ciName);
            string? methodsSource = loadMethods(methodsItem) ? methodsItem.GetProgramTextAsString() : null;

            CompIntfcDefinition definition = CompIntfcDefinitionBuilder.Build(
                ciName, version, standardMethodBits, rows, methodsSource);
            definitions[ciName] = definition;
            return definition;
        }

        /// <summary>
        /// Maps the items query result to rows: BCITEMPARENT, BCITEMNAME, RECNAME and FIELDNAME
        /// trimmed, COMMENTSHORT trailing-trimmed, and a null FIELDTYPE (collections) as Unknown.
        /// </summary>
        public static IReadOnlyList<CompIntfcItemRow> ReadItemRows(DataTable result)
        {
            static string Text(DataRow row, string column) =>
                row[column] is DBNull ? string.Empty : row[column].ToString() ?? string.Empty;

            List<CompIntfcItemRow> rows = new();
            foreach (DataRow row in result.Rows)
            {
                PeopleCodeType fieldType = Number(row["FIELDTYPE"]) is int type
                    ? FieldTypes.ToPeopleCodeType(type)
                    : PeopleCodeType.Unknown;

                rows.Add(new CompIntfcItemRow(
                    Number(row["BCTYPE"]) ?? 0,
                    Text(row, "BCITEMPARENT").Trim(),
                    Text(row, "BCITEMNAME").Trim(),
                    Number(row["SEQUENCE_NBR_6"]) ?? 0,
                    Number(row["BCACCESS"]) ?? 0,
                    Text(row, "RECNAME").Trim(),
                    Text(row, "FIELDNAME").Trim(),
                    row["COMMENTSHORT"] is DBNull ? null : row["COMMENTSHORT"].ToString()?.TrimEnd(),
                    fieldType));
            }
            return rows;
        }

        /// <summary>
        /// A numeric column's value, or null when it is null or blank. The SQL proxy returns a null
        /// column (such as FIELDTYPE for a collection row, whose blank FIELDNAME matches no
        /// PSDBFIELD row) as an empty string rather than DBNull.
        /// </summary>
        private static int? Number(object value) => value switch
        {
            DBNull or null => null,
            int i => i,
            string s => int.TryParse(s.Trim(), out int parsed) ? parsed : null,
            _ => Convert.ToInt32(value)
        };

        /// <summary>
        /// The empty PeopleCodeItem naming the CI's Methods program in PSPCMPROG.
        /// </summary>
        public static PeopleCodeItem MethodsProgramItem(string ciName) =>
            new(
                new[] { (int)PSCLASSID.COMPONENTINTERFACE, (int)PSCLASSID.METHOD, 0, 0, 0, 0, 0 },
                new[] { ciName, "Methods", " ", " ", " ", " ", " " },
                Array.Empty<byte>(),
                new List<NameReference>());
    }
}
