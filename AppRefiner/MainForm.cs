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
using AppRefiner.TooltipProviders;
using AppRefiner.Snapshots;

namespace AppRefiner
{
    public partial class MainForm : Form
    {
        // Delegate used for both EnumWindows and EnumChildWindows.
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // WinEvent constants
        private const uint EVENT_OBJECT_FOCUS = 0x8005;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        // WinEvent function delegate
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // WinEvent hook handle
        private IntPtr winEventHook = IntPtr.Zero;
        
        // Keep reference to delegate to prevent garbage collection
        private WinEventDelegate winEventDelegate;

        private ScintillaEditor? activeEditor = null;

        /// <summary>
        /// Gets the currently active editor
        /// </summary>
        public ScintillaEditor? ActiveEditor => activeEditor;

        private List<BaseLintRule> linterRules = new();
        private List<BaseStyler> stylers = new(); // Changed from List<BaseStyler> analyzers
        private List<ITooltipProvider> tooltipProviders = new();

        // Map of process IDs to their corresponding data managers
        private Dictionary<uint, IDataManager> processDataManagers = new();

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
        KeyboardHook superGoTo = new();
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
            InitLinterOptions();
            InitStylerOptions();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Set the application icon explicitly
            LoadSettings();
            LoadLinterStates();
            LoadStylerStates();
            LoadTooltipStates();
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

            // We no longer need to start the scanning timer since we now process editors:
            // 1. When they receive focus via WinEvent 
            // 2. When typing pauses are detected via AR_TYPING_PAUSE messages
            RegisterCommands();
            // Initialize the tooltip providers
            TooltipManager.Initialize();
            InitTooltipOptions();

            // Register keyboard hooks for code folding
            collapseLevel.KeyPressed += collapseLevelHandler;
            collapseLevel.RegisterHotKey(AppRefiner.ModifierKeys.Alt, Keys.Left);

            expandLevel.KeyPressed += expandLevelHandler;
            expandLevel.RegisterHotKey(AppRefiner.ModifierKeys.Alt, Keys.Right);

            collapseAll.KeyPressed += collapseAllHandler;
            collapseAll.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.Left);

            expandAll.KeyPressed += expandAllHandler;
            expandAll.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.Right);

            lintCodeHook.KeyPressed += lintCodeHandler;
            lintCodeHook.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.L);

            commandPaletteHook.KeyPressed += ShowCommandPalette;
            commandPaletteHook.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Shift, Keys.P);

            applyTemplateHook.KeyPressed += (s, e) => ApplyTemplateCommand();
            applyTemplateHook.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.T);

            superGoTo.KeyPressed += (s, e) => SuperGoToCommand();
            superGoTo.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.G);


            // Register standard shortcuts in the tracking dictionary
            registeredShortcuts["CollapseLevel"] = (AppRefiner.ModifierKeys.Alt, Keys.Left);
            registeredShortcuts["ExpandLevel"] = (AppRefiner.ModifierKeys.Alt, Keys.Right);
            registeredShortcuts["CollapseAll"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.Left);
            registeredShortcuts["ExpandAll"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.Right);
            registeredShortcuts["LintCode"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.L);
            registeredShortcuts["CommandPalette"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Shift, Keys.P);
            registeredShortcuts["ApplyTemplate"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.T);
            registeredShortcuts["SuperGoTo"] = (AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Alt, Keys.G);

            RegisterRefactorShortcuts();
            
            // Setup the WinEvent hook to monitor focus changes
            SetupWinEventHook();

            // Initialize snapshot manager
            snapshotManager = SnapshotManager.CreateFromSettings();
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
            // Clean up all hooks to ensure they're properly removed
            AppRefiner.Events.EventHookInstaller.CleanupAllHooks();
            
            // Clean up the WinEvent hook if active
            if (winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(winEventHook);
                winEventHook = IntPtr.Zero;
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
            isLoadingSettings = true;
            try
            {
                chkInitCollapsed.Checked = Properties.Settings.Default.initCollapsed;
                chkOnlyPPC.Checked = Properties.Settings.Default.onlyPPC;
                chkBetterSQL.Checked = Properties.Settings.Default.betterSQL;
                chkAutoDark.Checked = Properties.Settings.Default.autoDark;
                chkLintAnnotate.Checked = Properties.Settings.Default.lintAnnotate;
                chkAutoPairing.Checked = Properties.Settings.Default.autoPair;
                chkPromptForDB.Checked = Properties.Settings.Default.promptForDB;
                LoadStylerStates();
                LoadLinterStates();
                LoadTooltipStates();
                // Removed call to LoadTemplates() as it's handled in OnLoad via TemplateManager
                // LoadTemplates(); 
            }
            finally
            {
                isLoadingSettings = false;
            }
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.initCollapsed = chkInitCollapsed.Checked;
            Properties.Settings.Default.onlyPPC = chkOnlyPPC.Checked;
            Properties.Settings.Default.betterSQL = chkBetterSQL.Checked;
            Properties.Settings.Default.autoDark = chkAutoDark.Checked;
            Properties.Settings.Default.lintAnnotate = chkLintAnnotate.Checked;
            Properties.Settings.Default.LintReportPath = lintReportPath;
            Properties.Settings.Default.autoPair = chkAutoPairing.Checked;
            Properties.Settings.Default.promptForDB = chkPromptForDB.Checked;

            SaveStylerStates();
            SaveLinterStates();
            SaveTooltipStates();

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
            catch
            { /* Use defaults if settings are corrupt */
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

        private void LoadTooltipStates()
        {
            try
            {
                var states = System.Text.Json.JsonSerializer.Deserialize<List<RuleState>>(
                    Properties.Settings.Default.TooltipStates);

                if (states == null) return;

                foreach (var state in states)
                {
                    var provider = tooltipProviders.FirstOrDefault(p => p.GetType().FullName == state.TypeName);
                    if (provider != null)
                    {
                        provider.Active = state.Active;
                        // Update corresponding grid row
                        var row = dataGridViewTooltips.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Tag is ITooltipProvider p && p == provider);
                        if (row != null)
                        {
                            row.Cells[0].Value = state.Active;
                        }
                    }
                }
            }
            catch
            { /* Use defaults if settings are corrupt */
            }
        }

        private void SaveTooltipStates()
        {
            var states = tooltipProviders.Select(p => new RuleState
            {
                TypeName = p.GetType().FullName ?? "",
                Active = p.Active
            }).ToList();

            Properties.Settings.Default.TooltipStates =
                System.Text.Json.JsonSerializer.Serialize(states);
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
            
            // Ensure our new InvalidAppClass styler is added
            var invalidAppClassStyler = new Stylers.InvalidAppClass();
            bool alreadyAdded = stylers.Any(s => s is Stylers.InvalidAppClass);
            if (!alreadyAdded)
            {
                int rowIndex = dataGridView3.Rows.Add(invalidAppClassStyler.Active, invalidAppClassStyler.Description);
                dataGridView3.Rows[rowIndex].Tag = invalidAppClassStyler;
                stylers.Add(invalidAppClassStyler);
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

            // Get the parsed program, token stream and comments using the caching mechanism
            var (program, stream, comments) = editor.GetParsedProgram();
            
            // Get active stylers filtering out those that require a database when one isn't available
            var activeStylers = stylers.Where(a => a.Active && (a.DatabaseRequirement != DataManagerRequirement.Required || editor.DataManager != null));
            
            // Assign data manager to each styler
            foreach (var styler in activeStylers)
            {
                styler.DataManager = editor.DataManager;
            }
            
            MultiParseTreeWalker walker = new();

            List<Indicator> newIndicators = new();

            foreach (var styler in activeStylers)
            {
                styler.Indicators = newIndicators;
                styler.Comments = comments;
                walker.AddListener(styler);
            }

            walker.Walk(program);

            foreach (var styler in activeStylers)
            {
                /* clear out any internal states for the stylers */
                styler.Reset();
            }

            // Get sets of current and new indicators for comparison
            var currentIndicators = editor.ActiveIndicators;
            
            // Build a set to track indicators to remove
            var indicatorsToRemove = new List<Indicator>();
            
            // Find all indicators that are no longer needed
            foreach (var currentIndicator in currentIndicators)
            {
                bool stillNeeded = newIndicators.Any(ni => 
                    ni.Start == currentIndicator.Start && 
                    ni.Length == currentIndicator.Length && 
                    ni.Color == currentIndicator.Color &&
                    ni.Type == currentIndicator.Type);
                
                if (!stillNeeded)
                {
                    indicatorsToRemove.Add(currentIndicator);
                }
            }
            
            // Find all indicators that need to be added
            var indicatorsToAdd = newIndicators.Where(ni => 
                !currentIndicators.Any(ci => 
                    ci.Start == ni.Start && 
                    ci.Length == ni.Length && 
                    ci.Color == ni.Color &&
                    ci.Type == ni.Type)).ToList();
            
            // Remove indicators that are no longer needed
            foreach (var indicator in indicatorsToRemove)
            {
                if (indicator.Type == IndicatorType.HIGHLIGHTER)
                {
                    ScintillaManager.RemoveHighlightWithColor(editor, indicator.Color, indicator.Start, indicator.Length);
                }
                else if (indicator.Type == IndicatorType.SQUIGGLE)
                {
                    ScintillaManager.RemoveSquiggleWithColor(editor, indicator.Color, indicator.Start, indicator.Length);
                }
                else if (indicator.Type == IndicatorType.TEXTCOLOR)
                {
                    ScintillaManager.RemoveTextColorWithColor(editor, indicator.Color, indicator.Start, indicator.Length);
                }
            }
            
            // Add new indicators
            foreach (var indicator in indicatorsToAdd)
            {
                if (indicator.Type == IndicatorType.HIGHLIGHTER)
                {
                    ScintillaManager.HighlightTextWithColor(editor, indicator.Color, indicator.Start, indicator.Length, indicator.Tooltip);
                }
                else if (indicator.Type == IndicatorType.SQUIGGLE)
                {
                    ScintillaManager.SquiggleTextWithColor(editor, indicator.Color, indicator.Start, indicator.Length, indicator.Tooltip);
                }
                else if (indicator.Type == IndicatorType.TEXTCOLOR)
                {
                    ScintillaManager.TextColorWithColor(editor, indicator.Color, indicator.Start, indicator.Length);
                }
            }
            
            // Update the editor's active indicator list with the new set
            editor.ActiveIndicators = newIndicators;
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

            // Get the parsed program, token stream and comments using the caching mechanism
            var (program, stream, comments) = activeEditor.GetParsedProgram();

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

            /* Process the reports */
            activeEditor.SetLinterReports(reports);
            dataGridView2.Rows.Clear();
            
            foreach (var g in reports.GroupBy(r => r.Line).OrderBy(b => b.First().Line))
            {
                List<string> messages = new();
                List<AnnotationStyle> styles = new();

                foreach (var report in g)
                {
                    int rowIndex = dataGridView2.Rows.Add(report.Type, report.Message, report.Line);
                    dataGridView2.Rows[rowIndex].Tag = report;

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

            // Get the parsed program, token stream and comments using the caching mechanism
            var (program, stream, comments) = activeEditor.GetParsedProgram();

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
                    Debug.Log($"Error getting refactor properties for {type.Name}: {ex.Message}");
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
                        var line = ScintillaManager.GetCurrentLineNumber(activeEditor);
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

            // Filter out hidden refactors
            refactorTypes = refactorTypes.Where(type => {
                try {
                    var isHiddenProperty = type.GetProperty("IsHidden", 
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    return isHiddenProperty == null || !(bool)isHiddenProperty.GetValue(null)!;
                }
                catch {
                    return true; // If we can't determine IsHidden, include the refactor
                }
            });

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
                    Debug.Log($"Error getting refactor properties for {type.Name}: {ex.Message}");
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
                Debug.Log($"Error getting shortcut text for {refactorType.Name}: {ex.Message}");
            }

            return string.Empty;
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
            
            // Check if editor is in a clean state before processing
            if (ScintillaManager.IsEditorClean(editor) || true)
            {                
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
                    ProcessStylers(editor);
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
                        if (activeEditor != null)
                        {
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
                        }
                        break;
                    case SCN_USERLISTSELECTION:
                        Debug.Log("User list selection received");
                        // wParam is the list type
                        int listType = m.WParam.ToInt32();
                        // lParam is a pointer to a UTF8 string in the editor's process memory
                        if (m.LParam != IntPtr.Zero && activeEditor != null)
                        {
                            // Read the UTF8 string from the editor's process memory
                            // Use a reasonable buffer size (256 bytes should be enough for most strings)
                            string? selectedText = ScintillaManager.ReadUtf8FromMemory(activeEditor, m.LParam, 256);
                            
                            if (!string.IsNullOrEmpty(selectedText))
                            {
                                Debug.Log($"User selected: {selectedText} (list type: {listType})");
                                HandleUserListSelection(activeEditor, selectedText, listType);
                            }
                        }
                        break;
                }
            }
            else if (m.Msg == AR_APP_PACKAGE_SUGGEST)
            {
                /* Handle app package suggestion request */
                Debug.Log($"Received app package suggest message. WParam: {m.WParam}, LParam: {m.LParam}");
                
                // WParam contains the current cursor position
                int position = m.WParam.ToInt32();
                
                // Show app package suggestions at the current position
                ShowAppPackageSuggestions(activeEditor, position);
            }
            else if (m.Msg == AR_CREATE_SHORTHAND)
            {
                /* Handle create shorthand detection */
                Debug.Log($"Received create shorthand message. WParam: {m.WParam}, LParam: {m.LParam}");
                
                // WParam contains auto-pairing status (bool)
                bool autoPairingEnabled = m.WParam.ToInt32() != 0;
                
                // LParam contains the current cursor position
                int position = m.LParam.ToInt32();
                
                // Process create shorthand at the current position
                HandleCreateShorthand(activeEditor, position, autoPairingEnabled);
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

        // Adds the WinEventHook methods to listen for Scintilla editor creation
        private void SetupWinEventHook()
        {
            // Create the delegate and save a reference so it won't be garbage collected
            winEventDelegate = new WinEventDelegate(WinEventProc);
            
            // Hook to listen for window creation events in all processes/threads
            winEventHook = SetWinEventHook(
                EVENT_OBJECT_FOCUS,       // Event to listen for (window creation)
                EVENT_OBJECT_FOCUS,   // Also listen for foreground changes
                IntPtr.Zero,               // No DLL injection
                winEventDelegate,          // Callback
                0,                         // All processes
                0,                         // All threads
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS // Options
            );
            
            if (winEventHook == IntPtr.Zero)
            {
                Debug.Log("Failed to set up WinEvent hook");
            }
            else
            {
                Debug.Log("Successfully set up WinEvent hook for window creation and focus events");
            }
        }
        
        // WinEvent callback - called when windows are created in other processes
        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, 
            int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == EVENT_OBJECT_FOCUS)
            {
                /* If we've focused something other than the active editor, set active editor to null */
                if (activeEditor != null && hwnd != activeEditor.hWnd && !activeEditor.IsValid())
                {
                    Debug.Log("Active editor has lost focus and isn't valid anymore.");
                    activeEditor = null;
                    Stylers.InvalidAppClass.ClearValidAppClassPathsCache();
                }

                // A window has gained focus - check if it's a Scintilla window
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                
                if (className.ToString().Contains("Scintilla"))
                {
                    /* Ensure hwnd is owned by "pside.exe" */

                    GetWindowThreadProcessId(hwnd, out var processId);
                    if ("pside".Equals(Process.GetProcessById((int)processId).ProcessName))
                    {
                        Debug.Log($"WinEvent detected Scintilla window focus: 0x{hwnd.ToInt64():X}");
                        Debug.Log($"idObject: {idObject}, idChild: {idChild}, dwEventThread: {dwEventThread}, dwmsEventTime: {dwmsEventTime}");
                        // Use BeginInvoke to process on UI thread
                        this.BeginInvoke(new Action(() =>
                        {
                            // Use SetActiveEditor to properly handle the window
                            SetActiveEditor(hwnd);

                            if (activeEditor != null && activeEditor.IsValid())
                            {
                                if (!activeEditor.FoldEnabled)
                                {
                                    ProcessNewEditor(activeEditor);
                                }
                                else
                                {
                                    // Check if content has changed
                                    CheckForContentChanges(activeEditor);
                                }
                            }
                        }));
                    }
                }
            }
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
            
            // Populate the DBName property
            PopulateEditorDBName(editor);
            
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
        /// Populates the DBName property of a ScintillaEditor based on its context
        /// </summary>
        /// <param name="editor">The editor to populate the DBName for</param>
        private void PopulateEditorDBName(ScintillaEditor editor)
        {
            // Start with the editor's handle
            IntPtr hwnd = editor.hWnd;
            
            // Walk up the parent chain until we find the Application Designer window
            while (hwnd != IntPtr.Zero)
            {
                StringBuilder caption = new StringBuilder(256);
                GetWindowText(hwnd, caption, caption.Capacity);
                string windowTitle = caption.ToString();
                
                // Check if this is the Application Designer window
                if (windowTitle.StartsWith("Application Designer"))
                {
                    // Split the title by " - " and get the second part (DB name)
                    string[] parts = windowTitle.Split(new[] { " - " }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        editor.DBName = parts[1].Trim();
                        Debug.Log($"Set editor DBName to: {editor.DBName}");
                        
                        // Now determine the relative file path based on the database name
                        DetermineRelativeFilePath(editor);
                        
                        break;
                    }
                }
                
                // Get the parent window
                hwnd = GetParent(hwnd);
            }
        }

        /// <summary>
        /// Determines the relative file path for an editor based on its window hierarchy and caption
        /// </summary>
        /// <param name="editor">The editor to determine the relative path for</param>
        private void DetermineRelativeFilePath(ScintillaEditor editor)
        {
            try
            {
                // Start with the editor's handle
                IntPtr hwnd = editor.hWnd;
                
                // Get the grandparent window to examine its caption
                IntPtr parentHwnd = GetParent(hwnd);
                if (parentHwnd != IntPtr.Zero)
                {
                    IntPtr grandparentHwnd = GetParent(parentHwnd);
                    if (grandparentHwnd != IntPtr.Zero)
                    {
                        StringBuilder caption = new StringBuilder(512);
                        GetWindowText(grandparentHwnd, caption, caption.Capacity);
                        string windowTitle = caption.ToString().Trim();
                        
                        Debug.Log($"Editor grandparent window title: {windowTitle}");
                        
                        // Determine editor type and generate appropriate relative path
                        string? relativePath = DetermineRelativePathFromCaption(windowTitle, editor.DBName);
                        
                        if (!string.IsNullOrEmpty(relativePath))
                        {
                            editor.RelativePath = relativePath;
                            Debug.Log($"Set editor RelativePath to: {relativePath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error determining relative path: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Determines the relative file path based on the window caption and database name
        /// </summary>
        /// <param name="caption">The window caption</param>
        /// <param name="dbName">The database name</param>
        /// <returns>The relative file path or null if can't be determined</returns>
        private string? DetermineRelativePathFromCaption(string caption, string? dbName)
        {
            // This handles specific parsing logic for different PeopleCode editor types
            // as indicated by the (type) suffix in the caption
            
            if (string.IsNullOrEmpty(caption))
                return null;
                
            Debug.Log($"Determining path from caption: {caption}");
            
            // Check for PeopleCode type in caption - usually appears as (PeopleCode) or some other type suffix
            int typeStartIndex = caption.LastIndexOf('(');
            int typeEndIndex = caption.LastIndexOf(')');
            
            if (typeStartIndex >= 0 && typeEndIndex > typeStartIndex)
            {
                string editorType = caption.Substring(typeStartIndex + 1, typeEndIndex - typeStartIndex - 1);
                string captionWithoutType = caption.Substring(0, typeStartIndex).Trim();
                
                Debug.Log($"Editor type: {editorType}, Caption without type: {captionWithoutType}");
                
                // Different logic based on editor type
                switch (editorType)
                {
                    case "App Engine Program PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "app_engine");
                    case "Application Package PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "app_package");
                    case "Component Interface PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "comp_intfc");
                    case "Menu PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "menu");
                    case "Message PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "message");
                    case "Page PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "page");
                    case "Record PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "record");
                    case "Component PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "component");
                        
                    case "SQL Definition":
                        return DetermineSqlDefinitionPath(captionWithoutType, dbName);
                        
                    case "HTML":
                        return DetermineHtmlPath(captionWithoutType, dbName);
                        
                    case "StyleSheet":
                    case "Style Sheet":
                        return DetermineStyleSheetPath(captionWithoutType, dbName);
                        
                    default:
                        // If it contains "PeopleCode" anywhere, treat it as PeopleCode
                        if (editorType.Contains("PeopleCode"))
                        {
                            return DeterminePeopleCodePath(captionWithoutType, dbName, "peoplecode");
                        }
                        
                        // Generic handling for unknown types
                        return $"unknown/{editorType.ToLower().Replace(" ", "_")}/{captionWithoutType.Replace(" ", "_").ToLower()}.txt";
                }
            }
            
            // No recognized type in caption
            return null;
        }
        
        /// <summary>
        /// Determines the relative file path for PeopleCode based on editor caption
        /// </summary>
        private string? DeterminePeopleCodePath(string captionWithoutType, string? dbName, string relativeRoot)
        {
            // DB name should always be present at this point, but default if not
            string db = dbName ?? "unknown_db";
            
            // Clean up the caption
            captionWithoutType = captionWithoutType.Trim();
            
            // PeopleCode paths follow a specific format
            string pcPath = $"{db.ToLower()}/peoplecode/{relativeRoot}/{captionWithoutType.Replace(".", "/")}.pcode";
            
            Debug.Log($"PeopleCode path: {pcPath}");
            
            return pcPath;
        }

        /// <summary>
        /// Determines the relative file path for SQL definitions
        /// </summary>
        private string? DetermineSqlDefinitionPath(string captionWithoutType, string? dbName)
        {
            // DB name should always be present at this point, but default if not
            string db = dbName ?? "unknown_db";
            
            // Clean up the caption
            captionWithoutType = captionWithoutType.Trim();
            
            // SQL objects use a specific suffix format like .0 or .2
            string sqlType = "sql_object";
            
            // Check for SQL type indicators at the end (.0, .1, .2, etc.)
            // Format is typically NAME.NUMBER
            int lastDotPos = captionWithoutType.LastIndexOf('.');
            if (lastDotPos >= 0 && lastDotPos < captionWithoutType.Length - 1)
            {
                // Try to parse the suffix after the last dot
                string suffix = captionWithoutType.Substring(lastDotPos + 1);
                
                // If the suffix is a number, determine the SQL type
                if (int.TryParse(suffix, out int sqlTypeNumber))
                {
                    switch (sqlTypeNumber)
                    {
                        case 0:
                            sqlType = "sql_object";
                            break;
                        case 2:
                            sqlType = "sql_view";
                            break;
                        default:
                            sqlType = $"sql_type_{sqlTypeNumber}";
                            break;
                    }
                    
                    // Remove the suffix for the path
                    captionWithoutType = captionWithoutType.Substring(0, lastDotPos);
                }
            }
            
            // Build the full path
            string fullPath = $"{db.ToLower()}/sql/{sqlType}/{captionWithoutType}.sql";
            
            Debug.Log($"SQL path: {fullPath}");
            
            return fullPath;
        }
        
        /// <summary>
        /// Determines the relative file path for an HTML editor
        /// </summary>
        private string? DetermineHtmlPath(string captionWithoutType, string? dbName)
        {
            // DB name should always be present at this point, but default if not
            string db = dbName ?? "unknown_db";
            
            // HTML paths follow a specific format
            string htmlPath = $"{db.ToLower()}/html/{captionWithoutType.Replace(".", "/")}.html";
            
            Debug.Log($"HTML path: {htmlPath}");
            
            return htmlPath;
        }
        
        /// <summary>
        /// Determines the relative file path for a stylesheet editor
        /// </summary>
        private string? DetermineStyleSheetPath(string captionWithoutType, string? dbName)
        {
            // DB name should always be present at this point, but default if not
            string db = dbName ?? "unknown_db";
            
            // Stylesheet paths follow a specific format
            string cssPath = $"{db.ToLower()}/stylesheet/{captionWithoutType.Replace(".", "/")}.css";
            
            Debug.Log($"Stylesheet path: {cssPath}");
            
            return cssPath;
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
        private void ProcessSavepoint(object? state)
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

        /// <summary>
        /// Shows app package suggestions when a colon is typed
        /// </summary>
        /// <param name="editor">The current Scintilla editor</param>
        /// <param name="position">Current cursor position</param>
        private void ShowAppPackageSuggestions(ScintillaEditor editor, int position)
        {
            try
            {
                if (editor == null || editor.DataManager == null) return;

                // Get the current line and content up to the cursor position
                int currentLine = (int)editor.SendMessage(0x2166, position, 0); // SCI_LINEFROMPOSITION
                int lineStartPos = (int)editor.SendMessage(0x2167, currentLine, 0); // SCI_POSITIONFROMLINE
                
                string content = ScintillaManager.GetScintillaText(editor) ?? "";
                string lineContent = content.Substring(lineStartPos, position - lineStartPos);

                // Check if there's a colon in the line content
                if (!lineContent.Contains(':'))
                {
                    Debug.Log("No colon found in line content");
                    return;
                }

                // Extract the potential package path - we need to look for identifiers before the colon
                string packagePath = ExtractPackagePathFromLine(lineContent);
                if (string.IsNullOrEmpty(packagePath))
                {
                    Debug.Log("No valid package path found");
                    return;
                }

                Debug.Log($"Extracted package path: {packagePath}");

                // Get package items from database
                var packageItems = editor.DataManager.GetAppPackageItems(packagePath);

                // Convert to list of strings for autocomplete
                List<string> suggestions = new List<string>();
                suggestions.AddRange(packageItems.Subpackages.Select(p => $"{p} (Package)"));
                suggestions.AddRange(packageItems.Classes.Select(c => $"{c} (Class)"));

                if (suggestions.Count > 0)
                {
                    // Show the user list popup with app package suggestions
                    Debug.Log($"Showing {suggestions.Count} app package suggestions for '{packagePath}'");
                    bool result = ScintillaManager.ShowUserList(editor, 1, position, suggestions);
                    
                    if (!result)
                    {
                        Debug.Log("Failed to show user list popup");
                    }
                }
                else
                {
                    Debug.Log($"No suggestions found for '{packagePath}'");
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting app package suggestions: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts a valid package path from a line of text
        /// </summary>
        /// <param name="lineContent">The line content to analyze</param>
        /// <returns>The extracted package path or empty string if not found</returns>
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

        private void HandleUserListSelection(ScintillaEditor? editor, string selection, int listType = 0)
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

            if (editor == null) return;
            bool isClassSelection = false;
            if (listType == 1)
            {
                var parts = selection.Split(" "); // Remove the type suffix
                selection = parts[0];
                isClassSelection = parts[1].Equals("(Class)", StringComparison.OrdinalIgnoreCase);
            }


            /* execute the resolve imports command */
            if (isClassSelection)
            {
                ScintillaManager.InsertTextAtCursor(editor, selection);
                var lineText = ScintillaManager.GetCurrentLineText(editor);
                if (lineText != String.Empty)
                {
                    AddImport resolveImports = new AddImport(editor, lineText.Split(" ").Last());
                    ProcessRefactor(resolveImports);
                }
            } else
            {
                ScintillaManager.InsertTextAtCursor(editor, $"{selection}:");

                /* Send ourselves a AR_APP_PACKAGE_SUGGEST message */
                //SendMessage(this.Handle, AR_APP_PACKAGE_SUGGEST, ScintillaManager.GetCursorPosition(editor), 0);

                /* after 100ms send the message to ourselves, but let this function return */
                Task.Delay(100).ContinueWith(_ =>
                {
                    //SendMessage(this.Handle, AR_APP_PACKAGE_SUGGEST, ScintillaManager.GetCursorPosition(editor), 0);
                    ShowAppPackageSuggestions(editor, ScintillaManager.GetCursorPosition(editor));
                });

            }

        }

        /// <summary>
        /// Handle the "create(" shorthand pattern detection
        /// </summary>
        /// <param name="editor">The active Scintilla editor</param>
        /// <param name="position">The current cursor position</param>
        private void HandleCreateShorthand(ScintillaEditor? editor, int position, bool autoPairingEnabled)
        {
            if (editor == null || !editor.IsValid()) return;
            
            // Execute the CreateAutoComplete refactor
            Debug.Log($"Create shorthand detected at position {position}");
            ProcessRefactor(new CreateAutoComplete(editor, autoPairingEnabled));
        }

    }
    
    /// <summary>
    /// Enumeration of code definition types
    /// </summary>
    public enum GoToDefinitionType
    {
        Method,
        Property,
        Function,
        Getter,
        Setter,
        Instance
    }

    /// <summary>
    /// Enumeration of code definition scopes
    /// </summary>
    public enum GoToDefinitionScope
    {
        Public,
        Protected,
        Private,
        Global  // For functions outside of classes
    }

    /// <summary>
    /// Represents a code definition (method, property, function, etc.) that can be navigated to
    /// </summary>
    public class GoToCodeDefinition
    {
        /// <summary>
        /// The name of the definition
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of the definition (return type for methods, property type, etc.)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The type of code definition (method, property, function, etc.)
        /// </summary>
        public GoToDefinitionType DefinitionType { get; set; }

        /// <summary>
        /// The scope of the definition (public, protected, private)
        /// </summary>
        public GoToDefinitionScope Scope { get; set; }

        /// <summary>
        /// The position in the source code where the definition begins
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Line number where the definition begins
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Gets the formatted display text for the definition
        /// </summary>
        public string DisplayText => $"{Name}: {Type}";

        /// <summary>
        /// Gets a description of the definition for tooltip or additional information
        /// </summary>
        public string Description => $"{GetScopePrefix()}{DefinitionType} {Name} of type {Type} at line {Line}";

        /// <summary>
        /// Creates a new code definition with the provided parameters
        /// </summary>
        public GoToCodeDefinition(string name, string type, GoToDefinitionType definitionType, GoToDefinitionScope scope, int position, int line)
        {
            Name = name;
            Type = type;
            DefinitionType = definitionType;
            Scope = scope;
            Position = position;
            Line = line;
        }

        /// <summary>
        /// Gets the scope prefix for display purposes
        /// </summary>
        private string GetScopePrefix()
        {
            return Scope switch
            {
                GoToDefinitionScope.Public => "[Public] ",
                GoToDefinitionScope.Protected => "[Protected] ",
                GoToDefinitionScope.Private => "[Private] ",
                GoToDefinitionScope.Global => "[Global] ",
                _ => string.Empty
            };
        }
    }

    /// <summary>
    /// A visitor implementation that collects code definitions for navigation purposes
    /// </summary>
    public class GoToDefinitionVisitor : PeopleCodeParserBaseListener
    {
        /// <summary>
        /// Collection of all definitions found in the code
        /// </summary>
        public List<GoToCodeDefinition> Definitions { get; } = new List<GoToCodeDefinition>();

        /// <summary>
        /// Tracks the current scope during traversal
        /// </summary>
        private GoToDefinitionScope currentScope = GoToDefinitionScope.Public;

        /// <summary>
        /// Dictionary to map method/property names to their scope from the header declarations
        /// </summary>
        private Dictionary<string, GoToDefinitionScope> memberScopes = new Dictionary<string, GoToDefinitionScope>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Dictionary to map property names to their types from the header declarations
        /// </summary>
        private Dictionary<string, string> propertyTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Process method headers to capture scope information
        /// </summary>
        public override void EnterMethodHeader(PeopleCodeParser.MethodHeaderContext context)
        {
            if (context.genericID() != null)
            {
                var methodName = context.genericID().GetText();
                // Store the method name with its scope from the header
                memberScopes[methodName] = currentScope;
            }
        }

        /// <summary>
        /// Process property declarations in headers to capture scope information
        /// </summary>
        public override void EnterPropertyGetSet(PeopleCodeParser.PropertyGetSetContext context)
        {
            var propertyName = context.genericID().GetText();
            var propertyType = GetTypeString(context.typeT());
            
            // Store the property name with its scope and type from the header
            memberScopes[propertyName] = currentScope;
            propertyTypes[propertyName] = propertyType;

            Definitions.Add(new GoToCodeDefinition(
                propertyName,
                propertyType,
                GoToDefinitionType.Property,
                currentScope,
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Process property declarations in headers to capture scope information
        /// </summary>
        public override void EnterPropertyDirect(PeopleCodeParser.PropertyDirectContext context)
        {
            var propertyName = context.genericID().GetText();
            var propertyType = GetTypeString(context.typeT());
            
            // Store the property name with its scope and type from the header
            memberScopes[propertyName] = currentScope;
            propertyTypes[propertyName] = propertyType;

            Definitions.Add(new GoToCodeDefinition(
                propertyName,
                propertyType,
                GoToDefinitionType.Property,
                currentScope,
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Method implementations
        /// </summary>
        /// <param name="context">The method context</param>
        public override void EnterMethod(PeopleCodeParser.MethodContext context)
        {
            var methodName = context.genericID().GetText();
            var returnType = "void"; // Default to void

            // Look up the method's scope from its header declaration
            var methodScope = GoToDefinitionScope.Public; // Default to public
            if (memberScopes.TryGetValue(methodName, out var scope))
            {
                methodScope = scope;
            }

            Definitions.Add(new GoToCodeDefinition(
                methodName,
                returnType,
                GoToDefinitionType.Method,
                methodScope,
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Property getter implementations
        /// </summary>
        /// <param name="context">The getter context</param>
        public override void EnterGetter(PeopleCodeParser.GetterContext context)
        {
            var propertyName = context.genericID().GetText();
            
            // Look up the property's scope from its header declaration
            var propertyScope = GoToDefinitionScope.Public; // Default to public
            if (memberScopes.TryGetValue(propertyName, out var scope))
            {
                propertyScope = scope;
            }
            
            // Look up the property's type from its header declaration
            var propertyType = "any"; // Default to any
            if (propertyTypes.TryGetValue(propertyName, out var type))
            {
                propertyType = type;
            }

            Definitions.Add(new GoToCodeDefinition(
                propertyName,
                propertyType,
                GoToDefinitionType.Getter,
                propertyScope,
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Property setter implementations
        /// </summary>
        /// <param name="context">The setter context</param>
        public override void EnterSetter(PeopleCodeParser.SetterContext context)
        {
            var propertyName = context.genericID().GetText();
            
            // Look up the property's scope from its header declaration
            var propertyScope = GoToDefinitionScope.Public; // Default to public
            if (memberScopes.TryGetValue(propertyName, out var scope))
            {
                propertyScope = scope;
            }
            
            // Look up the property's type from its header declaration
            var propertyType = "any"; // Default to any
            if (propertyTypes.TryGetValue(propertyName, out var type))
            {
                propertyType = type;
            }

            Definitions.Add(new GoToCodeDefinition(
                propertyName,
                propertyType,
                GoToDefinitionType.Setter,
                propertyScope,
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Function definitions
        /// </summary>
        /// <param name="context">The function definition context</param>
        public override void EnterFunctionDefinition(PeopleCodeParser.FunctionDefinitionContext context)
        {
            var functionName = context.allowableFunctionName().GetText();
            var returnType = "void"; // Default to void

            // Check if there's a return type specified
            if (context.typeT() != null)
            {
                returnType = GetTypeString(context.typeT());
            }

            Definitions.Add(new GoToCodeDefinition(
                functionName,
                returnType,
                GoToDefinitionType.Function,
                GoToDefinitionScope.Global, // Functions are always global scope
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit instance declarations (private variables)
        /// </summary>
        public override void EnterInstanceDecl(PeopleCodeParser.InstanceDeclContext context)
        {
            var propertyType = GetTypeString(context.typeT());

            // An instance declaration can define multiple variables
            foreach (var varNode in context.USER_VARIABLE())
            {
                var variableName = varNode.GetText();
                
                // Store the variable type, but mark as Instance type rather than Property
                propertyTypes[variableName] = propertyType;

                Definitions.Add(new GoToCodeDefinition(
                    variableName,
                    propertyType,
                    GoToDefinitionType.Instance, // Use Instance instead of Property
                    GoToDefinitionScope.Private, // Instance variables are always private
                    varNode.Symbol.StartIndex,
                    varNode.Symbol.Line
                ));
            }
        }

        /// <summary>
        /// Enter public header section
        /// </summary>
        public override void EnterPublicHeader(PeopleCodeParser.PublicHeaderContext context)
        {
            currentScope = GoToDefinitionScope.Public;
        }

        /// <summary>
        /// Enter protected header section
        /// </summary>
        public override void EnterProtectedHeader(PeopleCodeParser.ProtectedHeaderContext context)
        {
            currentScope = GoToDefinitionScope.Protected;
        }

        /// <summary>
        /// Enter private header section
        /// </summary>
        public override void EnterPrivateHeader(PeopleCodeParser.PrivateHeaderContext context)
        {
            currentScope = GoToDefinitionScope.Private;
        }

        /// <summary>
        /// Helper method to extract type information from type context
        /// </summary>
        private string GetTypeString(PeopleCodeParser.TypeTContext typeContext)
        {
            if (typeContext == null)
                return "any";

            if (typeContext is PeopleCodeParser.ArrayTypeContext arrayType)
            {
                var baseType = arrayType.typeT() != null
                    ? GetTypeString(arrayType.typeT())
                    : "any";
                return $"Array of {baseType}";
            }
            else if (typeContext is PeopleCodeParser.BaseExceptionTypeContext)
            {
                return "Exception";
            }
            else if (typeContext is PeopleCodeParser.AppClassTypeContext appClass)
            {
                return appClass.appClassPath().GetText();
            }
            else if (typeContext is PeopleCodeParser.SimpleTypeTypeContext simpleType)
            {
                return simpleType.simpleType().GetText();
            }

            return typeContext.GetText();
        }
    }
}


