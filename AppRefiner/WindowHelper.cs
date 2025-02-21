using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    public static class WindowHelper
    {
        // Retrieves the handle to the foreground window (i.e. the currently focused window).
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // Retrieves a handle to the specified window's parent or owner.
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        // Copies the text of the specified window's title bar (if it has one) into a buffer.
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        /// <summary>
        /// Returns the handle (hWnd) of the currently focused (foreground) window.
        /// </summary>
        public static IntPtr GetCurrentlyFocusedWindow()
        {
            return GetForegroundWindow();
        }

        public static void FocusWindow(IntPtr hWnd)
        {
            SetForegroundWindow(hWnd);
        }

        /// <summary>
        /// Given a window handle (hWnd), retrieves the caption of its grandparent window.
        /// If the parent or grandparent does not exist, an empty string is returned.
        /// </summary>
        /// <param name="hWnd">The handle of the starting window.</param>
        /// <returns>The caption text of the grandparent window, or an empty string if not found.</returns>
        public static string GetGrandparentWindowCaption(IntPtr hWnd)
        {
            // First, get the parent window.
            IntPtr parent = GetParent(hWnd);
            if (parent == IntPtr.Zero)
            {
                return string.Empty;
            }

            // Then, get the grandparent window.
            IntPtr grandparent = GetParent(parent);
            if (grandparent == IntPtr.Zero)
            {
                return string.Empty;
            }

            // Prepare a buffer to hold the window caption.
            StringBuilder caption = new StringBuilder(256);
            int length = GetWindowText(grandparent, caption, caption.Capacity);
            if (length > 0)
            {
                return caption.ToString();
            }
            return string.Empty;
        }
    }

}
