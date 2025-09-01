using AppRefiner.Database;
using AppRefiner.Shared.SQL;
using AppRefiner.Shared.SQL.Models;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace AppRefiner.Linters
{
    /* Issues:
     * If SQLText is "", then we cannot validate any usage of this SQL variable. (Open/Exec/Fetch/etc)
     */
    public class CreateSQLVariableCount : BaseLintRule
    {
        public override string LINTER_ID => "CREATE_SQL";

        private readonly SQLValidationContext validationContext;
        private readonly SQLVariableValidator validator;

        public CreateSQLVariableCount()
        {
            Description = "Validate bind counts in CreateSQL objects.";
            Type = ReportType.Error;
            Active = true;

            validationContext = new SQLValidationContext { DataManager = DataManager };
            validator = new SQLVariableValidator(validationContext);
        }

        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        public override void Reset()
        {
            base.Reset();
            validator.Reset();
        }

        public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
        {
            // Use shared validator to handle SQL variable declarations
            var reports = validator.ValidateVariableDeclaration(node);
            ProcessReports(reports);
            base.VisitLocalVariableDeclaration(node);
        }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            // Use shared validator to handle SQL variable declarations with assignment
            var reports = validator.ValidateVariableDeclarationWithAssignment(node);
            ProcessReports(reports);
            base.VisitLocalVariableDeclarationWithAssignment(node);
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            // Handle CreateSQL and GetSQL function calls
            var reports = validator.ValidateCreateSQL(node);
            ProcessReports(reports);

            // Handle SQL method calls (converted from VisitMethodCall)
            if (node.Function is MemberAccessNode memberAccess)
            {
                var methodReports = validator.ValidateSQLMethodCall(memberAccess, node);
                ProcessReports(methodReports);
            }
            
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