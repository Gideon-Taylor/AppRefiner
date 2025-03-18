using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.Linters
{
    public enum ReportType
    {
        GrayOut,
        Style,
        Error,
        Warning,
        Info
    }
    public class Report
    {
        public string Message = "";
        public ReportType Type;
        public int Line;
        public (int Start, int Stop) Span;
        public string? Text;
        public string? Detail;
        public bool IsError => Type == ReportType.Error;
        public bool IsWarning => Type == ReportType.Warning;
        public bool IsInfo => Type == ReportType.Info;
        
        // New fields for identifying reports for suppression
        public string LinterId { get; set; } = string.Empty;
        public int ReportNumber { get; set; }
        
        // Helper method to get the full identifier of a report (for suppression)
        public string GetFullId() => $"{LinterId}:{ReportNumber}";
    }
}
