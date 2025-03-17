using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Drawing;

namespace AppRefiner
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Drawing;

    public static class DarkModeHelper
    {
        // Constants for TreeView messages
        private const int TV_FIRST = 0x1100;
        private const int TVM_SETBKCOLOR = TV_FIRST + 29;
        private const int TVM_SETTEXTCOLOR = TV_FIRST + 30; // Using +30 as requested

        // Constants for ListView messages
        private const int LVM_FIRST = 0x1000;
        private const int LVM_SETBKCOLOR = LVM_FIRST + 1;
        private const int LVM_SETTEXTCOLOR = LVM_FIRST + 36;
        private const int LVM_SETOUTLINECOLOR = LVM_FIRST + 177;
        // Constant for WM_SETREDRAW
        private const int WM_SETREDRAW = 0x000B;

        // Dark mode colors: background 0x1A1A1A and white text
        private static readonly Color DarkBackground = Color.FromArgb(0x1A, 0x1A, 0x1A);
        private static readonly Color LightText = Color.White;
        private static readonly Color GridLines = Color.FromArgb(0xA0, 0xA0, 0xA0);
        // Delegate used by EnumWindows and EnumChildWindows
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        // InvalidateRect function to force a window repaint.
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        // UpdateWindow forces the window to repaint immediately.
        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        // Converts a Color into a COLORREF value.
        private static IntPtr ColorToCOLORREF(Color color)
        {
            int colorRef = ColorTranslator.ToWin32(color);
            return new IntPtr(colorRef);
        }

        /// <summary>
        /// Enumerates all top-level windows in the given process, then recursively applies dark mode
        /// styling to any SysTreeView32 and SysListView32 child windows.
        /// </summary>
        /// <param name="processId">The target process ID.</param>
        public static void ApplyDarkModeToControls(int processId)
        {
            EnumWindows((hWnd, lParam) =>
            {
                // Check if this top-level window belongs to the specified process.
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == processId)
                {
                    ProcessWindowAndChildren(hWnd);
                }
                return true; // Continue enumeration.
            }, IntPtr.Zero);
        }

        // Recursively processes the window and its child windows.
        private static void ProcessWindowAndChildren(IntPtr hWnd)
        {
            // Retrieve the window's class name.
            StringBuilder className = new StringBuilder(256);
            if (GetClassName(hWnd, className, className.Capacity) != 0)
            {
                string clsName = className.ToString();
                if (clsName.Equals("SysTreeView32", StringComparison.Ordinal))
                {
                    ApplyTreeViewDarkMode(hWnd);
                }
                else if (clsName.Equals("SysListView32", StringComparison.Ordinal))
                {
                    ApplyListViewDarkMode(hWnd);
                }
            }

            // Enumerate child windows of this window.
            EnumChildWindows(hWnd, (childHwnd, lParam) =>
            {
                ProcessWindowAndChildren(childHwnd);
                return true; // Continue enumeration.
            }, IntPtr.Zero);
        }

        // Applies dark mode to a SysTreeView32 control.
        private static void ApplyTreeViewDarkMode(IntPtr hwnd)
        {
            // Set the background color.
            SendMessage(hwnd, TVM_SETBKCOLOR, IntPtr.Zero, ColorToCOLORREF(DarkBackground));
            // Set the text color.
            SendMessage(hwnd, TVM_SETTEXTCOLOR, IntPtr.Zero, ColorToCOLORREF(LightText));
        }

        // Applies dark mode to a SysListView32 control.
        private static void ApplyListViewDarkMode(IntPtr hwnd)
        {
            // Set the background color.
            SendMessage(hwnd, LVM_SETBKCOLOR, IntPtr.Zero, ColorToCOLORREF(DarkBackground));
            // Set the text color.
            SendMessage(hwnd, LVM_SETTEXTCOLOR, IntPtr.Zero, ColorToCOLORREF(LightText));
            SendMessage(hwnd, LVM_SETOUTLINECOLOR, IntPtr.Zero, ColorToCOLORREF(GridLines));

            // Force the control to redraw using the InvalidateRect approach.
            // Disable redraw.
            SendMessage(hwnd, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            // Invalidate the entire client area.
            InvalidateRect(hwnd, IntPtr.Zero, true);
            // Force an immediate repaint.
            UpdateWindow(hwnd);
            // Re-enable redraw.
            SendMessage(hwnd, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            InvalidateRect(hwnd, IntPtr.Zero, true);
            UpdateWindow(hwnd);
        }
    }

}