using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using AppRefiner.Database;
using AppRefiner.Services;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Ast
{
    /// <summary>
    /// Represents an Interface definition in PeopleCode.
    /// </summary>
    public class Interface
    {
        /// <summary>
        /// Gets the name of the interface.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the full path of the interface (e.g., PKG:SubPKG:InterfaceName).
        /// </summary>
        public string FullPath { get; private set; }

        /// <summary>
        /// Gets the interface this interface extends, if any.
        /// </summary>
        public Interface? ExtendedInterface { get; private set; }

        /// <summary>
        /// Gets the list of methods declared in this interface.
        /// </summary>
        public List<Method> Methods { get; private set; }

        /// <summary>
        /// Gets the list of properties declared in this interface.
        /// </summary>
        public List<Property> Properties { get; private set; }

        // Private constructor to force use of Parse
        private Interface(string fullPath)
        {
            FullPath = fullPath;
            Name = fullPath.Split(':').LastOrDefault() ?? fullPath; // Basic name extraction
            Methods = new List<Method>();
            Properties = new List<Property>();
        }

        /// <summary>
        /// Parses an InterfaceProgramContext or InterfaceDeclarationContext to create an Interface instance.
        /// This requires a resolver function to handle recursive parsing of extended interfaces.
        /// </summary>
        /// <param name="context">The ANTLR context for the interface program or declaration.</param>
        /// <param name="fullPath">The full path of the interface being parsed.</param>
        /// <param name="dataManager">The data manager for fetching extended interface source.</param>
        /// <param name="parseResolver">A function that can take source code and return a parsed Interface object.</param>
        /// <returns>A new Interface instance.</returns>
        public static Interface Parse(
            InterfaceProgramContext progContext, // Changed context type
            string fullPath, 
            IDataManager dataManager)
        {
            InterfaceDeclarationContext declarationContext = progContext.interfaceDeclaration();
            ClassHeaderContext? headerContext = null;

            var astInterface = new Interface(fullPath); // astInterface.Name is set here

            // Handle extension and extract header
            string? extendedInterfacePath = null;
            if (declarationContext is InterfaceDeclarationExtensionContext extContext)
            {
                var superClassCtx = extContext.superclass();
                if (superClassCtx is AppClassSuperClassContext appClassSuperCtx)
                { 
                    extendedInterfacePath = appClassSuperCtx.appClassPath().GetText();
                }
                 headerContext = extContext.classHeader();
            }
             else if (declarationContext is InterfaceDeclarationPlainContext plainContext)
            {
                 headerContext = plainContext.classHeader();
            }
             else
            {
                throw new ArgumentException("Unknown interface declaration context type.", nameof(declarationContext));
            }

            if (!string.IsNullOrEmpty(extendedInterfacePath))
            {
                astInterface.ExtendedInterface = new AstService(dataManager).GetInterfaceAst(extendedInterfacePath);
            }

            // Parse methods and properties from the extracted header
            if (headerContext != null)
            {
                 // Pass astInterface.Name to the header parsing method
                 // Interfaces only have Public scope directly defined in header
                ParseHeader(headerContext.publicHeader()?.nonPrivateHeader(), Scope.Public, astInterface, dataManager, astInterface.Name);
                // Although grammar allows protected/private headers, they shouldn't contain methods/props for a valid interface
                ParseHeader(headerContext.protectedHeader()?.nonPrivateHeader(), Scope.Protected, astInterface, dataManager, astInterface.Name);
            }
            
            return astInterface;
        }

        private static void ParseHeader(NonPrivateHeaderContext? headerContext, Scope scope, Interface targetInterface, IDataManager dataManager, string interfaceName)
        {
            if (headerContext == null) return;

            foreach (var member in headerContext.nonPrivateMember())
            {
                if (member is NonPrivateMethodHeaderContext methodMember)
                {
                    targetInterface.Methods.Add(Method.Parse(methodMember.methodHeader(), scope, interfaceName, dataManager));
                }
                else if (member is NonPrivatePropertyContext propertyMember)
                {
                    targetInterface.Properties.Add(Property.Parse(propertyMember.propertyDeclaration(), scope, dataManager));
                }
            }
        }

        /// <summary>
        /// Recursively gathers all methods and properties from this interface and its extended interfaces.
        /// Filters out members whose signatures are already present in the provided set.
        /// Interface members are implicitly abstract.
        /// </summary>
        /// <param name="alreadyImplementedSignatures">A set of signatures (e.g., "M:Name(ParamCount)", "P:Name") that should be excluded.</param>
        /// <returns>A tuple containing dictionaries of methods and properties, keyed by their signature.</returns>
        public (Dictionary<string, Method> InterfaceMethods, Dictionary<string, Property> InterfaceProperties) GetAllInterfaceMembersRecursive(HashSet<string> alreadyImplementedSignatures)
        {
            var interfaceMethods = new Dictionary<string, Method>();
            var interfaceProperties = new Dictionary<string, Property>();

            // 1. Get members defined directly in *this* interface
            foreach (var method in this.Methods) // All interface methods are implicitly abstract
            {
                string signature = $"M:{method.Name}({method.Parameters.Count})";
                if (!alreadyImplementedSignatures.Contains(signature))
                {
                    interfaceMethods.TryAdd(signature, method);
                }
            }
            foreach (var prop in this.Properties) // All interface properties are implicitly abstract
            {
                 string signature = $"P:{prop.Name}";
                 if (!alreadyImplementedSignatures.Contains(signature))
                 {
                    interfaceProperties.TryAdd(signature, prop);
                 }
            }

            // 2. Recurse up to the extended interface
            if (this.ExtendedInterface != null)
            {
                 // Pass the *same* set of already implemented signatures up the chain.
                 var (parentMethods, parentProperties) = this.ExtendedInterface.GetAllInterfaceMembersRecursive(alreadyImplementedSignatures);
                foreach(var kvp in parentMethods)
                {
                    interfaceMethods.TryAdd(kvp.Key, kvp.Value); // Add parent's if not already present
                }
                 foreach(var kvp in parentProperties)
                {
                    interfaceProperties.TryAdd(kvp.Key, kvp.Value);
                }
            }

            return (interfaceMethods, interfaceProperties);
        }
    }
} 