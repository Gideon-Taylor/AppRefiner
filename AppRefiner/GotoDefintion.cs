using AppRefiner.Database;
using AppRefiner.Database.Models;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner
{
    public class GoToDefnResult
    {
        public OpenTarget? TargetProgram;
        public SourceSpan? SourceSpan;
        public string? ErrorMessage;
    }


    public class GoToDefinitionVisitor : ScopedAstVisitor<object>
    {
        private int _position;
        private IDataManager? _dataManager;
        private ProgramNode _program;
        private string? _targetFunctionName;

        public GoToDefnResult Result { get; set; }

        public GoToDefinitionVisitor(ProgramNode program, int currentPosition, IDataManager? dataManager) 
        {
            _dataManager = dataManager;
            _position = currentPosition;
            _program = program;
            Result = new(); 
        }
        public override void VisitProgram(ProgramNode node)
        {
            Result.ErrorMessage = null;
            Result.SourceSpan = null;
            Result.TargetProgram = null;
            _targetFunctionName = null;
            base.VisitProgram(node);


            /* If we have a _targetFunctionName, locate that function and go to it */
            if (_targetFunctionName != null)
            {
                var targetFunction = node.Functions.Where(f => f.Name.Equals(_targetFunctionName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (targetFunction != null)
                {
                    Result.SourceSpan = targetFunction.NameToken.SourceSpan;
                }
            }

        }

        public override void VisitMethod(MethodNode node)
        {
            base.VisitMethod(node);

            /* Cursor is on name in class header */
            if (node.NameToken.SourceSpan.ContainsPosition(_position) && node.Implementation is not null)
            {
                Result.SourceSpan = node.Implementation.NameToken.SourceSpan;
            }

            /* Cursor is on name in implementation */
            else if (node.Implementation is not null && node.Implementation.NameToken.SourceSpan.ContainsPosition(_position))
            {
                Result.SourceSpan = node.NameToken.SourceSpan;
            }
        }

        public override void VisitFunction(FunctionNode node)
        {
            base.VisitFunction(node);

            if (_dataManager is null) return;

            if (node.IsDeclaration && node.NameToken.SourceSpan.ContainsPosition(_position))
            {
                if (node.RecordName is null || node.FieldName is null || node.RecordEvent is null)
                {
                    return;
                }
                var remoteFuncOpenTarget = new OpenTarget(OpenTargetType.RecordFieldPeopleCode, 
                    node.Name, 
                    "Function declaration", 
                    [
                        (PSCLASSID.RECORD, node.RecordName),
                        (PSCLASSID.FIELD, node.FieldName),
                        (PSCLASSID.METHOD, node.RecordEvent)
                    ]);

                
                var program = GetParsedProgram(remoteFuncOpenTarget);

                if (program is null) 
                {
                    Result.ErrorMessage = $"Unable to find target program: {node.RecordName}.{node.FieldName}.{node.RecordEvent} in the database.";
                    return;
                }

                var targetFunc = program.Functions.Where(f => f.Name == node.Name).First();

                Result.SourceSpan = targetFunc.NameToken.SourceSpan;
                Result.TargetProgram = remoteFuncOpenTarget;
            }

        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            base.VisitFunctionCall(node);

            if (node.Function is IdentifierNode literalFunc && literalFunc.SourceSpan.ContainsPosition(_position))
            {
                _targetFunctionName = literalFunc.Name;
            }
        }


        public override void VisitIdentifier(IdentifierNode node)
        {
            base.VisitIdentifier(node);
            /* Handle variables and property-as-variable here */

            if (node.SourceSpan.ContainsPosition(_position))
            {
                var name = node.Name;

                /* Handle &variable */ 
                if (name.StartsWith("&")) {
                    if (_program.AppClass is not null)
                    {
                        var matchingProperty = _program.AppClass.Properties.Where(p => p.Name.Equals(name.Substring(1),StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                        if (matchingProperty != null)
                        {
                            /* there is a property matching this &variable */
                            Result.SourceSpan = matchingProperty.NameToken.SourceSpan;
                            return;
                        }
                    }

                    /* Check for any in scope variables or parameters */
                    var matchingVariable = GetVariablesInScope(GetCurrentScope()).Where(v => v.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                    if (matchingVariable != null)
                    {
                        Result.SourceSpan = matchingVariable.DeclarationNode switch
                        {
                            LocalVariableDeclarationNode declarationNode => declarationNode.VariableNameInfos.Where(i => i.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).First().SourceSpan,
                            LocalVariableDeclarationWithAssignmentNode assignmentNode => assignmentNode.VariableNameInfo.SourceSpan,
                            ProgramVariableNode varNode => varNode.NameInfos.Where(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).First().SourceSpan,
                            ParameterNode paramNode => paramNode.NameToken.SourceSpan,
                            _ => null
                        };
                    }
                }
            }

        }

        public override void VisitMemberAccess(MemberAccessNode node)
        {
            base.VisitMemberAccess(node);

            if (node.MemberNameSpan.ContainsPosition(_position))
            {
                /* Left hand is an identifer and it is %Super or %This */
                if (node.Target is IdentifierNode id && (id.Name.Equals("%this",StringComparison.OrdinalIgnoreCase) || id.Name.Equals("%super", StringComparison.OrdinalIgnoreCase)))
                {
                    var bypassSelf = id.Name.Equals("%super", StringComparison.OrdinalIgnoreCase);

                    /* We have %This.Something */
                    if (_program.AppClass == null) return;

                    var memberName = node.MemberName;
                    OpenTarget? foundTarget = null;
                    SourceSpan? foundSpan = null;



                    if (node.Parent is FunctionCallNode functionCallNode)
                    {
                        (foundTarget, foundSpan) = FindMethodForClass(_program.AppClass, memberName, bypassSelf);
                    }
                    else
                    {
                        (foundTarget, foundSpan) = FindPropertyForClass(_program.AppClass, memberName, bypassSelf);
                    }


                    if (foundTarget != null && foundSpan != null)
                    {
                        if (foundTarget.Equals(_program.AppClass))
                        {
                            /* Just need to set the span */
                            Result.SourceSpan = foundSpan;
                        }
                        else
                        {
                            /* Its in a different app class, we need to set the open target */
                            Result.TargetProgram = foundTarget;
                            Result.SourceSpan = foundSpan;
                        }
                    }
                }
            }
        }


        private (OpenTarget? targetClass, SourceSpan? span) FindMethodForClass(AppClassNode classNode, string memberName, bool skipSelf = false)
        {
            if (!skipSelf)
            {
                /* Try to send to the implementation first */
                var matchingMethod = classNode.Methods.Where(m => m.IsImplementation && m.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                /* If no implementation lets look for a method declaration */
                if (matchingMethod == null)
                {
                    matchingMethod = classNode.Methods.Where(m => m.IsDeclaration && m.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                }

                if (matchingMethod != null)
                {
                    return (null, matchingMethod.Implementation is null ? matchingMethod.NameToken.SourceSpan : matchingMethod.Implementation.NameToken.SourceSpan);
                }
            }

            /* We checked ourself and didn't find it, lets get our parent... */
            AppClassTypeNode? baseClassType = null;

            if (classNode.BaseClass is not null and AppClassTypeNode)
            {
                baseClassType = (AppClassTypeNode)classNode.BaseClass;
            }
            else if (classNode.ImplementedInterface is not null and AppClassTypeNode)
            {
                baseClassType = (AppClassTypeNode)classNode.ImplementedInterface;
            }


            if (baseClassType != null)
            {
                List<(PSCLASSID, string)> targetParts = [];

                var packageClassID = 104;
                foreach (var package in baseClassType.PackagePath)
                {
                    targetParts.Add(((PSCLASSID)packageClassID++, package));
                }

                targetParts.Add((PSCLASSID.APPLICATION_CLASS, baseClassType.ClassName));
                targetParts.Add((PSCLASSID.METHOD, "OnExecute"));

                var openTarget = new OpenTarget(OpenTargetType.ApplicationClass, baseClassType.ClassName, "", targetParts);

                var parsedProg = GetParsedProgram(openTarget);
                if (parsedProg != null && parsedProg.AppClass != null)
                {
                    (_, var parentSpan) = FindMethodForClass(parsedProg.AppClass, memberName, false);
                    if (parentSpan != null)
                    {
                        return (openTarget, parentSpan);
                    }
                }
                else
                {
                    return (null, null);
                }
            }
            return (null, null);
        }
        public override void VisitAppClassType(AppClassTypeNode node)
        {
            base.VisitAppClassType(node);

            if (node.SourceSpan.ContainsPosition(_position))
            {
                List<(PSCLASSID, string)> targetParts = [];

                var packageClassID = 104;
                foreach (var package in node.PackagePath)
                {
                    targetParts.Add(((PSCLASSID)packageClassID++, package));
                }

                targetParts.Add((PSCLASSID.APPLICATION_CLASS, node.ClassName));
                targetParts.Add((PSCLASSID.METHOD, "OnExecute"));

                var openTarget = new OpenTarget(OpenTargetType.ApplicationClass, node.ClassName, "", targetParts);
                Result.TargetProgram = openTarget;

                var parsedProg = GetParsedProgram(openTarget);
                if (parsedProg != null && parsedProg.AppClass != null)
                {
                    Result.SourceSpan = parsedProg.AppClass.NameToken.SourceSpan;
                }
                else
                {
                    Result.SourceSpan = new();
                }

            }

        }
        private (OpenTarget? targetClass, SourceSpan? span) FindPropertyForClass(AppClassNode classNode, string memberName, bool skipSelf = false)
        {
            if (!skipSelf)
            {
                /* Try to send to the implementation first */
                var matchingProperty = classNode.Properties.Where(p => p.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                if (matchingProperty != null)
                {
                    return (null, matchingProperty.NameToken.SourceSpan);
                }
            }

            /* We checked ourself and didn't find it, lets get our parent... */
            AppClassTypeNode? baseClassType = null;

            if (classNode.BaseClass is not null and AppClassTypeNode)
            {
                baseClassType = (AppClassTypeNode)classNode.BaseClass;
            }
            else if (classNode.ImplementedInterface is not null and AppClassTypeNode )
            {
                baseClassType = (AppClassTypeNode)classNode.ImplementedInterface;
            }

            if (baseClassType != null)
            {
                List<(PSCLASSID, string)> targetParts = [];

                var packageClassID = 104;
                foreach (var package in baseClassType.PackagePath)
                {
                    targetParts.Add(((PSCLASSID)packageClassID++, package));
                }

                targetParts.Add((PSCLASSID.APPLICATION_CLASS, baseClassType.ClassName));
                targetParts.Add((PSCLASSID.METHOD, "OnExecute"));
                var openTarget = new OpenTarget(OpenTargetType.ApplicationClass, baseClassType.ClassName, "", targetParts);

                var parsedProg = GetParsedProgram(openTarget);
                if (parsedProg != null && parsedProg.AppClass != null)
                {
                    (_, var parentSpan) = FindPropertyForClass(parsedProg.AppClass, memberName, false);
                    if (parentSpan != null)
                    {
                        return (openTarget, parentSpan);
                    }
                }
                else
                {
                    return (null, null);
                }
            }
            return (null, null);
        }

        private ProgramNode? GetParsedProgram(OpenTarget openTarget)
        {
            if (_dataManager is null) return null;
            var sourceCode = _dataManager.GetPeopleCodeProgram(openTarget);

            if (sourceCode is null) return null;

            var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(sourceCode);
            var tokens = lexer.TokenizeAll();
            var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
            return parser.ParseProgram();
        }

    }
}
