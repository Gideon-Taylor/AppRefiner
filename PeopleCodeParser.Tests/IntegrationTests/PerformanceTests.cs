using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FluentAssertions;
using Xunit;
using AppRefiner.PeopleCode;
using System.Diagnostics;

namespace PeopleCodeParser.Tests.IntegrationTests;

/// <summary>
/// Performance benchmarks and tests for the parser
/// </summary>
[MemoryDiagnoser]
public class PerformanceBenchmarks
{
    private readonly string _simpleProgram;
    private readonly string _complexClass;
    private readonly string _largeProgram;

    public PerformanceBenchmarks()
    {
        _simpleProgram = @"
        LOCAL string &name = ""test"";
        LOCAL number &count = 0;
        FOR &i = 1 TO 10
            &count = &count + 1;
        END-FOR;
        ";

        _complexClass = @"
        IMPORT MyPackage:Utilities:*;
        
        CLASS BusinessLogic EXTENDS MyPackage:BaseClasses:ServiceBase
                            IMPLEMENTS MyPackage:Interfaces:IBusinessLogic
            
            PROPERTY string ServiceName GET;
            PROPERTY number MaxRetries GET SET;
            
        PROTECTED  
            METHOD Initialize() RETURNS boolean;
            METHOD ValidateInput(&data AS any) RETURNS boolean;
            
        PRIVATE
            INSTANCE string &serviceName;
            INSTANCE number &maxRetries;
            CONSTANT DEFAULT_RETRIES = 3;
            
        END-CLASS;
        
        METHOD BusinessLogic.Initialize
        /+ RETURNS boolean +/
            TRY
                &serviceName = ""BusinessLogic Service"";
                &maxRetries = DEFAULT_RETRIES;
                RETURN TRUE;
            CATCH Exception &e
                ERROR ""Failed to initialize: "" | &e.Message;
                RETURN FALSE;
            END-TRY;
        END-METHOD;
        ";

        _largeProgram = GenerateLargeProgram();
    }

    [Benchmark]
    public void ParseSimpleProgram()
    {
        var result = ProgramParser.Parse(_simpleProgram);
        if (result == null) throw new InvalidOperationException("Parse result was null");
    }

    [Benchmark]
    public void ParseComplexClass()
    {
        var result = ProgramParser.Parse(_complexClass);
        if (result == null) throw new InvalidOperationException("Parse result was null");
    }

    [Benchmark]
    public void ParseLargeProgram()
    {
        var result = ProgramParser.Parse(_largeProgram);
        if (result == null) throw new InvalidOperationException("Parse result was null");
    }

    private string GenerateLargeProgram()
    {
        var program = @"
        IMPORT LargePackage:Utilities:*;
        IMPORT LargePackage:BusinessLogic:*;
        IMPORT LargePackage:DataAccess:*;
        
        GLOBAL string &g_ApplicationName;
        GLOBAL number &g_Version;
        COMPONENT any &c_Configuration;
        
        ";

        // Generate multiple functions
        for (int i = 1; i <= 50; i++)
        {
            program += $@"
            DECLARE FUNCTION ProcessData{i} PEOPLECODE FUNCLIB.UTILITIES FieldFormula;
            ";
        }

        // Generate a large class with many methods
        program += @"
        CLASS LargeBusinessProcessor EXTENDS BaseProcessor
            ";

        // Add many properties
        for (int i = 1; i <= 20; i++)
        {
            program += $"PROPERTY string Property{i} GET SET;\n            ";
        }

        program += @"
        PROTECTED
            ";

        // Add many method declarations
        for (int i = 1; i <= 30; i++)
        {
            program += $"METHOD ProcessStep{i}(&data AS any) RETURNS boolean;\n            ";
        }

        program += @"
        PRIVATE
            ";

        // Add many instance variables
        for (int i = 1; i <= 25; i++)
        {
            program += $"INSTANCE any &step{i}Data;\n            ";
        }

        program += "END-CLASS;\n";

        // Generate method implementations
        for (int i = 1; i <= 10; i++) // Generate fewer implementations to keep size reasonable
        {
            program += $@"
            METHOD LargeBusinessProcessor.ProcessStep{i}
            /+ &data AS any +/
            /+ RETURNS boolean +/
                LOCAL number &counter = 0;
                LOCAL string &result = """";
                LOCAL boolean &success = FALSE;
                
                TRY
                    IF &data <> NULL THEN
                        FOR &j = 1 TO 100
                            &counter = &counter + 1;
                            &result = &result | ""Step "" | String(&j) | "" "";
                            
                            EVALUATE &counter
                            WHEN > 50
                                &success = TRUE;
                                BREAK;
                            WHEN > 25
                                &result = &result | ""(halfway) "";
                            WHEN-OTHER
                                /* Continue processing */
                            END-EVALUATE;
                        END-FOR;
                        
                        WHILE &counter > 0 AND &success
                            &counter = &counter - 1;
                            IF &counter MOD 10 = 0 THEN
                                &result = &result | ""Checkpoint "" | String(&counter) | "" "";
                            END-IF;
                        END-WHILE;
                    END-IF;
                    
                    RETURN &success;
                CATCH Exception &e
                    ERROR ""Error in ProcessStep{i}: "" | &e.Message;
                    RETURN FALSE;
                END-TRY;
            END-METHOD;
            ";
        }

        return program;
    }
}

/// <summary>
/// Performance tests using xUnit (for regular test execution)
/// </summary>
public class PerformanceTests
{
    [Fact]
    public void Simple_Program_Should_Parse_Quickly()
    {
        var sourceCode = @"
        LOCAL string &name = ""test"";
        LOCAL number &count = 0;
        FOR &i = 1 TO 10
            &count = &count + 1;
        END-FOR;
        ";

        var stopwatch = Stopwatch.StartNew();
        var result = ProgramParser.Parse(sourceCode);
        stopwatch.Stop();

        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Simple program should parse in under 100ms");
    }

    [Fact]
    public void Complex_Class_Should_Parse_Reasonably_Fast()
    {
        var sourceCode = @"
        IMPORT MyPackage:Utilities:*;
        
        CLASS BusinessLogic EXTENDS MyPackage:BaseClasses:ServiceBase
                            IMPLEMENTS MyPackage:Interfaces:IBusinessLogic
            
            PROPERTY string ServiceName GET;
            PROPERTY number MaxRetries GET SET;
            
        PROTECTED  
            METHOD Initialize() RETURNS boolean;
            METHOD ValidateInput(&data AS any) RETURNS boolean;
            
        PRIVATE
            INSTANCE string &serviceName;
            INSTANCE number &maxRetries;
            CONSTANT DEFAULT_RETRIES = 3;
            
        END-CLASS;
        
        METHOD BusinessLogic.Initialize
        /+ RETURNS boolean +/
            TRY
                &serviceName = ""BusinessLogic Service"";
                &maxRetries = DEFAULT_RETRIES;
                RETURN TRUE;
            CATCH Exception &e
                ERROR ""Failed to initialize: "" | &e.Message;
                RETURN FALSE;
            END-TRY;
        END-METHOD;
        ";

        var stopwatch = Stopwatch.StartNew();
        var result = ProgramParser.Parse(sourceCode);
        stopwatch.Stop();

        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, "Complex class should parse in under 500ms");
    }

    [Fact]
    public void Multiple_Parse_Calls_Should_Not_Leak_Memory()
    {
        var sourceCode = @"
        LOCAL string &test = ""memory test"";
        FOR &i = 1 TO 100
            &test = &test | String(&i);
        END-FOR;
        ";

        var initialMemory = GC.GetTotalMemory(true);

        // Parse the same code multiple times
        for (int i = 0; i < 1000; i++)
        {
            var result = ProgramParser.Parse(sourceCode);
            result.Should().NotBeNull();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Allow for some reasonable memory increase, but not excessive
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, "Memory usage should not increase excessively");
    }

    [Fact]
    public async Task Concurrent_Parsing_Should_Be_Thread_Safe()
    {
        var sourceCode = @"
        LOCAL string &threadTest = ""concurrent test"";
        LOCAL number &result = 0;
        FOR &i = 1 TO 50
            &result = &result + &i;
        END-FOR;
        ";

        var tasks = new Task[10];
        var results = new bool[10];

        for (int i = 0; i < 10; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    var result = ProgramParser.Parse(sourceCode);
                    results[index] = result != null;
                }
                catch
                {
                    results[index] = false;
                }
            });
        }

        await Task.WhenAll(tasks);

        // All parsing operations should succeed
        results.Should().AllSatisfy(result => result.Should().BeTrue("All concurrent parsing operations should succeed"));
    }
}

/// <summary>
/// Utility class to run benchmarks from tests
/// </summary>
public class BenchmarkRunner
{
    [Fact(Skip = "Manual benchmark execution only")]
    public void RunPerformanceBenchmarks()
    {
        // This test is skipped by default but can be run manually to execute benchmarks
        var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<PerformanceBenchmarks>();
        summary.Should().NotBeNull();
    }
}