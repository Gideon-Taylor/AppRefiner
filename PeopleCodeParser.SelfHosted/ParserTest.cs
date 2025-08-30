using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Test;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Simple test to verify parser functionality
/// </summary>
public static class ParserTest
{
    public static void RunBasicTest()
    {
        Console.WriteLine("Running basic parser test...");

        // Test simple expression parsing
       /* TestExpression("1 + 2 * 3");
        TestExpression("\"hello\" | \" world\"");
        TestExpression("myVar = 42");
        TestExpression("myFunction(1, 2, 3)");

        // Test simple statement parsing
        TestStatement("IF x > 0 THEN y = 1; END-IF;");
        TestStatement("FOR i = 1 TO 10 x = x + i; END-FOR;");
        TestStatement("WHILE x < 100 x = x * 2; END-WHILE;");
        TestStatement("RETURN x + y;");

        // Test simple program
        TestProgram("LOCAL INTEGER x; x = 42; RETURN x;");

        // Test EXCEPTION type integration
        TestExceptionTypes();

        // Test built-in object types
        TestBuiltInObjectTypes();*/

        TestProgram(File.ReadAllText(@"C:\Users\tslat\Downloads\TopicSkill.ppc"));

        // Test compiler directives
        DirectiveTest.RunTests();
        
        // Test improved error recovery
        TestImprovedErrorRecovery();

        Console.WriteLine("Basic parser test completed.");
    }

    private static void TestExpression(string expression)
    {
        Console.WriteLine($"Testing expression: {expression}");
        try
        {
            var lexer = new PeopleCodeLexer(expression);
            var tokens = lexer.TokenizeAll();
            
            if (tokens.Count == 0)
            {
                Console.WriteLine("  No tokens generated");
                return;
            }
            var parser = new PeopleCodeParser(tokens);
            var result = parser.ParseExpression();
            
            if (result != null)
            {
                Console.WriteLine($"  SUCCESS: {result.GetType().Name} - {result}");
            }
            else
            {
                Console.WriteLine("  FAILED: Expression parsing returned null");
            }

            if (parser.Errors.Count > 0)
            {
                Console.WriteLine("  Errors:");
                foreach (var error in parser.Errors)
                {
                    Console.WriteLine($"    {error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  EXCEPTION: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static void TestStatement(string statement)
    {
        Console.WriteLine($"Testing statement: {statement}");
        try
        {
            var lexer = new PeopleCodeLexer(statement);
            var tokens = lexer.TokenizeAll();
            
            if (tokens.Count == 0)
            {
                Console.WriteLine("  No tokens generated");
                return;
            }

            var parser = new PeopleCodeParser(tokens);
            // Access statement parsing through program parsing for now
            var program = parser.ParseProgram();
            
            if (program != null)
            {
                Console.WriteLine($"  SUCCESS: Program parsed with {program.MainBlock?.Statements.Count ?? 0} statements");
            }
            else
            {
                Console.WriteLine("  FAILED: Statement parsing returned null");
            }

            if (parser.Errors.Count > 0)
            {
                Console.WriteLine("  Errors:");
                foreach (var error in parser.Errors)
                {
                    Console.WriteLine($"    {error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  EXCEPTION: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static void TestProgram(string program)
    {
        Console.WriteLine($"Testing program: {program}");
        try
        {
            var lexer = new PeopleCodeLexer(program);
            var tokens = lexer.TokenizeAll();
            
            if (tokens.Count == 0)
            {
                Console.WriteLine("  No tokens generated");
                return;
            }

            var parser = new PeopleCodeParser(tokens);
            var result = parser.ParseProgram();
            
            if (result != null)
            {
                Console.WriteLine($"  SUCCESS: Program with {result.MainBlock?.Statements.Count ?? 0} statements");
                Console.WriteLine($"    Imports: {result.Imports.Count}");
                Console.WriteLine($"    Functions: {result.Functions.Count}");
                Console.WriteLine($"    Variables: {result.Variables.Count}");
            }
            else
            {
                Console.WriteLine("  FAILED: Program parsing returned null");
            }

            if (parser.Errors.Count > 0)
            {
                Console.WriteLine("  Errors:");
                foreach (var error in parser.Errors)
                {
                    Console.WriteLine($"    {error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  EXCEPTION: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static void TestExceptionTypes()
    {
        Console.WriteLine("Testing EXCEPTION type integration:");

        // Test EXCEPTION as built-in type
        TestExpression("CREATE EXCEPTION(\"test message\")");

        // Test EXCEPTION in variable declarations
        TestStatement("LOCAL EXCEPTION &ex;");

        // Test EXCEPTION as superclass
        var exceptionClassCode = @"
CLASS TestException EXTENDS EXCEPTION
    METHOD TestMethod(&param AS EXCEPTION);
END-CLASS;

METHOD TestMethod
/+ &param AS EXCEPTION +/
END-METHOD;
";
        TestProgram(exceptionClassCode);

        // Test comprehensive exception usage
        if (File.Exists(@"C:\Users\tslat\repos\GitHub\AppRefiner\exception_test.pcd"))
        {
            var exceptionTestCode = File.ReadAllText(@"C:\Users\tslat\repos\GitHub\AppRefiner\exception_test.pcd");
            TestProgram(exceptionTestCode);
        }

        Console.WriteLine("EXCEPTION type integration test completed.\n");
    }

    private static void TestBuiltInObjectTypes()
    {
        Console.WriteLine("Testing built-in object types integration:");

        // Test common PeopleCode object types in variable declarations
        TestStatement("LOCAL Field &field;");
        TestStatement("LOCAL Record &rec;");
        TestStatement("LOCAL XmlDoc &xmlDoc;");
        TestStatement("LOCAL JsonObject &json;");
        TestStatement("LOCAL Chart &chart;");
        TestStatement("LOCAL Session &session;");

        // Test object types in method parameters
        TestStatement("METHOD TestMethod(&field AS Field, &rec AS Record);");

        // Test object types in return types
        var methodWithObjectReturn = @"
CLASS TestClass
    METHOD GetRecord() RETURNS Record;
    METHOD GetField() RETURNS Field;
    METHOD GetXmlDoc() RETURNS XmlDoc;
END-CLASS;

METHOD GetRecord
RETURNS Record
END-METHOD;

METHOD GetField
RETURNS Field
END-METHOD;

METHOD GetXmlDoc
RETURNS XmlDoc
END-METHOD;
";
        TestProgram(methodWithObjectReturn);

        // Test mixed primitive and object types
        TestStatement("LOCAL STRING &name; LOCAL Field &field; LOCAL INTEGER &count;");

        Console.WriteLine("Built-in object types integration test completed.\n");
    }

    private static void TestNullLiteralBugFix()
    {
        Console.WriteLine("Testing NULL literal bug fix:");

        // Test the specific problematic case that was failing
        var problematicCode = @"
If (&smallTalkCast = Null And
      &skeletonCast = Null) Then
End-If;
";

        Console.WriteLine("Testing problematic IF statement with NULL literals...");
        TestProgram(problematicCode);

        // Test NULL literal in other contexts
        TestExpression("Null");
        TestExpression("&var = Null");
        TestExpression("Null = &var");
        TestExpression("(&var = Null And &other <> Null)");

        // Test error recovery - malformed expression followed by THEN
        var errorRecoveryTest = @"
If (&badExpression = && invalid tokens here) Then
    &x = 1;
End-If;
";

        Console.WriteLine("Testing error recovery in IF statements...");
        TestProgram(errorRecoveryTest);

        Console.WriteLine("NULL literal bug fix test completed.\n");
    }
    
    /// <summary>
    /// Test improved error recovery for incomplete local variable declarations
    /// </summary>
    public static void TestImprovedErrorRecovery()
    {
        Console.WriteLine("Testing improved error recovery for incomplete local variable declarations:");

        // Test the specific case: incomplete local variable declaration followed by IF statement
        var incompleteLocalVarTest = @"
method FieldValueInRecord
   /+ &recName as String, +/
   /+ &fldName as String, +/
   /+ &fldValue as String +/
   /+ Returns Boolean +/
   Local string &FetchStr = ""SELECT COUNT(* ) FROM "" | &recName | "" WHERE "" | &fldName | ""='"" | &fldValue | ""'"";
   Local SQL &FetchSQL = CreateSQL(&FetchStr);
   Local integer &cnt;
   While &FetchSQL.Fetch(&cnt)
      Break;
   End-While;

   local integer 
   If &cnt <> 0 Then
      Return True;
   Else
      Return False;
   End-If;
   
end-method;
";

        Console.WriteLine("Testing incomplete local variable declaration followed by IF statement...");
        TestProgram(incompleteLocalVarTest);

        // Test simpler case - just incomplete declaration and IF
        var simpleCase = @"
local integer
If &x > 0 Then
   &y = 1;
End-If;
";
        
        Console.WriteLine("Testing simple incomplete local variable declaration...");
        TestProgram(simpleCase);

        Console.WriteLine("Improved error recovery test completed.\n");
    }
}