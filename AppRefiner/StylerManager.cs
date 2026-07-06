using AppRefiner.Database;
using AppRefiner.Plugins;
using PeopleCodeParser.SelfHosted.Visitors; // For settings serialization and TypeInferenceVisitor
using PeopleCodeTypeInfo.Inference; // For TypeMetadataBuilder
using System.Collections.Concurrent;
using System.Reflection; // Added for Assembly.GetExecutingAssembly
using PeopleCodeParser.SelfHosted.Nodes; // For ProgramNode

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Manages the discovery, configuration, and execution of stylers.
    /// </summary>
    public class StylerManager
    {
        private readonly List<BaseStyler> stylers = new();
        private readonly DataGridView stylerGrid; // DataGridView for styler options
        private readonly MainForm mainForm; // Needed for Invoke potentially, though aiming to minimize direct use
        private readonly SettingsService settingsService; // Added SettingsService

        /// <summary>
        /// Pending styler work per editor. New requests for the same editor replace older ones.
        /// </summary>
        private readonly ConcurrentDictionary<ScintillaEditor, StylerWorkItem> _pendingWork = new();

        /// <summary>
        /// 0 = idle, 1 = background consumer is running.
        /// </summary>
        private int _isProcessing = 0;

        /// <summary>
        /// Encapsulates the state captured at request time for async styler processing.
        /// </summary>
        private record StylerWorkItem(
            ScintillaEditor Editor,
            ProgramNode Program,
            int ContentVersion,
            IDataManager? DataManager);

        public StylerManager(MainForm form, DataGridView stylerOptionsGrid, SettingsService settings)
        {
            mainForm = form; // Store reference if needed for Invoke
            stylerGrid = stylerOptionsGrid;
            settingsService = settings; // Store SettingsService
        }

        public IEnumerable<BaseStyler> StylerRules => stylers;

        /// <summary>
        /// Discovers stylers, populates the grid, and loads saved states.
        /// </summary>
        public void InitializeStylerOptions()
        {
            stylers.Clear();
            stylerGrid.Rows.Clear();

            // Discover core stylers from the main assembly
            var executingAssembly = Assembly.GetExecutingAssembly();
            var coreStylerTypes = executingAssembly.GetTypes()
                .Where(p => typeof(BaseStyler).IsAssignableFrom(p) && !p.IsAbstract && !p.IsInterface
                    && p.Name != "ScopedStyler" && p.Name != "BaseStyler");

            // Discover stylers from plugins
            var pluginStylers = PluginManager.DiscoverStylerTypes();

            // Combine core and plugin stylers, ensuring uniqueness
            var allStylerTypes = coreStylerTypes.Concat(pluginStylers).Distinct();

            foreach (var type in allStylerTypes)
            {
                try
                {
                    // Create instance - all stylers now inherit from ScopedStyler which implements IStyler
                    if (Activator.CreateInstance(type) is BaseStyler styler)
                    {
                        /* We don't want to add this styler to the grid as its controlled by editor tweak setting/editor toggle */
                        if (type != typeof(FunctionParameterNames))
                        {
                            int rowIndex = stylerGrid.Rows.Add(styler.Active, styler.Description);
                            stylerGrid.Rows[rowIndex].Tag = styler;
                        }
                        
                        stylers.Add(styler);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"Failed to instantiate styler: {type.FullName}");
                }
            }

            // Load saved active states using SettingsService
            settingsService.LoadStylerStates(stylers, stylerGrid);
        }

        public void ClearMemberCache()
        {
            foreach (var styler in stylers)
            {
                if (styler is InvalidMemberAccess invalidMemberAccessStyler)
                {
                    invalidMemberAccessStyler.ClearMemberCache();
                }
            }
        }

        public void ClearMemberCacheForClass(string appClassPath)         {
            foreach (var styler in stylers)
            {
                if (styler is InvalidMemberAccess invalidMemberAccessStyler)
                {
                    invalidMemberAccessStyler.ClearMemberCacheForClass(appClassPath);
                }
            }
        }

        /// <summary>
        /// Processes active stylers for the given editor.
        /// Parsing and type inference run synchronously (fast, cached).
        /// Styler execution runs on a background thread; results are verified
        /// against a content hash before being applied.
        /// </summary>
        /// <param name="editor">The Scintilla editor to process.</param>
        public void ProcessStylersForEditor(ScintillaEditor? editor)
        {
            if (editor == null || !editor.IsValid() || editor.Type != EditorType.PeopleCode)
            {
                return; // Only process valid PeopleCode editors
            }

            // Parse synchronously (fast, cached)
            var program = editor.GetParsedProgram();
            if (program == null)
            {
                return; // Unable to parse
            }

            // Capture the content version for the staleness check after async processing —
            // the version increments on every WM_AR_DOC_MODIFIED from the hook
            int contentVersion = editor.ContentVersion;

            // Enqueue work — replaces any pending request for the same editor
            _pendingWork[editor] = new StylerWorkItem(editor, program, contentVersion, editor.DataManager);

            // Start the background consumer if not already running
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
            {
                Task.Run(DrainPendingWork);
            }
        }

        /// <summary>
        /// Background consumer that processes pending styler work items sequentially.
        /// Serialization is required because styler instances are shared across editors.
        /// </summary>
        private void DrainPendingWork()
        {
            try
            {
                while (!_pendingWork.IsEmpty)
                {
                    foreach (var key in _pendingWork.Keys.ToList())
                    {
                        if (_pendingWork.TryRemove(key, out var item))
                        {
                            ProcessStylerWorkItem(item);
                        }
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);

                // Re-check: work may have been enqueued while we were resetting the flag
                if (!_pendingWork.IsEmpty && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
                {
                    Task.Run(DrainPendingWork);
                }
            }
        }

        /// <summary>
        /// Runs all active stylers for a single work item and applies indicators
        /// only if the editor content has not changed since the request was captured.
        /// </summary>
        private void ProcessStylerWorkItem(StylerWorkItem item)
        {
            var editor = item.Editor;

            if (!editor.IsValid())
                return;

            // Run type inference on the background thread — this may hit the database
            // for AppClass resolution and should not block the caller
            RunTypeInferenceForProgram(item.Program, editor);

            var activeStylers = stylers.Where(a => a.Active
                && (a.DatabaseRequirement != DataManagerRequirement.Required || item.DataManager != null)
                && a.GetType() != typeof(BaseStyler));

            List<Indicator> newIndicators = new();

            foreach (var styler in activeStylers)
            {
                try
                {
                    styler.Reset();
                    styler.DataManager = item.DataManager;
                    styler.Editor = editor;

                    item.Program.Accept((IAstVisitor)styler);

                    newIndicators.AddRange(styler.Indicators);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"Error processing styler {styler.GetType().Name}");
                }
            }

            // Verify content hasn't changed before applying indicators. The version counter
            // (bumped per WM_AR_DOC_MODIFIED) replaces the previous full document re-read +
            // hash comparison — one less cross-process copy per styler pass.
            if (!editor.IsValid())
                return;

            if (editor.ContentVersion != item.ContentVersion)
            {
                Debug.Log($"ProcessStylerWorkItem: Content changed during processing for {editor.RelativePath ?? "unknown"}, discarding results");
                return;
            }

            ApplyIndicators(editor, newIndicators);
        }

        /// <summary>
        /// Applies the new set of indicators to the editor, removing old ones and adding new ones efficiently.
        /// </summary>
        /// <param name="editor">The editor to apply indicators to.</param>
        /// <param name="newIndicators">The list of indicators generated by the stylers.</param>
        private void ApplyIndicators(ScintillaEditor editor, List<Indicator> newIndicators)
        {
            if (editor == null || editor.ActiveIndicators == null)
                return;

            // Take a snapshot of current indicators under lock to ensure thread safety
            List<Indicator> currentIndicators;
            lock (editor.IndicatorLock)
            {
                currentIndicators = new List<Indicator>(editor.ActiveIndicators);
            }

            Debug.Log($"ApplyIndicators: Editor {editor.RelativePath ?? "unknown"} - " +
                     $"Current: {currentIndicators.Count}, New: {newIndicators.Count}");

            // Do NOT diff old vs new and remove by recorded ranges: Scintilla shifts painted
            // indicator ranges as the user edits, while our recorded Start/Length do not move.
            // Removing by stale recorded range clears the wrong spot and leaves lingering
            // paint (only a save/force-refresh used to fix it). Instead, wipe the full
            // document range for every indicator number involved and repaint the fresh set —
            // a handful of messages per distinct indicator color, and deterministic.
            ScintillaManager.ClearIndicatorNumbers(
                editor,
                currentIndicators
                    .Concat(newIndicators)
                    .Concat(editor.SearchIndicators)
                    .Concat(editor.BookmarkIndicators));

            lock (editor.IndicatorLock)
            {
                editor.ActiveIndicators.Clear();
            }

            // Add new indicators (AddIndicator repopulates editor.ActiveIndicators)
            foreach (var indicator in newIndicators)
            {
                AddIndicator(editor, indicator);
            }

            /* Re-add any active Better Find markers */
            foreach (var indicator in editor.SearchIndicators)
            {
                AddIndicator(editor, indicator);
            }

            /* Re-add any active bookmark indicators */
            foreach (var indicator in editor.BookmarkIndicators)
            {
                AddIndicator(editor, indicator);
            }
        }

        public void RemoveIndicator(ScintillaEditor editor, Indicator indicator)
        {
            ScintillaManager.RemoveIndicator(editor, indicator);
        }

        public void AddIndicator(ScintillaEditor editor, Indicator indicator)
        {
            ScintillaManager.AddIndicator(editor, indicator);
        }


        // --- Grid Event Handlers (to be called from MainForm) ---

        /// <summary>
        /// Handles clicks within the styler options grid (e.g., checkbox changes).
        /// To be called by the MainForm's event handler.
        /// </summary>
        public void HandleStylerGridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Commit any pending edits, especially for checkboxes
            stylerGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);

            // Future: If configuration buttons are added, handle them here.
            // if (e.ColumnIndex == CONFIG_BUTTON_COLUMN_INDEX && e.RowIndex >= 0)
            // {
            //     if (stylerGrid.Rows[e.RowIndex].Tag is BaseStyler styler)
            //     {
            //         // Show configuration dialog for the styler
            //     }
            // }
        }

        /// <summary>
        /// Handles changes to cell values in the styler options grid, specifically the 'Active' checkbox.
        /// To be called by the MainForm's event handler.
        /// </summary>
        public void HandleStylerGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0) // Assuming column 0 is the 'Active' checkbox
            {
                if (stylerGrid.Rows[e.RowIndex].Tag is BaseStyler styler)
                {
                    // Ensure the value is a boolean before casting
                    if (stylerGrid.Rows[e.RowIndex].Cells[0].Value is bool isActive)
                    {
                        styler.Active = isActive;
                        // Settings are now saved centrally on form close

                        // Optional: Trigger immediate re-styling of the active editor if needed
                        // This might require access to the active editor, passed in or retrieved.
                        // ProcessStylersForEditor(MainForm.ActiveEditor, ...);
                    }
                    else
                    {
                        Debug.Log($"Unexpected value type in StylerGrid Active column: {stylerGrid.Rows[e.RowIndex].Cells[0].Value?.GetType()}");
                    }
                }
            }
        }

        /// <summary>
        /// Runs type inference on the given program if a TypeResolver is available.
        /// This ensures type information is populated on AST nodes before stylers execute.
        /// </summary>
        /// <param name="program">The parsed program AST</param>
        /// <param name="editor">The editor containing the program</param>
        private void RunTypeInferenceForProgram(ProgramNode program, ScintillaEditor editor)
        {
            try
            {
                // Get the AppDesigner process and TypeResolver
                var appDesignerProcess = editor?.AppDesignerProcess;
                if (appDesignerProcess == null)
                {
                    Debug.Log("StylerManager: No AppDesigner process available for type inference");
                    return;
                }

                var typeResolver = appDesignerProcess.TypeResolver;
                if (typeResolver == null)
                {
                    Debug.Log("StylerManager: TypeResolver is null (database not connected?), skipping type inference");
                    return;
                }

                // Determine qualified name for the program
                string qualifiedName = DetermineQualifiedName(program, editor);

                // Extract metadata from the program
                var programMetadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);

                // Determine default record/field for record PeopleCode
                string? defaultRecord = null;
                string? defaultField = null;
                if (editor.Caption?.EndsWith("(Record PeopleCode)") == true)
                {
                    var parts = qualifiedName.Split('.');
                    if (parts.Length >= 2)
                    {
                        defaultRecord = parts[0];
                        defaultField = parts[1];
                    }
                }

                // Run type inference - this populates node.Attributes["TypeInfo"] throughout the AST
                TypeInferenceVisitor.Run(
                    program,
                    programMetadata,
                    typeResolver,
                    defaultRecord,
                    defaultField,
                    inferAutoDeclaredTypes: false,
                    onUndefinedVariable: mainForm.TypeExtensionManager != null ? mainForm.TypeExtensionManager.HandleUndefinedVariable : null);

                Debug.Log($"StylerManager: Type inference completed for '{qualifiedName}'");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "StylerManager: Error during type inference");
                // Don't fail the entire styler pipeline if type inference fails
            }
        }

        /// <summary>
        /// Determines the qualified name for the current program.
        /// Extracted from TypeErrorStyler for reuse.
        /// </summary>
        /// <param name="node">The program AST node</param>
        /// <param name="editor">The editor containing the program</param>
        /// <returns>The qualified name of the program</returns>
        private string DetermineQualifiedName(ProgramNode node, ScintillaEditor editor)
        {
            // Try to extract from AST structure first
            if (node.AppClass != null)
            {
                var className = node.AppClass.Name;

                if (editor?.Caption != null && !string.IsNullOrWhiteSpace(editor.Caption))
                {
                    var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
                    if (openTarget != null)
                    {
                        var methodIndex = Array.IndexOf(openTarget.ObjectIDs, PSCLASSID.METHOD);
                        openTarget.ObjectIDs[methodIndex] = PSCLASSID.NONE;
                        openTarget.ObjectValues[methodIndex] = null;
                        return openTarget.Path;
                    }
                    else
                    {
                        return className;
                    }
                }
                else
                {
                    return className;
                }
            }
            else
            {
                // For function libraries or other programs
                if (editor?.Caption != null && !string.IsNullOrWhiteSpace(editor.Caption))
                {
                    var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
                    if (openTarget != null)
                    {
                        return string.Join(".", openTarget.ObjectValues);
                    }
                }

                return "Program";
            }
        }
    }
}