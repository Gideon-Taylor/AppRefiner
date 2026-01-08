using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;

namespace AppRefiner.LanguageExtensions
{
    /// <summary>
    /// Contains information about how a language extension is being used in code
    /// </summary>
    public class ExtensionUsageInfo
    {
        /// <summary>
        /// The type of usage detected
        /// </summary>
        public ExtensionUsageType UsageType { get; set; }

        // Properties for VariableAssignment usage
        /// <summary>
        /// The name of the variable being assigned to (for VariableAssignment usage)
        /// </summary>
        public string? VariableName { get; set; }

        /// <summary>
        /// The type of the variable being assigned to (for VariableAssignment usage)
        /// </summary>
        public TypeInfo? VariableType { get; set; }

        /// <summary>
        /// The assignment AST node (for VariableAssignment usage)
        /// </summary>
        public AssignmentNode? AssignmentNode { get; set; }

        // Properties for VariableDeclaration usage
        /// <summary>
        /// The name of the variable being declared (for VariableDeclaration usage)
        /// </summary>
        public string? DeclaredVariableName { get; set; }

        /// <summary>
        /// The type of the variable being declared (for VariableDeclaration usage)
        /// </summary>
        public TypeInfo? DeclaredVariableType { get; set; }

        /// <summary>
        /// The variable declaration AST node (for VariableDeclaration usage)
        /// </summary>
        public LocalVariableDeclarationWithAssignmentNode? DeclarationNode { get; set; }

        // Properties for FunctionParameter usage
        /// <summary>
        /// The zero-based index of the parameter (for FunctionParameter usage)
        /// </summary>
        public int? ParameterIndex { get; set; }

        /// <summary>
        /// The function call AST node (for FunctionParameter usage)
        /// </summary>
        public FunctionCallNode? FunctionCallNode { get; set; }

        /// <summary>
        /// The name of the function being called (for FunctionParameter usage)
        /// </summary>
        public string? FunctionName { get; set; }
    }
}
