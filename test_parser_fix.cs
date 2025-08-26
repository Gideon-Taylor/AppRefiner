using System;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;

class TestParserFix
{
    static void Main()
    {
        var code = @"SQLExec(""SELECT 'Y' FROM DUAL"");";
        
        var lexer = new PeopleCodeLexer(code);
        var tokens = lexer.Tokenize();
        var parser = new PeopleCodeParser(tokens);
        
        try
        {
            var program = parser.ParseProgram();
            
            if (program.Statements.Count > 0 && program.Statements[0] is ExpressionStatementNode exprStmt)
            {
                if (exprStmt.Expression is FunctionCallNode functionCall)
                {
                    Console.WriteLine("SUCCESS: SQLExec is correctly parsed as FunctionCallNode");
                    Console.WriteLine($"Function: {functionCall.Function}");
                    Console.WriteLine($"Arguments count: {functionCall.Arguments.Count}");
                }
                else if (exprStmt.Expression is ArrayAccessNode arrayAccess)
                {
                    Console.WriteLine("ERROR: SQLExec is incorrectly parsed as ArrayAccessNode");
                }
                else
                {
                    Console.WriteLine($"UNEXPECTED: SQLExec is parsed as {exprStmt.Expression.GetType().Name}");
                }
            }
            else
            {
                Console.WriteLine("ERROR: Could not parse the statement");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PARSE ERROR: {ex.Message}");
        }
    }
}