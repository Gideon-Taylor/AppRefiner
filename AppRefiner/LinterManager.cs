using AppRefiner.Database;
using AppRefiner.Database.Models;
using AppRefiner.Dialogs;
using AppRefiner.Linters;
using AppRefiner.Plugins;
using System.Diagnostics;

namespace AppRefiner
{
    public class LinterManager
    {
        private readonly List<BaseLintRule> linterRules = new();
        private readonly MainForm mainForm; // Reference to MainForm for UI updates/Invoke
        private readonly DataGridView linterGrid; // DataGridView for linter options
        private readonly Label lblStatus; // Status label
        private readonly ProgressBar progressBar; // Progress bar
        private readonly SettingsService settingsService; // Added SettingsService

        // We might still need access to the general settings like LintReportPath
        // Pass it during construction or retrieve via a SettingsService later.
        private string? lintReportPath;

        // Public property to expose LintReportPath
        public string? LintReportPath => lintReportPath;

        public LinterManager(MainForm form, DataGridView linterOptionsGrid, Label statusLabel, ProgressBar progBar,
                             string? initialLintReportPath, SettingsService settings)
        {
            mainForm = form;
            linterGrid = linterOptionsGrid;
            lblStatus = statusLabel;
            progressBar = progBar;
            lintReportPath = initialLintReportPath;
            settingsService = settings; // Store SettingsService
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
                    MessageBoxButtons.OK);
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
                        buttonCell.Value = string.Empty;
                        buttonCell.ReadOnly = true;
                        buttonCell.FlatStyle = FlatStyle.Flat;
                        buttonCell.Style.BackColor = SystemColors.Control;
                        buttonCell.Style.ForeColor = SystemColors.Control;
                        buttonCell.Style.SelectionBackColor = SystemColors.Control;
                        buttonCell.Style.SelectionForeColor = SystemColors.Control;
                        linterGrid.Rows[rowIndex].Cells[2].Tag = "NoConfig";
                    }
                    linterRules.Add(linter);
                }
            }
            // Apply saved configurations to linters
            LinterConfigManager.ApplyConfigurations(linterRules);

            // Now load saved active states using SettingsService
            settingsService.LoadLinterStates(linterRules, linterGrid);
        }

        private void ShowMessageBox(ScintillaEditor activeEditor, string message, string caption, MessageBoxButtons buttons, Action<DialogResult>? callback = null)
        {
            Task.Delay(100).ContinueWith(_ =>
            {
                // Show message box with specific error
                var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                var handleWrapper = new WindowWrapper(mainHandle);
                new MessageBoxDialog(message, caption, buttons, mainHandle, callback).ShowDialog(handleWrapper);
            });
        }

        public void ProcessLintersForActiveEditor(ScintillaEditor? activeEditor, IDataManager? editorDataManager)
        {
            if (activeEditor == null) return;

            ScintillaManager.ClearAnnotations(activeEditor);

            if (activeEditor.Type != EditorType.PeopleCode)
            {
                ShowMessageBox(activeEditor, "Linting is only available for PeopleCode editors", "Error", MessageBoxButtons.OK);
                return;
            }

            if (activeEditor.ContentString == null)
            {
                activeEditor.ContentString = ScintillaManager.GetScintillaText(activeEditor);
            }

            // Use self-hosted parser approach
            ProcessLintersWithSelfHostedParser(activeEditor.ContentString, activeEditor, editorDataManager);
        }

        /// <summary>
        /// Processes linters using the self-hosted parser approach
        /// </summary>
        private void ProcessLintersWithSelfHostedParser(string sourceCode, ScintillaEditor activeEditor, IDataManager? editorDataManager)
        {
            try
            {
                // Parse with self-hosted parser
                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(sourceCode);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var program = parser.ParseProgram();

                if (program == null)
                {
                    ShowMessageBox(activeEditor, "Failed to parse PeopleCode. Please check for syntax errors.", "Parse Error", MessageBoxButtons.OK);
                    return;
                }

                // Get active linters
                var activeLinters = linterRules.Where(a => a.Active).ToList();

                if (editorDataManager == null)
                {
                    activeLinters = activeLinters.Where(a => a.DatabaseRequirement != DataManagerRequirement.Required).ToList();
                }

                if (!activeLinters.Any())
                {
                    return;
                }

                // Create suppression processor
                var suppressionProcessor = new LinterSuppressionProcessor();

                // Process the program with suppression processor first
                suppressionProcessor.DataManager = editorDataManager;
                program.Accept(suppressionProcessor);

                // Process each linter
                List<Report> reports = new();
                foreach (var linter in activeLinters)
                {
                    linter.Reset();
                    linter.DataManager = editorDataManager;
                    linter.Reports = reports;
                    linter.SuppressionProcessor = suppressionProcessor;

                    // Visit the program with this linter
                    program.Accept(linter);
                }

                activeEditor.SetLinterReports(reports);
                DisplayLintReports(reports, activeEditor);
            }
            catch (Exception ex)
            {
                ShowMessageBox(activeEditor, $"Error during linting: {ex.Message}", "Linting Error", MessageBoxButtons.OK);
            }
        }

        public void ProcessSingleLinter(BaseLintRule linter, ScintillaEditor? activeEditor, IDataManager? editorDataManager)
        {
            if (activeEditor == null) return;

            ScintillaManager.ClearAnnotations(activeEditor);

            if (activeEditor.Type != EditorType.PeopleCode)
            {
                ShowMessageBox(activeEditor, "Linting is only available for PeopleCode editors", "Error", MessageBoxButtons.OK);
                return;
            }

            if (linter.DatabaseRequirement == DataManagerRequirement.Required && editorDataManager == null)
            {
                ShowMessageBox(activeEditor, "This linting rule requires a database connection", "Database Required", MessageBoxButtons.OK);
                return;
            }

            if (activeEditor.ContentString == null)
            {
                activeEditor.ContentString = ScintillaManager.GetScintillaText(activeEditor);
            }

            try
            {
                // Parse with self-hosted parser
                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(activeEditor.ContentString);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var program = parser.ParseProgram();

                if (program == null)
                {
                    ShowMessageBox(activeEditor, "Failed to parse PeopleCode. Please check for syntax errors.", "Parse Error", MessageBoxButtons.OK);
                    return;
                }

                // Create suppression processor
                var suppressionProcessor = new LinterSuppressionProcessor();

                // Process the program with suppression processor first
                suppressionProcessor.DataManager = editorDataManager;
                program.Accept(suppressionProcessor);

                // Configure and run the specific linter
                linter.Reset();
                linter.DataManager = editorDataManager;
                List<Report> reports = new();
                linter.Reports = reports;
                linter.SuppressionProcessor = suppressionProcessor;

                // Visit the program with this linter
                program.Accept(linter);

                activeEditor.SetLinterReports(reports);
                DisplayLintReports(reports, activeEditor);
            }
            catch (Exception ex)
            {
                ShowMessageBox(activeEditor, $"Error during linting: {ex.Message}", "Linting Error", MessageBoxButtons.OK);
            }
        }

        private void DisplayLintReports(List<Report> reports, ScintillaEditor activeEditor)
        {

            foreach (var g in reports.GroupBy(r => r.Line).OrderBy(b => b.First().Line))
            {
                List<string> messages = new();
                List<AnnotationStyle> styles = new();

                foreach (var report in g)
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

                ScintillaManager.SetAnnotations(activeEditor, messages, g.First().Line, styles);
            }
        }

        public void ClearLintResults(ScintillaEditor? activeEditor)
        {
            if (activeEditor == null) return;
            ScintillaManager.ClearAnnotations(activeEditor);
        }

        // --- Project Linting with Callback Support ---
        public void LintProject(ScintillaEditor editorContext,
            Action<string>? updateHeader = null,
            Action<string>? updateStatus = null,
            Action<int, int>? updateProgress = null,
            Func<bool>? shouldCancel = null)
        {
            if (editorContext?.DataManager == null)
            {
                ShowMessageBox(editorContext, "Database connection required for project linting.", "Database Required", MessageBoxButtons.OK);
                return;
            }
            IDataManager dataManager = editorContext.DataManager;

            string projectName = ScintillaManager.GetProjectName(editorContext);
            if (projectName == "Untitled" || string.IsNullOrEmpty(projectName))
            {
                ShowMessageBox(editorContext, string.IsNullOrEmpty(projectName) ? "Unable to determine the project name." : "Please open a project first.",
                               "Project Linting Error", MessageBoxButtons.OK);
                return;
            }

            if (string.IsNullOrEmpty(lintReportPath) || !Directory.Exists(lintReportPath))
            {
                ShowMessageBox(editorContext, $"Lint report directory is not set or does not exist: {lintReportPath}",
                               "Lint Report Path Error", MessageBoxButtons.OK);
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportFileName = $"{projectName}_LintReport_{timestamp}.html";
            string reportPath = Path.Combine(lintReportPath, reportFileName);

            var ppcProgsMeta = dataManager.GetPeopleCodeItemMetadataForProject(projectName);
            var activeProjectLinters = linterRules.Where(a => a.Active).ToList();

            updateStatus?.Invoke($"Found {ppcProgsMeta.Count} items to lint...");
            updateHeader?.Invoke("Linting Project");

            List<(PeopleCodeItem Program, Report LintReport)> allReports = new();
            int processedCount = 0;

            foreach (var ppcProg in ppcProgsMeta)
            {
                // Check for cancellation
                if (shouldCancel?.Invoke() == true)
                {
                    return;
                }

                processedCount++;
                if (processedCount % 10 == 0 || processedCount == 1)
                {
                    updateStatus?.Invoke($"Processing {processedCount} of {ppcProgsMeta.Count} items...");
                    updateProgress?.Invoke(processedCount, ppcProgsMeta.Count);
                    GC.Collect();
                }

                if (!dataManager.LoadPeopleCodeItemContent(ppcProg)) continue;
                var programText = ppcProg.GetProgramTextAsString();
                if (string.IsNullOrEmpty(programText)) continue;

                // Parsing logic using self-hosted parser
                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(programText);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var program = parser.ParseProgram();

                if (program == null) continue; // Skip if parsing failed

                List<Report> programReports = new();

                // Create suppression processor for self-hosted parser
                var suppressionProcessor = new LinterSuppressionProcessor();
                suppressionProcessor.DataManager = dataManager;
                program.Accept(suppressionProcessor);

                foreach (var linter in activeProjectLinters)
                {
                    linter.Reset();
                    linter.DataManager = dataManager;
                    linter.Reports = programReports;
                    linter.SuppressionProcessor = suppressionProcessor;

                    // Visit the program with this linter
                    program.Accept(linter);
                }

                foreach (var report in programReports) allReports.Add((ppcProg, report));
                foreach (var linter in activeProjectLinters) linter.Reset();

                ppcProg.SetProgramText(Array.Empty<byte>()); // Free memory
                ppcProg.SetNameReferences(new List<NameReference>());
            }

            // Check for cancellation before finalizing
            if (shouldCancel?.Invoke() == true)
            {
                return;
            }

            updateHeader?.Invoke("Finalizing Report");
            updateStatus?.Invoke("Generating HTML report...");

            GenerateHtmlReport(editorContext, reportPath, projectName, allReports);

            updateStatus?.Invoke("Complete!");

            // Show completion message
            FinalizeProjectLinting(editorContext, reportPath, allReports.Count);
        }

        private void FinalizeProjectLinting(ScintillaEditor editorContext, string reportPath, int issueCount)
        {
            mainForm.Invoke(() =>
            {
                string message = issueCount > 0
                  ? $"Project linting complete. {issueCount} issues found.\n\nWould you like to open the report?"
                  : "Project linting complete. No issues found.\n\nWould you like to open the report?";

                ShowMessageBox(editorContext, message, "Project Linting Complete", MessageBoxButtons.YesNo, (result) =>
                {
                    if (result == DialogResult.Yes)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo { FileName = reportPath, UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            ShowMessageBox(editorContext, $"Error opening report: {ex.Message}", "Error", MessageBoxButtons.OK);
                        }
                    }
                });
            });
        }

        public void GenerateHtmlReport(ScintillaEditor editorContext, string reportPath, string projectName,
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
                    using Stream? stream = GetType().Assembly.GetManifestResourceStream("AppRefiner.Templates.LintReportTemplate.html");
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        templateHtml = reader.ReadToEnd();
                    }
                    else { ShowMessageBox(editorContext, "Lint report template not found.", "Template Missing", MessageBoxButtons.OK); return; }
                }

                string finalHtml = templateHtml.Replace("{{projectName}}", projectName)
                                             .Replace("{{timestamp}}", DateTime.Now.ToString());
                finalHtml = finalHtml.Replace("</head>", $"<script>const reportJSON = {reportJson};</script>\n</head>");

                File.WriteAllText(reportPath, finalHtml);
            }
            catch (Exception ex)
            {
                ShowMessageBox(editorContext, $"Error creating report: {ex.Message}", "Report Error", MessageBoxButtons.OK);
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
                        using var dialog = new LinterConfigDialog(linter);
                        dialog.ShowDialog(mainForm); // Show dialog owned by MainForm
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
                    // Settings are now saved centrally on form close
                }
            }
        }
    }
}