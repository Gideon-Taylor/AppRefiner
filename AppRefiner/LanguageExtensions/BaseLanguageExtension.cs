using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeInfo = PeopleCodeTypeInfo.Types.TypeInfo;

namespace AppRefiner.LanguageExtensions
{
    /// <summary>
    /// Specifies whether a language extension is a property or method extension
    /// </summary>
    public enum LanguageExtensionType
    {
        Property,
        Method
    }

    /// <summary>
    /// Base class for all language extensions. Language extensions allow extending PeopleCode types
    /// with new properties and methods (similar to C# extension methods) via code transformations.
    /// </summary>
    public abstract class BaseLanguageExtension
    {
        #region Metadata Properties

        /// <summary>
        /// The transforms provided by this extension.
        /// Each transform represents a property or method that can be added to target types.
        /// </summary>
        public abstract List<ExtensionTransform> Transforms { get; }

        /// <summary>
        /// The target type this extension applies to.
        /// All transforms defined in this extension will be available on this type.
        /// </summary>
        public abstract TypeInfo TargetType { get; }

        #endregion

        #region State Management

        /// <summary>
        /// Whether this extension is currently active
        /// </summary>
        [JsonIgnore]
        public bool Active { get; set; } = false;

        #endregion

        #region Database Support

        /// <summary>
        /// Specifies whether this extension requires a database connection
        /// </summary>
        public virtual DataManagerRequirement DatabaseRequirement => DataManagerRequirement.NotRequired;

        /// <summary>
        /// The data manager instance (if database is required)
        /// </summary>
        [JsonIgnore]
        public IDataManager? DataManager { get; set; }

        #endregion

        #region Configuration Support

        /// <summary>
        /// Gets the list of configurable properties for this extension.
        /// Excludes core properties like Transforms, TargetTypes, Active, etc.
        /// </summary>
        /// <returns>List of properties that can be configured by the user</returns>
        public List<System.Reflection.PropertyInfo> GetConfigurableProperties()
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite &&
                          p.GetCustomAttribute<JsonIgnoreAttribute>() == null &&
                          p.Name != nameof(Transforms) &&
                          p.Name != nameof(TargetType) &&
                          p.Name != nameof(Active) &&
                          p.Name != nameof(DatabaseRequirement) &&
                          p.Name != nameof(DataManager))
                .ToList();

            return properties;
        }

        /// <summary>
        /// Gets the current configuration for this extension as a JSON string
        /// </summary>
        /// <returns>JSON string containing configurable property values</returns>
        public string GetExtensionConfig()
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
        /// Sets the configuration for this extension from a JSON string
        /// </summary>
        /// <param name="jsonConfig">JSON string containing configuration values</param>
        public void SetExtensionConfig(string jsonConfig)
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

        #endregion
    }
}
