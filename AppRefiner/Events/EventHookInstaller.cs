using System.Runtime.InteropServices;

namespace AppRefiner.Events
{

    public static class EventHookInstaller
    {
        private const uint WM_USER = 0x400;
        private const uint WM_TOGGLE_AUTO_PAIRING = WM_USER + 1002;
        private const uint WM_SUBCLASS_SCINTILLA_PARENT_WINDOW = WM_USER + 1003;
        private const uint WM_REMOVE_HOOK = WM_USER + 1004;
        private const uint WM_SUBCLASS_MAIN_WINDOW = WM_USER + 1005;
        private const uint WM_SET_MAIN_WINDOW_SHORTCUTS = WM_USER + 1006;
        private const uint WM_AR_SUBCLASS_RESULTS_LIST = WM_USER + 1007;
        private const uint WM_AR_SET_OPEN_TARGET = WM_USER + 1008;

        // Bit field for shortcut types
        [Flags]
        public enum ShortcutType : uint
        {
            None = 0,
            CommandPalette = 1 << 0,  // Always enabled - Ctrl+Shift+P
            Open = 1 << 1,            // Override Ctrl+O
            Search = 1 << 2,          // Override Ctrl+F, Ctrl+H, F3
            All = CommandPalette | Open | Search
        }

        private static Dictionary<uint, IntPtr> _activeHooks = new();
        private static Dictionary<uint, IntPtr> _activeKeyboardHooks = new();

        // Win32 API imports
        [DllImport("user32.dll")]
        public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

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
        /// Installs hooks for the specified thread ID. This should be called proactively when 
        /// an AppDesigner process is detected to ensure hooks are available for all operations.
        /// </summary>
        /// <param name="threadId">The thread ID to install hooks for</param>
        /// <returns>True if hooks are successfully installed or already exist</returns>
        public static bool InstallHook(uint threadId)
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

            // Set auto-pairing if subclassing was successful - now synchronous
            if (result && mainWindowHandle != IntPtr.Zero)
            {
                // Small delay to ensure subclassing completes, then set auto-pairing synchronously
                Thread.Sleep(5);
                result = SetAutoPairing(mainWindowHandle, autoPairingEnabled);
            }

            return result;
        }

        // Method to remove the hook
        public static bool RemoveHook(uint threadId)
        {
            return PostThreadMessage(threadId, WM_REMOVE_HOOK, IntPtr.Zero, IntPtr.Zero);
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
