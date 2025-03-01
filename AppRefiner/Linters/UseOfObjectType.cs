using Antlr4.Runtime.Misc;
using AppRefiner.Linters.Models;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    public class UseOfObjectType : ScopedLintRule<VariableInfo>
    {
        public UseOfObjectType()
        {
            Description = "Check for variables declared as 'object' that are assigned specific types.";
            Type = ReportType.Warning;
            Active = true;
        }

        private bool IsObjectType(TypeTContext typeContext)
        {
            if (typeContext is SimpleTypeTypeContext simpleType)
            {
                if (simpleType.simpleType() is SimpleGenericIDContext genericId)
                {
                    return genericId.GENERIC_ID_LIMITED()?.GetText().Equals("object", StringComparison.OrdinalIgnoreCase) ?? false;
                }
            }
            return false;
        }

        public override void EnterLocalVariableDefinition(LocalVariableDefinitionContext context)
        {
            base.EnterLocalVariableDefinition(context);

            if (!IsObjectType(context.typeT()))
                return;

            foreach (var varNode in context.USER_VARIABLE())
            {
                var varName = varNode.GetText();
                // Add to current scope via the base class method
                AddLocalVariable(
                    varName,
                    "object",
                    varNode.Symbol.Line,
                    varNode.Symbol.StartIndex,
                    varNode.Symbol.StopIndex
                );
            }
        }

        public override void EnterLocalVariableDeclAssignment(LocalVariableDeclAssignmentContext context)
        {
            base.EnterLocalVariableDeclAssignment(context);

            if (!IsObjectType(context.typeT()))
                return;

            var varName = context.USER_VARIABLE().GetText();
            // Add to current scope
            AddLocalVariable(
                varName,
                "object",
                context.USER_VARIABLE().Symbol.Line,
                context.USER_VARIABLE().Symbol.StartIndex,
                context.USER_VARIABLE().Symbol.StopIndex
            );

            // Check if the assignment is a create expression
            if (context.expression() is ObjectCreateExprContext)
            {
                Reports?.Add(new Report
                {
                    Type = ReportType.Warning,
                    Line = context.Start.Line - 1,
                    Span = (context.Start.StartIndex, context.Stop.StopIndex),
                    Message = $"Variable is declared as 'object' but assigned a specific type."
                });
            }
        }

        public override void EnterEqualityExpr([NotNull] EqualityExprContext context)
        {
            // Only interested in assignments (=), not equality comparisons
            if (context.EQ() == null)
                return;

            // Check if left side is a variable we're tracking
            if (context.expression(0) is IdentifierExprContext identExpr)
            {
                var varName = identExpr.ident().GetText();
                if (TryGetVariableInfo(varName, out var info) && info?.Type.Equals("object", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Check if right side is a create expression
                    if (context.expression(1) is ObjectCreateExprContext)
                    {
                        Reports?.Add(new Report
                        {
                            Type = ReportType.Warning,
                            Line = context.Start.Line - 1,
                            Span = (context.Start.StartIndex, context.Stop.StopIndex),
                            Message = $"Variable is declared as 'object' but assigned a specific type."
                        });
                    }
                }
            }
        }
    }
}
