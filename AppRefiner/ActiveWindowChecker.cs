using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace AppRefiner
{
    public class ActiveWindowChecker
    {
        // Win32 API imports
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public System.Drawing.Rectangle rcCaret;
        }

        /// <summary>
        /// Checks if the currently active window is a Scintilla editor within pside.exe
        /// </summary>
        /// <returns>IntPtr to the Scintilla window if active, otherwise IntPtr.Zero</returns>
        public static IntPtr GetActiveScintillaWindow()
        {
            // Get the foreground window
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return IntPtr.Zero;

            // Get the process ID and thread ID of the foreground window
            uint processId;
            uint threadId = GetWindowThreadProcessId(foregroundWindow, out processId);

            // Check if the process is pside.exe
            try
            {
                Process process = Process.GetProcessById((int)processId);
                if (!string.Equals(process.ProcessName, "pside", StringComparison.OrdinalIgnoreCase))
                    return new IntPtr(-1);
            }
            catch
            {
                return new IntPtr(-1);
            }

            // Get the GUI thread info to find the focused window
            GUITHREADINFO guiInfo = new();
            guiInfo.cbSize = Marshal.SizeOf(guiInfo);

            if (GetGUIThreadInfo(threadId, ref guiInfo))
            {
                // Check if the focused window is a Scintilla window
                if (guiInfo.hwndFocus != IntPtr.Zero && IsScintillaWindow(guiInfo.hwndFocus))
                    return guiInfo.hwndFocus;
            }

            // If we can't find a focused Scintilla window, return null
            return IntPtr.Zero;
        }

        /// <summary>
        /// Checks if the given window is a Scintilla editor window
        /// </summary>
        private static bool IsScintillaWindow(IntPtr hwnd)
        {
            StringBuilder className = new(256);
            GetClassName(hwnd, className, className.Capacity);
            return className.ToString() == "Scintilla";
        }
    }
}