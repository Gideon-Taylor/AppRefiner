using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;

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
    public class GoToDefinitionVisitor : ScopedAstVisitor<GoToDefinitionType>
    {
        /// <summary>
        /// Collection of all definitions found in the code
        /// </summary>
        public List<GoToCodeDefinition> Definitions { get; } = new List<GoToCodeDefinition>();

        /// <summary>
        /// Dictionary to map property names to their types from the header declarations
        /// </summary>
        private Dictionary<string, string> propertyTypes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Current PeopleCode scope (public, protected, private)
        /// </summary>
        private GoToDefinitionScope currentPeopleCodeScope = GoToDefinitionScope.Private; // Default to private

        /// <summary>
        /// Process methods to capture scope information
        /// </summary>
        public override void VisitMethod(MethodNode node)
        {
            // Extract method information
            var methodName = node.Name;
            var returnType = GetReturnTypeString(node.ReturnType);

            Definitions.Add(new GoToCodeDefinition(
                methodName,
                returnType,
                GoToDefinitionType.Method,
                currentPeopleCodeScope,
                node.SourceSpan.Start.ByteIndex,
                node.SourceSpan.Start.Line
            ));

            // Call base to continue traversal
            base.VisitMethod(node);
        }

        /// <summary>
        /// Process properties to capture scope information
        /// </summary>
        public override void VisitProperty(PropertyNode node)
        {
            // Extract property information
            var propertyName = node.Name;
            var propertyType = GetTypeString(node.Type);

            // Store the property type for getter/setter lookup
            propertyTypes[propertyName] = propertyType;

            Definitions.Add(new GoToCodeDefinition(
                propertyName,
                propertyType,
                GoToDefinitionType.Property,
                currentPeopleCodeScope,
                node.SourceSpan.Start.ByteIndex,
                node.SourceSpan.Start.Line
            ));

            // Call base to continue traversal
            base.VisitProperty(node);
        }


        /// <summary>
        /// Visit Function definitions
        /// </summary>
        public override void VisitFunction(FunctionNode node)
        {
            var functionName = node.Name;
            var returnType = GetReturnTypeString(node.ReturnType);

            Definitions.Add(new GoToCodeDefinition(
                functionName,
                returnType,
                GoToDefinitionType.Function,
                GoToDefinitionScope.Global, // Functions are always global scope
                node.SourceSpan.Start.ByteIndex,
                node.SourceSpan.Start.Line
            ));

            // Call base to continue traversal
            base.VisitFunction(node);
        }

        /// <summary>
        /// Visit instance variable declarations
        /// </summary>
        public override void VisitVariable(VariableNode node)
        {
            // Only process instance variables (private scope)
            if (node.Scope == VariableScope.Instance)
            {
                var variableType = GetTypeString(node.Type);

                // A variable declaration can define multiple variables
                foreach (var nameInfo in node.NameInfos)
                {
                    var variableName = nameInfo.Name;

                    // Store the variable type for potential future lookups
                    propertyTypes[variableName] = variableType;

                    Definitions.Add(new GoToCodeDefinition(
                        variableName,
                        variableType,
                        GoToDefinitionType.Instance,
                        GoToDefinitionScope.Private, // Instance variables are always private
                        nameInfo.SourceSpan.Start.ByteIndex,
                        nameInfo.SourceSpan.Start.Line
                    ));
                }
            }

            // Call base to continue traversal
            base.VisitVariable(node);
        }

        /// <summary>
        /// Helper method to extract type information from type node
        /// </summary>
        private string GetTypeString(TypeNode? typeNode)
        {
            if (typeNode == null)
                return "any";

            if (typeNode is ArrayTypeNode arrayType)
            {
                var baseType = arrayType.ElementType != null
                    ? GetTypeString(arrayType.ElementType)
                    : "any";
                return $"Array of {baseType}";
            }
            else if (typeNode is BuiltInTypeNode builtInType)
            {
                return builtInType.Type.ToString();
            }
            else if (typeNode is AppClassTypeNode appClassType)
            {
                return appClassType.TypeName;
            }

            return "any";
        }

        /// <summary>
        /// Helper method to extract return type information
        /// </summary>
        private string GetReturnTypeString(TypeNode? returnType)
        {
            if (returnType == null)
                return "void";

            return GetTypeString(returnType);
        }

        /// <summary>
        /// Override OnEnterGlobalScope to initialize scope tracking
        /// </summary>
        protected override void OnEnterGlobalScope(ScopeContext scope, ProgramNode node)
        {
            // Reset scope to private at the start
            currentPeopleCodeScope = GoToDefinitionScope.Private;

            // Call base
            base.OnEnterGlobalScope(scope, node);
        }

        /// <summary>
        /// Override OnEnterClassScope to handle class-level scope changes
        /// For now, we'll use a simplified approach where class members default to private
        /// A more sophisticated implementation would parse scope modifiers from tokens
        /// </summary>
        protected override void OnEnterClassScope(ScopeContext scope, AppClassNode node)
        {
            // Reset scope for each class (PeopleCode classes start with private by default)
            currentPeopleCodeScope = GoToDefinitionScope.Private;

            // Call base
            base.OnEnterClassScope(scope, node);
        }
    }
}