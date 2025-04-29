using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using AppRefiner.PeopleCode; // Use the namespace specified in the csproj for generated types

namespace AppRefiner.PeopleCode
{
    /// <summary>
    /// Provides a reusable way to parse PeopleCode source text into an ANTLR parse tree.
    /// </summary>
    public static class ProgramParser
    {
        /// <summary>
        /// Parses the given PeopleCode source text.
        /// </summary>
        /// <param name="sourceText">The PeopleCode source code as a string.</param>
        /// <returns>The root of the parse tree (ProgramContext).</returns>
        public static PeopleCodeParser.ProgramContext Parse(string sourceText)
        {
            // Create the ANTLR stream and lexer
            AntlrInputStream inputStream = new(sourceText);
            PeopleCodeLexer lexer = new(inputStream);
            
            // Reset lexer state if it holds any from previous runs (optional but good practice)
            lexer.Reset(); 

            // Create the token stream and parser
            CommonTokenStream tokenStream = new(lexer);
            PeopleCodeParser parser = new(tokenStream);

            // Reset parser state (optional but good practice)
            parser.Reset();
            
            // Set a custom error handler if needed (optional)
            // parser.RemoveErrorListeners(); 
            // parser.AddErrorListener(new YourCustomErrorListener());

            PeopleCodeParser.ProgramContext parseTree;
            try
            {
                 // Parse the program starting from the 'program' rule
                parseTree = parser.program();
            }
            finally
            {
                // Clear the DFA cache to release memory, as shown in the example
                // This is important if parsing many files sequentially.
                parser.Interpreter.ClearDFA();
            }

            // Consider adding basic error checking here, e.g., checking parser.NumberOfSyntaxErrors
            // if (parser.NumberOfSyntaxErrors > 0) { // Handle or log errors }

            return parseTree;
        }

        // Future Consideration: Add a method to also return tokens or comments if needed
        // public static (PeopleCodeParser.ProgramContext Tree, IList<IToken> Tokens) ParseWithTokens(string sourceText) { ... }
    }
} 