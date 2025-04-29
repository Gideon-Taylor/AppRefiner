using System;
using System.Collections.Generic;
using System.Linq;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Ast
{
    /// <summary>
    /// Represents a private instance variable declaration within a PeopleCode Application Class.
    /// </summary>
    public class InstanceVariable
    {
        /// <summary>
        /// Gets the declared type of the instance variable.
        /// </summary>
        public Type Type { get; private set; }

        /// <summary>
        /// Gets the list of variable names declared with this type.
        /// (e.g., INSTANCE String &s1, &s2;)
        /// </summary>
        public List<string> Names { get; private set; }

        // Private constructor
        private InstanceVariable(Type type, List<string> names)
        {
            Type = type;
            Names = names;
        }

        /// <summary>
        /// Parses an InstanceDeclContext to create an InstanceVariable AST node.
        /// </summary>
        /// <param name="context">The ANTLR InstanceDeclContext.</param>
        /// <returns>A new InstanceVariable instance.</returns>
        public static InstanceVariable Parse(InstanceDeclContext context)
        {
            var type = Type.Parse(context.typeT());
            var names = context.USER_VARIABLE().Select(v => v.GetText()).ToList();
            return new InstanceVariable(type, names);
        }
    }
} 