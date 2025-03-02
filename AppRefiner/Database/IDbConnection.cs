using System;
using System.Collections.Generic;
using System.Data;

namespace AppRefiner.Database
{
    /// <summary>
    /// Interface for database connections
    /// </summary>
    public interface IDbConnection : IDisposable
    {
        /// <summary>
        /// Gets or sets the connection string
        /// </summary>
        string ConnectionString { get; set; }
        
        /// <summary>
        /// Gets the current state of the connection
        /// </summary>
        ConnectionState State { get; }
        
        /// <summary>
        /// Gets the name of the database server
        /// </summary>
        string ServerName { get; }
        
        /// <summary>
        /// Opens the database connection
        /// </summary>
        void Open();
        
        /// <summary>
        /// Closes the database connection
        /// </summary>
        void Close();
        
        /// <summary>
        /// Creates a command associated with this connection
        /// </summary>
        IDbCommand CreateCommand();
        
        /// <summary>
        /// Executes a query and returns the results as a DataTable
        /// </summary>
        DataTable ExecuteQuery(string sql, Dictionary<string, object> parameters = null);
        
        /// <summary>
        /// Executes a non-query command and returns the number of rows affected
        /// </summary>
        int ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null);
    }
}
