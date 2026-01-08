using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using PeopleCodeTypeInfo.Inference;
using System.Reflection;
using System.Text;
using AppRefiner.Database;

namespace AppRefiner.Services
{
    /// <summary>
    /// Service for generating type error reports that can be submitted to GitHub.
    /// Reports include function signatures and inferred types. Users can edit the report
    /// to remove any proprietary information before submission.
    /// </summary>
    public class TypeErrorReporter
    {

        /// <summary>
        /// Generates a type error report for the current cursor position in the editor.
        /// </summary>
        /// <param name="editor">The Scintilla editor</param>
        /// <param name="extensionManager">The type extension manager for language extensions (optional)</param>
        /// <returns>A formatted markdown report, or null if no errors found</returns>
        public string? GenerateReport(ScintillaEditor editor, AppRefiner.LanguageExtensions.TypeExtensionManager? extensionManager = null)
        {
            if (editor == null || !editor.IsValid())
            {
                return null;
            }

            // Get cursor position
            int position = ScintillaManager.GetCursorPosition(editor);

            // Get AST
            var program = editor.GetParsedProgram(forceReparse: true);
            if (program == null)
            {
                return null;
            }

            // Get type system infrastructure
            var appDesignerProcess = editor.AppDesignerProcess;
            if (appDesignerProcess == null)
            {
                return null;
            }

            var typeResolver = appDesignerProcess.TypeResolver;
            if (typeResolver == null)
            {
                return null;
            }

            // Determine the qualified name for the current program
            string qualifiedName = DetermineQualifiedName(program, editor);

            try
            {
                // Run type checking pipeline to populate type information
                var programMetadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);

                string? defaultRecord = null;
                string? defaultField = null;
                if (editor.Caption != null && editor.Caption.EndsWith("(Record PeopleCode)"))
                {
                    var parts = qualifiedName.Split('.');
                    if (parts.Length >= 2)
                    {
                        defaultRecord = parts[0];
                        defaultField = parts[1];
                    }
                }

                // Run type inference
                TypeInferenceVisitor.Run(
                    program,
                    programMetadata,
                    typeResolver,
                    defaultRecord,
                    defaultField,
                    inferAutoDeclaredTypes: false,
                    onUndefinedVariable: extensionManager != null ? extensionManager.HandleUndefinedVariable : null);

                // Run type checking
                TypeCheckerVisitor.Run(program, typeResolver, typeResolver.Cache);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "TypeErrorReporter: Error during type checking");
                return null;
            }

            // Find type errors at cursor position
            var errors = FindTypeErrorsAtPosition(program, position);
            if (!errors.Any())
            {
                return null;
            }

            // Build report
            var sb = new StringBuilder();
            sb.AppendLine("## Type Checker Issue Report");
            sb.AppendLine();
            sb.AppendLine("**IMPORTANT**: This report may contain proprietary information. Please review and edit out any sensitive details before submitting.");
            sb.AppendLine();
            sb.AppendLine($"**AppRefiner Version**: {GetAppRefinerVersion()}");
            sb.AppendLine($"**Date**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();

            int errorNumber = 1;
            foreach (var error in errors)
            {
                sb.AppendLine($"### Error {errorNumber}: {GetErrorCategory(error)}");
                sb.AppendLine();
                FormatError(sb, error);
                sb.AppendLine();
                errorNumber++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Finds all type errors that overlap the given position
        /// </summary>
        private List<TypeError> FindTypeErrorsAtPosition(ProgramNode program, int byteIndex)
        {
            var errors = new List<TypeError>();
            var allErrors = program.GetAllTypeErrors();

            foreach (var error in allErrors)
            {
                // Check if error overlaps the cursor position
                if (error.Node.SourceSpan.Start.ByteIndex <= byteIndex &&
                    error.Node.SourceSpan.End.ByteIndex >= byteIndex)
                {
                    errors.Add(error);
                }
            }

            return errors;
        }

        /// <summary>
        /// Determines the category of a type error
        /// </summary>
        private string GetErrorCategory(TypeError error)
        {
            return error.Node switch
            {
                FunctionCallNode => "Function Call Type Mismatch",
                AssignmentNode => "Assignment Type Mismatch",
                LocalVariableDeclarationWithAssignmentNode => "Variable Declaration Type Mismatch",
                ExpressionStatementNode => "Uncaptured Return Value",
                _ => "Type Error"
            };
        }

        /// <summary>
        /// Formats an error with detailed information
        /// </summary>
        private void FormatError(StringBuilder sb, TypeError error)
        {
            sb.AppendLine($"- **Error**: {error.Message}");

            // Check if error node is part of a function call (could be argument node)
            var functionCallNode = error.Node as FunctionCallNode ?? error.Node.FindAncestor<FunctionCallNode>();

            if (functionCallNode != null && (error.Message.Contains("Argument") || error.Message.Contains("parameter")))
            {
                // This is a function call error (possibly on an argument node)
                FormatFunctionCallError(sb, functionCallNode);
                return;
            }

            switch (error.Node)
            {
                case FunctionCallNode fcn:
                    FormatFunctionCallError(sb, fcn);
                    break;
                case AssignmentNode assignment:
                    FormatAssignmentError(sb, assignment);
                    break;
                case LocalVariableDeclarationWithAssignmentNode decl:
                    FormatDeclarationError(sb, decl);
                    break;
                case ExpressionStatementNode exprStmt:
                    FormatExpressionStatementError(sb, exprStmt);
                    break;
            }
        }

        /// <summary>
        /// Formats function call error details
        /// </summary>
        private void FormatFunctionCallError(StringBuilder sb, FunctionCallNode fcn)
        {
            var functionInfo = fcn.GetFunctionInfo();
            if (functionInfo != null)
            {
                sb.AppendLine($"- **Function Signature**: `{functionInfo.ToString()}`");

                // Format inferred argument types
                var inferredTypes = fcn.Arguments.Select(a => a.GetInferredType()?.ToString() ?? "unknown");
                sb.AppendLine($"- **Inferred Types**: `({string.Join(", ", inferredTypes)})`");
            }
        }

        /// <summary>
        /// Formats assignment error details
        /// </summary>
        private void FormatAssignmentError(StringBuilder sb, AssignmentNode assignment)
        {
            var leftType = assignment.Target.GetInferredType();
            var rightType = assignment.Value.GetInferredType();

            sb.AppendLine($"- **Target Type**: `{leftType?.ToString() ?? "Unknown"}`");
            sb.AppendLine($"- **Value Type**: `{rightType?.ToString() ?? "Unknown"}`");
        }

        /// <summary>
        /// Formats declaration error details
        /// </summary>
        private void FormatDeclarationError(StringBuilder sb, LocalVariableDeclarationWithAssignmentNode decl)
        {
            var declaredType = ConvertTypeNodeToTypeInfo(decl.Type);
            var valueType = decl.InitialValue.GetInferredType();

            sb.AppendLine($"- **Declared Type**: `{declaredType?.ToString() ?? "Unknown"}`");
            sb.AppendLine($"- **Value Type**: `{valueType?.ToString() ?? "Unknown"}`");
        }

        /// <summary>
        /// Formats expression statement error details
        /// </summary>
        private void FormatExpressionStatementError(StringBuilder sb, ExpressionStatementNode exprStmt)
        {
            if (exprStmt.Expression is FunctionCallNode fcn)
            {
                var functionInfo = fcn.GetFunctionInfo();
                if (functionInfo != null)
                {
                    string signature = $"{functionInfo.ToString()}";
                    sb.AppendLine($"- **Function Signature**: `{signature}`");
                }
            }
        }

        /// <summary>
        /// Converts a TypeNode to TypeInfo (copied from TypeCheckerVisitor)
        /// </summary>
        private PeopleCodeTypeInfo.Types.TypeInfo? ConvertTypeNodeToTypeInfo(TypeNode? typeNode)
        {
            if (typeNode == null)
                return UnknownTypeInfo.Instance;

            return typeNode switch
            {
                BuiltInTypeNode builtin => PeopleCodeTypeInfo.Types.TypeInfo.FromPeopleCodeType(builtin.Type),
                ArrayTypeNode array => new ArrayTypeInfo(
                    array.Dimensions,
                    ConvertTypeNodeToTypeInfo(array.ElementType)),
                AppClassTypeNode appClass => new AppClassTypeInfo(
                    string.Join(":", appClass.PackagePath.Concat(new[] { appClass.ClassName }))),
                _ => UnknownTypeInfo.Instance
            };
        }

        /// <summary>
        /// Gets the AppRefiner version
        /// </summary>
        private string GetAppRefinerVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Determines the qualified name for the current program.
        /// Copied from TypeErrorStyler.
        /// </summary>
        private string DetermineQualifiedName(ProgramNode node, ScintillaEditor editor)
        {
            // Try to extract from AST structure first
            if (node.AppClass != null)
            {
                // For app classes/interfaces, try to build qualified name from imports or use simple name
                var className = node.AppClass.Name;

                if (editor?.Caption != null && !string.IsNullOrWhiteSpace(editor.Caption))
                {
                    // Parse caption to get program identifier
                    var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
                    if (openTarget != null)
                    {
                        var methodIndex = Array.IndexOf(openTarget.ObjectIDs, PSCLASSID.METHOD);
                        if (methodIndex >= 0)
                        {
                            openTarget.ObjectIDs[methodIndex] = PSCLASSID.NONE;
                            openTarget.ObjectValues[methodIndex] = null;
                        }
                        return openTarget.Path;
                    }
                    else
                    {
                        return className;
                    }
                }
                else
                {
                    return className;
                }
            }
            else
            {
                // For function libraries or other programs, use a generic name
                // Try to extract from editor caption if available
                if (editor?.Caption != null && !string.IsNullOrWhiteSpace(editor.Caption))
                {
                    // Parse caption to get program identifier
                    var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
                    if (openTarget != null)
                    {
                        return string.Join(".", openTarget.ObjectValues.Where(v => v != null));
                    }
                }

                // Fallback to generic name
                return "Program";
            }
        }
    }
}
