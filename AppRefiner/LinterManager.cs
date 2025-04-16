using AppRefiner.Database;
using AppRefiner.Database.Models;
using AppRefiner.Dialogs;
using AppRefiner.PeopleCode;
using AppRefiner.Plugins;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using AppRefiner.Linters;

namespace AppRefiner
{
    public class LinterManager
    {
        private readonly List<BaseLintRule> linterRules = new();
        private readonly MainForm mainForm; // Reference to MainForm for UI updates/Invoke
        private readonly DataGridView linterGrid; // DataGridView for linter options
        private readonly DataGridView reportGrid; // DataGridView for lint reports
        private readonly CheckBox chkLintAnnotate; // Checkbox for annotations
        private readonly Label lblStatus; // Status label
        private readonly ProgressBar progressBar; // Progress bar
        
        // We might still need access to the general settings like LintReportPath
        // Pass it during construction or retrieve via a SettingsService later.
        private string? lintReportPath; 

        public LinterManager(MainForm form, DataGridView linterOptionsGrid, DataGridView lintReportGrid, 
                             CheckBox annotateCheckbox, Label statusLabel, ProgressBar progBar, string? initialLintReportPath)
        {
            mainForm = form;
            linterGrid = linterOptionsGrid;
            reportGrid = lintReportGrid;
            chkLintAnnotate = annotateCheckbox;
            lblStatus = statusLabel;
            progressBar = progBar;
            lintReportPath = initialLintReportPath;
        }

        public IEnumerable<BaseLintRule> LinterRules => linterRules;

        public void SetLintReportDirectory()
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
                // Persist this change - ideally via SettingsService, but for now directly
                Properties.Settings.Default.LintReportPath = lintReportPath; 
                Properties.Settings.Default.Save(); 

                MessageBox.Show($"Lint reports will be saved to: {lintReportPath}",
                    "Lint Report Directory Updated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        public void InitializeLinterOptions()
        {
            linterRules.Clear();
            linterGrid.Rows.Clear();
            
            /* Find all classes in this assembly that extend BaseLintRule*/
            var linters = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(BaseLintRule).IsAssignableFrom(p) && !p.IsAbstract);

            // Plugin discovery should remain here or be moved to a central PluginService
            string pluginDirectory = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty,
                Properties.Settings.Default.PluginDirectory); // Settings access

            PluginManager.LoadPlugins(pluginDirectory);

            // Linter config loading should also remain here or be moved
            LinterConfigManager.LoadLinterConfigs();

            // Add plugin linter types
            var pluginLinters = PluginManager.DiscoverLinterTypes();
            linters = linters.Concat(pluginLinters);

            foreach (var type in linters)
            {
                BaseLintRule? linter = (BaseLintRule?)Activator.CreateInstance(type);

                if (linter != null)
                {
                    int rowIndex = linterGrid.Rows.Add(linter.Active, linter.Description);
                    linterGrid.Rows[rowIndex].Tag = linter;

                    var configurableProperties = linter.GetConfigurableProperties();
                    DataGridViewButtonCell buttonCell = (DataGridViewButtonCell)linterGrid.Rows[rowIndex].Cells[2];

                    if (configurableProperties.Count > 0)
                    {
                        buttonCell.Value = "Configure...";
                        linterGrid.Rows[rowIndex].Cells[2].Tag = null;
                    }
                    else
                    {
                        buttonCell.Value = " ";
                        buttonCell.FlatStyle = FlatStyle.Flat;
                        buttonCell.Style.BackColor = SystemColors.Control;
                        // ... other styling ...
                        linterGrid.Rows[rowIndex].Cells[2].Tag = "NoConfig";
                    }
                    linterRules.Add(linter);
                }
            }
            // Apply saved configurations to linters
            LinterConfigManager.ApplyConfigurations(linterRules);
            
             // Now load saved active states (assuming direct access for now)
            LoadLinterStatesFromSettings();
        }
        
        // Method to load active states (could be part of SettingsService later)
        private void LoadLinterStatesFromSettings()
        {
             try
            {
                 var states = System.Text.Json.JsonSerializer.Deserialize<List<MainForm.RuleState>>(
                    Properties.Settings.Default.LinterStates);

                if (states == null) return;
                
                var ruleMap = linterRules.ToDictionary(l => l.GetType().FullName ?? "");

                foreach (var state in states)
                {
                    if (ruleMap.TryGetValue(state.TypeName, out var linter))
                    {
                        linter.Active = state.Active;
                        // Update corresponding grid row
                        var row = linterGrid.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Tag is BaseLintRule l && l == linter);
                        if (row != null)
                        {
                             // Check if the row and cell exist before accessing
                            if (row.Index >= 0 && row.Cells.Count > 0)
                            {
                                row.Cells[0].Value = state.Active;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { 
                Debug.LogException(ex, "Error loading linter states in LinterManager");
                /* Use defaults if settings are corrupt */ 
            }
        }

        public void ProcessLintersForActiveEditor(ScintillaEditor? activeEditor, IDataManager? editorDataManager)
        {
            if (activeEditor == null) return;

            ScintillaManager.ClearAnnotations(activeEditor);

            if (activeEditor.Type != EditorType.PeopleCode)
            {
                MessageBox.Show("Linting is only available for PeopleCode editors", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (activeEditor.ContentString == null)
            {
                activeEditor.ContentString = ScintillaManager.GetScintillaText(activeEditor);
            }

            var (program, stream, comments) = activeEditor.GetParsedProgram();
            var activeLinters = linterRules.Where(a => a.Active);

            if (editorDataManager == null)
            {
                activeLinters = activeLinters.Where(a => a.DatabaseRequirement != DataManagerRequirement.Required);
            }

            MultiParseTreeWalker walker = new();
            List<Report> reports = new();
            
            var suppressionListener = new LinterSuppressionListener(stream, comments);
            walker.AddListener(suppressionListener);

            foreach (var linter in activeLinters)
            {
                linter.DataManager = editorDataManager; // Assign potentially updated manager
                linter.Reports = reports;
                linter.Comments = comments;
                linter.SuppressionListener = suppressionListener;
                walker.AddListener(linter);
            }

            walker.Walk(program);

            foreach (var linter in activeLinters)
            {
                linter.Reset();
            }

            activeEditor.SetLinterReports(reports);
            DisplayLintReports(reports, activeEditor);
        }

        public void ProcessSingleLinter(BaseLintRule linter, ScintillaEditor? activeEditor, IDataManager? editorDataManager)
        {
             if (activeEditor == null) return;

            ScintillaManager.ClearAnnotations(activeEditor);
            reportGrid.Rows.Clear(); // Clear previous single reports

            if (activeEditor.Type != EditorType.PeopleCode)
            {
                MessageBox.Show("Linting is only available for PeopleCode editors", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            if (linter.DatabaseRequirement == DataManagerRequirement.Required && editorDataManager == null)
            {
                MessageBox.Show("This linting rule requires a database connection", "Database Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            if (activeEditor.ContentString == null)
            {
                activeEditor.ContentString = ScintillaManager.GetScintillaText(activeEditor);
            }
            
            var (program, stream, comments) = activeEditor.GetParsedProgram();
            MultiParseTreeWalker walker = new();
            List<Report> reports = new();
            var suppressionListener = new LinterSuppressionListener(stream, comments);
            walker.AddListener(suppressionListener);

            // Configure and run the specific linter
            linter.DataManager = editorDataManager;
            linter.Reports = reports;
            linter.Comments = comments;
            linter.SuppressionListener = suppressionListener;
            walker.AddListener(linter);
            
            walker.Walk(program);
            linter.Reset();

            activeEditor.SetLinterReports(reports);
            DisplayLintReports(reports, activeEditor);
        }

        private void DisplayLintReports(List<Report> reports, ScintillaEditor activeEditor)
        {
            mainForm.Invoke(() => reportGrid.Rows.Clear());
            
            foreach (var g in reports.GroupBy(r => r.Line).OrderBy(b => b.First().Line))
            {
                List<string> messages = new();
                List<AnnotationStyle> styles = new();

                foreach (var report in g)
                {
                    mainForm.Invoke(() => {
                         int rowIndex = reportGrid.Rows.Add(report.Type, report.Message, report.Line);
                         reportGrid.Rows[rowIndex].Tag = report;
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
        }

        public void ClearLintResults(ScintillaEditor? activeEditor)
        {
            if (activeEditor == null) return;
            ScintillaManager.ClearAnnotations(activeEditor);
            reportGrid.Rows.Clear();
        }

        // --- Project Linting --- 
        // Note: This requires significant state/dependencies (DB manager, active editor for context)
        public void LintProject(ScintillaEditor editorContext, CommandProgressDialog? progressDialog)
        {
            if (editorContext?.DataManager == null)
            {
                MessageBox.Show("Database connection required for project linting.", "Database Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            IDataManager dataManager = editorContext.DataManager;

            string projectName = ScintillaManager.GetProjectName(editorContext);
            if (projectName == "Untitled" || string.IsNullOrEmpty(projectName))
            {
                 MessageBox.Show(string.IsNullOrEmpty(projectName) ? "Unable to determine the project name." : "Please open a project first.", 
                                "Project Linting Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (string.IsNullOrEmpty(lintReportPath) || !Directory.Exists(lintReportPath))
            {
                 MessageBox.Show($"Lint report directory is not set or does not exist: {lintReportPath}", 
                                "Lint Report Path Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportFileName = $"{projectName}_LintReport_{timestamp}.html";
            string reportPath = Path.Combine(lintReportPath, reportFileName);

            var ppcProgsMeta = dataManager.GetPeopleCodeItemMetadataForProject(projectName);
            var activeProjectLinters = linterRules.Where(a => a.Active).ToList();

            UpdateStatus($"Linting project - found {ppcProgsMeta.Count} items...", true);

            List<(PeopleCodeItem Program, Report LintReport)> allReports = new();
            int processedCount = 0;

            foreach (var ppcProg in ppcProgsMeta)
            {
                processedCount++;
                if (processedCount % 10 == 0)
                {
                    UpdateStatus($"Linting project - processed {processedCount} of {ppcProgsMeta.Count} items...", true);
                    progressDialog?.UpdateHeader($"Linting project - processed {processedCount} of {ppcProgsMeta.Count} items...");
                    GC.Collect();
                }

                if (!dataManager.LoadPeopleCodeItemContent(ppcProg)) continue;
                var programText = ppcProg.GetProgramTextAsString();
                if (string.IsNullOrEmpty(programText)) continue;
                
                // Parsing logic...
                PeopleCodeLexer? lexer = new(new AntlrInputStream(programText));
                var stream = new CommonTokenStream(lexer);
                stream.Fill();
                var comments = stream.GetTokens()
                    .Where(token => token.Channel == PeopleCodeLexer.COMMENTS || token.Channel == PeopleCodeLexer.API_COMMENTS)
                    .ToList();
                PeopleCodeParser? parser = new(stream);
                var program = parser.program();
                parser.Interpreter.ClearDFA();

                List<Report> programReports = new();
                MultiParseTreeWalker walker = new();
                var suppressionListener = new LinterSuppressionListener(stream, comments);
                walker.AddListener(suppressionListener);

                foreach (var linter in activeProjectLinters)
                {
                    linter.Reset();
                    linter.DataManager = dataManager;
                    linter.Reports = programReports;
                    linter.Comments = comments;
                    linter.SuppressionListener = suppressionListener;
                    walker.AddListener(linter);
                }

                walker.Walk(program);

                foreach (var report in programReports) allReports.Add((ppcProg, report));
                foreach (var linter in activeProjectLinters) linter.Reset();
                
                ppcProg.SetProgramText(Array.Empty<byte>()); // Free memory
                ppcProg.SetNameReferences(new List<NameReference>());
            }

            UpdateStatus("Finalizing Report...", true);
            progressDialog?.UpdateHeader("Finalizing Report...");

            GenerateHtmlReport(reportPath, projectName, allReports);

            FinalizeProjectLinting(reportPath, allReports.Count);
        }
        
        private void UpdateStatus(string message, bool marquee)
        {
             mainForm.Invoke(() => {
                  lblStatus.Text = message;
                  progressBar.Style = marquee ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
                  if(marquee) progressBar.MarqueeAnimationSpeed = 30;
             });
        }
        
        private void FinalizeProjectLinting(string reportPath, int issueCount)
        {
             mainForm.Invoke(() => {
                  lblStatus.Text = "Monitoring...";
                  progressBar.Style = ProgressBarStyle.Blocks;
                  
                  string message = issueCount > 0
                    ? $"Project linting complete. {issueCount} issues found.\n\nWould you like to open the report?"
                    : "Project linting complete. No issues found.\n\nWould you like to open the report?";

                var result = MessageBox.Show(message, "Project Linting Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                {
                    try {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = reportPath, UseShellExecute = true });
                    } catch (Exception ex) {
                        MessageBox.Show($"Error opening report: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
             });
        }
        
        private void GenerateHtmlReport(string reportPath, string projectName,
            List<(PeopleCodeItem Program, Report LintReport)> reportData)
        {
            try
            {
                var groupedReports = reportData
                    .GroupBy(r => r.Program.BuildPath())
                    .OrderBy(g => g.Key)
                    .ToList();

                int totalErrors = reportData.Count(r => r.LintReport.Type == ReportType.Error);
                int totalWarnings = reportData.Count(r => r.LintReport.Type == ReportType.Warning);
                int totalInfo = reportData.Count(r => r.LintReport.Type == ReportType.Info);

                var activeLinterInfo = linterRules // Use the manager's list
                    .Where(l => l.Active)
                    .Select(l => new { name = l.GetType().Name, description = l.Description })
                    .ToList();

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
                        peopleCodeType = pg.First().Program.Type.ToString(),
                        reports = pg.Select(item => new
                        {
                            type = item.LintReport.Type.ToString(),
                            line = item.LintReport.Line + 1,
                            message = item.LintReport.Message
                        }).OrderBy(r => r.line).ToList()
                    }).ToList()
                };

                string reportJson = System.Text.Json.JsonSerializer.Serialize(report);
                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "LintReportTemplate.html");
                string templateHtml;

                if (File.Exists(templatePath))
                {
                    templateHtml = File.ReadAllText(templatePath);
                }
                else
                {
                    using (Stream? stream = GetType().Assembly.GetManifestResourceStream("AppRefiner.Templates.LintReportTemplate.html"))
                    {
                        if (stream != null) { using (var reader = new StreamReader(stream)) { templateHtml = reader.ReadToEnd(); } }
                        else { MessageBox.Show("Lint report template not found.", "Template Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    }
                }

                string finalHtml = templateHtml.Replace("{{projectName}}", projectName)
                                             .Replace("{{timestamp}}", DateTime.Now.ToString());
                finalHtml = finalHtml.Replace("</head>", $"<script>const reportJSON = {reportJson};</script>\n</head>");

                File.WriteAllText(reportPath, finalHtml);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating report: {ex.Message}", "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- Grid Event Handlers (to be called from MainForm) ---

        public void HandleLinterGridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            linterGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            if (e.ColumnIndex == 2 && e.RowIndex >= 0)
            {
                if (linterGrid.Rows[e.RowIndex].Tag is BaseLintRule linter)
                {
                    if (linterGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag?.ToString() != "NoConfig")
                    {
                         // Need to show dialog relative to the main form
                        using (var dialog = new LinterConfigDialog(linter))
                        {
                            dialog.ShowDialog(mainForm); // Show dialog owned by MainForm
                        }
                    }
                }
            }
        }

        public void HandleLinterGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0)
            {
                if (linterGrid.Rows[e.RowIndex].Tag is BaseLintRule linter)
                {
                    linter.Active = (bool)linterGrid.Rows[e.RowIndex].Cells[0].Value;
                    // Persist change (optional, could be done on form close)
                    SaveLinterStatesToSettings(); 
                }
            }
        }
        
        // Method to save active states (could be part of SettingsService later)
        public void SaveLinterStatesToSettings()
        {
            try
            {
                 var states = linterRules.Select(l => new MainForm.RuleState // Use MainForm's RuleState for now
                {
                    TypeName = l.GetType().FullName ?? "",
                    Active = l.Active
                }).ToList();

                Properties.Settings.Default.LinterStates =
                    System.Text.Json.JsonSerializer.Serialize(states);
                // Consider calling Properties.Settings.Default.Save() here or centrally
            }
             catch (Exception ex)
            {
                Debug.LogException(ex, "Error saving linter states");
            }
        }

        public void HandleReportGridCellClick(object sender, DataGridViewCellEventArgs e, ScintillaEditor? activeEditor)
        {
            if (activeEditor == null || reportGrid.SelectedRows.Count == 0) return;
            
            if (reportGrid.SelectedRows[0].Tag is Report report)
            {
                ScintillaManager.SetSelection(activeEditor, report.Span.Start, report.Span.Stop);
                WindowHelper.FocusWindow(activeEditor.hWnd);
            }
        }
    }
} 