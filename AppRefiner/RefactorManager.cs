using Antlr4.Runtime.Tree;
using AppRefiner.Events; // For ModifierKeys
using AppRefiner.PeopleCode;
using AppRefiner.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Stores metadata about a discovered refactoring operation.
    /// </summary>
    public readonly struct RefactorInfo
    {
        public Type RefactorType { get; }
        public string Name { get; }
        public string Description { get; }
        public bool RegisterShortcut { get; }
        public ModifierKeys Modifiers { get; }
        public Keys Key { get; }
        public string ShortcutText { get; }

        public RefactorInfo(Type type, string name, string description, bool registerShortcut, ModifierKeys modifiers, Keys key)
        {
            RefactorType = type;
            Name = name;
            Description = description;
            RegisterShortcut = registerShortcut;
            Modifiers = modifiers;
            Key = key;
            ShortcutText = FormatShortcutText(registerShortcut, modifiers, key);
        }

        /// <summary>
        /// Formats the keyboard shortcut text for display.
        /// </summary>
        private static string FormatShortcutText(bool register, ModifierKeys mods, Keys k)
        {
            if (!register || k == Keys.None)
            {
                return string.Empty;
            }

            StringBuilder shortcutText = new StringBuilder(" (");
            if ((mods & ModifierKeys.Control) == ModifierKeys.Control) shortcutText.Append("Ctrl+");
            if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift) shortcutText.Append("Shift+");
            if ((mods & ModifierKeys.Alt) == ModifierKeys.Alt) shortcutText.Append("Alt+");
            shortcutText.Append(k.ToString());
            shortcutText.Append(")");
            return shortcutText.ToString();
        }
    }

    /// <summary>
    /// Manages the discovery, metadata retrieval, and execution of refactoring operations.
    /// </summary>
    public class RefactorManager
    {
        private readonly List<RefactorInfo> availableRefactors = new();
        private readonly MainForm mainForm; // Needed for Invoke, dialog ownership

        public RefactorManager(MainForm form)
        {
            mainForm = form;
            DiscoverAndCacheRefactors();
        }

        /// <summary>
        /// Gets the collection of discovered and cached refactor metadata.
        /// </summary>
        public IEnumerable<RefactorInfo> AvailableRefactors => availableRefactors;

        /// <summary>
        /// Discovers refactors from the main assembly and plugins, extracts metadata,
        /// and caches it.
        /// </summary>
        private void DiscoverAndCacheRefactors()
        {
            availableRefactors.Clear();

            // Discover types (similar to MainForm.DiscoverRefactorTypes)
            var refactorTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(BaseRefactor).IsAssignableFrom(p) &&
                              !p.IsAbstract &&
                              p != typeof(BaseRefactor) &&
                              !p.IsGenericTypeDefinition); // Avoid ScopedRefactor<>

            // Add plugin refactors
            var pluginRefactors = PluginManager.DiscoverRefactorTypes();
            if (pluginRefactors != null)
            {
                refactorTypes = refactorTypes.Concat(pluginRefactors);
            }

            // Filter and extract metadata
            foreach (var type in refactorTypes)
            {
                try
                {
                    // Check if hidden
                    var isHiddenProperty = type.GetProperty("IsHidden", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                    if (isHiddenProperty != null && (bool)isHiddenProperty.GetValue(null)!)
                    {
                        continue; // Skip hidden refactors
                    }

                    // Get metadata using reflection (similar to MainForm logic)
                    string name = GetStaticStringProperty(type, "RefactorName") ?? type.Name;
                    string description = GetStaticStringProperty(type, "RefactorDescription") ?? "Perform refactoring";
                    bool registerShortcut = GetStaticBoolProperty(type, "RegisterKeyboardShortcut");
                    ModifierKeys modifiers = GetStaticEnumProperty<ModifierKeys>(type, "ShortcutModifiers");
                    Keys key = GetStaticEnumProperty<Keys>(type, "ShortcutKey");

                    // Create and cache info
                    availableRefactors.Add(new RefactorInfo(type, name, description, registerShortcut, modifiers, key));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"Error discovering or caching metadata for refactor type: {type.FullName}");
                }
            }
              // Sort by name for consistent ordering
             availableRefactors.Sort((r1, r2) => string.Compare(r1.Name, r2.Name, StringComparison.OrdinalIgnoreCase));
        }

        // Helper methods for reflection
        private static string? GetStaticStringProperty(Type type, string propertyName)
        {
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            return prop?.GetValue(null) as string;
        }

        private static bool GetStaticBoolProperty(Type type, string propertyName)
        {
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            return prop != null && (bool)prop.GetValue(null)!;
        }

        private static T GetStaticEnumProperty<T>(Type type, string propertyName) where T : struct, Enum
        {
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (prop != null && prop.PropertyType.IsEnum && prop.PropertyType == typeof(T)) // Ensure the property type matches T
            {
                 try
                 {
                      object? value = prop.GetValue(null);
                      if (value == null) return default(T); // Return default if value is null

                      // Special handling for ModifierKeys (Flags enum)
                      if (typeof(T) == typeof(ModifierKeys))
                      {
                            // For flags, the combined value is valid, skip Enum.IsDefined
                            return (T)value; 
                      }
                      
                      // Original check for non-flags enums
                      if (Enum.IsDefined(typeof(T), value))
                      {
                           return (T)value;
                      }
                 }
                 catch(InvalidCastException castEx) 
                 { 
                      // Log specific cast error if needed
                      Debug.Log($"Cast exception retrieving {propertyName} from {type.Name}: {castEx.Message}");
                 } 
                 catch (Exception ex) // Catch other potential reflection/conversion errors
                 { 
                      Debug.LogException(ex, $"Error retrieving enum property {propertyName} from {type.Name}");
                      /* Ignore conversion errors, return default */ 
                 }
            }
            return default(T);
        }


        /// <summary>
        /// Executes the provided refactoring operation on the active editor.
        /// (Based on MainForm.ProcessRefactor)
        /// </summary>
        /// <param name="refactorClass">The instantiated refactor class to execute.</param>
        /// <param name="activeEditor">The editor to apply the refactoring to.</param>
        public void ExecuteRefactor(BaseRefactor refactorClass, ScintillaEditor? activeEditor)
        {
            if (activeEditor == null || !activeEditor.IsValid())
            {
                 Debug.Log("ExecuteRefactor called with null or invalid editor.");
                 return;
            }
            if (refactorClass == null)
            {
                 Debug.Log("ExecuteRefactor called with null refactor instance.");
                 return;
            }

            // Use Invoke for UI operations like MessageBox
            Action showErrorMessage = () => MessageBox.Show(
                        mainForm, // Use mainForm as owner
                        "Refactoring Failed",
                        "Refactoring Failed", // Default title
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

            try
            {
                ScintillaManager.ClearAnnotations(activeEditor); // Consider if this should be optional

                var freshText = ScintillaManager.GetScintillaText(activeEditor);
                if (freshText == null)
                {
                    Debug.Log("Failed to get text from editor for refactoring.");
                    mainForm.Invoke(showErrorMessage);
                    return;
                }

                // Capture current cursor position and visible line
                int currentCursorPosition = ScintillaManager.GetCursorPosition(activeEditor);
                int currentFirstVisibleLine = ScintillaManager.GetFirstVisibleLine(activeEditor);

                // Check for pre-visitor dialog
                if (refactorClass.RequiresUserInputDialog && !refactorClass.DeferDialogUntilAfterVisitor)
                {
                    if (!refactorClass.ShowRefactorDialog()) // Pass owner Removed owner
                    {
                         Debug.Log("Refactoring cancelled by user (pre-dialog).");
                         return; // User cancelled
                    }
                }

                // Ensure the refactor uses the latest text
                activeEditor.ContentString = freshText;
                var (program, stream, _) = activeEditor.GetParsedProgram(true); // Force refresh

                // Initialize the refactor
                refactorClass.Initialize(freshText, stream, currentCursorPosition);

                // Run the refactor visitor
                ParseTreeWalker walker = new();
                walker.Walk(refactorClass, program);

                // Check result
                var result = refactorClass.GetResult();
                if (!result.Success)
                {
                    Debug.Log($"Refactoring failed: {result.Message}");
                    // Update message box call to show specific error
                     mainForm.Invoke(() => MessageBox.Show(
                        mainForm,
                        result.Message, // Show specific message
                        "Refactoring Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    ));
                    return;
                }

                // Check for post-visitor dialog
                if (refactorClass.RequiresUserInputDialog && refactorClass.DeferDialogUntilAfterVisitor)
                {
                    if (!refactorClass.ShowRefactorDialog()) // Pass owner Removed owner
                    {
                        Debug.Log("Refactoring cancelled by user (post-dialog).");
                        return; // User cancelled
                    }
                }

                // Apply the refactored code
                var newText = refactorClass.GetRefactoredCode();
                if (newText == null)
                {
                     Debug.Log("Refactoring produced null text output.");
                     // Optionally show an error, but maybe success was just no change needed
                     if (!string.IsNullOrEmpty(result.Message))
                     {
                          // Show success message if provided
                          mainForm.Invoke(() => MessageBox.Show(mainForm, result.Message, "Refactoring Note", MessageBoxButtons.OK, MessageBoxIcon.Information));
                     }
                     return; 
                }

                // TODO: Integrate Scintilla undo transaction?
                // ScintillaManager.BeginUndoAction(activeEditor);
                ScintillaManager.SetScintillaText(activeEditor, newText);
                // ScintillaManager.EndUndoAction(activeEditor);

                // Get and set the updated cursor position and scroll
                int updatedCursorPosition = refactorClass.GetUpdatedCursorPosition();
                if (updatedCursorPosition >= 0)
                {
                    ScintillaManager.SetCursorPositionWithoutScroll(activeEditor, updatedCursorPosition);
                    ScintillaManager.SetFirstVisibleLine(activeEditor, currentFirstVisibleLine); // Restore original view
                }
                else
                {
                     // If no specific position, maybe just restore scroll?
                      ScintillaManager.SetFirstVisibleLine(activeEditor, currentFirstVisibleLine);
                }

                 if (!string.IsNullOrEmpty(result.Message))
                 {
                    // Show success message if provided
                    mainForm.Invoke(() => MessageBox.Show(mainForm, result.Message, "Refactoring Complete", MessageBoxButtons.OK, MessageBoxIcon.Information));
                 }

            }
            catch (Exception ex)
            {
                 Debug.LogException(ex, $"Critical error during ExecuteRefactor for {refactorClass.GetType().Name}");
                 mainForm.Invoke(showErrorMessage); // Show generic error on unexpected exception
            }
        }
    }
} 