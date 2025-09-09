using System.Reflection;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Extension methods for refactoring operations
    /// </summary>
    public static class RefactorExtensions
    {
        /// <summary>
        /// Gets the configurable properties for a refactor type
        /// </summary>
        /// <param name="refactorType">The refactor type to inspect</param>
        /// <returns>A list of configurable properties</returns>
        public static List<PropertyInfo> GetConfigurableProperties(this Type refactorType)
        {
            var properties = new List<PropertyInfo>();

            // Get all instance properties with a ConfigurableProperty attribute
            foreach (var prop in refactorType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var configurableAttr = prop.GetCustomAttribute<ConfigurablePropertyAttribute>();
                if (configurableAttr != null)
                {
                    properties.Add(prop);
                }
            }

            return properties;
        }
    }
}
