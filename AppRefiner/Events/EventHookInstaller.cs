using System.Runtime.InteropServices;

namespace AppRefiner.Events
{

    internal static class EventHookInstaller
    {
        private const uint WM_USER = 0x400;
        private const uint WM_TOGGLE_AUTO_PAIRING = WM_USER + 1002;
        private const uint WM_SUBCLASS_WINDOW = WM_USER + 1003;
        private const uint WM_REMOVE_HOOK = WM_USER + 1004;
        private const uint WM_SUBCLASS_MAIN_WINDOW = WM_USER + 1005;
        private const uint WM_TOGGLE_MAIN_WINDOW_SHORTCUTS = WM_USER + 1006;
        private const uint WM_AR_SUBCLASS_RESULTS_LIST = WM_USER + 1007;
        private const uint WM_AR_SET_OPEN_TARGET = WM_USER + 1008;

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

        // Method to subclass a window
        public static bool SubclassWindow(uint threadId, IntPtr windowToSubclass, IntPtr callbackWindow, bool autoPairingEnabled)
        {
            // If we already have a hook for this thread, use it
            if (!_activeHooks.TryGetValue(threadId, out IntPtr existingHookId))
            {
                // Set a new hook
                IntPtr hookId = SetHook(threadId);
                if (hookId == IntPtr.Zero)
                {
                    return false;
                }

                // Store the hook ID
                _activeHooks[threadId] = hookId;
            }

            // Send the thread message to subclass the window
            bool result = PostThreadMessage(threadId, WM_SUBCLASS_WINDOW, windowToSubclass, callbackWindow);

            // Toggle auto-pairing if subclassing was successful
            if (result)
            {
                result = PostThreadMessage(threadId, WM_TOGGLE_AUTO_PAIRING, autoPairingEnabled ? 1 : 0, IntPtr.Zero);
            }

            // Do not unhook here, as unhooking might cause the DLL to unload
            // Unhook();

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

        // Method to send auto-pairing toggle to a specific thread
        public static bool SendAutoPairingToggle(uint threadId, bool enabled)
        {
            if (_activeHooks.ContainsKey(threadId))
            {
                return PostThreadMessage(threadId, WM_TOGGLE_AUTO_PAIRING, enabled ? 1 : 0, IntPtr.Zero);
            }
            return false;
        }

        // Method to subclass main window
        public static bool SubclassMainWindow(uint threadId, IntPtr mainWindow, IntPtr callbackWindow, bool shortcutsEnabled)
        {
            // Ensure we have a hook for this thread first
            if (!_activeHooks.ContainsKey(threadId))
            {
                // Set a new hook
                IntPtr hookId = SetHook(threadId);
                if (hookId == IntPtr.Zero)
                {
                    return false;
                }

                // Store the hook ID
                _activeHooks[threadId] = hookId;
            }

            // Also set up keyboard hook for better shortcut interception
            if (!_activeKeyboardHooks.ContainsKey(threadId))
            {
                IntPtr keyboardHookId = SetKeyboardHook(threadId);
                if (keyboardHookId != IntPtr.Zero)
                {
                    _activeKeyboardHooks[threadId] = keyboardHookId;
                }
            }

            // Send the thread message to subclass the main window
            bool result = PostThreadMessage(threadId, WM_SUBCLASS_MAIN_WINDOW, mainWindow, callbackWindow);

            // Toggle main window shortcuts if subclassing was successful
            if (result)
            {
                result = PostThreadMessage(threadId, WM_TOGGLE_MAIN_WINDOW_SHORTCUTS, shortcutsEnabled ? 1 : 0, IntPtr.Zero);
            }

            return result;
        }

        /// <summary>
        /// Subclasses the Results list view for IDE open target functionality
        /// </summary>
        /// <param name="threadId">Thread ID where the Results list view belongs</param>
        /// <param name="resultsListView">Handle to the Results list view (SysListView32)</param>
        /// <param name="callbackWindow">AppRefiner main window handle for callbacks</param>
        /// <returns>True if subclassing was successful</returns>
        public static bool SubclassResultsList(uint threadId, IntPtr resultsListView, IntPtr callbackWindow)
        {
            // Ensure we have a hook for this thread first
            if (!_activeHooks.ContainsKey(threadId))
            {
                // Set a new hook
                IntPtr hookId = SetHook(threadId);
                if (hookId == IntPtr.Zero)
                {
                    return false;
                }

                // Store the hook ID
                _activeHooks[threadId] = hookId;
            }

            // Send the thread message to subclass the Results list view
            return PostThreadMessage(threadId, WM_AR_SUBCLASS_RESULTS_LIST, resultsListView, callbackWindow);
        }

        /// <summary>
        /// Sets the open target string for Results list interception and triggers double-click
        /// </summary>
        /// <param name="threadId">Thread ID where the Results list view belongs</param>
        /// <param name="resultsListView">Handle to the Results list view</param>
        /// <param name="processId">Process ID of the target Editor process</param>
        /// <param name="openTarget">Target string to open (max 255 chars)</param>
        /// <returns>True if operation was successful</returns>
        public static bool SetOpenTarget(ScintillaEditor editor, IntPtr resultsListView, string openTarget)
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

            if (!_activeHooks.ContainsKey(editor.ThreadID))
            {
                return false;
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

                IntPtr remoteBuffer = editor.AppDesignerProcess.GetStandaloneProcessBuffer(bufferSize);
                if (remoteBuffer == IntPtr.Zero)
                {
                    return false;
                }

                // Write the wide string to the remote buffer
                bool writeSuccess = ScintillaManager.WriteWideStringToProcess(editor, remoteBuffer, openTarget);
                if (!writeSuccess)
                {
                    editor.AppDesignerProcess.FreeStandaloneProcessBuffer(remoteBuffer);
                    return false;
                }

                // Send the set open target message with the remote buffer pointer and character count
                bool setTargetSuccess = PostThreadMessage(editor.ThreadID, WM_AR_SET_OPEN_TARGET, remoteBuffer, charCount);

                if (setTargetSuccess)
                {
                    // Send synthetic double-click to trigger IDE behavior
                    const int WM_LBUTTONDBLCLK = 0x0203;
                    const int MK_LBUTTON = 0x0001;
                    IntPtr lParam = IntPtr.Zero; // MAKELONG(0, 0) - coordinates (0,0)

                    bool doubleClickSuccess = SendMessage(resultsListView, WM_LBUTTONDBLCLK, MK_LBUTTON, lParam) > 0;

                    // Free the buffer after use
                    editor.AppDesignerProcess.FreeStandaloneProcessBuffer(remoteBuffer);

                    return doubleClickSuccess;
                }
                else
                {
                    // Free the buffer if set target failed
                    editor.AppDesignerProcess.FreeStandaloneProcessBuffer(remoteBuffer);
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method to send main window shortcuts toggle to a specific thread
        public static bool ToggleMainWindowShortcuts(uint threadId, bool enabled)
        {
            if (_activeHooks.ContainsKey(threadId))
            {
                return PostThreadMessage(threadId, WM_TOGGLE_MAIN_WINDOW_SHORTCUTS, enabled ? 1 : 0, IntPtr.Zero);
            }
            return false;
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
    }
}
