using AppRefiner;
using AppRefiner.Database;
using ParserPorting.Stylers;
using ParserPorting.Stylers.Impl;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;

var source = File.ReadAllText(@"c:\temp\test.pcode");

var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(source);
var tokens = lexer.TokenizeAll();

// Comments are now automatically collected by the parser and stored in ProgramNode.Comments

var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
var program = parser.ParseProgram();

// Create a mock ScintillaEditor for testing (normally this would come from the actual editor)
var mockEditor = new ScintillaEditor(IntPtr.Zero, 0, 0, "TestPackage:SubPackage:TestClass (Application Package PeopleCode)");

// TODO: Manually instantiate your IDataManager here
// Example: IDataManager dataManager = new YourDataManagerImplementation();


var connectionString = $"Data Source=IH91U019;User Id=SYSADM;Password=SYSADM;";

IDataManager? dataManager = new OraclePeopleSoftDataManager(connectionString);
dataManager.Connect();

// Create stylers
var stylers = new List<IStyler>
{
    new UnusedVariables(),
    new UnusedImports(),
    new MeaninglessVariableNameStyler(),
    new MissingConstructor(),
    new ReusedForIterator(),
    new ClassNameMismatch(),
    new LinterSuppressionStyler(),
    new InvalidAppClass()
};

// Set DataManager on stylers that need it and run them
foreach (var styler in stylers)
{
    // Check if styler requires database connection
    if (styler.DatabaseRequirement == DataManagerRequirement.Required && dataManager == null)
    {
        Console.WriteLine($"Skipping {styler.GetType().Name} - requires database connection but none available");
        continue;
    }
    
    // Set DataManager if available
    if (dataManager != null)
    {
        styler.DataManager = dataManager;
    }
    
    // Set Editor (Comments are now available directly from ProgramNode)
    styler.Editor = mockEditor;
    
    // Reset and run the styler
    styler.Reset();
    
    // Run the styler based on its type
    try 
    {
        switch (styler)
        {
            case UnusedVariables unusedVars:
                unusedVars.VisitProgram(program);
                Console.WriteLine($"UnusedVariables found {unusedVars.Indicators.Count} indicators");
                break;
            case MissingConstructor missingCtor:
                missingCtor.VisitProgram(program);
                Console.WriteLine($"MissingConstructor found {missingCtor.Indicators.Count} indicators");
                break;
            case ClassNameMismatch classNameMismatch:
                classNameMismatch.VisitProgram(program);
                Console.WriteLine($"ClassNameMismatch found {classNameMismatch.Indicators.Count} indicators");
                break;
            case LinterSuppressionStyler suppressionStyler:
                suppressionStyler.VisitProgram(program);
                Console.WriteLine($"LinterSuppressionStyler found {suppressionStyler.Indicators.Count} indicators");
                break;
            case InvalidAppClass invalidAppClass:
                invalidAppClass.VisitProgram(program);
                Console.WriteLine($"InvalidAppClass found {invalidAppClass.Indicators.Count} indicators");
                break;
            default:
                // Generic handling for other styler types
                if (styler is BaseStyler baseStyler)
                {
                    baseStyler.VisitProgram(program);
                    Console.WriteLine($"{styler.GetType().Name} found {baseStyler.Indicators.Count} indicators");
                }
                else if (styler is ScopedStyler scopedStyler)
                {
                    scopedStyler.VisitProgram(program);
                    Console.WriteLine($"{styler.GetType().Name} found {scopedStyler.Indicators.Count} indicators");
                }
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error running {styler.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine("Done");