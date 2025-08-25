using ParserPorting.Stylers;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;

var source = File.ReadAllText(@"c:\temp\test.pcode");

PeopleCodeLexer lexer = new PeopleCodeLexer(source);
var tokens = lexer.TokenizeAll();

PeopleCodeParser.SelfHosted.PeopleCodeParser parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);

var program = parser.ParseProgram();

//UnusedImports unusedImportsStyler = new UnusedImports();
//unusedImportsStyler.VisitProgram(program);

UnusedVariables unusedVariablesStyler = new UnusedVariables();
unusedVariablesStyler.VisitProgram(program);

Console.WriteLine("Done");