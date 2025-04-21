using AppRefiner.Stylers;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using System.Collections.Generic;
using Antlr4.Runtime.Misc;

namespace PluginSample
{
    /// <summary>
    /// Sample styler that uses the parse tree context to highlight method names.
    /// </summary>
    public class SampleStyler : BaseStyler
    {
        private const uint METHOD_NAME_COLOR = 0x90EE90; // Light Green

        public SampleStyler()
        {
            Description = "Sample: Highlights method names green.";
            Active = true;
        }

        // Override the method called when entering a method header in the parse tree
        public override void EnterMethodHeader(MethodHeaderContext context)
        {
            // Get the terminal node representing the method's name (genericID)
            var methodNameNode = context.genericID();
            if (methodNameNode != null)
            {
                // Add an indicator to the list
                Indicators?.Add(new Indicator
                {
                    Start = methodNameNode.Start.StartIndex,
                    Length = methodNameNode.Stop.StopIndex - methodNameNode.Start.StartIndex + 1,
                    Color = METHOD_NAME_COLOR,
                    Type = IndicatorType.HIGHLIGHTER, // Use HIGHLIGHTER for background color
                    Tooltip = "Sample Styler: Method Name"
                });
            }
        }

        // Override the method called when entering a method implementation 
        public override void EnterMethod([NotNull] MethodContext context)
        {
            // Get the terminal node representing the method's name (genericID)
            var methodNameNode = context.genericID();
            if (methodNameNode != null)
            {
                // Add an indicator to the list
                Indicators?.Add(new Indicator
                {
                    Start = methodNameNode.Start.StartIndex,
                    Length = methodNameNode.Stop.StopIndex - methodNameNode.Start.StartIndex + 1,
                    Color = METHOD_NAME_COLOR,
                    Type = IndicatorType.HIGHLIGHTER, // Use HIGHLIGHTER for background color
                    Tooltip = "Sample Styler: Method Name"
                });
            }

        }

        // Always provide a Reset method, even if it just calls the base
        public override void Reset()
        {
            base.Reset(); // Ensures Indicators list is cleared/reinitialized
        }
    }
}
