using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.ComponentInterfaces;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Contracts;
using System.Text;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Shows a component interface property's type, record field, access and comment when hovering
    /// over it on a named CI or a known item. Uses the CI definition the editor's type resolver
    /// looks up, so nothing is shown unless that lookup finds it.
    /// </summary>
    public class ComponentInterfaceTooltipProvider : BaseTooltipProvider
    {
        private ITypeMetadataResolver? typeResolver;

        public override string Name => "Component Interface Properties";

        public override string Description => "Shows component interface property details when hovering over them";

        /// <summary>
        /// Above PeopleSoft object info and the inferred type
        /// </summary>
        public override int Priority => 75;

        /// <summary>
        /// The definition comes through the type resolver, which answers Unknown without a database
        /// </summary>
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.NotRequired;

        public override bool CanProvideTooltipAt(ScintillaEditor editor, ProgramNode program, List<Token> tokens, int cursorPosition, int lineNumber)
        {
            typeResolver = editor.AppDesignerProcess?.TypeResolver;
            return typeResolver != null && base.CanProvideTooltipAt(editor, program, tokens, cursorPosition, lineNumber);
        }

        public override void VisitMemberAccess(MemberAccessNode node)
        {
            base.VisitMemberAccess(node);

            if (node.IsDynamic || !node.MemberNameSpan.ContainsPosition(CurrentPosition))
                return;

            // A method call's name is the method-parameters tooltip's
            if (node.Parent is FunctionCallNode call && ReferenceEquals(call.Function, node))
                return;

            if (node.Target.GetInferredType() is not { } targetType
                || CompIntfcTarget.Find(targetType, typeResolver) is not { } ci
                || ci.FindProperty(node.MemberName) is not { } property)
                return;

            RegisterTooltip(node.MemberNameSpan, FormatProperty(property));
        }

        private static string FormatProperty(CompIntfcProperty property)
        {
            var sb = new StringBuilder();
            sb.Append($"{property.Name}: {CompIntfcTarget.PropertyValueType(property).Name}");
            sb.Append($"\nRecord field: {property.RecordName}.{property.FieldName}");
            if (property.IsReadOnly)
                sb.Append("\nRead-only");
            if (!string.IsNullOrWhiteSpace(property.Comment))
                sb.Append($"\n{property.Comment.Trim()}");
            return sb.ToString();
        }
    }
}
