using AppRefiner.Database;
using AppRefiner.Events;
using DiffPlex.Model;
using PeopleCodeParser.SelfHosted;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner
{
    public class NullTypeMetadataResolver : ITypeMetadataResolver
    {

        protected override TypeMetadata? GetTypeMetadataCore(string qualifiedName)
        {
            return null;
        }

        protected override Task<TypeMetadata?> GetTypeMetadataCoreAsync(string qualifiedName)
        {
            return Task.FromResult<TypeMetadata?>(null);
        }

        protected override TypeInfo GetFieldTypeCore(string fieldName)
        {
            // For testing purposes, return AnyTypeInfo
            return AnyTypeInfo.Instance;
        }

        protected override Task<TypeInfo> GetFieldTypeCoreAsync(string fieldName)
        {
            return Task.FromResult<TypeInfo>(AnyTypeInfo.Instance);
        }

        protected override List<string> GetClassesInPackageCore(string packagePath)
        {
            return new List<string>();
        }
    }

    public class AppDesignerProcess
    {
        public static IntPtr CallbackWindow;
        public Dictionary<IntPtr, ScintillaEditor> Editors = [];

        public uint ProcessId = 0;
        public IntPtr ProcessHandle = IntPtr.Zero;
        public bool HasLexilla { get; set; }

        /// <summary>
        /// Directory the enhanced Scintilla builds are deployed to. Trailing separator so
        /// prefix checks can't match sibling directories that merely start with the name.
        /// </summary>
        public static readonly string ScintillaModsDirectory = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
            "scintilla_mods") + System.IO.Path.DirectorySeparatorChar;

        /// <summary>
        /// True when the Scintilla DLL loaded in this process (Scintilla.dll on Tools 8.61+,
        /// SciLexer.dll on 8.60 and earlier) came from AppRefiner's scintilla_mods directory —
        /// i.e. the enhanced editor replacement actually took. Note HasLexilla is NOT this:
        /// Lexilla.dll only exists on Scintilla 5.x installs, so it indicates the Tools era,
        /// not the mod. Re-evaluated per call because the hook swaps the DLL asynchronously
        /// after process registration.
        /// </summary>
        public bool IsEnhancedEditorLoaded()
        {
            try
            {
                using var process = Process.GetProcessById((int)ProcessId);
                foreach (ProcessModule module in process.Modules)
                {
                    if (string.Equals(module.ModuleName, "Scintilla.dll", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(module.ModuleName, "SciLexer.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        if (module.FileName.StartsWith(ScintillaModsDirectory, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"IsEnhancedEditorLoaded: could not inspect modules for process {ProcessId}: {ex.Message}");
            }
            return false;
        }
        public uint MainThreadId { get; private set; }
        public IntPtr MainWindowHandle { get; set; }
        public Dictionary<string, RemoteBuffer> iconBuffers = new();

        /// <summary>
        /// Memory manager for allocating and managing buffers in the Application Designer process
        /// </summary>
        public MemoryManager MemoryManager { get; private set; }
        /// <summary>
        /// Handle to the Results ListView for this Application Designer process, if known.
        /// </summary>
        public IntPtr ResultsListView { get; set; }
        public IDataManager? DataManager { get; set; }

        /// <summary>
        /// Type metadata cache shared across all editors in this AppDesigner process.
        /// Used by type inference and type checking systems.
        /// </summary>

        private ITypeMetadataResolver? _typeResolver;
        private IDataManager? _typeResolverDataManager;

        /// <summary>
        /// Type metadata resolver that retrieves type information from the database.
        /// Initialized lazily when DataManager is available and cached — the resolver holds
        /// metadata caches that must survive across accesses. Recreated only when the
        /// DataManager instance changes (connect/disconnect/reconnect).
        /// </summary>
        public ITypeMetadataResolver? TypeResolver
        {
            get
            {
                if (DataManager != null)
                {
                    if (_typeResolver is not DatabaseTypeMetadataResolver || !ReferenceEquals(_typeResolverDataManager, DataManager))
                    {
                        _typeResolver = new DatabaseTypeMetadataResolver(DataManager);
                        _typeResolverDataManager = DataManager;
                    }
                }
                else
                {
                    // Use the shared Contracts null resolver (always-misses) so compile
                    // checks that require a live DB can detect "no connection" via
                    // CompileChecker's NullTypeMetadataResolver normalization. Do not
                    // invent a local null-resolver type that would bypass that guard.
                    if (_typeResolver is not PeopleCodeTypeInfo.Contracts.NullTypeMetadataResolver)
                    {
                        _typeResolver = PeopleCodeTypeInfo.Contracts.NullTypeMetadataResolver.Instance;
                        _typeResolverDataManager = null;
                    }
                }

                return _typeResolver;
            }
        }

        public GeneralSettingsData Settings {  get; set; }

        public bool DoNotPromptForDB { get; set; }
        public string DBName { get; internal set; }

        /// <summary>
        /// Human-readable description of the active DB connection (e.g. "Bootstrap as SYSADM").
        /// Stamped on successful connect, cleared on disconnect. Null when not connected.
        /// </summary>
        public string? ConnectionDescription { get; set; }

        /// <summary>
        /// PeopleTools version captured once at connect time (never re-queried). Null when
        /// not connected.
        /// </summary>
        public string? ToolsVersion { get; set; }

        public SourceSpan? PendingSelection { get; set; }

        /// <summary>
        /// Navigation history for F12 Go To Definition navigation
        /// </summary>
        public List<NavigationHistoryEntry> NavigationHistory { get; set; } = new List<NavigationHistoryEntry>();

        /// <summary>
        /// Current position in the navigation history stack
        /// </summary>
        public int NavigationHistoryIndex { get; set; } = -1;

        /// <summary>
        /// Updates the Results ListView handle for this process
        /// </summary>
        /// <param name="resultsListView">Handle to the Results ListView</param>
        public void SetResultsListView(IntPtr resultsListView)
        {
            ResultsListView = resultsListView;
        }

        /// <summary>
        /// Reads raw bytes from an arbitrary address in this App Designer process.
        /// Use for pointers handed to us by the hook (e.g. notification payloads) — this
        /// performs a plain read with no ownership semantics, unlike RemoteBuffer which
        /// frees its memory and must only wrap allocations AppRefiner made itself.
        /// </summary>
        /// <param name="address">Address in the remote process</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>The bytes read, or null on failure</returns>
        public byte[]? ReadMemory(IntPtr address, int length)
        {
            if (address == IntPtr.Zero || length <= 0)
            {
                return null;
            }

            byte[] buffer = new byte[length];
            if (!WinApi.ReadProcessMemory(ProcessHandle, address, buffer, length, out int bytesRead) || bytesRead != length)
            {
                return null;
            }

            return buffer;
        }

        public AppDesignerProcess(uint pid, IntPtr resultsListView, GeneralSettingsData settings, EventHookInstaller.ShortcutType enabledShortcuts = EventHookInstaller.ShortcutType.All)
        {
            ProcessId = pid;
            ResultsListView = resultsListView;
            Settings = settings;
            ProcessHandle = WinApi.OpenProcess(WinApi.PROCESS_VM_READ | WinApi.PROCESS_VM_WRITE | WinApi.PROCESS_VM_OPERATION, false, ProcessId);

            // Initialize memory manager for this process
            MemoryManager = new MemoryManager(this);

            // Check if any module in the process has the name "Lexilla.dll"
            Process process = Process.GetProcessById((int)ProcessId);
            var lexillaLoaded = process.Modules
                    .Cast<ProcessModule>()
                    .Any(module => string.Equals(module.ModuleName, "Lexilla.dll", StringComparison.OrdinalIgnoreCase));
            HasLexilla = lexillaLoaded;

            MainWindowHandle = process.MainWindowHandle;

            // The UI thread is the one that owns the main window — Process.Threads has no
            // guaranteed ordering, so Threads[0] may be a worker thread and hooks installed
            // on it would never see any messages
            if (MainWindowHandle != IntPtr.Zero)
            {
                MainThreadId = WinApi.GetWindowThreadProcessId(MainWindowHandle, out _);
            }
            else
            {
                // Fallback for the rare case the main window isn't available yet
                MainThreadId = (uint)process.Threads[0].Id;
                Debug.Log($"AppDesignerProcess: no main window for process {ProcessId}, falling back to first thread {MainThreadId}");
            }

            // Proactively install hooks for this process's main thread
            // This ensures hooks are available immediately for operations like SetOpenTarget
            bool hookInstalled = Events.EventHookInstaller.InstallHook(MainThreadId);
            if (!hookInstalled)
            {
                Debug.Log($"Warning: Failed to install hooks for AppDesigner process {ProcessId}, thread {MainThreadId}");
            }
            // Always enable Command Palette, use the provided shortcuts
            var shortcuts = EventHookInstaller.EnsureCommandPaletteEnabled(enabledShortcuts);
            EventHookInstaller.SubclassMainWindow(this, CallbackWindow, shortcuts);

            EventHookInstaller.SubclassResultsList(MainThreadId, ResultsListView, IntPtr.Zero);
        }

        private const int ICON_BASE = 90;

        public enum AutoCompleteIcons : int
        {
            ClassMethod = ICON_BASE,
            SystemVariable,
            LocalVariable,
            InstanceVariable,
            ComponentVariable,
            GlobalVariable,
            Parameter,
            Property,
            ExternalFunction,
            ConstantValue,
            Field
        }


        /// <summary>
        /// Finds Scintilla editor windows that already exist in this process and runs the
        /// normal editor initialization for each. Covers App Designer instances that were
        /// already running (with editors open) before AppRefiner started — those editors
        /// never produce a creation event, and would otherwise stay un-subclassed until
        /// first focused. Matches by exact window-class prefix so the hook's minimap
        /// windows (class AppRefinerMinimap) are not picked up.
        /// </summary>
        public void InitExistingEditors()
        {
            if (MainWindowHandle == IntPtr.Zero)
            {
                return;
            }

            var found = new List<IntPtr>();
            WinApi.EnumChildWindows(MainWindowHandle, (hwnd, _) =>
            {
                StringBuilder className = new(256);
                WinApi.GetClassName(hwnd, className, className.Capacity);
                if (className.ToString().StartsWith("Scintilla", StringComparison.Ordinal))
                {
                    found.Add(hwnd);
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            int initialized = 0;
            foreach (var hwnd in found)
            {
                if (Editors.ContainsKey(hwnd))
                {
                    continue; // Already tracked (e.g. the focused editor that triggered registration)
                }

                Debug.Log($"InitExistingEditors: found pre-existing Scintilla editor 0x{hwnd.ToInt64():X}");
                InitEditor(hwnd);
                initialized++;
            }

            if (initialized > 0)
            {
                Debug.Log($"InitExistingEditors: initialized {initialized} pre-existing editor(s) in process {ProcessId}");
            }
        }

        public ScintillaEditor GetOrInitEditor(IntPtr hwnd)
        {
            if (Editors.TryGetValue(hwnd, out ScintillaEditor? editor))
            {
                Debug.Log($"GetOrInitEditor: cache hit for hwnd 0x{hwnd.ToInt64():X} (caption '{editor.Caption}', Initialized={editor.Initialized}) — skipping init");
                return editor;
            }
            else
            {
                return InitEditor(hwnd);
            }
        }
        public ScintillaEditor InitEditor(IntPtr hWnd)
        {
            var caption = WindowHelper.GetGrandparentWindowCaption(hWnd);
            if (caption == "Suppress")
            {
                Thread.Sleep(1000);
                caption = WindowHelper.GetGrandparentWindowCaption(hWnd);
            }
            // Get the process ID associated with the Scintilla window.
            var threadId = WinApi.GetWindowThreadProcessId(hWnd, out uint processId);


            var editor = new ScintillaEditor(this, hWnd, caption);
            editor.DataManager = DataManager;
            editor.AppDesignerProcess = this;
            Editors.Add(hWnd, editor);

            Debug.Log($"InitEditor: full init for hwnd 0x{hWnd.ToInt64():X} (caption '{caption}', type {editor.Type}, thread {threadId}, parent 0x{WindowHelper.GetParentWindow(hWnd).ToInt64():X})");

            ScintillaManager.SetAutoCompleteIcons(editor);
            ScintillaManager.FixEditorTabs(editor);
            if (Settings.OnlyPPC && editor.Type != EditorType.PeopleCode)
            {
                /* Skip the rest of the editor initialization */
                Debug.Log($"InitEditor: OnlyPPC set and editor type is {editor.Type} — skipping subclass/minimap/param-names init for hwnd 0x{hWnd.ToInt64():X}");
                return editor;
            }

            if (Settings.AutoDark)
            {
                ScintillaManager.SetDarkMode(editor);
            }

            /* This subclasses the parent window for scintilla notifications *and* the scintilla editor itself */
            EventHookInstaller.SubclassScintillaParentWindow(threadId, WindowHelper.GetParentWindow(hWnd), CallbackWindow, MainWindowHandle, Settings.AutoPair);


            ScintillaManager.SetMouseDwellTime(editor, 1000);

            if (Settings.CodeFolding)
            {
                ScintillaManager.EnableFolding(editor);
                editor.ContentString = ScintillaManager.GetScintillaText(editor);

                if (Settings.RememberFolds)
                {
                    editor.CollapsedFoldPaths = FoldingManager.RetrievePersistedFolds(editor);
                }

                FoldingManager.ProcessFolding(editor);
                FoldingManager.ApplyCollapsedFoldPaths(editor);
            }

            if (Settings.BetterSQL && editor.Type == EditorType.SQL)
            {
                ScintillaManager.ApplyBetterSQL(editor);
            }

            if (Settings.InitCollapsed)
            {
                ScintillaManager.CollapseTopLevel(editor);
            }

            ScintillaManager.InitAnnotationStyles(editor);

            // Apply persisted editor-feature states now that subclassing is complete.
            // Both messages are posted to the same thread as the subclass message, so they
            // will be processed after ComboBoxButton::Setup has run.
            EventHookInstaller.SetMinimap(editor, Properties.Settings.Default.miniMapOpen);
            EventHookInstaller.SetParamNames(editor, Properties.Settings.Default.showParamNames);

            /* Disable default Ctrl+/ and Ctrl+\ */
            // For Ctrl+/
            int keyDef_CtrlSlash = '/' + (2 << 16);
            editor.SendMessage(2071, (IntPtr)keyDef_CtrlSlash, (IntPtr)0); // SCI_CLEARCMDKEY = 2071

            // For Ctrl+\
            int keyDef_CtrlBackslash = '\\' + (2 << 16);
            editor.SendMessage(2071, (IntPtr)keyDef_CtrlBackslash, (IntPtr)0);

            editor.Initialized = true;
            return editor;
        }

        /// <summary>
        /// Optionally call this method to clean up persistent resources when your application exits.
        /// </summary>
        public void Cleanup()
        {
            foreach (var editor in Editors.Values)
            {
                editor.Cleanup();
            }

            // Clean up all managed memory buffers
            MemoryManager?.Cleanup();

            WinApi.CloseHandle(ProcessHandle);
        }

        /// <summary>
        /// Sets the open target string for Results list interception and triggers double-click
        /// </summary>
        /// <param name="openTarget">Target string to open (max 255 chars)</param>
        /// <returns>True if operation was successful</returns>
        public bool SetOpenTarget(string openTarget)
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);


            // Constants from EventHookInstaller
            const uint WM_USER = 0x400;
            const uint WM_AR_SET_OPEN_TARGET = WM_USER + 1008;

            if (!Events.EventHookInstaller.HasActiveHook(MainThreadId))
            {
                return false;
            }

            if (ResultsListView == IntPtr.Zero)
            {
                return false; // No Results list view available
            }

            if (string.IsNullOrEmpty(openTarget) || openTarget.Length >= 256)
            {
                return false; // Exceed buffer size limit
            }

            try
            {
                // Allocate buffer in target process for the wide string
                int charCount = openTarget.Length;
                uint bufferSize = (uint)(charCount + 1) * 2; // +1 for null terminator, *2 for wide chars

                bool setTargetSuccess;

                // Shared buffer: hold the lock for the full write → send sequence
                lock (MemoryManager.SyncRoot)
                {
                    var remoteBuffer = MemoryManager.GetOrCreateBuffer("openTarget", bufferSize);
                    remoteBuffer.Reset();
                    Debug.Log($"SetOpenTarget Got remote buffer: 0x{remoteBuffer.Address:X}");
                    if (remoteBuffer.Address == IntPtr.Zero)
                    {
                        Debug.Log($"SetOpenTarget Remote Buffer was 0");
                        return false;
                    }

                    // Write the wide string to the remote buffer
                    if (remoteBuffer.WriteString(openTarget, Encoding.Unicode) == null)
                    {
                        Debug.Log("SetOpenTarget Failed to write target string to remote buffer");
                        return false;
                    }

                    // Send the set open target message with the remote buffer pointer and character count
                    Debug.Log($"SetOpenTarget Sending the message with buffer: 0x{remoteBuffer.Address:X}");
                    setTargetSuccess = SendMessage(MainWindowHandle, (int)WM_AR_SET_OPEN_TARGET, remoteBuffer.Address, charCount) != IntPtr.Zero;
                }
                Debug.Log($"SetOpenTarget setTargetSuccess: {setTargetSuccess}");
                if (setTargetSuccess)
                {
                    // Send synthetic double-click to trigger IDE behavior
                    const int WM_LBUTTONDBLCLK = 0x0203;
                    const int MK_LBUTTON = 0x0001;
                    IntPtr lParam = IntPtr.Zero; // MAKELONG(0, 0) - coordinates (0,0)
                    Debug.Log("SetOpenTarget Sending Double Click.");
                    bool doubleClickSuccess = SendMessage(ResultsListView, WM_LBUTTONDBLCLK, MK_LBUTTON, lParam) != IntPtr.Zero;
                    Debug.Log($"SetOpenTarget Double Click Success: {doubleClickSuccess}");
                    return doubleClickSuccess;
                }
                return false;

            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Loads the Scintilla.dll into the Application Designer process.
        /// </summary>
        /// <param name="dllPath">Full path to the Scintilla.dll file</param>
        /// <returns>True if the message was sent successfully</returns>
        public bool LoadScintillaDll(string dllPath)
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

            const uint WM_USER = 0x400;
            const uint WM_LOAD_SCINTILLA_DLL = WM_USER + 1009;

            // Validate parameters
            if (!Events.EventHookInstaller.HasActiveHook(MainThreadId))
            {
                Debug.Log("LoadScintillaDll: No active hook for this thread");
                return false;
            }

            if (string.IsNullOrEmpty(dllPath))
            {
                Debug.Log("LoadScintillaDll: DLL path is null or empty");
                return false;
            }

            if (dllPath.Length > 512)
            {
                Debug.Log($"LoadScintillaDll: DLL path too long ({dllPath.Length} chars, max 512)");
                return false;
            }

            try
            {
                int charCount = dllPath.Length;
                uint bufferSize = (uint)(charCount + 1) * 2; // Wide chars

                // Shared buffer: hold the lock for the write → post sequence. Note the message is
                // POSTED (async) — the remote thread reads the buffer later, so this buffer must
                // not be rewritten until then. In practice LoadScintillaDll runs once per process.
                lock (MemoryManager.SyncRoot)
                {
                    var remoteBuffer = MemoryManager.GetOrCreateBuffer("scintillaDLL", bufferSize);
                    Debug.Log($"LoadScintillaDll: Allocated remote buffer at 0x{remoteBuffer.Address:X}");

                    if (remoteBuffer.Address == IntPtr.Zero)
                    {
                        Debug.Log("LoadScintillaDll: Failed to allocate remote buffer");
                        return false;
                    }

                    remoteBuffer.Reset();
                    if (remoteBuffer.WriteString(dllPath, Encoding.Unicode) == null)
                    {
                        Debug.Log("LoadScintillaDll: Failed to write DLL path to remote buffer");
                        return false;
                    }

                    Debug.Log($"LoadScintillaDll: Sending message to thread {MainThreadId}");
                    bool postSuccess = PostThreadMessage(MainThreadId, WM_LOAD_SCINTILLA_DLL,
                                                        remoteBuffer.Address, (IntPtr)charCount);

                    Debug.Log($"LoadScintillaDll: PostThreadMessage returned {postSuccess}");
                    return postSuccess;
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"LoadScintillaDll: Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates the enabled shortcuts for this AppDesigner process.
        /// Note: Command Palette shortcut is always enabled and cannot be disabled.
        /// </summary>
        /// <param name="enabledShortcuts">The shortcuts to enable</param>
        /// <returns>True if the update was successful</returns>
        public bool UpdateShortcuts(EventHookInstaller.ShortcutType enabledShortcuts)
        {
            // Always ensure Command Palette is enabled
            var shortcuts = EventHookInstaller.EnsureCommandPaletteEnabled(enabledShortcuts);
            return EventHookInstaller.SetMainWindowShortcuts(MainWindowHandle, shortcuts);
        }

        /// <summary>
        /// Pushes a new location onto the navigation history stack.
        /// Prunes any forward history entries after the current index.
        /// </summary>
        /// <param name="entry">The navigation history entry to add</param>
        public void PushNavigationLocation(NavigationHistoryEntry entry)
        {
            // Remove any forward history (everything after current index)
            if (NavigationHistoryIndex >= 0 && NavigationHistoryIndex < NavigationHistory.Count)
            {
                NavigationHistory.RemoveRange(NavigationHistoryIndex, NavigationHistory.Count - NavigationHistoryIndex);
            }

            // Add the new entry
            NavigationHistory.Add(entry);
            // Set index to Count (one past the end) to indicate we're at a new location beyond the stack
            NavigationHistoryIndex = NavigationHistory.Count;

            Debug.Log($"Pushed navigation location. Stack size: {NavigationHistory.Count}, Index: {NavigationHistoryIndex}");
        }

        /// <summary>
        /// Checks if navigation backward is possible
        /// </summary>
        /// <returns>True if there are entries to navigate back to</returns>
        public bool CanNavigateBackward()
        {
            return NavigationHistoryIndex > 0;
        }

        /// <summary>
        /// Checks if navigation forward is possible
        /// </summary>
        /// <returns>True if there are entries to navigate forward to</returns>
        public bool CanNavigateForward()
        {
            return NavigationHistoryIndex >= 0 && NavigationHistoryIndex < NavigationHistory.Count - 1;
        }

        /// <summary>
        /// Navigates backward in the navigation history
        /// </summary>
        /// <returns>The navigation entry to navigate to, or null if cannot navigate backward</returns>
        public NavigationHistoryEntry? NavigateBackward()
        {
            if (!CanNavigateBackward())
                return null;

            NavigationHistoryIndex--;
            Debug.Log($"Navigate backward to index {NavigationHistoryIndex}");
            return NavigationHistory[NavigationHistoryIndex];
        }

        /// <summary>
        /// Navigates forward in the navigation history
        /// </summary>
        /// <returns>The navigation entry to navigate to, or null if cannot navigate forward</returns>
        public NavigationHistoryEntry? NavigateForward()
        {
            if (!CanNavigateForward())
                return null;

            NavigationHistoryIndex++;
            Debug.Log($"Navigate forward to index {NavigationHistoryIndex}");
            return NavigationHistory[NavigationHistoryIndex];
        }

    }
}
