using AppRefiner.Database;
using AppRefiner.Linters;
using AppRefiner.Shared.SQL;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers;

/// <summary>
/// Highlights potential issues with SQL variable counts (CreateSQL/SQLExec) using shared validation logic.
/// Ported from ANTLR-based SQLVariableCountStyler to work with self-hosted parser AST.
/// </summary>
public class SQLVariableCountStyler : BaseStyler
{
    // Corrected BGRA format colors
    private const uint ErrorColor = 0x0000FFFF;   // Opaque Red
    private const uint WarningColor = 0x00FFFF00; // Opaque Yellow

    private readonly SQLValidationContext validationContext;
    private readonly SQLVariableValidator createSqlValidator;
    private readonly SQLExecValidator sqlExecValidator;

    public SQLVariableCountStyler()
    {
        // Initialize validation context and validators
        validationContext = new SQLValidationContext();
        createSqlValidator = new SQLVariableValidator(validationContext);
        sqlExecValidator = new SQLExecValidator(validationContext);
    }

    public override string Description => "SQL variable count";

    public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

    public override void VisitProgram(ProgramNode node)
    {
        // Set up validation context
        validationContext.DataManager = DataManager;

        // Reset validators
        createSqlValidator.Reset();
        sqlExecValidator.Reset();

        // Clear any previous indicators
        Reset();

        // Visit the AST
        base.VisitProgram(node);
    }

    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        // Validate SQL variable declarations
        var reports = createSqlValidator.ValidateVariableDeclaration(node);
        ProcessReports(reports);

        base.VisitLocalVariableDeclaration(node);
    }

    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        // Validate SQL variable declarations with assignment
        var reports = createSqlValidator.ValidateVariableDeclarationWithAssignment(node);
        ProcessReports(reports);

        base.VisitLocalVariableDeclarationWithAssignment(node);
    }

    public override void VisitFunctionCall(FunctionCallNode node)
    {
        var allReports = new List<Report>();

        // Validate CreateSQL/GetSQL calls
        var createSqlReports = createSqlValidator.ValidateCreateSQL(node);
        allReports.AddRange(createSqlReports);

        // Validate SQLExec calls
        var sqlExecReports = sqlExecValidator.ValidateSQLExec(node);
        allReports.AddRange(sqlExecReports);

        ProcessReports(allReports);

        base.VisitFunctionCall(node);
    }

    public override void VisitExpressionStatement(ExpressionStatementNode node)
    {
        base.VisitExpressionStatement(node);
    }

    public override void VisitMemberAccess(MemberAccessNode node)
    {
        // Check if this member access is followed by a function call (method call pattern)
        // We need to look at the parent context to see if this is part of a method call
        if (node.Parent is FunctionCallNode functionCall && functionCall.Function == node)
        {
            var reports = createSqlValidator.ValidateSQLMethodCall(node, functionCall);
            ProcessReports(reports);
        }

        base.VisitMemberAccess(node);
    }

    private void ProcessReports(IEnumerable<Report> reports)
    {
        if (reports == null) return;

        // Filter out Info reports and process Errors and Warnings
        foreach (var report in reports.Where(r => r.Type != ReportType.Info))
        {
            // Ensure span values are valid
            if (report.Span.Start < 0 || report.Span.Stop < report.Span.Start) continue;

            uint color = report.Type switch
            {
                ReportType.Error => ErrorColor,
                ReportType.Warning => WarningColor,
                _ => WarningColor
            };

            AddIndicator(
                new SourceSpan(report.Span.Start, report.Span.Stop),
                IndicatorType.SQUIGGLE,
                color,
                report.Message
            );
        }
    }

    protected override void OnReset()
    {
        base.OnReset();

        // Reset validators when the styler is reset
        createSqlValidator?.Reset();
        sqlExecValidator?.Reset();
    }
}