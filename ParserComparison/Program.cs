using ParserComparison.Models;
using ParserComparison.Tests;
using ParserComparison.Utils;

namespace ParserComparison;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            //SingleFileTest.RunTest(@"C:\Users\tslat\Downloads\idadev1-master\PeopleCode\Application Packages\IS_CO_PKG\Project\ProjectDefn.pcode");

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
                RunBulkDirectoryTest(config);
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
        Console.WriteLine("  -h, --help                Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ParserComparison -f \"C:\\code\\sample.pcode\"");
        Console.WriteLine("  ParserComparison -d \"C:\\PeopleCode\\\" -v");
        Console.WriteLine("  ParserComparison -d \"C:\\PeopleCode\\\" --max-files 100 --continue-on-error");
    }
}