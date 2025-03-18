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
        public int[] ObjectIDs { get; }
        
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
            ObjectIDs = new int[4] { objectId1, objectId2, objectId3, objectId4 };
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
        public List<Tuple<int,string>> ToProgramFields()
        {
            var result = new List<Tuple<int, string>>(7); // Initialize with capacity for 7 items
            var objectType = ObjectType;

            // First key is always the ObjectType
            result.Add(new Tuple<int, string>(ObjectIDs[0], ObjectValues[0]));

            if (ObjectIDs[0] == 58)
            {
                // For objectType 58, only add values with non-zero ObjectIDs
                for (var i = 1; i < 4; i++)
                {
                    if (ObjectIDs[i] != 0)
                    {
                        result.Add(new Tuple<int, string>(ObjectIDs[i], ObjectValues[i]));
                    }
                }

                // Add the special "OnExecute" value with ID 12
                result.Add(new Tuple<int, string>(12, "OnExecute"));

                // Fill the rest with empty entries to have 7 total
                while (result.Count < 7)
                {
                    result.Add(new Tuple<int, string>(0, " "));
                }
            }
            else if (ObjectIDs[0] == 66)
            {
                // For objectType 66, we need to split the composite in projectItem.ObjectValues[1]
                var composite = ObjectValues[1];

                // Extract the components from the composite string
                string component1 = composite.Substring(0, Math.Min(8, composite.Length)).Trim();
                string component2 = composite.Length > 8 ? composite.Substring(8, Math.Min(3, composite.Length - 8)).Trim() : string.Empty;
                string component3 = composite.Length > 11 ? composite.Substring(11, Math.Min(9, composite.Length - 11)).Trim() : string.Empty;
                string component4 = composite.Length > 20 ? composite.Substring(20).Trim() : string.Empty;

                // Add the components
                result.Add(new Tuple<int, string>(77, component1));
                result.Add(new Tuple<int, string>(39, component2));
                result.Add(new Tuple<int, string>(20, component3));
                result.Add(new Tuple<int, string>(21, component4));

                // Add the last two keys
                result.Add(new Tuple<int, string>(ObjectIDs[2], ObjectValues[2]));
                result.Add(new Tuple<int, string>(ObjectIDs[3], ObjectValues[3]));
            }
            else if (ObjectIDs[0] == 104)
            {
                /* add each object id/value that isn't ID == 0 */
                for (var x = 1; x < 4; x++)
                {
                    if (ObjectIDs[x] != 0)
                    {
                        result.Add(new Tuple<int, string>(ObjectIDs[x], ObjectValues[x]));
                    }
                }

                /* add final 12, OnExecute */
                result.Add(new Tuple<int, string>(12, "OnExecute"));

                /* add remaining until we have 7 */
                while (result.Count < 7)
                {
                    result.Add(new Tuple<int, string>(0, " "));
                }

                int i = 3;
            }
            else if (ObjectIDs[0] == 10 && ObjectValues[3].Length > 18)
            {
                if (ObjectIDs[1] == 39)
                {
                    int i = 3;
                }
                // For objectType 10 with long ObjectValues[3]
                // Add the next 2 keys as they are
                for (var i = 1; i < 3; i++)
                {
                    result.Add(new Tuple<int, string>(ObjectIDs[i], ObjectValues[i]));
                }

                // Split the fourth value
                var key4 = ObjectValues[3].Substring(0, 18).Trim();
                var key5 = ObjectValues[3].Substring(18).Trim();

                result.Add(new Tuple<int, string>(ObjectIDs[3], key4));
                result.Add(new Tuple<int, string>(12, key5));

                // Fill the rest with empty entries
                while (result.Count < 7)
                {
                    result.Add(new Tuple<int, string>(0, " "));
                }
            }
            else
            {
                // Default case
                // Add the remaining 3 keys
                for (var i = 1; i < 4; i++)
                {
                    result.Add(new Tuple<int, string>(ObjectIDs[i], ObjectValues[i]));
                }

                // Fill the rest with empty entries
                while (result.Count < 7)
                {
                    result.Add(new Tuple<int, string>(0, " "));
                }
            }

            return result;
        }
    }
}
