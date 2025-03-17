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
        /// Gets the first object ID
        /// </summary>
        public int ObjectId1 { get; }
        
        /// <summary>
        /// Gets the first object value
        /// </summary>
        public string ObjectValue1 { get; }
        
        /// <summary>
        /// Gets the second object ID
        /// </summary>
        public int ObjectId2 { get; }
        
        /// <summary>
        /// Gets the second object value
        /// </summary>
        public string ObjectValue2 { get; }
        
        /// <summary>
        /// Gets the third object ID
        /// </summary>
        public int ObjectId3 { get; }
        
        /// <summary>
        /// Gets the third object value
        /// </summary>
        public string ObjectValue3 { get; }
        
        /// <summary>
        /// Gets the fourth object ID
        /// </summary>
        public int ObjectId4 { get; }
        
        /// <summary>
        /// Gets the fourth object value
        /// </summary>
        public string ObjectValue4 { get; }
        
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
            ObjectId1 = objectId1;
            ObjectValue1 = objectValue1 ?? string.Empty;
            ObjectId2 = objectId2;
            ObjectValue2 = objectValue2 ?? string.Empty;
            ObjectId3 = objectId3;
            ObjectValue3 = objectValue3 ?? string.Empty;
            ObjectId4 = objectId4;
            ObjectValue4 = objectValue4 ?? string.Empty;
        }
        
        /// <summary>
        /// Builds a path from the object values
        /// </summary>
        /// <returns>A colon-separated path of non-empty object values</returns>
        public string BuildPath()
        {
            List<string> pathParts = new List<string>();
            
            if (!string.IsNullOrEmpty(ObjectValue1?.Trim()))
                pathParts.Add(ObjectValue1);
                
            if (!string.IsNullOrEmpty(ObjectValue2?.Trim()))
                pathParts.Add(ObjectValue2);
                
            if (!string.IsNullOrEmpty(ObjectValue3?.Trim()))
                pathParts.Add(ObjectValue3);
                
            if (!string.IsNullOrEmpty(ObjectValue4?.Trim()))
                pathParts.Add(ObjectValue4);
                
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
            parameters["OBJECTID1"] = ObjectId1;
            parameters["OBJECTVALUE1"] = ObjectValue1;
            parameters["OBJECTID2"] = ObjectId2;
            parameters["OBJECTVALUE2"] = ObjectValue2;
            parameters["OBJECTID3"] = ObjectId3;
            parameters["OBJECTVALUE3"] = ObjectValue3;
            parameters["OBJECTID4"] = ObjectId4;
            parameters["OBJECTVALUE4"] = ObjectValue4;
            
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
