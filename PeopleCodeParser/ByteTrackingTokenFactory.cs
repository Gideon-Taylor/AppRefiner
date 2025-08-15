using Antlr4.Runtime;

namespace AppRefiner.PeopleCode
{
  
    public class ByteTrackingTokenFactory : ITokenFactory
    {
        public static readonly ByteTrackingTokenFactory Default = new ByteTrackingTokenFactory();

        protected ByteTrackingTokenFactory()
        {
        }

        public IToken Create(Tuple<ITokenSource, ICharStream> source, int type,
                                        string text, int channel, int start, int stop,
                                        int line, int charPositionInLine)
        {
            var token = new ByteTrackingToken(source, type, channel, start, stop)
            {
                Line = line,
                Column = charPositionInLine,
                Text = text
            };

            // Set byte positions if we have a ByteTrackingCharStream
            if (source.Item2 is ByteTrackingCharStream byteStream)
            {
                token.ByteStartIndex = byteStream.GetByteIndex(start);
                token.ByteStopIndex = byteStream.GetByteIndex(stop + 1) - 1;
            }

            return token;
        }

        public IToken Create(int type, string text)
        {
            return new ByteTrackingToken(type, text);
        }
    }
}
