using AppRefiner.Database.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace AppRefiner.Database
{
    /// <summary>
    /// Interface for data management operations
    /// </summary>
    public interface IDataManager : IDisposable
    {
        /// <summary>
        /// Gets the underlying database connection
        /// </summary>
        IDbConnection Connection { get; }
        
        /// <summary>
        /// Gets whether the manager is connected
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Connect to the database
        /// </summary>
        /// <returns>True if connection was successful</returns>
        bool Connect();
        
        /// <summary>
        /// Disconnect from the database
        /// </summary>
        void Disconnect();
        
        /// <summary>
        /// Retrieves the SQL definition for a given object name
        /// </summary>
        /// <param name="objectName">Name of the SQL object</param>
        /// <returns>The SQL definition as a string</returns>
        string GetSqlDefinition(string objectName);
        
        /// <summary>
        /// Retrieves all available SQL definitions
        /// </summary>
        /// <returns>Dictionary mapping object names to their SQL definitions</returns>
        Dictionary<string, string> GetAllSqlDefinitions();
        
        /// <summary>
        /// Retrieves the HTML definition for a given object name
        /// </summary>
        /// <param name="objectName">Name of the HTML object</param>
        /// <returns>The HTML definition</returns>
        HtmlDefinition GetHtmlDefinition(string objectName);

        /// <summary>
        /// Retrieves all available HTML definitions
        /// </summary>
        /// <returns>Dictionary mapping object names to their HTML definitions</returns>
        Dictionary<string, HtmlDefinition> GetAllHtmlDefinitions();
        
        /// <summary>
        /// Gets all PeopleCode definitions for a specified project
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>List of tuples containing path and content (initially empty)</returns>
        List<Tuple<string, string>> GetPeopleCodeForProject(string projectName);
    }
}
