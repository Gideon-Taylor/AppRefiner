namespace AppRefiner.Database.Models
{
    /// <summary>
    /// Configuration options for searching open targets
    /// </summary>
    public class OpenTargetSearchOptions
    {
        /// <summary>
        /// Gets or sets the definition types to include in the search
        /// </summary>
        public HashSet<OpenTargetType> EnabledTypes { get; set; } = new();

        /// <summary>
        /// Gets or sets the maximum number of results to return per type
        /// </summary>
        public int MaxRowsPerType { get; set; } = 10;

        /// <summary>
        /// Gets or sets whether to sort results by last update date (true) or by name (false)
        /// </summary>
        public bool SortByDate { get; set; } = false;

        /// <summary>
        /// Gets or sets the search term for ID field
        /// </summary>
        public string IDSearchTerm { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the search term for Description field
        /// </summary>
        public string DescriptionSearchTerm { get; set; } = string.Empty;

        /// <summary>
        /// Creates a new OpenTargetSearchOptions with default settings
        /// </summary>
        public OpenTargetSearchOptions()
        {
            // Initialize with all types enabled by default
            foreach (OpenTargetType type in Enum.GetValues<OpenTargetType>())
            {
                EnabledTypes.Add(type);
            }
        }

        /// <summary>
        /// Creates a new OpenTargetSearchOptions with specified enabled types
        /// </summary>
        /// <param name="enabledTypes">The types to enable for searching</param>
        public OpenTargetSearchOptions(IEnumerable<OpenTargetType> enabledTypes)
        {
            EnabledTypes = new HashSet<OpenTargetType>(enabledTypes);
        }

        /// <summary>
        /// Creates a new OpenTargetSearchOptions with all settings specified
        /// </summary>
        /// <param name="enabledTypes">The types to enable for searching</param>
        /// <param name="maxRowsPerType">Maximum results per type</param>
        /// <param name="sortByDate">Whether to sort by date</param>
        public OpenTargetSearchOptions(IEnumerable<OpenTargetType> enabledTypes, int maxRowsPerType, bool sortByDate)
        {
            EnabledTypes = new HashSet<OpenTargetType>(enabledTypes);
            MaxRowsPerType = maxRowsPerType;
            SortByDate = sortByDate;
        }

        /// <summary>
        /// Creates a new OpenTargetSearchOptions with search terms and settings
        /// </summary>
        /// <param name="enabledTypes">The types to enable for searching</param>
        /// <param name="maxRowsPerType">Maximum results per type</param>
        /// <param name="sortByDate">Whether to sort by date</param>
        /// <param name="idSearchTerm">Search term for ID field</param>
        /// <param name="descriptionSearchTerm">Search term for Description field</param>
        public OpenTargetSearchOptions(IEnumerable<OpenTargetType> enabledTypes, int maxRowsPerType, bool sortByDate, 
            string idSearchTerm, string descriptionSearchTerm)
        {
            EnabledTypes = new HashSet<OpenTargetType>(enabledTypes);
            MaxRowsPerType = maxRowsPerType;
            SortByDate = sortByDate;
            IDSearchTerm = idSearchTerm ?? string.Empty;
            DescriptionSearchTerm = descriptionSearchTerm ?? string.Empty;
        }
    }
}