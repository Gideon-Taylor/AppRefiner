using static AppRefiner.PeopleCode.PeopleCodeParser;
using Antlr4.Runtime.Tree; 

namespace AppRefiner.Ast
{
    /// <summary>
    /// Represents a private constant declaration within a PeopleCode Application Class.
    /// </summary>
    public class Constant
    {
        /// <summary>
        /// Gets the name of the constant.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the literal value assigned to the constant as a string.
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Gets the inferred type of the constant based on its literal value.
        /// </summary>
        public Type InferredType { get; private set; } 

        // Private constructor
        private Constant(string name, string value, Type inferredType)
        {
            Name = name;
            Value = value;
            InferredType = inferredType;
        }

        /// <summary>
        /// Parses a ConstantDeclarationContext to create a Constant AST node.
        /// </summary>
        /// <param name="context">The ANTLR ConstantDeclarationContext.</param>
        /// <returns>A new Constant instance.</returns>
        public static Constant Parse(ConstantDeclarationContext context)
        {
            var name = context.USER_VARIABLE().GetText();
            var literalCtx = context.literal();
            var value = literalCtx.GetText();
            var inferredType = InferTypeFromLiteral(literalCtx);

            return new Constant(name, value, inferredType);
        }

        // Helper to infer Type from literal context
        private static Type InferTypeFromLiteral(LiteralContext context)
        {
            string typeName;
            TypeCode typeCode = TypeCode.BuiltIn; // Most literals are built-in

            // Check the specific child type of the literal context
            ITree child = context.GetChild(0);
            if (child is ITerminalNode terminalNode)
            {
                switch (terminalNode.Symbol.Type) 
                {
                    case PeopleCodeLexer.StringLiteral:
                        typeName = "String";
                        break;
                    case PeopleCodeLexer.IntegerLiteral:
                        typeName = "Integer";
                        break;
                    case PeopleCodeLexer.DecimalLiteral: // Treat DecimalLiteral as Number or Float?
                        typeName = "Number"; // Or "Float"? PeopleCode often uses Number.
                        break;
                    case PeopleCodeLexer.BooleanLiteral:
                        typeName = "Boolean";
                        break;
                    case PeopleCodeLexer.NULL:
                        typeName = "Any"; // Null is compatible with Any or object types
                        typeCode = TypeCode.Any;
                        break;
                    default:
                        typeName = "Unknown"; // Should not happen for valid literals
                        typeCode = TypeCode.GenericId;
                        break;
                }
            }
            else
            {
                 typeName = "Unknown"; // Non-terminal literal? Should not happen.
                 typeCode = TypeCode.GenericId;
            }

            // Create a simple Type object based on inference
            return new Type(typeName) { Kind = typeCode, Name = typeName };
        }
    }
} 