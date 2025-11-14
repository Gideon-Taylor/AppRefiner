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
    }

    public class AppDesignerProcess
    {
        public static IntPtr CallbackWindow;
        public Dictionary<IntPtr, ScintillaEditor> Editors = [];
        private (IntPtr address, uint size) processBuffer = (0, 0);
        public IntPtr ProcessBuffer { get { return processBuffer.address; } }

        public uint ProcessId = 0;
        public IntPtr ProcessHandle = IntPtr.Zero;
        public bool HasLexilla { get; set; }
        public uint MainThreadId { get; private set; }
        public IntPtr MainWindowHandle { get; set; }
        public Dictionary<string, IntPtr> iconBuffers = new();

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

        /// <summary>
        /// Type metadata resolver that retrieves type information from the database.
        /// Initialized lazily when DataManager is available.
        /// </summary>
        public ITypeMetadataResolver? TypeResolver
        {
            get
            {
                // Lazy initialization when DataManager is available
                if (_typeResolver == null && DataManager != null)
                {
                    _typeResolver = new DatabaseTypeMetadataResolver(DataManager);
                }
                // Clear resolver if DataManager was disconnected
                else 
                {
                    _typeResolver = new NullTypeMetadataResolver();
                }

                if (_typeResolver is NullTypeMetadataResolver && DataManager != null)
                {
                    _typeResolver = new DatabaseTypeMetadataResolver(DataManager);
                }

                return _typeResolver;
            }
        }

        public GeneralSettingsData Settings {  get; set; }

        public bool DoNotPromptForDB { get; set; }
        public string DBName { get; internal set; }

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

            // Get the main thread ID for this process
            MainThreadId = (uint)process.Threads[0].Id;

            MainWindowHandle = process.MainWindowHandle;

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


        public ScintillaEditor GetOrInitEditor(IntPtr hwnd)
        {
            if (Editors.TryGetValue(hwnd, out ScintillaEditor? editor))
            {
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

            ScintillaManager.SetAutoCompleteIcons(editor);
            ScintillaManager.FixEditorTabs(editor);
            if (Settings.OnlyPPC && editor.Type != EditorType.PeopleCode)
            {
                /* Skip the rest of the editor initialization */
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


        public IntPtr GetStandaloneProcessBuffer(uint neededSize)
        {
            var buffer = WinApi.VirtualAllocEx(ProcessHandle, IntPtr.Zero, neededSize, WinApi.MEM_COMMIT, WinApi.PAGE_READWRITE);
            return buffer;
        }

        public void FreeStandaloneProcessBuffer(IntPtr address)
        {
            WinApi.VirtualFreeEx(ProcessHandle, address, 0, WinApi.MEM_RELEASE);
        }

        public IntPtr GetProcessBuffer(uint neededSize)
        {
            var currentBuffer = processBuffer.address;
            var currentSize = processBuffer.size;
            if (neededSize > currentSize) {
                if (currentBuffer != IntPtr.Zero)
                {
                    WinApi.VirtualFreeEx(ProcessHandle, currentBuffer, 0, WinApi.MEM_RELEASE);
                }
                var buffer = WinApi.VirtualAllocEx(ProcessHandle, IntPtr.Zero, neededSize, WinApi.MEM_COMMIT, WinApi.PAGE_READWRITE);
                processBuffer = (buffer, neededSize);
            }

            return processBuffer.address;
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

                IntPtr remoteBuffer = GetStandaloneProcessBuffer(bufferSize);
                Debug.Log($"SetOpenTarget Got remote buffer: 0x{remoteBuffer:X}");
                if (remoteBuffer == IntPtr.Zero)
                {
                    Debug.Log($"SetOpenTarget Remote Buffer was 0");
                    return false;
                }

                // Write the wide string to the remote buffer
                bool writeSuccess = WriteWideStringToProcess(remoteBuffer, openTarget);
                Debug.Log($"SetOpenTarget Write Success: {writeSuccess}");
                if (!writeSuccess)
                {
                    Debug.Log("SetOpenTarget Since we didn't succeed, freeing the buffer!");
                    FreeStandaloneProcessBuffer(remoteBuffer);
                    return false;
                }

                // Send the set open target message with the remote buffer pointer and character count
                Debug.Log($"SetOpenTarget Sending the message with buffer: 0x{remoteBuffer:X}");
                bool setTargetSuccess = SendMessage(MainWindowHandle, (int)WM_AR_SET_OPEN_TARGET, remoteBuffer, charCount) != IntPtr.Zero;
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
                    // Free the buffer after use
                    FreeStandaloneProcessBuffer(remoteBuffer);
                    Debug.Log($"SetOpenTarget Freed buffer 0x{remoteBuffer:X} after double click");
                    return doubleClickSuccess;
                }
                else
                {
                    Debug.Log("SetOpenTarget Freeing buffer because setTargetSuccess was false");
                    // Free the buffer if set target failed
                    FreeStandaloneProcessBuffer(remoteBuffer);
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes a wide string (UTF-16) to the remote process memory
        /// </summary>
        /// <param name="remoteBuffer">Remote buffer address</param>
        /// <param name="text">Text to write</param>
        /// <returns>True if successful</returns>
        private bool WriteWideStringToProcess(IntPtr remoteBuffer, string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
                
            // Convert to wide string (UTF-16) and marshall to remote process
            byte[] stringBytes = Encoding.Unicode.GetBytes(text);
            int neededSize = stringBytes.Length + 2; // +2 for null terminator
            // Create buffer with null terminator
            byte[] buffer = new byte[neededSize];
            Buffer.BlockCopy(stringBytes, 0, buffer, 0, stringBytes.Length);
            buffer[neededSize - 2] = 0; // Ensure null termination
            buffer[neededSize - 1] = 0; // Ensure null termination
            // Write to remote process memory
            return WinApi.WriteProcessMemory(ProcessHandle, remoteBuffer, buffer, neededSize, out int bytesWritten) && bytesWritten == neededSize;
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
