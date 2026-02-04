using AppRefiner.Dialogs;
using AppRefiner.Plugins;
using PeopleCodeParser.SelfHosted.Visitors;
using System.Diagnostics;
using System.Reflection;
using System.Text;

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
                return "";
            }

            StringBuilder shortcutText = new();
            if ((mods & ModifierKeys.Control) == ModifierKeys.Control) shortcutText.Append("Ctrl+");
            if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift) shortcutText.Append("Shift+");
            if ((mods & ModifierKeys.Alt) == ModifierKeys.Alt) shortcutText.Append("Alt+");
            shortcutText.Append(k.ToString());
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
        private readonly DataGridView? refactorGrid; // DataGridView for refactor options

        public RefactorManager(MainForm form, DataGridView? refactorOptionsGrid = null)
        {
            mainForm = form;
            refactorGrid = refactorOptionsGrid;
            DiscoverAndCacheRefactors();

            // Load refactor configurations
            RefactorConfigManager.LoadRefactorConfigs();
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

            // Discover types from main assembly only (not AppDomain, which includes plugin assemblies)
            var refactorTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(p => typeof(BaseRefactor).IsAssignableFrom(p) &&
                              !p.IsAbstract &&
                              !p.IsGenericTypeDefinition);

            // Add plugin refactors
            var pluginRefactors = PluginManager.DiscoverRefactorTypes();
            if (pluginRefactors != null)
            {
                refactorTypes = refactorTypes.Concat(pluginRefactors).Distinct();
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

        /// <summary>
        /// Initializes the refactor options grid if provided
        /// </summary>
        public void InitializeRefactorOptions()
        {
            if (refactorGrid == null) return;

            refactorGrid.Rows.Clear();

            // Plugin discovery should remain here or be moved to a central PluginService
            string pluginDirectory = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty,
                Properties.Settings.Default.PluginDirectory);

            PluginManager.LoadPlugins(pluginDirectory);

            // Refresh discovery after loading plugins
            DiscoverAndCacheRefactors();

            foreach (var refactorInfo in availableRefactors)
            {
                int rowIndex = refactorGrid.Rows.Add(refactorInfo.Description, String.IsNullOrEmpty(refactorInfo.ShortcutText) ? "Cmd Palette" : refactorInfo.ShortcutText);
                refactorGrid.Rows[rowIndex].Tag = refactorInfo;

                var configurableProperties = refactorInfo.RefactorType.GetConfigurableProperties();
                DataGridViewButtonCell buttonCell = (DataGridViewButtonCell)refactorGrid.Rows[rowIndex].Cells[2];

                if (configurableProperties.Count > 0)
                {
                    buttonCell.Value = "Configure...";
                    refactorGrid.Rows[rowIndex].Cells[2].Tag = null;
                }
                else
                {
                    buttonCell.Value = string.Empty;
                    buttonCell.ReadOnly = true;
                    buttonCell.FlatStyle = FlatStyle.Flat;
                    buttonCell.Style.BackColor = SystemColors.Control;
                    buttonCell.Style.ForeColor = SystemColors.Control;
                    buttonCell.Style.SelectionBackColor = SystemColors.Control;
                    buttonCell.Style.SelectionForeColor = SystemColors.Control;
                    refactorGrid.Rows[rowIndex].Cells[2].Tag = "NoConfig";
                }
            }
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
                catch (InvalidCastException castEx)
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
        public void ExecuteRefactor(BaseRefactor refactorClass, ScintillaEditor? activeEditor, bool showUserMessages = true)
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

            try
            {
                //ScintillaManager.ClearAnnotations(activeEditor); // Consider if this should be optional

                activeEditor.ContentString = ScintillaManager.GetScintillaText(activeEditor);
                if (activeEditor.ContentString == null)
                {
                    Debug.Log("Failed to get text from editor for refactoring.");
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        // Show message box with specific error
                        var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                        var handleWrapper = new WindowWrapper(mainHandle);
                        new MessageBoxDialog("Refactoring failed", "Refactoring Failed", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                    });
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

                var (program, tokens) = activeEditor.GetParsedProgramWithTokens(true); // Force refresh

                // Check if parsing was successful and if this refactor can run on incomplete parses
                if (program == null || (!activeEditor.IsSelfHostedParseSuccessful && !refactorClass.RunOnIncompleteParse))
                {
                    Debug.Log($"Skipping refactor '{refactorClass.GetType().Name}' due to parse errors and RunOnIncompleteParse=false");
                    if (showUserMessages)
                    {
                        Task.Delay(100).ContinueWith(_ =>
                        {
                            var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                            var handleWrapper = new WindowWrapper(mainHandle);
                            new MessageBoxDialog($"The refactor '{refactorClass.GetType().Name}' cannot run because there are syntax errors in the code.\n\n" +
                                "Please fix the syntax errors first, then try the refactor again.",
                                "Refactor Skipped - Syntax Errors", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                        });
                    }
                    return;
                }

                // Initialize the refactor
                refactorClass.Initialize(activeEditor.ContentString, currentCursorPosition);

                // NEW: Apply configuration just-in-time before visitor runs
                 RefactorConfigManager.ApplyConfigurationToInstance(refactorClass);

                // Run the refactor visitor using AST visitor pattern
                if (refactorClass is IAstVisitor visitor)
                {
                    program.Accept(visitor);
                }
                else
                {
                    Debug.Log($"Refactor {refactorClass.GetType().Name} does not implement IAstVisitor");
                    if (showUserMessages)
                    {
                        Task.Delay(100).ContinueWith(_ =>
                        {
                            var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                            var handleWrapper = new WindowWrapper(mainHandle);
                            new MessageBoxDialog($"The refactor {refactorClass.GetType().Name} cannot be executed because it does not implement IAstVisitor.",
                                "Refactor Error", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                        });
                    }
                    return;
                }

                // Check result
                var result = refactorClass.GetResult();
                if (!result.Success)
                {
                    Debug.Log($"Refactoring failed: {result.Message}");
                    // Update message box call to show specific error

                    Task.Delay(100).ContinueWith(_ =>
                    {
                        // Show message box with specific error
                        var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                        var handleWrapper = new WindowWrapper(mainHandle);
                        new MessageBoxDialog(result.Message ?? "Refactoring failed", "Refactoring Failed", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                    });

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

                // Apply refactoring changes
                ScintillaManager.BeginUndoAction(activeEditor); // Start undo action for all changes
                var refactorResult = refactorClass.ApplyRefactoring();
                ScintillaManager.EndUndoAction(activeEditor); // End undo action

                if (!refactorResult.Success)
                {
                    Debug.Log($"Refactoring failed: {refactorResult.Message}");
                    if (showUserMessages)
                    {
                        Task.Delay(100).ContinueWith(_ =>
                        {
                            var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                            var handleWrapper = new WindowWrapper(mainHandle);
                            new MessageBoxDialog(refactorResult.Message ?? "Refactoring failed", "Refactoring Failed", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                        });
                    }
                    return;
                }


                // Restore original scroll position (cursor positioning is handled by Scintilla automatically)
                ScintillaManager.SetFirstVisibleLine(activeEditor, currentFirstVisibleLine);

                // Check for and execute follow-up refactor
                Type? followUpType = refactorClass.FollowUpRefactorType;
                if (followUpType != null)
                {
                    Debug.Log($"Executing follow-up refactor: {followUpType.Name}");
                    try
                    {
                        // Instantiate the follow-up refactor
                        if (Activator.CreateInstance(followUpType, activeEditor) is BaseRefactor followUpRefactor)
                        {
                            // Execute it immediately
                            ExecuteRefactor(followUpRefactor, activeEditor); // Recursive call
                        }
                        else
                        {
                            Debug.Log($"Failed to create instance of follow-up refactor type: {followUpType.FullName}");
                            if (showUserMessages)
                            {
                                Task.Delay(100).ContinueWith(_ =>
                                {
                                    var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                                    var handleWrapper = new WindowWrapper(mainHandle);
                                    new MessageBoxDialog($"Could not start follow-up refactor: {followUpType.Name}", "Follow-up Error", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                                });
                            }
                        }
                    }
                    catch (Exception followUpEx)
                    {
                        Debug.LogException(followUpEx, $"Error executing follow-up refactor {followUpType.Name}");
                        if (showUserMessages)
                        {
                            Task.Delay(100).ContinueWith(_ =>
                            {
                                var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                                var handleWrapper = new WindowWrapper(mainHandle);
                                new MessageBoxDialog($"An error occurred during the follow-up refactor: {followUpType.Name}\n\n{followUpEx.Message}", "Follow-up Error", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                            });
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(result.Message) && showUserMessages) // Only show initial message if no follow-up
                {
                    // Show success message if provided
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                        var handleWrapper = new WindowWrapper(mainHandle);
                        new MessageBoxDialog(result.Message, "Refactoring Complete", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                    });
                }

            }
            catch (Exception ex)
            {
                Debug.LogException(ex, $"Critical error during ExecuteRefactor for {refactorClass.GetType().Name}");
                Task.Delay(100).ContinueWith(_ =>
                {
                    // Show message box with specific error
                    var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                    var handleWrapper = new WindowWrapper(mainHandle);
                    new MessageBoxDialog($"Execption during refactor: {ex.ToString()}", "Refactoring Failed", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                });
            }
        }

        // --- Grid Event Handlers (to be called from MainForm) ---

        public void HandleRefactorGridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (refactorGrid == null) return;

            refactorGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            if (e.ColumnIndex == 2 && e.RowIndex >= 0)
            {
                if (refactorGrid.Rows[e.RowIndex].Tag is RefactorInfo refactorInfo)
                {
                    if (refactorGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag?.ToString() != "NoConfig")
                    {
                        // Show configuration dialog for the refactor type
                        using var dialog = new RefactorConfigDialog(refactorInfo.RefactorType);
                        dialog.ShowDialog(mainForm); // Show dialog owned by MainForm
                    }
                }
            }
        }

    }
}