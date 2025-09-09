using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using System;
using System.Reflection;
using System.Text;
using static SqlParser.Ast.RoleOption;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips showing method parameter information when hovering over method calls.
    /// Specifically focuses on %This.Method() calls and &variable.Method() calls with application class types,
    /// and attempts to find the method definition within the class or its inheritance chain.
    /// </summary>
    public class MethodParametersTooltipProvider : BaseTooltipProvider
    {
        private AppClassNode? classNode;
        private string basePackage;

        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public override string Name => "Method Parameters";

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public override string Description => "Shows method parameter information on method calls";

        /// <summary>
        /// Medium priority
        /// </summary>
        public override int Priority => 50;

        /// <summary>
        /// Database connection is required to look up parent classes
        /// </summary>
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        public override bool CanProvideTooltipAt(ScintillaEditor editor, ProgramNode program, List<Token> tokens, int cursorPosition, int lineNumber)
        {
            basePackage = editor.ClassPath;
            return base.CanProvideTooltipAt(editor, program, tokens, cursorPosition, lineNumber);
        }

        /// <summary>
        /// Processes the AST to collect method information and register tooltips
        /// </summary>
        public override void ProcessProgram(ProgramNode program, int position, int lineNumber)
        {

            if (program.AppClass != null)
            {
                classNode = program.AppClass;
            }

            base.ProcessProgram(program, position, lineNumber);
        }


        /// <summary>
        /// Override to process function calls that might be method calls
        /// </summary>
        public override void VisitFunctionCall(FunctionCallNode node)
        {
            if (!node.SourceSpan.ContainsPosition(CurrentPosition)) return;
            // Check if this is a method call (function is a member access)
            if (node.Function is MemberAccessNode memberAccess)
            {
                ProcessMethodCall(memberAccess, node);
            }
            base.VisitFunctionCall(node);
        }


        /// <summary>
        /// Processes a method call to register tooltips
        /// </summary>
        private void ProcessMethodCall(MemberAccessNode memberAccess, FunctionCallNode functionCall)
        {
            string methodName = memberAccess.MemberName;

            // Check if target is an identifier (%This, &variable, etc.)
            if (memberAccess.Target is IdentifierNode targetIdentifier)
            {
                string objectText = targetIdentifier.Name;
                bool isThis = objectText.Equals("%This", StringComparison.OrdinalIgnoreCase);
                bool isVariable = objectText.StartsWith("&");

                if (isThis)
                {
                    // Handle %This.Method()
                    HandleThisMethodCall(methodName, memberAccess.SourceSpan);
                }
                else if (isVariable)
                {
                    // Handle &variable.Method()
                    string variableName = objectText;
                    HandleVariableMethodCall(variableName, methodName, memberAccess.SourceSpan);
                }
            }
            else
            {
                // Handle more complex expressions like (someExpr).Method()
                // For now, we could potentially handle these by analyzing the expression
                // but this would be more complex and may not be necessary for basic functionality
            }
        }

        /// <summary>
        /// Handle %This.MethodName() calls to find method parameters
        /// </summary>
        private void HandleThisMethodCall(string methodName, SourceSpan span)
        {
            if (classNode == null) return;

            var tooltipText = GetMethodTooltipFromClass($"{basePackage}", methodName);

            if (tooltipText != null)
            {
                RegisterTooltip(span, tooltipText);
            }

        }
        private string FormatTooltipForMethod(MethodNode method, string fromClass = "")
        {
            bool foundInParent = !($"{basePackage}".Equals(fromClass, StringComparison.OrdinalIgnoreCase));

            // Found in a parent class - add the class name to the tooltip
            string tooltipText = $"Method: {method.Name} {( foundInParent ? $"(inherited from {fromClass})" : "") }\n" +
                                            $"Access: {method.Visibility}\n";

            // Add the rest of the method info
            if (method.IsAbstract)
                tooltipText += "Abstract Method\n";

            if (method.ReturnType != null)
                tooltipText += $"Returns: {method.ReturnType.ToString()}\n";

            // Parameters
            if (method.Parameters.Count > 0)
            {
                tooltipText += "Parameters:\n";
                foreach (var param in method.Parameters)
                {
                    tooltipText += $"   {param}\n";
                }
            }
            else
            {
                tooltipText += "Parameters: None\n";
            }

            return tooltipText;
        }
   

        /* Note that we accept an AppClassNode here since only classes can contain code, which is how you could get a function/method call. */
        private string? GetMethodTooltipFromClass(string startingTypePath, string methodName)
        {
            Stack<string> typeStack = [];
            typeStack.Push(startingTypePath);
           
            while (typeStack.Count > 0)
            {
                var type = typeStack.Pop();
                if (type.Contains(':'))
                {
                    ProgramNode? typeProgram = null;
                    /* if it matches our class path, it has to be a class, not an interface, because we trigger on
                     * function calls, and interfaces can't contain those 
                     */
                    if (type.Equals($"{basePackage}"))
                    {
                        /* Synthetic program node since that's what used in the "not this" case */
                        typeProgram = new ProgramNode() { AppClass = classNode };
                    }
                    else
                    {
                      typeProgram = ParseExternalClass(type);
                    }

                    if (typeProgram == null) return null;

                    if (typeProgram.AppClass != null)
                    {
                        var parentMethod = typeProgram.AppClass.Methods.Where(m => m.Name == methodName).FirstOrDefault();
                        if (parentMethod != null)
                        {
                            return FormatTooltipForMethod(parentMethod, type);
                        } else if (typeProgram.AppClass.BaseClass != null)
                        {
                            typeStack.Push(typeProgram.AppClass.BaseClass.TypeName);
                        }
                    }

                    if (typeProgram.Interface != null)
                    {
                        var parentMethod = typeProgram.Interface.Methods.Where(m => m.Name == methodName).FirstOrDefault();
                        if (parentMethod != null)
                        {
                            return FormatTooltipForMethod(parentMethod, type);
                        } else if (typeProgram.Interface.BaseInterface != null)
                        {
                            typeStack.Push(typeProgram.Interface.BaseInterface.TypeName);
                        }
                    }
                }
            }

            /* Didn't find this method in the class heirarchy */
            return null;
        }

        /// <summary>
        /// Handle &variable.MethodName() calls to find method parameters
        /// </summary>
        private void HandleVariableMethodCall(string variableName, string methodName, SourceSpan span)
        {
            // First, check if we know this variable's type using the ScopedAstTooltipProvider functionality
            if (!TryGetVariableInfo(variableName, out var varInfo) || varInfo == null)
            {
                // Unknown variable
                string stubMessage = $"Method: {methodName}\n(Variable type unknown)";
                RegisterTooltip(span, stubMessage);
                return;
            }

            // Determine if we're dealing with an application class type
            if (varInfo.Type.Contains(':'))
            {
                // For app class types, we need to look up the method in that class
                var classType = varInfo.Type;
                string? tooltipText = GetMethodTooltipFromClass(classType, methodName);
                if (tooltipText != null)
                {
                    RegisterTooltip(span, tooltipText);
                }
            }
            else
            {
                // For built-in types, no information available
                string stubMessage = $"Method: {methodName}\nType: {varInfo.Type}\n(Built-in method information not available)";
                RegisterTooltip(span, stubMessage);
            }
        }


        /// <summary>
        /// Attempts to find variable information in the current scope
        /// </summary>
        private bool TryGetVariableInfo(string name, out VariableInfo? info)
        {
            info = GetVariablesAtPosition().FirstOrDefault(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return info != null;
        }

    }
}
