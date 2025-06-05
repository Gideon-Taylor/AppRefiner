using AppRefiner.Linters;
using Antlr4.Runtime.Tree;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using AppRefiner.Database;
using AppRefiner.PeopleCode; // Added for Report
using AppRefiner.Linters.Models; // Added for ReportType enum and Report class
using System.Linq; // Added for Where clause
using System.Collections.Generic; // Added for List

namespace AppRefiner.Stylers
{
    public class SQLVariableCountStyler : BaseStyler
    {
        // Corrected BGRA format (BBGGRRAA)
        private const uint ErrorColor = 0x0000FFFF;   // Opaque Red
        private const uint WarningColor = 0x00FFFF00; // Opaque Yellow

        public SQLVariableCountStyler()
        {
            Description = "Highlights potential issues with SQL variable counts (CreateSQL/SQLExec).";
            Active = true; // Set to true to enable by default, or manage externally
        }

        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        public override void ExitProgram(ProgramContext context)
        {
            base.ExitProgram(context);

            if (Indicators == null)
            {
                Indicators = [];
            }

            var createSqlLinter = new CreateSQLVariableCount { DataManager = this.DataManager };
            var sqlExecLinter = new SQLExecVariableCount { DataManager = this.DataManager };
            List<Report> linterReports = [];
            createSqlLinter.Reports = linterReports;
            sqlExecLinter.Reports = linterReports;

            // Walk the tree with both linters
            MultiParseTreeWalker walker = new MultiParseTreeWalker();
            walker.AddListener(createSqlLinter);
            walker.AddListener(sqlExecLinter);
            
            walker.Walk(context);

            // Collect reports and create indicators
            ProcessReports(createSqlLinter.Reports);
        }

        private void ProcessReports(List<Report> reports)
        {
            if (reports == null || Indicators == null) return;

            // Filter out Info reports and process Errors and Warnings
            foreach (var report in reports.Where(r => r.Type != ReportType.Info))
            {
                // Ensure span values are valid
                if (report.Span.Start < 0 || report.Span.Stop < report.Span.Start) continue;

                uint color;
                switch (report.Type)
                {
                    case ReportType.Error:
                        color = ErrorColor;
                        break;
                    case ReportType.Warning:
                        color = WarningColor;
                        break;
                    default: // Should not happen due to Where clause, but good practice
                        continue;
                }

                Indicators.Add(new Indicator
                {
                    Start = report.Span.Start,
                    Length = report.Span.Stop - report.Span.Start + 1,
                    Color = color, // Use the determined color based on severity
                    Tooltip = report.Message,
                    Type = IndicatorType.SQUIGGLE,
                    QuickFixes = []
                });
            }
        }

        public override void Reset()
        {
            base.Reset();
            // Any additional reset logic specific to this styler can go here
        }
    }
} 