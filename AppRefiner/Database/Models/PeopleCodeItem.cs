using System;
using System.Collections.Generic;
using System.Text;

namespace AppRefiner.Database.Models
{
    /// <summary>
    /// Represents a reference to a name in PeopleCode
    /// </summary>
    public class NameReference
    {
        /// <summary>
        /// Gets the name number
        /// </summary>
        public int NameNum { get; }
        
        /// <summary>
        /// Gets the record name
        /// </summary>
        public string RecName { get; }
        
        /// <summary>
        /// Gets the reference name
        /// </summary>
        public string RefName { get; }
        
        /// <summary>
        /// Creates a new name reference with the specified values
        /// </summary>
        /// <param name="nameNum">The name number</param>
        /// <param name="recName">The record name</param>
        /// <param name="refName">The reference name</param>
        public NameReference(int nameNum, string recName, string refName)
        {
            NameNum = nameNum;
            RecName = recName ?? string.Empty;
            RefName = refName ?? string.Empty;
        }
    }
    
    /// <summary>
    /// Represents a PeopleSoft PeopleCode item, storing object ID/value pairs and program text
    /// </summary>
    public class PeopleCodeItem
    {
        /// <summary>
        /// Gets the object IDs
        /// </summary>
        public int[] ObjectIDs { get; }
        
        /// <summary>
        /// Gets the object values
        /// </summary>
        public string[] ObjectValues { get; }
        
        /// <summary>
        /// Gets the program text as a byte array
        /// </summary>
        public byte[] ProgramText { get; }
        
        /// <summary>
        /// Gets the list of name references
        /// </summary>
        public List<NameReference> NameReferences { get; }
        
        /// <summary>
        /// Creates a new PeopleCode item with the specified object ID/value pairs and program text
        /// </summary>
        /// <param name="objectId1">First object ID</param>
        /// <param name="objectValue1">First object value</param>
        /// <param name="objectId2">Second object ID</param>
        /// <param name="objectValue2">Second object value</param>
        /// <param name="objectId3">Third object ID</param>
        /// <param name="objectValue3">Third object value</param>
        /// <param name="objectId4">Fourth object ID</param>
        /// <param name="objectValue4">Fourth object value</param>
        /// <param name="objectId5">Fifth object ID</param>
        /// <param name="objectValue5">Fifth object value</param>
        /// <param name="objectId6">Sixth object ID</param>
        /// <param name="objectValue6">Sixth object value</param>
        /// <param name="objectId7">Seventh object ID</param>
        /// <param name="objectValue7">Seventh object value</param>
        /// <param name="programText">The program text as a byte array</param>
        /// <param name="nameReferences">The list of name references</param>
        public PeopleCodeItem(
            int objectId1, string objectValue1,
            int objectId2, string objectValue2,
            int objectId3, string objectValue3,
            int objectId4, string objectValue4,
            int objectId5, string objectValue5,
            int objectId6, string objectValue6,
            int objectId7, string objectValue7,
            byte[] programText,
            List<NameReference> nameReferences = null)
        {
            ObjectIDs = new int[7] { 
                objectId1, objectId2, objectId3, objectId4, 
                objectId5, objectId6, objectId7 
            };
            
            ObjectValues = new string[7] { 
                objectValue1 ?? string.Empty,
                objectValue2 ?? string.Empty,
                objectValue3 ?? string.Empty,
                objectValue4 ?? string.Empty,
                objectValue5 ?? string.Empty,
                objectValue6 ?? string.Empty,
                objectValue7 ?? string.Empty
            };
            
            ProgramText = programText ?? Array.Empty<byte>();
            NameReferences = nameReferences ?? new List<NameReference>();
        }
        
        /// <summary>
        /// Creates a new PeopleCode item with arrays of object IDs and values
        /// </summary>
        /// <param name="objectIDs">Array of object IDs (must contain 7 elements)</param>
        /// <param name="objectValues">Array of object values (must contain 7 elements)</param>
        /// <param name="programText">The program text as a byte array</param>
        /// <param name="nameReferences">The list of name references</param>
        public PeopleCodeItem(
            int[] objectIDs, 
            string[] objectValues, 
            byte[] programText,
            List<NameReference> nameReferences = null)
        {
            if (objectIDs == null || objectIDs.Length != 7)
                throw new ArgumentException("ObjectIDs array must contain exactly 7 elements", nameof(objectIDs));
                
            if (objectValues == null || objectValues.Length != 7)
                throw new ArgumentException("ObjectValues array must contain exactly 7 elements", nameof(objectValues));
                
            ObjectIDs = (int[])objectIDs.Clone();
            
            ObjectValues = new string[7];
            for (int i = 0; i < 7; i++)
            {
                ObjectValues[i] = objectValues[i] ?? string.Empty;
            }
            
            ProgramText = programText ?? Array.Empty<byte>();
            NameReferences = nameReferences ?? new List<NameReference>();
        }
        
        /// <summary>
        /// Gets the program text as a string using the specified encoding
        /// </summary>
        /// <param name="encoding">The encoding to use (defaults to UTF-8)</param>
        /// <returns>The program text as a string</returns>
        public string GetProgramTextAsString()
        {
            if (ProgramText == null || ProgramText.Length == 0)
                return string.Empty;

            PeopleCodeDecoder decoder = new PeopleCodeDecoder();
            return decoder.ParsePPC(ProgramText, NameReferences);
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
        /// Creates a ProjectItem from this PeopleCode item
        /// </summary>
        /// <returns>A new ProjectItem with the first 4 object ID/value pairs</returns>
        public ProjectItem ToProjectItem()
        {
            // Derive object type based on object IDs or other logic
            int objectType = DeriveObjectType();
            
            return new ProjectItem(
                objectType,
                ObjectIDs[0], ObjectValues[0],
                ObjectIDs[1], ObjectValues[1],
                ObjectIDs[2], ObjectValues[2],
                ObjectIDs[3], ObjectValues[3]
            );
        }
        
        /// <summary>
        /// Derives the object type based on this PeopleCode item's characteristics
        /// </summary>
        /// <returns>The derived object type</returns>
        private int DeriveObjectType()
        {
            // This is a placeholder method - actual implementation would need
            // to determine the appropriate object type based on the ObjectIDs/ObjectValues
            // and potentially other factors
            
            // For now, just return a default value
            return 8; // A default PeopleCode object type
        }
    }
}
