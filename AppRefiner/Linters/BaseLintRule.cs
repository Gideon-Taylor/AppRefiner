using Antlr4.Runtime;
using AppRefiner.Database;
using AppRefiner.PeopleCode;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppRefiner.Linters
{
    public abstract class BaseLintRule : PeopleCodeParserBaseListener
    {
        // Linter ID must be set by all subclasses
        public abstract string LINTER_ID { get; }

        public bool Active = false;
        public string Description = "Description not set";
        public ReportType Type;
        public List<Report>? Reports;
        public virtual DataManagerRequirement DatabaseRequirement => DataManagerRequirement.NotRequired;
        public IDataManager? DataManager;

        // Add collection to store comments from lexer
        public IList<IToken>? Comments;

        // The suppression listener shared across all linters
        public LinterSuppressionListener? SuppressionListener;

        public virtual void Reset() {
            
        }

        // Helper method to create a report with the proper linter ID set and add it to the Reports list
        // if it's not suppressed by a pragma directive
        protected void AddReport(int reportNumber, string message, ReportType type, int line, (int Start, int Stop) span)
        {
            Report report = new()
            {
                LinterId = LINTER_ID,
                ReportNumber = reportNumber,
                Message = message,
                Type = type,
                Line = line,
                Span = span
            };

            // Initialize Reports list if needed
            if (Reports == null)
            {
                Reports = new List<Report>();
            }

            // Only add the report if it's not suppressed
            if (SuppressionListener == null || !SuppressionListener.IsSuppressed(report.LinterId, report.ReportNumber, report.Line))
            {
                Reports.Add(report);
            }
        }

        /// <summary>
        /// Gets all configurable properties of the linter
        /// </summary>
        /// <returns>A list of PropertyInfo objects representing configurable properties</returns>
        public List<PropertyInfo> GetConfigurableProperties()
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && 
                          p.GetCustomAttribute<JsonIgnoreAttribute>() == null &&
                          p.Name != nameof(LINTER_ID) &&
                          p.Name != nameof(DatabaseRequirement) &&
                          p.Name != nameof(DataManager) &&
                          p.Name != nameof(Comments) &&
                          p.Name != nameof(SuppressionListener) &&
                          p.Name != nameof(Reports))
                .ToList();

            return properties;
        }

        /// <summary>
        /// Gets the linter configuration as a JSON string
        /// </summary>
        /// <returns>JSON string containing the linter configuration</returns>
        public string GetLinterConfig()
        {
            var configProperties = GetConfigurableProperties();
            var config = new Dictionary<string, object?>();

            foreach (var property in configProperties)
            {
                config[property.Name] = property.GetValue(this);
            }

            return JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <summary>
        /// Sets the linter configuration from a JSON string
        /// </summary>
        /// <param name="jsonConfig">JSON string containing the linter configuration</param>
        public void SetLinterConfig(string jsonConfig)
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonConfig);
            if (config == null) return;

            var configProperties = GetConfigurableProperties();

            foreach (var property in configProperties)
            {
                if (config.TryGetValue(property.Name, out var value))
                {
                    try
                    {
                        var typedValue = JsonSerializer.Deserialize(value.GetRawText(), property.PropertyType);
                        property.SetValue(this, typedValue);
                    }
                    catch
                    {
                        // Skip properties that can't be deserialized
                    }
                }
            }
        }
    }
}
