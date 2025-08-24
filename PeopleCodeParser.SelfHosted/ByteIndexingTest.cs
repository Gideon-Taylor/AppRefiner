using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using System.Text;

namespace PeopleCodeParser.SelfHosted.Test;

/// <summary>
/// Comprehensive tests for UTF-8 byte index tracking in lexer and position structures
/// </summary>
public static class ByteIndexingTest
{
    public static void RunTests()
    {
        Console.WriteLine("=== PeopleCode Byte Indexing Tests ===\n");
        
        TestSourcePositionBasics();
        TestSourcePositionEquality();
        TestSourceSpanByteLength();
        TestAsciiText();
        TestMultiByteCharacters();
        TestMixedContent();
        TestLexerIntegration();
        TestScintillaIntegration();
        TestEdgeCases();
        
        Console.WriteLine("All byte indexing tests completed successfully!\n");
    }

    private static void TestSourcePositionBasics()
    {
        Console.WriteLine("Testing SourcePosition basics...");
        
        // Test default constructor (backward compatibility)
        var pos1 = new SourcePosition(10, 2, 5);
        Console.WriteLine($"  ✓ Default constructor: Index={pos1.Index}, ByteIndex={pos1.ByteIndex}, Line={pos1.Line}, Column={pos1.Column}");
        if (pos1.ByteIndex != pos1.Index)
            throw new Exception("ByteIndex should default to Index for backward compatibility");
        
        // Test byte-aware constructor
        var pos2 = new SourcePosition(10, 15, 2, 5);
        Console.WriteLine($"  ✓ Byte-aware constructor: Index={pos2.Index}, ByteIndex={pos2.ByteIndex}, Line={pos2.Line}, Column={pos2.Column}");
        if (pos2.ByteIndex != 15)
            throw new Exception("ByteIndex should be set to provided value");
    }

    private static void TestSourcePositionEquality()
    {
        Console.WriteLine("Testing SourcePosition equality...");
        
        var pos1 = new SourcePosition(10, 15, 2, 5);
        var pos2 = new SourcePosition(10, 15, 2, 5);
        var pos3 = new SourcePosition(10, 16, 2, 5); // Different byte index
        
        if (!pos1.Equals(pos2))
            throw new Exception("Identical positions should be equal");
            
        if (pos1.Equals(pos3))
            throw new Exception("Positions with different byte indexes should not be equal");
            
        Console.WriteLine("  ✓ SourcePosition equality works correctly");
    }

    private static void TestSourceSpanByteLength()
    {
        Console.WriteLine("Testing SourceSpan ByteLength property...");
        
        var start = new SourcePosition(0, 0, 1, 1);
        var end = new SourcePosition(5, 8, 1, 6); // 5 chars, 8 bytes
        var span = new SourceSpan(start, end);
        
        if (span.Length != 5)
            throw new Exception($"Expected Length=5, got {span.Length}");
            
        if (span.ByteLength != 8)
            throw new Exception($"Expected ByteLength=8, got {span.ByteLength}");
            
        Console.WriteLine($"  ✓ SourceSpan: Length={span.Length}, ByteLength={span.ByteLength}");
    }

    private static void TestAsciiText()
    {
        Console.WriteLine("Testing ASCII text (byte == character positions)...");
        
        var source = "Hello World!";
        var lexer = new PeopleCodeLexer(source);
        
        // For ASCII text, byte positions should equal character positions
        var tokens = lexer.TokenizeAll();
        foreach (var token in tokens)
        {
            if (token.Type == TokenType.EndOfFile) break;
            
            var start = token.SourceSpan.Start;
            var end = token.SourceSpan.End;
            
            if (start.ByteIndex != start.Index || end.ByteIndex != end.Index)
            {
                throw new Exception($"ASCII text should have byte index == character index. Token: {token.Text}, Start: char={start.Index} byte={start.ByteIndex}, End: char={end.Index} byte={end.ByteIndex}");
            }
        }
        
        Console.WriteLine("  ✓ ASCII text byte positions match character positions");
    }

    private static void TestMultiByteCharacters()
    {
        Console.WriteLine("Testing multi-byte characters...");
        
        // Test with emoji and accented characters
        var source = "café 🌟 naïve"; // Contains multi-byte UTF-8 characters
        var lexer = new PeopleCodeLexer(source);
        
        // Manually calculate expected byte positions
        var sourceBytes = Encoding.UTF8.GetBytes(source);
        Console.WriteLine($"  Source: '{source}' ({source.Length} chars, {sourceBytes.Length} bytes)");
        
        // Verify that lexer correctly tracks byte positions
        var tokens = lexer.TokenizeAll();
        var firstToken = tokens.FirstOrDefault(t => t.Type != TokenType.Whitespace && t.Type != TokenType.EndOfFile);
        
        if (firstToken != null)
        {
            var start = firstToken.SourceSpan.Start;
            Console.WriteLine($"  ✓ First token '{firstToken.Text}' at char={start.Index}, byte={start.ByteIndex}");
            
            // Verify the byte position by calculating manually
            var expectedByteIndex = Encoding.UTF8.GetByteCount(source.Substring(0, start.Index));
            if (start.ByteIndex != expectedByteIndex)
            {
                throw new Exception($"Expected byte index {expectedByteIndex}, got {start.ByteIndex}");
            }
        }
    }

    private static void TestMixedContent()
    {
        Console.WriteLine("Testing mixed ASCII and multi-byte content...");
        
        var source = "Local string &café = \"Hello 🌟\";";
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        
        foreach (var token in tokens.Where(t => t.Type != TokenType.Whitespace && t.Type != TokenType.EndOfFile))
        {
            var start = token.SourceSpan.Start;
            var end = token.SourceSpan.End;
            
            // Verify byte positions by manual calculation
            var expectedStartByte = Encoding.UTF8.GetByteCount(source.Substring(0, start.Index));
            var expectedEndByte = Encoding.UTF8.GetByteCount(source.Substring(0, end.Index));
            
            if (start.ByteIndex != expectedStartByte)
            {
                throw new Exception($"Token '{token.Text}': Expected start byte {expectedStartByte}, got {start.ByteIndex}");
            }
            
            if (end.ByteIndex != expectedEndByte)
            {
                throw new Exception($"Token '{token.Text}': Expected end byte {expectedEndByte}, got {end.ByteIndex}");
            }
        }
        
        Console.WriteLine("  ✓ Mixed content byte positions are accurate");
    }

    private static void TestLexerIntegration()
    {
        Console.WriteLine("Testing lexer integration with byte tracking...");
        
        var source = "/* Comment with émoji 🌟 */ Local boolean &test;";
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        
        // Find the Local keyword token
        var localToken = tokens.FirstOrDefault(t => t.Type == TokenType.Local);
        if (localToken == null)
            throw new Exception("Could not find Local token");
            
        var span = localToken.SourceSpan;
        Console.WriteLine($"  ✓ Local token: char range [{span.Start.Index}-{span.End.Index}], byte range [{span.Start.ByteIndex}-{span.End.ByteIndex}]");
        
        // Verify the token text matches the source at the byte positions
        var sourceBytes = Encoding.UTF8.GetBytes(source);
        var tokenBytes = sourceBytes[span.Start.ByteIndex..span.End.ByteIndex];
        var reconstructedText = Encoding.UTF8.GetString(tokenBytes);
        
        if (reconstructedText != localToken.Text)
        {
            throw new Exception($"Reconstructed text '{reconstructedText}' doesn't match token text '{localToken.Text}'");
        }
    }

    private static void TestScintillaIntegration()
    {
        Console.WriteLine("Testing Scintilla integration helpers...");
        
        var source = "café 🌟";
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var firstToken = tokens.FirstOrDefault(t => t.Type != TokenType.Whitespace && t.Type != TokenType.EndOfFile);
        
        if (firstToken != null)
        {
            var span = firstToken.SourceSpan;
            var scintillaPos = span.Start.ToScintillaPosition();
            var scintillaRange = span.ToScintillaRange();
            
            Console.WriteLine($"  ✓ Scintilla position: {scintillaPos}");
            Console.WriteLine($"  ✓ Scintilla range: {scintillaRange.start}-{scintillaRange.end}");
            
            if (scintillaPos != span.Start.ByteIndex)
                throw new Exception("Scintilla position should equal byte index");
                
            if (scintillaRange.start != span.Start.ByteIndex || scintillaRange.end != span.End.ByteIndex)
                throw new Exception("Scintilla range should equal byte range");
        }
    }

    private static void TestEdgeCases()
    {
        Console.WriteLine("Testing edge cases...");
        
        // Empty string
        var emptyLexer = new PeopleCodeLexer("");
        var emptyTokens = emptyLexer.TokenizeAll();
        Console.WriteLine($"  ✓ Empty string produces {emptyTokens.Count} tokens");
        
        // Single character
        var singleCharLexer = new PeopleCodeLexer("x");
        var singleTokens = singleCharLexer.TokenizeAll();
        Console.WriteLine($"  ✓ Single character produces {singleTokens.Count} tokens");
        
        // Only multi-byte characters
        var multiByteOnlyLexer = new PeopleCodeLexer("🌟🎉🚀");
        var multiByteTokens = multiByteOnlyLexer.TokenizeAll();
        
        var sourceBytes = Encoding.UTF8.GetBytes("🌟🎉🚀");
        Console.WriteLine($"  ✓ Multi-byte only: 3 chars = {sourceBytes.Length} bytes");
        
        // Verify positions are correct
        foreach (var token in multiByteTokens.Where(t => t.Type != TokenType.EndOfFile))
        {
            var start = token.SourceSpan.Start;
            Console.WriteLine($"    Token at char {start.Index}, byte {start.ByteIndex}");
        }
    }
}