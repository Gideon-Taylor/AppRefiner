using System;
using System.Collections.Generic;

namespace AppRefiner.Database.Models
{
    /// <summary>
    /// Represents a PeopleSoft project item, storing object type and object ID/value pairs
    /// </summary>
    public class ProjectItem
    {
        /// <summary>
        /// Gets the object type code
        /// </summary>
        public int ObjectType { get; }
        
        /// <summary>
        /// Gets the object type code
        /// </summary>
        public int[] ObjectIds { get; }
        
        /// <summary>
        /// Gets the object values
        /// </summary>
        public string[] ObjectValues { get; }
        
        /// <summary>
        /// Creates a new project item with the specified object type and ID/value pairs
        /// </summary>
        /// <param name="objectType">The object type code</param>
        /// <param name="objectId1">First object ID</param>
        /// <param name="objectValue1">First object value</param>
        /// <param name="objectId2">Second object ID</param>
        /// <param name="objectValue2">Second object value</param>
        /// <param name="objectId3">Third object ID</param>
        /// <param name="objectValue3">Third object value</param>
        /// <param name="objectId4">Fourth object ID</param>
        /// <param name="objectValue4">Fourth object value</param>
        public ProjectItem(
            int objectType,
            int objectId1, string objectValue1,
            int objectId2, string objectValue2,
            int objectId3, string objectValue3,
            int objectId4, string objectValue4)
        {
            ObjectType = objectType;
            ObjectIds = new int[4] { objectId1, objectId2, objectId3, objectId4 };
            ObjectValues = new string[4] 
            { 
                objectValue1 ?? string.Empty,
                objectValue2 ?? string.Empty,
                objectValue3 ?? string.Empty,
                objectValue4 ?? string.Empty
            };
        }
        
        /// <summary>
        /// Builds a path from the object values
        /// </summary>
        /// <returns>A colon-separated path of non-empty object values</returns>
        public string BuildPath()
        {
            List<string> pathParts = new List<string>();
            
            for (int i = 0; i < ObjectValues.Length; i++)
            {
                if (!string.IsNullOrEmpty(ObjectValues[i]?.Trim()))
                    pathParts.Add(ObjectValues[i]);
            }
            
            return string.Join(":", pathParts);
        }
        
        /// <summary>
        /// Converts project item object IDs/values to program object IDs/values (7 pairs)
        /// </summary>
        /// <returns>Dictionary mapping column names to values for PSPCMPROG query</returns>
        public Dictionary<string, object> ToProgramFields()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            
            // Copy the existing values
            for (int i = 0; i < ObjectIds.Length; i++)
            {
                parameters[$"OBJECTID{i+1}"] = ObjectIds[i];
                parameters[$"OBJECTVALUE{i+1}"] = ObjectValues[i];
            }
            
            // Default values for remaining parameters
            // These will need to be overridden based on the object type in a real implementation
            parameters["OBJECTID5"] = DBNull.Value;
            parameters["OBJECTVALUE5"] = DBNull.Value;
            parameters["OBJECTID6"] = DBNull.Value;
            parameters["OBJECTVALUE6"] = DBNull.Value;
            parameters["OBJECTID7"] = DBNull.Value;
            parameters["OBJECTVALUE7"] = DBNull.Value;
            
            // TODO: Implement type-specific mapping logic here
            // Different object types may have different mapping rules
            
            return parameters;
        }
    }
}
