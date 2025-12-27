using AppRefiner.Database;
using AppRefiner.LanguageExtensions;
using AppRefiner.Refactors; // For ScopedRefactor, AddImport, CreateAutoComplete
using AppRefiner.TooltipProviders; // For DllImport
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Database;  // For PeopleCodeTypeDatabase
using PeopleCodeTypeInfo.Functions; // For FunctionInfo, PropertyInfo, BuiltinObjectInfo
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;     // For TypeInfo and subclasses
using PeopleCodeTypeInfo.Validation;
using System.Runtime.InteropServices;
using System.Text;
using static AppRefiner.AppDesignerProcess;
using static AppRefiner.ScintillaEditor;
using static SqlParser.Ast.Statement;

namespace AppRefiner
{
    internal class PositionIsInVariableDeclaration : ScopedAstVisitor<object>
    {
        int targetPosition;
        bool foundPositionInVarDecl;
        public bool IsInVariableDecl() => foundPositionInVarDecl;

        public PositionIsInVariableDeclaration(int position)
        {
            targetPosition = position;
        }

        public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
        {
            base.VisitLocalVariableDeclaration(node);
            foreach (var nameInfo in node.VariableNameInfos)
            {
                if (nameInfo.Token != null && nameInfo.Token.SourceSpan.ContainsPosition(targetPosition)) {
                    foundPositionInVarDecl = true;
                }
            }
        }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            base.VisitLocalVariableDeclarationWithAssignment(node);
            if (node.VariableNameInfo.Token != null && node.VariableNameInfo.Token.SourceSpan.ContainsPosition(targetPosition))
            {
                foundPositionInVarDecl = true;
            }
        }

        public override void VisitProgramVariable(ProgramVariableNode node)
        {
            base.VisitProgramVariable(node);
            foreach (var nameInfo in node.NameInfos)
            {
                if (nameInfo.Token != null && nameInfo.Token.SourceSpan.ContainsPosition(targetPosition))
                {
                    foundPositionInVarDecl = true;
                }
            }
        }


    }
    /// <summary>
    /// Internal variable collector for auto-completion suggestions
    /// </summary>
    internal class VariableCollector : ScopedAstVisitor<object>
    {
        private readonly int targetPosition;
        private ScopeContext? targetScope;
        private List<VariableInfo> accessibleVariables = new();
        private bool targetIsInString = false;
        public VariableCollector(int position)
        {
            targetPosition = position;
        }

        public List<VariableInfo> GetAccessibleVariables() => accessibleVariables;

        public override void VisitLiteral(LiteralNode node)
        {
            base.VisitLiteral(node);
            if (!targetIsInString && node.LiteralType == LiteralType.String)
            {
                targetIsInString = (node.SourceSpan.ContainsPosition(targetPosition));
            }
            
        }

        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);

            if (targetIsInString)
            {
                accessibleVariables.Clear();
            }
            else
            {
                accessibleVariables = accessibleVariables.Where(v => v.DeclarationNode.SourceSpan.Start.ByteIndex < targetPosition).ToList();
            }

        }
        protected override void OnExitFunctionScope(ScopeContext scope, FunctionNode node, Dictionary<string, object> customData)
        {
            base.OnExitFunctionScope(scope, node, customData);

            if (node.Body != null && node.Body.SourceSpan.ContainsPosition(targetPosition))
            {
                accessibleVariables = GetAccessibleVariables(scope).ToList();
            }

        }
        protected override void OnExitMethodScope(ScopeContext scope, MethodNode node, Dictionary<string, object> customData)
        {
            base.OnExitMethodScope(scope, node, customData);

            if (node.Body != null && node.Body.SourceSpan.ContainsPosition(targetPosition))
            {
                accessibleVariables = GetAccessibleVariables(scope).ToList();
            }
        }

        protected override void OnExitPropertyGetterScope(ScopeContext scope, PropertyImplNode node, Dictionary<string, object> customData)
        {
            base.OnExitPropertyGetterScope(scope, node, customData);
            if (node.Body != null && node.Body.SourceSpan.ContainsPosition(targetPosition))
            {
                accessibleVariables = GetAccessibleVariables(scope).ToList();
            }
        }

        protected override void OnExitPropertySetterScope(ScopeContext scope, PropertyImplNode node, Dictionary<string, object> customData)
        {
            base.OnExitPropertySetterScope(scope, node, customData);
            if (node.Body != null && node.Body.SourceSpan.ContainsPosition(targetPosition))
            {
                accessibleVariables = GetAccessibleVariables(scope).ToList();
            }
        }

        protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, object> customData)
        {
            base.OnExitGlobalScope(scope, node, customData);
            if (accessibleVariables.Count == 0)
            {
                accessibleVariables = GetAccessibleVariables(scope).ToList();
            }
        }

    }

    /// <summary>
    /// Provides services for handling code auto-completion features.
    /// </summary>
    public class AutoCompleteService
    {
        /// <summary>
        /// Extension manager for checking extension methods.
        /// Set by MainForm during initialization.
        /// </summary>
        public static LanguageExtensionManager? ExtensionManager { get; set; }

        public enum UserListType
        {
            AppPackage = 1,
            QuickFix = 2,
            Variable = 3,
            ObjectMembers = 4,
            SystemVariables = 5
        }

        // Constants related to Scintilla messages (can be kept private if only used here)
        private const int SCI_LINEFROMPOSITION = 2166;
        private const int SCI_POSITIONFROMLINE = 2167;
        private const int SCI_GETCURRENTPOS = 2008;
        private const int SCI_SETSEL = 2160;
        private const int AR_APP_PACKAGE_SUGGEST = 2500; // Keep for recursive call

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private readonly MainForm mainForm;

        public AutoCompleteService(MainForm mainForm)
        {
            this.mainForm = mainForm;
        }

        public void ShowQuickFixSuggestions(ScintillaEditor? editor, int position)
        {
            if (editor == null || !editor.IsValid()) return;

            List<string> quickFixList = new();

            editor.ActiveIndicators.Where(i => i.Start <= position && i.Start + i.Length >= position && i.QuickFixes != null)
                .ToList()
                .ForEach(indicator =>
                {

                    quickFixList.AddRange(indicator.QuickFixes.Select(q => q.Description));

                });

            if (quickFixList.Count > 0)
            {
                ScintillaManager.ShowUserList(editor, UserListType.QuickFix, position, quickFixList);
            }
        }


        /// <summary>
        /// Shows app package suggestions based on the text preceding the cursor.
        /// </summary>
        /// <param name="editor">The current Scintilla editor.</param>
        /// <param name="position">Current cursor position.</param>
        public void ShowAppPackageSuggestions(ScintillaEditor? editor, int position)
        {
            if (editor == null || !editor.IsValid() || editor.DataManager == null) return;

            try
            {
                // Get the current line and content up to the cursor position
                int currentLine = (int)editor.SendMessage(SCI_LINEFROMPOSITION, position, 0);
                int lineStartPos = (int)editor.SendMessage(SCI_POSITIONFROMLINE, currentLine, 0);

                string content = ScintillaManager.GetScintillaText(editor) ?? "";
                // Ensure position is within content bounds before substring
                if (position < lineStartPos || position > lineStartPos + content.Length)
                {
                    Debug.Log($"Position {position} is out of bounds for line {currentLine} starting at {lineStartPos}");
                    return;
                }
                string lineContent = content.Substring(lineStartPos, position - lineStartPos);

                // Check if there's a colon in the line content (or if it ends with one)
                if (!lineContent.Contains(':'))
                {
                    Debug.Log("No colon found in line content for app package suggestion.");
                    return;
                }

                // Extract the potential package path
                string packagePath = ExtractPackagePathFromLine(lineContent);
                if (string.IsNullOrEmpty(packagePath))
                {
                    Debug.Log("No valid package path found for suggestion.");
                    return;
                }

                Debug.Log($"Extracted package path for suggestion: {packagePath}");

                // Get package items from database
                var packageItems = editor.DataManager.GetAppPackageItems(packagePath);

                // Convert to list of strings for autocomplete
                List<string> suggestions = new();
                // Sort alphabetically, packages first, then classes
                suggestions.AddRange(packageItems.Subpackages.OrderBy(p => p).Select(p => $"{p} (Package)"));
                suggestions.AddRange(packageItems.Classes.OrderBy(c => c).Select(c => $"{c} (Class)"));


                if (suggestions.Count > 0)
                {
                    // Show the autocompletion list with app package suggestions
                    Debug.Log($"Showing {suggestions.Count} app package suggestions for '{packagePath}'");
                    bool result = ScintillaManager.ShowAutoComplete(editor, AutoCompleteContext.AppPackage, position, suggestions);

                    if (!result)
                    {
                        Debug.Log("Failed to show user list popup for app packages.");
                    }
                }
                else
                {
                    Debug.Log($"No suggestions found for '{packagePath}'");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error getting app package suggestions");
            }
        }

        /// <summary>
        /// Shows variable suggestions based on the current scope at the cursor position.
        /// </summary>
        /// <param name="editor">The current Scintilla editor.</param>
        /// <param name="position">Current cursor position.</param>
        public void ShowVariableSuggestions(ScintillaEditor? editor, int position)
        {
            if (editor == null || !editor.IsValid()) return;

            try
            {
                // Get the current document text
                string content = ScintillaManager.GetScintillaText(editor) ?? "";
                if (string.IsNullOrEmpty(content))
                {
                    Debug.Log("No content available for variable suggestions.");
                    return;
                }

                // Parse the current document to get AST
                var lexer = new PeopleCodeLexer(content);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var program = parser.ParseProgram();
                RunTypeInference(editor, program);

                if (program == null)
                {
                    Debug.Log("Failed to parse document for variable suggestions.");
                    return;
                }

                var inNameDeclCheck = new PositionIsInVariableDeclaration(position);
                inNameDeclCheck.VisitProgram(program);

                if (inNameDeclCheck.IsInVariableDecl())
                {
                    return;
                }

                var currentToken = program.FindNodes(n => n.SourceSpan.ContainsPosition(position)).LastOrDefault();
                List<FunctionCallValidator.ParameterTypeInfo>? allowedTypes = null;
                if (currentToken != null)
                {
                    var parent = currentToken.Parent;
                    FunctionCallNode? funcCallNode = null;
                    while (parent != null) {
                        if (parent is UnaryOperationNode u && u.Operator == UnaryOperator.Reference)
                        {
                            break;
                        } else if (parent is FunctionCallNode) {
                            funcCallNode = parent as FunctionCallNode;
                            break;
                        }
                        parent = parent.Parent;
                    }

                    /* Are we inside a dynamic reference ? */

                    if (funcCallNode != null)
                    {
                        var matchingArg = funcCallNode.Arguments.Where(a => a.SourceSpan.ContainsPosition(position)).FirstOrDefault();
                        if (matchingArg != null)
                        {
                            var argIndex = funcCallNode.Arguments.IndexOf(matchingArg);
                            var funcInfo = funcCallNode.GetFunctionInfo();
                            if (funcInfo != null)
                            {
                                allowedTypes = funcInfo.GetAllowedNextTypes(funcCallNode.Arguments.Select(a => a.GetInferredType()).Take(argIndex).ToArray(), editor.AppDesignerProcess.TypeResolver);
                            }
                        }
                    }
                }

                // Use the variable collector to get accessible variables at the current position
                var collector = new VariableCollector(position);
                collector.VisitProgram(program);

                var accessibleVariables = collector.GetAccessibleVariables();
                
                /* remove the & variable */
                accessibleVariables = accessibleVariables.Where(v => v.Name != "&").ToList();
                List<string> suggestions = new();
                List<VariableInfo> sortedVariables = new();
                if (allowedTypes != null)
                {
                    var allowedTypeStrings = allowedTypes.Select(a => a.TypeName.ToLower()).ToList();
                    allowedTypeStrings.Add("any");
                    allowedTypeStrings.Add("object");
                    var matchingTypeVars = accessibleVariables.Where(v => allowedTypeStrings.Contains(v.Type)).DistinctBy(v => v.Name).OrderBy(v => v.Name);
                    var nonMatchingTypes = accessibleVariables.Where(v => !allowedTypeStrings.Contains(v.Type)).DistinctBy(v => v.Name).OrderBy(v => v.Name);

                    sortedVariables.AddRange(matchingTypeVars);
                    sortedVariables.AddRange(nonMatchingTypes);
                } else
                {
                    sortedVariables.AddRange(accessibleVariables.DistinctBy(v => v.Name).OrderBy(v => v.Name));
                }

                foreach (var variable in sortedVariables)
                {
                    int iconNumber = variable.Kind switch
                    {
                        VariableKind.Local => (int)AutoCompleteIcons.LocalVariable,
                        VariableKind.Instance => (int)AutoCompleteIcons.InstanceVariable,
                        VariableKind.Global => (int)AutoCompleteIcons.GlobalVariable,
                        VariableKind.Component => (int)AutoCompleteIcons.ComponentVariable,
                        VariableKind.Parameter => (int)AutoCompleteIcons.Parameter,
                        VariableKind.Constant => (int)AutoCompleteIcons.ConstantValue,
                        VariableKind.Property => (int)AutoCompleteIcons.Property,
                        VariableKind.Exception => (int)AutoCompleteIcons.LocalVariable,
                        _ => -1
                    };

                    if (iconNumber >= 0) 
                    {
                        suggestions.Add($"{FormatVariableSuggestion(variable)}?{iconNumber}");
                    } else
                    {
                        suggestions.Add(FormatVariableSuggestion(variable));
                    }

                }

                if (suggestions.Count > 0)
                {
                    Debug.Log($"Showing {suggestions.Count} variable suggestions at position {position}");
                    bool result = ScintillaManager.ShowAutoComplete(editor, AutoCompleteContext.Variable, position, suggestions, customOrder: true);

                    if (!result)
                    {
                        Debug.Log("Failed to show user list popup for variables.");
                    }
                }
                else
                {
                    Debug.Log("No variables found in current scope.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error getting variable suggestions");
            }
        }

        /// <summary>
        /// Shows object member suggestions (methods and properties) for a given type.
        /// </summary>
        /// <param name="editor">The current Scintilla editor.</param>
        /// <param name="position">Current cursor position.</param>
        /// <param name="typeInfo">The TypeInfo to enumerate members from.</param>
        public void ShowObjectMembers(ScintillaEditor editor, int position, TypeInfo typeInfo, MemberVisibility maximumVisibility = MemberVisibility.Public)
        {
            if (editor == null || !editor.IsValid()) return;
            if (typeInfo == null) return;

            try
            {
                List<string> suggestions = new();

                // Handle builtin object types
                if (typeInfo is BuiltinObjectTypeInfo builtinType
                    && builtinType.PeopleCodeType.HasValue
                    && (builtinType.PeopleCodeType.Value != PeopleCodeType.Record
                        || typeInfo is not RecordTypeInfo
                        || (typeInfo is RecordTypeInfo rti && !rti.DirectRecordAccess)))
                {
                    string typeName = builtinType.PeopleCodeType.Value.GetTypeName();
                    var objectInfo = PeopleCodeTypeDatabase.GetObject(typeName);

                    if (objectInfo != null)
                    {
                        // Add all methods
                        foreach (var method in objectInfo.GetAllMethods().OrderBy(m => m.Name))
                        {
                            if (method.Visibility <= maximumVisibility)
                            {
                                suggestions.Add($"{method.Name}() -> {FormatReturnType(typeInfo, method)} (Method)?{(int)AutoCompleteIcons.ClassMethod}");
                            }
                        }

                        // Add all properties
                        foreach (var prop in objectInfo.GetAllProperties().OrderBy(p => p.Name))
                        {
                            suggestions.Add($"{prop.Name} -> {FormatPropertyType(prop)} (Property)?{(int)AutoCompleteIcons.Property}");
                        }
                    }
                }
                else if (typeInfo is ArrayTypeInfo arrayType)
                {
                    var objectInfo = PeopleCodeTypeDatabase.GetObject("array");

                    if (objectInfo != null)
                    {
                        // Add all methods
                        foreach (var method in objectInfo.GetAllMethods().OrderBy(m => m.Name))
                        {
                            if (method.Visibility <= maximumVisibility)
                            {
                                suggestions.Add($"{method.Name}() -> {FormatReturnType(typeInfo, method)} (Method)?{(int)AutoCompleteIcons.ClassMethod}");
                            }
                        }

                        // Add all properties
                        foreach (var prop in objectInfo.GetAllProperties().OrderBy(p => p.Name))
                        {
                            suggestions.Add($"{prop.Name} -> {FormatPropertyType(prop)} (Property)?{(int)AutoCompleteIcons.Property}");
                        }
                    }
                }
                // Handle AppClass types
                else if (typeInfo is AppClassTypeInfo appClass)
                {
                    var typeResolver = editor.AppDesignerProcess?.TypeResolver;
                    if (typeResolver != null)
                    {
                        var metadata = typeResolver.GetTypeMetadata(appClass.QualifiedName);
                        if (metadata != null)
                        {
                            // Add methods from metadata
                            foreach (var method in metadata.Methods.Values.OrderBy(m => m.Name))
                            {
                                if (method.Visibility <= maximumVisibility)
                                {
                                    string returnTypeStr = method.ReturnType != null
                                    ? method.ReturnType.ToString()
                                    : "void";
                                    suggestions.Add($"{method.Name}() -> {returnTypeStr}?{(int)AutoCompleteIcons.ClassMethod}");
                                }
                            }

                            // Add properties from metadata
                            foreach (var prop in metadata.Properties.OrderBy(p => p.Key))
                            {
                                if (prop.Value.Visibility <= maximumVisibility)
                                {
                                    string propTypeStr = prop.Value.Type.ToString();
                                    suggestions.Add($"{prop.Key} -> {propTypeStr}?{(int)AutoCompleteIcons.Property}");
                                }
                            }
                        }

                        foreach (var item in appClass.InheritanceChain)
                        {
                            if (item.QualifiedName == appClass.QualifiedName) continue;

                            metadata = typeResolver.GetTypeMetadata(item.QualifiedName);
                            if (metadata != null)
                            {
                                // Add methods from metadata
                                foreach (var method in metadata.Methods.Values.OrderBy(m => m.Name))
                                {
                                    if (method.Visibility <= MemberVisibility.Protected)
                                    {
                                        string returnTypeStr = method.ReturnType != null
                                        ? method.ReturnType.ToString()
                                        : "void";
                                        suggestions.Add($"{method.Name}() -> {returnTypeStr}?{(int)AutoCompleteIcons.ClassMethod}");
                                    }
                                }

                                // Add properties from metadata
                                foreach (var prop in metadata.Properties.OrderBy(p => p.Key))
                                {
                                    if (prop.Value.Visibility <= MemberVisibility.Protected)
                                    {
                                        string propTypeStr = prop.Value.Type.ToString();
                                        suggestions.Add($"{prop.Key} -> {propTypeStr}?{(int)AutoCompleteIcons.Property}");
                                    }
                                }
                            }


                        }

                    }
                }

                /* we are going to do the sorting and not let Scintilla do it
                 * this is so we can keep Value at the top */
                suggestions.Sort();

                if (typeInfo is RecordTypeInfo ri && editor.DataManager != null)
                {
                    var fields = editor.DataManager.GetRecordFields(ri.RecordName) ?? [];

                    foreach (var field in fields.OrderBy(f => f.FieldName))
                    {
                        suggestions.Insert(0, $"{field.FieldName} -> {field.FieldTypeName}?{(int)AutoCompleteIcons.Field}");
                    }
                }

                if (typeInfo is FieldTypeInfo fi)
                {
                    suggestions.Insert(0, $"Value -> {fi.GetFieldDataType()}?{(int)AutoCompleteIcons.Property}");
                }

                // Add language extension suggestions
                if (typeInfo != null && ExtensionManager != null)
                {
                    var extensions = ExtensionManager.GetExtensionsForType(typeInfo);

                    foreach (var transform in extensions)
                    {
                        // Format: "{Name} [Ext] -> {ReturnType} ({Type})?{Icon}"
                        string displayText = transform.GetName();
                        string memberKind = transform.ExtensionType == LanguageExtensionType.Property ? "Property" : "Method";

                        if (transform.GetReturnType().Type != PeopleCodeType.Void)
                        {
                            string returnTypeStr = FormatReturnType(typeInfo, transform.GetReturnType());
                            displayText += $" -> {returnTypeStr}";
                        }



                        // Add [Ext] marker to distinguish extensions
                        displayText += $" [Ext]";

                        // Note: Return type information removed from base class
                        // Extensions can provide this via GetFunctionInfo() if needed for tooltips

                        displayText += $" ({memberKind})";

                        // Add icon (use same icons as regular members)
                        int icon = transform.ExtensionType == LanguageExtensionType.Property
                            ? (int)AutoCompleteIcons.Property
                            : (int)AutoCompleteIcons.ClassMethod;

                        displayText += $"?{icon}";

                        suggestions.Add(displayText);
                    }
                }

                // Sort all suggestions (regular + extensions) alphabetically
                suggestions.Sort();

                if (suggestions.Count > 0)
                {
                    Debug.Log($"Showing {suggestions.Count} object member suggestions at position {position}");
                    bool result = ScintillaManager.ShowAutoComplete(editor, AutoCompleteContext.ObjectMembers, position, suggestions, customOrder: true);

                    if (!result)
                    {
                        Debug.Log("Failed to show user list popup for object members.");
                    }
                }
                else
                {
                    Debug.Log($"No members found for type: {typeInfo.Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error showing object members");
            }
        }

        /// <summary>
        /// Shows system variable suggestions that match the expected type.
        /// </summary>
        /// <param name="editor">The current Scintilla editor.</param>
        /// <param name="position">Current cursor position.</param>
        /// <param name="expectedType">The expected TypeInfo to filter system variables.</param>
        public void ShowSystemVariables(ScintillaEditor editor, ProgramNode program, int position, TypeInfo expectedType)
        {
            if (editor == null || !editor.IsValid()) return;
            if (expectedType == null) return;

            try
            {
                List<string> suggestions = new();

                // Get System object
                var systemObj = PeopleCodeTypeDatabase.GetObject("System");
                if (systemObj == null)
                {
                    Debug.Log("System builtin object not found");
                    return;
                }

                // Get all system variable properties
                var allProperties = systemObj.GetAllProperties();

                /* Add in synthetic %This and %Super is appropriate */
                List<PropertyInfo> synthetics = new();
                if (program.AppClass != null)
                {
                    synthetics.Add(new PropertyInfo(PeopleCodeType.AppClass, 0, DetermineQualifiedName(editor)) { Name = "%This" });

                    if (program.AppClass.BaseType != null)
                    {
                        synthetics.Add(new PropertyInfo(PeopleCodeType.AppClass, 0, program.AppClass.BaseType.TypeName) { Name = "%Super" });
                    }
                }

                // Convert and filter by expected type
                var matchingProperties = allProperties
                    .Where(prop =>
                    {
                        var propType = ConvertPropertyToTypeInfo(prop);
                        return expectedType.IsAssignableFrom(propType);
                    })
                    .OrderBy(prop => prop.Name);

                foreach(var prop in synthetics)
                {
                    string typeName = prop.IsAppClass && !string.IsNullOrEmpty(prop.AppClassPath)
                        ? prop.AppClassPath
                        : prop.Type.GetTypeName();

                    string arrayIndicator = prop.IsArray ? $"[{prop.ArrayDimensionality}]" : "";
                    suggestions.Add($"{prop.Name} -> {typeName}{arrayIndicator}?{(int)AutoCompleteIcons.SystemVariable}");
                }

                // Format suggestions
                foreach (var prop in matchingProperties)
                {
                    string typeName = prop.IsAppClass && !string.IsNullOrEmpty(prop.AppClassPath)
                        ? prop.AppClassPath
                        : prop.Type.GetTypeName();

                    string arrayIndicator = prop.IsArray ? $"[{prop.ArrayDimensionality}]" : "";
                    suggestions.Add($"{prop.Name} -> {typeName}{arrayIndicator}?{(int)AutoCompleteIcons.SystemVariable}");
                }

                if (suggestions.Count > 0)
                {
                    Debug.Log($"Showing {suggestions.Count} system variable suggestions at position {position}");
                    bool result = ScintillaManager.ShowAutoComplete(editor, AutoCompleteContext.SystemVariables, position, suggestions, customOrder: true);

                    if (!result)
                    {
                        Debug.Log("Failed to show user list popup for system variables.");
                    }
                }
                else
                {
                    Debug.Log($"No matching system variables found for type: {expectedType.Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error showing system variables");
            }
        }

        /// <summary>
        /// Converts a PropertyInfo to TypeInfo for type compatibility checking.
        /// </summary>
        private TypeInfo ConvertPropertyToTypeInfo(PeopleCodeTypeInfo.Functions.PropertyInfo prop)
        {
            if (prop.IsAppClass && !string.IsNullOrEmpty(prop.AppClassPath))
            {
                var baseType = new AppClassTypeInfo(prop.AppClassPath);
                return prop.IsArray
                    ? new ArrayTypeInfo(prop.ArrayDimensionality, baseType)
                    : baseType;
            }
            else
            {
                var baseType = TypeInfo.FromPeopleCodeType(prop.Type);
                return prop.IsArray
                    ? new ArrayTypeInfo(prop.ArrayDimensionality, baseType)
                    : baseType;
            }
        }

        /// <summary>
        /// Formats a method return type for display.
        /// </summary>
        private string FormatReturnType(TypeInfo type, FunctionInfo functionInfo)
        {
            if (functionInfo.ReturnUnionTypes != null && functionInfo.ReturnUnionTypes.Count > 0)
            {
                return string.Join("|", functionInfo.ReturnUnionTypes);
            }

            var returnType = functionInfo.ReturnType;

            if (returnType.Type == PeopleCodeType.SameAsObject)
            {
                byte dimensions = 0;
                
                if (type is ArrayTypeInfo arrayType)
                {
                    dimensions = (byte)arrayType.Dimensions;
                    string elementType = (arrayType.ElementType?.ToString() ?? "Any");
                    return $"array{(dimensions > 1 ? dimensions : "")} of {elementType}";
                }
            }
            else if (returnType.Type == PeopleCodeType.ElementOfObject)
            {
                byte dimensions = 0;

                if (type is ArrayTypeInfo arrayType)
                {
                    dimensions = (byte)(arrayType.Dimensions - 1);
                    string elementType = (arrayType.ElementType?.ToString() ?? "Any");
                    if (dimensions > 0)
                    {
                        return $"array{(dimensions > 1 ? dimensions : "")} of {elementType}";
                    }
                    else 
                    {
                        return $"{elementType}";
                    }
                }
            }

            string arrayPrefix = "";
            if (returnType.IsArray)
            {
                arrayPrefix = $"array{(returnType.ArrayDimensionality > 1 ? returnType.ArrayDimensionality : "")} of ";
            }
            return $"{arrayPrefix}{returnType}";
        }

        /// <summary>
        /// Formats a return type for language extensions.
        /// </summary>
        private string FormatReturnType(TypeInfo contextType, TypeWithDimensionality returnType)
        {
            if (returnType.ArrayDimensionality > 0)
            {
                string arrayPrefix = string.Join("", Enumerable.Repeat("array of ", returnType.ArrayDimensionality));

                if (returnType.IsAppClass)
                {
                    return arrayPrefix + returnType.AppClassPath;
                }
                else
                {
                    string elementTypeName = returnType.Type.GetTypeName();
                    return arrayPrefix + elementTypeName;
                }
            }

            if (returnType.IsAppClass)
            {
                return returnType.AppClassPath ?? "AppClass";
            }

            return returnType.Type.GetTypeName();
        }

        /// <summary>
        /// Formats a property type for display.
        /// </summary>
        private string FormatPropertyType(PeopleCodeTypeInfo.Functions.PropertyInfo prop)
        {
            string typeName;
            if (prop.IsAppClass && !string.IsNullOrEmpty(prop.AppClassPath))
            {
                typeName = prop.AppClassPath;
            }
            else
            {
                typeName = prop.Type.GetTypeName();
            }

            string arrayIndicator = prop.IsArray ? $"[{prop.ArrayDimensionality}]" : "";
            return $"{typeName}{arrayIndicator}";
        }

        /// <summary>
        /// Determines the qualified name for the current program for type inference.
        /// </summary>
        private static string DetermineQualifiedName(ScintillaEditor editor)
        {
            if (editor?.Caption != null && !string.IsNullOrWhiteSpace(editor.Caption))
            {
                // Parse caption to get program identifier
                var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
                if (openTarget != null && editor.Caption.Contains("Application Package"))
                {
                    var methodIndex = Array.IndexOf(openTarget.ObjectIDs, PSCLASSID.METHOD);
                    openTarget.ObjectIDs[methodIndex] = PSCLASSID.NONE;
                    openTarget.ObjectValues[methodIndex] = null;
                    return openTarget.Path;
                }
                else
                {
                    /* probably never what you want but we have to return something? */
                    return "Program";
                }
            }
            else
            {
                /* probably never what you want but we have to return something? */
                return "Program";
            }
        }

        private static void RunTypeInference(ScintillaEditor editor, ProgramNode program)
        {
            try
            {
                string qualifiedName = DetermineQualifiedName(editor);

                var metadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);

                // Get type resolver (may be null if no database)
                var typeResolver = editor.AppDesignerProcess?.TypeResolver;
                string? defaultRecord = null;
                string? defaultField = null;
                if (editor.Caption.EndsWith("(Record PeopleCode)"))
                {
                    var parts = qualifiedName.Split('.');
                    defaultRecord = parts[0];
                    defaultField = parts[1];
                }

                // Run type inference (works even with null resolver)
                TypeInferenceVisitor.Run(program, metadata, typeResolver, defaultRecord, defaultField);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error running type inference for tooltips: {ex.Message}");
            }
        }

        /// <summary>
        /// Formats a variable for display in the suggestion list
        /// </summary>
        private string FormatVariableSuggestion(VariableInfo variable)
        {
            string kindText = variable.Kind switch
            {
                VariableKind.Local => "Local",
                VariableKind.Parameter => "Parameter",
                VariableKind.Instance => "Instance",
                VariableKind.Global => "Global",
                VariableKind.Component => "Component",
                VariableKind.Property => "Property",
                VariableKind.Exception => "Exception",
                VariableKind.Constant => "Constant",
                _ => "Variable"
            };

            // Remove the & prefix from variable name for display since it's already in the document
            string displayName = variable.Name.StartsWith("&") ? variable.Name.Substring(1) : variable.Name;

            // Include type information if available
            if (!string.IsNullOrEmpty(variable.Type) && variable.Type != "any")
            {
                return $"{displayName} -> {variable.Type} ({kindText})";
            }
            else
            {
                return $"{displayName} -> any ({kindText})";
            }
        }

        /// <summary>
        /// Extracts a valid package path (sequence of identifiers separated by colons)
        /// from the portion of a line preceding the cursor.
        /// </summary>
        /// <param name="lineContentBeforeCursor">The line content up to the cursor.</param>
        /// <returns>The extracted package path or empty string if not found.</returns>
        private string ExtractPackagePathFromLine(string lineContent)
        {
            // If the line ends with a colon, we need to extract everything up to that colon
            if (lineContent.EndsWith(':'))
            {
                // Find the last colon before the end
                int colonIndex = lineContent.Length - 1;

                // Extract everything before the colon
                string beforeColon = lineContent.Substring(0, colonIndex);

                // Find the last valid package identifier
                // This could be after a space, another colon, or other delimiters
                int lastDelimiterIndex = Math.Max(
                    Math.Max(
                        beforeColon.LastIndexOf(' '),
                        beforeColon.LastIndexOf('\t')
                    ),
                    Math.Max(
                        beforeColon.LastIndexOf('.'),
                        beforeColon.LastIndexOf('=')
                    )
                );

                // If we found a delimiter, extract the text after it
                if (lastDelimiterIndex >= 0 && lastDelimiterIndex < beforeColon.Length - 1)
                {
                    return beforeColon.Substring(lastDelimiterIndex + 1).Trim();
                }

                // If no delimiter, return the whole thing (rare case)
                return beforeColon.Trim();
            }
            else if (lineContent.Contains(':'))
            {
                // We might be in the middle of a package path like "Package:SubPackage:"
                int lastColonIndex = lineContent.LastIndexOf(':');

                // Start from the last colon and work backward to find the beginning of the path
                string beforeLastColon = lineContent.Substring(0, lastColonIndex);

                // Find the last non-package-path character
                int lastNonPathCharIndex = -1;
                for (int i = beforeLastColon.Length - 1; i >= 0; i--)
                {
                    if (!char.IsLetterOrDigit(beforeLastColon[i]) &&
                        beforeLastColon[i] != '_' &&
                        beforeLastColon[i] != ':')
                    {
                        lastNonPathCharIndex = i;
                        break;
                    }
                }

                // Extract the package path
                if (lastNonPathCharIndex >= 0)
                {
                    return beforeLastColon.Substring(lastNonPathCharIndex + 1);
                }

                return beforeLastColon;
            }

            return string.Empty;
        }

        private BaseRefactor? HandleAppPackageListSelection(ScintillaEditor editor, string selection)
        {
            bool isClassSelection = false;
            string itemText = selection; // The text to potentially insert

            var parts = selection.Split(new[] { " (" }, StringSplitOptions.None); // Split carefully
            if (parts.Length >= 2)
            {
                itemText = parts[0]; // Get the actual item name
                isClassSelection = parts[1].StartsWith("Class)", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Debug.Log($"Could not parse App Package selection: {selection}");
                return null; // Couldn't parse, do nothing
            }


            if (isClassSelection)
            {
                // Insert the class name
                ScintillaManager.InsertTextAtCursor(editor, itemText);

                // Get the current cursor position after insertion
                int currentPos = ScintillaManager.GetCursorPosition(editor);
                int currentLine = ScintillaManager.GetLineFromPosition(editor, currentPos);
                int lineStartPos = ScintillaManager.GetLineStartIndex(editor, currentLine);

                // Get the full line text and trim it to cursor position
                var fullLineText = ScintillaManager.GetCurrentLineText(editor);
                int cursorPosInLine = currentPos - lineStartPos;

                if (cursorPosInLine > 0 && cursorPosInLine <= fullLineText.Length)
                {
                    string lineTextToCursor = fullLineText.Substring(0, cursorPosInLine);
                    string classPath = lineTextToCursor.Split(' ').Last();
                    return new AddImport(editor, classPath);
                }
                else
                {
                    Debug.Log("Could not determine cursor position in line for import extraction");
                    return null;
                }
            }
            else // It's a package selection or from another list type
            {
                // Insert the package name followed by a colon
                ScintillaManager.InsertTextAtCursor(editor, $"{itemText}:");

                // Trigger the suggestions again after a short delay
                // Use Task.Run to avoid blocking the UI thread if SendMessage takes time
                // and capture necessary context.
                IntPtr editorHwnd = editor.hWnd; // Capture HWND
                Task.Delay(100).ContinueWith(_ =>
                {
                    try
                    {
                        int currentPos = ScintillaManager.GetCursorPosition(editor); // Use captured HWND
                        if (currentPos >= 0)
                        {
                            Debug.Log($"Triggering recursive app package suggestion from HandleUserListSelection at pos {currentPos}");
                            ShowAppPackageSuggestions(editor, currentPos); // Call the method to show suggestions

                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex, "Error in delayed ShowAppPackageSuggestions call");
                    }
                }, TaskScheduler.Default); // Use default scheduler

                return null; // No immediate refactoring needed
            }
        }

        public BaseRefactor? HandleQuickFixSelection(ScintillaEditor editor, string selection)
        {

            /* Look through active indicators of the editor, find one that contains a QuickFixDescriptions entry that matches the selection
             * and return the QuickFix refactor type associated with it */

            if (editor == null || !editor.IsValid()) return null;
            foreach (var indicator in editor.ActiveIndicators)
            {
                if (indicator.QuickFixes != null && indicator.QuickFixes.Select(q => q.Description).Contains(selection))
                {
                    var quickFix = indicator.QuickFixes.Where(q => q.Description == selection).FirstOrDefault();
                    var refactorType = quickFix.RefactorClass;
                    if (refactorType == null)
                    {
                        Debug.Log($"No refactor type found for selection '{selection}'");
                        return null;
                    }
                    var instance = Activator.CreateInstance(refactorType, [editor]);
                    if (instance == null)
                    {
                        Debug.Log($"Failed to create instance of refactor type '{refactorType}'");
                        return null;
                    }
                    return (BaseRefactor)instance; // Create an instance of the refactor type
                }
            }

            return null;
        }

        private BaseRefactor? HandleSystemVariableListSelection(ScintillaEditor editor, string selection)
        {
            // Extract the variable name and kind from the formatted selection
            // Format is: "variableName (Type Kind)" or "variableName (Kind)"
            string variableName = selection;
            bool isProperty = false;

            var variableEndIndex = selection.IndexOf(" -> ");
            if (variableEndIndex > 0)
            {
                variableName = selection.Substring(0, variableEndIndex);
            }

            // Replace the partial variable reference with the complete variable name
            try
            {
                // Get current cursor position
                int currentPos = (int)editor.SendMessage(SCI_GETCURRENTPOS, 0, 0);

                // Find the start of the variable reference by looking backwards for the & character
                string content = ScintillaManager.GetScintillaText(editor) ?? "";

                int percentPos = -1;
                for (int i = currentPos - 1; i >= 0; i--)
                {
                    if (i < content.Length && content[i] == '%')
                    {
                        percentPos = i;
                        break;
                    }
                    // Stop if we hit whitespace or other non-identifier characters
                    if (i < content.Length && !char.IsLetterOrDigit(content[i]) && content[i] != '_')
                    {
                        break;
                    }
                }

                if (percentPos >= 0)
                {
                    // Replace from & position to current position with the full variable name
                    editor.SendMessage(SCI_SETSEL, percentPos + 1, currentPos);
                    ScintillaManager.InsertTextAtCursor(editor, variableName);
                    Debug.Log($"Replaced text from position {percentPos + 1} to {currentPos} with {variableName}");
                }
                else
                {
                    // Fallback: just insert the full variable name
                    ScintillaManager.InsertTextAtCursor(editor, variableName);
                    Debug.Log($"Could not find & start position, inserted {variableName} at cursor");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error handling variable selection");
                // Fallback to simple insertion
                ScintillaManager.InsertTextAtCursor(editor, variableName);
            }

            return null; // No refactoring needed for variable insertion
        }
        private BaseRefactor? HandleObjectMemberListSelection(ScintillaEditor editor, string selection)
        {
            // Extract the variable name and kind from the formatted selection
            // Format is: "variableName (Type Kind)" or "variableName (Kind)"
            string memberName = selection;
            bool isMethod = false;
            bool isExtension = selection.Contains(" [Ext]");

            // Extract member name (before " [Ext]" or " -> ")
            var memberEndIndex = selection.IndexOf(" -> ");
            if (memberEndIndex < 0)
            {
                memberEndIndex = selection.IndexOf(" [Ext]");
            }

            if (memberEndIndex > 0)
            {
                memberName = selection.Substring(0, memberEndIndex);
                isMethod = memberName.EndsWith("()");
                if (isMethod)
                {
                    memberName = memberName.Substring(0, memberName.Length - 2);
                }
            }
            Debug.Log($"Variable selected: {memberName} from formatted text: {selection}");

            // If this is an extension, defer transform and let autocomplete insert normally
            if (isExtension)
            {
                try
                {
                    // For property extensions: schedule transform after insertion completes
                    if (!isMethod && ExtensionManager != null)
                    {
                        // Schedule transform after insertion completes
                        Task.Delay(50).ContinueWith(_ =>
                        {
                            mainForm.Invoke((MethodInvoker)delegate
                            {
                                int currentPos = ScintillaManager.GetCursorPosition(editor);
                                mainForm.TryFindAndTransformExtension(
                                    editor,
                                    currentPos,
                                    LanguageExtensionType.Property
                                );
                            });
                        }, TaskScheduler.Default);
                    }

                    // Log and fall through to normal insertion
                    Debug.Log($"{(isMethod ? "Method" : "Property")} extension '{memberName}' - deferring transform");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"Error handling language extension for '{memberName}'");
                    // Fall through to normal insertion on error
                }
                finally
                {
                    // No cache to clear - using AST-based pattern matching
                }
            }

            // Replace the partial variable reference with the complete variable name
            try
            {
                // Get current cursor position
                int currentPos = (int)editor.SendMessage(SCI_GETCURRENTPOS, 0, 0);

                // Find the start of the variable reference by looking backwards for the & character
                string content = ScintillaManager.GetScintillaText(editor) ?? "";

                int dotPos = -1;
                for (int i = currentPos - 1; i >= 0; i--)
                {
                    if (i < content.Length && content[i] == '.')
                    {
                        dotPos = i;
                        break;
                    }
                    // Stop if we hit whitespace or other non-identifier characters
                    if (i < content.Length && !char.IsLetterOrDigit(content[i]) && content[i] != '_')
                    {
                        break;
                    }
                }

                if (dotPos >= 0)
                {
                    // Replace from & position to current position with the full variable name
                    editor.SendMessage(SCI_SETSEL, dotPos + 1, currentPos);
                    ScintillaManager.InsertTextAtCursor(editor, memberName + (isMethod ? "(" : ""));
                    var newPosition = ScintillaManager.GetCursorPosition(editor);
                    Task.Delay(100).ContinueWith((a) =>
                    {
                        WinApi.SendMessage(AppDesignerProcess.CallbackWindow, MainForm.AR_FUNCTION_CALL_TIP, newPosition, '(');
                    });

                    Debug.Log($"Replaced text from position {dotPos + 1} to {currentPos} with {memberName}");
                }
                else
                {
                    // Fallback: just insert the full variable name
                    ScintillaManager.InsertTextAtCursor(editor, memberName);
                    Debug.Log($"Could not find & start position, inserted {memberName} at cursor");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error handling variable selection");
                // Fallback to simple insertion
                ScintillaManager.InsertTextAtCursor(editor, memberName);
            }

            // Clear cache for non-extension selections
            return null; // No refactoring needed for variable insertion
        }

        private BaseRefactor? HandleVariableListSelection(ScintillaEditor editor, string selection)
        {
            // Extract the variable name and kind from the formatted selection
            // Format is: "variableName (Type Kind)" or "variableName (Kind)"
            string variableName = selection;
            bool isProperty = false;

            var variableEndIndex = selection.IndexOf(" -> ");
            if (variableEndIndex > 0)
            {
                variableName = selection.Substring(0, variableEndIndex);

                isProperty = selection.Contains("(Property)");                
            }

            // Determine the appropriate prefix based on variable kind
            string prefix = isProperty ? "%This." : "&";
            string fullVariableName = prefix + variableName;

            Debug.Log($"Variable selected: {variableName} from formatted text: {selection}, isProperty: {isProperty}, using prefix: {prefix}");

            // Replace the partial variable reference with the complete variable name
            try
            {
                // Get current cursor position
                int currentPos = (int)editor.SendMessage(SCI_GETCURRENTPOS, 0, 0);

                // Find the start of the variable reference by looking backwards for the & character
                string content = ScintillaManager.GetScintillaText(editor) ?? "";

                int ampersandPos = -1;
                for (int i = currentPos - 1; i >= 0; i--)
                {
                    if (i < content.Length && content[i] == '&')
                    {
                        ampersandPos = i;
                        break;
                    }
                    // Stop if we hit whitespace or other non-identifier characters
                    if (i < content.Length && !char.IsLetterOrDigit(content[i]) && content[i] != '_')
                    {
                        break;
                    }
                }

                if (ampersandPos >= 0)
                {
                    // Replace from & position to current position with the full variable name
                    editor.SendMessage(SCI_SETSEL, ampersandPos, currentPos);
                    ScintillaManager.InsertTextAtCursor(editor, fullVariableName);
                    Debug.Log($"Replaced text from position {ampersandPos} to {currentPos} with {fullVariableName}");
                }
                else
                {
                    // Fallback: just insert the full variable name
                    ScintillaManager.InsertTextAtCursor(editor, fullVariableName);
                    Debug.Log($"Could not find & start position, inserted {fullVariableName} at cursor");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error handling variable selection");
                // Fallback to simple insertion
                ScintillaManager.InsertTextAtCursor(editor, fullVariableName);
            }

            return null; // No refactoring needed for variable insertion
        }

        /// <summary>
        /// Handles the selection made by the user from an autocomplete list.
        /// </summary>
        /// <param name="editor">The active Scintilla editor.</param>
        /// <param name="selection">The raw text selected by the user.</param>
        /// <param name="listType">The type identifier of the list shown (e.g., 1 for App Packages).</param>
        /// <returns>A ScopedRefactor instance if refactoring is needed (e.g., AddImport), otherwise null.</returns>
        public BaseRefactor? HandleUserListSelection(ScintillaEditor editor, string selection, UserListType listType)
        {
            if (editor == null || !editor.IsValid()) return null;

            switch (listType)
            {
                case UserListType.AppPackage:
                    return HandleAppPackageListSelection(editor, selection);
                case UserListType.QuickFix:
                    // Handle quick fix selection here if needed
                    return HandleQuickFixSelection(editor, selection);
                case UserListType.Variable:
                    return HandleVariableListSelection(editor, selection);
                case UserListType.ObjectMembers:
                    return HandleObjectMemberListSelection(editor, selection);
                case UserListType.SystemVariables:
                    return HandleSystemVariableListSelection(editor, selection);
                default:
                    Debug.Log($"Unknown list type: {listType}");
                    return null;
            }
        }

        /// <summary>
        /// Handles the detection of the "create(" shorthand pattern.
        /// </summary>
        /// <param name="editor">The active Scintilla editor.</param>
        /// <param name="position">The current cursor position where the pattern was completed.</param>
        /// <param name="autoPairingEnabled">Whether auto-pairing is enabled in the editor settings.</param>
        /// <returns>A CreateAutoComplete refactor instance to be processed.</returns>
        public BaseRefactor? PrepareCreateAutoCompleteRefactor(ScintillaEditor editor, int position, bool autoPairingEnabled)
        {
            if (editor == null || !editor.IsValid()) return null;

            Debug.Log($"Create shorthand detected at position {position}. Auto-pairing: {autoPairingEnabled}");
            // Return the refactor instance for MainForm to process
            return new CreateAutoComplete(editor);
        }


        public BaseRefactor? PrepareConcatAutoCompleteRefactor(ScintillaEditor editor)
        {
            if (editor == null || !editor.IsValid()) return null;

            // Return the refactor instance for MainForm to process
            return new ConcatAutoComplete(editor);
        }

        /// <summary>
        /// Handles the detection of the "MsgBox(" shorthand pattern.
        /// </summary>
        /// <param name="editor">The active Scintilla editor.</param>
        /// <param name="position">The current cursor position where the pattern was completed.</param>
        /// <param name="autoPairingEnabled">Whether auto-pairing is enabled in the editor settings.</param>
        /// <returns>A MsgBoxAutoComplete refactor instance to be processed.</returns>
        public BaseRefactor? PrepareMsgBoxAutoCompleteRefactor(ScintillaEditor editor, int position, bool autoPairingEnabled)
        {
            if (editor == null || !editor.IsValid()) return null;

            Debug.Log($"MsgBox shorthand detected at position {position}. Auto-pairing: {autoPairingEnabled}");
            // Return the refactor instance for MainForm to process
            return new MsgBoxAutoComplete(editor, autoPairingEnabled);
        }
    }
}