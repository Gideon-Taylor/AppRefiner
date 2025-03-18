using AppRefiner.PeopleCode;
using SqlParser.Dialects;
using SqlParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using AppRefiner.Database;
using System.Text.RegularExpressions;

namespace AppRefiner.Linters
{
    public abstract class BaseLintRule : PeopleCodeParserBaseListener
    {
        // Linter ID must be set by all subclasses
        public abstract string LINTER_ID { get; }
        
        public bool Active = false;
        public string Description = "Description not set";
        public ReportType Type;
        public List<Report>? Reports;
        public virtual DataManagerRequirement DatabaseRequirement => DataManagerRequirement.NotRequired;
        public IDataManager? DataManager;

        // Add collection to store comments from lexer
        public IList<IToken>? Comments;
        
        // The suppression listener shared across all linters
        public LinterSuppressionListener? SuppressionListener;
        
        public abstract void Reset();
        
        // Helper method to create a report with the proper linter ID set and add it to the Reports list
        // if it's not suppressed by a pragma directive
        protected void AddReport(int reportNumber, string message, ReportType type, int line, (int Start, int Stop) span)
        {
            Report report = new Report
            {
                LinterId = LINTER_ID,
                ReportNumber = reportNumber,
                Message = message,
                Type = type,
                Line = line,
                Span = span
            };
            
            // Initialize Reports list if needed
            if (Reports == null)
            {
                Reports = new List<Report>();
            }
            
            // Only add the report if it's not suppressed
            if (SuppressionListener == null || !SuppressionListener.IsSuppressed(report.LinterId, report.ReportNumber, report.Line))
            {
                Reports.Add(report);
            }
        }
        
    }
}
