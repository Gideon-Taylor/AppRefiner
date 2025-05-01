using AppRefiner.Linters;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using System.Collections.Generic; // For List
using Antlr4.Runtime; // For IToken

namespace PluginSample
{
    public class SampleLinter : BaseLintRule
    {
        public override string LINTER_ID => "SAMPLE_LINTER";

        // Configurable property example
        public int MaxMethodLength { get; set; } = 50; // Lines

        public SampleLinter()
        {
            Description = "Sample: Detects methods longer than MaxMethodLength lines.";
            Type = ReportType.Warning; // Or Error, Info, Style
            Active = true; // Default to active, user can disable
        }

        public override void EnterMethod(MethodContext context)
        {
            int startLine = context.Start.Line;
            int stopLine = context.Stop.Line;
            int methodLength = stopLine - startLine + 1;

            if (methodLength > MaxMethodLength)
            {
                // Use the helper to add reports. Reports requires a unique ReportNumber per linter run
                // For simple linters, 1 is often sufficient.
                AddReport(
                    1,
                    $"Method '{context.genericID()?.GetText()}' is {methodLength} lines long (max {MaxMethodLength}).",
                    Type,
                    startLine, // Report on the starting line
                    (context.Start.StartIndex, context.Stop.StopIndex) // Span covers the whole method
                );
            }
        }

        // Always provide a Reset method to clear state between runs
        public override void Reset()
        {
            base.Reset(); // Call base Reset if needed
            // Clear any linter-specific state here
        }
    }
} 