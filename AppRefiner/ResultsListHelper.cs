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
        /// Adds an item to the Results ListView using cross-process memory allocation
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
                
                // Allocate buffer in target process for the ANSI string
                int charCount = text.Length;
                uint bufferSize = (uint)(charCount + 1); // +1 for null terminator

                IntPtr remoteTextBuffer = process.GetStandaloneProcessBuffer(bufferSize);
                if (remoteTextBuffer == IntPtr.Zero)
                {
                    Debug.Log($"Failed to allocate remote buffer for text '{text}'");
                    return false;
                }

                // Write the ANSI string to the remote buffer
                byte[] textBytes = System.Text.Encoding.Default.GetBytes(text + '\0');
                bool writeSuccess = WinApi.WriteProcessMemory(process.ProcessHandle, remoteTextBuffer, textBytes, (int)textBytes.Length, out _);
                if (!writeSuccess)
                {
                    Debug.Log($"Failed to write text '{text}' to remote buffer");
                    process.FreeStandaloneProcessBuffer(remoteTextBuffer);
                    return false;
                }

                // Allocate buffer for LVITEM structure in target process
                uint lvItemSize = (uint)Marshal.SizeOf<WinApi.LVITEM>();
                IntPtr remoteLvItemBuffer = process.GetStandaloneProcessBuffer(lvItemSize);
                if (remoteLvItemBuffer == IntPtr.Zero)
                {
                    Debug.Log($"Failed to allocate remote buffer for LVITEM structure");
                    process.FreeStandaloneProcessBuffer(remoteTextBuffer);
                    return false;
                }

                // Create LVITEM structure with remote text pointer
                var lvItem = new WinApi.LVITEM
                {
                    mask = WinApi.LVIF_TEXT,
                    iItem = itemCount, // Insert at the end
                    iSubItem = 0,
                    pszText = remoteTextBuffer, // Point to remote buffer
                    cchTextMax = text.Length
                };

                // Write LVITEM structure to remote buffer
                IntPtr processHandle = process.ProcessHandle;
                byte[] lvItemBytes = new byte[lvItemSize];
                IntPtr lvItemPtr = Marshal.AllocHGlobal((int)lvItemSize);
                
                try
                {
                    Marshal.StructureToPtr(lvItem, lvItemPtr, false);
                    Marshal.Copy(lvItemPtr, lvItemBytes, 0, (int)lvItemSize);
                    
                    bool writeStructSuccess = WinApi.WriteProcessMemory(processHandle, remoteLvItemBuffer, lvItemBytes, (int)lvItemSize, out _);
                    if (!writeStructSuccess)
                    {
                        Debug.Log($"Failed to write LVITEM structure to remote buffer");
                        return false;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(lvItemPtr);
                }

                // Insert the item using remote LVITEM buffer
                int result = (int)WinApi.SendMessage(listViewHandle, WinApi.LVM_INSERTITEM, 0, remoteLvItemBuffer);
                
                // Free the allocated buffers
                //process.FreeStandaloneProcessBuffer(remoteTextBuffer);
               // process.FreeStandaloneProcessBuffer(remoteLvItemBuffer);
                
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