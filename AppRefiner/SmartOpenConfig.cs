using System.Text.Json.Serialization;

namespace AppRefiner
{
    /// <summary>
    /// Configuration settings for the Smart Open feature
    /// </summary>
    public class SmartOpenConfig
    {
        /// <summary>
        /// Dictionary of definition types and whether they are enabled for searching
        /// </summary>
        [JsonPropertyName("enabledTypes")]
        public Dictionary<string, bool> EnabledTypes { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// Maximum number of results to return per definition type
        /// </summary>
        [JsonPropertyName("maxResultsPerType")]
        public int MaxResultsPerType { get; set; } = 10;

        /// <summary>
        /// Whether to sort results by last update date
        /// </summary>
        [JsonPropertyName("sortByLastUpdate")]
        public bool SortByLastUpdate { get; set; } = false;

        /// <summary>
        /// Gets the default configuration with all definition types enabled
        /// </summary>
        /// <returns>A SmartOpenConfig with default settings</returns>
        public static SmartOpenConfig GetDefault()
        {
            var config = new SmartOpenConfig
            {
                MaxResultsPerType = 10,
                SortByLastUpdate = false
            };

            // Enable all definition types by default
            var definitionTypes = new[]
            {
                "Activity",
                "Analytic Model",
                "Analytic Type",
                "App Engine Program",
                "Application Package",
                "Application Class",
                "Approval Rule Set",
                "Business Interlink",
                "Business Process",
                "Component",
                "Component Interface",
                "Field",
                "File Layout",
                "File Reference",
                "HTML",
                "Image",
                "Menu",
                "Message",
                "Optimization Model",
                "Page",
                "Page (Fluid)",
                "Project",
                "Record",
                "SQL",
                "Style Sheet"
            };

            foreach (var type in definitionTypes)
            {
                config.EnabledTypes[type] = true;
            }

            return config;
        }

        /// <summary>
        /// Gets all available definition types
        /// </summary>
        /// <returns>Array of all definition type names</returns>
        public static string[] GetAllDefinitionTypes()
        {
            return new[]
            {
                "Activity",
                "Analytic Model", 
                "Analytic Type",
                "App Engine Program",
                "Application Package",
                "Application Class",
                "Approval Rule Set",
                "Business Interlink",
                "Business Process",
                "Component",
                "Component Interface",
                "Field",
                "File Layout",
                "File Reference",
                "HTML",
                "Image",
                "Menu",
                "Message",
                "Optimization Model",
                "Page",
                "Page (Fluid)",
                "Project",
                "Record",
                "SQL",
                "Style Sheet"
            };
        }
    }
}