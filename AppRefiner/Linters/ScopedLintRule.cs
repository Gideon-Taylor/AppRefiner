using AppRefiner.Database;
using PeopleCodeParser.SelfHosted.Visitors;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Base class for lint rules that need to track variables and other elements within code scopes.
    /// Works with LinterSuppressionListener to respect scope-based suppression directives.
    /// Uses ScopedAstVisitor for comprehensive scope and variable tracking.
    /// </summary>
    /// <typeparam name="T">The type of data to track in scopes</typeparam>
    public abstract class BaseLintRule : ScopedAstVisitor<object>
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

        // Helper method to add a custom report
        protected void AddReport(int reportNumber, string message, ReportType type, int line, PeopleCodeParser.SelfHosted.SourceSpan span)
        {
            var report = new Report
            {
                LinterId = LINTER_ID,
                ReportNumber = reportNumber,
                Message = message,
                Type = type,
                Line = line,
                Span = (span.Start.ByteIndex, span.End.ByteIndex)
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

        // ILinter interface implementation methods
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
