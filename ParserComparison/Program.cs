using ParserComparison.Models;
using ParserComparison.Tests;
using ParserComparison.Utils;
using SharpCompress.Common;

namespace ParserComparison;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            BulkDirectoryTest.RunTest(new TestConfiguration
            {
                DirectoryPath = @"C:\Users\tslat\repos\GitHub\AppRefiner\ParserComparison\bin\Release\net8.0\failed",
                VerboseOutput = true,
                StopOnFirstError = true,
                MaxFiles = 10000,
                IncludeMemoryAnalysis = true,
                ProgressInterval = 1000
            });
            //var result = SingleFileTest.RunTest(@"C:\Users\tslat\repos\GitHub\AppRefiner\ParserComparison\bin\Release\net8.0\failed\Activate.pcode");
            ConsoleLogger.WriteHeader("PeopleCode Parser Comparison Tool");
            
            var config = ParseArguments(args);
            
            if (config == null)
            {
                ShowUsage();
                return;
            }

            if (!string.IsNullOrEmpty(config.SingleFilePath))
            {
                RunSingleFileTest(config.SingleFilePath);
            }
            else if (!string.IsNullOrEmpty(config.DirectoryPath))
            {
                if (config.SelfHostedOnlyMode)
                {
                    RunSelfHostedOnlyBulkTest(config);
                }
                else
                {
                    RunBulkDirectoryTest(config);
                }
            }
            else
            {
                ShowUsage();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static TestConfiguration? ParseArguments(string[] args)
    {
        if (args.Length == 0)
            return null;

        var config = new TestConfiguration();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-f":
                case "--file":
                    if (i + 1 < args.Length)
                        config.SingleFilePath = args[++i];
                    break;
                
                case "-d":
                case "--directory":
                    if (i + 1 < args.Length)
                        config.DirectoryPath = args[++i];
                    break;
                
                case "-v":
                case "--verbose":
                    config.VerboseOutput = true;
                    break;
                
                case "--continue-on-error":
                    config.StopOnFirstError = false;
                    break;
                
                case "--max-files":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int maxFiles))
                        config.MaxFiles = maxFiles;
                    break;
                
                case "--no-memory":
                    config.IncludeMemoryAnalysis = false;
                    break;
                
                case "--progress-interval":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int interval) && interval > 0)
                        config.ProgressInterval = interval;
                    break;
                
                case "--self-hosted-only":
                    config.SelfHostedOnlyMode = true;
                    break;
                
                case "--failed-dir":
                    if (i + 1 < args.Length)
                        config.FailedFilesDirectory = args[++i];
                    break;
                
                case "-h":
                case "--help":
                    return null;
            }
        }

        return config;
    }

    private static void RunSingleFileTest(string filePath)
    {
        var result = SingleFileTest.RunTest(filePath);
        
        if (!result.BothSuccessful)
        {
            Environment.ExitCode = 1;
        }
    }

    private static void RunBulkDirectoryTest(TestConfiguration config)
    {
        var results = BulkDirectoryTest.RunTest(config);
        
        var failures = results.Where(r => !r.SelfHostedResult.Success).ToList();
        if (failures.Any())
        {
            Environment.ExitCode = 1;
        }
    }

    private static void RunSelfHostedOnlyBulkTest(TestConfiguration config)
    {
        var results = SelfHostedOnlyBulkTest.RunTest(config);
        
        var failures = results.Where(r => !r.SelfHostedResult.Success).ToList();
        if (failures.Any())
        {
            Environment.ExitCode = 1;
        }
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ParserComparison -f <file-path>              # Test single file");
        Console.WriteLine("  ParserComparison -d <directory-path>         # Test all .pcode files in directory");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -f, --file <path>         Parse a single .pcode file");
        Console.WriteLine("  -d, --directory <path>    Parse all .pcode files in directory (recursive)");
        Console.WriteLine("  -v, --verbose             Show progress during bulk testing");
        Console.WriteLine("  --continue-on-error       Continue parsing even if self-hosted parser fails");
        Console.WriteLine("  --max-files <n>           Limit number of files to process in bulk test");
        Console.WriteLine("  --no-memory               Skip memory usage analysis");
        Console.WriteLine("  --progress-interval <n>   Show status every n files (default: 1000)");
        Console.WriteLine("  --self-hosted-only        Run only self-hosted parser and copy failed files");
        Console.WriteLine("  --failed-dir <path>       Directory for failed files (default: 'failed')");
        Console.WriteLine("  -h, --help                Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ParserComparison -f \"C:\\code\\sample.pcode\"");
        Console.WriteLine("  ParserComparison -d \"C:\\PeopleCode\\\" -v");
        Console.WriteLine("  ParserComparison -d \"C:\\PeopleCode\\\" --max-files 100 --continue-on-error");
        Console.WriteLine("  ParserComparison -d \"C:\\PeopleCode\\\" --self-hosted-only --failed-dir errors");
    }
}