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
    /// Represents an Application Class definition in PeopleCode.
    /// </summary>
    public class AppClass
    {
        /// <summary>
        /// Gets the name of the class.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the full path of the class (e.g., PKG:SubPKG:ClassName).
        /// </summary>
        public string FullPath { get; private set; }

        /// <summary>
        /// Gets the class this class extends, if any.
        /// </summary>
        public AppClass? ExtendedClass { get; private set; }

        /// <summary>
        /// Gets the interface this class implements, if any.
        /// </summary>
        public Interface? ImplementedInterface { get; private set; }

        /// <summary>
        /// Gets the list of methods declared in this class (across all scopes).
        /// </summary>
        public List<Method> Methods { get; private set; }

        /// <summary>
        /// Gets the list of properties declared in this class (across all scopes).
        /// </summary>
        public List<Property> Properties { get; private set; }

        /// <summary>
        /// Gets the list of private instance variables declared in this class.
        /// </summary>
        public List<InstanceVariable> InstanceVariables { get; private set; }

        /// <summary>
        /// Gets the list of private constants declared in this class.
        /// </summary>
        public List<Constant> Constants { get; private set; }

        // TODO: Add storage for private instance variables and constants if needed later.

        // Private constructor to force use of Parse
        private AppClass(string fullPath)
        {
            FullPath = fullPath;
            Name = fullPath.Split(':').LastOrDefault() ?? fullPath; // Basic name extraction
            Methods = new List<Method>();
            Properties = new List<Property>();
            InstanceVariables = new List<InstanceVariable>(); // Initialize list
            Constants = new List<Constant>();           // Initialize list
        }

        /// <summary>
        /// Parses an AppClassProgramContext to create an AppClass instance.
        /// Requires resolver functions to handle recursive parsing of extended classes and implemented interfaces.
        /// </summary>
        /// <param name="progContext">The ANTLR context for the application class program.</param>
        /// <param name="fullPath">The full path of the class being parsed.</param>
        /// <param name="dataManager">The data manager for fetching parent/interface source.</param>
        /// <param name="appClassParseResolver">A function that can take an AppClass path and return a parsed AppClass object.</param>
        /// <param name="interfaceParseResolver">A function that can take an Interface path and return a parsed Interface object.</param>
        /// <returns>A new AppClass instance.</returns>
        public static AppClass Parse(
            AppClassProgramContext progContext,
            string fullPath, 
            IDataManager? dataManager)
        {
            // AppClassProgram only contains one ClassDeclaration
            ClassDeclarationContext declarationContext = progContext.classDeclaration();
            ClassHeaderContext? headerContext = null;
            
            var astClass = new AppClass(fullPath);

            // Handle extension and extract header
            string? extendedClassPath = null;
            if (declarationContext is ClassDeclarationExtensionContext extContext)
            {
                 var superClassCtx = extContext.superclass();
                 if (superClassCtx is AppClassSuperClassContext appClassSuperCtx)
                 {
                     extendedClassPath = appClassSuperCtx.appClassPath().GetText();
                 }
                 // TODO: Handle ExceptionSuperClass, SimpleTypeSuperclass if needed
                 headerContext = extContext.classHeader();
            }
            else if (declarationContext is ClassDeclarationImplementationContext implContext)
            {
                 // Handle implementation first for this type
                 string? implementedInterfacePath = implContext.appClassPath().GetText();
                 if (!string.IsNullOrEmpty(implementedInterfacePath))
                 {
                    astClass.ImplementedInterface = new AstService(dataManager).GetInterfaceAst(implementedInterfacePath);
                 }
                 headerContext = implContext.classHeader();
            }
            else if (declarationContext is ClassDeclarationPlainContext plainContext)
            {
                headerContext = plainContext.classHeader();
            }
            else
            {
                // Should not happen if grammar is correct and complete
                throw new ArgumentException("Unknown class declaration context type.", nameof(declarationContext));
            }

            // Parse extension if found (moved after header extraction)
            if (!string.IsNullOrEmpty(extendedClassPath))
            {
                astClass.ExtendedClass = new AstService(dataManager).GetAppClassAst(extendedClassPath);
            }

            // Parse implementation if found (moved after header extraction, handled partially above)
            if (declarationContext is ClassDeclarationImplementationContext implContextForHeader && astClass.ImplementedInterface == null)
            {
                 string? implementedInterfacePath = implContextForHeader.appClassPath().GetText();
                 if (!string.IsNullOrEmpty(implementedInterfacePath))
                 {
                    astClass.ImplementedInterface = new AstService(dataManager).GetInterfaceAst(implementedInterfacePath);
                 } 
            }
            
            // Parse methods and properties from the extracted header
            if(headerContext != null)
            {
                ParseNonPrivateHeader(headerContext.publicHeader()?.nonPrivateHeader(), Scope.Public, astClass, dataManager, astClass.Name);
                ParseNonPrivateHeader(headerContext.protectedHeader()?.nonPrivateHeader(), Scope.Protected, astClass, dataManager, astClass.Name);
                ParsePrivateHeader(headerContext.privateHeader(), Scope.Private, astClass, astClass.Name);
            }

            return astClass;
        }

        private static void ParseNonPrivateHeader(NonPrivateHeaderContext? headerContext, Scope scope, AppClass targetClass, IDataManager? dataManager, string className)
        {
            if (headerContext == null) return;

            foreach (var member in headerContext.nonPrivateMember())
            {
                if (member is NonPrivateMethodHeaderContext methodMember)
                {
                    var method = Method.Parse(methodMember.methodHeader(), scope, className);

                    /* check if method name exists in any parent class or interface */
                    if (targetClass.ExtendedClass != null)
                    {
                        var parent = targetClass.ExtendedClass;
                        while (parent != null)
                        {
                            if (parent.Methods.Any(m => m.Name == method.Name))
                            {
                                method.OverridesBaseMethod = true;
                            }
                            parent = parent.ExtendedClass;
                        }
                    }

                    if (targetClass.ImplementedInterface != null)
                    {
                        var parent = targetClass.ImplementedInterface;
                        while (parent != null)
                        {
                            if (parent.Methods.Any(m => m.Name == method.Name))
                            {
                                method.OverridesBaseMethod = true;
                            }
                            parent = parent.ExtendedInterface;
                        }
                    }


                    targetClass.Methods.Add(method);
                }
                else if (member is NonPrivatePropertyContext propertyMember)
                {
                    targetClass.Properties.Add(Property.Parse(propertyMember.propertyDeclaration(), scope));
                }
            }
        }

         private static void ParsePrivateHeader(PrivateHeaderContext? headerContext, Scope scope, AppClass targetClass, string className)
        {
            if (headerContext == null) return;

            foreach (var member in headerContext.privateMember())
            {
                if (member is PrivateMethodHeaderContext methodMember)
                {
                    targetClass.Methods.Add(Method.Parse(methodMember.methodHeader(), scope, className));
                }
                else if (member is PrivatePropertyContext propertyMember)
                {
                    // PrivatePropertyContext contains InstanceDeclarationContext
                    var instanceDecl = propertyMember.instanceDeclaration();
                    if (instanceDecl is InstanceDeclContext instanceDeclContext)
                    {
                        // Handle instance variable declaration
                        targetClass.InstanceVariables.Add(InstanceVariable.Parse(instanceDeclContext));
                    }
                    // Ignore EmptyInstanceDeclContext as it's meaningless
                }
                else if (member is PrivateConstantContext constantMember)
                {
                     // PrivateConstantContext contains ConstantDeclarationContext
                     var constDecl = constantMember.constantDeclaration();
                     targetClass.Constants.Add(Constant.Parse(constDecl));
                }
            }
        }

        /// <summary>
        /// Recursively gathers all abstract methods and properties from this class and its ancestors
        /// that have not been implemented by this class or its intermediate ancestors.
        /// </summary>
        /// <returns>A tuple containing lists of unimplemented abstract methods and properties.</returns>
        public (List<Method> UnimplementedMethods, List<Property> UnimplementedProperties) GetAllUnimplementedAbstractMembers()
        {
            var inheritedAbstractMethods = new Dictionary<string, Method>(); // Use signature for key?
            var inheritedAbstractProperties = new Dictionary<string, Property>();
            var implementedSignatures = new HashSet<string>();

            // Get members implemented in *this* class to filter later
            // Use a simplified signature (Name+ParamCount) for now. Might need more robust matching.
            foreach (var method in this.Methods.Where(m => !m.IsConstructor && !m.IsAbstract)) // Concrete methods
            {
                implementedSignatures.Add($"M:{method.Name}({method.Parameters.Count})");
            }
            foreach (var prop in this.Properties.Where(p => !p.IsAbstract)) // Concrete properties
            {
                implementedSignatures.Add($"P:{prop.Name}");
            }

            // Recursive call to gather from parent (Internal method handles setting DeclaringTypeFullName for parents)
            if (this.ExtendedClass != null)
            {
                var (parentMethods, parentProperties) = this.ExtendedClass.GetAllUnimplementedAbstractMembersInternal(implementedSignatures, this.ExtendedClass.FullPath);
                foreach(var kvp in parentMethods)
                {
                    inheritedAbstractMethods.TryAdd(kvp.Key, kvp.Value);
                }
                foreach(var kvp in parentProperties)
                {
                    inheritedAbstractProperties.TryAdd(kvp.Key, kvp.Value);
                }
            }
            
            // Gather members from the implemented interface (if any)
            if (this.ImplementedInterface != null)
            {
                 // Assume/create a method GetInterfaceMembersRecursive on Interface.
                 // Pass implementedSignatures to avoid adding already implemented members.
                 var (interfaceMethods, interfaceProperties) = this.ImplementedInterface.GetAllInterfaceMembersRecursive(implementedSignatures); 
                 
                 foreach(var kvp in interfaceMethods)
                 {
                    // Add interface method if not already implemented by the class or its base class
                    if (!inheritedAbstractMethods.ContainsKey(kvp.Key)) 
                    {
                       // Set the declaring type before adding
                       kvp.Value.DeclaringTypeFullName = this.ImplementedInterface.FullPath; 
                       inheritedAbstractMethods.TryAdd(kvp.Key, kvp.Value);
                    }
                 }
                 foreach(var kvp in interfaceProperties)
                 {
                    // Add interface property if not already implemented
                     if (!inheritedAbstractProperties.ContainsKey(kvp.Key)) 
                    {
                        // Set the declaring type before adding
                        kvp.Value.DeclaringTypeFullName = this.ImplementedInterface.FullPath; 
                        inheritedAbstractProperties.TryAdd(kvp.Key, kvp.Value);
                    }
                 }
            }

            return (inheritedAbstractMethods.Values.ToList(), inheritedAbstractProperties.Values.ToList());
        }

        /// <summary>
        /// Internal helper for recursive gathering. Tracks already implemented signatures.
        /// </summary>
        /// <param name="alreadyImplementedSignatures">Set of signatures already implemented down the chain.</param>
        /// <param name="currentTypeFullPath">The full path of the type being processed in this step of the recursion.</param>
        private (Dictionary<string, Method> AbstractMethods, Dictionary<string, Property> AbstractProperties) GetAllUnimplementedAbstractMembersInternal(HashSet<string> alreadyImplementedSignatures, string currentTypeFullPath)
        {
            var abstractMethods = new Dictionary<string, Method>();
            var abstractProperties = new Dictionary<string, Property>();

            // 1. Get abstract members defined directly in *this* class
            foreach (var method in this.Methods.Where(m => m.IsAbstract))
            {
                string signature = $"M:{method.Name}({method.Parameters.Count})";
                if (!alreadyImplementedSignatures.Contains(signature))
                {
                    method.DeclaringTypeFullName = currentTypeFullPath; // Set declaring type
                    abstractMethods.TryAdd(signature, method);
                }
            }
            foreach (var prop in this.Properties.Where(p => p.IsAbstract))
            {
                 string signature = $"P:{prop.Name}";
                 if (!alreadyImplementedSignatures.Contains(signature))
                 {
                    prop.DeclaringTypeFullName = currentTypeFullPath; // Set declaring type
                    abstractProperties.TryAdd(signature, prop);
                 }
            }

            // 2. Get concrete members implemented in *this* class (to prevent parent's abstract ones from propagating)
            foreach (var method in this.Methods.Where(m => !m.IsConstructor && !m.IsAbstract))
            {
                alreadyImplementedSignatures.Add($"M:{method.Name}({method.Parameters.Count})");
            }
            foreach (var prop in this.Properties.Where(p => !p.IsAbstract))
            {
                alreadyImplementedSignatures.Add($"P:{prop.Name}");
            }

            // 3. Recurse up to parent
            if (this.ExtendedClass != null)
            {
                 // Pass the parent's full path for the next level
                 var (parentMethods, parentProperties) = this.ExtendedClass.GetAllUnimplementedAbstractMembersInternal(alreadyImplementedSignatures, this.ExtendedClass.FullPath);
                foreach(var kvp in parentMethods)
                {
                    abstractMethods.TryAdd(kvp.Key, kvp.Value); // Add parent's abstract if not already added/implemented
                }
                 foreach(var kvp in parentProperties)
                {
                    abstractProperties.TryAdd(kvp.Key, kvp.Value);
                }
            }
            // Interface members are handled in the public method now, not needed here.

            return (abstractMethods, abstractProperties);
        }
    }
} 