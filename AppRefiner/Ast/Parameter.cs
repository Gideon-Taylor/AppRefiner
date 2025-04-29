using System;

namespace AppRefiner.Ast
{
    /// <summary>
    /// Represents a parameter in a PeopleCode method or function signature.
    /// </summary>
    public class Parameter
    {
        /// <summary>
        /// Gets the name of the parameter.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the structured type of the parameter.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets a value indicating whether the parameter is declared with the OUT keyword.
        /// </summary>
        public bool IsOut { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Parameter"/> class.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="type">The parameter type.</param>
        /// <param name="isOut">Whether the parameter is an OUT parameter.</param>
        public Parameter(string name, Type type, bool isOut)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            IsOut = isOut;
        }

        // Note: We might add parsing logic here later if needed, 
        // but for now, it's simpler to construct it directly.
    }
} 