using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;

namespace AppRefiner.Stylers
{


    public class UnusedImports : BaseStyler
    {
        private class ImportInfo
        {
            public ImportNode Node;
            public bool Used;
        }

        Dictionary<string, ImportInfo> importUseMap = new();

        public override string Description => "Unused imports";

        private void MarkAppClassTypeAsUsed(AppClassTypeNode appClassType)
        {
            if (importUseMap.TryGetValue(appClassType.QualifiedName, out ImportInfo fullImport))
            {
                fullImport.Used = true;
            }

            var packagePath = string.Join(":", appClassType.PackagePath);
            if (importUseMap.TryGetValue(packagePath, out ImportInfo wildcardImport))
            {
                wildcardImport.Used = true;
            }
        }

        private void MarkAppClassTypeAsUsed(string qualifiedName)
        {
            if (importUseMap.TryGetValue(qualifiedName, out ImportInfo fullImport))
            {
                fullImport.Used = true;
            }

            var packagePath = string.Join(":", qualifiedName.Split(':')[..^1]);
            if (importUseMap.TryGetValue(packagePath, out ImportInfo wildcardImport))
            {
                wildcardImport.Used = true;
            }
        }

        public override void VisitProgram(ProgramNode node)
        {
            importUseMap.Clear();
            base.VisitProgram(node);
            foreach (var import in importUseMap.Values)
            {
                if (import.Used == false)
                {
                    AddIndicator(import.Node.SourceSpan, IndicatorType.TEXTCOLOR, 0xFF808080, $"Unused import");
                }
            }
        }

        public override void VisitAppClass(AppClassNode node)
        {
            base.VisitAppClass(node);

            if (node.BaseType != null && node.BaseType is AppClassTypeNode baseType)
            {
                MarkAppClassTypeAsUsed(baseType);
            }
        }

        public override void VisitImport(ImportNode node)
        {
            var packageKey = node.FullPath.Replace(":*", "");
            if (importUseMap.ContainsKey(packageKey) == false)
            {
                importUseMap[packageKey] = new() { Node = node, Used = false };
            }
            else
            {
                /* duplicate import */
                AddIndicator(node.SourceSpan, IndicatorType.TEXTCOLOR, 0xFFFFA500, "Duplicate import");
            }
        }

        public override void VisitObjectCreation(ObjectCreationNode node)
        {
            if (node.Type is AppClassTypeNode appClassType)
            {
                MarkAppClassTypeAsUsed(appClassType);
            }

            var inferredType = node.GetInferredType();
            if ( inferredType != null && inferredType is AppClassTypeInfo acti) 
            {
                MarkAppClassTypeAsUsed(acti.QualifiedName);
            }

            base.VisitObjectCreation(node);
        }

        public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
        {
            if (node.Type is AppClassTypeNode appClassType)
            {
                MarkAppClassTypeAsUsed(appClassType);
                var inferredType = appClassType.GetInferredType();
                if (inferredType != null && inferredType is AppClassTypeInfo acti)
                {
                    MarkAppClassTypeAsUsed(acti.QualifiedName);
                }
            }
            else if (node.Type is ArrayTypeNode arrayTypeNode && arrayTypeNode.ElementType is AppClassTypeNode arrayClassType)
            {
                MarkAppClassTypeAsUsed(arrayClassType);
                var inferredType = arrayTypeNode.GetInferredType();
                if (inferredType != null && inferredType is ArrayTypeInfo arrTypeInfo && arrTypeInfo.ElementType is AppClassTypeInfo acti)
                {
                    MarkAppClassTypeAsUsed(acti.QualifiedName);
                }
            }
            base.VisitLocalVariableDeclaration(node);
        }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            if (node.Type is AppClassTypeNode appClassType)
            {
                MarkAppClassTypeAsUsed(appClassType);
                var inferredType = appClassType.GetInferredType();
                if (inferredType != null && inferredType is AppClassTypeInfo acti)
                {
                    MarkAppClassTypeAsUsed(acti.QualifiedName);
                }
            }
            else if (node.Type is ArrayTypeNode arrayTypeNode && arrayTypeNode.ElementType is AppClassTypeNode arrayClassType)
            {
                MarkAppClassTypeAsUsed(arrayClassType);
                var inferredType = arrayTypeNode.GetInferredType();
                if (inferredType != null && inferredType is ArrayTypeInfo arrTypeInfo && arrTypeInfo.ElementType is AppClassTypeInfo acti)
                {
                    MarkAppClassTypeAsUsed(acti.QualifiedName);
                }
            }
            base.VisitLocalVariableDeclarationWithAssignment(node);
        }

        public override void VisitTypeCast(TypeCastNode node)
        {
            if (node.TargetType is AppClassTypeNode appClassType)
            {
                MarkAppClassTypeAsUsed(appClassType);
            }
            else if (node.TargetType is ArrayTypeNode arrayTypeNode && arrayTypeNode.ElementType is AppClassTypeNode arrayClassType)
            {
                MarkAppClassTypeAsUsed(arrayClassType);
            }
            base.VisitTypeCast(node);
        }

        public override void VisitProgramVariable(ProgramVariableNode node)
        {
            if (node.Type is AppClassTypeNode appClassType)
            {
                MarkAppClassTypeAsUsed(appClassType);
            }
            else if (node.Type is ArrayTypeNode arrayTypeNode && arrayTypeNode.ElementType is AppClassTypeNode arrayClassType)
            {
                MarkAppClassTypeAsUsed(arrayClassType);
            }
            base.VisitProgramVariable(node);
        }

    }
}
