using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using System.IO.Hashing;
using SQL.Formatter.Core;
using SQL.Formatter.Language;
using SQL.Formatter;

namespace AppRefiner
{
    public enum AnnotationStyle 
    {
        Gray = 0,
        Yellow = 1, 
        Red = 2
    }

    public class ScintillaManager
    {
        // Scintilla messages.
        private const int SCI_SETTEXT = 2181;
        private const int SCI_GETTEXT = 2182;
        private const int SCI_GETLENGTH = 2006;
        private const int SCI_SETPROPERTY = 4004;
        private const int SCI_SETFOLDLEVEL = 2222;
        private const int SC_FOLDLEVELBASE = 0x400;
        private const int SC_FOLDLEVELWHITEFLAG = 0x1000;
        private const int SC_FOLDLEVELHEADERFLAG = 0x2000;
        private const int SC_FOLDLEVELNUMBERMASK = 0x0FFF;
        private const int SCI_SETMARGINTYPEN = 2240;
        private const int SCI_SETMARGINWIDTHN = 2242;
        private const int SCI_SETFOLDFLAGS = 2233;
        private const int SCI_SETMARGINMASKN = 2244;
        private const int SCI_SETMARGINSENSITIVEN = 2246;
        private const int SC_MARGIN_SYMBOL = 0;
        private const uint SC_MASK_FOLDERS = 0xFE000000;
        private const int SCI_MARKERDEFINE = 2040;
        private const int SC_MARKNUM_FOLDEREND = 25;
        private const int SC_MARKNUM_FOLDEROPENMID = 26;
        private const int SC_MARKNUM_FOLDERMIDTAIL = 27;
        private const int SC_MARKNUM_FOLDERTAIL = 28;
        private const int SC_MARKNUM_FOLDERSUB = 29;
        private const int SC_MARKNUM_FOLDER = 30;
        private const int SC_MARKNUM_FOLDEROPEN = 31;
        private const int SC_MARK_BOXPLUS = 12;
        private const int SC_MARK_BOXMINUS = 14;
        private const int SC_MARK_VLINE = 9;
        private const int SC_MARK_LCORNER = 10;
        private const int SC_MARK_BOXPLUSCONNECTED = 13;
        private const int SC_MARK_BOXMINUSCONNECTED = 15;
        private const int SC_MARK_TCORNER = 11;
        private const int SCI_FOLDALL = 2662;
        private const int SCI_STYLECLEARALL = 2050;
        private const int SCI_STYLESETFORE = 2051;
        private const int SCI_STYLESETBACK = 2052;
        private const int SCI_MARKERSETFORE = 2041;
        private const int SCI_MARKERSETBACK = 2042;
        private const int SCI_MARKERSETBACKSELECTED = 2292;
        private const int SCI_MARKERSETFORETRANSLUCENT = 2294;
        private const int SCI_MARKERSETBACKTRANSLUCENT = 2295;
        private const int SCI_MARKERSETBACKSELECTEDTRANSLUCENT = 2296;
        private const int SCI_GETMODIFY = 2159;
        private const int SCI_SETSAVEPOINT = 2014;


        private const int SCI_INDICSETSTYLE = 2080;
        private const int SCI_INDICGETSTYLE = 2081;
        private const int SCI_INDICSETFORE = 2082;
        private const int SCI_INDICGETFORE = 2083;
        private const int SCI_INDICSETUNDER = 2510;
        private const int SCI_INDICGETUNDER = 2511;
        private const int SCI_INDICSETHOVERSTYLE = 2680;
        private const int SCI_INDICGETHOVERSTYLE = 2681;
        private const int SCI_INDICSETHOVERFORE = 2682;
        private const int SCI_INDICGETHOVERFORE = 2683;
        private const int INDIC_SQUIGGLE = 1;
        private const int INDIC_FULLBOX = 16;
        private const int SCI_SETCARETSTYLE = 2512;
        private const int SCI_GETCARETSTYLE = 2513;
        private const int SCI_SETINDICATORCURRENT = 2500;
        private const int SCI_GETINDICATORCURRENT = 2501;
        private const int SCI_SETINDICATORVALUE = 2502;
        private const int SCI_GETINDICATORVALUE = 2503;
        private const int SCI_INDICATORFILLRANGE = 2504;
        private const int SCI_INDICATORCLEARRANGE = 2505;
        private const int SCI_INDICATORALLONFOR = 2506;
        private const int SCI_INDICATORVALUEAT = 2507;
        private const int SCI_INDICATORSTART = 2508;
        private const int SCI_INDICSETALPHA = 2523;


        private const int SCI_ANNOTATIONSETTEXT = 2540;
        private const int SCI_ANNOTATIONGETTEXT = 2541;
        private const int SCI_ANNOTATIONSETSTYLE = 2542;
        private const int SCI_ANNOTATIONGETSTYLE = 2543;
        private const int SCI_ANNOTATIONSETSTYLES = 2544;
        private const int SCI_ANNOTATIONGETSTYLES = 2545;
        private const int SCI_ANNOTATIONGETLINES = 2546;
        private const int SCI_ANNOTATIONCLEARALL = 2547;
        private const int ANNOTATION_HIDDEN = 0;
        private const int ANNOTATION_STANDARD = 1;
        private const int ANNOTATION_BOXED = 2;
        private const int ANNOTATION_INDENTED = 3;
        private const int SCI_ANNOTATIONSETVISIBLE = 2548;
        private const int SCI_ANNOTATIONGETVISIBLE = 2549;
        private const int SCI_ANNOTATIONSETSTYLEOFFSET = 2550;
        private const int SCI_ANNOTATIONGETSTYLEOFFSET = 2551;
        private const int SCI_ALLOCATEEXTENDEDSTYLES = 2553;

        private const int SCI_SETTABINDENTS = 2260;
        private const int SCI_GETTABINDENTS = 2261;
        private const int SCI_SETBACKSPACEUNINDENTS = 2262;
        private const int SCI_GETBACKSPACEUNINDENTS = 2263;
        private const int SCI_SETTABWIDTH = 2036;
        private const int SC_MARK_BACKGROUND = 22;
        private const int SCI_SETSELECTIONSTART = 2142;
        private const int SCI_GETSELECTIONSTART = 2143;
        private const int SCI_SETSELECTIONEND = 2144;
        private const int SCI_GETSELECTIONEND = 2145;
        private const int SCI_SETEMPTYSELECTION = 2556;
        private const int SCI_GETCURRENTPOS = 2008;
        private const int SCI_SETSEL = 2160;
        private const int SCI_STARTSTYLING = 2032;
        private const int SCI_SETSTYLING = 2033;

        // indicators 
        private const int SALMON_HIGLIGHTER = 0;
        private const int GRAY_HIGLIGHTER = 1;

        // font colors
        private const int GRAY_TEXT = 250;

        // Process access rights.;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;

        // Memory allocation constants.
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;

        // Persistent static variables for the remote process buffer (for text retrieval).

        // PInvoke declarations.
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private static Dictionary<IntPtr, ScintillaEditor> editors = new();
        private static Dictionary<uint, (IntPtr address, uint size)> processBuffers = new();
        private static Dictionary<uint, IntPtr> processHandles = new();
        private static Dictionary<uint, bool> hasLexilla = new();

        private const int ANNOTATION_STYLE_OFFSET = 0x100; // Lower, more standard offset value
        private const int BASE_ANNOT_STYLE = ANNOTATION_STYLE_OFFSET;
        private const int ANNOT_STYLE_GRAY = BASE_ANNOT_STYLE;
        private const int ANNOT_STYLE_YELLOW = BASE_ANNOT_STYLE + 1;
        private const int ANNOT_STYLE_RED = BASE_ANNOT_STYLE + 2;

        public static void FixEditorTabs(ScintillaEditor editor, bool funkySQLTabs = false)
        {
            editor.SendMessage(SCI_SETTABINDENTS, (IntPtr)1, (IntPtr)0);
            var width = 4;
            if (editor.Type == EditorType.PeopleCode || (editor.Type == EditorType.SQL && funkySQLTabs))
            {
                width = 3;
            }
            editor.SendMessage(SCI_SETTABWIDTH, (IntPtr)width, (IntPtr)0);
            editor.SendMessage(SCI_SETBACKSPACEUNINDENTS, (IntPtr)1, (IntPtr)0);
        }

        public static void InitEditor(IntPtr hWnd)
        {
            var caption = WindowHelper.GetGrandparentWindowCaption(hWnd);
            // Get the process ID associated with the Scintilla window.
            GetWindowThreadProcessId(hWnd, out uint processId);

            var editor = new ScintillaEditor(hWnd, processId, caption);
            editors.Add(hWnd, editor);

            if (processHandles.ContainsKey(processId))
            {
                editor.hProc = processHandles[processId];
            }
            else
            {
                editor.hProc = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, processId);
                processHandles.Add(processId, editor.hProc);
            }

            if (hasLexilla.ContainsKey(processId))
            {
                editor.HasLexilla = hasLexilla[processId];
            }
            else
            {
                // Check if any module in the process has the name "Lexilla.dll"
                Process process = Process.GetProcessById((int)processId);
                var lexillaLoaded = process.Modules
                    .Cast<ProcessModule>()
                    .Any(module => string.Equals(module.ModuleName, "Lexilla.dll", StringComparison.OrdinalIgnoreCase));
                hasLexilla[processId] = lexillaLoaded;
                editor.HasLexilla = lexillaLoaded;
            }

            // Initialize annotations right after editor creation
            InitAnnotationStyles(editor);
        }

        public static ScintillaEditor GetEditor(IntPtr hWnd)
        {
            if (!editors.ContainsKey(hWnd))
            {
                InitEditor(hWnd);
            }
            return editors[hWnd];
        }

        public static IntPtr GetProcessBuffer(ScintillaEditor editor, uint neededSize)
        {
            var processId = editor.ProcessId;
            if (!processBuffers.ContainsKey(processId))
            {
                var processHandle = editor.hProc;
                var buffer = VirtualAllocEx(processHandle, IntPtr.Zero, neededSize, MEM_COMMIT, PAGE_READWRITE);
                processBuffers.Add(processId, (buffer, neededSize));
            }
            else
            {
                /* If buffer is too small, free current one and allocate a new one */
                var (buffer, size) = processBuffers[processId];
                if (size < neededSize)
                {
                    VirtualFreeEx(editor.hProc, buffer, 0, MEM_RELEASE);
                    buffer = VirtualAllocEx(editor.hProc, IntPtr.Zero, neededSize, MEM_COMMIT, PAGE_READWRITE);
                    processBuffers[processId] = (buffer, neededSize);
                }
            }

            return processBuffers[processId].address;
        }

        /// <summary>
        /// Retrieves the text from a Scintilla editor window in another process,
        /// reusing a persistent remote memory buffer.
        /// </summary>
        /// <param name="scintillaHwnd">The window handle of the Scintilla control.</param>
        /// <returns>The document text, or null if retrieval failed.</returns>
        public static string GetScintillaText(ScintillaEditor editor)
        {

            // Retrieve the text length.
            int textLength = (int)editor.SendMessage(SCI_GETLENGTH, IntPtr.Zero, IntPtr.Zero);
            if (textLength == 0)
                return string.Empty;

            // Determine the size needed (text length plus one for the terminating NUL).
            int neededSize = textLength + 1;

            var remoteBuffer = GetProcessBuffer(editor, (uint)neededSize);


            // Request the text. SCI_GETTEXT writes the text (with a terminating NUL) into the remote buffer.
            editor.SendMessage(SCI_GETTEXT, new IntPtr(neededSize), remoteBuffer);

            // Read the text from the remote process into a local buffer.
            byte[] buffer = new byte[neededSize];
            if (!ReadProcessMemory(editor.hProc, remoteBuffer, buffer, neededSize, out int bytesRead) || bytesRead == 0)
                return null;

            // Convert the retrieved bytes into a string (up to the first null terminator).
            int stringLength = Array.IndexOf(buffer, (byte)0);
            if (stringLength < 0)
                stringLength = buffer.Length;
            return Encoding.Default.GetString(buffer, 0, stringLength);
        }

        /// <summary>
        /// Sets the text of a Scintilla editor window in another process using SCI_SETTEXT.
        /// </summary>
        /// <param name="scintillaHwnd">The window handle of the Scintilla control.</param>
        /// <param name="text">The text to set in the editor.</param>
        /// <returns>True if the text was successfully set; false otherwise.</returns>
        public static bool SetScintillaText(ScintillaEditor editor, string text)
        {

            // Convert the new text into a byte array using the default encoding.
            // We need to include an extra byte for the terminating null.
            byte[] textBytes = Encoding.Default.GetBytes(text ?? string.Empty);
            int neededSize = textBytes.Length + 1; // +1 for the null terminator

            var remoteBuffer = GetProcessBuffer(editor, (uint)neededSize);

            // Create a buffer that includes a terminating null.
            byte[] buffer = new byte[neededSize];
            Buffer.BlockCopy(textBytes, 0, buffer, 0, textBytes.Length);
            buffer[neededSize - 1] = 0;  // Ensure null termination.

            // Write the text into the remote process's memory.
            if (!WriteProcessMemory(editor.hProc, remoteBuffer, buffer, neededSize, out int bytesWritten) || bytesWritten != neededSize)
                return false;

            // Use SCI_SETTEXT to replace the document text.
            // SCI_SETTEXT(wParam unused, lParam = pointer to null-terminated string in remote process)
            editor.SendMessage(SCI_SETTEXT, IntPtr.Zero, remoteBuffer);

            return true;
        }

        public static void SetWindowProperty(ScintillaEditor editor, string propName, string propValue)
        {
            // Calculate combined buffer size: property name + null terminator + property value + null terminator.
            int combinedSize = propName.Length + 1 + propValue.Length + 1;
            byte[] combinedBuffer = new byte[combinedSize];
            
            // Encode the strings as ASCII.
            Encoding ascii = Encoding.ASCII;
            byte[] nameBytes = ascii.GetBytes(propName);
            byte[] valueBytes = ascii.GetBytes(propValue);

            // Copy the property name and add a null terminator.
            Array.Copy(nameBytes, 0, combinedBuffer, 0, nameBytes.Length);
            combinedBuffer[nameBytes.Length] = 0;

            // Copy the property value immediately after (including its null terminator).
            Array.Copy(valueBytes, 0, combinedBuffer, nameBytes.Length + 1, valueBytes.Length);
            combinedBuffer[combinedSize - 1] = 0; // Ensure last byte is null.

            // Allocate a single remote memory block for the combined buffer.
            IntPtr remoteCombined = VirtualAllocEx(editor.hProc, IntPtr.Zero, (uint)combinedSize, MEM_COMMIT, PAGE_READWRITE);
            if (remoteCombined == IntPtr.Zero)
            {
                return;
            }

            // Write the combined buffer into the remote process.
            if (!WriteProcessMemory(editor.hProc, remoteCombined, combinedBuffer, combinedSize, out int bytesWritten) || bytesWritten != combinedSize)
            {
                VirtualFreeEx(editor.hProc, remoteCombined, 0, MEM_RELEASE);
                return;
            }

            // The property name is at the start of the buffer,
            // and the property value immediately follows the null terminator.
            IntPtr remoteName = remoteCombined;
            IntPtr remoteValue = remoteCombined + (propName.Length + 1);

            // Send the SCI_SETPROPERTY message with the remote pointers.
            editor.SendMessage(SCI_SETPROPERTY, remoteName, remoteValue);

            editor.PropertyBuffers.Add(remoteCombined);
        }

        /// <summary>
        /// Enables code folding in the Scintilla editor by setting the "fold" property to "1".
        /// This sends the SCI_SETPROPERTY message using a single remote memory block containing both
        /// the property name and value (each null terminated).
        /// </summary>
        /// <param name="scintillaHwnd">The window handle of the Scintilla control.</param>
        public static void EnableFolding(ScintillaEditor editor)
        {
            if (hasLexilla[editor.ProcessId])
            {
                SetWindowProperty(editor, "fold", "1");
            }

            // margin stuff
            editor.SendMessage(SCI_SETMARGINTYPEN, (IntPtr)2, (IntPtr)SC_MARGIN_SYMBOL);
            editor.SendMessage(SCI_SETMARGINWIDTHN, (IntPtr)2, (IntPtr)20);      // width in pixels
            editor.SendMessage(SCI_SETMARGINSENSITIVEN, (IntPtr)2, (IntPtr)1);
            editor.SendMessage(SCI_SETMARGINMASKN, (IntPtr)2, (IntPtr)SC_MASK_FOLDERS);
            // Collapse Label Style
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDER, (IntPtr)SC_MARK_BOXPLUS);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDEROPEN, (IntPtr)SC_MARK_BOXMINUS);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDEREND, (IntPtr)SC_MARK_BOXPLUSCONNECTED);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDEROPENMID, (IntPtr)SC_MARK_BOXMINUSCONNECTED);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDERMIDTAIL, (IntPtr)SC_MARK_TCORNER);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDERSUB, (IntPtr)SC_MARK_VLINE);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDERTAIL, (IntPtr)SC_MARK_LCORNER);

            // Collapse Label Color
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERSUB, 0xa0a0a0);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERMIDTAIL, 0xa0a0a0);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERTAIL, 0xa0a0a0);

            editor.SendMessage(SCI_SETFOLDFLAGS, (IntPtr)(16 | 4), (IntPtr)0); //Display a horizontal line above and below the line after folding 

            /* Use marker 21 for salmon highlight */
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)21, (IntPtr)SC_MARK_BACKGROUND);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)21, (IntPtr)0x7AA0FF);

            /* Use marker 22 for gray highlight */
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)22, (IntPtr)SC_MARK_BACKGROUND);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)22, (IntPtr)0x808080);

            /* Set up indicators */

            /* Create Salmon Highlighter */
            editor.SendMessage(SCI_INDICSETSTYLE, (IntPtr)SALMON_HIGLIGHTER, (IntPtr)INDIC_FULLBOX);
            editor.SendMessage(SCI_INDICSETFORE, (IntPtr)SALMON_HIGLIGHTER, (IntPtr)0x4DB7FF);
            editor.SendMessage(SCI_INDICSETALPHA, (IntPtr)SALMON_HIGLIGHTER, (IntPtr)0x80);
            editor.SendMessage(SCI_INDICSETUNDER, (IntPtr)SALMON_HIGLIGHTER, (IntPtr)1);

            editor.SendMessage(SCI_INDICSETSTYLE, (IntPtr)GRAY_HIGLIGHTER, (IntPtr)INDIC_FULLBOX);
            editor.SendMessage(SCI_INDICSETFORE, (IntPtr)GRAY_HIGLIGHTER, (IntPtr)0x808080);
            editor.SendMessage(SCI_INDICSETALPHA, (IntPtr)GRAY_HIGLIGHTER, (IntPtr)0x60);
            editor.SendMessage(SCI_INDICSETUNDER, (IntPtr)GRAY_HIGLIGHTER, (IntPtr)1);

            editor.FoldEnabled = true;
        }

        public static void HighlightText(ScintillaEditor editor, HighlightColor color, int start, int length)
        {
            int indicatorNumber = 0;
            switch(color)
            {
                case HighlightColor.Salmon:
                    indicatorNumber = SALMON_HIGLIGHTER;
                    break;
                case HighlightColor.Gray:
                    indicatorNumber = GRAY_HIGLIGHTER;
                    break;
            }

            editor.SendMessage(SCI_SETINDICATORCURRENT, (IntPtr)indicatorNumber, IntPtr.Zero);
            editor.SendMessage(SCI_INDICATORFILLRANGE, (IntPtr)start, (IntPtr)length);
        }

        public static void InitAnnotationStyles(ScintillaEditor editor)
        {
            try
            {
                if (editor.AnnotationStyleOffset == IntPtr.Zero)
                {
                    const int EXTRA_STYLES = 64; // Reduced from 256 to a more reasonable number
                    var newStyleOffset = editor.SendMessage(SCI_ALLOCATEEXTENDEDSTYLES, (IntPtr)EXTRA_STYLES, IntPtr.Zero);
                    editor.AnnotationStyleOffset = newStyleOffset;

                    // Set the annotation style offset
                    editor.SendMessage(SCI_ANNOTATIONSETSTYLEOFFSET, (IntPtr)newStyleOffset, IntPtr.Zero);
                }


                // Enable annotations and set visibility mode first
                editor.SendMessage(SCI_ANNOTATIONSETVISIBLE, (IntPtr)ANNOTATION_BOXED, IntPtr.Zero);

                // Define colors in BGR format
                const int GRAY_BACK = 0xEFEFEF;
                const int GRAY_FORE = 0;
                const int YELLOW_BACK = 0xF0FFFF;
                const int YELLOW_FORE = 0x0089B3;
                const int RED_BACK = 0xF0F0FF;
                const int RED_FORE = 0x000080;


                // Configure Gray style
                editor.SendMessage(SCI_STYLESETFORE, (IntPtr)(editor.AnnotationStyleOffset + (int)AnnotationStyle.Gray), (IntPtr)GRAY_FORE);
                editor.SendMessage(SCI_STYLESETBACK, (IntPtr)(editor.AnnotationStyleOffset + (int)AnnotationStyle.Gray), (IntPtr)GRAY_BACK);

                // Configure Yellow style
                editor.SendMessage(SCI_STYLESETFORE, (IntPtr)(editor.AnnotationStyleOffset + (int)AnnotationStyle.Yellow), (IntPtr)YELLOW_FORE);
                editor.SendMessage(SCI_STYLESETBACK, (IntPtr)(editor.AnnotationStyleOffset + (int)AnnotationStyle.Yellow), (IntPtr)YELLOW_BACK);

                // Configure Red style
                editor.SendMessage(SCI_STYLESETFORE, (IntPtr)(editor.AnnotationStyleOffset + (int)AnnotationStyle.Red), (IntPtr)RED_FORE);
                editor.SendMessage(SCI_STYLESETBACK, (IntPtr)(editor.AnnotationStyleOffset + (int)AnnotationStyle.Red), (IntPtr)RED_BACK);

                // Store initialization state
                editor.AnnotationsInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize annotations: {ex.Message}");
                editor.AnnotationsInitialized = false;
            }
        }

        /// <summary>
        /// Optionally call this method to clean up persistent resources when your application exits.
        /// </summary>
        public static void Cleanup()
        {
            foreach (var editor in editors.Values)
            {


                /* Free all annotation strings */
                foreach (var v in editor.AnnotationPointers.Values)
                {
                    if (v != IntPtr.Zero)
                    {
                        VirtualFreeEx(editor.hProc, v, 0, MEM_RELEASE);
                    }
                }

                foreach (var v in editor.PropertyBuffers)
                {
                    if (v != IntPtr.Zero)
                    {
                        VirtualFreeEx(editor.hProc, v, 0, MEM_RELEASE);
                    }
                }
                editor.AnnotationPointers.Clear();
                editor.PropertyBuffers.Clear();
                editor.Caption = null;
                editor.ContentString = null;

                CloseHandle(editor.hProc);
                
            }
            editors.Clear();
        }

        public static void ContractTopLevel(ScintillaEditor editor)
        {
            editor.SendMessage(SCI_FOLDALL, (IntPtr)0, (IntPtr)0);
        }

        public static void ExpandTopLevel(ScintillaEditor editor)
        {
            editor.SendMessage(SCI_FOLDALL, (IntPtr)1, (IntPtr)0);
        }

        static int styleIndex = 250;
        internal static void SetDarkMode(ScintillaEditor editor)
        {
            /* Style 0 - whitespace */
            /* Style 1 - comments */
            /* style 2 - numbers */
            /* Style 3 - keywords1 */
            /* style 4 - strings */
            /* Style 6 - symbols like . and () and ; */
            /* Style 7 is literals */
            /* Style 10 is keywords2 */
            /* Style 11 is keywords3 */
            /* Style 24 is block comments */
            /* Style 32 is main background */
            /* Style 33 is margins! */
            //foreach (var s in styles)

            editor.SendMessage(SCI_STYLESETBACK, (IntPtr)32, (IntPtr)0x1E1E1E);
            editor.SendMessage(SCI_STYLESETBACK, (IntPtr)33, (IntPtr)0x1E1E1E);
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)32, (IntPtr)0xD4D4D4);
            editor.SendMessage(SCI_STYLECLEARALL, (IntPtr)0, (IntPtr)0);

            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)3, (IntPtr)0xD69C56);
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)4, (IntPtr)0x7891CE);
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)10, (IntPtr)0x9CDCFE);
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)11, (IntPtr)0xDCDCAA);
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)1, (IntPtr)0x55996A);
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)24, (IntPtr)0x55996A);
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)2, (IntPtr)0xA8CEB5);

        }

        internal static bool IsEditorClean(ScintillaEditor editor)
        {
            return editor.SendMessage(SCI_GETMODIFY, (IntPtr)0, (IntPtr)0) == (IntPtr)0;
        }

        internal static int GetContentHash(ScintillaEditor editor)
        {
            /* make crc32 hash of content string */
            var content = GetScintillaText(editor);
            editor.ContentString = content;
            return GetContentHashFromString(content);
        }

        internal static int GetContentHashFromString(string content)
        {
            /* make crc32 hash of content string */
            return (int)Crc32.HashToUInt32(Encoding.UTF8.GetBytes(content));
        }

        internal static void SetFoldRegions(ScintillaEditor editor)
        {
           
            if (editor.ContentString == null)
            {
                editor.ContentString = GetScintillaText(editor);
            }

            // Split the document text into lines.
            // This handles both Windows (CRLF) and Unix (LF) line endings.
            string[] lines = editor.ContentString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int lineCount = lines.Length;

            // Precompute an indent value for each line.
            // The indent value here includes our base fold level (SC_FOLDLEVELBASE)
            // plus a count of leading whitespace. For blank lines, we set the white flag.
            int[] indentAmounts = new int[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                indentAmounts[i] = GetIndentAmount(lines[i]);
            }

            // Process each line to calculate and set its fold level.
            for (int line = 0; line < lineCount; line++)
            {
                int indentCurrent = indentAmounts[line];
                // Start with the current line's indent as the base fold level.
                int level = indentCurrent;

                // Get the indent for the next line, if any.
                int indentNext = (line + 1 < lineCount) ? indentAmounts[line + 1] : indentCurrent;

                // Only non-blank lines (those without the white flag) may be fold headers.
                if ((indentCurrent & SC_FOLDLEVELWHITEFLAG) == 0)
                {
                    // If the next line is indented more than the current line,
                    // mark the current line as a header.
                    if ((indentCurrent & SC_FOLDLEVELNUMBERMASK) < (indentNext & SC_FOLDLEVELNUMBERMASK))
                    {
                        level |= SC_FOLDLEVELHEADERFLAG;
                    }
                    // Otherwise, if the next line is blank then check the line after that.
                    else if ((indentNext & SC_FOLDLEVELWHITEFLAG) != 0)
                    {
                        int indentNext2 = (line + 2 < lineCount) ? indentAmounts[line + 2] : indentCurrent;
                        if ((indentCurrent & SC_FOLDLEVELNUMBERMASK) < (indentNext2 & SC_FOLDLEVELNUMBERMASK))
                        {
                            level |= SC_FOLDLEVELHEADERFLAG;
                        }
                    }
                }

                // Send the message to set the fold level for this line.
                // The wParam is the line number, and lParam is the fold level value.
                editor.SendMessage(SCI_SETFOLDLEVEL, (IntPtr)line, (IntPtr)level);
            }
        }

        /// <summary>
        /// Calculates the “indent” amount for a given line.
        /// Returns a value that is SC_FOLDLEVELBASE + number of leading whitespace characters.
        /// For blank or whitespace-only lines, the white flag is also set.
        /// </summary>
        /// <param name="line">A single line of text.</param>
        /// <returns>An int representing the indent level with appropriate flags.</returns>
        private static int GetIndentAmount(string line)
        {
            // If the line is blank or consists only of whitespace,
            // mark it with the SC_FOLDLEVELWHITEFLAG.
            if (string.IsNullOrWhiteSpace(line))
            {
                return SC_FOLDLEVELWHITEFLAG | SC_FOLDLEVELBASE;
            }

            int indentCount = 0;
            foreach (char ch in line)
            {
                if (ch == ' ' || ch == '\t')
                {
                    // You may wish to treat tabs as more than one space.
                    indentCount++;
                }
                else
                {
                    break;
                }
            }
            return SC_FOLDLEVELBASE + indentCount;
        }
        static FormatConfig formatConfig = FormatConfig.Builder().Indent("  ")
            .Uppercase(true)
            .LinesBetweenQueries(2)
            .MaxColumnLength(80)
            .Build();

        internal static void ApplyBetterSQL(ScintillaEditor editor)
        {

            var formatted = SqlFormatter.Of(Dialect.StandardSql)
                .Extend(cfg => cfg.PlusSpecialWordChars("%"))
                .Format(editor.ContentString, formatConfig);
            editor.ContentString = formatted;
            SetScintillaText(editor, formatted);
            editor.SendMessage(SCI_SETSAVEPOINT, (IntPtr)0, (IntPtr)0);

            /* update local content hash */
            editor.LastContentHash = GetContentHashFromString(formatted);
        }

        internal static void ApplySquiggles(ScintillaEditor editor)
        {
            /* set indicator to indicator 0 */
            editor.SendMessage(SCI_SETINDICATORCURRENT, (IntPtr)0, (IntPtr)0);

            /* fill in the first 10 characters */
            editor.SendMessage(SCI_INDICATORFILLRANGE, (IntPtr)0, (IntPtr)10);

            SetAnnotation(editor, 0, "Import is not needed");
        }



        internal static void SetAnnotation(ScintillaEditor editor, int line, string text, AnnotationStyle style = AnnotationStyle.Gray)
        {
            if (!editor.AnnotationsInitialized)
            {
                InitAnnotationStyles(editor);
            }

            try
            {
                // If pointer for this exact text exists, use it
                var pointer = editor.AnnotationPointers.ContainsKey(text) ? editor.AnnotationPointers[text] : IntPtr.Zero;
                if (pointer != IntPtr.Zero)
                {
                    editor.SendMessage(SCI_ANNOTATIONSETSTYLE, (IntPtr)line, (IntPtr)((int)style));
                    editor.SendMessage(SCI_ANNOTATIONSETTEXT, (IntPtr)line, pointer);
                    return;
                }

                // Allocate memory for new annotation text
                var textBytes = Encoding.Default.GetBytes(text);
                var neededSize = textBytes.Length + 1;
                var remoteBuffer = VirtualAllocEx(editor.hProc, IntPtr.Zero, (uint)neededSize, MEM_COMMIT, PAGE_READWRITE);
                
                if (remoteBuffer == IntPtr.Zero)
                {
                    Debug.WriteLine($"Failed to allocate memory for annotation: {Marshal.GetLastWin32Error()}");
                    return;
                }

                if (!WriteProcessMemory(editor.hProc, remoteBuffer, textBytes, neededSize, out int bytesWritten) || bytesWritten != neededSize)
                {
                    VirtualFreeEx(editor.hProc, remoteBuffer, 0, MEM_RELEASE);
                    Debug.WriteLine($"Failed to write annotation text to memory: {Marshal.GetLastWin32Error()}");
                    return;
                }

                editor.AnnotationPointers[text] = remoteBuffer;
                editor.SendMessage(SCI_ANNOTATIONSETSTYLE, (IntPtr)line, (IntPtr)((int)style));
                editor.SendMessage(SCI_ANNOTATIONSETTEXT, (IntPtr)line, remoteBuffer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting annotation: {ex.Message}");
            }
        }

        public static int GetSelectionLength(ScintillaEditor editor)
        {
            var selectionStart = editor.SendMessage(SCI_GETSELECTIONSTART, IntPtr.Zero, IntPtr.Zero);
            var selectionEnd = editor.SendMessage(SCI_GETSELECTIONEND, IntPtr.Zero, IntPtr.Zero);
            return (int)selectionEnd - (int)selectionStart;
        }

        public static void SetSelection(ScintillaEditor editor, int startIndex, int endIndex)
        {
            editor.SendMessage(SCI_SETSEL, (IntPtr)startIndex, (IntPtr)endIndex);
        }
        public static void ColorText(ScintillaEditor editor, FontColor color, int start, int length)
        {
            // Define color constants using BGR format (Scintilla uses BGR)
            int foreColor;
            switch (color)
            {
                case FontColor.Gray:
                    foreColor = 0x808080; // Gray in BGR format
                    break;
                default:
                    foreColor = 0x000000; // Black in BGR format
                    break;
            }

            // Create a temporary style for colored text
            const int TEMP_STYLE = 35; // Using a high style number to avoid conflicts

            // Set the style's foreground color
            //editor.SendMessage(SCI_STYLESETFORE, (IntPtr)TEMP_STYLE, (IntPtr)foreColor);

            // Start styling at the specified position
            editor.SendMessage(SCI_STARTSTYLING, (IntPtr)start, 0);

            editor.SendMessage(SCI_SETSTYLING, (IntPtr)length, (IntPtr)TEMP_STYLE);
        }

        public static void ClearAnnotations(ScintillaEditor editor)
        {
            // Clear all annotations
            editor.SendMessage(SCI_ANNOTATIONCLEARALL, IntPtr.Zero, IntPtr.Zero);

            // Free all annotation strings
            foreach (var pointer in editor.AnnotationPointers.Values)
            {
                if (pointer != IntPtr.Zero)
                {
                    VirtualFreeEx(editor.hProc, pointer, 0, MEM_RELEASE);
                }
            }

            // Clear the annotation pointers dictionary
            editor.AnnotationPointers.Clear();
        }

        /// <summary>
        /// Sets multiple annotations on a single line with different styles per annotation.
        /// Each character in the combined annotation text can have its own style.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance</param>
        /// <param name="annotations">List of annotation strings to combine</param>
        /// <param name="line">Line number to add annotations to</param>
        /// <param name="styles">List of styles matching the annotations list length</param>
        public static void SetAnnotations(ScintillaEditor editor, List<string> annotations, int line, List<AnnotationStyle> styles)
        {
            if (annotations.Count != styles.Count)
            {
                throw new ArgumentException("Number of annotations must match number of styles");
            }

            InitAnnotationStyles(editor);

            try
            {
                // First construct complete strings with prefixes to match exact byte layout
                var formattedAnnotations = annotations.Select(a => $"^^ {a}").ToList();
                var combinedText = string.Join("\n", formattedAnnotations);
                var textBytes = Encoding.Default.GetBytes(combinedText);
                var neededSize = textBytes.Length + 1; // +1 for null terminator

                // Create style bytes array matching exact text bytes length
                var styleBytes = new byte[neededSize];
                int currentPos = 0;
                
                // Fill style bytes array for each formatted annotation including prefix and newline
                for (int i = 0; i < formattedAnnotations.Count; i++)
                {
                    var annotationWithPrefix = formattedAnnotations[i];
                    var bytesForThisLine = Encoding.Default.GetByteCount(annotationWithPrefix);
                    var styleValue = (byte)((int)styles[i]);
                    
                    // Style all bytes for this line including prefix
                    for (int j = 0; j < bytesForThisLine; j++)
                    {
                        styleBytes[currentPos++] = styleValue;
                    }

                    // Add style for newline character if not the last annotation
                    if (i < formattedAnnotations.Count - 1)
                    {
                        styleBytes[currentPos++] = styleValue;
                    }
                }
                // Set last byte as null terminator style
                styleBytes[neededSize - 1] = styleBytes[Math.Max(0, neededSize - 2)];

                // Allocate and write the annotation text buffer
                var remoteTextBuffer = VirtualAllocEx(editor.hProc, IntPtr.Zero, (uint)neededSize, MEM_COMMIT, PAGE_READWRITE);
                if (remoteTextBuffer == IntPtr.Zero)
                {
                    throw new Exception($"Failed to allocate text buffer: {Marshal.GetLastWin32Error()}");
                }

                // Allocate and write the styles buffer
                var remoteStyleBuffer = VirtualAllocEx(editor.hProc, IntPtr.Zero, (uint)neededSize, MEM_COMMIT, PAGE_READWRITE);
                if (remoteStyleBuffer == IntPtr.Zero)
                {
                    VirtualFreeEx(editor.hProc, remoteTextBuffer, 0, MEM_RELEASE);
                    throw new Exception($"Failed to allocate style buffer: {Marshal.GetLastWin32Error()}");
                }

                // Write the text and styles to remote buffers
                if (!WriteProcessMemory(editor.hProc, remoteTextBuffer, textBytes, neededSize, out int bytesWritten) ||
                    !WriteProcessMemory(editor.hProc, remoteStyleBuffer, styleBytes, neededSize, out int stylesBytesWritten))
                {
                    VirtualFreeEx(editor.hProc, remoteTextBuffer, 0, MEM_RELEASE);
                    VirtualFreeEx(editor.hProc, remoteStyleBuffer, 0, MEM_RELEASE);
                    throw new Exception("Failed to write to remote buffers");
                }

                // Set the annotation text and styles
                editor.SendMessage(SCI_ANNOTATIONSETTEXT, (IntPtr)line, remoteTextBuffer);
                editor.SendMessage(SCI_ANNOTATIONSETSTYLES, (IntPtr)line, remoteStyleBuffer);

                // Store buffers for cleanup
                editor.AnnotationPointers[combinedText] = remoteTextBuffer;
                editor.AnnotationPointers[combinedText + "_styles"] = remoteStyleBuffer;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting annotations: {ex.Message}");
                throw;
            }
        }
    }
    public enum EditorType
    {
        PeopleCode, HTML, SQL, CSS, Other
    }

    public class ScintillaEditor
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public IntPtr hWnd;
        public IntPtr hProc;
        public uint ProcessId;
        public string Caption = "";
        public bool FoldEnabled = false;
        public bool HasLexilla = false;

        public EditorType Type;

        public Dictionary<string, IntPtr> AnnotationPointers = new();
        public List<IntPtr> PropertyBuffers = new();

        public int LastContentHash { get; set; }
        public string? ContentString = null;
        public string? SnapshotText = null;
        public bool AnnotationsInitialized { get; set; } = false;
        public IntPtr AnnotationStyleOffset = IntPtr.Zero;
        public ScintillaEditor(IntPtr hWnd, uint procID, string caption)
        {
            this.hWnd = hWnd;
            ProcessId = procID;
            Caption = caption;

            if (caption.Contains("PeopleCode"))
            {
                Type = EditorType.PeopleCode;
            }
            else if (caption.Contains("(HTML)"))
            {
                Type = EditorType.HTML;
            }
            else if (caption.Contains("(SQL Definition)"))
            {
                Type = EditorType.SQL;
            }
            else if (caption.Contains("(StyleSheet)"))
            {
                Type = EditorType.CSS;
            }
            else
            {
                Type = EditorType.Other;
            }

            // TODO: move these into manager method
            //FoldEnabled = ScintillaHelper.GetWindowPropertyInt(handle, "ninja") == 1;
            //HasLexilla = ScintillaHelper.IsLexillaLoaded(handle);
        }

        public IntPtr SendMessage(int Msg, IntPtr wParam, IntPtr lParam)
        {
            return SendMessage(hWnd, Msg, wParam, lParam);
        }

        public override string ToString()
        {
            return Caption;
        }
    }
    public enum HighlightColor
    {
        Salmon = 0,
        Gray = 1
    }

    public enum FontColor
    {
        Gray = 0
    }
}
