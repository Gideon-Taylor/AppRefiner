using Antlr4.Runtime;

namespace AppRefiner.PeopleCode
{
    public static class ByteTrackingTokenExtensions
    {
        /// <summary>
        /// Converts a token to use byte-based StartIndex/StopIndex for Scintilla positioning.
        /// If the token has byte tracking information, returns a new token with byte indexes.
        /// Otherwise, returns the original token unchanged.
        /// </summary>
        /// <param name="token">The token to convert</param>
        /// <returns>Token with byte-based indexes or original token</returns>
        public static int ByteStartIndex(this IToken token)
        {
            if (token is ByteTrackingToken btToken && btToken.ByteStartIndex >= 0 && btToken.ByteStopIndex >= 0)
            {
                return btToken.ByteStartIndex;
            }
            return token.StartIndex; // Fallback to StartIndex if no byte tracking available
        }

        public static int ByteStopIndex(this IToken token)
        {
            if (token is ByteTrackingToken btToken && btToken.ByteStartIndex >= 0 && btToken.ByteStopIndex >= 0)
            {
                return btToken.ByteStopIndex;
            }
            return token.StopIndex; // Fallback to StopIndex if no byte tracking available
        }
    }


    public class ByteTrackingToken : CommonToken
    {
        public int ByteStartIndex { get; set; }
        public int ByteStopIndex { get; set; }

        public ByteTrackingToken(int type, string text) : base(type, text)
        {
        }

        public ByteTrackingToken(IToken oldToken) : base(oldToken)
        {
            if (oldToken is ByteTrackingToken btToken)
            {
                ByteStartIndex = btToken.ByteStartIndex;
                ByteStopIndex = btToken.ByteStopIndex;
            }
        }

        public ByteTrackingToken(int type) : base(type)
        {
        }

        public ByteTrackingToken(Tuple<ITokenSource, ICharStream> source, int type,
                                 int channel, int start, int stop)
            : base(source, type, channel, start, stop)
        {
        }
    }
}
