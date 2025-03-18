using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AppRefiner.Linters
{
    public static class WarningSuppressionHelper
    {
        /// <summary>
        /// Filters reports based on suppression directives in the comments
        /// </summary>
        /// <param name="reports">Original list of reports</param>
        /// <param name="comments">Comments from the source code</param>
        /// <returns>Filtered list of reports (excluding suppressed ones)</returns>
        public static List<Report> FilterSuppressedReports(List<Report> reports, IList<IToken> comments)
        {
            if (reports == null || comments == null || !reports.Any())
                return reports ?? new List<Report>();

            var result = new List<Report>();
            
            foreach (var report in reports)
            {
                if (!BaseLintRule.IsSuppressed(report, comments))
                {
                    result.Add(report);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Parses a line with pragma suppress directive and returns a list of suppressed report IDs
        /// </summary>
        /// <param name="line">Comment line to parse</param>
        /// <returns>List of suppressed report IDs in "linterId:reportNumber" format</returns>
        public static List<string> ParseSuppressionDirective(string line)
        {
            var result = new List<string>();
            
            // Look for #pragma suppress directive
            Match match = Regex.Match(line, @"(?:REM|/\*)\s*#pragma\s+suppress\s+(.+?)(?:\*/|\s*$)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string suppressionList = match.Groups[1].Value.Trim();
                
                // Parse comma-separated list of suppressions
                foreach (var suppression in suppressionList.Split(','))
                {
                    string trimmedSuppression = suppression.Trim();
                    if (!string.IsNullOrEmpty(trimmedSuppression))
                    {
                        result.Add(trimmedSuppression);
                    }
                }
            }
            
            return result;
        }
    }
}
