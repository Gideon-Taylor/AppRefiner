using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AppRefiner
{
    public static class ResultsListHelper
    {
        // Note: P/Invoke declarations moved to WinApi.cs for centralized access

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

                if (enumData.ResultsListView != IntPtr.Zero)
                {
                    // Add "AppRefiner Connected" item to the Results ListView
                    AddItemToResultsList(enumData.ResultsListView, "AppRefiner Connected");
                }

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
                    if (WinApi.GetWindowThreadProcessId(hWnd, out uint windowProcessId) > 0 &&
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

        /// <summary>
        /// Adds a custom message to the Results ListView for a given process
        /// </summary>
        /// <param name="processId">The target Editor process ID</param>
        /// <param name="message">The message to add to the Results list</param>
        /// <returns>True if the message was successfully added, false otherwise</returns>
        public static bool AddMessageToResults(uint processId, string message)
        {
            var listViewHandle = FindResultsListView(processId);
            if (listViewHandle != IntPtr.Zero)
            {
                return AddItemToResultsList(listViewHandle, message);
            }
            return false;
        }

        /// <summary>
        /// Adds an item to the Results ListView
        /// </summary>
        /// <param name="listViewHandle">Handle to the SysListView32 control</param>
        /// <param name="text">Text to add as a new item</param>
        /// <returns>True if the item was successfully added, false otherwise</returns>
        private static bool AddItemToResultsList(IntPtr listViewHandle, string text)
        {
            try
            {
                // Get the current item count to determine the insertion index
                int itemCount = (int)WinApi.SendMessage(listViewHandle, WinApi.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                
                // Create LVITEM structure
                var lvItem = new WinApi.LVITEM
                {
                    mask = WinApi.LVIF_TEXT,
                    iItem = itemCount, // Insert at the end
                    iSubItem = 0,
                    pszText = text,
                    cchTextMax = text.Length
                };

                // Insert the item
                int result = (int)WinApi.SendMessage(listViewHandle, WinApi.LVM_INSERTITEM, 0, ref lvItem);
                
                bool success = result != -1;
                if (success)
                {
                    Debug.Log($"Successfully added '{text}' to Results ListView (item index: {result})");
                }
                else
                {
                    Debug.Log($"Failed to add '{text}' to Results ListView");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error adding item to Results ListView: {ex.Message}");
                return false;
            }
        }

    }
}