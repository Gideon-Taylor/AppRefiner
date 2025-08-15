using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System;
using System.Text;

namespace AppRefiner.PeopleCode
{
    public class ByteTrackingCharStream : ICharStream
    {
        private readonly string input;
        private readonly int[] charToByteMap;
        private int currentCharIndex = 0;
        private string sourceName = "<unknown>";

        public ByteTrackingCharStream(string input) : this(input, "<unknown>")
        {
        }

        public ByteTrackingCharStream(string input, string sourceName)
        {
            this.input = input;
            this.sourceName = sourceName;
            this.charToByteMap = BuildCharToByteMap(input);
        }

        private int[] BuildCharToByteMap(string str)
        {
            // Create array with one extra position for end-of-string
            int[] map = new int[str.Length + 1];
            byte[] bytes = Encoding.UTF8.GetBytes(str);

            int bytePos = 0;

            for (int charPos = 0; charPos < str.Length; charPos++)
            {
                map[charPos] = bytePos;

                char c = str[charPos];

                // Handle surrogate pairs (for emojis and other 4-byte UTF-8 chars)
                if (char.IsHighSurrogate(c) && charPos + 1 < str.Length &&
                    char.IsLowSurrogate(str[charPos + 1]))
                {
                    // This is a surrogate pair (4 bytes in UTF-8)
                    bytePos += 4;
                    charPos++; // Skip the low surrogate
                    map[charPos] = bytePos; // Map the low surrogate position too
                }
                else
                {
                    // Get the UTF-8 byte count for this character
                    string charStr = c.ToString();
                    bytePos += Encoding.UTF8.GetByteCount(charStr);
                }
            }

            map[str.Length] = bytePos; // End position
            return map;
        }

        // Get byte position for current character position
        public int GetCurrentByteIndex()
        {
            return charToByteMap[currentCharIndex];
        }

        // Get byte position for any character position
        public int GetByteIndex(int charIndex)
        {
            if (charIndex < 0) return 0;
            if (charIndex >= charToByteMap.Length)
                return charToByteMap[charToByteMap.Length - 1];
            return charToByteMap[charIndex];
        }

        // ICharStream implementation
        public void Consume()
        {
            if (currentCharIndex < input.Length)
            {
                currentCharIndex++;
            }
        }

        public int LA(int i)
        {
            if (i == 0) return 0;

            if (i < 0)
            {
                i++; // e.g., translate LA(-1) to use offset i=0
                int pos = currentCharIndex + i;
                if (pos < 0 || pos >= input.Length) return IntStreamConstants.EOF;
                return input[pos];
            }

            int position = currentCharIndex + i - 1;
            if (position >= input.Length) return IntStreamConstants.EOF;
            if (position < 0) return IntStreamConstants.EOF;
            return input[position];
        }

        public int Mark()
        {
            return -1; // Not supporting mark/release in this implementation
        }

        public void Release(int marker)
        {
            // Not supporting mark/release
        }

        public int Index => currentCharIndex;

        public void Seek(int index)
        {
            currentCharIndex = Math.Min(index, input.Length);
        }

        public int Size => input.Length;

        public string SourceName => sourceName;

        public string GetText(Interval interval)
        {
            int start = interval.a;
            int stop = interval.b;

            if (start < 0 || stop < start - 1)
                return "";

            if (stop >= input.Length)
                stop = input.Length - 1;

            if (start >= input.Length)
                return "";

            return input.Substring(start, stop - start + 1);
        }
    }
}
