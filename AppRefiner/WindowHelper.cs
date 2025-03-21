namespace AppRefiner
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Forms; // Added this line

    public static class WindowHelper
    {
        // Delegate for EnumWindows callback function
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

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
        /// Given a process ID, retrieves the caption of its main window.
        /// </summary>
        /// <param name="processId">The process ID to get the main window caption for.</param>
        /// <returns>The caption text of the main window, or an empty string if not found.</returns>
        public static string GetMainWindowCaption(uint processId)
        {
            // Import the EnumWindows function from user32.dll
            [DllImport("user32.dll")]
            static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

            // Import the GetWindowThreadProcessId function from user32.dll
            [DllImport("user32.dll", SetLastError = true)]
            static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

            IntPtr foundWindow = IntPtr.Zero;
            string windowCaption = string.Empty;
            // Enumerate all top-level windows
            EnumWindows((hWnd, lParam) =>
            {
                // Check if this window belongs to our process
                GetWindowThreadProcessId(hWnd, out uint winProcessId);
                if (winProcessId == processId)
                {
                    // Check if window is visible and has a title
                    StringBuilder caption = new(256);
                    int length = GetWindowText(hWnd, caption, caption.Capacity);
                    windowCaption = caption.ToString();
                    if (length > 0 && windowCaption.StartsWith("Application Designer"))
                    {
                        foundWindow = hWnd;
                        return false; // Stop enumeration
                    }
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            return windowCaption;
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
            StringBuilder caption = new(256);
            int length = GetWindowText(grandparent, caption, caption.Capacity);
            return length > 0 ? caption.ToString() : string.Empty;
        }

        /// <summary>
        /// Centers a form on the specified window.
        /// </summary>
        /// <param name="form">The form to center.</param>
        /// <param name="ownerWindow">The window to center on.</param>
        public static void CenterFormOnWindow(Form form, IntPtr ownerWindow)
        {
            if (ownerWindow == IntPtr.Zero)
                return;

            // Get the owner window's rectangle
            if (GetWindowRect(ownerWindow, out RECT ownerRect))
            {
                int ownerWidth = ownerRect.Right - ownerRect.Left;
                int ownerHeight = ownerRect.Bottom - ownerRect.Top;
                int ownerCenterX = ownerRect.Left + (ownerWidth / 2);
                int ownerCenterY = ownerRect.Top + (ownerHeight / 2);

                // Calculate the form's position
                int formX = ownerCenterX - (form.Width / 2);
                int formY = ownerCenterY - (form.Height / 2);

                // Set the form's position
                form.Location = new System.Drawing.Point(formX, formY);
            }
        }
    }


    public class WindowWrapper : IWin32Window
    {
        public WindowWrapper(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }
}
