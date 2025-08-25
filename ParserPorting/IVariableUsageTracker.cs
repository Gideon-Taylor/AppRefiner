using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// Interface for tracking variable usage across scopes
/// </summary>
public interface IVariableUsageTracker
{
    /// <summary>
    /// Registers a variable in the specified scope
    /// </summary>
    /// <param name="variable">The variable information</param>
    /// <param name="scope">The scope where the variable is declared</param>
    void RegisterVariable(VariableInfo variable, ScopeInfo scope);
    
    /// <summary>
    /// Marks a variable as used by name, searching in the current scope and parent scopes
    /// </summary>
    /// <param name="name">The variable name</param>
    /// <param name="currentScope">The current scope to start searching from</param>
    /// <returns>True if a variable was found and marked as used, false otherwise</returns>
    bool MarkAsUsed(string name, ScopeInfo currentScope);
    
    /// <summary>
    /// Checks if a variable is used
    /// </summary>
    /// <param name="variable">The variable to check</param>
    /// <param name="scope">The scope where the variable is declared</param>
    /// <returns>True if the variable is used, false otherwise</returns>
    bool IsUsed(VariableInfo variable, ScopeInfo scope);
    
    /// <summary>
    /// Gets all unused variables across all scopes
    /// </summary>
    /// <returns>A collection of unused variables with their scopes</returns>
    IEnumerable<(VariableInfo Variable, ScopeInfo Scope)> GetUnusedVariables();
    
    /// <summary>
    /// Gets all unused variables in a specific scope
    /// </summary>
    /// <param name="scope">The scope to check</param>
    /// <returns>A collection of unused variables in the specified scope</returns>
    IEnumerable<VariableInfo> GetUnusedVariablesInScope(ScopeInfo scope);
    
    /// <summary>
    /// Resets the tracker to its initial state
    /// </summary>
    void Reset();
}
