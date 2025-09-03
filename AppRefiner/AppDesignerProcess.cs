using AppRefiner.Database;
using AppRefiner.Events;
using DiffPlex.Model;
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

        /// <summary>
        /// Handle to the Results ListView for this Application Designer process, if known.
        /// </summary>
        public IntPtr ResultsListView { get; set; }
        public IDataManager? DataManager { get; set; }

        public GeneralSettingsData Settings {  get; set; }

        public bool DoNotPromptForDB { get; set; }

        /// <summary>
        /// Updates the Results ListView handle for this process
        /// </summary>
        /// <param name="resultsListView">Handle to the Results ListView</param>
        public void SetResultsListView(IntPtr resultsListView)
        {
            ResultsListView = resultsListView;
        }

        public AppDesignerProcess(uint pid, IntPtr resultsListView, GeneralSettingsData settings)
        {
            ProcessId = pid;
            ResultsListView = resultsListView;
            Settings = settings;
            ProcessHandle = WinApi.OpenProcess(WinApi.PROCESS_VM_READ | WinApi.PROCESS_VM_WRITE | WinApi.PROCESS_VM_OPERATION, false, ProcessId);

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
            EventHookInstaller.SubclassMainWindow(this, CallbackWindow, true);

            EventHookInstaller.SubclassResultsList(MainThreadId, ResultsListView, IntPtr.Zero);
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
            EventHookInstaller.SubclassScintillaParentWindow(threadId, WindowHelper.GetParentWindow(hWnd), CallbackWindow, true);


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

            [DllImport("user32.dll")]
            static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

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
                if (remoteBuffer == IntPtr.Zero)
                {
                    return false;
                }

                // Write the wide string to the remote buffer
                bool writeSuccess = WriteWideStringToProcess(remoteBuffer, openTarget);
                if (!writeSuccess)
                {
                    FreeStandaloneProcessBuffer(remoteBuffer);
                    return false;
                }

                // Send the set open target message with the remote buffer pointer and character count
                bool setTargetSuccess = PostThreadMessage(MainThreadId, WM_AR_SET_OPEN_TARGET, remoteBuffer, charCount);

                if (setTargetSuccess)
                {
                    // Send synthetic double-click to trigger IDE behavior
                    const int WM_LBUTTONDBLCLK = 0x0203;
                    const int MK_LBUTTON = 0x0001;
                    IntPtr lParam = IntPtr.Zero; // MAKELONG(0, 0) - coordinates (0,0)

                    bool doubleClickSuccess = SendMessage(ResultsListView, WM_LBUTTONDBLCLK, MK_LBUTTON, lParam) != IntPtr.Zero;

                    // Free the buffer after use
                    FreeStandaloneProcessBuffer(remoteBuffer);

                    return doubleClickSuccess;
                }
                else
                {
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

    }
}
