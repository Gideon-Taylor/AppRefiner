namespace AppRefiner.Database.Models
{
    public enum OpenTargetType
    {
        Project,
        Page
    }

    /// <summary>
    /// Represents a target that can be opened in Application Designer
    /// </summary>
    public class OpenTarget
    {
        /// <summary>
        /// Gets the type of the target
        /// </summary>
        public OpenTargetType Type { get; }

        /// <summary>
        /// Gets the display name of the target
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the description of the target
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the object IDs (PSCLASSID values)
        /// </summary>
        public PSCLASSID[] ObjectIDs { get; }

        /// <summary>
        /// Gets the object values
        /// </summary>
        public string?[] ObjectValues { get; }

        /// <summary>
        /// Creates a new OpenTarget with the specified type, name, description, and object pairs
        /// </summary>
        /// <param name="type">The type of the target</param>
        /// <param name="name">The display name</param>
        /// <param name="description">The description</param>
        /// <param name="objectPairs">The object ID/value pairs (up to 7)</param>
        public OpenTarget(
            OpenTargetType type,
            string name,
            string description,
            IEnumerable<(PSCLASSID ObjectID, string ObjectValue)> objectPairs)
        {
            Type = type;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;

            ObjectIDs = new PSCLASSID[7];
            ObjectValues = new string?[7];

            // Initialize all to defaults
            for (int i = 0; i < 7; i++)
            {
                ObjectIDs[i] = PSCLASSID.NONE;
                ObjectValues[i] = null;
            }

            // Fill with provided pairs
            int index = 0;
            foreach (var (objectID, objectValue) in objectPairs.Take(7))
            {
                ObjectIDs[index] = objectID;
                ObjectValues[index] = objectValue;
                index++;
            }
        }

        /// <summary>
        /// Gets the path representation for display
        /// </summary>
        public string Path => BuildPath();

        /// <summary>
        /// Builds a path from the object values based on the target type
        /// </summary>
        /// <returns>A formatted path string</returns>
        public string BuildPath()
        {
            switch (Type)
            {
                case OpenTargetType.Project:
                    return ObjectValues[0] ?? string.Empty;
                
                case OpenTargetType.Page:
                    return ObjectValues[0] ?? string.Empty;
                
                default:
                    // Fallback: join non-empty values
                    return string.Join(":", ObjectValues.Where(v => !string.IsNullOrEmpty(v)));
            }
        }

        /// <summary>
        /// Returns a string representation suitable for display
        /// </summary>
        public override string ToString()
        {
            var typeName = Type.ToString();
            var displayName = !string.IsNullOrEmpty(Description) && Description != Name 
                ? $"{Name} - {Description}"
                : Name;
            
            return $"{typeName}: {displayName}";
        }
    }
}