using Antlr4.Runtime.Tree;
using AppRefiner.Database;
using AppRefiner.Database.Models;
using AppRefiner.Dialogs;
using AppRefiner.Linters;
using AppRefiner.PeopleCode;
using AppRefiner.Plugins;
using AppRefiner.Refactors;
using AppRefiner.Stylers;
using AppRefiner.Templates;
using AppRefiner.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Text;
using AppRefiner.TooltipProviders;
using AppRefiner.Snapshots;
using System.Runtime.InteropServices;

namespace AppRefiner
{
    public partial class MainForm : Form
    {
        // Services for handling OS-level interactions
        private WinEventService? winEventService;
        private KeyboardShortcutService? keyboardShortcutService;
        private LinterManager? linterManager; // Added LinterManager
        private StylerManager? stylerManager; // Added StylerManager
        private AutoCompleteService? autoCompleteService; // Added AutoCompleteService
        private RefactorManager? refactorManager; // Added RefactorManager
        private SettingsService? settingsService; // Added SettingsService
        private ScintillaEditor? activeEditor = null;

        /// <summary>
        /// Gets the currently active editor
        /// </summary>
        public ScintillaEditor? ActiveEditor => activeEditor;

        private List<ITooltipProvider> tooltipProviders = new();

        // Map of process IDs to their corresponding data managers
        private Dictionary<uint, IDataManager> processDataManagers = new();

        // Static list of available commands
        public static List<Command> AvailableCommands = new();

        public class RuleState
        {
            public string TypeName { get; set; } = "";
            public bool Active { get; set; }
        }

        // Path for linting report output
        private string? lintReportPath;

        private const int WM_SCN_EVENT_MASK = 0x7000;
        private const int SCN_DWELLSTART = 2016;
        private const int SCN_DWELLEND = 2017;
        private const int SCN_SAVEPOINTREACHED = 2002;
        private const int AR_APP_PACKAGE_SUGGEST = 2500; // New constant for app package suggest
        private const int AR_CREATE_SHORTHAND = 2501; // New constant for create shorthand detection
        private const int AR_TYPING_PAUSE = 2502; // New constant for typing pause detection
        private const int SCN_USERLISTSELECTION = 2014; // User list selection notification
        private const int SCI_REPLACESEL = 0x2170; // Constant for SCI_REPLACESEL

        private bool isLoadingSettings = false;

        // Add a private field for the GitRepositoryManager
        private SnapshotManager? snapshotManager;

        // Fields for editor management
        private HashSet<ScintillaEditor> knownEditors = new HashSet<ScintillaEditor>();
        
        // Fields for debouncing SAVEPOINTREACHED events
        private readonly object savepointLock = new object();
        private DateTime lastSavepointTime = DateTime.MinValue;
        private System.Threading.Timer? savepointDebounceTimer = null;
        private ScintillaEditor? pendingSaveEditor = null;
        private const int SAVEPOINT_DEBOUNCE_MS = 300;

        // Add instance of the new TemplateManager
        private TemplateManager templateManager = new TemplateManager();

        // Dictionary to keep track of generated UI controls for template parameters
        private Dictionary<string, Control> currentTemplateInputControls = new Dictionary<string, Control>();

        public MainForm()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Instantiate and start services
            settingsService = new SettingsService(); // Instantiate SettingsService first
            keyboardShortcutService = new KeyboardShortcutService();
            winEventService = new WinEventService();
            winEventService.WindowFocused += HandleWindowFocusEvent; 
            winEventService.Start(); 
            
            // Instantiate LinterManager (passing UI elements)
            // LoadGeneralSettings needs lintReportPath BEFORE LinterManager is created
            settingsService.LoadGeneralSettings(chkInitCollapsed, chkOnlyPPC, chkBetterSQL, chkAutoDark, chkAutoPairing, chkPromptForDB, out lintReportPath);
            linterManager = new LinterManager(this, dataGridView1, lblStatus, progressBar1, lintReportPath, settingsService);
            linterManager.InitializeLinterOptions(); // Initialize linters via the manager
            dataGridView1.CellPainting += dataGridView1_CellPainting; // Wire up CellPainting
            
            // Instantiate StylerManager (passing UI elements)
            stylerManager = new StylerManager(this, dataGridView3, settingsService); 
            stylerManager.InitializeStylerOptions(); // Initialize stylers via the manager

            // Instantiate AutoCompleteService
            autoCompleteService = new AutoCompleteService();

            // Instantiate RefactorManager
            refactorManager = new RefactorManager(this);
            
            // Load templates using the manager and populate ComboBox
            templateManager.LoadTemplates();
            cmbTemplates.Items.Clear();
            templateManager.LoadedTemplates.ForEach(t => cmbTemplates.Items.Add(t));
            if (cmbTemplates.Items.Count > 0)
            {
                cmbTemplates.SelectedIndex = 0;
                // Trigger selection change to initialize UI
                CmbTemplates_SelectedIndexChanged(cmbTemplates, EventArgs.Empty); 
            }
            cmbTemplates.SelectedIndexChanged += CmbTemplates_SelectedIndexChanged;

            RegisterCommands();
            // Initialize the tooltip providers
            TooltipManager.Initialize();
            InitTooltipOptions(); // Needs to run before LoadTooltipStates
            
            // Load Tooltip states using the service
            settingsService.LoadTooltipStates(tooltipProviders, dataGridViewTooltips);

            // Register keyboard shortcuts using the service (using fully qualified Enum access)
            keyboardShortcutService?.RegisterShortcut("CollapseLevel", AppRefiner.ModifierKeys.Alt, Keys.Left, collapseLevelHandler);
            keyboardShortcutService?.RegisterShortcut("ExpandLevel", AppRefiner.ModifierKeys.Alt, Keys.Right, expandLevelHandler);
            keyboardShortcutService?.RegisterShortcut("CollapseAll", AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.Left, collapseAllHandler);
            keyboardShortcutService?.RegisterShortcut("ExpandAll", AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.Right, expandAllHandler);
            keyboardShortcutService?.RegisterShortcut("LintCode", AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.L, lintCodeHandler);
            keyboardShortcutService?.RegisterShortcut("CommandPalette", AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Shift, Keys.P, ShowCommandPalette); // Use the parameterless overload
            keyboardShortcutService?.RegisterShortcut("ApplyTemplate", AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.T, ApplyTemplateCommand);
            keyboardShortcutService?.RegisterShortcut("SuperGoTo", AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.G, SuperGoToCommand); // Use the parameterless overload
            
            // Register refactor shortcuts using the RefactorManager
            RegisterRefactorShortcuts();
            
            // Initialize snapshot manager
            snapshotManager = SnapshotManager.CreateFromSettings();
        }

        // Renamed from keyboard hook handlers to simple action methods
        private void collapseLevelHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.SetLineFoldStatus(activeEditor, true);
        }
        
        private void expandLevelHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.SetLineFoldStatus(activeEditor, false);
        }
        
        private void collapseAllHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.CollapseTopLevel(activeEditor);
        }
        
        private void expandAllHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.ExpandTopLevel(activeEditor);
        }
        
        private void lintCodeHandler()
        {
            if (activeEditor == null) return;
            linterManager?.ProcessLintersForActiveEditor(activeEditor, activeEditor.DataManager);
        }
        
        // Parameterless overload for shortcut service
        private void ShowCommandPalette()
        {
            ShowCommandPalette(null, null); 
        }
        
        // Original handlers for direct KeyPressedEventArgs might need slight adaptation
        // depending on whether the KeyboardShortcutService provides arguments.
        // For now, assume simple Action is sufficient.
        
        // This is called by the Command Palette
        private async void ShowCommandPalette(object? sender, KeyPressedEventArgs? e) // Keep original args for now
        {
            if (activeEditor == null) return;
            var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;
            var handleWrapper = new WindowWrapper(mainHandle);
            // Create the command palette dialog
            var palette = new CommandPaletteDialog(AvailableCommands, mainHandle);

            // Show the dialog
            DialogResult result = palette.ShowDialog(handleWrapper);

            // If a command was selected, execute it with progress dialog
            if (result == DialogResult.OK)
            {
                CommandAction? selectedAction = palette.GetSelectedAction();
                if (selectedAction != null)
                {
                    await ExecuteCommandWithProgressAsync(selectedAction, mainHandle);
                }
            }
        }

        private async Task ExecuteCommandWithProgressAsync(CommandAction commandAction, IntPtr parentHandle)
        {
            // Create progress dialog with parent handle
            var progressDialog = new CommandProgressDialog(parentHandle);

            // Create a task completion source to wait for command execution
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                // Show the dialog without blocking so we can execute the command
                progressDialog.Show(new WindowWrapper(parentHandle));
                progressDialog.BringToFront();
                Application.DoEvents();

                // Execute the command asynchronously
                await Task.Run(() =>
                {
                    try
                    {
                        commandAction(progressDialog);
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        // Handle any exceptions during command execution
                        this.Invoke(() =>
                        {
                            MessageBox.Show("Error executing command: " + ex.Message,
                                "Command Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        });
                        tcs.SetResult(false);
                    }
                });

                // Wait for a short delay to ensure progress is visible
                await Task.Delay(200);
            }
            finally
            {
                // Close the progress dialog
                progressDialog.Close();
                progressDialog.Dispose();
            }
        }

        private void expandAllHandler(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.ExpandTopLevel(activeEditor);
        }

        private void collapseAllHandler(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.CollapseTopLevel(activeEditor);
        }

        private void expandLevelHandler(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.SetLineFoldStatus(activeEditor, false);
        }

        private void collapseLevelHandler(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.SetLineFoldStatus(activeEditor, true);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Clean up all hooks to ensure they're properly removed
            AppRefiner.Events.EventHookInstaller.CleanupAllHooks();
            
            // Clean up the WinEvent hook if active
            if (winEventService != null)
            {
                winEventService.Dispose();
                winEventService = null;
            }
            
            // Dispose the savepoint debounce timer if it exists
            savepointDebounceTimer?.Dispose();
            savepointDebounceTimer = null;
            
            SaveSettings();
        }

        /// <summary>
        /// Set the directory where linting reports will be saved
        /// </summary>
        private void SetLintReportDirectory()
        {
            // Create folder browser dialog
            FolderBrowserDialog folderDialog = new()
            {
                Description = "Select directory for linting reports",
                UseDescriptionForTitle = true,
                SelectedPath = lintReportPath ?? string.Empty
            };

            // Show dialog and update path if OK
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                lintReportPath = folderDialog.SelectedPath;
                Properties.Settings.Default.LintReportPath = lintReportPath;
                Properties.Settings.Default.Save();

                MessageBox.Show($"Lint reports will be saved to: {lintReportPath}",
                    "Lint Report Directory Updated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void SaveSettings()
        {
            // Save general settings via service
            settingsService?.SaveGeneralSettings(
                chkInitCollapsed.Checked,
                chkOnlyPPC.Checked,
                chkBetterSQL.Checked,
                chkAutoDark.Checked,
                chkAutoPairing.Checked,
                chkPromptForDB.Checked,
                lintReportPath
            );

            // Save states for each component via service
            // Note: Requires managers/components to expose their items if service doesn't hold them
            if (linterManager != null) settingsService?.SaveLinterStates(linterManager.LinterRules);
            if (stylerManager != null) settingsService?.SaveStylerStates(stylerManager.StylerRules);
            settingsService?.SaveTooltipStates(tooltipProviders); // Save tooltip states

            // Persist all changes via service
            settingsService?.SaveChanges();
        }

        private void InitTooltipOptions()
        {
            // Get all tooltip providers from the TooltipManager
            tooltipProviders = TooltipManager.Providers.ToList();

            // Update the DataGridView with the tooltip providers
            foreach (var provider in tooltipProviders)
            {
                int rowIndex = dataGridViewTooltips.Rows.Add(provider.Active, provider.Description);
                dataGridViewTooltips.Rows[rowIndex].Tag = provider;
            }
        }

        private void dataGridViewTooltips_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dataGridViewTooltips.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridViewTooltips_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            if (e.ColumnIndex != 0)
            {
                return;
            }
            if (dataGridViewTooltips.Rows[e.RowIndex].Tag == null)
            {
                return;
            }
            if (dataGridViewTooltips.Rows[e.RowIndex].Tag is ITooltipProvider provider)
            {
                provider.Active = (bool)dataGridViewTooltips.Rows[e.RowIndex].Cells[0].Value;
            }
        }

        private void EnableUIActions()
        {
            this.Invoke(() =>
            {
                btnLintCode.Enabled = true;
                btnClearLint.Enabled = true;
                btnApplyTemplate.Text = "Apply Template";

                btnConnectDB.Text = activeEditor?.DataManager == null ? "Connect DB..." : "Disconnect DB";

            });

        }

        private void DisableUIActions()
        {
            this.Invoke(() =>
            {
                btnLintCode.Enabled = false;
                btnClearLint.Enabled = false;
                btnApplyTemplate.Text = "Generate Template";
            });
        }

        private void dataGridView3_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            stylerManager?.HandleStylerGridCellContentClick(sender, e);
        }

        private void dataGridView3_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            stylerManager?.HandleStylerGridCellValueChanged(sender, e);
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            linterManager?.HandleLinterGridCellValueChanged(sender, e);
        }

        private void btnClearLint_Click(object sender, EventArgs e)
        {
            linterManager?.ClearLintResults(activeEditor);
        }


        private void ProcessRefactor(BaseRefactor refactorClass)
        {
            if (activeEditor == null) return;
            ScintillaManager.ClearAnnotations(activeEditor);

            var freshText = ScintillaManager.GetScintillaText(activeEditor);
            if (freshText == null) return;

            // Capture current cursor position
            int currentCursorPosition = ScintillaManager.GetCursorPosition(activeEditor);

            // Capture current first visible line
            int currentFirstVisibleLine = ScintillaManager.GetFirstVisibleLine(activeEditor);

            // Check if this refactor requires user input dialog and is not deferred
            if (refactorClass.RequiresUserInputDialog && !refactorClass.DeferDialogUntilAfterVisitor)
            {
                // Show the dialog and check if user confirmed
                bool dialogConfirmed = refactorClass.ShowRefactorDialog();

                // If user canceled, abort the refactoring
                if (!dialogConfirmed)
                {
                    return;
                }
            }

            // Ensure we have the latest content
            activeEditor.ContentString = freshText;
            
            // Get the parsed program, token stream and comments using the caching mechanism
            var (program, stream, _) = activeEditor.GetParsedProgram(true); // Force refresh to ensure we're using the freshest content

            // Initialize the refactor with cursor position
            refactorClass.Initialize(freshText, stream, currentCursorPosition);

            // Run the refactor
            ParseTreeWalker walker = new();
            walker.Walk(refactorClass, program);

            // Check if refactoring was successful
            var result = refactorClass.GetResult();
            if (!result.Success)
            {
                this.Invoke(() =>
                {
                    MessageBox.Show(
                        this,
                        result.Message,
                        "Refactoring Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                });
                return;
            }

            // Check if this refactor requires a deferred user input dialog
            if (refactorClass.RequiresUserInputDialog && refactorClass.DeferDialogUntilAfterVisitor)
            {
                // Show the dialog and check if user confirmed
                bool dialogConfirmed = refactorClass.ShowRefactorDialog();

                // If user canceled, abort the refactoring
                if (!dialogConfirmed)
                {
                    return;
                }
            }
            // TODO start undo transaction?

            // Apply the refactored code
            var newText = refactorClass.GetRefactoredCode();
            if (newText == null) return;

            ScintillaManager.SetScintillaText(activeEditor, newText);

            // Get and set the updated cursor position
            int updatedCursorPosition = refactorClass.GetUpdatedCursorPosition();
            if (updatedCursorPosition >= 0)
            {
                // Set the cursor position without scrolling to it
                ScintillaManager.SetCursorPositionWithoutScroll(activeEditor, updatedCursorPosition);
                
                // Restore the first visible line
                    ScintillaManager.SetFirstVisibleLine(activeEditor, currentFirstVisibleLine);
            }
        }

        private void btnConnectDB_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            if (activeEditor.DataManager != null)
            {
                activeEditor.DataManager.Disconnect();
                processDataManagers.Remove(activeEditor.ProcessId);
                activeEditor.DataManager = null;
                btnConnectDB.Text = "Connect DB...";
                return;
            }

            var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;
            var handleWrapper = new WindowWrapper(mainHandle);
            DBConnectDialog dialog = new(mainHandle, activeEditor.DBName);

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                IDataManager? manager = dialog.DataManager;
                if (manager != null)
                {
                    processDataManagers[activeEditor.ProcessId] = manager;
                    activeEditor.DataManager = manager;
                    btnConnectDB.Text = "Disconnect DB";
                }
            }
        }

        private async void btnLintCode_Click(object sender, EventArgs e)
        {
            // Set the status label and progress bar before starting the background task
            this.Invoke(() =>
            {
                lblStatus.Text = "Linting...";
                progressBar1.Style = ProgressBarStyle.Marquee;
                progressBar1.MarqueeAnimationSpeed = 30;
            });
            Application.DoEvents();
            
            // Run the linting operation in a background thread via the manager
            await Task.Run(() =>
            {
                // Need to pass current DataManager associated with the active editor
                IDataManager? currentDataManager = null;
                if(activeEditor != null)
                {
                    processDataManagers.TryGetValue(activeEditor.ProcessId, out currentDataManager);
                    // If not found in map, use the one directly on the editor object if available
                    currentDataManager ??= activeEditor.DataManager; 
                }
                linterManager?.ProcessLintersForActiveEditor(activeEditor, currentDataManager);
            });

            // Update the UI after the background task completes
            this.Invoke(() =>
            {
                lblStatus.Text = "Monitoring...";
                progressBar1.Style = ProgressBarStyle.Blocks;
            });
            Application.DoEvents();
        }

        private void CmbTemplates_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbTemplates.SelectedItem is Template selectedTemplate)
            {
                templateManager.ActiveTemplate = selectedTemplate;
                GenerateTemplateUI(); // Call helper to generate UI
            }
            else
            {
                templateManager.ActiveTemplate = null;
                pnlTemplateParams.Controls.Clear(); // Clear panel if no template selected
                currentTemplateInputControls.Clear();
            }
        }

        /// <summary>
        /// Generates the UI controls for the currently active template's parameters.
        /// </summary>
        private void GenerateTemplateUI()
        {
            pnlTemplateParams.Controls.Clear();
            currentTemplateInputControls.Clear(); // Clear previous controls

            if (templateManager.ActiveTemplate == null) return;

            var definitions = templateManager.GetParameterDefinitionsForActiveTemplate();

            if (definitions == null || definitions.Count == 0)
            {
                return;
            }

            const int labelWidth = 150;
            const int controlWidth = 200;
            const int verticalSpacing = 30;
            const int horizontalPadding = 10;
            int currentY = 10;

            foreach (var definition in definitions)
            {
                // Create label for parameter
                Label label = new Label
                {
                    Text = definition.Label + ":",
                    Location = new Point(horizontalPadding, currentY + 3),
                    Size = new Size(labelWidth, 20),
                    AutoSize = false,
                    Visible = definition.IsVisible,
                    Tag = definition.Id // Store input ID in Tag for easy reference
                };
                pnlTemplateParams.Controls.Add(label);

                // Create input control based on parameter type
                Control inputControl;
                switch (definition.Type.ToLower())
                {
                    case "boolean":
                        var chkBox = new CheckBox
                        {
                            Checked = definition.CurrentValue.Equals("true", StringComparison.OrdinalIgnoreCase),
                            Location = new Point(labelWidth + (horizontalPadding * 2), currentY),
                            Size = new Size(controlWidth, 23),
                            Text = "", // No text needed since we have the label
                            Visible = definition.IsVisible,
                            Tag = definition.Id // Store input ID in Tag
                        };
                        // Add event handler to update manager and regenerate UI
                        chkBox.CheckedChanged += TemplateControl_ValueChanged;
                        inputControl = chkBox;
                        break;

                    default: // Default to TextBox for string, number, etc.
                        var txtBox = new TextBox
                        {
                            Text = definition.CurrentValue,
                            Location = new Point(labelWidth + (horizontalPadding * 2), currentY),
                            Size = new Size(controlWidth, 23),
                            Visible = definition.IsVisible,
                            Tag = definition.Id // Store input ID in Tag
                        };
                         // Add event handler to update manager and regenerate UI
                        txtBox.TextChanged += TemplateControl_ValueChanged;
                        inputControl = txtBox;
                        break;
                }

                // Add tooltip if description is available
                if (!string.IsNullOrEmpty(definition.Description))
                {
                    ToolTip tooltip = new ToolTip();
                    tooltip.SetToolTip(inputControl, definition.Description);
                    tooltip.SetToolTip(label, definition.Description);
                }

                pnlTemplateParams.Controls.Add(inputControl);
                currentTemplateInputControls[definition.Id] = inputControl; // Store reference

                if (definition.IsVisible)
                {
                     currentY += verticalSpacing;
                }
            }
            
            // Reflow controls after initial generation
            ReflowTemplateUI();
        }

        /// <summary>
        /// Event handler for when a template parameter control's value changes.
        /// Updates the TemplateManager and potentially regenerates the UI.
        /// </summary>
        private void TemplateControl_ValueChanged(object? sender, EventArgs e)
        {
            if (sender is Control control && control.Tag is string inputId)
            {
                string newValue = "";
                if (control is CheckBox chk) {
                    newValue = chk.Checked ? "true" : "false";
                } else if (control is TextBox txt) {
                    newValue = txt.Text;
                }
                // Add other control types if needed

                templateManager.UpdateParameterValue(inputId, newValue);
                
                // Regenerate UI to handle potential changes in display conditions
                GenerateTemplateUI(); 
            }
        }

        /// <summary>
        /// Reflows the template parameter controls in the panel to remove gaps from hidden controls.
        /// </summary>
        private void ReflowTemplateUI()
        {
            const int verticalSpacing = 30;
            int currentY = 10;

            // Order controls based on their original definition order if possible,
            // otherwise iterate through the panel's controls.
            // Assuming controls were added in order: Label then Input for each parameter.
            var orderedIds = templateManager.GetParameterDefinitionsForActiveTemplate()?.Select(d => d.Id).ToList() ?? new List<string>();

            foreach (string id in orderedIds)
            {
                // Find the Label and Control pair for this ID
                var label = pnlTemplateParams.Controls.OfType<Label>().FirstOrDefault(lbl => lbl.Tag as string == id);
                var control = currentTemplateInputControls.TryGetValue(id, out var ctrl) ? ctrl : null;

                if (label != null && control != null)
                {
                    if (label.Visible) // Assume if label is visible, control should be too
                    {
                        label.Location = new Point(label.Location.X, currentY + 3);
                        control.Location = new Point(control.Location.X, currentY);
                        currentY += verticalSpacing;
                    }
                }
            }
        }


        private void btnApplyTemplate_Click(object? sender, EventArgs e)
        {
             if (activeEditor == null) {
                 MessageBox.Show("No active editor to apply template to.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
             }
             if (templateManager.ActiveTemplate == null) {
                 MessageBox.Show("No template selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
             }
             
             // Validate inputs using the manager
             if (!templateManager.ValidateInputs())
             {
                 MessageBox.Show("Please fill in all required fields.", "Required Fields Missing",
                     MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
             }
             
             // Check for replacement warning only if it's not insert mode
             if (!templateManager.ActiveTemplate.IsInsertMode && !string.IsNullOrWhiteSpace(ScintillaManager.GetScintillaText(activeEditor)))
             {
                var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;
                var handleWrapper = new WindowWrapper(mainHandle);
                 using var confirmDialog = new TemplateConfirmationDialog(
                     "Applying this template will replace all content in the current editor. Do you want to continue?",
                     mainHandle);

                 if (confirmDialog.ShowDialog(handleWrapper) != DialogResult.Yes)
                 {
                     return; // User cancelled replacement
                 }
             }

             // Apply the template using the manager
             templateManager.ApplyActiveTemplateToEditor(activeEditor);
        }

        private void ShowGeneratedTemplateDialog(string content, string title)
        {
            var dialog = new Form
            {
                Text = $"Generated Template: {title}",
                Size = new Size(800, 600),
                StartPosition = FormStartPosition.CenterParent
            };

            var textBox = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                Text = content,
                Font = new Font("Consolas", 10)
            };

            dialog.Controls.Add(textBox);
            dialog.ShowDialog();
        }

        private void RegisterCommands()
        {
            // Clear any existing commands
            AvailableCommands.Clear();

            // Add editor commands with "Editor:" prefix
            AvailableCommands.Add(new Command(
                "Editor: Lint Current Code (Ctrl+Alt+L)",
                "Run linting rules against the current editor",
                (progressDialog) =>
                {
                    if (activeEditor == null) return;
                    // Delegate to button click handler which uses the manager
                        progressDialog?.UpdateHeader("Running linters...");
                    linterManager?.ProcessLintersForActiveEditor(activeEditor, activeEditor.DataManager);
                }
            ));

            AvailableCommands.Add(new Command(
                "Editor: Dark Mode",
                "Apply dark mode to the current editor",
                (progressDialog) =>
                {
                    if (activeEditor != null)
                    {
                        progressDialog?.UpdateHeader("Applying dark mode...");
                        ScintillaManager.SetDarkMode(activeEditor);
                    }
                }
            ));

            AvailableCommands.Add(new Command(
                "Editor: Collapse All (Ctrl+Alt+Left)",
                "Collapse all foldable sections",
                () =>
                {
                    if (activeEditor != null)
                        ScintillaManager.CollapseTopLevel(activeEditor);
                }
            ));

            AvailableCommands.Add(new Command(
                "Editor: Expand All (Ctrl+Alt+Right)",
                "Expand all foldable sections",
                () =>
                {
                    if (activeEditor != null)
                        ScintillaManager.ExpandTopLevel(activeEditor);
                }
            ));

            // Add toggle commands for MainForm checkboxes
            AvailableCommands.Add(new Command(
                "Editor: Toggle Auto Collapse",
                () => chkInitCollapsed.Checked ? "Auto Collapse is ON - Click to disable" : "Auto Collapse is OFF - Click to enable",
                () =>
                {
                    chkInitCollapsed.Checked = !chkInitCollapsed.Checked;
                }
            ));

            AvailableCommands.Add(new Command(
                "Editor: Toggle Only PeopleCode Editors",
                () => chkOnlyPPC.Checked ? "Only PeopleCode is ON - Click to disable" : "Only PeopleCode is OFF - Click to enable",
                () =>
                {
                    chkOnlyPPC.Checked = !chkOnlyPPC.Checked;
                }
            ));

            AvailableCommands.Add(new Command(
                "Editor: Toggle Auto Dark Mode",
                () => chkAutoDark.Checked ? "Auto Dark Mode is ON - Click to disable" : "Auto Dark Mode is OFF - Click to enable",
                () =>
                {
                    chkAutoDark.Checked = !chkAutoDark.Checked;
                }
            ));

            AvailableCommands.Add(new Command(
                "Editor: Toggle Auto Format SQL",
                () => chkBetterSQL.Checked ? "Auto Format SQL is ON - Click to disable" : "Auto Format SQL is OFF - Click to enable",
                () =>
                {
                    chkBetterSQL.Checked = !chkBetterSQL.Checked;
                }
            ));




            // Add styler toggle commands with "Styler:" prefix
            foreach (var styler in stylerManager?.StylerRules ?? new List<BaseStyler>())
            {
                AvailableCommands.Add(new Command(
                    $"Styler: Toggle {styler.Description}",
                    () => styler.Active ? $"Currently enabled - Click to disable" : $"Currently disabled - Click to enable",
                    () =>
                    {
                        styler.Active = !styler.Active;
                        
                        stylerManager?.ProcessStylersForEditor(activeEditor);

                        // Update corresponding grid row if exists
                        var row = dataGridView3.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Tag is BaseStyler s && s == styler);
                        if (row != null)
                        {
                            row.Cells[0].Value = styler.Active;
                        }
                    }
                ));
            }

            // Add refactoring commands using RefactorManager
            if (refactorManager != null)
            {
                foreach (var refactorInfo in refactorManager.AvailableRefactors)
                {
                    // Capture the info for the lambda
                    RefactorInfo currentRefactorInfo = refactorInfo; 
                AvailableCommands.Add(new Command(
                        $"Refactor: {currentRefactorInfo.Name}{currentRefactorInfo.ShortcutText}",
                        currentRefactorInfo.Description,
                    () =>
                    {
                        if (activeEditor != null)
                        {
                                try
                                {
                                    // Create an instance of the refactor
                            var refactor = (BaseRefactor?)Activator.CreateInstance(
                                        currentRefactorInfo.RefactorType,
                                        [activeEditor] // Assuming constructor takes ScintillaEditor
                            );

                            if (refactor != null)
                            {
                                        // Execute via the manager
                                        refactorManager.ExecuteRefactor(refactor, activeEditor);
                                    }
                                    else
                                    {
                                          Debug.LogError($"Failed to create instance of refactor: {currentRefactorInfo.RefactorType.FullName}");
                                          MessageBox.Show(this, "Error creating refactor instance.", "Refactor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                }
                                catch (Exception ex)
                                {
                                     Debug.LogException(ex, $"Error instantiating or executing refactor: {currentRefactorInfo.RefactorType.FullName}");
                                     MessageBox.Show(this, $"Error running refactor: {ex.Message}", "Refactor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                ));
                }
            }

            // Add the suppress lint errors command (special case)
            AvailableCommands.Add(new Command(
                "Linter: Suppress lint errors",
                "Suppress lint errors with configurable scope",
                () =>
                {
                    if (activeEditor != null)
                    {
                        // Instantiate the specific refactor
                        var suppressRefactor = new SuppressReportRefactor(activeEditor);
                        // Execute via the manager
                        refactorManager?.ExecuteRefactor(suppressRefactor, activeEditor);
                    }
                }
            ));

            // Add individual linter commands with "Lint: " prefix
            // Need to get linters from the manager
            if (linterManager != null)
            {
                foreach (var linter in linterManager.LinterRules)
                {
                    // Capture the linter instance for the lambda
                    BaseLintRule currentLinter = linter; 
                AvailableCommands.Add(new Command(
                        $"Lint: {currentLinter.Description}",
                        $"Run {currentLinter.Description} linting rule",
                    () =>
                    {
                            if (activeEditor != null) {
                                // Need to pass current DataManager
                                IDataManager? currentDataManager = null;
                                processDataManagers.TryGetValue(activeEditor.ProcessId, out currentDataManager);
                                currentDataManager ??= activeEditor.DataManager; 
                                linterManager?.ProcessSingleLinter(currentLinter, activeEditor, currentDataManager);
                            }
                        }
                    ));
                }
            }

            // Add linter toggle commands
            if (linterManager != null)
            {
                foreach (var linter in linterManager.LinterRules)
                {
                    BaseLintRule currentLinter = linter; // Capture instance
                AvailableCommands.Add(new Command(
                        $"Lint: Toggle {currentLinter.Description}",
                        () => currentLinter.Active ? $"Currently enabled - Click to disable" : $"Currently disabled - Click to enable",
                    () =>
                    {
                            currentLinter.Active = !currentLinter.Active;

                        // Update corresponding grid row if exists
                        var row = dataGridView1.Rows.Cast<DataGridViewRow>()
                                .FirstOrDefault(r => r.Tag is BaseLintRule l && l == currentLinter);
                        if (row != null)
                        {
                                row.Cells[0].Value = currentLinter.Active;
                        }
                    }
                ));
                }
            }

            // Add database commands with dynamic enabled states
            AvailableCommands.Add(new Command(
                "Database: Connect to DB",
                "Connect to database for advanced functionality",
                () =>
                {
                    if (activeEditor != null)
                    {
                        var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;
                        var handleWrapper = new WindowWrapper(mainHandle);
                        DBConnectDialog dialog = new(mainHandle, activeEditor.DBName);
                        dialog.StartPosition = FormStartPosition.CenterParent;

                        if (dialog.ShowDialog(handleWrapper) == DialogResult.OK)
                        {
                            IDataManager? manager = dialog.DataManager;
                            if (manager != null)
                            {
                                processDataManagers[activeEditor.ProcessId] = manager;
                                activeEditor.DataManager = manager;
                            }
                        }
                    }
                },
                () => activeEditor != null && activeEditor.DataManager == null
            ));

            AvailableCommands.Add(new Command(
                "Database: Disconnect DB",
                "Disconnect from current database",
                () =>
                {
                    if (activeEditor != null && activeEditor.DataManager != null)
                    {
                        activeEditor.DataManager.Disconnect();
                        processDataManagers.Remove(activeEditor.ProcessId);
                        activeEditor.DataManager = null;
                    }
                },
                () => activeEditor != null && activeEditor.DataManager != null
            ));

            // Add clear annotations command
            AvailableCommands.Add(new Command(
                "Editor: Clear Annotations",
                "Clear all annotations from the current editor",
                () =>
                {
                    if (activeEditor != null)
                    {
                        ScintillaManager.ClearAnnotations(activeEditor);
                    }
                }
            ));

            // Add Force Refresh command
            AvailableCommands.Add(new Command(
                "Editor: Force Refresh",
                "Force the editor to refresh styles",
                () =>
                {
                    if (activeEditor != null)
                    {
                        // Clear content string to force re-reading
                        activeEditor.ContentString = null;
                        // Clear annotations
                        ScintillaManager.ClearAnnotations(activeEditor);
                        // Reset styles
                        ScintillaManager.ResetStyles(activeEditor);
                        // Update status
                        lblStatus.Text = "Force refreshing editor...";
                        CheckForContentChanges(activeEditor);
                    }
                }
            ));

            // Add project linting commands
            AvailableCommands.Add(new Command(
                "Project: Set Lint Report Directory",
                // Need to get path from manager or settings
                 () => $"Current directory: {lintReportPath ?? "Not Set"}", 
                (progressDialog) =>
                {
                    linterManager?.SetLintReportDirectory();
                }
            ));

            AvailableCommands.Add(new Command(
                "Project: Lint Project",
                "Run all linters on the entire project and generate a report",
                (progressDialog) =>
                {
                    if (activeEditor != null)
                     { // Pass active editor for context
                          linterManager?.LintProject(activeEditor, progressDialog);
                    }
                },
                () => activeEditor != null && activeEditor.DataManager != null
            ));

            AvailableCommands.Add(new Command(
                "Template: Apply Template (Ctrl+Alt+T)",
                "Apply a template to the current editor",
                () =>
                {
                    // Directly trigger the button click logic
                    btnApplyTemplate_Click(null, EventArgs.Empty); 
                },
                () => activeEditor != null
            ));

            AvailableCommands.Add(new Command(
                "Navigation: Go To Definition",
                "\"Navigate to methods, properties, functions, getters, and setters within the current file\"",
                () =>
                {
                    // No action needed
                    SuperGoToCommand();
                },
                () => activeEditor != null
            ));

            // Git
            // Add Git Revert to Previous Version command
            AvailableCommands.Add(new Command(
                "Snapshot: Revert to Previous Version",
                "View file history and revert to a previous snapshot",
                (progressDialog) =>
                {
                    try
                    {
                        if (activeEditor == null)
                        {
                            MessageBox.Show("No active editor found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        if (string.IsNullOrEmpty(activeEditor.RelativePath))
                        {
                            MessageBox.Show("This editor is not associated with a file in the Git repository.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        // Get process handle for dialog ownership
                        var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;

                        // Show the Git history dialog
                        progressDialog?.UpdateHeader("Loading commit history...");

                        // Run on UI thread to show dialog
                        this.Invoke(() =>
                        {
                            using var historyDialog = new Dialogs.SnapshotHistoryDialog(snapshotManager, activeEditor, mainHandle);

                            if (historyDialog.ShowDialog(new WindowWrapper(mainHandle)) == DialogResult.OK)
                            {
                                progressDialog?.UpdateHeader("Reverted to previous version");
                                progressDialog?.UpdateProgress($"File reverted to version from {historyDialog.SelectedSnapshot?.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss") ?? "unknown"}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Error in Git revert command: {ex.Message}");
                        progressDialog?.UpdateProgress($"Error: {ex.Message}");
                        MessageBox.Show($"Error: {ex.Message}", "Git Revert Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                },
                () => activeEditor != null && !string.IsNullOrEmpty(activeEditor.RelativePath) &&
                      snapshotManager != null
            ));
        }

        private void btnPlugins_Click(object sender, EventArgs e)
        {
            // Load plugins from the plugin directory
            string pluginDirectory = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty,
                Properties.Settings.Default.PluginDirectory);

            PluginManagerDialog dialog = new(pluginDirectory);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // Update the PluginDirectory setting and save it
                Properties.Settings.Default.PluginDirectory = dialog.PluginDirectory;
                Properties.Settings.Default.Save();
            }
        }

        private void RegisterRefactorShortcuts()
        {
            // Use RefactorManager to get shortcut info
            if (refactorManager == null || keyboardShortcutService == null) return;

            foreach (var refactorInfo in refactorManager.AvailableRefactors)
            {
                // Check if this refactor wants a keyboard shortcut
                if (refactorInfo.RegisterShortcut && refactorInfo.Key != Keys.None)
                {
                    // Capture info for the lambda
                    RefactorInfo currentRefactorInfo = refactorInfo;

                    // Register the shortcut using the service
                    bool registered = keyboardShortcutService.RegisterShortcut(
                        currentRefactorInfo.Name, // Use Name for unique ID
                        currentRefactorInfo.Modifiers, 
                        currentRefactorInfo.Key, 
                        () => // Action lambda
                        {
                            if (activeEditor == null) return;

                            try
                            {
                                var newRefactor = (BaseRefactor?)Activator.CreateInstance(
                                    currentRefactorInfo.RefactorType,
                                    [activeEditor] // Assuming constructor takes ScintillaEditor
                                );

                                if (newRefactor != null)
                                {
                                    // Execute via manager
                                    refactorManager.ExecuteRefactor(newRefactor, activeEditor);
                                }
                                else
                                {
                                     Debug.LogError($"Failed to create instance for shortcut: {currentRefactorInfo.RefactorType.FullName}");
                                     MessageBox.Show(this, "Error creating refactor instance for shortcut.", "Refactor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                                Debug.LogException(ex, $"Error instantiating or executing refactor from shortcut: {currentRefactorInfo.RefactorType.FullName}");
                                MessageBox.Show(this, $"Error running refactor from shortcut: {ex.Message}", "Refactor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    );

                    if (!registered)
                    {
                         Debug.LogWarning($"Failed to register shortcut for refactor: {currentRefactorInfo.Name}");
                    }
                }
            }
        }

        private ScintillaEditor? SetActiveEditor(IntPtr hwnd)
        {
            try
            {
                // Check if this is the same editor we already have
                if (activeEditor != null && activeEditor.hWnd == hwnd)
                {
                    // Same editor as before - just return it
                    return activeEditor;
                }
                
                // This is a different editor or we didn't have one before
                try
                {
                    var editor = ScintillaManager.GetEditor(hwnd);
                    activeEditor = editor;
                    
                    return editor;
                }
                catch (Exception ex)
                {
                    Debug.Log($"Error getting Scintilla editor: {ex.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Exception in GetActiveEditor: {ex.Message}");
                return null;
            }
        }

        // Check if content has changed and process if necessary
        private void CheckForContentChanges(ScintillaEditor editor)
        {
            if (editor == null) return;


            if (chkBetterSQL.Checked && editor.Type == EditorType.SQL)
            {
                ScintillaManager.ApplyBetterSQL(editor);
            }

            // Apply dark mode whenever content changes if auto dark mode is enabled
            if (chkAutoDark.Checked)
            {
                ScintillaManager.SetDarkMode(editor);
            }

            // Process stylers for PeopleCode
            if (editor.Type == EditorType.PeopleCode)
            {
                stylerManager?.ProcessStylersForEditor(editor);
            }

            if (!editor.HasLexilla || editor.Type == EditorType.SQL || editor.Type == EditorType.Other)
            {
                // Perform folding ourselves 
                // 1. if they are missing Lexilla
                // 2. if it is a SQL object 
                // 3. if its an editor type we don't know
                DoExplicitFolding();
            }

        }

        /* TODO: override WndProc */
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Need activeEditor for most messages, but check null within specific cases
            // if (activeEditor == null) return; 
            // Removed early return, check activeEditor inside cases

            /* if message is a WM_SCN_EVENT (check the mask) */
            if ((m.Msg & WM_SCN_EVENT_MASK) == WM_SCN_EVENT_MASK)
            {
                 // Only process if we have an active editor
                 if (activeEditor == null || !activeEditor.IsValid()) return;

                /* remove mask */
                var eventCode = m.Msg & ~WM_SCN_EVENT_MASK;

                switch (eventCode)
                {
                    case SCN_DWELLSTART:
                        Debug.Log($"SCN_DWELLSTART: {m.WParam} -- {m.LParam}");
                        TooltipProviders.TooltipManager.ShowTooltip(activeEditor, m.WParam.ToInt32(), m.LParam.ToInt32());
                        break;
                    case SCN_DWELLEND:
                        TooltipProviders.TooltipManager.HideTooltip(activeEditor);
                        break;
                    case SCN_SAVEPOINTREACHED:
                        Debug.Log("SAVEPOINTREACHED...");
                        // Active editor check already done above
                            lock (savepointLock)
                            {
                                // Cancel any pending savepoint timer
                                savepointDebounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                                // Store the editor for later processing
                                pendingSaveEditor = activeEditor;

                                // Record the time of this savepoint
                                lastSavepointTime = DateTime.Now;

                                // Start a new timer to process this savepoint after the debounce period
                                savepointDebounceTimer = new System.Threading.Timer(
                                    ProcessSavepoint, null, SAVEPOINT_DEBOUNCE_MS, Timeout.Infinite);
                        }
                        break;
                    case SCN_USERLISTSELECTION:
                        Debug.Log("User list selection received");
                        // Active editor check already done above
                        // wParam is the list type
                        int listType = m.WParam.ToInt32();
                        // lParam is a pointer to a UTF8 string in the editor's process memory
                        if (m.LParam != IntPtr.Zero)
                        {
                            // Read the UTF8 string from the editor's process memory
                            string? selectedText = ScintillaManager.ReadUtf8FromMemory(activeEditor, m.LParam, 256);
                            
                            if (!string.IsNullOrEmpty(selectedText))
                            {
                                Debug.Log($"User selected: {selectedText} (list type: {listType})");
                                // Call the AutoCompleteService
                                var refactor = autoCompleteService?.HandleUserListSelection(activeEditor, selectedText, listType);
                                if (refactor != null)
                                {
                                    // Execute via RefactorManager
                                    refactorManager?.ExecuteRefactor(refactor, activeEditor);
                                }
                            }
                        }
                        break;
                }
            }
            else if (m.Msg == AR_APP_PACKAGE_SUGGEST)
            {
                 // Only process if we have an active editor and service
                 if (activeEditor == null || !activeEditor.IsValid() || autoCompleteService == null) return;

                /* Handle app package suggestion request */
                Debug.Log($"Received app package suggest message. WParam: {m.WParam}, LParam: {m.LParam}");
                
                // WParam contains the current cursor position
                int position = m.WParam.ToInt32();
                
                // Call the AutoCompleteService
                autoCompleteService.ShowAppPackageSuggestions(activeEditor, position);
            }
            else if (m.Msg == AR_CREATE_SHORTHAND)
            {
                 // Only process if we have an active editor and service
                 if (activeEditor == null || !activeEditor.IsValid() || autoCompleteService == null) return;

                /* Handle create shorthand detection */
                Debug.Log($"Received create shorthand message. WParam: {m.WParam}, LParam: {m.LParam}");
                
                // WParam contains auto-pairing status (bool)
                bool autoPairingEnabled = m.WParam.ToInt32() != 0;
                
                // LParam contains the current cursor position
                int position = m.LParam.ToInt32();
                
                // Call the AutoCompleteService
                var refactor = autoCompleteService.PrepareCreateAutoCompleteRefactor(activeEditor, position, autoPairingEnabled);
                if (refactor != null)
                {
                     // Execute via RefactorManager
                    refactorManager?.ExecuteRefactor(refactor, activeEditor);
                }
            }
            else if (m.Msg == AR_TYPING_PAUSE)
            {
                /* Handle typing pause detection */
                int position = m.WParam.ToInt32();
                int line = m.LParam.ToInt32();
                
                Debug.Log($"Received typing pause message. Position: {position}, Line: {line}");
                
                // Only process if we have an active editor
                if (activeEditor != null && activeEditor.IsValid())
                {
                    // Log the typing pause event
                    Debug.Log($"User stopped typing at position {position}, line {line}");
                    
                    // Process the editor content now that typing has paused
                    // This replaces the periodic scanning from the timer
                    CheckForContentChanges(activeEditor);
                }
            }
        }

        private async void DoExplicitFolding()
        {
            if (activeEditor == null)
            {
                return;
            }

            // Set the status label and progress bar before starting the background task
            this.Invoke(() =>
            {
                lblStatus.Text = "Folding...";
                progressBar1.Style = ProgressBarStyle.Marquee;
                progressBar1.MarqueeAnimationSpeed = 30;
            });
            Application.DoEvents();
            // Run the folding operation in a background thread
            await Task.Run(() =>
            {
                // Ensure the activeEditor is not null before proceeding
                if (activeEditor != null)
                {
                    ScintillaManager.SetFoldRegions(activeEditor);
                }
            });

            // Update the UI after the background task completes
            this.Invoke(() =>
            {
                lblStatus.Text = "Monitoring...";
                progressBar1.Style = ProgressBarStyle.Blocks;
            });
            Application.DoEvents();
        }

        public void SuperGoToCommand()
        {
            if (activeEditor == null) return;

            try
            {
                // Parse the content using the parser
                var definitions = CollectGoToDefinitions();

                if (definitions.Count == 0)
                {
                    Debug.Log("No definitions found in the file");
                    return;
                }

                // Create and show the definition selection dialog
                IntPtr mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;

                 ShowGoToDefinitionDialog(definitions, mainHandle);
               
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in GoToDefinitionCommand: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the definition selection dialog
        /// </summary>
        private void ShowGoToDefinitionDialog(List<GoToCodeDefinition> definitions, IntPtr mainHandle)
        {
            var handleWrapper = new WindowWrapper(mainHandle);
            var dialog = new DefinitionSelectionDialog(definitions, mainHandle);

            DialogResult result = dialog.ShowDialog(handleWrapper);

            if (result == DialogResult.OK)
            {
                GoToCodeDefinition? selectedDefinition = dialog.SelectedDefinition;
                if (activeEditor != null && selectedDefinition != null)
                {
                    // Navigate to the selected definition
                    ScintillaManager.SetCursorPosition(activeEditor, selectedDefinition.Position);

                    /* Get line from position and make that line the first visible line */
                    var lineNumber = ScintillaManager.GetLineFromPosition(activeEditor, selectedDefinition.Position);
                    ScintillaManager.SetFirstVisibleLine(activeEditor, lineNumber);
                    var startIndex = ScintillaManager.GetLineStartIndex(activeEditor, lineNumber);
                    var length = ScintillaManager.GetLineLength(activeEditor, lineNumber);
                    ScintillaManager.SetSelection(activeEditor, startIndex, startIndex + length);

                }
            }
        }

        /// <summary>
        /// Collects all definitions from the editor using the ANTLR parser
        /// </summary>
        /// <param name="editor">The editor to collect definitions from</param>
        /// <returns>A list of code definitions</returns>
        private List<GoToCodeDefinition> CollectGoToDefinitions()
        {
            if (activeEditor == null) return [];

            // Use the parse tree from the editor if available, otherwise parse it now
            var (program, tokenStream, comments) = activeEditor.GetParsedProgram();

            // Create and run the visitor to collect definitions
            var visitor = new GoToDefinitionVisitor();
            ParseTreeWalker.Default.Walk(visitor, program);

            return visitor.Definitions;
        }


        private void ApplyTemplateCommand()
        {
            // This method is now primarily for the command palette / hotkey
            // It should trigger the same logic as the button click.
            btnApplyTemplate_Click(null, EventArgs.Empty);
        }

        private void btnDebugLog_Click(object sender, EventArgs e)
        {
            Debug.Log("Displaying debug dialog...");
            Debug.ShowDebugDialog(Handle);
        }
        
        // Handle a newly detected editor
        private void ProcessNewEditor(ScintillaEditor editor)
        {
            if (editor == null) return;
            
            if (!editor.IsValid())
            {
                ScintillaManager.CleanupEditor(editor);
                return;
            }

            // Check if there's recently a new datamanager for this editor
            if (processDataManagers.TryGetValue(editor.ProcessId, out IDataManager? dataManager))
            {
                editor.DataManager = dataManager;
            }

            // Update the active editor reference
            activeEditor = editor;
            EnableUIActions();
            
            // If "only PPC" is checked and the editor is not PPC, skip
            if (chkOnlyPPC.Checked && editor.Type != EditorType.PeopleCode)
            {
                return;
            }
            
            if (!editor.FoldEnabled)
            {
                Debug.Log($"Found new editor via WinEvent, enabling folding. HWND: {editor.hWnd:X}, PID: {editor.ProcessId:X} Thread: {editor.ThreadID:X}");
                
                if (chkAutoDark.Checked)
                {
                    ScintillaManager.SetDarkMode(editor);
                }
                
                Debug.Log($"Editor isn't subclassed: {editor.hWnd}");
                bool success = EventHookInstaller.SubclassWindow(editor.ThreadID, WindowHelper.GetParentWindow(editor.hWnd), this.Handle);
                Debug.Log($"Window subclassing result: {success}");
                ScintillaManager.SetMouseDwellTime(editor, 1000);
                
                
                ScintillaManager.EnableFolding(editor);
                ScintillaManager.FixEditorTabs(editor, !chkBetterSQL.Checked);
                editor.FoldEnabled = true;
                
                if (chkBetterSQL.Checked && editor.Type == EditorType.SQL)
                {
                    ScintillaManager.ApplyBetterSQL(editor);
                }
                
                if (chkInitCollapsed.Checked)
                {
                    ScintillaManager.CollapseTopLevel(editor);
                }
                
                // Save initial content to Git repository
                if (!string.IsNullOrEmpty(editor.RelativePath))
                {
                    SaveSnapshot(editor);
                }
            }

            /* If promptForDB is set, lets check if we have a datamanger already? if not, prompt for a db connection */
            if (chkPromptForDB.Checked && editor.DataManager == null)
            {
                ConnectToDB();
            }

        }

        private void ConnectToDB()
        {
            if (activeEditor == null) return;

            var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;
            var handleWrapper = new WindowWrapper(mainHandle);
            
            // Pass the editor's DBName to the dialog constructor
            DBConnectDialog dialog = new(mainHandle, activeEditor.DBName);
            dialog.StartPosition = FormStartPosition.CenterParent;

            if (dialog.ShowDialog(handleWrapper) == DialogResult.OK)
            {
                IDataManager? manager = dialog.DataManager;
                if (manager != null)
                {
                    processDataManagers[activeEditor.ProcessId] = manager;
                    activeEditor.DataManager = manager;
                }
            }
        }

        /// <summary>
        /// Saves the content of the editor to the Git repository
        /// </summary>
        /// <param name="editor">The editor to save content from</param>
        private void SaveSnapshot(ScintillaEditor editor)
        {
            if (editor == null || string.IsNullOrEmpty(editor.RelativePath))
            {
                return;
            }
            
            try
            {
                // Get content from editor
                string? content = ScintillaManager.GetScintillaText(editor);
                if (string.IsNullOrEmpty(content))
                {
                    Debug.Log($"No content to save for editor: {editor.hWnd:X}");
                    return;
                }
                
                // Save and commit the content
                snapshotManager?.SaveEditorSnapshot(editor, content);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error saving to Git repository: {ex.Message}");
            }
        }

        // Add this new method to process the savepoint after debouncing
        private void ProcessSavepoint(object? _)
        {
            ScintillaEditor? editorToSave = null;
            
            lock (savepointLock)
            {
                // If there's no pending editor, just return
                if (pendingSaveEditor == null)
                    return;
                
                // Get the editor to save
                editorToSave = pendingSaveEditor;
                
                // Clear the pending editor
                pendingSaveEditor = null;
            }
            
            try
            {
                // Make sure we're on the UI thread
                this.Invoke(() =>
                {
                    // Check if the editor is still valid
                    if (editorToSave != null && editorToSave.IsValid())
                    {
                        Debug.Log($"Processing debounced SAVEPOINTREACHED for {editorToSave.RelativePath}");
                        lock (ScintillaManager.editorsExpectingSavePoint)
                        {
                            if (ScintillaManager.editorsExpectingSavePoint.Contains(editorToSave.hWnd))
                            {
                                // Remove the editor from the list of expecting save points
                                ScintillaManager.editorsExpectingSavePoint.Remove(editorToSave.hWnd);
                                return;
                            }
                        }
                        // Reset editor state
                        editorToSave.ContentString = null;
                        
                        // Clear annotations and reset styles
                        ScintillaManager.ClearAnnotations(editorToSave);
                        ScintillaManager.ResetStyles(editorToSave);
                        
                        // Save content to Git repository
                        if (!string.IsNullOrEmpty(editorToSave.RelativePath))
                        {
                            SaveSnapshot(editorToSave);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.Log($"Error processing debounced savepoint: {ex.Message}");
            }
        }

        // Renamed from WinEventProc and updated signature for EventHandler
        private void HandleWindowFocusEvent(object? sender, IntPtr hwnd)
        {
             // Check if the focused window is a Scintilla window
             StringBuilder className = new StringBuilder(256);
             NativeMethods.GetClassName(hwnd, className, className.Capacity); // Use NativeMethods
             
             if (className.ToString().Contains("Scintilla"))
             {
                 // Ensure hwnd is owned by "pside.exe"
                 NativeMethods.GetWindowThreadProcessId(hwnd, out var processId); // Use NativeMethods
                 try
                 {
                     if ("pside".Equals(Process.GetProcessById((int)processId).ProcessName, StringComparison.OrdinalIgnoreCase))
                     {
                         Debug.Log($"WinEvent detected Scintilla window focus: 0x{hwnd.ToInt64():X}");
                         
                         // The event handler is already invoked on the correct synchronization context
                         // by WinEventService, so no need for BeginInvoke here.
                         
                         // Handle potential focus loss on the previous editor first
                         if (activeEditor != null && hwnd != activeEditor.hWnd && !activeEditor.IsValid())
                         {
                             Debug.Log("Previous active editor lost focus or became invalid.");
                              activeEditor = null;
                              Stylers.InvalidAppClass.ClearValidAppClassPathsCache(); // Example cleanup
                         }
                         
                         // Use SetActiveEditor to properly handle the newly focused window
                         var newlyFocusedEditor = SetActiveEditor(hwnd);
 
                         if (newlyFocusedEditor != null && newlyFocusedEditor.IsValid())
                             {
                                 if (!newlyFocusedEditor.FoldEnabled) // Check if it's truly a *new* editor needing init
                                 {
                                     ProcessNewEditor(newlyFocusedEditor);
                                 }
                                 else
                                 {
                                     // Editor already known and initialized, just check content
                                     CheckForContentChanges(newlyFocusedEditor);
                                 }
                             }
                         else
                         {
                             // Focused editor is null or invalid
                             if(activeEditor?.hWnd == hwnd) // If the invalid one was our active one
                             {
                                 activeEditor = null; // Clear active editor
                             }
                         }
                     }
                 }
                 catch (ArgumentException) { /* Process might have exited */ }
             }
        }

        // Add this new CellPainting event handler method
        private void dataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // Check if it's the button column (index 2) and a valid row
            if (e.ColumnIndex == 2 && e.RowIndex >= 0)
            {
                // Check the tag of the cell
                var cell = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];
                if (cell.Tag?.ToString() == "NoConfig")
                {
                    // Paint the background to match the grid's default background
                    using (Brush backColorBrush = new SolidBrush(SystemColors.Control))
                    using (Pen gridLinePen = new Pen(dataGridView1.GridColor,1)) // Use the grid color for the border
                    {
                        // Erase the cell background
                        e.Graphics.FillRectangle(backColorBrush, e.CellBounds);

                        // Draw the grid lines (border) - Adjust coordinates slightly for standard appearance
                        e.Graphics.DrawRectangle(gridLinePen, e.CellBounds.Left - 1, e.CellBounds.Top - 1, e.CellBounds.Width, e.CellBounds.Height);

                        // Prevent default painting (including hover effects)
                        e.Handled = true;
                    }
                }
                // Allow default painting for normal button cells or other columns
            }
        }

    }
    
}


