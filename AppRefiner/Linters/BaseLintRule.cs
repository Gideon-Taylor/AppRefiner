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
        
        // The suppression listener for this linter
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
            if (SuppressionListener == null || !IsSuppressed(report, SuppressionListener))
            {
                Reports.Add(report);
            }
        }
        
        // Finds all suppression directives that apply to the given report
        [Obsolete("Use LinterSuppressionListener instead")]
        public static List<string> GetSuppressedWarnings(IList<IToken> comments, int line)
        {
            var suppressions = new List<string>();
            
            // Look for #pragma suppress directives in comments preceding the line
            foreach (var comment in comments)
            {
                int commentLine = comment.Line;
                
                // Only check comments that are on the lines preceding the current line
                if (commentLine < line)
                {
                    string commentText = comment.Text;
                    
                    // Extract suppression directives using regex
                    // Format: REM #pragma suppress LINTER_ID:REPORT_NUMBER [, LINTER_ID:REPORT_NUMBER]
                    Match match = Regex.Match(commentText, @"(?:REM|/\*)\s*#AppRefiner\s+suppress\s+(.+?)(?:\*/|\s*$)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string suppressionList = match.Groups[1].Value.Trim();
                        
                        // Parse comma-separated list of suppressions
                        foreach (var suppression in suppressionList.Split(','))
                        {
                            string trimmedSuppression = suppression.Trim();
                            if (!string.IsNullOrEmpty(trimmedSuppression))
                            {
                                suppressions.Add(trimmedSuppression);
                            }
                        }
                    }
                }
                // Skip comments that are after the current line
                else if (commentLine > line)
                {
                    break;
                }
            }
            
            return suppressions;
        }
        
        // Checks if a specific report should be suppressed based on the suppression listener
        public static bool IsSuppressed(Report report, LinterSuppressionListener suppressionListener)
        {
            if (suppressionListener == null || report == null)
                return false;
                
            return suppressionListener.IsSuppressed(report.LinterId, report.ReportNumber, report.Line);
        }
    }
}
