using AppRefiner.Database;
using AppRefiner.Database.Models;
using AppRefiner.PeopleCode;
using AppRefiner.Refactors; // For BaseRefactor, AddImport, CreateAutoComplete
using AppRefiner.Stylers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices; // For DllImport
using System.Text;
using System.Threading.Tasks;
using static SqlParser.Ast.Expression;

namespace AppRefiner
{
    /// <summary>
    /// Provides services for handling code auto-completion features.
    /// </summary>
    public class AutoCompleteService
    {
        public enum UserListType
        {
            AppPackage = 1,
            QuickFix = 2
        }

        // Constants related to Scintilla messages (can be kept private if only used here)
        private const int SCI_LINEFROMPOSITION = 2166;
        private const int SCI_POSITIONFROMLINE = 2167;
        private const int AR_APP_PACKAGE_SUGGEST = 2500; // Keep for recursive call

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public void ShowQuickFixSuggestions(ScintillaEditor? editor, int position)
        {
            if (editor == null || !editor.IsValid()) return;

            List<string> quickFixList = new List<string>();

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
                List<string> suggestions = new List<string>();
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
                    return (BaseRefactor)instance ; // Create an instance of the refactor type
                }
            }

            return null;
        }

        /// <summary>
        /// Handles the selection made by the user from an autocomplete list.
        /// </summary>
        /// <param name="editor">The active Scintilla editor.</param>
        /// <param name="selection">The raw text selected by the user.</param>
        /// <param name="listType">The type identifier of the list shown (e.g., 1 for App Packages).</param>
        /// <returns>A BaseRefactor instance if refactoring is needed (e.g., AddImport), otherwise null.</returns>
        public BaseRefactor? HandleUserListSelection(ScintillaEditor editor, string selection, UserListType listType)
        {
            if (editor == null || !editor.IsValid()) return null;

            switch(listType)
            {
                case UserListType.AppPackage:
                    return HandleAppPackageListSelection(editor, selection);
                case UserListType.QuickFix:
                    // Handle quick fix selection here if needed
                    return HandleQuickFixSelection(editor, selection);
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
            return new CreateAutoComplete(editor, autoPairingEnabled);
        }


        public BaseRefactor? PrepareConcatAutoCompleteRefactor(ScintillaEditor editor) {
            if (editor == null || !editor.IsValid()) return null;

            // Return the refactor instance for MainForm to process
            return new ConcatAutoComplete(editor);
        }
    }
} 