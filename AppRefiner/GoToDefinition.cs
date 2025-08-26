using Antlr4.Runtime.Tree;
using AppRefiner.PeopleCode; // For PeopleCodeParser types
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppRefiner
{
    /// <summary>
    /// Enumeration of code definition types
    /// </summary>
    public enum GoToDefinitionType
    {
        Method,
        Property,
        Function,
        Getter,
        Setter,
        Instance
    }

    /// <summary>
    /// Enumeration of code definition scopes
    /// </summary>
    public enum GoToDefinitionScope
    {
        Public,
        Protected,
        Private,
        Global  // For functions outside of classes
    }

    /// <summary>
    /// Represents a code definition (method, property, function, etc.) that can be navigated to
    /// </summary>
    public class GoToCodeDefinition
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
        public GoToDefinitionType DefinitionType { get; set; }

        /// <summary>
        /// The scope of the definition (public, protected, private)
        /// </summary>
        public GoToDefinitionScope Scope { get; set; }

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
        public string DisplayText => $"{Name}: {Type}";

        /// <summary>
        /// Gets a description of the definition for tooltip or additional information
        /// </summary>
        public string Description => $"{GetScopePrefix()}{DefinitionType} {Name} of type {Type} at line {Line}";

        /// <summary>
        /// Creates a new code definition with the provided parameters
        /// </summary>
        public GoToCodeDefinition(string name, string type, GoToDefinitionType definitionType, GoToDefinitionScope scope, int position, int line)
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
                GoToDefinitionScope.Public => "[Public] ",
                GoToDefinitionScope.Protected => "[Protected] ",
                GoToDefinitionScope.Private => "[Private] ",
                GoToDefinitionScope.Global => "[Global] ",
                _ => string.Empty
            };
        }
    }

    /// <summary>
    /// A visitor implementation that collects code definitions for navigation purposes
    /// </summary>
    public class GoToDefinitionVisitor : PeopleCodeParserBaseListener
    {
        /// <summary>
        /// Collection of all definitions found in the code
        /// </summary>
        public List<GoToCodeDefinition> Definitions { get; } = new List<GoToCodeDefinition>();

        /// <summary>
        /// Tracks the current scope during traversal
        /// </summary>
        private GoToDefinitionScope currentScope = GoToDefinitionScope.Public;

        /// <summary>
        /// Dictionary to map method/property names to their scope from the header declarations
        /// </summary>
        private Dictionary<string, GoToDefinitionScope> memberScopes = new Dictionary<string, GoToDefinitionScope>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Dictionary to map property names to their types from the header declarations
        /// </summary>
        private Dictionary<string, string> propertyTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Process method headers to capture scope information
        /// </summary>
        
        public override void EnterMethodHeader(PeopleCode.PeopleCodeParser.MethodHeaderContext context)
        {
            if (context.genericID() != null)
            {
                var methodName = context.genericID().GetText();
                // Store the method name with its scope from the header
                memberScopes[methodName] = currentScope;
            }
        }

        /// <summary>
        /// Process property declarations in headers to capture scope information
        /// </summary>
        public override void EnterPropertyGetSet(PeopleCode.PeopleCodeParser.PropertyGetSetContext context)
        {
            var propertyName = context.genericID().GetText();
            var propertyType = GetTypeString(context.typeT());
            
            // Store the property name with its scope and type from the header
            memberScopes[propertyName] = currentScope;
            propertyTypes[propertyName] = propertyType;

            Definitions.Add(new GoToCodeDefinition(
                propertyName,
                propertyType,
                GoToDefinitionType.Property,
                currentScope,
                context.Start.ByteStartIndex(),
                context.Start.Line
            ));
        }

        /// <summary>
        /// Process property declarations in headers to capture scope information
        /// </summary>
        public override void EnterPropertyDirect(PeopleCode.PeopleCodeParser.PropertyDirectContext context)
        {
            var propertyName = context.genericID().GetText();
            var propertyType = GetTypeString(context.typeT());
            
            // Store the property name with its scope and type from the header
            memberScopes[propertyName] = currentScope;
            propertyTypes[propertyName] = propertyType;

            Definitions.Add(new GoToCodeDefinition(
                propertyName,
                propertyType,
                GoToDefinitionType.Property,
                currentScope,
                context.Start.ByteStartIndex(),
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Method implementations
        /// </summary>
        /// <param name="context">The method context</param>
        public override void EnterMethod(PeopleCode.PeopleCodeParser.MethodContext context)
        {
            var methodName = context.genericID().GetText();
            var returnType = "void"; // Default to void

            // Look up the method's scope from its header declaration
            var methodScope = GoToDefinitionScope.Public; // Default to public
            if (memberScopes.TryGetValue(methodName, out var scope))
            {
                methodScope = scope;
            }

            Definitions.Add(new GoToCodeDefinition(
                methodName,
                returnType,
                GoToDefinitionType.Method,
                methodScope,
                context.Start.ByteStartIndex(),
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Property getter implementations
        /// </summary>
        /// <param name="context">The getter context</param>
        public override void EnterGetter(PeopleCode.PeopleCodeParser.GetterContext context)
        {
            var propertyName = context.genericID().GetText();
            
            // Look up the property's scope from its header declaration
            var propertyScope = GoToDefinitionScope.Public; // Default to public
            if (memberScopes.TryGetValue(propertyName, out var scope))
            {
                propertyScope = scope;
            }
            
            // Look up the property's type from its header declaration
            var propertyType = "any"; // Default to any
            if (propertyTypes.TryGetValue(propertyName, out var type))
            {
                propertyType = type;
            }

            Definitions.Add(new GoToCodeDefinition(
                propertyName,
                propertyType,
                GoToDefinitionType.Getter,
                propertyScope,
                context.Start.ByteStartIndex(),
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Property setter implementations
        /// </summary>
        /// <param name="context">The setter context</param>
        public override void EnterSetter(PeopleCode.PeopleCodeParser.SetterContext context)
        {
            var propertyName = context.genericID().GetText();
            
            // Look up the property's scope from its header declaration
            var propertyScope = GoToDefinitionScope.Public; // Default to public
            if (memberScopes.TryGetValue(propertyName, out var scope))
            {
                propertyScope = scope;
            }
            
            // Look up the property's type from its header declaration
            var propertyType = "any"; // Default to any
            if (propertyTypes.TryGetValue(propertyName, out var type))
            {
                propertyType = type;
            }

            Definitions.Add(new GoToCodeDefinition(
                propertyName,
                propertyType,
                GoToDefinitionType.Setter,
                propertyScope,
                context.Start.ByteStartIndex(),
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Function definitions
        /// </summary>
        /// <param name="context">The function definition context</param>
        public override void EnterFunctionDefinition(PeopleCode.PeopleCodeParser.FunctionDefinitionContext context)
        {
            var functionName = context.allowableFunctionName().GetText();
            var returnType = "void"; // Default to void

            // Check if there's a return type specified
            if (context.typeT() != null)
            {
                returnType = GetTypeString(context.typeT());
            }

            Definitions.Add(new GoToCodeDefinition(
                functionName,
                returnType,
                GoToDefinitionType.Function,
                GoToDefinitionScope.Global, // Functions are always global scope
                context.Start.ByteStartIndex(),
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit instance declarations (private variables)
        /// </summary>
        public override void EnterInstanceDecl(PeopleCode.PeopleCodeParser.InstanceDeclContext context)
        {
            var propertyType = GetTypeString(context.typeT());

            // An instance declaration can define multiple variables
            foreach (var varNode in context.USER_VARIABLE())
            {
                var variableName = varNode.GetText();
                
                // Store the variable type, but mark as Instance type rather than Property
                propertyTypes[variableName] = propertyType;

                Definitions.Add(new GoToCodeDefinition(
                    variableName,
                    propertyType,
                    GoToDefinitionType.Instance, // Use Instance instead of Property
                    GoToDefinitionScope.Private, // Instance variables are always private
                    varNode.Symbol.ByteStartIndex(),
                    varNode.Symbol.Line
                ));
            }
        }

        /// <summary>
        /// Enter public header section
        /// </summary>
        public override void EnterPublicHeader(PeopleCode.PeopleCodeParser.PublicHeaderContext context)
        {
            currentScope = GoToDefinitionScope.Public;
        }

        /// <summary>
        /// Enter protected header section
        /// </summary>
        public override void EnterProtectedHeader(PeopleCode.PeopleCodeParser.ProtectedHeaderContext context)
        {
            currentScope = GoToDefinitionScope.Protected;
        }

        /// <summary>
        /// Enter private header section
        /// </summary>
        public override void EnterPrivateHeader(PeopleCode.PeopleCodeParser.PrivateHeaderContext context)
        {
            currentScope = GoToDefinitionScope.Private;
        }

        /// <summary>
        /// Helper method to extract type information from type context
        /// </summary>
        private string GetTypeString(PeopleCode.PeopleCodeParser.TypeTContext typeContext)
        {
            if (typeContext == null)
                return "any";

            if (typeContext is PeopleCode.PeopleCodeParser.ArrayTypeContext arrayType)
            {
                var baseType = arrayType.typeT() != null
                    ? GetTypeString(arrayType.typeT())
                    : "any";
                return $"Array of {baseType}";
            }
            else if (typeContext is PeopleCode.PeopleCodeParser.BaseExceptionTypeContext)
            {
                return "Exception";
            }
            else if (typeContext is PeopleCode.PeopleCodeParser.AppClassTypeContext appClass)
            {
                return appClass.appClassPath().GetText();
            }
            else if (typeContext is PeopleCode.PeopleCodeParser.SimpleTypeTypeContext simpleType)
            {
                return simpleType.simpleType().GetText();
            }

            return typeContext.GetText();
        }
    }
} 