using AppRefiner.Database;
using AppRefiner.Database.Models;
using AppRefiner.PeopleCode;
using AppRefiner.Refactors; // For BaseRefactor, AddImport, CreateAutoComplete
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices; // For DllImport
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner
{
    /// <summary>
    /// Provides services for handling code auto-completion features.
    /// </summary>
    public class AutoCompleteService
    {
        // Constants related to Scintilla messages (can be kept private if only used here)
        private const int SCI_LINEFROMPOSITION = 0x2166;
        private const int SCI_POSITIONFROMLINE = 0x2167;
        private const int AR_APP_PACKAGE_SUGGEST = 2500; // Keep for recursive call

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);


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
                    bool result = ScintillaManager.ShowUserList(editor, 1, position, suggestions);

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
        private string ExtractPackagePathFromLine(string lineContentBeforeCursor)
        {
            // Trim trailing whitespace which might interfere
            lineContentBeforeCursor = lineContentBeforeCursor.TrimEnd();

            // Work backwards from the end to find the start of the package path
            int endIndex = lineContentBeforeCursor.Length;
            int startIndex = -1;

            for (int i = endIndex - 1; i >= 0; i--)
            {
                char c = lineContentBeforeCursor[i];
                // Valid characters in a package path are letters, digits, underscore, and colon
                if (char.IsLetterOrDigit(c) || c == '_' || c == ':')
                {
                    // Continue scanning backwards
                    continue;
                }
                else
                {
                    // Found a character not allowed in a package path, the path starts after this
                    startIndex = i + 1;
                    break;
                }
            }

            // If we scanned all the way to the beginning
            if (startIndex == -1)
            {
                startIndex = 0;
            }

            // Extract the potential path
            string potentialPath = lineContentBeforeCursor.Substring(startIndex, endIndex - startIndex);

            // Validate: Must contain at least one colon, and not end with double colon etc.
            // Clean up potential leading/trailing colons for robustness before final check
            potentialPath = potentialPath.Trim(':');
            if (string.IsNullOrEmpty(potentialPath) || !potentialPath.Contains(':'))
            {
                return string.Empty; // Not a valid multi-part path
            }

            // Further validation could be added here if needed (e.g., check for invalid sequences like "::")

            return potentialPath;
        }


        /// <summary>
        /// Handles the selection made by the user from an autocomplete list.
        /// </summary>
        /// <param name="editor">The active Scintilla editor.</param>
        /// <param name="selection">The raw text selected by the user.</param>
        /// <param name="listType">The type identifier of the list shown (e.g., 1 for App Packages).</param>
        /// <returns>A BaseRefactor instance if refactoring is needed (e.g., AddImport), otherwise null.</returns>
        public BaseRefactor? HandleUserListSelection(ScintillaEditor editor, string selection, int listType)
        {
            if (editor == null || !editor.IsValid()) return null;

            bool isClassSelection = false;
            string itemText = selection; // The text to potentially insert

            // If listType is 1 (App Package), parse the selection
            if (listType == 1)
            {
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
            }

            if (isClassSelection)
            {
                // Insert the class name
                ScintillaManager.InsertTextAtCursor(editor, itemText);

                // Prepare the AddImport refactor
                // We need the *last* identifier inserted, which should be itemText
                 return new AddImport(editor, itemText); // Return refactor for MainForm to process
            }
            else // It's a package selection or from another list type
            {
                // Insert the package name followed by a colon
                ScintillaManager.InsertTextAtCursor(editor, $"{itemText}:");

                 // Check if the list type was App Package (1)
                if (listType == 1)
                {
                    // Trigger the suggestions again after a short delay
                    // Use Task.Run to avoid blocking the UI thread if SendMessage takes time
                    // and capture necessary context.
                     IntPtr editorHwnd = editor.hWnd; // Capture HWND
                     Task.Delay(100).ContinueWith(_ =>
                     {
                         try
                         {
                             int currentPos = ScintillaManager.GetCursorPosition(editor); // Use captured HWND
                             if(currentPos >= 0)
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
                }
                 return null; // No immediate refactoring needed
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
    }
} 