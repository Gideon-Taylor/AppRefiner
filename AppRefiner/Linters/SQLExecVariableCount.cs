using AppRefiner.Database;
using AppRefiner.Shared.SQL;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace AppRefiner.Linters
{
    public class SQLExecVariableCount : BaseLintRule
    {
        public override string LINTER_ID => "SQL_EXEC";

        private readonly SQLValidationContext validationContext;
        private readonly SQLExecValidator validator;

        public SQLExecVariableCount()
        {
            Description = "Validate bind counts in SQLExec functions.";
            Type = ReportType.Error;
            Active = false;

            validationContext = new SQLValidationContext { DataManager = DataManager };
            validator = new SQLExecValidator(validationContext);
        }

        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        public override void Reset()
        {
            base.Reset();
            validator.Reset();
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            // Handle SQLExec function calls
            var reports = validator.ValidateSQLExec(node);
            ProcessReports(reports);
            base.VisitFunctionCall(node);
        }

        /// <summary>
        /// Helper method to process reports from the shared validator
        /// </summary>
        private void ProcessReports(List<AppRefiner.Linters.Report> reports)
        {
            if (Reports == null)
                Reports = new List<AppRefiner.Linters.Report>();

            foreach (var report in reports)
            {
                var linterReport = new AppRefiner.Linters.Report
                {
                    LinterId = LINTER_ID,
                    ReportNumber = report.ReportNumber,
                    Message = report.Message,
                    Type = report.Type,
                    Line = report.Line,
                    Span = report.Span
                };

                // Check suppression
                if (SuppressionProcessor == null ||
                    !SuppressionProcessor.IsSuppressed(linterReport.LinterId, linterReport.ReportNumber, linterReport.Line))
                {
                    Reports.Add(linterReport);
                }
            }
        }
    }
}
