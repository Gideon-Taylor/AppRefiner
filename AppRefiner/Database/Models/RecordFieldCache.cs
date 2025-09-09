namespace AppRefiner.Database.Models
{
    /// <summary>
    /// Represents cached field information for a PeopleSoft record.
    /// </summary>
    public class RecordFieldCache
    {
        /// <summary>
        /// The name of the record
        /// </summary>
        public string RecordName { get; }

        /// <summary>
        /// The version number from PSRECDEFN.VERSION
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// The timestamp when this cache entry was created
        /// </summary>
        public DateTime CacheTimestamp { get; }

        /// <summary>
        /// The cached list of field information
        /// </summary>
        public List<RecordFieldInfo> Fields { get; }

        public RecordFieldCache(string recordName, int version, List<RecordFieldInfo> fields)
        {
            RecordName = recordName;
            Version = version;
            Fields = fields;
            CacheTimestamp = DateTime.Now;
        }
    }
}