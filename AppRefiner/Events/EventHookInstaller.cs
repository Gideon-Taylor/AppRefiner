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
        private const uint WM_SET_CALLBACK_WINDOW = WM_USER + 1001;
        private const uint WM_TOGGLE_AUTO_PAIRING = WM_USER + 1002;

        // Win32 API imports
        [DllImport("user32.dll")]
        public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        // DLL imports
        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SetHook(uint threadId);

        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Unhook();
        // Method to send pipe name to the hooked thread
        public static bool SendWindowHandleToHookedThread(uint threadId, IntPtr windowHandle)
        {
            // Send the thread message with the pipe ID
            bool result = PostThreadMessage(threadId, WM_SET_CALLBACK_WINDOW, new IntPtr(windowHandle), IntPtr.Zero);

            result = PostThreadMessage(threadId, WM_TOGGLE_AUTO_PAIRING, 1, IntPtr.Zero);

            return result;
        }
    }
}
