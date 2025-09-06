using AppRefiner.Linters;
using PeopleCodeParser.SelfHosted.Nodes;

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

        public override void VisitMethod(MethodNode node)
        {
            int startLine = node.SourceSpan.Start.Line;
            int stopLine = node.SourceSpan.End.Line;
            int methodLength = stopLine - startLine + 1;

            if (methodLength > MaxMethodLength)
            {
                // Use the helper to add reports. Reports requires a unique ReportNumber per linter run
                // For simple linters, 1 is often sufficient.
                AddReport(
                    1,
                    $"Method '{node.Name}' is {methodLength} lines long (max {MaxMethodLength}).",
                    Type,
                    startLine, // Report on the starting line
                    node.NameToken.SourceSpan
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