using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AppRefiner
{
    public static class ResultsListHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private class EnumWindowsData
        {
            public IntPtr ResultsListView = IntPtr.Zero;
            public uint TargetProcessId;
        }

        /// <summary>
        /// Finds the Results list view (SysListView32) for a given process ID.
        /// Searches for a SysListView32 control with empty caption, whose parent has caption "Results"
        /// and great grandparent has caption "Output Window".
        /// </summary>
        /// <param name="processId">The target Editor process ID</param>
        /// <returns>Handle to the Results list view, or IntPtr.Zero if not found</returns>
        public static IntPtr FindResultsListView(uint processId)
        {
            try
            {
                // Get the main window handle for the process
                var process = Process.GetProcessById((int)processId);
                if (process?.MainWindowHandle == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                var enumData = new EnumWindowsData { TargetProcessId = processId };

                // Enumerate all child windows of the main window
                EnumerateAllChildWindows(process.MainWindowHandle, enumData);

                return enumData.ResultsListView;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error finding Results list view for process {processId}: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        private static void EnumerateAllChildWindows(IntPtr parentWindow, EnumWindowsData enumData)
        {
            WindowHelper.EnumerateChildWindows(parentWindow, (hWnd, lParam) =>
            {
                if (enumData.ResultsListView != IntPtr.Zero)
                {
                    return false; // Found it, stop enumeration
                }

                // Check if this is a SysListView32 control
                var className = WindowHelper.GetWindowClass(hWnd);
                if (className == "SysListView32")
                {
                    // Verify it belongs to our target process
                    if (GetWindowThreadProcessId(hWnd, out uint windowProcessId) > 0 &&
                        windowProcessId == enumData.TargetProcessId)
                    {
                        // Check if this SysListView32 has empty caption
                        var listViewCaption = WindowHelper.GetWindowText(hWnd);
                        if (string.IsNullOrEmpty(listViewCaption))
                        {
                            // Validate the window hierarchy
                            if (ValidateResultsListHierarchy(hWnd))
                            {
                                enumData.ResultsListView = hWnd;
                                return false; // Found it, stop enumeration
                            }
                        }
                    }
                }

                // Recursively enumerate child windows
                EnumerateAllChildWindows(hWnd, enumData);
                return true; // Continue enumeration
            }, IntPtr.Zero);
        }

        private static bool ValidateResultsListHierarchy(IntPtr listViewHandle)
        {
            // Get parent window
            var parent = WindowHelper.GetParentWindow(listViewHandle);
            if (parent == IntPtr.Zero)
            {
                return false;
            }

            // Check parent caption is "Results"
            var parentCaption = WindowHelper.GetWindowText(parent);
            if (parentCaption != "Results")
            {
                return false;
            }

            // Get grandparent window
            var grandparent = WindowHelper.GetParentWindow(parent);
            if (grandparent == IntPtr.Zero)
            {
                return false;
            }

            // Get great grandparent window
            var greatGrandparent = WindowHelper.GetParentWindow(grandparent);
            if (greatGrandparent == IntPtr.Zero)
            {
                return false;
            }

            // Check great grandparent caption is "Output Window"
            var greatGrandparentCaption = WindowHelper.GetWindowText(greatGrandparent);
            return greatGrandparentCaption == "Output Window";
        }


    }
}