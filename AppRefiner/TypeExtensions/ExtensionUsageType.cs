namespace AppRefiner.LanguageExtensions
{
    /// <summary>
    /// Describes how a language extension is being used in code
    /// </summary>
    public enum ExtensionUsageType
    {
        /// <summary>
        /// Extension used as a standalone statement
        /// Example: &array.ForEach(&item);
        /// </summary>
        Statement,

        /// <summary>
        /// Extension used on the right-hand side of a variable assignment
        /// Example: &result = &string.ToUpper;
        /// </summary>
        VariableAssignment,

        /// <summary>
        /// Extension used in a variable declaration with initialization
        /// Example: Local string &upper = &string.ToUpper;
        /// </summary>
        VariableDeclaration,

        /// <summary>
        /// Extension used as a function call parameter
        /// Example: MessageBox(0, "", 0, 0, &string.ToUpper);
        /// </summary>
        FunctionParameter,

        /// <summary>
        /// Extension used in a complex expression or other context
        /// Example: &result = &string.ToUpper + " suffix";
        /// </summary>
        Other
    }
}
