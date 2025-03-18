using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using System.IO.Hashing;
using SQL.Formatter.Core;
using SQL.Formatter.Language;
using SQL.Formatter;
using AppRefiner.Database;
using System.CodeDom;

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
        private const int SC_MARK_FULLRECT = 26;
        private const int SCI_GETLINEENDPOSITION = 2136;
        private const int SC_MARK_BOXPLUS = 12;
        private const int SC_MARK_BOXMINUS = 14;
        private const int SC_MARK_VLINE = 9;
        private const int SC_MARK_LCORNER = 10;
        private const int SC_MARK_BOXPLUSCONNECTED = 13;
        private const int SC_MARK_BOXMINUSCONNECTED = 15;
        private const int SC_MARK_TCORNER = 11;
        private const int SCI_FOLDALL = 2662;
        private const int SCI_TOGGLEFOLD = 2231;
        private const int SCI_FOLDLINE = 2237;
        private const int SCI_GETCURRENTPOS = 2008;
        private const int SCI_LINEFROMPOSITION = 2166;
        private const int SCI_GETFOLDLEVEL = 2223;
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
        private const int SCI_SETFOLDMARGINCOLOUR = 2290;
        private const int SCI_SETFOLDMARGINHICOLOUR = 2291;

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
        private const int SCI_SETSEL = 2160;
        private const int SCI_STARTSTYLING = 2032;
        private const int SCI_SETSTYLING = 2033;
        private const int SCI_CLEARDOCUMENTSTYLE = 2005;
        private const int SCI_COLOURISE = 4003;
        private const int SCI_GOTOPOS = 2025;
        private const int SCI_SCROLLCARET = 2169;
        private const int SCI_SETCARETFORE = 2069;
        private const int SCI_SETCARETWIDTH = 2188;
        private const int SCI_SETSELFORE = 2067;
        private const int SCI_SETSELBACK = 2068;
        private const int SCI_SETSELALPHA = 2478;
        private const int SCI_SETELEMENTCOLOUR = 2753;
        
        // Element identifiers
        private const int SC_ELEMENT_SELECTION_BACK = 31;
        private const int SC_ELEMENT_SELECTION_TEXT = 32;


        // indicators 
        private const int SALMON_HIGLIGHTER = 0;
        private const int GRAY_HIGLIGHTER = 1;
        private const int BLUE_HIGLIGHTER = 2;
        private const int LINTER_SUPPRESSION_HIGHLIGHTER = 3;

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
        private static HashSet<uint> ProcessesWithDarkMode = new();

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
            if (caption == "Suppress")
            {
                Thread.Sleep(1000);
                caption = WindowHelper.GetGrandparentWindowCaption(hWnd);
            }
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
        public static string? GetScintillaText(ScintillaEditor editor)
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
            editor.SendMessage(SCI_SETMARGINMASKN, (IntPtr)2, unchecked((IntPtr)SC_MASK_FOLDERS));
            // Collapse Label Style
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDER, (IntPtr)SC_MARK_BOXPLUS);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDEROPEN, (IntPtr)SC_MARK_BOXMINUS);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDEREND, (IntPtr)SC_MARK_BOXPLUSCONNECTED);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDEROPENMID, (IntPtr)SC_MARK_BOXMINUSCONNECTED);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDERMIDTAIL, (IntPtr)SC_MARK_TCORNER);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDERSUB, (IntPtr)SC_MARK_VLINE);
            editor.SendMessage(SCI_MARKERDEFINE, (IntPtr)SC_MARKNUM_FOLDERTAIL, (IntPtr)SC_MARK_LCORNER);

            // Collapse Label Color
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDER, (IntPtr)0x0);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDEROPEN, (IntPtr)0x0);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDEREND, (IntPtr)0x0);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDEROPENMID, (IntPtr)0x0);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDERMIDTAIL, (IntPtr)0x0);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDERSUB, (IntPtr)0x0);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDERTAIL, (IntPtr)0x0);

            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDER, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDEROPEN, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDEREND, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDEROPENMID, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDERMIDTAIL, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDERSUB, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDERTAIL, (IntPtr)0xFFFFFF);



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

            /* Create Blue Highlighter */
            editor.SendMessage(SCI_INDICSETSTYLE, (IntPtr)BLUE_HIGLIGHTER, (IntPtr)INDIC_FULLBOX);
            editor.SendMessage(SCI_INDICSETFORE, (IntPtr)BLUE_HIGLIGHTER, (IntPtr)0xD9D6A5); /* Blue-orange in BGR format */
            editor.SendMessage(SCI_INDICSETALPHA, (IntPtr)BLUE_HIGLIGHTER, (IntPtr)0x60);
            editor.SendMessage(SCI_INDICSETUNDER, (IntPtr)BLUE_HIGLIGHTER, (IntPtr)1);

            /* Create Linter Suppression Highlighter */
            editor.SendMessage(SCI_INDICSETSTYLE, (IntPtr)LINTER_SUPPRESSION_HIGHLIGHTER, (IntPtr)INDIC_FULLBOX);
            editor.SendMessage(SCI_INDICSETFORE, (IntPtr)LINTER_SUPPRESSION_HIGHLIGHTER, (IntPtr)0x50CB50); /* Green in BGR format */
            editor.SendMessage(SCI_INDICSETALPHA, (IntPtr)LINTER_SUPPRESSION_HIGHLIGHTER, (IntPtr)0x70);
            editor.SendMessage(SCI_INDICSETUNDER, (IntPtr)LINTER_SUPPRESSION_HIGHLIGHTER, (IntPtr)1);

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
                case HighlightColor.Blue:
                    indicatorNumber = BLUE_HIGLIGHTER;
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

                // Define colors in BGR format based on the mode
                int GRAY_BACK, GRAY_FORE, YELLOW_BACK, YELLOW_FORE, RED_BACK, RED_FORE;
                
                if (editor.IsDarkMode)
                {
                    // Dark mode colors
                    GRAY_BACK = 0x303030;   // Dark gray background
                    GRAY_FORE = 0xCCCCCC;   // Light gray text
                    YELLOW_BACK = 0x2D2D54; // Dark blue/yellow background
                    YELLOW_FORE = 0x4DC4FF; // Light orange/amber text
                    RED_BACK = 0x252550;    // Dark blue/red background  
                    RED_FORE = 0x7070FF;    // Light red text
                }
                else
                {
                    // Light mode colors (original)
                    GRAY_BACK = 0xEFEFEF;   // Light gray background
                    GRAY_FORE = 0;          // Black text
                    YELLOW_BACK = 0xF0FFFF; // Light yellow background 
                    YELLOW_FORE = 0x0089B3; // Brown text
                    RED_BACK = 0xF0F0FF;    // Light red background
                    RED_FORE = 0x000080;    // Dark red text
                }


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

        public static void CleanupEditor(ScintillaEditor editor)
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

        public static void CollapseTopLevel(ScintillaEditor editor)
        {
            editor.SendMessage(SCI_FOLDALL, (IntPtr)0, (IntPtr)0);
        }
        public static void ExpandTopLevel(ScintillaEditor editor)
        {
            editor.SendMessage(SCI_FOLDALL, (IntPtr)1, (IntPtr)0);
        }

        internal static void SetLineFoldStatus(ScintillaEditor activeEditor, bool folded)
        {
            // Get current cursor position
            int cursorPos = (int)activeEditor.SendMessage(SCI_GETCURRENTPOS, IntPtr.Zero, IntPtr.Zero);

            // Get line number from position
            int lineNum = (int)activeEditor.SendMessage(SCI_LINEFROMPOSITION, (IntPtr)cursorPos, IntPtr.Zero);

            // Check if the line is a fold point and toggle it
            int foldLevel = (int)activeEditor.SendMessage(SCI_GETFOLDLEVEL, (IntPtr)lineNum, IntPtr.Zero);
            int currentLineLevel = foldLevel & SC_FOLDLEVELNUMBERMASK;
            // Check if the line is a fold header (can be folded)
            if ((foldLevel & SC_FOLDLEVELHEADERFLAG) != 0)
            {
                // Toggle the fold (will collapse if expanded)
                activeEditor.SendMessage(SCI_FOLDLINE, (IntPtr)lineNum, folded ? 0 : 1);
            }
            else
            {
                /* If the line is not a fold header, try to find the fold header */
                int parentLine = lineNum - 1;
                while (parentLine >= 0)
                {
                    foldLevel = (int)activeEditor.SendMessage(SCI_GETFOLDLEVEL, (IntPtr)parentLine, IntPtr.Zero);
                    if ((foldLevel & SC_FOLDLEVELHEADERFLAG) != 0 && (foldLevel & SC_FOLDLEVELNUMBERMASK) < currentLineLevel)
                    {
                        activeEditor.SendMessage(SCI_FOLDLINE, (IntPtr)parentLine, (IntPtr)(folded ? 0 : 1));
                        /* Set cursor to the end of the parent line */
                        activeEditor.SendMessage(SCI_GOTOPOS, (IntPtr)activeEditor.SendMessage(SCI_GETLINEENDPOSITION, (IntPtr)parentLine, IntPtr.Zero), IntPtr.Zero);
                        break;
                    }
                    parentLine--;
                }

                
            }
        }

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

            // Darker background for better contrast
            editor.SendMessage(SCI_STYLESETBACK, (IntPtr)32, (IntPtr)0x1A1A1A);
            editor.SendMessage(SCI_STYLESETBACK, (IntPtr)33, (IntPtr)0x1A1A1A);
            // Brighter text for better readability
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)32, (IntPtr)0xE8E8E8);
            editor.SendMessage(SCI_STYLECLEARALL, (IntPtr)0, (IntPtr)0);

            // Keywords (orange in Visual Studio Code style)
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)3, (IntPtr)0x2C8BE2);
            // Strings (green)
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)4, (IntPtr)0x64D356);
            // Secondary keywords (cyan/light blue)
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)10, (IntPtr)0xD5AA6F);
            // Tertiary keywords (softer blue/teal)
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)11, (IntPtr)0xC7953B);
            // Comments (green-gray)
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)1, (IntPtr)0x6A9955);
            // Block comments (same as line comments)
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)24, (IntPtr)0x6A9955);
            // Numbers (purplish blue)
            editor.SendMessage(SCI_STYLESETFORE, (IntPtr)2, (IntPtr)0xB682AA);
            
            // Set white cursor color (hex FFFFFF)
            editor.SendMessage(SCI_SETCARETFORE, (IntPtr)0xFFFFFF, IntPtr.Zero);
    
            // Make cursor slightly wider for better visibility
            editor.SendMessage(SCI_SETCARETWIDTH, (IntPtr)2, IntPtr.Zero);
    
            // Set selection colors: navy blue background with white text
            editor.SendMessage(SCI_SETELEMENTCOLOUR, (IntPtr)SC_ELEMENT_SELECTION_BACK, (IntPtr)0xC19429FF); // Navy blue in BGR (800000 = RGB 0,0,128)
            editor.SendMessage(SCI_SETELEMENTCOLOUR, (IntPtr)SC_ELEMENT_SELECTION_TEXT, (IntPtr)0xFF00FFFF); // White text (hex FFFFFF)

            /* fold margin colors */
            // Collapse Label Style
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDER, (IntPtr)0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDEROPEN, (IntPtr)0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDEREND, (IntPtr)0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDEROPENMID, (IntPtr)0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDERMIDTAIL, (IntPtr)0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDERSUB, (IntPtr)0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, (IntPtr)SC_MARKNUM_FOLDERTAIL, (IntPtr)0x1A1A1A);

            // Collapse Label Color
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERSUB, 0x0);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERMIDTAIL, 0x0);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERTAIL, 0x0);

            editor.SendMessage(SCI_MARKERSETBACK, SC_MARK_FULLRECT, 0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARK_BACKGROUND , 0x1A1A1A);

            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDER, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDEROPEN, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDEREND, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDEROPENMID, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDERMIDTAIL, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDERSUB, (IntPtr)0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, (IntPtr)SC_MARKNUM_FOLDERTAIL, (IntPtr)0xFFFFFF);

            // Set dark mode flag
            editor.IsDarkMode = true;
            
            // Also update annotation styles for dark mode
            InitAnnotationStyles(editor);


            if (!ProcessesWithDarkMode.Contains(editor.ProcessId))
            {
                DarkModeHelper.ApplyDarkModeToControls((int)editor.ProcessId);
                ProcessesWithDarkMode.Add(editor.ProcessId);
            }

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
            if (content != null)
            {
                return GetContentHashFromString(content);
            } else
            {
                return 0;
            }
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
                if (editor.ContentString == null) { return; }
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
            if (string.IsNullOrEmpty(line))
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
            editor.ContentString ??= GetScintillaText(editor);

            var formatted = SqlFormatter.Of(Dialect.StandardSql)
                .Extend(cfg => cfg.PlusSpecialWordChars("%").PlusNamedPlaceholderTypes(new string[] { ":" }).PlusOperators(new string[] {"%Concat"}))
                .Format(editor.ContentString, formatConfig).Replace("\n", "\r\n");
            if (string.IsNullOrEmpty(formatted))
            {
                return;
            }
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

        /// <summary>
        /// Sets the selection range in the Scintilla editor
        /// </summary>
        /// <param name="editor">The editor to set the selection in</param>
        /// <param name="startIndex">The starting position of the selection</param>
        /// <param name="endIndex">The ending position of the selection</param>
        public static void SetSelection(ScintillaEditor editor, int startIndex, int endIndex)
        {
            editor.SendMessage(SCI_SETSEL, (IntPtr)startIndex, (IntPtr)endIndex);
        }
        
        public static void ColorText(ScintillaEditor editor, FontColor color, int start, int length)
        { 
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

        internal static void ResetStyles(ScintillaEditor activeEditor)
        {
            activeEditor.SendMessage(SCI_CLEARDOCUMENTSTYLE, (IntPtr)0, (IntPtr)0);
            var docLength = activeEditor.SendMessage(SCI_GETLENGTH, 0, 0);
            activeEditor.SendMessage(SCI_COLOURISE, 0, docLength);
        }

        internal static int GetCursorPosition(ScintillaEditor? activeEditor)
        {
            if (activeEditor == null) return -1;
            return (int)activeEditor.SendMessage(SCI_GETCURRENTPOS, 0, 0);
        }
        
        /// <summary>
        /// Gets the project name for the current editor
        /// </summary>
        /// <param name="editor">The editor to get the project name for</param>
        /// <returns>The project name or a default name if it cannot be determined</returns>
        public static string GetProjectName(ScintillaEditor editor)
        {
            // Get the caption from the main window of the editor's process
            string caption = WindowHelper.GetMainWindowCaption(editor.ProcessId);
            
            if (!string.IsNullOrEmpty(caption))
            {
                // Split the caption on "-" character
                string[] parts = caption.Split('-');
                
                // Check if we have at least 3 parts (to access the third item)
                if (parts.Length >= 3)
                {
                    // Return the third part, trimmed
                    return parts[2].Trim();
                }
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Sets the cursor position in the Scintilla editor
        /// </summary>
        /// <param name="editor">The editor to set the cursor position in</param>
        /// <param name="position">The position to place the cursor</param>
        public static void SetCursorPosition(ScintillaEditor editor, int position)
        {
            if (editor == null) return;
            
            // Set the cursor position
            editor.SendMessage(SCI_GOTOPOS, (IntPtr)position, IntPtr.Zero);
            
            // Ensure the position is visible by scrolling to it
            editor.SendMessage(SCI_SCROLLCARET, IntPtr.Zero, IntPtr.Zero);
        }

        internal static void ExpandCurrent(ScintillaEditor activeEditor)
        {
            // Get current cursor position
            int cursorPos = (int)activeEditor.SendMessage(SCI_GETCURRENTPOS, IntPtr.Zero, IntPtr.Zero);
            
            // Get line number from position
            int lineNum = (int)activeEditor.SendMessage(SCI_LINEFROMPOSITION, (IntPtr)cursorPos, IntPtr.Zero);
            
            // Check if the line is a fold point and toggle it
            int foldLevel = (int)activeEditor.SendMessage(SCI_GETFOLDLEVEL, (IntPtr)lineNum, IntPtr.Zero);
            
            // Check if the line is a fold header (can be folded)
            if ((foldLevel & SC_FOLDLEVELHEADERFLAG) != 0)
            {
                // Toggle the fold (will expand if collapsed)
                activeEditor.SendMessage(SCI_TOGGLEFOLD, (IntPtr)lineNum, IntPtr.Zero);
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

        public int? SnapshotCursorPosition { get; set; }
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        public IntPtr hWnd;
        public IntPtr hProc;
        public uint ProcessId;
        public string? Caption = null;
        public bool FoldEnabled = false;
        public bool HasLexilla = false;

        public EditorType Type;

        public Dictionary<string, IntPtr> AnnotationPointers = new();
        public List<IntPtr> PropertyBuffers = new();

        public int LastContentHash { get; set; }
        public string? ContentString = null;
        public string? SnapshotText = null;
        public bool AnnotationsInitialized { get; set; } = false;
        public bool IsDarkMode { get; set; } = false;
        public IntPtr AnnotationStyleOffset = IntPtr.Zero;
        public IDataManager? DataManager = null;

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

        public bool IsValid()
        {
            return IsWindow(hWnd);
        }
    }
    public enum HighlightColor
    {
        Salmon = 0,
        Gray = 1,
        Blue = 2,
        LinterSuppression = 3
    }

    public enum FontColor
    {
        Gray = 0
    }
}
