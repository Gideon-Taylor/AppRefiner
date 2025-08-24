using System;
using System.Collections.Generic;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Scoped.Models;

namespace PeopleCodeParser.SelfHosted.Scoped.Examples;

/// <summary>
/// Example showing how to use the UnusedVariablesVisitor in practice
/// </summary>
public class UnusedVariablesExample
{
    public static void AnalyzeCode(string sourceCode)
    {
        // First tokenize the source code
        var lexer = new Lexing.PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        
        // Parse the tokens
        var parser = new PeopleCodeParser(tokens);
        ProgramNode program = parser.ParseProgram();
        
        if (parser.Errors.Count > 0)
        {
            Console.WriteLine($"Parsed with {parser.Errors.Count} errors.");
            foreach (var error in parser.Errors)
            {
                Console.WriteLine($"  - {error.Message} at {error.Location}");
            }
        }
        
        // Create and run the visitor
        var visitor = new UnusedVariablesVisitor();
        program.Accept(visitor);
        
        // Display the results
        if (visitor.Indicators.Count == 0)
        {
            Console.WriteLine("No unused variables found!");
            return;
        }
        
        Console.WriteLine($"Found {visitor.Indicators.Count} unused variables:");
        foreach (var indicator in visitor.Indicators)
        {
            Console.WriteLine($"- {indicator.Tooltip} at position {indicator.Start}-{indicator.Start + indicator.Length}");
        }
    }
    
    // Example of how to apply indicators to an editor
    public static void ApplyIndicatorsToEditor(List<Indicator> indicators, object editor)
    {
        // This would be implemented based on the editor being used
        // For example, with Scintilla:
        // foreach (var indicator in indicators)
        // {
        //     editor.IndicatorStyle[indicator.Type] = ScintillaNET.IndicatorStyle.Plain;
        //     editor.IndicatorFore[indicator.Type] = Color.FromArgb((int)indicator.Color);
        //     editor.IndicatorStart = indicator.Start;
        //     editor.IndicatorEnd = indicator.Start + indicator.Length;
        //     editor.IndicatorFillRange(indicator.Start, indicator.Length);
        // }
        
        Console.WriteLine("Applied indicators to editor");
    }
    
    // Example usage
    public static void Main()
    {
        string sampleCode = File.ReadAllText(@"C:\temp\test.pcode");
        
        AnalyzeCode(sampleCode);
        
        // Expected output:
        // Found 2 unused variables:
        // - Unused parameter: &unusedParam at position X-Y
        // - Unused method parameter/variable: &localVar1 at position X-Y
        // 
        // Note that &unusedInstance is detected as unused
        // But &usedInstance, &MyProperty, &localVar2, and &usedVar are all used
    }
}
