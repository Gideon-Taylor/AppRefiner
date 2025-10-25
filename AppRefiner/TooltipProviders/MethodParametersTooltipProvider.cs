using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Types;
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
        /// Override to process function calls that might be method calls or global builtin functions
        /// </summary>
        public override void VisitFunctionCall(FunctionCallNode node)
        {
            if (!node.SourceSpan.ContainsPosition(CurrentPosition)) return;

            if (node.Function is IdentifierNode identifier)
            {
                // Global function like Split(), Left(), Right()
                HandleGlobalFunction(identifier.Name, node.SourceSpan);
            }
            else if (node.Function is MemberAccessNode memberAccess)
            {
                // Method call - enhanced with type inference
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

            // Try to get inferred type from type inference (works with or without database)
            var targetType = memberAccess.Target.GetInferredType();

            if (targetType != null)
            {
                // Check if it's a builtin type
                string? typeName = GetBuiltinTypeName(targetType);
                if (typeName != null)
                {
                    var methodInfo = PeopleCodeTypeDatabase.GetMethod(typeName, methodName);
                    if (methodInfo != null)
                    {
                        var tooltip = FormatBuiltinFunctionTooltip(methodInfo);
                        RegisterTooltip(memberAccess.SourceSpan, tooltip);
                        return; // Found builtin, done
                    }
                }

                // Check if it's an app class type (requires database)
                if (targetType is AppClassTypeInfo appClassType)
                {
                    var tooltipText = GetMethodTooltipFromClass(appClassType.QualifiedName, methodName);
                    if (tooltipText != null)
                    {
                        RegisterTooltip(memberAccess.SourceSpan, tooltipText);
                        return;
                    }
                }
            }

            // Fall back to existing logic (variable tracking)
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

        /// <summary>
        /// Handles global function calls (both declared external functions and builtin functions).
        /// Checks declared functions first (which may be imports from other programs),
        /// then falls back to builtin functions.
        /// </summary>
        private void HandleGlobalFunction(string functionName, SourceSpan span)
        {
            // First check if this is a declared external function
            if (Program != null)
            {
                var declaredFunc = Program.Functions.FirstOrDefault(f =>
                    f.IsDeclaration &&
                    f.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));

                if (declaredFunc != null)
                {
                    // The FunctionInfo should have been pre-resolved by TypeInferenceVisitor.ProcessDeclaredFunctions
                    var funcInfo = declaredFunc.GetFunctionInfo();
                    if (funcInfo != null)
                    {
                        var tooltip = FormatBuiltinFunctionTooltip(funcInfo);
                        RegisterTooltip(span, tooltip);
                        return;
                    }
                }
            }

            // Fallback to builtin functions
            var functionInfo = PeopleCodeTypeDatabase.GetFunction(functionName);
            if (functionInfo != null)
            {
                var tooltip = FormatBuiltinFunctionTooltip(functionInfo);
                RegisterTooltip(span, tooltip);
            }
        }

        /// <summary>
        /// Maps TypeInfo to PeopleCodeTypeDatabase type names for builtin object lookups.
        /// </summary>
        /// <param name="typeInfo">The inferred type from type inference</param>
        /// <returns>The type name for PeopleCodeTypeDatabase lookup, or null if not a builtin type</returns>
        private string? GetBuiltinTypeName(PeopleCodeTypeInfo.Types.TypeInfo typeInfo)
        {
            // Map TypeInfo to PeopleCodeTypeDatabase type names
            switch (typeInfo.PeopleCodeType)
            {
                case PeopleCodeType.String:
                case PeopleCodeType.Number:
                case PeopleCodeType.Integer:
                case PeopleCodeType.Boolean:
                case PeopleCodeType.Date:
                case PeopleCodeType.Time:
                case PeopleCodeType.DateTime:
                    return "System"; // Global functions live here

                case PeopleCodeType.Record:
                    return "Record";
                case PeopleCodeType.Row:
                    return "Row";
                case PeopleCodeType.Rowset:
                    return "Rowset";
                case PeopleCodeType.Field:
                    return "Field";
                case PeopleCodeType.Apiobject:
                    return "ApiObject";
                case PeopleCodeType.Jsonobject:
                    return "JsonObject";
                case PeopleCodeType.Jsonarray:
                    return "JsonArray";
                case PeopleCodeType.File:
                    return "File";
                case PeopleCodeType.Sql:
                    return "Sql";

                default:
                    return null; // Not a builtin type
            }
        }

        /// <summary>
        /// Formats a builtin function/method tooltip using PeopleCodeTypeDatabase information.
        /// </summary>
        private string FormatBuiltinFunctionTooltip(PeopleCodeTypeInfo.Functions.FunctionInfo functionInfo)
        {
            var sb = new StringBuilder();

            // Use PeopleCodeTypeDatabase's signature formatter
            sb.AppendLine($"Builtin: {PeopleCodeTypeDatabase.GetSignature(functionInfo)}");
            sb.AppendLine();

            // Parameter details
            if (functionInfo.Parameters.Count > 0)
            {
                sb.AppendLine("Parameters:");
                foreach (var param in functionInfo.Parameters)
                {
                    sb.AppendLine($"   {param}");
                }
            }
            else
            {
                sb.AppendLine("Parameters: None");
            }

            // Argument count info (helpful for overloaded functions)
            if (functionInfo.MinArgumentCount != functionInfo.MaxArgumentCount)
            {
                sb.AppendLine($"Min args: {functionInfo.MinArgumentCount}, Max args: {functionInfo.MaxArgumentCount}");
            }

            return sb.ToString();
        }

    }
}
