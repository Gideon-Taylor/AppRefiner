using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using static PeopleCodeParser.SelfHosted.Visitors.VariableUsageTracker;

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
    /// Marks a variable as used by name with location tracking, searching in the current scope and parent scopes
    /// </summary>
    /// <param name="name">The variable name</param>
    /// <param name="location">The source location where the variable is referenced</param>
    /// <param name="currentScope">The current scope to start searching from</param>
    /// <returns>True if a variable was found and marked as used, false otherwise</returns>
    bool MarkAsUsedWithLocation(string name, SourceSpan location, ScopeInfo currentScope);
    
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
    
    /// <summary>
    /// Checks if a variable is defined in any accessible scope
    /// </summary>
    /// <param name="name">The variable name to check</param>
    /// <param name="currentScope">The current scope to start searching from</param>
    /// <returns>True if the variable is defined in any accessible scope, false otherwise</returns>
    bool IsVariableDefined(string name, ScopeInfo currentScope);
    
    /// <summary>
    /// Tracks a reference to an undefined variable
    /// </summary>
    /// <param name="name">The undefined variable name</param>
    /// <param name="location">The source location where the undefined variable was referenced</param>
    /// <param name="scope">The scope where the undefined reference occurred</param>
    void TrackUndefinedReference(string name, SourceSpan location, ScopeInfo scope);
    
    /// <summary>
    /// Gets all tracked undefined variable references
    /// </summary>
    /// <returns>A collection of undefined variable references with their locations and scopes</returns>
    IEnumerable<(string Name, SourceSpan Location, ScopeInfo Scope)> GetUndefinedReferences();
    
    /// <summary>
    /// Gets all reference locations for a variable by name in the specified scope
    /// </summary>
    /// <param name="name">The variable name</param>
    /// <param name="scope">The scope where the variable is declared</param>
    /// <returns>A collection of source locations where the variable is referenced</returns>
    IEnumerable<SourceSpan> GetVariableReferences(string name, ScopeInfo scope);
    
    /// <summary>
    /// Gets all reference locations for a variable in the specified scope
    /// </summary>
    /// <param name="variable">The variable information</param>
    /// <param name="scope">The scope where the variable is declared</param>
    /// <returns>A collection of source locations where the variable is referenced</returns>
    IEnumerable<SourceSpan> GetVariableReferences(VariableInfo variable, ScopeInfo scope);

    IEnumerable<VariableInfo> GetAllVariablesInScope(ScopeInfo scope);
}
