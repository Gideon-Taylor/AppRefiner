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

        private System.Timers.Timer tmrScan = new System.Timers.Timer();

        private object scanningLock = new();
        private bool scanningFlag = false;
        private ScintillaEditor? activeEditor = null;
        private List<BaseLintRule> linterRules = new();
        private List<BaseStyler> stylers = new(); // Changed from List<BaseStyler> analyzers

        public MainForm()
        {
            InitializeComponent();

            tmrScan.Interval = 1000;
            tmrScan.Elapsed += (sender, e) => PerformScan();
            tmrScan.Enabled = false;

            InitLinterOptions();
            InitStylerOptions(); // Changed from InitAnalyzerOptions
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            tmrScan.Enabled = !tmrScan.Enabled;
            btnStart.Text = tmrScan.Enabled ? "Stop" : "Start";
            lblStatus.Text = tmrScan.Enabled ? "Monitoring..." : "Ready";

            activeEditor = null;
            if (tmrScan.Enabled == false)
            {
                grpEditorActions.Enabled = tmrScan.Enabled;
                grpRefactorImports.Enabled = tmrScan.Enabled;
                grpRefactorVariables.Enabled = tmrScan.Enabled;
                btnLintCode.Enabled = tmrScan.Enabled;
            }
        }

        private void EnableUIActions()
        {
            this.Invoke((Action)(() =>
            {
                grpEditorActions.Enabled = true;
                btnLintCode.Enabled = true;
                grpRefactorImports.Enabled = true;
                grpRefactorVariables.Enabled = true;

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
            var topWindow = WindowHelper.GetCurrentlyFocusedWindow();
            GetWindowThreadProcessId(topWindow, out uint procId);

            /* get the process name */
            var procName = Process.GetProcessById((int)procId).ProcessName;
            if (procName != "pside")
            {
                return null;
            }

            var className = new StringBuilder(256);
            ScintillaEditor? editor = null;
            EnumChildWindows(topWindow, (hWnd, lParam) =>
            {
                if (GetClassName(hWnd, className, className.Capacity) != 0)
                {
                    if (className.ToString() == "Scintilla")
                    {

                        editor = ScintillaManager.GetEditor(hWnd);
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            className.Clear();
            className = null;

            return editor;
        }

        private void PerformScan()
        {
            lock (scanningLock)
            {
                if (scanningFlag)
                {
                    return;
                }
                scanningFlag = true;
            }

            var currentEditor = GetActiveEditor();
            if (currentEditor == null)
            {
                lock (scanningLock)
                {
                    scanningFlag = false;
                }
                return;
            }

            if (activeEditor == null)
            {
                EnableUIActions();
            }
            else if (activeEditor.hWnd != currentEditor.hWnd)
            {
                dataGridView2.Rows.Clear();
            }

            activeEditor = currentEditor;

            /* If "only PPC" is checked and the editor is not PPC, skip */
            if (chkOnlyPPC.Checked && activeEditor.Type != EditorType.PeopleCode)
            {
                lock (scanningLock)
                {
                    scanningFlag = false;
                }
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
                    ScintillaManager.ContractTopLevel(activeEditor);
                }


                lock (scanningLock)
                {
                    scanningFlag = false;
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
                    lock (scanningLock)
                    {
                        scanningFlag = false;
                    }
                    return;
                }


                /* compare content hash to see if things have changed */
                var contentHash = ScintillaManager.GetContentHash(activeEditor);
                if (contentHash == activeEditor.LastContentHash)
                {
                    lock (scanningLock)
                    {
                        scanningFlag = false;
                    }
                    return;
                }

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

                /* Create parse tree */
                if (activeEditor.Type == EditorType.PeopleCode)
                {
                    PeopleCodeLexer lexer = new PeopleCodeLexer(new Antlr4.Runtime.AntlrInputStream(activeEditor.ContentString));
                    var stream = new Antlr4.Runtime.CommonTokenStream(lexer);
                    PeopleCodeParser parser = new PeopleCodeParser(new Antlr4.Runtime.CommonTokenStream(lexer));
                    var program = parser.program();


                    ProcessStylers(program);
                }

                activeEditor.LastContentHash = contentHash;
            }

            lock (scanningLock)
            {
                scanningFlag = false;
            }
        }

        private async void DoExplicitFolding()
        {
            if (activeEditor == null)
            {
                return;
            }

            if (!activeEditor.HasLexilla)
            {
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
        private void ProcessStylers(ProgramContext program) // Changed from ProcessAnalyzers
        {
            if (activeEditor == null || activeEditor.Type != EditorType.PeopleCode)
            {
                return;
            }

            var activeStylers = stylers.Where(a => a.Active); // Changed from activeAnalyzers
            MultiParseTreeWalker walker = new();

            List<CodeAnnotation> annotations = new();
            List<CodeHighlight> highlights = new();
            List<CodeColor> colors = new();

            foreach (var styler in activeStylers) // Changed from analyzer
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

            foreach (var annotation in annotations)
            {
                ScintillaManager.SetAnnotation(activeEditor, annotation.LineNumber, annotation.Message);
            }

            foreach (var highlight in highlights)
            {
                ScintillaManager.HighlightText(activeEditor, highlight.Color, highlight.Start, highlight.Length);
            }

            foreach (var color in colors)
            {
                ScintillaManager.ColorText(activeEditor, color.Color, color.Start, color.Length);
            }

            walker.Walk(program);
        }

        private void ProcessLinters()
        {
            if (activeEditor == null || activeEditor.Type != EditorType.PeopleCode)
            {
                MessageBox.Show("Linting is only available for PeopleCode editors", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            dataGridView2.Rows.Clear();
            if (activeEditor.ContentString == null)
            {
                activeEditor.ContentString = ScintillaManager.GetScintillaText(activeEditor);
            }

            PeopleCodeLexer lexer = new PeopleCodeLexer(new Antlr4.Runtime.AntlrInputStream(activeEditor.ContentString));
            var stream = new Antlr4.Runtime.CommonTokenStream(lexer);
            PeopleCodeParser parser = new PeopleCodeParser(new Antlr4.Runtime.CommonTokenStream(lexer));
            var program = parser.program();

            var activeLinters = linterRules.Where(a => a.Active);
            MultiParseTreeWalker walker = new();
            List<Report> reports = new();
            foreach (var linter in activeLinters)
            {
                linter.Reports = reports;
                walker.AddListener(linter);
            }
            walker.Walk(program);
            foreach (var linter in activeLinters)
            {
                linter.Reset();
            }

            /* For each report, add a row to datagridview2 which has columns Level, Message, Line Number */
            /* TODO: Work out the styles for each character so we can report multiple issues on a single line */
            foreach (var g in reports.GroupBy(r => r.Line))
            {
                List<string> messages = new();
                List<AnnotationStyle> styles = new();

                foreach (var report in g)
                {
                    int rowIndex = dataGridView2.Rows.Add(report.Type, report.Message, report.Line);
                    dataGridView2.Rows[rowIndex].Tag = report;
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
                /* SetAnnotations*/


                /* StringBuilder sb = new();
                
                

                if (chkLintAnnotate.Checked)
                {
                    AnnotationStyle style = report.Type switch
                    {
                        ReportType.Error => AnnotationStyle.Red,
                        ReportType.Warning => AnnotationStyle.Yellow,
                        ReportType.Info => AnnotationStyle.Gray,
                        _ => AnnotationStyle.Gray
                    };
                    ScintillaManager.SetAnnotation(activeEditor, report.Line, sb.ToString().Trim(), style);
                } */
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
            BaseStyler styler = (BaseStyler)dataGridView3.Rows[e.RowIndex].Tag;
            styler.Active = (bool)dataGridView3.Rows[e.RowIndex].Cells[0].Value;
        }

        private void btnLintCode_Click(object sender, EventArgs e)
        {
            ProcessLinters();
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
            BaseLintRule linter = (BaseLintRule)dataGridView1.Rows[e.RowIndex].Tag;
            linter.Active = (bool)dataGridView1.Rows[e.RowIndex].Cells[0].Value;
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }


        private void dataGridView2_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            /* Get the tag (Report ) from the selected row */
            /* instruct Scintilla to select the line and send focus to main window */
            if (dataGridView2.SelectedRows.Count == 0)
            {
                return;
            }
            Report report = (Report)dataGridView2.SelectedRows[0].Tag;
            if (report == null)
            {
                return;
            }
            ScintillaManager.SetSelection(activeEditor, report.Span.Start, report.Span.Stop);
            WindowHelper.FocusWindow(activeEditor.hWnd);
        }

        private void btnClearLint_Click(object sender, EventArgs e)
        {
            ScintillaManager.ClearAnnotations(activeEditor);
            dataGridView2.Rows.Clear();
        }
    }
}
