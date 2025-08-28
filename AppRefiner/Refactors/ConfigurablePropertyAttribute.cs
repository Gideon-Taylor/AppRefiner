namespace AppRefiner.Refactors
{
    /// <summary>
    /// Attribute to mark a property as configurable in the refactor configuration dialog
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigurablePropertyAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the display name for the property
        /// </summary>
        public string DisplayName { get; set; }
        
        /// <summary>
        /// Gets or sets the description for the property
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Creates a new configurable property attribute
        /// </summary>
        /// <param name="displayName">Display name for the property</param>
        /// <param name="description">Description for the property</param>
        public ConfigurablePropertyAttribute(string displayName, string description)
        {
            DisplayName = displayName;
            Description = description;
        }
    }
}
