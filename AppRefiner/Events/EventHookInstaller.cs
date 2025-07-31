using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.Events
{
    
    internal static class EventHookInstaller
    {
        private const uint WM_USER = 0x400;
        private const uint WM_TOGGLE_AUTO_PAIRING = WM_USER + 1002;
        private const uint WM_SUBCLASS_WINDOW = WM_USER + 1003;
        private const uint WM_REMOVE_HOOK = WM_USER + 1004;

        private static Dictionary<uint, IntPtr> _activeHooks = new Dictionary<uint, IntPtr>();

        // Win32 API imports
        [DllImport("user32.dll")]
        public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        // DLL imports
        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SetHook(uint threadId);

        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Unhook();

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
            
            _activeHooks.Clear();
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

        // Method to unhook a specific thread
        public static bool UnhookThread(uint threadId)
        {
            if (_activeHooks.TryGetValue(threadId, out IntPtr hookId))
            {
                bool result = Unhook();
                if (result)
                {
                    _activeHooks.Remove(threadId);
                }
                return result;
            }
            
            return false;
        }
    }
}
