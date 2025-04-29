namespace AppRefiner.Ast
{
    /// <summary>
    /// Specifies the category of a PeopleCode type in the AST.
    /// </summary>
    public enum TypeCode
    {
        /// <summary>
        /// A built-in simple type (String, Number, Boolean, etc.).
        /// </summary>
        BuiltIn,
        /// <summary>
        /// A reference to an Application Class or Interface.
        /// </summary>
        AppClass,
        /// <summary>
        /// An array type.
        /// </summary>
        Array,
        /// <summary>
        /// The base Exception type.
        /// </summary>
        Exception,
        /// <summary>
        /// A generic identifier used as a type (less common, potentially an error or unresolved type).
        /// </summary>
        GenericId,
        /// <summary>
        /// The Any type.
        /// </summary>
        Any // Explicitly handle Any as it's often treated specially
    }
} 