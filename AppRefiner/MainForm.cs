using AppRefiner.Stylers;
using AppRefiner.Linters;
using AppRefiner.PeopleCode;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using static SqlParser.Ast.CopyTarget;
using Antlr4.Build.Tasks;
using AppRefiner.Refactors;
using SharpCompress.Readers;
using Antlr4.Runtime.Tree;
using AppRefiner.Database;
using AppRefiner.Templates;

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

        // Import GetWindowRect from user32.dll.
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

        System.Threading.Timer scanTimer;
        private bool timerRunning = false;
        private object scanningLock = new();
        private ScintillaEditor? activeEditor = null;
        private List<BaseLintRule> linterRules = new();
        private List<BaseStyler> stylers = new(); // Changed from List<BaseStyler> analyzers

        // Map of process IDs to their corresponding data managers
        private Dictionary<uint, IDataManager> processDataManagers = new Dictionary<uint, IDataManager>();
        private Dictionary<string, Control> templateInputControls = new Dictionary<string, Control>();
        private Dictionary<string, Control> templateInputLabels = new Dictionary<string, Control>();
        private Dictionary<string, DisplayCondition> templateInputsDisplayConditions = new Dictionary<string, DisplayCondition>();

        // Static list of available commands
        public static List<Command> AvailableCommands = new List<Command>();
        
        KeyboardHook renameVarHook = new KeyboardHook();
        KeyboardHook lintCodeHook = new KeyboardHook();
        KeyboardHook collapseLevel = new KeyboardHook();
        KeyboardHook expandLevel = new KeyboardHook();
        KeyboardHook collapseAll = new KeyboardHook();
        KeyboardHook expandAll = new KeyboardHook();
        KeyboardHook commandPaletteHook = new KeyboardHook();
        private class RuleState
        {
            public string TypeName { get; set; } = "";
            public bool Active { get; set; }
        }

        public MainForm()
        {
            InitializeComponent();
            InitLinterOptions();
            InitStylerOptions(); // Changed from InitAnalyzerOptions
            RegisterCommands(); // Register the default commands
        }

        protected override void OnLoad(EventArgs e)
        {
            LoadSettings();
            renameVarHook.KeyPressed += renameVariable;
            renameVarHook.RegisterHotKey(AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Shift, Keys.R);

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

        }

        private void lintCodeHandler(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            ProcessLinters();
        }

        private void ShowCommandPalette(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            
            // Create the command palette dialog
            var palette = new CommandPalette(AvailableCommands);
            
            // Set the owner to the editor's parent window
            var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;
            
            // Show the dialog
            palette.ShowDialog(new WindowWrapper(mainHandle));
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

        private void renameVariable(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            /* Ask the user for a new variable name */
            string newName = "";

            var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;

            var dlgResult = ShowInputDialog("New variable name", "Enter new variable name", ref newName, mainHandle);

            if (dlgResult != DialogResult.OK) return;

            /* Get the current cursor position */
            int cursorPosition = ScintillaManager.GetCursorPosition(activeEditor);

            /* Create a new instance of the refactoring class */
            RenameLocalVariable refactor = new RenameLocalVariable(cursorPosition, newName);
            ProcessRefactor(refactor, mainHandle);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
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
                Label label = new Label
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
                            Location = new Point(labelWidth + horizontalPadding * 2, currentY),
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
                            Location = new Point(labelWidth + horizontalPadding * 2, currentY),
                            Size = new Size(controlWidth, 23),
                            Tag = input.Id // Store input ID in Tag for easier reference
                        };
                        break;
                }

                // Add tooltip if description is available
                if (!string.IsNullOrEmpty(input.Description))
                {
                    ToolTip tooltip = new ToolTip();
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

                if (templateInputControls.TryGetValue(inputId, out Control control))
                {
                    bool shouldDisplay = Template.IsDisplayConditionMet(condition, currentValues);
                    visibilityChanged |= (control.Visible != shouldDisplay);
                    control.Visible = shouldDisplay;

                    // Also update the label visibility
                    if (templateInputLabels.TryGetValue(inputId, out Control label))
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
                if (templateInputControls.TryGetValue(inputId, out Control control) &&
                    templateInputLabels.TryGetValue(inputId, out Control label))
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
            catch { /* Use defaults if settings are corrupt */ }
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

        private void btnStart_Click(object sender, EventArgs e)
        {
            timerRunning = !timerRunning;
            if (timerRunning)
            {
                scanTimer = new System.Threading.Timer(ScanTick, null, 1000, Timeout.Infinite);
            }
            btnStart.Text = timerRunning ? "Stop" : "Start";
        }

        private void EnableUIActions()
        {
            this.Invoke((Action)(() =>
            {
                grpEditorActions.Enabled = true;
                btnLintCode.Enabled = true;
                btnClearLint.Enabled = true;
                grpRefactors.Enabled = true;
                btnApplyTemplate.Text = "Apply Template";

                if (activeEditor?.DataManager == null)
                {
                    btnConnectDB.Text = "Connect DB...";
                }
                else
                {
                    btnConnectDB.Text = "Disconnect DB";
                }

            }));

        }

        private void DisableUIActions()
        {
            this.Invoke((Action)(() =>
            {
                grpEditorActions.Enabled = false;
                btnLintCode.Enabled = false;
                btnClearLint.Enabled = false;
                grpRefactors.Enabled = false;
                btnApplyTemplate.Text = "Generate Template";
            }));
        }

        private void InitLinterOptions()
        {
            /* Find all classes in this assembly that extend BaseLintRule*/
            var linters = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(BaseLintRule).IsAssignableFrom(p) && !p.IsAbstract);

            foreach (var type in linters)
            {
                /* Create instance of the linter */
                BaseLintRule? linter = (BaseLintRule?)Activator.CreateInstance(type);

                if (linter != null)
                {
                    /* Create row for datadgridview */
                    int rowIndex = dataGridView1.Rows.Add(linter.Active, linter.Description, linter.Type.ToString());
                    dataGridView1.Rows[rowIndex].Tag = linter;
                    linterRules.Add(linter);
                }
            }
        }


        private ScintillaEditor? GetActiveEditor()
        {
            var activeWindow = ActiveWindowChecker.GetActiveScintillaWindow();

            /* If currently focused window is *not* owned by PSIDE, return the last active editor */
            if (activeWindow == new IntPtr(-1)) return activeEditor;

            /* If the active window is not a Scintilla editor, return null */
            if (activeWindow == IntPtr.Zero) return null;

            return ScintillaManager.GetEditor(activeWindow);
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
                ScintillaManager.EnableFolding(activeEditor);
                ScintillaManager.FixEditorTabs(activeEditor, !chkBetterSQL.Checked);
                activeEditor.FoldEnabled = true;

                if (chkBetterSQL.Checked && activeEditor.Type == EditorType.SQL)
                {
                    ScintillaManager.ApplyBetterSQL(activeEditor);
                }

                if (!activeEditor.HasLexilla || (activeEditor.Type == EditorType.SQL || activeEditor.Type == EditorType.Other))
                {
                    /* Perform folding ourselves 
                        1. if they are missing Lexilla
                        2. if it is a SQL object 
                        3. if its an editor type we don't know
                    */
                    DoExplicitFolding();
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
                    activeEditor.LastContentHash = ScintillaManager.GetContentHash(activeEditor);
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

                /* Process stylers for PeopleCode */
                if (activeEditor.Type == EditorType.PeopleCode)
                {
                    ProcessStylers(activeEditor);
                }

                if (!activeEditor.HasLexilla || (activeEditor.Type == EditorType.SQL || activeEditor.Type == EditorType.Other))
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
                scanTimer.Change(1000, Timeout.Infinite);
            }
        }

        private async void DoExplicitFolding()
        {
            if (activeEditor == null)
            {
                return;
            }

            // Set the status label and progress bar before starting the background task
            this.Invoke((Action)(() =>
            {
                lblStatus.Text = "Folding...";
                progressBar1.Style = ProgressBarStyle.Marquee;
                progressBar1.MarqueeAnimationSpeed = 30;
            }));
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
            this.Invoke((Action)(() =>
            {
                lblStatus.Text = "Monitoring...";
                progressBar1.Style = ProgressBarStyle.Blocks;
            }));
            Application.DoEvents();

        }
        private void InitStylerOptions() // Changed from InitAnalyzerOptions
        {
            var stylerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(BaseStyler).IsAssignableFrom(p) && !p.IsAbstract);

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
            PeopleCodeLexer lexer = new PeopleCodeLexer(new Antlr4.Runtime.AntlrInputStream(editor.ContentString));
            PeopleCodeParser parser = new PeopleCodeParser(new Antlr4.Runtime.CommonTokenStream(lexer));
            var program = parser.program();

            var activeStylers = stylers.Where(a => a.Active);
            MultiParseTreeWalker walker = new();

            List<CodeAnnotation> annotations = new();
            List<CodeHighlight> highlights = new();
            List<CodeColor> colors = new();

            foreach (var styler in activeStylers)
            {
                styler.Annotations = annotations;
                styler.Highlights = highlights;
                styler.Colors = colors;
                walker.AddListener(styler);
            }

            walker.Walk(program);

            foreach (var styler in activeStylers)
            {
                /* clear out any internal states for the stylers */
                styler.Reset();
            }

            ScintillaManager.ResetStyles(editor);

            foreach (var annotation in annotations)
            {
                ScintillaManager.SetAnnotation(editor, annotation.LineNumber, annotation.Message);
            }

            foreach (var highlight in highlights)
            {
                ScintillaManager.HighlightText(editor, highlight.Color, highlight.Start, highlight.Length);
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

            PeopleCodeLexer? lexer = new PeopleCodeLexer(new Antlr4.Runtime.AntlrInputStream(activeEditor.ContentString));
            var stream = new Antlr4.Runtime.CommonTokenStream(lexer);

            // Get all tokens including those on hidden channels
            stream.Fill();

            // Collect all comments from both comment channels
            var comments = stream.GetTokens()
                .Where(token => token.Channel == PeopleCodeLexer.COMMENTS || token.Channel == PeopleCodeLexer.API_COMMENTS)
                .ToList();

            PeopleCodeParser? parser = new PeopleCodeParser(stream);
            var program = parser.program();
            var activeLinters = linterRules.Where(a => a.Active);

            /* Free up ANTLR resources */
            lexer = null;
            parser = null;
            stream = null;

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

            IDataManager? dataManger = activeEditor.DataManager;
            foreach (var linter in activeLinters)
            {
                linter.DataManager = dataManger;
                linter.Reports = reports;
                linter.Comments = comments;  // Make comments available to each linter
                walker.AddListener(linter);
            }

            walker.Walk(program);

            foreach (var linter in activeLinters)
            {
                linter.Reset();
            }
            program = null;
            foreach (var g in reports.GroupBy(r => r.Line).OrderBy(b => b.First().Line))
            {
                List<string> messages = new();
                List<AnnotationStyle> styles = new();

                foreach (var report in g)
                {
                    this.Invoke((Action)(() =>
                    {
                        int rowIndex = dataGridView2.Rows.Add(report.Type, report.Message, report.Line);
                        dataGridView2.Rows[rowIndex].Tag = report;
                    }));

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

        private async void btnLintCode_Click(object sender, EventArgs e)
        {
            // Set the status label and progress bar before starting the background task
            this.Invoke((Action)(() =>
            {
                lblStatus.Text = "Linting...";
                progressBar1.Style = ProgressBarStyle.Marquee;
                progressBar1.MarqueeAnimationSpeed = 30;
                dataGridView2.Rows.Clear();
            }));
            Application.DoEvents();
            // Run the folding operation in a background thread
            await Task.Run(() =>
            {
                // Ensure the activeEditor is not null before proceeding
                ProcessLinters();
            });

            // Update the UI after the background task completes
            this.Invoke((Action)(() =>
            {
                lblStatus.Text = "Monitoring...";
                progressBar1.Style = ProgressBarStyle.Blocks;
            }));
            Application.DoEvents();

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

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
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
            btnRestoreSnapshot.Enabled = true;
        }

        private void btnRestoreSnapshot_Click(object sender, EventArgs e)
        {
            if (activeEditor == null) return;
            if (activeEditor.SnapshotText == null) return;

            ScintillaManager.ClearAnnotations(activeEditor);
            ScintillaManager.SetScintillaText(activeEditor, activeEditor.SnapshotText);
            activeEditor.SnapshotText = null;
            btnRestoreSnapshot.Enabled = false;

        }

        private void btnAddFlowerBox_Click(object sender, EventArgs e)
        {
            ProcessRefactor(new AddFlowerBox());
        }

        private void ProcessRefactor(BaseRefactor refactorClass, IntPtr owner = 0)
        {
            if (activeEditor == null) return;
            ScintillaManager.ClearAnnotations(activeEditor);

            var freshText = ScintillaManager.GetScintillaText(activeEditor);
            if (freshText == null) return;

            activeEditor.SnapshotText = freshText;
            btnRestoreSnapshot.Enabled = true;

            PeopleCodeLexer lexer = new PeopleCodeLexer(new Antlr4.Runtime.AntlrInputStream(freshText));
            var stream = new Antlr4.Runtime.CommonTokenStream(lexer);
            PeopleCodeParser parser = new PeopleCodeParser(stream);
            var program = parser.program();

            refactorClass.Initialize(freshText, stream);

            ParseTreeWalker walker = new();
            walker.Walk(refactorClass, program);

            var result = refactorClass.GetResult();
            if (!result.Success)
            {
                MessageBox.Show(owner != IntPtr.Zero ? new WindowWrapper(owner) : this, result.Message, "Refactoring Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                btnRestoreSnapshot.Enabled = false;
                return;
            }

            var newText = refactorClass.GetRefactoredCode();
            if (newText == null) return;

            ScintillaManager.SetScintillaText(activeEditor, newText);
        }

        private void btnOptimizeImports_Click(object sender, EventArgs e)
        {

            ProcessRefactor(new OptimizeImports());
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

            DBConnectDialog dialog = new DBConnectDialog();
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

        private void btnRenameLocalVar_Click(object sender, EventArgs e)
        {
            /* Ask the user for a new variable name */
            string newName = "";
            var dlgResult = ShowInputDialog("New variable name", "Enter new variable name", ref newName);

            if (dlgResult != DialogResult.OK) return;

            /* Get the current cursor position */
            int cursorPosition = ScintillaManager.GetCursorPosition(activeEditor);

            /* Create a new instance of the refactoring class */
            RenameLocalVariable refactor = new RenameLocalVariable(cursorPosition, newName);
            ProcessRefactor(refactor);
        }


        private void CmbTemplates_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbTemplates.SelectedItem is Template selectedTemplate)
            {
                GenerateTemplateParameterControls(selectedTemplate);
            }
        }

        private void btnApplyTemplate_Click(object sender, EventArgs e)
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

                if (activeEditor != null)
                {
                    // Take a snapshot of the current content
                    activeEditor.SnapshotText = ScintillaManager.GetScintillaText(activeEditor);
                    btnRestoreSnapshot.Enabled = true;

                    // Set the generated content in the editor
                    ScintillaManager.SetScintillaText(activeEditor, generatedContent);

                    // Handle cursor position or selection range if specified in the template
                    if (selectedTemplate.SelectionStart >= 0 && selectedTemplate.SelectionEnd >= 0)
                    {
                        // Set the selection range
                        ScintillaManager.SetSelection(activeEditor, selectedTemplate.SelectionStart, selectedTemplate.SelectionEnd);
                        WindowHelper.FocusWindow(activeEditor.hWnd);
                    }
                    else if (selectedTemplate.CursorPosition >= 0)
                    {
                        ScintillaManager.SetCursorPosition(activeEditor, selectedTemplate.CursorPosition);
                        WindowHelper.FocusWindow(activeEditor.hWnd);
                    }
                }
                else
                {
                    // If no editor is active, show the content in a dialog
                    ShowGeneratedTemplateDialog(generatedContent, selectedTemplate.TemplateName);
                }
            }
        }

        private void RenameByShortcut()
        {

        }
        private void ShowGeneratedTemplateDialog(string content, string templateName)
        {
            Form dialog = new Form
            {
                Text = $"Generated {templateName}",
                Size = new Size(600, 400),
                StartPosition = FormStartPosition.CenterParent
            };

            TextBox textBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                Text = content,
                WordWrap = false
            };

            Button copyButton = new Button
            {
                Text = "Copy to Clipboard",
                Dock = DockStyle.Bottom
            };

            copyButton.Click += (s, e) =>
            {
                Clipboard.SetText(content);
                MessageBox.Show("Content copied to clipboard!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            dialog.Controls.Add(textBox);
            dialog.Controls.Add(copyButton);

            dialog.ShowDialog();
        }

        private static DialogResult ShowInputDialog(string title, string text, ref string result, IntPtr owner = 0)
        {
            Size size = new Size(300, 70);
            Form inputBox = new Form();

            inputBox.FormBorderStyle = FormBorderStyle.FixedDialog;
            inputBox.ClientSize = size;
            inputBox.Text = title;
            inputBox.StartPosition = FormStartPosition.CenterParent;

            TextBox textBox = new TextBox();
            textBox.Size = new Size(size.Width - 10, 23);
            textBox.Location = new Point(5, 5);
            textBox.Text = text;
            inputBox.Controls.Add(textBox);

            Button okButton = new Button();
            okButton.DialogResult = DialogResult.OK;
            okButton.Name = "okButton";
            okButton.Size = new Size(75, 23);
            okButton.Text = "&OK";
            okButton.Location = new Point(size.Width - 80 - 80, 39);
            inputBox.Controls.Add(okButton);

            Button cancelButton = new Button();
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
                "Editor: Lint Current Code", 
                "Run linting rules against the current editor",
                () => { 
                    if (activeEditor != null) 
                        ProcessLinters(); 
                }
            ));
            
            AvailableCommands.Add(new Command(
                "Editor: Dark Mode", 
                "Apply dark mode to the current editor",
                () => { 
                    if (activeEditor != null) 
                        ScintillaManager.SetDarkMode(activeEditor); 
                }
            ));
            
            AvailableCommands.Add(new Command(
                "Editor: Collapse All", 
                "Collapse all foldable sections",
                () => { 
                    if (activeEditor != null) 
                        ScintillaManager.CollapseTopLevel(activeEditor); 
                }
            ));
            
            AvailableCommands.Add(new Command(
                "Editor: Expand All", 
                "Expand all foldable sections",
                () => { 
                    if (activeEditor != null) 
                        ScintillaManager.ExpandTopLevel(activeEditor); 
                }
            ));
            
            AvailableCommands.Add(new Command(
                "Editor: Take Snapshot", 
                "Take a snapshot of the current editor content",
                () => { 
                    if (activeEditor != null) {
                        activeEditor.SnapshotText = ScintillaManager.GetScintillaText(activeEditor);
                        btnRestoreSnapshot.Enabled = true;
                    }
                }
            ));
            
            AvailableCommands.Add(new Command(
                "Editor: Restore Snapshot", 
                "Restore editor content from the last snapshot",
                () => { 
                    if (activeEditor != null && activeEditor.SnapshotText != null) {
                        ScintillaManager.ClearAnnotations(activeEditor);
                        ScintillaManager.SetScintillaText(activeEditor, activeEditor.SnapshotText);
                        activeEditor.SnapshotText = null;
                        btnRestoreSnapshot.Enabled = false;
                    }
                }
            ));
            
            // Add toggle commands for MainForm checkboxes
            AvailableCommands.Add(new Command(
                "Editor: Toggle Auto Collapse", 
                () => chkInitCollapsed.Checked ? "Auto Collapse is ON - Click to disable" : "Auto Collapse is OFF - Click to enable",
                () => { 
                    chkInitCollapsed.Checked = !chkInitCollapsed.Checked;
                }
            ));
            
            AvailableCommands.Add(new Command(
                "Editor: Toggle Only PeopleCode Editors", 
                () => chkOnlyPPC.Checked ? "Only PeopleCode is ON - Click to disable" : "Only PeopleCode is OFF - Click to enable",
                () => { 
                    chkOnlyPPC.Checked = !chkOnlyPPC.Checked;
                }
            ));
            
            AvailableCommands.Add(new Command(
                "Editor: Toggle Auto Dark Mode", 
                () => chkAutoDark.Checked ? "Auto Dark Mode is ON - Click to disable" : "Auto Dark Mode is OFF - Click to enable",
                () => { 
                    chkAutoDark.Checked = !chkAutoDark.Checked;
                }
            ));
            
            AvailableCommands.Add(new Command(
                "Editor: Toggle Auto Format SQL", 
                () => chkBetterSQL.Checked ? "Auto Format SQL is ON - Click to disable" : "Auto Format SQL is OFF - Click to enable",
                () => { 
                    chkBetterSQL.Checked = !chkBetterSQL.Checked;
                }
            ));
            
            // Add styler toggle commands with "Styler:" prefix
            foreach (var styler in stylers)
            {
                AvailableCommands.Add(new Command(
                    $"Styler: Toggle {styler.Description}",
                    () => styler.Active ? $"Currently enabled - Click to disable" : $"Currently disabled - Click to enable",
                    () => {
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
            AvailableCommands.Add(new Command(
                "Refactor: Add Flower Box", 
                "Add a flower box header to the current file",
                () => { 
                    if (activeEditor != null)
                        ProcessRefactor(new AddFlowerBox());
                }
            ));
            
            AvailableCommands.Add(new Command(
                "Refactor: Optimize Imports", 
                "Clean up and organize import statements",
                () => { 
                    if (activeEditor != null)
                        ProcessRefactor(new OptimizeImports());
                }
            ));
           
        }
    }

    public class WindowWrapper : System.Windows.Forms.IWin32Window
    {
        public WindowWrapper(IntPtr handle)
        {
            _hwnd = handle;
        }

        public IntPtr Handle
        {
            get { return _hwnd; }
        }

        private IntPtr _hwnd;
    }
}
