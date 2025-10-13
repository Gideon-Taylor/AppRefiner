using AppRefiner.Refactors; // For ScopedRefactor, AddImport, CreateAutoComplete
using System.Runtime.InteropServices; // For DllImport
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;

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

        protected override void OnExitPropertyGetterScope(ScopeContext scope, PropertyNode node, Dictionary<string, object> customData)
        {
            base.OnExitPropertyGetterScope(scope, node, customData);
            if (node.GetterBody != null && node.GetterBody.SourceSpan.ContainsPosition(targetPosition))
            {
                accessibleVariables = GetAccessibleVariables(scope).ToList();
            }
        }

        protected override void OnExitPropertySetterScope(ScopeContext scope, PropertyNode node, Dictionary<string, object> customData)
        {
            base.OnExitPropertySetterScope(scope, node, customData);
            if (node.SetterBody != null && node.SetterBody.SourceSpan.ContainsPosition(targetPosition))
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
        public enum UserListType
        {
            AppPackage = 1,
            QuickFix = 2,
            Variable = 3
        }

        // Constants related to Scintilla messages (can be kept private if only used here)
        private const int SCI_LINEFROMPOSITION = 2166;
        private const int SCI_POSITIONFROMLINE = 2167;
        private const int SCI_GETCURRENTPOS = 2008;
        private const int SCI_SETSEL = 2160;
        private const int AR_APP_PACKAGE_SUGGEST = 2500; // Keep for recursive call

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

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
                    // Show the user list popup with app package suggestions
                    // ListType 1 indicates App Package suggestions
                    Debug.Log($"Showing {suggestions.Count} app package suggestions for '{packagePath}'");
                    bool result = ScintillaManager.ShowUserList(editor, UserListType.AppPackage, position, suggestions);

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

                // Use the variable collector to get accessible variables at the current position
                var collector = new VariableCollector(position);
                collector.VisitProgram(program);

                var accessibleVariables = collector.GetAccessibleVariables();

                // Convert to list of strings for autocomplete, filtering out duplicates by name
                List<string> suggestions = new();
                var variableGroups = accessibleVariables
                    .GroupBy(v => v.Name)
                    .Select(g => g.First()) // Take first occurrence of each variable name
                    .OrderBy(v => v.Name);

                foreach (var variable in variableGroups)
                {
                    // Format variable with its type and kind for better context
                    string suggestion = FormatVariableSuggestion(variable);
                    suggestions.Add(suggestion);
                }

                if (suggestions.Count > 0)
                {
                    Debug.Log($"Showing {suggestions.Count} variable suggestions at position {position}");
                    bool result = ScintillaManager.ShowUserList(editor, UserListType.Variable, position, suggestions);

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
                return $"{displayName} ({variable.Type} {kindText})";
            }
            else
            {
                return $"{displayName} ({kindText})";
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

        private BaseRefactor? HandleVariableListSelection(ScintillaEditor editor, string selection)
        {
            // Extract the variable name and kind from the formatted selection
            // Format is: "variableName (Type Kind)" or "variableName (Kind)"
            string variableName = selection;
            bool isProperty = false;

            var openParenIndex = selection.IndexOf(" (");
            if (openParenIndex > 0)
            {
                variableName = selection.Substring(0, openParenIndex);

                // Extract the kind information to determine if it's a property
                string kindInfo = selection.Substring(openParenIndex + 2); // Skip " ("
                var closeParenIndex = kindInfo.LastIndexOf(')');
                if (closeParenIndex > 0)
                {
                    kindInfo = kindInfo.Substring(0, closeParenIndex);
                    // Check if the kind contains "Property"
                    isProperty = kindInfo.Contains("Property");
                }
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