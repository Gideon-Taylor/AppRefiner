using AppRefiner.Database;
using PeopleCodeParser.SelfHosted.Visitors;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Common interface for all linter types
    /// </summary>
    public interface ILinter
    {
        string LINTER_ID { get; }
        bool Active { get; set; }
        string Description { get; set; }
        ReportType Type { get; set; }
        List<Report>? Reports { get; set; }
        DataManagerRequirement DatabaseRequirement { get; }
        IDataManager? DataManager { get; set; }
        void Reset();
        List<PropertyInfo> GetConfigurableProperties();
        string GetLinterConfig();
        void SetLinterConfig(string jsonConfig);
    }

    public abstract class BaseLintRule : AstVisitorBase, ILinter
    {
        // Linter ID must be set by all subclasses
        public abstract string LINTER_ID { get; }

        public bool Active { get; set; } = false;
        public string Description { get; set; } = "Description not set";
        public ReportType Type { get; set; }
        public List<Report>? Reports { get; set; }
        public virtual DataManagerRequirement DatabaseRequirement => DataManagerRequirement.NotRequired;
        public IDataManager? DataManager { get; set; }

        // The suppression processor shared across all linters
        public LinterSuppressionProcessor? SuppressionProcessor { get; set; }

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
            if (SuppressionProcessor == null || !SuppressionProcessor.IsSuppressed(report.LinterId, report.ReportNumber, report.Line))
            {
                Reports.Add(report);
            }
        }

        /// <summary>
        /// Helper method to create a report using SourceSpan for precise positioning.
        /// </summary>
        /// <param name="reportNumber">The report number</param>
        /// <param name="message">The report message</param>
        /// <param name="type">The report type</param>
        /// <param name="line">The line number</param>
        /// <param name="span">The SourceSpan containing start and end positions</param>
        protected void AddReport(int reportNumber, string message, ReportType type, int line, PeopleCodeParser.SelfHosted.SourceSpan span)
        {
            AddReport(reportNumber, message, type, line, (span.Start.ByteIndex, span.End.ByteIndex));
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
                          p.Name != nameof(SuppressionProcessor) &&
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
