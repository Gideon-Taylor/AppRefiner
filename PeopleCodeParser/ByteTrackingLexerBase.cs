using Antlr4.Runtime;

namespace AppRefiner.PeopleCode
{
    // Inherit from this instead of Lexer for your generated lexer
    public abstract class ByteTrackingLexerBase : Lexer
    {
        protected ByteTrackingLexerBase(ICharStream input) : base(input)
        {
            TokenFactory = ByteTrackingTokenFactory.Default;
        }

        protected ByteTrackingLexerBase(ICharStream input, TextWriter output,
                                       TextWriter errorOutput)
            : base(input, output, errorOutput)
        {
            TokenFactory = ByteTrackingTokenFactory.Default;
        }
    }
}
