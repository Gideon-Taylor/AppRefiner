using System.Runtime.InteropServices;

namespace AppRefiner.Events
{

    public static class EventHookInstaller
    {
        private const uint WM_USER = 0x400;
        private const uint WM_TOGGLE_AUTO_PAIRING = WM_USER + 1002;
        private const uint WM_SUBCLASS_SCINTILLA_PARENT_WINDOW = WM_USER + 1003;
        private const uint WM_SUBCLASS_MAIN_WINDOW = WM_USER + 1005;
        private const uint WM_SET_MAIN_WINDOW_SHORTCUTS = WM_USER + 1006;
        private const uint WM_AR_SUBCLASS_RESULTS_LIST = WM_USER + 1007;
        private const uint WM_AR_SET_OPEN_TARGET = WM_USER + 1008;
        private const uint WM_LOAD_SCINTILLA_DLL = WM_USER + 1009;
        private const uint WM_AR_SET_MINIMAP = WM_USER + 1010;
        private const uint WM_AR_SET_PARAM_NAMES = WM_USER + 1011;
        private const uint WM_AR_DETACH = WM_USER + 1012;

        /// <summary>
        /// Status flags reported by the hook in WM_AR_SUBCLASS_ACK (mirrors AR_SUB_ACK_* in
        /// AppRefinerHook/Common.h) describing how far editor subclassing got.
        /// </summary>
        [Flags]
        public enum SubclassAckFlags : uint
        {
            None = 0,
            ParentSubclassed = 0x0001,
            ScintillaFoundDirect = 0x0002,
            ScintillaFoundRecursive = 0x0004,
            ScintillaSubclassed = 0x0008,
            DialogFound = 0x0010,
            DialogAlreadySubclassed = 0x0020,
            DialogSubclassed = 0x0040,
            ButtonPresent = 0x0080,
            InvalidParent = 0x0100,
        }

        // Bit field for shortcut types
        [Flags]
        public enum ShortcutType : uint
        {
            None = 0,
            CommandPalette = 1 << 0,  // Always enabled - Ctrl+Shift+P
            Open = 1 << 1,            // Override Ctrl+O
            Search = 1 << 2,          // Override Ctrl+F, Ctrl+H, F3
            LineSelection = 1 << 3,   // Override Shift+Up/Down for line selection
            All = CommandPalette | Open | Search | LineSelection
        }

        private static Dictionary<uint, IntPtr> _activeHooks = new();
        private static Dictionary<uint, IntPtr> _activeKeyboardHooks = new();

        // Win32 API imports
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam,
            IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        // DLL imports
        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SetHook(uint threadId);

        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SetKeyboardHook(uint threadId);

        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Unhook();

        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool UnhookKeyboard();

        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool UnsubclassWindow(IntPtr hWnd);

        /// <summary>
        /// Synchronization context of a persistent thread (the UI thread), set once at
        /// startup. SetWindowsHookEx hooks are removed by Windows when their installing
        /// thread exits, so installation must never run on a thread-pool thread (e.g. a
        /// retry timer callback) — the pool retiring that idle thread minutes later would
        /// silently tear the hooks down and kill every posted-message feature.
        /// </summary>
        public static SynchronizationContext? HookInstallContext { get; set; }

        /// <summary>
        /// Installs hooks for the specified thread ID. This should be called proactively when
        /// an AppDesigner process is detected to ensure hooks are available for all operations.
        /// Marshals to <see cref="HookInstallContext"/> so the hooks are always owned by a
        /// persistent thread regardless of the caller.
        /// </summary>
        /// <param name="threadId">The thread ID to install hooks for</param>
        /// <returns>True if hooks are successfully installed or already exist</returns>
        public static bool InstallHook(uint threadId)
        {
            if (HookInstallContext != null)
            {
                if (Thread.CurrentThread.IsThreadPoolThread)
                {
                    Debug.Log($"InstallHook: called from a thread-pool thread for thread {threadId} — marshalling to the UI thread so the hooks survive");
                }

                bool result = false;
                // Send executes inline when already on the target thread
                HookInstallContext.Send(_ => result = InstallHookCore(threadId), null);
                return result;
            }

            return InstallHookCore(threadId);
        }

        private static bool InstallHookCore(uint threadId)
        {
            bool success = true;

            // Install main GetMessage hook if not already present
            if (!_activeHooks.ContainsKey(threadId))
            {
                IntPtr hookId = SetHook(threadId);
                if (hookId != IntPtr.Zero)
                {
                    _activeHooks[threadId] = hookId;
                    Debug.Log($"Successfully installed main hook for thread {threadId}");
                }
                else
                {
                    Debug.Log($"Failed to install main hook for thread {threadId}");
                    success = false;
                }
            }

            // Also install keyboard hook for better shortcut interception
            if (!_activeKeyboardHooks.ContainsKey(threadId))
            {
                IntPtr keyboardHookId = SetKeyboardHook(threadId);
                if (keyboardHookId != IntPtr.Zero)
                {
                    _activeKeyboardHooks[threadId] = keyboardHookId;
                    Debug.Log($"Successfully installed keyboard hook for thread {threadId}");
                }
                else
                {
                    Debug.Log($"Warning: Failed to install keyboard hook for thread {threadId} (non-critical)");
                    // Don't mark as failure since keyboard hook is optional
                }
            }

            return success;
        }

        // Method to subclass a window
        public static bool SubclassScintillaParentWindow(uint threadId, IntPtr windowToSubclass, IntPtr callbackWindow, IntPtr mainWindowHandle, bool autoPairingEnabled)
        {
            // Ensure we have a hook for this thread (should already be installed proactively)
            if (!_activeHooks.ContainsKey(threadId))
            {
                Debug.Log($"Warning: No hook found for thread {threadId} in SubclassWindow - hook should have been installed proactively");
                return false;
            }

            // Send the thread message to subclass the window
            bool result = PostThreadMessage(threadId, WM_SUBCLASS_SCINTILLA_PARENT_WINDOW, windowToSubclass, callbackWindow);
            if (!result)
            {
                int error = Marshal.GetLastWin32Error();
                Debug.Log($"SubclassScintillaParentWindow: PostThreadMessage to thread {threadId} FAILED, Win32 error {error}");
            }

            // Set auto-pairing if subclassing was successful - now synchronous
            if (result && mainWindowHandle != IntPtr.Zero)
            {
                // Small delay to ensure subclassing completes, then set auto-pairing synchronously
                Thread.Sleep(5);
                result = SetAutoPairing(mainWindowHandle, autoPairingEnabled);
            }

            return result;
        }

        // Method to unhook all active hooks (call this when closing the application)
        public static void CleanupAllHooks()
        {
            foreach (var hookPair in _activeHooks.ToList())
            {
                UnhookThread(hookPair.Key);
            }

            foreach (var keyboardHookPair in _activeKeyboardHooks.ToList())
            {
                UnhookKeyboardForThread(keyboardHookPair.Key);
            }

            _activeHooks.Clear();
            _activeKeyboardHooks.Clear();
        }

        /// <summary>
        /// Asks the hook DLL inside the given App Designer process to tear down all
        /// subclasses/child windows and release its self-reference, so the DLL can be
        /// unloaded once the hooks are removed. Sent to the main window synchronously
        /// with a timeout so an unresponsive App Designer cannot block AppRefiner's close.
        /// Best-effort: on timeout/no-response the DLL simply stays loaded in that instance.
        /// </summary>
        public static void DetachFromProcess(IntPtr mainWindowHandle)
        {
            if (mainWindowHandle == IntPtr.Zero)
            {
                return;
            }

            IntPtr ret = SendMessageTimeout(mainWindowHandle, WM_AR_DETACH, IntPtr.Zero,
                IntPtr.Zero, SMTO_ABORTIFHUNG, 2000, out _);

            if (ret == IntPtr.Zero)
            {
                Debug.Log($"DetachFromProcess: no response from main window {mainWindowHandle} " +
                          "(timeout or hung); continuing shutdown");
            }
            else
            {
                Debug.Log($"DetachFromProcess: teardown acknowledged by main window {mainWindowHandle}");
            }
        }

        // Method to send auto-pairing toggle to a specific main window (deprecated - use SetAutoPairing instead)
        public static bool SendAutoPairingToggle(uint threadId, bool enabled)
        {
            // This method is deprecated - callers should use SetAutoPairing with main window handle
            // For now, return false to indicate the old method no longer works
            Debug.Log($"Warning: SendAutoPairingToggle is deprecated. Use SetAutoPairing with main window handle instead.");
            return false;
        }

        // Method to subclass main window
        public static bool SubclassMainWindow(AppDesignerProcess appDesigner, IntPtr callbackWindow, ShortcutType enabledShortcuts)
        {
            // Ensure we have a hook for this thread (should already be installed proactively)
            if (!_activeHooks.ContainsKey(appDesigner.MainThreadId))
            {
                Debug.Log($"Warning: No hook found for thread {appDesigner.MainThreadId} in SubclassMainWindow - hook should have been installed proactively");
                return false;
            }

            // Send the thread message to subclass the main window
            bool result = PostThreadMessage(appDesigner.MainThreadId, WM_SUBCLASS_MAIN_WINDOW, appDesigner.MainWindowHandle, callbackWindow);

            // Set main window shortcuts if subclassing was successful
            // Since WM_SET_MAIN_WINDOW_SHORTCUTS is now handled synchronously in MainWindowSubclassProc,
            // we need to ensure the subclassing is complete before calling it
            if (result)
            {
                // Retry logic to ensure subclassing is complete before setting shortcuts
                bool shortcutResult = false;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    Thread.Sleep(5); // Small delay to allow subclassing to complete
                    shortcutResult = SetMainWindowShortcuts(appDesigner.MainWindowHandle, enabledShortcuts);
                    if (shortcutResult)
                        break;
                }
                Debug.Log($"SetMainWindowShortcuts result for process {appDesigner.ProcessId}: {shortcutResult}");
            }

            return result;
        }

        /// <summary>
        /// Subclasses the Results list view for IDE open target functionality
        /// Note: SetOpenTarget functionality has been moved to AppDesignerProcess class
        /// </summary>
        /// <param name="threadId">Thread ID where the Results list view belongs</param>
        /// <param name="resultsListView">Handle to the Results list view (SysListView32)</param>
        /// <param name="callbackWindow">AppRefiner main window handle for callbacks</param>
        /// <returns>True if subclassing was successful</returns>
        public static bool SubclassResultsList(uint threadId, IntPtr resultsListView, IntPtr callbackWindow)
        {
            // Ensure we have a hook for this thread (should already be installed proactively)
            if (!_activeHooks.ContainsKey(threadId))
            {
                Debug.Log($"Warning: No hook found for thread {threadId} in SubclassResultsList - hook should have been installed proactively");
                return false;
            }

            // Send the thread message to subclass the Results list view
            return PostThreadMessage(threadId, WM_AR_SUBCLASS_RESULTS_LIST, resultsListView, callbackWindow);
        }


        // Method to set main window shortcuts for a specific main window
        public static bool SetMainWindowShortcuts(IntPtr mainWindowHandle, ShortcutType enabledShortcuts)
        {
            if (mainWindowHandle != IntPtr.Zero)
            {
                return WinApi.SendMessage(mainWindowHandle, WM_SET_MAIN_WINDOW_SHORTCUTS, (IntPtr)(uint)enabledShortcuts, IntPtr.Zero) != IntPtr.Zero;
            }
            return false;
        }

        // Method to set auto-pairing for a specific main window
        public static bool SetAutoPairing(IntPtr mainWindowHandle, bool enabled)
        {
            if (mainWindowHandle != IntPtr.Zero)
            {
                return WinApi.SendMessage(mainWindowHandle, (int)WM_TOGGLE_AUTO_PAIRING, enabled ? 1 : 0, IntPtr.Zero) != IntPtr.Zero;
            }
            return false;
        }

        /// <summary>
        /// Enables or disables the minimap for a specific editor.
        /// Posts a message to the editor's thread; the hook enables/disables via MinimapManager
        /// and syncs the context-menu checkbox.
        /// </summary>
        public static bool SetMinimap(ScintillaEditor editor, bool enabled)
        {
            uint threadId = WinApi.GetWindowThreadProcessId(editor.hWnd, out _);
            return PostThreadMessage(threadId, WM_AR_SET_MINIMAP, editor.hWnd, enabled ? (IntPtr)1 : IntPtr.Zero);
        }

        /// <summary>
        /// Syncs the parameter-names checkbox state in the hook's context menu for a specific editor.
        /// The caller is responsible for the actual inlay-hint toggle on the C# side
        /// (e.g. activating/deactivating the FunctionParameterNames styler).
        /// </summary>
        public static bool SetParamNames(ScintillaEditor editor, bool enabled)
        {
            uint threadId = WinApi.GetWindowThreadProcessId(editor.hWnd, out _);
            return PostThreadMessage(threadId, WM_AR_SET_PARAM_NAMES, editor.hWnd, enabled ? (IntPtr)1 : IntPtr.Zero);
        }

        // Helper method to ensure Command Palette is always enabled
        public static ShortcutType EnsureCommandPaletteEnabled(ShortcutType shortcuts)
        {
            return shortcuts | ShortcutType.CommandPalette;
        }

        // Method to unhook keyboard hook for a specific thread
        public static bool UnhookKeyboardForThread(uint threadId)
        {
            if (_activeKeyboardHooks.TryGetValue(threadId, out IntPtr keyboardHookId))
            {
                bool result = UnhookKeyboard();
                if (result)
                {
                    _activeKeyboardHooks.Remove(threadId);
                }
                return result;
            }

            return false;
        }

        // Method to unhook a specific thread
        public static bool UnhookThread(uint threadId)
        {
            bool result = true;

            // Unhook keyboard hook first
            if (_activeKeyboardHooks.ContainsKey(threadId))
            {
                result &= UnhookKeyboardForThread(threadId);
            }

            // Unhook main hook
            if (_activeHooks.TryGetValue(threadId, out IntPtr hookId))
            {
                bool unhookResult = Unhook();
                if (unhookResult)
                {
                    _activeHooks.Remove(threadId);
                }
                result &= unhookResult;
            }

            return result;
        }

        /// <summary>
        /// Checks if a thread has an active hook
        /// </summary>
        /// <param name="threadId">Thread ID to check</param>
        /// <returns>True if the thread has an active hook</returns>
        public static bool HasActiveHook(uint threadId)
        {
            return _activeHooks.ContainsKey(threadId);
        }
    }
}
