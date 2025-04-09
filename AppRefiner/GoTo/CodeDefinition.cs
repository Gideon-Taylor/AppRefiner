using System;
using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Commands
{
    /// <summary>
    /// Enumeration of code definition types
    /// </summary>
    public enum DefinitionType
    {
        Method,
        Property,
        Function,
        Getter,
        Setter
    }

    /// <summary>
    /// Enumeration of code definition scopes
    /// </summary>
    public enum DefinitionScope
    {
        Public,
        Protected,
        Private,
        Global  // For functions outside of classes
    }

    /// <summary>
    /// Represents a code definition (method, property, function, etc.) that can be navigated to
    /// </summary>
    public class CodeDefinition
    {
        /// <summary>
        /// The name of the definition
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of the definition (return type for methods, property type, etc.)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The type of code definition (method, property, function, etc.)
        /// </summary>
        public DefinitionType DefinitionType { get; set; }

        /// <summary>
        /// The scope of the definition (public, protected, private)
        /// </summary>
        public DefinitionScope Scope { get; set; }

        /// <summary>
        /// The position in the source code where the definition begins
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Line number where the definition begins
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Gets the formatted display text for the definition
        /// </summary>
        public string DisplayText => $"{GetScopePrefix()}{Name}: {Type} [{DefinitionType}]";

        /// <summary>
        /// Gets a description of the definition for tooltip or additional information
        /// </summary>
        public string Description => $"{GetScopePrefix()}{DefinitionType} {Name} of type {Type} at line {Line}";

        /// <summary>
        /// Creates a new code definition with the provided parameters
        /// </summary>
        public CodeDefinition(string name, string type, DefinitionType definitionType, DefinitionScope scope, int position, int line)
        {
            Name = name;
            Type = type;
            DefinitionType = definitionType;
            Scope = scope;
            Position = position;
            Line = line;
        }

        /// <summary>
        /// Gets the scope prefix for display purposes
        /// </summary>
        private string GetScopePrefix()
        {
            return Scope switch
            {
                DefinitionScope.Public => "[Public] ",
                DefinitionScope.Protected => "[Protected] ",
                DefinitionScope.Private => "[Private] ",
                DefinitionScope.Global => "[Global] ",
                _ => string.Empty
            };
        }
    }
} 