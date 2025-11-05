using System.Text.Json.Serialization;

namespace AppRefiner
{
    /// <summary>
    /// Configuration settings for the Auto Suggest feature
    /// </summary>
    public class AutoSuggestSettings
    {
        [JsonPropertyName("variableSuggestions")]
        public bool VariableSuggestions { get; set; }

        [JsonPropertyName("functionSignatures")]
        public bool FunctionSignatures { get; set; }

        [JsonPropertyName("objectMembers")]
        public bool ObjectMembers { get; set; }

        [JsonPropertyName("systemVariables")]
        public bool SystemVariables { get; set; }

        /// <summary>
        /// Gets the default AutoSuggest configuration with all features enabled
        /// </summary>
        /// <returns>AutoSuggestSettings with default values</returns>
        public static AutoSuggestSettings GetDefault()
        {
            return new AutoSuggestSettings
            {
                VariableSuggestions = true,
                FunctionSignatures = true,
                ObjectMembers = true,
                SystemVariables = true
            };
        }
    }
}
