#if DEBUG
// Debug-only: a diagnostic for type inference work, excluded from Release builds.
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Diagnostic linter that marks where type inference gave up: an expression typed Any,
    /// Unknown or an untyped ApiObject whose own inputs were typed (weak-spots §4). Only the
    /// origin is reported, not every expression the unknown flows into, so each note is a
    /// candidate fix. Declared <c>any</c> variables are deliberate and not reported.
    /// Inactive by default; run via the command palette ("Lint: Annotate untyped expressions")
    /// or enable it and use Editor: Lint Current Code.
    /// </summary>
    public class UntypedExpressionLinter : BaseLintRule
    {
        public override string LINTER_ID => "UNTYPED_EXPRESSION";

        public override bool RequiresTypeInference => true;

        public UntypedExpressionLinter()
        {
            Description = "Annotate untyped expressions";
            Type = ReportType.Info;
            Active = false;
        }

        public override void VisitProgram(ProgramNode node)
        {
            var reported = new HashSet<(int Line, string Message)>();
            foreach (var expression in node.FindDescendants<ExpressionNode>())
            {
                if (Explain(expression) is not { } message)
                    continue;

                int line = expression.SourceSpan.Start.Line;
                if (reported.Add((line, message)))
                    AddReport(1, message, Type, line, expression.SourceSpan);
            }

            // No base.VisitProgram: expressions are walked via FindDescendants and this
            // diagnostic needs no scope tracking.
        }

        /// <summary>
        /// Why <paramref name="expression"/> is where typing gave up, or null when its type is
        /// known, it wasn't typed at all, or it only inherited an unknown from an input.
        /// </summary>
        private static string? Explain(ExpressionNode expression)
        {
            var type = expression.GetInferredType();
            if (type == null || !GaveUp(type))
                return null;

            switch (expression)
            {
                // A method's target is reported with its call
                case MemberAccessNode member when member.Parent is FunctionCallNode call && call.Function == member:
                    return null;

                case MemberAccessNode { IsDynamic: false } member:
                    return FromTarget(member.Target, $"{Describe(member.Target)}.{member.MemberName}", type);

                case FunctionCallNode { Function: MemberAccessNode method } call:
                    return FromTarget(method.Target, $"{Describe(method.Target)}.{method.MemberName}()", type);

                case FunctionCallNode { Function: IdentifierNode function }:
                    return $"Untyped: {function.Name}() returns {Name(type)}";

                case IdentifierNode identifier when type is UnknownTypeInfo:
                    return $"Untyped: {identifier.Name} is unresolved";

                case ObjectCreationNode creation when type is UnknownTypeInfo:
                    return $"Untyped: class {creation.Type.TypeName} is unresolved";

                case ArrayAccessNode access:
                    return FromTarget(access.Array, $"element of {Describe(access.Array)}", type);

                case BinaryOperationNode binary
                    when !GaveUp(binary.Left.GetInferredType()) && !GaveUp(binary.Right.GetInferredType()):
                    return $"Untyped: {binary.Operator} of {Name(binary.Left.GetInferredType())} and {Name(binary.Right.GetInferredType())} is {Name(type)}";

                default:
                    return null;
            }
        }

        private static string? FromTarget(ExpressionNode target, string what, TypeInfo type)
        {
            var targetType = target.GetInferredType();
            if (targetType is BuiltinObjectTypeInfo { IsApiObjectBase: true })
                return $"Untyped: {what} is {Name(type)} (target is an untyped ApiObject)";

            return GaveUp(targetType) ? null : $"Untyped: {what} is {Name(type)} (on {Name(targetType)})";
        }

        /// <summary>
        /// Any, Unknown and the untyped ApiObject base: the sentinels checks accept without
        /// knowing the type. A missing type counts too, so it isn't blamed on what consumes it.
        /// </summary>
        private static bool GaveUp(TypeInfo? type) =>
            type is null or AnyTypeInfo or UnknownTypeInfo or BuiltinObjectTypeInfo { IsApiObjectBase: true };

        private static string Name(TypeInfo? type) => type switch
        {
            null => "untyped",
            UnknownTypeInfo unknown => unknown.Name == "unknown" ? "unknown" : $"unknown '{unknown.Name}'",
            _ => type.Name
        };

        private static string Describe(ExpressionNode expression)
        {
            var text = expression.ToString() ?? "";
            return text.Length <= 40 ? text : text[..37] + "...";
        }
    }
}
#endif
