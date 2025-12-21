using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        /// The name of the extension member (e.g., "Length", "IndexOf")
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Description of what this extension does
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Whether this is a property or method extension
        /// </summary>
        public abstract LanguageExtensionType ExtensionType { get; }

        /// <summary>
        /// All target types this extension applies to.
        /// For single-type extensions, return a list with one element.
        /// For multi-type extensions (e.g., Length for both String and Rowset), return multiple types.
        /// </summary>
        public abstract List<TypeWithDimensionality> TargetTypes { get; }

        #endregion

        #region Method-Specific Properties

        /// <summary>
        /// Parameters for method extensions. Returns null for property extensions.
        /// </summary>
        public virtual List<Parameter>? Parameters => null;

        /// <summary>
        /// Return type of the extension. Can be null if return type is not specified.
        /// </summary>
        public virtual TypeWithDimensionality? ReturnType => null;

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

        #region Transform Method

        /// <summary>
        /// Performs the code transformation for this extension.
        /// Implementation deferred - will be called when trigger mechanism is implemented.
        /// </summary>
        /// <param name="editor">The Scintilla editor containing the code</param>
        /// <param name="node">The AST node where the extension is used</param>
        /// <param name="matchedType">The actual type that was matched (important for multi-type extensions)</param>
        public abstract void Transform(ScintillaEditor editor, AstNode node, TypeWithDimensionality matchedType);

        #endregion

        #region Configuration Support

        /// <summary>
        /// Gets the list of configurable properties for this extension.
        /// Excludes core properties like Name, Description, Active, etc.
        /// </summary>
        /// <returns>List of properties that can be configured by the user</returns>
        public List<System.Reflection.PropertyInfo> GetConfigurableProperties()
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite &&
                          p.GetCustomAttribute<JsonIgnoreAttribute>() == null &&
                          p.Name != nameof(Name) &&
                          p.Name != nameof(Description) &&
                          p.Name != nameof(ExtensionType) &&
                          p.Name != nameof(TargetTypes) &&
                          p.Name != nameof(Parameters) &&
                          p.Name != nameof(ReturnType) &&
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
