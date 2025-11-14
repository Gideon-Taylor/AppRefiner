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
        /// <param name="process">The AppDesignerProcess instance for the target process</param>
        /// <param name="message">The message to add to the Results list</param>
        /// <returns>True if the message was successfully added, false otherwise</returns>
        public static bool AddMessageToResults(AppDesignerProcess process, string message)
        {
            var listViewHandle = FindResultsListView(process.ProcessId);
            if (listViewHandle != IntPtr.Zero)
            {
                return AddItemToResultsList(process, listViewHandle, message);
            }
            return false;
        }

        /// <summary>
        /// Adds a message to the Results ListView after a specified delay.
        /// Useful for ensuring the Application Designer UI is fully loaded before showing connection messages.
        /// </summary>
        /// <param name="process">The AppDesignerProcess instance for the target process</param>
        /// <param name="listViewHandle">Handle to the SysListView32 control</param>
        /// <param name="message">The message to add to the Results list</param>
        /// <param name="delayMs">Delay in milliseconds before adding the message (default: 2000ms)</param>
        /// <returns>Task that completes when the message has been added or the operation fails</returns>
        public static async Task<bool> AddDelayedMessageToResultsList(AppDesignerProcess process, IntPtr listViewHandle, string message, int delayMs = 2000)
        {
            try
            {
                Debug.Log($"Scheduling delayed message '{message}' to Results ListView in {delayMs}ms");
                await Task.Delay(delayMs);
                
                if (listViewHandle != IntPtr.Zero)
                {
                    return AddItemToResultsList(process, listViewHandle, message);
                }
                else
                {
                    Debug.Log($"Cannot add delayed message '{message}': ListView handle is zero");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error adding delayed message to Results ListView: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adds an item to the Results ListView using shared memory buffers.
        /// Uses reusable "results_text" and "results_item" buffers from MemoryManager to avoid memory leaks.
        /// </summary>
        /// <param name="process">The AppDesignerProcess instance for the target process</param>
        /// <param name="listViewHandle">Handle to the SysListView32 control</param>
        /// <param name="text">Text to add as a new item</param>
        /// <returns>True if the item was successfully added, false otherwise</returns>
        private static bool AddItemToResultsList(AppDesignerProcess process, IntPtr listViewHandle, string text)
        {
            try
            {
                // Get the current item count to determine the insertion index
                int itemCount = (int)WinApi.SendMessage(listViewHandle, WinApi.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);

                // Get or create shared text buffer (starts at 1024 bytes, grows as needed)
                var textBuffer = process.MemoryManager.GetOrCreateBuffer("results_text", 1024);

                // Write the ANSI string to the shared buffer
                IntPtr? remoteTextAddress = textBuffer.WriteString(text, System.Text.Encoding.Default, offset: 0);
                if (!remoteTextAddress.HasValue)
                {
                    // Buffer too small, resize and retry
                    uint requiredSize = (uint)(System.Text.Encoding.Default.GetByteCount(text) + 1);
                    textBuffer.Resize(requiredSize);
                    remoteTextAddress = textBuffer.WriteString(text, System.Text.Encoding.Default, offset: 0);

                    if (!remoteTextAddress.HasValue)
                    {
                        Debug.Log($"Failed to write text '{text}' to shared buffer even after resize");
                        return false;
                    }
                }

                // Get or create shared LVITEM structure buffer
                uint lvItemSize = (uint)Marshal.SizeOf<WinApi.LVITEM>();
                var itemBuffer = process.MemoryManager.GetOrCreateBuffer("results_item", lvItemSize);

                // Create LVITEM structure with pointer to text in shared buffer
                var lvItem = new WinApi.LVITEM
                {
                    mask = WinApi.LVIF_TEXT,
                    iItem = itemCount, // Insert at the end
                    iSubItem = 0,
                    pszText = remoteTextAddress.Value, // Point to text in shared buffer
                    cchTextMax = text.Length
                };

                // Marshal LVITEM structure to byte array
                byte[] lvItemBytes = new byte[lvItemSize];
                IntPtr lvItemPtr = Marshal.AllocHGlobal((int)lvItemSize);

                try
                {
                    Marshal.StructureToPtr(lvItem, lvItemPtr, false);
                    Marshal.Copy(lvItemPtr, lvItemBytes, 0, (int)lvItemSize);
                }
                finally
                {
                    Marshal.FreeHGlobal(lvItemPtr);
                }

                // Write LVITEM structure to shared buffer
                IntPtr? remoteLvItemAddress = itemBuffer.Write(lvItemBytes, offset: 0);
                if (!remoteLvItemAddress.HasValue)
                {
                    Debug.Log($"Failed to write LVITEM structure to shared buffer");
                    return false;
                }

                // Insert the item using shared LVITEM buffer
                int result = (int)WinApi.SendMessage(listViewHandle, WinApi.LVM_INSERTITEM, 0, remoteLvItemAddress.Value);

                // Note: No cleanup needed! Buffers are reused for the next message.
                // This fixes the memory leak that existed when cleanup was commented out.

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