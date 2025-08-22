using FluentAssertions;
using System.Diagnostics;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.Tests.Utilities;

/// <summary>
/// Helper utilities for parser testing
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// Parse source code and return the result, failing the test if parsing fails
    /// </summary>
    public static ProgramNode ParseAndAssertSuccess(string sourceCode, string? context = null)
    {
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull(context ?? "Parse result should not be null");
        
        // Check for lexer errors first
        if (lexer.Errors.Count > 0)
        {
            var lexerErrors = string.Join("\n", lexer.Errors.Select(e => $"Line {e.Position.Line}: {e.Message}"));
            throw new InvalidOperationException($"Lexing failed{(context != null ? $" ({context})" : "")} with errors:\n{lexerErrors}\nSource code:\n{sourceCode}");
        }
        
        // Check for parsing errors
        if (parser.Errors.Count > 0)
        {
            var errorMessages = string.Join("\n", parser.Errors.Select(e => $"Line {e.Location.Start.Line}: {e.Message}"));
            throw new InvalidOperationException($"Parsing failed{(context != null ? $" ({context})" : "")} with errors:\n{errorMessages}\nSource code:\n{sourceCode}");
        }
        
        return result;
    }

    /// <summary>
    /// Parse source code and assert that it fails (for error testing)
    /// </summary>
    public static ProgramNode ParseAndExpectErrors(string sourceCode, string? context = null)
    {
        var lexer = new PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseProgram();
        
        result.Should().NotBeNull(context ?? "Parser should not crash even on malformed input");
        
        // Either lexer or parser should have errors
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        hasErrors.Should().BeTrue(context ?? "Parser should report errors for malformed input");
        
        return result;
    }

    /// <summary>
    /// Measure the time it takes to parse source code
    /// </summary>
    public static TimeSpan MeasureParseTime(string sourceCode, int iterations = 1)
    {
        // Warm up
        var warmupLexer = new PeopleCodeLexer(sourceCode);
        var warmupTokens = warmupLexer.TokenizeAll();
        var warmupParser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(warmupTokens);
        warmupParser.ParseProgram();

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var lexer = new PeopleCodeLexer(sourceCode);
            var tokens = lexer.TokenizeAll();
            var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
            parser.ParseProgram();
        }
        stopwatch.Stop();

        return TimeSpan.FromTicks(stopwatch.ElapsedTicks / iterations);
    }

    /// <summary>
    /// Measure memory usage for parsing source code
    /// </summary>
    public static long MeasureMemoryUsage(string sourceCode, int iterations = 100)
    {
        // Force garbage collection to get accurate baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);

        // Parse multiple times to get measurable memory usage
        for (int i = 0; i < iterations; i++)
        {
            var lexer = new PeopleCodeLexer(sourceCode);
            var tokens = lexer.TokenizeAll();
            var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
            parser.ParseProgram();
        }

        var finalMemory = GC.GetTotalMemory(false);
        return finalMemory - initialMemory;
    }

    /// <summary>
    /// Compare parsing performance between different implementations
    /// </summary>
    public static void CompareParsingPerformance(string sourceCode, Func<string, object> parser1, Func<string, object> parser2, 
        string parser1Name = "Parser 1", string parser2Name = "Parser 2", int iterations = 100)
    {
        var time1 = MeasureParseTimeGeneric(sourceCode, parser1, iterations);
        var time2 = MeasureParseTimeGeneric(sourceCode, parser2, iterations);

        Console.WriteLine($"{parser1Name} average time: {time1.TotalMilliseconds:F2}ms");
        Console.WriteLine($"{parser2Name} average time: {time2.TotalMilliseconds:F2}ms");
        
        if (time1 < time2)
        {
            var improvement = (time2.TotalMilliseconds - time1.TotalMilliseconds) / time2.TotalMilliseconds * 100;
            Console.WriteLine($"{parser1Name} is {improvement:F1}% faster");
        }
        else
        {
            var improvement = (time1.TotalMilliseconds - time2.TotalMilliseconds) / time1.TotalMilliseconds * 100;
            Console.WriteLine($"{parser2Name} is {improvement:F1}% faster");
        }
    }

    private static TimeSpan MeasureParseTimeGeneric(string sourceCode, Func<string, object> parser, int iterations)
    {
        // Warm up
        parser(sourceCode);

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            parser(sourceCode);
        }
        stopwatch.Stop();

        return TimeSpan.FromTicks(stopwatch.ElapsedTicks / iterations);
    }

    /// <summary>
    /// Generate test data for various PeopleCode constructs
    /// </summary>
    public static class SampleCode
    {
        public static readonly string SimpleVariableDeclaration = "LOCAL string &test = \"hello\";";
        
        public static readonly string SimpleMethod = @"
            METHOD TestMethod(&param AS string) RETURNS boolean
                LOCAL boolean &result = TRUE;
                RETURN &result;
            END-METHOD;
        ";

        public static readonly string SimpleClass = @"
            CLASS TestClass
                PROPERTY string Name GET SET;
                METHOD GetDisplayName() RETURNS string;
            PRIVATE
                INSTANCE string &name;
            END-CLASS;
        ";

        public static readonly string ComplexControlFlow = @"
            LOCAL number &total = 0;
            LOCAL string &status = ""processing"";
            
            TRY
                FOR &i = 1 TO 100
                    IF &i MOD 10 = 0 THEN
                        EVALUATE &i
                        WHEN 10
                            &status = ""ten percent"";
                        WHEN 50  
                            &status = ""halfway"";
                        WHEN 100
                            &status = ""complete"";
                        END-EVALUATE;
                    END-IF;
                    
                    &total = &total + &i;
                    
                    IF &total > 1000 THEN
                        BREAK;
                    END-IF;
                END-FOR;
                
                WHILE &total > 0
                    &total = &total - 1;
                    IF &total MOD 100 = 0 THEN
                        WARNING ""Countdown: "" | String(&total);
                    END-IF;
                END-WHILE;
                
            CATCH Exception &e
                ERROR ""Processing failed: "" | &e.Message;
                &status = ""error"";
            END-TRY;
        ";

        public static readonly string ComplexExpressions = @"
            LOCAL number &result1 = (1 + 2) * 3 ** 2 / 4 - 5;
            LOCAL boolean &result2 = &a AND (&b OR NOT &c) AND (&d >= &e);
            LOCAL string &result3 = &str1 | "" - "" | &str2 | "" (concatenated)"";
            LOCAL any &result4 = CREATE MyPackage:MyClass(&param1, &param2);
            LOCAL MyClass &result5 = &obj AS MyPackage:MyClass;
            LOCAL number &result6 = &array[&index1][&index2].Property.Method(&arg);
        ";

        /// <summary>
        /// Generate a large program for performance testing
        /// </summary>
        public static string GenerateLargeProgram(int methodCount = 50, int variableCount = 100)
        {
            var code = @"
                IMPORT LargeApp:Utilities:*;
                IMPORT LargeApp:BusinessLogic:*;
                
                GLOBAL string &g_AppName;
                COMPONENT any &c_Config;
            ";

            // Add many variable declarations
            for (int i = 1; i <= variableCount; i++)
            {
                code += $"LOCAL string &var{i} = \"test{i}\";\n";
            }

            // Add a large class
            code += @"
                CLASS LargeProcessor
            ";

            // Add many method declarations
            for (int i = 1; i <= methodCount; i++)
            {
                code += $"    METHOD Process{i}(&data AS any) RETURNS boolean;\n";
            }

            code += "END-CLASS;\n";

            // Add some method implementations
            for (int i = 1; i <= Math.Min(methodCount, 10); i++)
            {
                code += $@"
                METHOD LargeProcessor.Process{i}
                /+ &data AS any +/
                /+ RETURNS boolean +/
                    LOCAL number &count = 0;
                    LOCAL string &result = """";
                    
                    FOR &j = 1 TO 50
                        &count = &count + 1;
                        &result = &result | String(&j) | "" "";
                    END-FOR;
                    
                    RETURN &count > 0;
                END-METHOD;
                ";
            }

            return code;
        }
    }
}