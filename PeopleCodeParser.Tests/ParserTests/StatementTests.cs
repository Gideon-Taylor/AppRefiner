using FluentAssertions;
using Xunit;
using AppRefiner.PeopleCode;

namespace PeopleCodeParser.Tests.ParserTests;

/// <summary>
/// Tests for statement parsing including control flow
/// </summary>
public class StatementTests
{
    [Theory]
    [InlineData("LOCAL string &var;", "Local variable declaration")]
    [InlineData("LOCAL string &var1, &var2;", "Multiple variable declaration")]
    [InlineData("LOCAL string &var = \"test\";", "Variable declaration with assignment")]
    [InlineData("GLOBAL number &globalVar;", "Global variable declaration")]
    [InlineData("COMPONENT any &compVar;", "Component variable declaration")]
    public void Should_Parse_Variable_Declarations(string statement, string description)
    {
        var parseTree = ProgramParser.Parse(statement);
        parseTree.Should().NotBeNull(description);
    }

    [Theory]
    [InlineData("IF &condition THEN &result = 1; END-IF;", "Simple IF statement")]
    [InlineData("IF &condition THEN &result = 1; ELSE &result = 2; END-IF;", "IF-ELSE statement")]
    [InlineData("IF &a > 0 THEN IF &b > 0 THEN &result = 1; END-IF; END-IF;", "Nested IF statements")]
    public void Should_Parse_Conditional_Statements(string statement, string description)
    {
        var parseTree = ProgramParser.Parse(statement);
        parseTree.Should().NotBeNull(description);
    }

    [Theory]
    [InlineData("FOR &i = 1 TO 10 &sum = &sum + &i; END-FOR;", "Simple FOR loop")]
    [InlineData("FOR &i = 1 TO 10 STEP 2 &sum = &sum + &i; END-FOR;", "FOR loop with STEP")]
    [InlineData("WHILE &condition &counter = &counter + 1; END-WHILE;", "WHILE loop")]
    [InlineData("REPEAT &counter = &counter + 1; UNTIL &counter > 10;", "REPEAT-UNTIL loop")]
    public void Should_Parse_Loop_Statements(string statement, string description)
    {
        var parseTree = ProgramParser.Parse(statement);
        parseTree.Should().NotBeNull(description);
    }

    [Theory]
    [InlineData("EVALUATE &value WHEN 1 &result = \"one\"; WHEN 2 &result = \"two\"; END-EVALUATE;", "EVALUATE statement")]
    [InlineData("EVALUATE &value WHEN > 0 &result = \"positive\"; WHEN-OTHER &result = \"other\"; END-EVALUATE;", "EVALUATE with comparison and OTHER")]
    public void Should_Parse_Evaluate_Statements(string statement, string description)
    {
        var parseTree = ProgramParser.Parse(statement);
        parseTree.Should().NotBeNull(description);
    }

    [Theory]
    [InlineData("TRY &result = &risky(); CATCH Exception &e &error = &e; END-TRY;", "TRY-CATCH statement")]
    [InlineData("TRY &result = &risky(); CATCH MyException &e &handleSpecific(); CATCH Exception &e &handleGeneral(); END-TRY;", "Multiple CATCH clauses")]
    public void Should_Parse_Exception_Handling_Statements(string statement, string description)
    {
        var parseTree = ProgramParser.Parse(statement);
        parseTree.Should().NotBeNull(description);
    }

    [Theory]
    [InlineData("RETURN &result;", "RETURN with value")]
    [InlineData("RETURN;", "RETURN without value")]
    [InlineData("EXIT 1;", "EXIT with code")]
    [InlineData("EXIT;", "EXIT without code")]
    [InlineData("BREAK;", "BREAK statement")]
    [InlineData("CONTINUE;", "CONTINUE statement")]
    [InlineData("THROW &exception;", "THROW statement")]
    [InlineData("ERROR &message;", "ERROR statement")]
    [InlineData("WARNING &message;", "WARNING statement")]
    public void Should_Parse_Jump_Statements(string statement, string description)
    {
        var parseTree = ProgramParser.Parse(statement);
        parseTree.Should().NotBeNull(description);
    }

    [Theory]
    [InlineData("&obj.Method();", "Method call statement")]
    [InlineData("&variable = &value;", "Assignment statement")]
    [InlineData("%SUPER = &value;", "Super assignment statement")]
    public void Should_Parse_Expression_Statements(string statement, string description)
    {
        var parseTree = ProgramParser.Parse(statement);
        parseTree.Should().NotBeNull(description);
    }

    [Fact]
    public void Should_Parse_Complex_Statement_Block()
    {
        var sourceCode = @"
        LOCAL string &name = ""test"";
        LOCAL number &counter = 0;
        
        IF &name <> """" THEN
            FOR &i = 1 TO 5
                &counter = &counter + 1;
                IF &counter > 3 THEN
                    BREAK;
                END-IF;
            END-FOR;
            
            TRY
                &result = SomeFunction(&name, &counter);
            CATCH Exception &e
                ERROR ""An error occurred: "" | &e.Message;
            END-TRY;
        ELSE
            WARNING ""Name is empty"";
        END-IF;
        ";

        var parseTree = ProgramParser.Parse(sourceCode);
        parseTree.Should().NotBeNull("Complex statement block should parse successfully");
    }
}