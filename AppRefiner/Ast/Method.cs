using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using AppRefiner.Database;
using AppRefiner.Database.Models;

namespace AppRefiner.Ast
{
    /// <summary>
    /// Represents a method definition within a PeopleCode Application Class or Interface.
    /// </summary>
    public class Method
    {
        /// <summary>
        /// Gets the name of the method.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the list of parameters for the method.
        /// </summary>
        public List<Parameter> Parameters { get; private set; } = new List<Parameter>();

        /// <summary>
        /// Gets the structured return type of the method. Null if the method returns nothing.
        /// </summary>
        public Type? ReturnType { get; private set; }

        /// <summary>
        /// Gets the scope (visibility) of the method.
        /// </summary>
        public Scope Scope { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the method is declared as abstract.
        /// </summary>
        public bool IsAbstract { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this method is the class constructor.
        /// </summary>
        public bool IsConstructor { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this method overrides a method from a base class.
        /// </summary>
        public bool OverridesBaseMethod { get; set; }

        /// <summary>
        /// Gets the full name of the declaring type.
        /// </summary>
        public string DeclaringTypeFullName { get; set; } = string.Empty;

        // Private constructor to force use of Parse
        private Method() 
        {
            Name = string.Empty;
            Parameters = new List<Parameter>();
            // Default IsConstructor to false
        }

        /// <summary>
        /// Parses a MethodHeaderContext to create a Method instance.
        /// </summary>
        /// <param name="context">The ANTLR context for the method header.</param>
        /// <param name="scope">The scope determined by the containing class section.</param>
        /// <param name="parentClassNameOrInterfaceName">The name of the containing class or interface.</param>
        /// <param name="dataManager">The data manager for potential type lookups (currently unused).</param>
        /// <returns>A new Method instance.</returns>
        public static Method Parse(MethodHeaderContext context, Scope scope, string parentClassNameOrInterfaceName)
        {
            var methodName = context.genericID().GetText();
            var method = new Method
            {
                Name = methodName,
                Scope = scope,
                IsAbstract = context.ABSTRACT() != null,
                // Check if method name matches the parent class/interface name
                IsConstructor = !string.IsNullOrEmpty(parentClassNameOrInterfaceName) && methodName.Equals(parentClassNameOrInterfaceName, StringComparison.OrdinalIgnoreCase),
                OverridesBaseMethod = false // Initialize to false
            };

            var argsContext = context.methodArguments();
            if (argsContext != null)
            {
                method.Parameters.AddRange(argsContext.methodArgument().Select(ParseParameter));
            }

            var returnTypeContext = context.typeT();
            if (returnTypeContext != null)
            {
                method.ReturnType = Type.Parse(returnTypeContext);
            }

            return method;
        }

        private static Parameter ParseParameter(MethodArgumentContext context)
        {
            var name = context.USER_VARIABLE().GetText();
            var type = Type.Parse(context.typeT());
            // Pass the Type object to the constructor
            // Check if the OUT keyword is present
            bool isOut = context.OUT() != null;
            return new Parameter(name, type, isOut);
        }
    }
} 