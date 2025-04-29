using System;
using Antlr4.Runtime.Tree;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using AppRefiner.Database;

namespace AppRefiner.Ast
{
    /// <summary>
    /// Represents a property definition within a PeopleCode Application Class or Interface.
    /// </summary>
    public class Property
    {
        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the type of the property as a string.
        /// </summary>
        public Type Type { get; private set; }

        /// <summary>
        /// Gets the scope (visibility) of the property.
        /// </summary>
        public Scope Scope { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the property has a getter.
        /// </summary>
        public bool HasGetter { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the property has a setter.
        /// </summary>
        public bool HasSetter { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the property is declared as readonly.
        /// </summary>
        public bool IsReadonly { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the property is declared as abstract.
        /// </summary>
        public bool IsAbstract { get; private set; }

        /// <summary>
        /// Gets the full name of the declaring type.
        /// </summary>
        public string DeclaringTypeFullName { get; set; } = string.Empty;

        /// <summary>
        /// Parses a PropertyDeclarationContext to create a Property instance.
        /// </summary>
        /// <param name="context">The ANTLR context for the property declaration.</param>
        /// <param name="scope">The scope determined by the containing class section.</param>
        /// <param name="dataManager">The data manager for potential type lookups (currently unused).</param>
        /// <returns>A new Property instance.</returns>
        public static Property Parse(PropertyDeclarationContext context, Scope scope, IDataManager dataManager)
        {
            // Initialize Name and Type here, will be set inside the type checks
            string name = string.Empty;
            Type? type = null;
            bool hasGetter = false;
            bool hasSetter = false;
            bool isReadonly = false;
            bool isAbstract = false;

            if (context is PropertyGetSetContext getSetContext)
            {
                name = getSetContext.genericID().GetText(); // Access here
                type = Ast.Type.Parse(getSetContext.typeT()); // Access here
                hasGetter = true; // GET is mandatory
                hasSetter = getSetContext.SET() != null;
                isReadonly = false; // Cannot be readonly if SET is possible
                isAbstract = false; // Get/Set properties cannot be abstract in the header
            }
            else if (context is PropertyDirectContext directContext)
            {
                name = directContext.genericID().GetText(); // Access here
                type = Ast.Type.Parse(directContext.typeT()); // Access here
                hasGetter = false; // Implied getter
                hasSetter = false;
                isReadonly = directContext.READONLY() != null;
                isAbstract = directContext.ABSTRACT() != null;
            }
            else
            {
                // Should not happen if grammar is correct
                throw new ArgumentException("Unknown property declaration context type.");
            }

            // Now create the Property object
            var property = new Property
            {
                Name = name,
                Type = type,
                Scope = scope,
                HasGetter = hasGetter,
                HasSetter = hasSetter,
                IsReadonly = isReadonly,
                IsAbstract = isAbstract
            };

            return property;
        }
    }
} 