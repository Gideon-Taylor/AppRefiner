using AppRefiner.Linters.Models;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Linters
{
    public class UseOfObjectType : BaseLintRule
    {
        public override string LINTER_ID => "OBJECT_TYPE";

        public UseOfObjectType()
        {
            Description = "Check for variables declared as 'object' that are assigned specific types.";
            Type = ReportType.Warning;
            Active = true;
        }

        private bool IsObjectType(TypeNode typeNode)
        {
            if (typeNode is BuiltInTypeNode builtInType)
            {
                return builtInType.Type == PeopleCodeTypeInfo.Types.PeopleCodeType.Object;
            }
            return false;
        }

        // Track object variables that are declared
        private readonly HashSet<string> objectVariables = new();

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            base.VisitLocalVariableDeclarationWithAssignment(node);

            if (!IsObjectType(node.Type))
                return;

            objectVariables.Add(node.VariableName);

            // Check if the assignment is an object creation
            if (node.InitialValue is ObjectCreationNode)
            {
                AddReport(
                    1,
                    "Variable is declared as 'object' but assigned a specific type.",
                    ReportType.Warning,
                    node.SourceSpan.Start.Line,
                    node.SourceSpan
                );
            }
        }

        public override void VisitAssignment(AssignmentNode node)
        {
            base.VisitAssignment(node);

            // Check if left side is a variable we're tracking as object type
            if (node.Target is IdentifierNode identNode && 
                GetAccessibleVariables(GetCurrentScope()).Where(v => v.Name == identNode.Name && v.Type.ToLower() == "object").Any())
            {
                // Check if right side is an object creation
                if (node.Value is ObjectCreationNode)
                {
                    AddReport(
                        1,
                        "Variable is declared as 'object' but assigned a specific type.",
                        ReportType.Warning,
                        node.SourceSpan.Start.Line,
                        node.SourceSpan
                    );
                }
            }
        }

        public new void Reset()
        {
            base.Reset();
            objectVariables.Clear();
        }
    }
}
