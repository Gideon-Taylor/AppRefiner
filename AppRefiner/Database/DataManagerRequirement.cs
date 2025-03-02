using System;

namespace AppRefiner.Database
{
    /// <summary>
    /// Specifies whether a lint rule requires a database connection
    /// </summary>
    public enum DataManagerRequirement
    {
        /// <summary>
        /// The rule requires a database connection to function
        /// </summary>
        Required,
        
        /// <summary>
        /// The rule can optionally use a database connection if available
        /// </summary>
        Optional,
        
        /// <summary>
        /// The rule does not require a database connection
        /// </summary>
        NotRequired
    }
}
