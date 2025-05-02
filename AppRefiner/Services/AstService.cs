using AppRefiner.Ast;
using AppRefiner.Database;
using AppRefiner.PeopleCode; // For ProgramParser and generated ANTLR types
using Antlr4.Runtime.Tree;
using System.Collections.Concurrent;
using System;
using System.Linq;

namespace AppRefiner.Services
{
    /// <summary>
    /// Service responsible for parsing PeopleCode source text into simplified AST objects.
    /// Handles resolving dependencies (extends, implements) using IDataManager.
    /// </summary>
    public class AstService
    {
        private readonly IDataManager _dataManager;
        // Cache stores fully qualified Program objects to avoid ambiguity
        private readonly ConcurrentDictionary<string, AppRefiner.Ast.Program> _programCache 
            = new ConcurrentDictionary<string, AppRefiner.Ast.Program>();

        public AstService(IDataManager dataManager)
        {
            _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
        }

        /// <summary>
        /// Parses an Application Class given its full path.
        /// Retrieves source code using IDataManager.
        /// </summary>
        /// <param name="fullPath">The full path of the Application Class (e.g., PKG:SUB:Class).</param>
        /// <returns>The parsed AppClass AST node, or null if not found or not an AppClass.</returns>
        public AppClass? GetAppClassAst(string fullPath)
        {
            // Use fully qualified type for variable
            AppRefiner.Ast.Program? programAst = GetOrParseProgram(fullPath);
            return programAst?.ContainedAppClass; // Extract AppClass from Program
        }

        /// <summary>
        /// Parses an Interface given its full path.
        /// Retrieves source code using IDataManager.
        /// </summary>
        /// <param name="fullPath">The full path of the Interface (e.g., PKG:SUB:Interface).</param>
        /// <returns>The parsed Interface AST node, or null if not found or not an Interface.</returns>
        public Interface? GetInterfaceAst(string fullPath)
        {
            // Use fully qualified type for variable
            AppRefiner.Ast.Program? programAst = GetOrParseProgram(fullPath);
            return programAst?.ContainedInterface; // Extract Interface from Program
        }

        /// <summary>
        /// Gets the full Program AST for a given path, using the cache if available.
        /// </summary>
        /// <param name="fullPath">The full path of the program (e.g., PKG:SUB:Class or PKG:SUB:Interface).</param>
        /// <returns>The parsed Program AST node, or null if not found or on error.</returns>
        public AppRefiner.Ast.Program? GetProgramAst(string fullPath)
        {
            return GetOrParseProgram(fullPath); // Call the private method
        }

        // Gets Program from cache or parses it - use fully qualified return type
        private AppRefiner.Ast.Program? GetOrParseProgram(string fullPath)
        {
             // Use fully qualified type for cache check
             if (_programCache.TryGetValue(fullPath, out AppRefiner.Ast.Program? cachedProgram))
            {
                return cachedProgram;
            }

            string? sourceCode = _dataManager.GetAppClassSourceByPath(fullPath);
            if (sourceCode == null)
            {
                // Consider logging a warning
                return null;
            }

            try
            {
                // Use the reusable parser
                PeopleCodeParser.ProgramContext programContext = ProgramParser.Parse(sourceCode);
                
                // Define resolvers that call back into this service's public methods
                Func<string, IDataManager, AppClass?> appClassResolver = (path, dm) => GetAppClassAst(path);
                Func<string, IDataManager, Interface?> interfaceResolver = (path, dm) => GetInterfaceAst(path);

                // Use the fully qualified Program AST Parse method
                var parsedProgram = AppRefiner.Ast.Program.Parse(programContext, fullPath, _dataManager);
                
                _programCache.TryAdd(fullPath, parsedProgram); // Cache the resulting Program object
                return parsedProgram;
            }
            catch (Exception ex) // Catch parsing errors or other issues
            {
                // Log the exception (using your preferred logging mechanism)
                Console.Error.WriteLine($"Error parsing program '{fullPath}': {ex.Message}");
                // Optionally cache a negative result marker if needed, otherwise return null
                return null;
            }
        }

        /// <summary>
        /// Clears the internal parse cache.
        /// </summary>
        public void ClearCache()
        {
            _programCache.Clear();
        }
    }
} 