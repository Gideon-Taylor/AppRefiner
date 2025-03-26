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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Text;

namespace AppRefiner
{
    public partial class MainForm : Form
    {
        // Delegate used for both EnumWindows and EnumChildWindows.
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        System.Threading.Timer? scanTimer;
        private bool timerRunning = false;
        private object scanningLock = new();
        private ScintillaEditor? activeEditor = null;
        
        /// <summary>
        /// Gets the currently active editor
        /// </summary>
        public ScintillaEditor? ActiveEditor => activeEditor;
        
        private List<BaseLintRule> linterRules = new();
        private List<BaseStyler> stylers = new(); // Changed from List<BaseStyler> analyzers

        // Map of process IDs to their corresponding data managers
        private Dictionary<uint, IDataManager> processDataManagers = new();
        private Dictionary<string, Control> templateInputControls = new();
        private Dictionary<string, Control> templateInputLabels = new();
        private Dictionary<string, DisplayCondition> templateInputsDisplayConditions = new();

        private static HashSet<uint> ThreadsWithEventHook = new();
        // Static list of available commands
        public static List<Command> AvailableCommands = new();

        // Standard keyboard hooks
        KeyboardHook collapseLevel = new();
        KeyboardHook expandLevel = new();
        KeyboardHook collapseAll = new();
        KeyboardHook expandAll = new();
        KeyboardHook commandPaletteHook = new();
        KeyboardHook lintCodeHook = new();
        KeyboardHook applyTemplateHook = new();

        private class RuleState
        {
            public string TypeName { get; set; } = "";
            public bool Active { get; set; }
        }

        // Dictionary to track registered keyboard shortcuts and their key combinations
        private Dictionary<string, KeyboardHook> refactorShortcuts = new();
        private Dictionary<string, (ModifierKeys, Keys)> registeredShortcuts = new();

        // Path for linting report output
        private string? lintReportPath;

        private const int WM_SCN_EVENT_MASK = 0x7000;
        private const int SCN_DWELLSTART = 2016;
        private const int SCN_DWELLEND = 2017;
        private const int WM_DWELLSTART = WM_SCN_EVENT_MASK | SCN_DWELLSTART;
        private const int WM_DWELLEND= WM_SCN_EVENT_MASK | SCN_DWELLEND;



        public MainForm()
        {
            InitializeComponent();
            InitLinterOptions();
            InitStylerOptions(); // Changed from InitAnalyzerOptions
            RegisterCommands(); // Register the default commands
        }

        protected override void OnLoad(EventArgs e)
        {
            // Initialize the linting report path
            lintReportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Properties.Settings.Default.LintReportPath);

            // Ensure the directory exists
            if (!Directory.Exists(lintReportPath))
            {
                try
                {
                    Directory.CreateDirectory(lintReportPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to create lint report directory: " + ex.Message,
                        "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            LoadSettings();
            collapseLevel.KeyPressed += collapseLevelHandler;
            collapseLevel.RegisterHotKey(AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.Left);

            expandLevel.KeyPressed += expandLevelHandler;
            expandLevel.RegisterHotKey(AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.Right);

            collapseAll.KeyPressed += collapseAllHandler;
            collapseAll.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.Left);

            expandAll.KeyPressed += expandAllHandler;
            expandAll.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.Right);

            lintCodeHook.KeyPressed += lintCodeHandler;
            lintCodeHook.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.L);

            commandPaletteHook.KeyPressed += ShowCommandPalette;
            commandPaletteHook.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Shift, System.Windows.Forms.Keys.P);

            applyTemplateHook.KeyPressed += (s, e) => ApplyTemplateCommand();
            applyTemplateHook.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.T);

            // Register standard shortcuts in the tracking dictionary
            registeredShortcuts["CollapseLevel"] = (AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.Left);
            registeredShortcuts["ExpandLevel"] = (AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.Right);
            registeredShortcuts["CollapseAll"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.Left);
            registeredShortcuts["ExpandAll"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.Right);
            registeredShortcuts["LintCode"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.L);
            registeredShortcuts["CommandPalette"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Shift, System.Windows.Forms.Keys.P);
            registeredShortcuts["ApplyTemplate"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, System.Windows.Forms.Keys.T);

            // Register keyboard shortcuts for refactors
            this.RegisterRefactorShortcuts();

            // Automatically start the scanning timer
            timerRunning = true;
            scanTimer = new System.Threading.Timer(ScanTick, null, 1000, Timeout.Infinite);
            lblStatus.Text = "Monitoring...";
        }

        private void lintCodeHandler(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            ProcessLinters();
        }

        private async void ShowCommandPalette(object? sender, KeyPressedEventArgs e)
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

        /// <summary>
        /// Generate lint reports for all files in the current project
        /// </summary>
        /// <param name="editor">The active editor, used to identify the project</param>
        private void LintProject(ScintillaEditor editor, CommandProgressDialog? progressDialog)
        {
            if (editor == null || editor.DataManager == null)
            {
                MessageBox.Show("Database connection required for project linting.",
                    "Database Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Get project name
            string projectName = ScintillaManager.GetProjectName(editor);

            // Check if project name is "Untitled", which means no project is open
            if (projectName == "Untitled")
            {
                MessageBox.Show("Please open a project before running the lint tool.",
                    "No Project Open",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Check if string.Empty is returned, which indicates a failure to get the project name
            if (string.IsNullOrEmpty(projectName))
            {
                MessageBox.Show("Unable to determine the project name. Linting cannot be completed.",
                    "Project Name Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Create a timestamp for the report filename
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportFileName = $"{projectName}_LintReport_{timestamp}.html";
            string reportPath = Path.Combine(lintReportPath ?? string.Empty, reportFileName);

            // Get metadata for all programs in the project without loading content
            var ppcProgsMeta = editor.DataManager.GetPeopleCodeItemMetadataForProject(projectName);
            var activeLinters = linterRules.Where(a => a.Active).ToList();

            // Message to show progress
            this.Invoke(() =>
            {
                lblStatus.Text = $"Linting project - found {ppcProgsMeta.Count} items...";
                progressBar1.Style = ProgressBarStyle.Marquee;
                progressBar1.MarqueeAnimationSpeed = 30;
            });

            // Master collection of all linting reports
            List<(PeopleCodeItem Program, Report LintReport)> allReports = new();
            var parseCount = 0;
            var emptyProgs = 0;
            var processedCount = 0;
            // Process each program in the project, one at a time
            foreach (var ppcProg in ppcProgsMeta)
            {
                processedCount++;

                // Update progress periodically
                if (processedCount % 10 == 0)
                {
                    this.Invoke(() =>
                    {
                        lblStatus.Text = $"Linting project - processed {processedCount} of {ppcProgsMeta.Count} items...";
                        progressDialog?.UpdateHeader($"Linting project - processed {processedCount} of {ppcProgsMeta.Count} items...");
                    });
                    GC.Collect();
                }

                // Load the content for this specific program
                if (!editor.DataManager.LoadPeopleCodeItemContent(ppcProg))
                {
                    emptyProgs++;
                    continue;
                }

                // Get the program text as string
                var programText = ppcProg.GetProgramTextAsString();
                if (string.IsNullOrEmpty(programText))
                {
                    emptyProgs++;
                    continue;
                }

                // Create lexer and parser for this program
                PeopleCodeLexer? lexer = new(new Antlr4.Runtime.AntlrInputStream(programText));
                var stream = new Antlr4.Runtime.CommonTokenStream(lexer);

                // Get all tokens including those on hidden channels
                stream.Fill();

                // Collect all comments from both comment channels
                var comments = stream.GetTokens()
                    .Where(token => token.Channel == PeopleCodeLexer.COMMENTS || token.Channel == PeopleCodeLexer.API_COMMENTS)
                    .ToList();

                PeopleCodeParser? parser = new(stream);
                var program = parser.program();
                parseCount++;
                parser.Interpreter.ClearDFA();

                // Collection for reports from this program
                List<Report> programReports = new();

                MultiParseTreeWalker walker = new();

                // Add the suppression listener first
                var suppresionListner = new LinterSuppressionListener(stream, comments);
                walker.AddListener(suppresionListner);

                // Configure and run each active linter
                foreach (var linter in activeLinters)
                {
                    // Reset linter state
                    linter.Reset();

                    // Configure linter for this program
                    linter.DataManager = editor.DataManager;
                    linter.Reports = programReports;
                    linter.Comments = comments;
                    linter.SuppressionListener = suppresionListner;
                    walker.AddListener(linter);
                }

                // Process the program with all linters at once
                walker.Walk(program);

                // Store reports with the program they came from
                foreach (var report in programReports)
                {
                    allReports.Add((ppcProg, report));
                }

                // Clean up walker listeners for next program
                foreach (var linter in activeLinters)
                {
                    linter.Reset();
                }

                programReports.Clear();
                comments.Clear();

                // Free up resources
                lexer = null;
                parser = null;
                stream = null;
                program = null;

                // Clear program text to free memory
                ppcProg.SetProgramText(Array.Empty<byte>());
                ppcProg.SetNameReferences(new List<NameReference>());

            }

            this.Invoke(() =>
            {
                lblStatus.Text = $"Finalizing Report...";
                progressDialog?.UpdateHeader($"Finalizing Report...");
            });

            // Always generate the HTML report, even if no issues found
            GenerateHtmlReport(reportPath, projectName, allReports);

            // Reset UI and show confirmation with link to report
            this.Invoke(() =>
            {
                lblStatus.Text = "Monitoring...";
                progressBar1.Style = ProgressBarStyle.Blocks;

                string message = allReports.Count > 0
                    ? $"Project linting complete. {allReports.Count} issues found.\n\nWould you like to open the report?"
                    : "Project linting complete. No issues found.\n\nWould you like to open the report?";

                var result = MessageBox.Show(
                    message,
                    "Project Linting Complete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    // Open the report in default browser
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = reportPath,
                        UseShellExecute = true
                    });
                }
            });
        }

        private void LoadSettings()
        {
            chkInitCollapsed.Checked = Properties.Settings.Default.initCollapsed;
            chkOnlyPPC.Checked = Properties.Settings.Default.onlyPPC;
            chkBetterSQL.Checked = Properties.Settings.Default.betterSQL;
            chkAutoDark.Checked = Properties.Settings.Default.autoDark;
            chkLintAnnotate.Checked = Properties.Settings.Default.lintAnnotate;

            LoadStylerStates();
            LoadLinterStates();
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            Template.GetAvailableTemplates().ForEach(t =>
            {
                cmbTemplates.Items.Add(t);
            });

            cmbTemplates.SelectedIndexChanged += CmbTemplates_SelectedIndexChanged;

            if (cmbTemplates.Items.Count > 0)
            {
                cmbTemplates.SelectedIndex = 0;
            }
        }

        private void GenerateTemplateParameterControls(Template template)
        {
            // Clear existing controls and dictionaries
            pnlTemplateParams.Controls.Clear();
            templateInputControls.Clear();
            templateInputLabels.Clear();
            templateInputsDisplayConditions.Clear();

            if (template == null || template.Inputs == null || template.Inputs.Count == 0)
            {
                return;
            }

            const int labelWidth = 150;
            const int controlWidth = 200;
            const int verticalSpacing = 30;
            const int horizontalPadding = 10;
            int currentY = 10;

            foreach (var input in template.Inputs)
            {
                // Create label for parameter
                Label label = new()
                {
                    Text = input.Label + ":",
                    Location = new Point(horizontalPadding, currentY + 3),
                    Size = new Size(labelWidth, 20),
                    AutoSize = false,
                    Tag = input.Id // Store input ID in Tag for easier reference
                };
                pnlTemplateParams.Controls.Add(label);
                templateInputLabels[input.Id] = label;

                // Create input control based on parameter type
                Control inputControl;
                switch (input.Type.ToLower())
                {
                    case "boolean":
                        inputControl = new CheckBox
                        {
                            Checked = !string.IsNullOrEmpty(input.DefaultValue) &&
                                      (input.DefaultValue.ToLower() == "true" || input.DefaultValue == "1" ||
                                       input.DefaultValue.ToLower() == "yes"),
                            Location = new Point(labelWidth + (horizontalPadding * 2), currentY),
                            Size = new Size(controlWidth, 23),
                            Text = "", // No text needed since we have the label
                            Tag = input.Id // Store input ID in Tag for easier reference
                        };
                        break;

                    // Could add more types here (dropdown, etc.) in the future

                    default: // Default to TextBox for string, number, etc.
                        inputControl = new TextBox
                        {
                            Text = input.DefaultValue ?? string.Empty,
                            Location = new Point(labelWidth + (horizontalPadding * 2), currentY),
                            Size = new Size(controlWidth, 23),
                            Tag = input.Id // Store input ID in Tag for easier reference
                        };
                        break;
                }

                // Add tooltip if description is available
                if (!string.IsNullOrEmpty(input.Description))
                {
                    ToolTip tooltip = new();
                    tooltip.SetToolTip(inputControl, input.Description);
                    tooltip.SetToolTip(label, input.Description);
                }

                // Store display condition if present
                if (input.DisplayCondition != null)
                {
                    templateInputsDisplayConditions[input.Id] = input.DisplayCondition;
                }

                pnlTemplateParams.Controls.Add(inputControl);
                templateInputControls[input.Id] = inputControl;

                currentY += verticalSpacing;
            }

            // Add event handlers for controls that affect display conditions
            foreach (var kvp in templateInputControls)
            {
                if (kvp.Value is CheckBox checkBox)
                {
                    checkBox.CheckedChanged += (s, e) => UpdateDisplayConditions();
                }
                else if (kvp.Value is TextBox textBox)
                {
                    textBox.TextChanged += (s, e) => UpdateDisplayConditions();
                }
                // Add handlers for other control types as needed
            }

            // Initial update of display conditions
            UpdateDisplayConditions();
        }

        private void UpdateDisplayConditions()
        {
            // Get current values from all controls
            var currentValues = GetTemplateParameterValues();

            // Track if we need to reflow controls
            bool visibilityChanged = false;

            // Update visibility for each control with a display condition
            foreach (var kvp in templateInputsDisplayConditions)
            {
                string inputId = kvp.Key;
                DisplayCondition condition = kvp.Value;

                if (templateInputControls.TryGetValue(inputId, out Control? control))
                {
                    bool shouldDisplay = Template.IsDisplayConditionMet(condition, currentValues);
                    visibilityChanged |= control.Visible != shouldDisplay;
                    control.Visible = shouldDisplay;

                    // Also update the label visibility
                    if (templateInputLabels.TryGetValue(inputId, out Control? label))
                    {
                        label.Visible = shouldDisplay;
                    }
                }
            }

            // Reflow visible controls to avoid gaps if needed
            if (visibilityChanged)
            {
                ReflowControls();
            }
        }

        private void ReflowControls()
        {
            // Reposition visible controls to avoid gaps
            const int verticalSpacing = 30;
            int currentY = 10;

            // Get all input IDs ordered as they were originally added
            var orderedInputs = templateInputControls.Keys.ToList();

            foreach (var inputId in orderedInputs)
            {
                if (templateInputControls.TryGetValue(inputId, out Control? control) &&
                    templateInputLabels.TryGetValue(inputId, out Control? label))
                {
                    if (control.Visible)
                    {
                        // Reposition the control and its label
                        label.Location = new Point(label.Location.X, currentY + 3);
                        control.Location = new Point(control.Location.X, currentY);

                        currentY += verticalSpacing;
                    }
                }
            }
        }

        private Dictionary<string, string> GetTemplateParameterValues()
        {
            var values = new Dictionary<string, string>();

            foreach (var kvp in templateInputControls)
            {
                string value = string.Empty;

                if (kvp.Value is TextBox textBox)
                {
                    value = textBox.Text;
                }
                else if (kvp.Value is CheckBox checkBox)
                {
                    value = checkBox.Checked ? "true" : "false";
                }
                // Add other control types as needed

                values[kvp.Key] = value;
            }

            return values;
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.initCollapsed = chkInitCollapsed.Checked;
            Properties.Settings.Default.onlyPPC = chkOnlyPPC.Checked;
            Properties.Settings.Default.betterSQL = chkBetterSQL.Checked;
            Properties.Settings.Default.autoDark = chkAutoDark.Checked;
            Properties.Settings.Default.lintAnnotate = chkLintAnnotate.Checked;
            Properties.Settings.Default.LintReportPath = lintReportPath;

            SaveStylerStates();
            SaveLinterStates();

            Properties.Settings.Default.Save();
        }

        private void LoadStylerStates()
        {
            try
            {
                var states = System.Text.Json.JsonSerializer.Deserialize<List<RuleState>>(
                    Properties.Settings.Default.StylerStates);

                if (states == null) return;

                foreach (var state in states)
                {
                    var styler = stylers.FirstOrDefault(s => s.GetType().FullName == state.TypeName);
                    if (styler != null)
                    {
                        styler.Active = state.Active;
                        // Update corresponding grid row
                        var row = dataGridView3.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Tag is BaseStyler s && s == styler);
                        if (row != null)
                        {
                            row.Cells[0].Value = state.Active;
                        }
                    }
                }
            }
            catch { /* Use defaults if settings are corrupt */
    }
}

        private void SaveStylerStates()
        {
            var states = stylers.Select(s => new RuleState
            {
                TypeName = s.GetType().FullName ?? "",
                Active = s.Active
            }).ToList();

            Properties.Settings.Default.StylerStates =
                System.Text.Json.JsonSerializer.Serialize(states);
        }

        private void LoadLinterStates()
        {
            try
            {
                var states = System.Text.Json.JsonSerializer.Deserialize<List<RuleState>>(
                    Properties.Settings.Default.LinterStates);

                if (states == null) return;

                foreach (var state in states)
                {
                    var linter = linterRules.FirstOrDefault(l => l.GetType().FullName == state.TypeName);
                    if (linter != null)
                    {
                        linter.Active = state.Active;
                        // Update corresponding grid row
                        var row = dataGridView1.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Tag is BaseLintRule l && l == linter);
                        if (row != null)
                        {
                            row.Cells[0].Value = state.Active;
                        }
                    }
                }
            }
            catch { /* Use defaults if settings are corrupt */ }
        }

        private void SaveLinterStates()
        {
            var states = linterRules.Select(l => new RuleState
            {
                TypeName = l.GetType().FullName ?? "",
                Active = l.Active
            }).ToList();

            Properties.Settings.Default.LinterStates =
                System.Text.Json.JsonSerializer.Serialize(states);
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

        private void InitLinterOptions()
        {
            /* Find all classes in this assembly that extend BaseLintRule*/
            var linters = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(BaseLintRule).IsAssignableFrom(p) && !p.IsAbstract);

            // Load plugins from the plugin directory
            string pluginDirectory = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty,
                Properties.Settings.Default.PluginDirectory);

            PluginManager.LoadPlugins(pluginDirectory);

            // Load linter configurations
            LinterConfigManager.LoadLinterConfigs();

            // Add plugin linter types
            var pluginLinters = PluginManager.DiscoverLinterTypes();
            linters = linters.Concat(pluginLinters);

            foreach (var type in linters)
            {
                /* Create instance of the linter */
                BaseLintRule? linter = (BaseLintRule?)Activator.CreateInstance(type);

                if (linter != null)
                {
                    /* Create row for datadgridview */
                    int rowIndex = dataGridView1.Rows.Add(linter.Active, linter.Description);
                    dataGridView1.Rows[rowIndex].Tag = linter;
                    
                    // Set the button text based on whether the linter has configurable properties
                    var configurableProperties = linter.GetConfigurableProperties();
                    DataGridViewButtonCell buttonCell = (DataGridViewButtonCell)dataGridView1.Rows[rowIndex].Cells[2];
                    
                    if (configurableProperties.Count > 0)
                    {
                        buttonCell.Value = "Configure...";
                        // Clear any tag that might indicate no configuration
                        dataGridView1.Rows[rowIndex].Cells[2].Tag = null;
                    }
                    else
                    {
                        // Hide the button for linters with no configurable properties
                        buttonCell.Value = " ";
                        buttonCell.FlatStyle = FlatStyle.Flat;
                        buttonCell.Style.BackColor = SystemColors.Control;
                        buttonCell.Style.SelectionBackColor = SystemColors.Control;
                        buttonCell.Style.ForeColor = SystemColors.Control;
                        buttonCell.Style.SelectionForeColor = SystemColors.Control;
                        buttonCell.Style.SelectionBackColor = SystemColors.Control;
                        // Store info about configurability in the cell's tag
                        dataGridView1.Rows[rowIndex].Cells[2].Tag = "NoConfig";
                    }
                    
                    linterRules.Add(linter);
                }
            }
            
            // Apply saved configurations to linters
            LinterConfigManager.ApplyConfigurations(linterRules);
        }
        
        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
            
            // Check if the clicked cell is in the Configure column
            if (e.ColumnIndex == 2 && e.RowIndex >= 0)
            {
                // Get the linter from the row's Tag
                if (dataGridView1.Rows[e.RowIndex].Tag is BaseLintRule linter)
                {
                    // Check if the linter has configurable properties
                    if (dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag?.ToString() != "NoConfig")
                    {
                        // Show the linter configuration dialog
                        using (var dialog = new LinterConfigDialog(linter))
                        {
                            if (dialog.ShowDialog(this) == DialogResult.OK)
                            {
                                // Configuration was updated and saved by the dialog
                                // No need to do anything else here
                            }
                        }
                    }
                }
            }
        }

        private void InitStylerOptions() // Changed from InitAnalyzerOptions
        {
            var stylerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(BaseStyler).IsAssignableFrom(p) && !p.IsAbstract);

            // Add plugin styler types
            var pluginStylers = PluginManager.DiscoverStylerTypes();
            stylerTypes = stylerTypes.Concat(pluginStylers);

            foreach (var type in stylerTypes)
            {
                BaseStyler? styler = (BaseStyler?)Activator.CreateInstance(type);
                if (styler != null)
                {
                    int rowIndex = dataGridView3.Rows.Add(styler.Active, styler.Description);
                    dataGridView3.Rows[rowIndex].Tag = styler;
                    stylers.Add(styler);
                }
            }
        }
        private void ProcessStylers(ScintillaEditor editor)
        {
            if (editor == null || editor.Type != EditorType.PeopleCode)
            {
                return;
            }

            // Get the text from the editor if needed
            if (editor.ContentString == null)
            {
                editor.ContentString = ScintillaManager.GetScintillaText(editor);
            }

            // Create parse tree
            PeopleCodeLexer? lexer = new(new Antlr4.Runtime.AntlrInputStream(editor.ContentString));
            var stream = new Antlr4.Runtime.CommonTokenStream(lexer);
            PeopleCodeParser? parser = new(stream);
            var program = parser.program();
            parser.Interpreter.ClearDFA();
            GC.Collect();
            var activeStylers = stylers.Where(a => a.Active);
            MultiParseTreeWalker walker = new();

            List<CodeAnnotation> annotations = new();
            List<CodeHighlight> highlights = new();
            List<CodeColor> colors = new();

            // Collect all comments from both comment channels
            var comments = stream.GetTokens()
                .Where(token => token.Channel == PeopleCodeLexer.COMMENTS || token.Channel == PeopleCodeLexer.API_COMMENTS)
                .ToList();

            foreach (var styler in activeStylers)
            {
                styler.Annotations = annotations;
                styler.Highlights = highlights;
                styler.Colors = colors;
                styler.Comments = comments;
                walker.AddListener(styler);
            }

            walker.Walk(program);

            foreach (var styler in activeStylers)
            {
                /* clear out any internal states for the stylers */
                styler.Reset();
            }

            ScintillaManager.ResetStyles(editor);

            // Store the annotations for later use after linter processing
            editor.StylerAnnotations = new List<CodeAnnotation>(annotations);

            foreach (var annotation in annotations)
            {
                ScintillaManager.SetAnnotation(editor, annotation.LineNumber, annotation.Message);
            }

            foreach (var highlight in highlights)
            {
                ScintillaManager.HighlightTextWithColor(editor, highlight.Color, highlight.Start, highlight.Length);
            }

            foreach (var color in colors)
            {
                ScintillaManager.ColorText(editor, color.Color, color.Start, color.Length);
            }

            // Clean up
            program = null;
            lexer = null;
            parser = null;
        }

        private void ProcessLinters()
        {
            if (activeEditor == null) return;

            ScintillaManager.ClearAnnotations(activeEditor);

            if (activeEditor == null || activeEditor.Type != EditorType.PeopleCode)
            {
                MessageBox.Show("Linting is only available for PeopleCode editors", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (activeEditor.ContentString == null)
            {
                activeEditor.ContentString = ScintillaManager.GetScintillaText(activeEditor);
            }

            PeopleCodeLexer? lexer = new(new Antlr4.Runtime.AntlrInputStream(activeEditor.ContentString));
            var stream = new Antlr4.Runtime.CommonTokenStream(lexer);

            // Get all tokens including those on hidden channels
            stream.Fill();

            // Collect all comments from both comment channels
            var comments = stream.GetTokens()
                .Where(token => token.Channel == PeopleCodeLexer.COMMENTS || token.Channel == PeopleCodeLexer.API_COMMENTS)
                .ToList();

            PeopleCodeParser? parser = new(stream);
            var program = parser.program();
            parser.Interpreter.ClearDFA();
            GC.Collect();

            var activeLinters = linterRules.Where(a => a.Active);

            /* Only run NotRequired or Optional linters if there is no database connection */
            if (activeEditor.DataManager == null)
            {
                activeLinters = activeLinters.Where(a => a.DatabaseRequirement != DataManagerRequirement.Required);
            }

            MultiParseTreeWalker walker = new();
            List<Report> reports = new();

            /* check to see if there's recently a new datamanager for this editor */
            if (processDataManagers.TryGetValue(activeEditor.ProcessId, out IDataManager? value))
            {
                activeEditor.DataManager = value;
            }

            // Add the suppression listener first
            var suppressionListener = new LinterSuppressionListener(stream, comments);
            walker.AddListener(suppressionListener);

            IDataManager? dataManger = activeEditor.DataManager;

            // Configure and run each active linter
            foreach (var linter in activeLinters)
            {
                linter.DataManager = dataManger;
                linter.Reports = reports;
                linter.Comments = comments;  // Make comments available to each linter
                linter.SuppressionListener = suppressionListener;
                walker.AddListener(linter);
            }

            walker.Walk(program);

            foreach (var linter in activeLinters)
            {
                linter.Reset();
            }

            /* Free up ANTLR resources */
            program = null;
            lexer = null;
            parser = null;
            stream = null;

            activeEditor.SetLinterReports(reports);
            // Process and display reports
            foreach (var g in reports.GroupBy(r => r.Line).OrderBy(b => b.First().Line))
            {
                List<string> messages = new();
                List<AnnotationStyle> styles = new();

                foreach (var report in g)
                {
                    this.Invoke(() =>
                    {
                        int rowIndex = dataGridView2.Rows.Add(report.Type, report.Message, report.Line);
                        dataGridView2.Rows[rowIndex].Tag = report;
                    });

                    if (chkLintAnnotate.Checked)
                    {
                        messages.Add($"{report.Message} ({report.GetFullId()})");
                        styles.Add(report.Type switch
                        {
                            ReportType.Error => AnnotationStyle.Red,
                            ReportType.Warning => AnnotationStyle.Yellow,
                            ReportType.Info => AnnotationStyle.Gray,
                            _ => AnnotationStyle.Gray
                        });
                    }
                }

                if (chkLintAnnotate.Checked)
                {
                    ScintillaManager.SetAnnotations(activeEditor, messages, g.First().Line, styles);
                }
            }
            
            // Re-apply styler annotations after linter processing
            if (activeEditor.StylerAnnotations != null && activeEditor.StylerAnnotations.Count > 0)
            {
                foreach (var annotation in activeEditor.StylerAnnotations)
                {
                    ScintillaManager.SetAnnotation(activeEditor, annotation.LineNumber, annotation.Message);
                }
            }
        }

        private void dataGridView3_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dataGridView3.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridView3_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            if (e.ColumnIndex != 0)
            {
                return;
            }
            if (dataGridView3.Rows[e.RowIndex].Tag == null)
            {
                return;
            }
            if (dataGridView3.Rows[e.RowIndex].Tag is BaseStyler styler)
            {
                styler.Active = (bool)dataGridView3.Rows[e.RowIndex].Cells[0].Value;
            }
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            if (e.ColumnIndex != 0)
            {
                return;
            }
            if (dataGridView1.Rows[e.RowIndex].Tag == null)
            {
                return;
            }
            if (dataGridView1.Rows[e.RowIndex].Tag is BaseLintRule linter)
            {
                linter.Active = (bool)dataGridView1.Rows[e.RowIndex].Cells[0].Value;
            }
        }


        private void dataGridView2_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (activeEditor == null) return;
            /* Get the tag (Report ) from the selected row */
            /* instruct Scintilla to select the line and send focus to main window */
            if (dataGridView2.SelectedRows.Count == 0)
            {
                return;
            }
            if (dataGridView2.SelectedRows[0].Tag is Report report)
            {
                ScintillaManager.SetSelection(activeEditor, report.Span.Start, report.Span.Stop);
                WindowHelper.FocusWindow(activeEditor.hWnd);
            }
        }

        private void btnClearLint_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.ClearAnnotations(activeEditor);
            dataGridView2.Rows.Clear();
        }

        private void btnDarkMode_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.SetDarkMode(activeEditor);
        }

        private void btnCollapseAll_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.CollapseTopLevel(activeEditor);
        }

        private void btnExpand_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.ExpandTopLevel(activeEditor);
        }

        private void btnTakeSnapshot_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            activeEditor.SnapshotText = ScintillaManager.GetScintillaText(activeEditor);
            activeEditor.SnapshotCursorPosition = ScintillaManager.GetCursorPosition(activeEditor);
        }

        private void btnRestoreSnapshot_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            if (activeEditor.SnapshotText == null) return;

            ScintillaManager.ClearAnnotations(activeEditor);
            ScintillaManager.SetScintillaText(activeEditor, activeEditor.SnapshotText);

            // Restore cursor position if it was saved
            if (activeEditor.SnapshotCursorPosition.HasValue)
            {
                ScintillaManager.SetCursorPosition(activeEditor, activeEditor.SnapshotCursorPosition.Value);
            }

            activeEditor.SnapshotText = null;
            activeEditor.SnapshotCursorPosition = null;
        }

        private void btnAddFlowerBox_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            ProcessRefactor(new AddFlowerBox(activeEditor));
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

            // Take a snapshot before refactoring
            activeEditor.SnapshotText = freshText;
            activeEditor.SnapshotCursorPosition = currentCursorPosition;
            activeEditor.SnapshotFirstVisibleLine = currentFirstVisibleLine;

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

            // Parse the code
            PeopleCodeLexer lexer = new(new Antlr4.Runtime.AntlrInputStream(freshText));
            var stream = new Antlr4.Runtime.CommonTokenStream(lexer);
            PeopleCodeParser parser = new(stream);
            var program = parser.program();
            parser.Interpreter.ClearDFA();
            GC.Collect();
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
                if (activeEditor.SnapshotFirstVisibleLine.HasValue)
                {
                    ScintillaManager.SetFirstVisibleLine(activeEditor, activeEditor.SnapshotFirstVisibleLine.Value);
                }
            }
        }

        private void btnOptimizeImports_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            ProcessRefactor(new OptimizeImports(activeEditor));
        }

        private void btnResolveImports_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            ProcessRefactor(new ResolveImports(activeEditor));
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

            DBConnectDialog dialog = new();
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
                dataGridView2.Rows.Clear();
            });
            Application.DoEvents();
            // Run the folding operation in a background thread
            await Task.Run(() =>
            {
                // Ensure the activeEditor is not null before proceeding
                ProcessLinters();
            });

            // Update the UI after the background task completes
            this.Invoke(() =>
            {
                lblStatus.Text = "Monitoring...";
                progressBar1.Style = ProgressBarStyle.Blocks;
            });
            Application.DoEvents();
        }

        private void btnRenameLocalVar_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;

            RenameLocalVariable refactor = new(activeEditor);
            ProcessRefactor(refactor);
        }

        private void ProcessSingleLinter(BaseLintRule linter)
        {
            if (activeEditor == null) return;

            ScintillaManager.ClearAnnotations(activeEditor);

            // Clear previous lint results
            this.Invoke(() =>
            {
                dataGridView2.Rows.Clear();
            });

            if (activeEditor.Type != EditorType.PeopleCode)
            {
                MessageBox.Show("Linting is only available for PeopleCode editors", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (activeEditor.ContentString == null)
            {
                activeEditor.ContentString = ScintillaManager.GetScintillaText(activeEditor);
            }

            PeopleCodeLexer? lexer = new(new Antlr4.Runtime.AntlrInputStream(activeEditor.ContentString));
            var stream = new Antlr4.Runtime.CommonTokenStream(lexer);

            // Get all tokens including those on hidden channels
            stream.Fill();

            // Collect all comments from both comment channels
            var comments = stream.GetTokens()
                .Where(token => token.Channel == PeopleCodeLexer.COMMENTS || token.Channel == PeopleCodeLexer.API_COMMENTS)
                .ToList();

            PeopleCodeParser? parser = new(stream);
            var program = parser.program();
            parser.Interpreter.ClearDFA();
            GC.Collect();

            // Check if the linter requires database connection
            if (linter.DatabaseRequirement == DataManagerRequirement.Required && activeEditor.DataManager == null)
            {
                MessageBox.Show("This linting rule requires a database connection", "Database Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MultiParseTreeWalker walker = new();
            List<Report> reports = new();

            /* check to see if there's recently a new datamanager for this editor */
            if (processDataManagers.TryGetValue(activeEditor.ProcessId, out IDataManager? value))
            {
                activeEditor.DataManager = value;
            }

            // Add the suppression listener first
            var suppressionListener = new LinterSuppressionListener(stream, comments);
            walker.AddListener(suppressionListener);

            IDataManager? dataManger = activeEditor.DataManager;

            // Configure and run the specific linter
            linter.DataManager = dataManger;
            linter.Reports = reports;
            linter.Comments = comments;
            linter.SuppressionListener = suppressionListener;

            walker.AddListener(linter);
            walker.Walk(program);

            // Reset the linter state
            linter.Reset();

            /* Free up ANTLR resources */
            program = null;
            lexer = null;
            parser = null;
            stream = null;

            activeEditor.SetLinterReports(reports);
            // Process and display reports
            foreach (var g in reports.GroupBy(r => r.Line).OrderBy(b => b.First().Line))
            {
                List<string> messages = new();
                List<AnnotationStyle> styles = new();

                foreach (var report in g)
                {
                    this.Invoke(() =>
                    {
                        int rowIndex = dataGridView2.Rows.Add(report.Type, report.Message, report.Line);
                        dataGridView2.Rows[rowIndex].Tag = report;
                    });

                    if (chkLintAnnotate.Checked)
                    {
                        messages.Add(report.Message);
                        styles.Add(report.Type switch
                        {
                            ReportType.Error => AnnotationStyle.Red,
                            ReportType.Warning => AnnotationStyle.Yellow,
                            ReportType.Info => AnnotationStyle.Gray,
                            _ => AnnotationStyle.Gray
                        });
                    }
                }

                if (chkLintAnnotate.Checked)
                {
                    ScintillaManager.SetAnnotations(activeEditor, messages, g.First().Line, styles);
                }
            }
        }

        private void CmbTemplates_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbTemplates.SelectedItem is Template selectedTemplate)
            {
                GenerateTemplateParameterControls(selectedTemplate);
            }
        }

        private void btnApplyTemplate_Click(object? sender, EventArgs e)
        {
            if (cmbTemplates.SelectedItem is Template selectedTemplate)
            {
                var parameterValues = GetTemplateParameterValues();

                if (!selectedTemplate.ValidateInputs(parameterValues))
                {
                    MessageBox.Show("Please fill in all required fields.", "Required Fields Missing",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string generatedContent = selectedTemplate.Apply(parameterValues);
                ApplyGeneratedTemplate(selectedTemplate, generatedContent);
            }
        }

        /// <summary>
        /// Generates an HTML report of linting results for a project
        /// </summary>
        /// <param name="reportPath">The path where the report should be saved</param>
        /// <param name="projectName">The name of the project</param>
        /// <param name="reportData">The collection of programs and their lint reports</param>
        private void GenerateHtmlReport(string reportPath, string projectName,
            List<(PeopleCodeItem Program, Report LintReport)> reportData)
        {
            try
            {
                // Group reports by program
                var groupedReports = reportData
                    .GroupBy(r => r.Program.BuildPath())
                    .OrderBy(g => g.Key)
                    .ToList();

                // Calculate statistics
                int totalErrors = reportData.Count(r => r.LintReport.Type == ReportType.Error);
                int totalWarnings = reportData.Count(r => r.LintReport.Type == ReportType.Warning);
                int totalInfo = reportData.Count(r => r.LintReport.Type == ReportType.Info);

                // Get active linters
                var activeLinterInfo = linterRules
                    .Where(l => l.Active)
                    .Select(l => new { name = l.GetType().Name, description = l.Description })
                    .ToList();

                // Create a structured report object for JSON serialization
                var report = new
                {
                    projectName,
                    timestamp = DateTime.Now.ToString(),
                    totalErrors,
                    totalWarnings,
                    totalInfo,
                    totalIssues = reportData.Count,
                    activeLinters = activeLinterInfo,
                    programReports = groupedReports.Select(pg => new
                    {
                        programPath = pg.Key,
                        // Include the PeopleCodeType information
                        peopleCodeType = pg.First().Program.Type.ToString(),
                        reports = pg.Select(item => new
                        {
                            type = item.LintReport.Type.ToString(),
                            line = item.LintReport.Line + 1,
                            message = item.LintReport.Message
                        }).OrderBy(r => r.line).ToList()
                    }).ToList()
                };

                // Convert the report object to JSON
                string reportJson = System.Text.Json.JsonSerializer.Serialize(report);

                // Get the template file path
                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "LintReportTemplate.html");
                string templateHtml;

                // Check if template file exists
                if (System.IO.File.Exists(templatePath))
                {
                    // Read the template file
                    templateHtml = System.IO.File.ReadAllText(templatePath);
                }
                else
                {
                    // If file doesn't exist, try to read from embedded resource
                    using (Stream? stream = GetType().Assembly.GetManifestResourceStream("AppRefiner.Templates.LintReportTemplate.html"))
                    {
                        if (stream != null)
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                templateHtml = reader.ReadToEnd();
                            }
                        }
                        else
                        {
                            // Fallback message if template isn't found
                            MessageBox.Show("Lint report template not found. Please check your installation.",
                                "Template Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                }

                // Inject the JSON data into the HTML
                string finalHtml = templateHtml.Replace("{{projectName}}", projectName)
                                             .Replace("{{timestamp}}", DateTime.Now.ToString());

                // Add the report data as a JavaScript variable
                finalHtml = finalHtml.Replace("</head>",
                    $"<script>const reportJSON = {reportJson};</script>\n</head>");

                // Write the final HTML to the report file
                System.IO.File.WriteAllText(reportPath, finalHtml);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating report: {ex.Message}", "Report Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

        private static DialogResult ShowInputDialog(string title, string text, ref string result, IntPtr owner = 0)
        {
            Size size = new(300, 70);
            Form inputBox = new();

            inputBox.FormBorderStyle = FormBorderStyle.FixedDialog;
            inputBox.ClientSize = size;
            inputBox.Text = title;
            inputBox.StartPosition = FormStartPosition.CenterParent;

            TextBox textBox = new();
            textBox.Size = new Size(size.Width - 10, 23);
            textBox.Location = new Point(5, 5);
            textBox.Text = text;
            inputBox.Controls.Add(textBox);

            Button okButton = new();
            okButton.DialogResult = DialogResult.OK;
            okButton.Name = "okButton";
            okButton.Size = new Size(75, 23);
            okButton.Text = "&OK";
            okButton.Location = new Point(size.Width - 80 - 80, 39);
            inputBox.Controls.Add(okButton);

            Button cancelButton = new();
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new Size(75, 23);
            cancelButton.Text = "&Cancel";
            cancelButton.Location = new Point(size.Width - 80, 39);
            inputBox.Controls.Add(cancelButton);

            inputBox.AcceptButton = okButton;
            inputBox.CancelButton = cancelButton;

            DialogResult dlgResult = inputBox.ShowDialog(owner != IntPtr.Zero ? new WindowWrapper(owner) : null);
            result = textBox.Text;
            return dlgResult;
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
                    if (activeEditor != null)
                    {
                        progressDialog?.UpdateHeader("Running linters...");
                        ProcessLinters();
                    }
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

            AvailableCommands.Add(new Command(
                "Editor: Take Snapshot",
                "Take a snapshot of the current editor content",
                () =>
                {
                    if (activeEditor != null)
                    {
                        activeEditor.SnapshotText = ScintillaManager.GetScintillaText(activeEditor);
                        activeEditor.SnapshotCursorPosition = ScintillaManager.GetCursorPosition(activeEditor);
                    }
                }
            ));

            AvailableCommands.Add(new Command(
                "Editor: Restore Snapshot",
                "Restore editor content from the last snapshot",
                () =>
                {
                    if (activeEditor != null && activeEditor.SnapshotText != null)
                    {
                        ScintillaManager.ClearAnnotations(activeEditor);
                        ScintillaManager.SetScintillaText(activeEditor, activeEditor.SnapshotText);

                        // Restore cursor position if it was saved
                        if (activeEditor.SnapshotCursorPosition.HasValue)
                        {
                            ScintillaManager.SetCursorPosition(activeEditor, activeEditor.SnapshotCursorPosition.Value);
                        }

                        activeEditor.SnapshotText = null;
                        activeEditor.SnapshotCursorPosition = null;
                    }
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
            foreach (var styler in stylers)
            {
                AvailableCommands.Add(new Command(
                    $"Styler: Toggle {styler.Description}",
                    () => styler.Active ? $"Currently enabled - Click to disable" : $"Currently disabled - Click to enable",
                    () =>
                    {
                        styler.Active = !styler.Active;
                        if (activeEditor != null)
                        {
                            ProcessStylers(activeEditor);
                        }

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

            // Add refactoring commands
            var refactorTypes = DiscoverRefactorTypes();
            foreach (var type in refactorTypes)
            {
                // Get the static properties for display name and description
                string refactorName = "Refactor";
                string refactorDescription = "Perform refactoring operation";

                try
                {
                    var nameProperty = type.GetProperty("RefactorName", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    if (nameProperty != null)
                    {
                        refactorName = nameProperty.GetValue(null) as string ?? "Refactor";
                    }

                    var descProperty = type.GetProperty("RefactorDescription", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    if (descProperty != null)
                    {
                        refactorDescription = descProperty.GetValue(null) as string ?? "Perform refactoring operation";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting refactor properties for {type.Name}: {ex.Message}");
                }

                // Create a command for this refactor
                AvailableCommands.Add(new Command(
                    $"Refactor: {refactorName}{GetShortcutText(type)}",
                    refactorDescription,
                    () =>
                    {
                        if (activeEditor != null)
                        {
                            // Create an instance of the refactor with the standard parameters
                            var refactor = (BaseRefactor?)Activator.CreateInstance(
                                type, 
                                [activeEditor]
                            );
                            
                            if (refactor != null)
                            {
                                ProcessRefactor(refactor);
                            }
                        }
                    }
                ));
            }

            // Add the suppress lint errors command (special case that needs the current line)
            AvailableCommands.Add(new Command(
                "Linter: Suppress lint errors",
                "Suppress lint errors with configurable scope",
                () =>
                {
                    if (activeEditor != null)
                    {
                        var line = ScintillaManager.GetCurrentLine(activeEditor);
                        int currentPosition = ScintillaManager.GetCursorPosition(activeEditor);
                        if (line != -1)
                        {
                            ProcessRefactor(new SuppressReportRefactor(activeEditor));
                        }
                    }
                }
            ));

            // Add individual linter commands with "Lint: " prefix
            foreach (var linter in linterRules)
            {
                AvailableCommands.Add(new Command(
                    $"Lint: {linter.Description}",
                    $"Run {linter.Description} linting rule",
                    () =>
                    {
                        if (activeEditor != null)
                            ProcessSingleLinter(linter);
                    }
                ));
            }

            // Add linter toggle commands
            foreach (var linter in linterRules)
            {
                AvailableCommands.Add(new Command(
                    $"Lint: Toggle {linter.Description}",
                    () => linter.Active ? $"Currently enabled - Click to disable" : $"Currently disabled - Click to enable",
                    () =>
                    {
                        linter.Active = !linter.Active;

                        // Update corresponding grid row if exists
                        var row = dataGridView1.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Tag is BaseLintRule l && l == linter);
                        if (row != null)
                        {
                            row.Cells[0].Value = linter.Active;
                        }
                    }
                ));
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
                        DBConnectDialog dialog = new(mainHandle);
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
                        dataGridView2.Rows.Clear();
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
                        // Reset the content hash to match so we don't process it next tick.
                        activeEditor.LastContentHash = 0;
                        // Clear content string to force re-reading
                        activeEditor.ContentString = null;
                        // Clear annotations
                        ScintillaManager.ClearAnnotations(activeEditor);
                        // Reset styles
                        ScintillaManager.ResetStyles(activeEditor);
                        // Update status
                        lblStatus.Text = "Force refreshing editor...";
                    }
                }
            ));

            // Add project linting commands
            AvailableCommands.Add(new Command(
                "Project: Set Lint Report Directory",
                $"Current directory: {lintReportPath}",
                (progressDialog) =>
                {
                    SetLintReportDirectory();
                }
            ));

            AvailableCommands.Add(new Command(
                "Project: Lint Project",
                "Run all linters on the entire project and generate a report",
                (progressDialog) =>
                {
                    if (activeEditor != null)
                    {
                        progressDialog?.UpdateHeader("Initializing project linting...");
                        LintProject(activeEditor, progressDialog);
                    }
                },
                () => activeEditor != null && activeEditor.DataManager != null
            ));

            AvailableCommands.Add(new Command(
                "Template: Apply Template (Ctrl+Alt+T)",
                "Apply a template to the current editor",
                () =>
                {
                    ApplyTemplateCommand();
                },
                () => activeEditor != null
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

        /// <summary>
        /// Discovers all available refactor types in the application and plugins
        /// </summary>
        /// <returns>A collection of refactor types</returns>
        private IEnumerable<Type> DiscoverRefactorTypes()
        {
            // Get refactor types from the main assembly
            var refactorTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(BaseRefactor).IsAssignableFrom(p) && !p.IsAbstract && p != typeof(ScopedRefactor<>) && p != typeof(BaseRefactor));

            // Get refactor types from plugins
            var pluginRefactors = PluginManager.DiscoverRefactorTypes();
            if (pluginRefactors != null)
            {
                refactorTypes = refactorTypes.Concat(pluginRefactors);
            }

            return refactorTypes;
        }

        private void RegisterRefactorShortcuts()
        {
            // Clean up any existing refactor shortcuts
            foreach (var hook in refactorShortcuts.Values)
            {
                hook.Dispose();
            }
            refactorShortcuts.Clear();

            // Get all refactor types
            // Add refactoring commands
            var refactorTypes = DiscoverRefactorTypes();
            foreach (var type in refactorTypes)
            {
                // Get the static properties for display name and description
                bool registerShortcut = false;
                ModifierKeys modifiers = (ModifierKeys)Keys.None;
                Keys key = Keys.None;
                string refactorName = string.Empty;
                
                try
                {
                    var registerShortcutProperty = type.GetProperty("RegisterKeyboardShortcut", 
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (registerShortcutProperty != null)
                    {
                        registerShortcut = (bool)registerShortcutProperty.GetValue(null)!;
                    }

                    var modifiersProperty = type.GetProperty("ShortcutModifiers", 
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    if (modifiersProperty != null)
                    {
                        modifiers = (ModifierKeys)modifiersProperty.GetValue(null)!;
                    }

                    var keyProperty = type.GetProperty("ShortcutKey", 
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    if (keyProperty != null)
                    {
                        key = (Keys)keyProperty.GetValue(null)!;
                    }

                    var nameProperty = type.GetProperty("RefactorName", 
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    if (nameProperty != null)
                    {
                        refactorName = (string)nameProperty.GetValue(null)!;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting refactor properties for {type.Name}: {ex.Message}");
                }
                    // Check if this refactor wants a keyboard shortcut
                if (registerShortcut && key != Keys.None)
                {
                    // Check for collision with existing shortcuts
                    bool hasCollision = false;
                    string? collisionName = null;
                    
                    foreach (var entry in registeredShortcuts)
                    {
                        if (entry.Value.Item1 == modifiers && entry.Value.Item2 == key)
                        {
                            hasCollision = true;
                            collisionName = entry.Key;
                            break;
                        }
                    }
                    
                    if (hasCollision)
                    {
                        // Show warning about collision
                        MessageBox.Show(
                            $"Failed to register keyboard shortcut for refactor '{refactorName}' due to a collision with '{collisionName}'. The refactor is still available in the command palette.",
                            "Shortcut Collision",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                    }
                    else
                    {
                        // Register the shortcut
                        var hook = new KeyboardHook();
                        
                        // Create a handler that creates and processes the refactor
                        hook.KeyPressed += (sender, e) =>
                        {
                            if (activeEditor == null) return;
                            
                            var newRefactor = (BaseRefactor?)Activator.CreateInstance(type, [activeEditor]);
                            if (newRefactor != null)
                            {
                                ProcessRefactor(newRefactor);
                            }
                        };
                        
                        // Register the hotkey
                        hook.RegisterHotKey(modifiers, key);
                        
                        // Store in our dictionaries
                        refactorShortcuts[type.FullName ?? type.Name] = hook;
                        registeredShortcuts[refactorName] = (modifiers, key);
                    }
                }

            }
        }

        private string GetShortcutText(Type refactorType)
        {
            try
            {
                // Check if this refactor has a keyboard shortcut
                var registerShortcutProperty = refactorType.GetProperty("RegisterKeyboardShortcut", 
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                
                if (registerShortcutProperty != null && (bool)registerShortcutProperty.GetValue(null)!)
                {
                    // Get the modifier keys and key
                    var modifiersProperty = refactorType.GetProperty("ShortcutModifiers", 
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    var keyProperty = refactorType.GetProperty("ShortcutKey", 
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (modifiersProperty != null && keyProperty != null)
                    {
                        ModifierKeys modifiers = (ModifierKeys)modifiersProperty.GetValue(null)!;
                        Keys key = (Keys)keyProperty.GetValue(null)!;
                        
                        if (key != Keys.None)
                        {
                            // Format the shortcut text
                            StringBuilder shortcutText = new StringBuilder(" (");
                            
                            if ((modifiers & AppRefiner.ModifierKeys.Control) == AppRefiner.ModifierKeys.Control)
                                shortcutText.Append("Ctrl+");
                            
                            if ((modifiers & AppRefiner.ModifierKeys.Shift) == AppRefiner.ModifierKeys.Shift)
                                shortcutText.Append("Shift+");
                            
                            if ((modifiers & AppRefiner.ModifierKeys.Alt) == AppRefiner.ModifierKeys.Alt)
                                shortcutText.Append("Alt+");
                            
                            shortcutText.Append(key.ToString());
                            shortcutText.Append(")");
                            
                            return shortcutText.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting shortcut text for {refactorType.Name}: {ex.Message}");
            }
            
            return string.Empty;
        }

        private ScintillaEditor? GetActiveEditor()
        {
            var activeWindow = ActiveWindowChecker.GetActiveScintillaWindow();

            /* If currently focused window is *not* owned by PSIDE, return the last active editor */
            if (activeWindow == new IntPtr(-1)) return activeEditor;

            /* If the active window is not a Scintilla editor, return null */
            return activeWindow == IntPtr.Zero ? null : ScintillaManager.GetEditor(activeWindow);
        }

        /* TODO: override WndProc */
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (activeEditor == null) return;

            /* if message is a WM_SCN_EVENT (check the mask) */
            if ((m.Msg & WM_SCN_EVENT_MASK) == WM_SCN_EVENT_MASK)
            {
                /* remove mask */
                var eventCode = m.Msg & ~WM_SCN_EVENT_MASK;

                switch(eventCode)
                {
                    case SCN_DWELLSTART:
                        ScintillaManager.ShowCallTip(activeEditor, m.WParam.ToInt32(), "This is a dwell message.");
                        break;
                    case SCN_DWELLEND:
                        ScintillaManager.HideCallTip(activeEditor);
                        break;
                }
            }

        }


        private void PerformScan()
        {
            var currentEditor = GetActiveEditor();
            if (currentEditor == null)
            {
                return;
            }

            /* Make sure the hwnd is still valid */
            if (!currentEditor.IsValid())
            {
                /* Releases any annotation buffers */
                ScintillaManager.CleanupEditor(currentEditor);

                currentEditor = null;
                activeEditor = null;
                DisableUIActions();
                return;
            }

            EnableUIActions();

            activeEditor = currentEditor;


            /* If "only PPC" is checked and the editor is not PPC, skip */
            if (chkOnlyPPC.Checked && activeEditor.Type != EditorType.PeopleCode)
            {
                return;
            }


            if (!activeEditor.FoldEnabled)
            {
                if (chkAutoDark.Checked)
                {
                    ScintillaManager.SetDarkMode(activeEditor);
                }

                if(btnAutoIndentation.Checked && File.Exists("AppRefinerHook.dll"))
                {
                    if (!ThreadsWithEventHook.Contains(activeEditor.ThreadID))
                    {
                        EventHookInstaller.SetHook(activeEditor.ThreadID);
                        this.Invoke(() =>
                        {
                            EventHookInstaller.SendWindowHandleToHookedThread(activeEditor.ThreadID, this.Handle);
                        });
                        ThreadsWithEventHook.Add(activeEditor.ThreadID);
                    }
                    ScintillaManager.SetMouseDwellTime(activeEditor, 1000);
                }
                
                ScintillaManager.EnableFolding(activeEditor);
                ScintillaManager.FixEditorTabs(activeEditor, !chkBetterSQL.Checked);
                activeEditor.FoldEnabled = true;

                if (chkBetterSQL.Checked && activeEditor.Type == EditorType.SQL)
                {
                    ScintillaManager.ApplyBetterSQL(activeEditor);
                }

                if (chkInitCollapsed.Checked)
                {
                    ScintillaManager.CollapseTopLevel(activeEditor);
                }

                return;
            }

            /* This will trigger for editors that have already had fold enabled */
            /* We only want to operate on "clean" editor states */
            if (ScintillaManager.IsEditorClean(activeEditor))
            {
                /* If there is a text selection, maybe the save failed and the error was highlighted... */
                if (activeEditor.Type == EditorType.PeopleCode && ScintillaManager.GetSelectionLength(activeEditor) > 0)
                {
                    /* We want to update our content hash to match so we don't process it next tick. */
                    activeEditor.LastContentHash = 0;
                    return;
                }


                /* compare content hash to see if things have changed */
                var contentHash = ScintillaManager.GetContentHash(activeEditor);
                if (contentHash == activeEditor.LastContentHash)
                {
                    return;
                }

                if (chkBetterSQL.Checked && activeEditor.Type == EditorType.SQL)
                {
                    ScintillaManager.ApplyBetterSQL(activeEditor);
                }

                // Apply dark mode whenever content changes if auto dark mode is enabled
                if (chkAutoDark.Checked)
                {
                    ScintillaManager.SetDarkMode(activeEditor);
                }

                /* Process stylers for PeopleCode */
                if (activeEditor.Type == EditorType.PeopleCode)
                {
                    ProcessStylers(activeEditor);
                }

                if (!activeEditor.HasLexilla || activeEditor.Type == EditorType.SQL || activeEditor.Type == EditorType.Other)
                {
                    /* Perform folding ourselves 
                        1. if they are missing Lexilla
                        2. if it is a SQL object 
                        3. if its an editor type we don't know
                    */
                    DoExplicitFolding();
                }

                activeEditor.LastContentHash = contentHash;
            }
        }
        private void ScanTick(object? state)
        {
            PerformScan();
            if (timerRunning)
            {
                scanTimer?.Change(1000, Timeout.Infinite);
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

        private void ApplyTemplateCommand()
        {
            /* only work if there's an active editor */
            if (activeEditor == null) return;
            
            // Get all available templates
            var templates = Template.GetAvailableTemplates();
            
            if (templates.Count == 0)
            {
                MessageBox.Show("No templates found.", "No Templates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;
            var handleWrapper = new WindowWrapper(mainHandle);
            // Check if editor is not empty and warn user
            if (!string.IsNullOrWhiteSpace(ScintillaManager.GetScintillaText(activeEditor)))
            {
                using var confirmDialog = new TemplateConfirmationDialog(
                    "Applying a template will replace all content in the current editor. Do you want to continue?",
                    mainHandle);
                
                if (confirmDialog.ShowDialog(handleWrapper) != DialogResult.Yes)
                {
                    return;
                }
            }

            // Show template selection dialog
            using var templateDialog = new TemplateSelectionDialog(templates, mainHandle);
            if (templateDialog.ShowDialog(handleWrapper) != DialogResult.OK || templateDialog.SelectedTemplate == null)
            {
                return;
            }

            var selectedTemplate = templateDialog.SelectedTemplate;

            // If the template has inputs, show parameter dialog
            if (selectedTemplate.Inputs != null && selectedTemplate.Inputs.Count > 0)
            {
                using var parameterDialog = new TemplateParameterDialog(selectedTemplate, mainHandle);
                if (parameterDialog.ShowDialog(handleWrapper) != DialogResult.OK)
                {
                    return;
                }

                var parameterValues = parameterDialog.ParameterValues;
                string generatedContent = selectedTemplate.Apply(parameterValues);
                ApplyGeneratedTemplate(selectedTemplate, generatedContent);
            }
            else
            {
                // Apply template without parameters
                string generatedContent = selectedTemplate.Apply(new Dictionary<string, string>());
                ApplyGeneratedTemplate(selectedTemplate, generatedContent);
            }
        }

        private void ApplyGeneratedTemplate(Template template, string generatedContent)
        {
            if (activeEditor != null)
            {
                // Take a snapshot of the current content and cursor position
                activeEditor.SnapshotText = ScintillaManager.GetScintillaText(activeEditor);
                activeEditor.SnapshotCursorPosition = ScintillaManager.GetCursorPosition(activeEditor);

                // Set the generated content in the editor
                ScintillaManager.SetScintillaText(activeEditor, generatedContent);

                // Handle cursor position or selection range if specified in the template
                if (template.SelectionStart >= 0 && template.SelectionEnd >= 0)
                {
                    // Set the selection range
                    ScintillaManager.SetSelection(activeEditor, template.SelectionStart, template.SelectionEnd);
                    WindowHelper.FocusWindow(activeEditor.hWnd);
                }
                else if (template.CursorPosition >= 0)
                {
                    ScintillaManager.SetCursorPosition(activeEditor, template.CursorPosition);
                    WindowHelper.FocusWindow(activeEditor.hWnd);
                }
            }
            else
            {
                // If no editor is active, show the content in a dialog
                ShowGeneratedTemplateDialog(generatedContent, template.TemplateName);
            }
        }
    }
}
