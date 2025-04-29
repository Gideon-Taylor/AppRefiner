using System;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Ast
{
    /// <summary>
    /// Represents a PeopleCode type in the AST.
    /// </summary>
    public class Type
    {
        /// <summary>
        /// Gets the category of this type.
        /// </summary>
        public TypeCode Kind { get; internal set; }

        /// <summary>
        /// Gets the name of the type (e.g., "String", "Integer", "PKG:SUB:MyClass").
        /// For arrays, this might be the base type name or empty if not specified.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// For Array types, gets the element type. Null otherwise.
        /// </summary>
        public Type? ElementType { get; internal set; }

        /// <summary>
        /// For Array types, gets the number of dimensions.
        /// </summary>
        public int ArrayDimensions { get; internal set; }

        /// <summary>
        /// Gets the full text representation as parsed from the source.
        /// </summary>
        public string FullText { get; private set; }

        // Internal constructor accessible within Ast assembly
        internal Type(string fullText)
        {
            Name = string.Empty; // Default
            FullText = fullText;
            Kind = TypeCode.GenericId; // Default Kind
            // Initialize others? ElementType = null, ArrayDimensions = 0 already default
        }

        /// <summary>
        /// Parses a TypeTContext from the ANTLR tree into a Type AST node.
        /// </summary>
        /// <param name="context">The ANTLR TypeTContext.</param>
        /// <returns>A new Type instance.</returns>
        public static Type Parse(TypeTContext context)
        {
            var astType = new Type(context.GetText());

            if (context is ArrayTypeContext arrayCtx)
            {
                astType.Kind = TypeCode.Array;
                astType.ArrayDimensions = 1 + arrayCtx.ARRAY().Length; // Initial ARRAY plus any subsequent OF ARRAY
                // The base type is the last typeT in the chain, if present
                var baseTypeCtx = arrayCtx.typeT(); 
                if(baseTypeCtx != null)
                {
                     astType.ElementType = Type.Parse(baseTypeCtx);
                     astType.Name = astType.ElementType.Name; // Array name is often base type name
                }
                else
                {
                    // Array of Any (implicitly)
                    astType.Name = "Any"; 
                    astType.ElementType = new Type("Any") { Kind = TypeCode.Any, Name = "Any" };
                }
            }
            else if (context is BaseExceptionTypeContext)
            {
                astType.Kind = TypeCode.Exception;
                astType.Name = "Exception";
            }
            else if (context is AppClassTypeContext appClassCtx)
            {
                astType.Kind = TypeCode.AppClass;
                astType.Name = appClassCtx.appClassPath().GetText(); // Full path like PKG:SUB:Class
            }
            else if (context is SimpleTypeTypeContext simpleTypeCtx)
            {
                var simpleType = simpleTypeCtx.simpleType();
                if (simpleType is SimpleBuiltInTypeContext builtInCtx)
                {
                    // Handle built-in types (including Any)
                    var terminalNode = builtInCtx.builtInType().GetChild(0);
                    astType.Name = terminalNode.GetText();
                    astType.Kind = astType.Name.Equals("Any", StringComparison.OrdinalIgnoreCase) ? TypeCode.Any : TypeCode.BuiltIn;
                }
                else if (simpleType is SimpleGenericIDContext genericIdCtx)
                {
                    astType.Kind = TypeCode.GenericId;
                    astType.Name = genericIdCtx.GENERIC_ID_LIMITED().GetText();
                }
                else
                {
                     // Should not happen
                    astType.Kind = TypeCode.GenericId; // Fallback?
                    astType.Name = simpleType.GetText();
                }
            }
            else
            {
                // Unknown type context, should not happen with current grammar
                astType.Kind = TypeCode.GenericId; // Fallback?
                astType.Name = context.GetText(); 
            }

            return astType;
        }

        /// <summary>
        /// Gets the default value for this type as a string literal suitable for code generation.
        /// </summary>
        /// <returns>"Null" for non-built-in types, specific defaults for built-ins.</returns>
        public string GetDefaultValue()
        {
            switch (Kind)
            {
                case TypeCode.BuiltIn:
                    // Case-insensitive comparison for built-in type names
                    if (Name.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                        return "False";
                    if (Name.Equals("Integer", StringComparison.OrdinalIgnoreCase) || Name.Equals("Number", StringComparison.OrdinalIgnoreCase) || Name.Equals("Float", StringComparison.OrdinalIgnoreCase))
                        return "0";
                    if (Name.Equals("String", StringComparison.OrdinalIgnoreCase))
                        return "\"\""; // Empty string literal
                    if (Name.Equals("Date", StringComparison.OrdinalIgnoreCase) || Name.Equals("Time", StringComparison.OrdinalIgnoreCase) || Name.Equals("DateTime", StringComparison.OrdinalIgnoreCase))
                        return "Null"; // Or perhaps a specific default date/time string? Null seems safer.
                    // Add other specific built-ins if necessary
                    return "Null"; // Default for unrecognized built-ins
                
                case TypeCode.Any:
                case TypeCode.AppClass:
                case TypeCode.Exception:
                case TypeCode.Array:
                case TypeCode.GenericId:
                default:
                    return "Null";
            }
        }

        public override string ToString()
        {
            return FullText;
        }
    }
} 