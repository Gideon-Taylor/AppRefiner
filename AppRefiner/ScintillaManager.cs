using AppRefiner.Database;
using AppRefiner.Linters;
using AppRefiner.Stylers;
using SQL.Formatter;
using SQL.Formatter.Core;
using SQL.Formatter.Language;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using AppRefiner.TooltipProviders;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using AppRefiner.PeopleCode;
using Antlr4.Runtime.Atn;
using static AppRefiner.AutoCompleteService;
using DiffPlex.Model;
using System.Threading.Tasks;
using AppRefiner.Dialogs;
using static SqlParser.Ast.MatchRecognizeSymbol;

namespace AppRefiner
{
    public enum AnnotationStyle
    {
        Gray = 0,
        Yellow = 1,
        Red = 2
    }

    public class SearchMatch
    {
        public int Position { get; set; }
        public int Length { get; set; }
        public int LineNumber { get; set; }
        public string LineText = "";
        

        public override string ToString()
        {
            // Format: "Line 42: function myFunction() {"
            string displayText = LineText?.Trim() ?? "";
            if (displayText.Length > 80)
            {
                displayText = displayText.Substring(0, 77) + "...";
            }
            return $"Line {LineNumber}: {displayText}";
        }
    }

    public class ScintillaManager
    {
        // Dialog tracking for singleton behavior
        private static readonly ConcurrentDictionary<ScintillaEditor, Dialogs.BetterFindDialog> _activeDialogs = new();

        // Scintilla messages.
        private const int SCI_SETTEXT = 2181;
        private const int SCI_GETTEXT = 2182;
        private const int SCI_INSERTTEXT = 2003;
        private const int SCI_GETLENGTH = 2006;
        private const int SCI_SETPROPERTY = 4004;
        private const int SCI_SETFOLDLEVEL = 2222;
        private const int SCI_GETFOLDEXPANDED = 2230;
        private const int SCI_REPLACESEL = 2170;
        private const int SC_FOLDLEVELBASE = 0x400;
        private const int SC_FOLDLEVELWHITEFLAG = 0x1000;
        private const int SC_FOLDLEVELHEADERFLAG = 0x2000;
        private const int SC_FOLDLEVELNUMBERMASK = 0x0FFF;
        private const int SCI_SETMOUSEDWELLTIME = 2264;
        private const int SCI_SETMARGINTYPEN = 2240;
        private const int SCI_SETMARGINWIDTHN = 2242;
        private const int SCI_SETFOLDFLAGS = 2233;
        private const int SCI_SETMARGINMASKN = 2244;
        private const int SCI_SETMARGINSENSITIVEN = 2246;
        private const int SC_MARGIN_SYMBOL = 0;
        private const uint SC_MASK_FOLDERS = 0xFE000000;
        private const int SCI_MARKERDEFINE = 2040;
        private const int SCI_CALLTIPSHOW = 2200;
        private const int SCI_CALLTIPCANCEL = 2201;
        private const int SC_MARKNUM_FOLDEREND = 25;
        private const int SC_MARKNUM_FOLDEROPENMID = 26;
        private const int SC_MARKNUM_FOLDERMIDTAIL = 27;
        private const int SC_MARKNUM_FOLDERTAIL = 28;
        private const int SC_MARKNUM_FOLDERSUB = 29;
        private const int SC_MARKNUM_FOLDER = 30;
        private const int SC_MARKNUM_FOLDEROPEN = 31;
        private const int SC_MARK_FULLRECT = 26;
        private const int SCI_GETLINEENDPOSITION = 2136;
        private const int SCI_GETLINECOUNT = 2154;
        private const int SCI_GETLINE = 2153;
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
        private const int SCI_LINELENGTH = 2350;
        private const int SCI_POSITIONFROMLINE = 2167;
        private const int SCI_POINTXFROMPOSITION = 2164;
        private const int SCI_POINTYFROMPOSITION = 2165;
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
        private const int SCI_GETFIRSTVISIBLELINE = 2152;
        private const int SCI_SETFIRSTVISIBLELINE = 2613;
        private const int SCI_LINESCROLL = 2168;

        // Search and replace constants
        private const int SCI_SETTARGETSTART = 2190;
        private const int SCI_GETTARGETSTART = 2191;
        private const int SCI_SETTARGETSTARTVIRTUALSPACE = 2728;
        private const int SCI_GETTARGETSTARTVIRTUALSPACE = 2729;
        private const int SCI_SETTARGETEND = 2192;
        private const int SCI_GETTARGETEND = 2193;
        private const int SCI_SETTARGETENDVIRTUALSPACE = 2730;
        private const int SCI_GETTARGETENDVIRTUALSPACE = 2731;
        private const int SCI_SETTARGETRANGE = 2686;
        private const int SCI_TARGETFROMSELECTION = 2287;
        private const int SCI_TARGETWHOLEDOCUMENT = 2690;
        private const int SCI_SETSEARCHFLAGS = 2198;
        private const int SCI_GETSEARCHFLAGS = 2199;
        private const int SCI_SEARCHINTARGET = 2197;
        private const int SCI_GETTARGETTEXT = 2687;
        private const int SCI_REPLACETARGET = 2194;
        private const int SCI_REPLACETARGETMINIMAL = 2779;
        private const int SCI_REPLACETARGETRE = 2195;
        private const int SCI_GETTAG = 2616;
        private const int SCI_FINDTEXT = 2150;
        private const int SCI_FINDTEXTFULL = 2199;
        private const int SCI_SEARCHANCHOR = 2366;
        private const int SCI_SEARCHNEXT = 2367;
        private const int SCI_SEARCHPREV = 2368;

        // Search flag constants
        private const int SCFIND_NONE = 0;
        private const int SCFIND_MATCHCASE = 4;
        private const int SCFIND_WHOLEWORD = 2;
        private const int SCFIND_WORDSTART = 0x00100000;
        private const int SCFIND_REGEXP = 0x00200000;
        private const int SCFIND_POSIX = 0x00400000;
        private const int SCFIND_CXX11REGEX = 0x00800000;

        // Autocompletion constants
        private const int SCI_AUTOCSHOW = 2100;
        private const int SCI_AUTOCCANCEL = 2101;
        private const int SCI_AUTOCACTIVE = 2102;
        private const int SCI_AUTOCGETSEPARATOR = 2107;
        private const int SCI_AUTOCSETFILLUPS = 2112;
        private const int SCI_AUTOCSETSEPARATOR = 2106;
        private const int SCI_AUTOCSETORDER = 2660;
        private const int SCI_AUTOCGETORDER = 2661;
        private const int SCI_AUTOCSETIGNORECASE = 2115;
        private const int SC_ORDER_PERFORMSORT = 1;
        /* ignore case */
        
        // User list constants
        private const int SCI_USERLISTSHOW = 2117;
        private const int SCN_USERLISTSELECTION = 2014;

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
        private const int SCI_SETUSETABS = 2124;
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

        private const int SCI_BEGINUNDOACTION = 2078;
        private const int SCI_ENDUNDOACTION = 2079;

        // indicator style
        private const int INDIC_TEXTFORE = 17;

        // Element identifiers
        private const int SC_ELEMENT_SELECTION_BACK = 31;
        private const int SC_ELEMENT_SELECTION_TEXT = 32;

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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

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

        public static HashSet<IntPtr> editorsExpectingSavePoint = new();

        private const int ANNOTATION_STYLE_OFFSET = 0x100; // Lower, more standard offset value
        private const int BASE_ANNOT_STYLE = ANNOTATION_STYLE_OFFSET;
        private const int ANNOT_STYLE_GRAY = BASE_ANNOT_STYLE;
        private const int ANNOT_STYLE_YELLOW = BASE_ANNOT_STYLE + 1;
        private const int ANNOT_STYLE_RED = BASE_ANNOT_STYLE + 2;

        public static void FixEditorTabs(ScintillaEditor editor, bool funkySQLTabs = false)
        {
            editor.SendMessage(SCI_SETUSETABS, 0, 0);
            editor.SendMessage(SCI_SETTABINDENTS, 1, 0);
            var width = 4;
            if (editor.Type == EditorType.PeopleCode || (editor.Type == EditorType.SQL && funkySQLTabs))
            {
                width = 3;
            }
            editor.SendMessage(SCI_SETTABWIDTH, width, 0);
            editor.SendMessage(SCI_SETBACKSPACEUNINDENTS, 1, 0);
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
            var threadId = GetWindowThreadProcessId(hWnd, out uint processId);

            var editor = new ScintillaEditor(hWnd, processId, threadId, caption);
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
            if (hWnd == IntPtr.Zero)
            {
                throw new ArgumentException("Window handle cannot be zero.", nameof(hWnd));
            }

            if (!IsWindow(hWnd))
            {
                throw new ArgumentException("Invalid window handle.", nameof(hWnd));
            }

            if (!editors.ContainsKey(hWnd))
            {
                try
                {
                    InitEditor(hWnd);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize editor: {ex.Message}");
                    throw;
                }
            }
            
            // Check that the editor is still valid
            var editor = editors[hWnd];
            if (!editor.IsValid())
            {
                // Editor is invalid, clean it up and remove from dictionary
                CleanupEditor(editor);
                editors.Remove(hWnd);
                throw new InvalidOperationException("Editor is no longer valid.");
            }
            
            return editor;
        }

        public static IntPtr GetStandaloneProcessBuffer(ScintillaEditor editor, uint neededSize)
        {
            var processHandle = editor.hProc;
            var buffer = VirtualAllocEx(processHandle, IntPtr.Zero, neededSize, MEM_COMMIT, PAGE_READWRITE);
            return buffer;
        }

        public static void FreeStandaloneProcessBuffer(ScintillaEditor editor, IntPtr address)
        {
            VirtualFreeEx(editor.hProc, address, 0, MEM_RELEASE);
            processBuffers.Remove(editor.ProcessId);
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
                var(buffer, size) = processBuffers[processId];
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

        /// <summary>
        /// Inserts text at the current cursor position in the Scintilla editor.
        /// </summary>
        /// <param name="editor">The editor to insert text into</param>
        /// <param name="text">The text to insert at the cursor position</param>
        /// <returns>True if the text was successfully inserted, false otherwise</returns>
        public static bool InsertTextAtCursor(ScintillaEditor editor, string text)
        {
            if (editor == null || string.IsNullOrEmpty(text))
                return false;

            // Convert the text into a byte array using the default encoding.
            // We need to include an extra byte for the terminating null.
            byte[] textBytes = Encoding.Default.GetBytes(text);
            int neededSize = textBytes.Length + 1; // +1 for the null terminator

            var remoteBuffer = GetProcessBuffer(editor, (uint)neededSize);

            // Create a buffer that includes a terminating null.
            byte[] buffer = new byte[neededSize];
            Buffer.BlockCopy(textBytes, 0, buffer, 0, textBytes.Length);
            buffer[neededSize - 1] = 0;  // Ensure null termination.

            // Write the text into the remote process's memory.
            if (!WriteProcessMemory(editor.hProc, remoteBuffer, buffer, neededSize, out int bytesWritten) || bytesWritten != neededSize)
                return false;

            // Use SCI_REPLACESEL to insert text at the current cursor position
            // SCI_REPLACESEL(0, pointer to null-terminated string)
            editor.SendMessage(SCI_REPLACESEL, IntPtr.Zero, remoteBuffer);

            return true;
        }

        /// <summary>
        /// Begins an undo action in the Scintilla editor
        /// </summary>
        /// <param name="editor">The editor to begin undo action in</param>
        public static void BeginUndoAction(ScintillaEditor editor)
        {
            if (editor == null) return;
            editor.SendMessage(SCI_BEGINUNDOACTION, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Ends an undo action in the Scintilla editor
        /// </summary>
        /// <param name="editor">The editor to end undo action in</param>
        public static void EndUndoAction(ScintillaEditor editor)
        {
            if (editor == null) return;
            editor.SendMessage(SCI_ENDUNDOACTION, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Deletes a range of text in the Scintilla editor using SCI_SETTARGETRANGE and SCI_REPLACETARGET
        /// </summary>
        /// <param name="editor">The editor to delete text from</param>
        /// <param name="startPos">The starting position of the text to delete</param>
        /// <param name="length">The length of text to delete</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        public static bool DeleteTextRange(ScintillaEditor editor, int startPos, int length)
        {
            if (editor == null || length <= 0) return false;

            // Set target range
            editor.SendMessage(SCI_SETTARGETRANGE, startPos, startPos + length);

            // Replace target with empty string (effectively deleting)
            editor.SendMessage(SCI_REPLACETARGET, 0, IntPtr.Zero);

            return true;
        }

        /// <summary>
        /// Replaces a range of text in the Scintilla editor using SCI_SETTARGETRANGE and SCI_REPLACETARGET
        /// </summary>
        /// <param name="editor">The editor to replace text in</param>
        /// <param name="startPos">The starting position of the text to replace</param>
        /// <param name="endPos">The ending position of the text to replace (inclusive)</param>
        /// <param name="newText">The new text to replace with</param>
        /// <returns>True if the replacement was successful, false otherwise</returns>
        public static bool ReplaceTextRange(ScintillaEditor editor, int startPos, int endPos, string newText)
        {
            if (editor == null) return false;

            // Set target range
            editor.SendMessage(SCI_SETTARGETRANGE, startPos, endPos + 1);

            if (string.IsNullOrEmpty(newText))
            {
                // Replace with empty string (delete)
                editor.SendMessage(SCI_REPLACETARGET, 0, IntPtr.Zero);
                return true;
            }

            // Convert the new text into a byte array using the default encoding.
            // We need to include an extra byte for the terminating null.
            byte[] textBytes = Encoding.Default.GetBytes(newText);
            int neededSize = textBytes.Length + 1; // +1 for the null terminator

            var remoteBuffer = GetProcessBuffer(editor, (uint)neededSize);

            // Create a buffer that includes a terminating null.
            byte[] buffer = new byte[neededSize];
            Buffer.BlockCopy(textBytes, 0, buffer, 0, textBytes.Length);
            buffer[neededSize - 1] = 0;  // Ensure null termination.

            // Write the text into the remote process's memory.
            if (!WriteProcessMemory(editor.hProc, remoteBuffer, buffer, neededSize, out int bytesWritten) || bytesWritten != neededSize)
                return false;

            // Replace the target with the new text
            editor.SendMessage(SCI_REPLACETARGET, textBytes.Length, remoteBuffer);

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
            editor.SendMessage(SCI_SETMARGINTYPEN, 2, SC_MARGIN_SYMBOL);
            editor.SendMessage(SCI_SETMARGINWIDTHN, 2, 20);      // width in pixels
            editor.SendMessage(SCI_SETMARGINSENSITIVEN, 2, 1);
            editor.SendMessage(SCI_SETMARGINMASKN, 2, unchecked((IntPtr)SC_MASK_FOLDERS));
            // Collapse Label Style
            editor.SendMessage(SCI_MARKERDEFINE, SC_MARKNUM_FOLDER, SC_MARK_BOXPLUS);
            editor.SendMessage(SCI_MARKERDEFINE, SC_MARKNUM_FOLDEROPEN, SC_MARK_BOXMINUS);
            editor.SendMessage(SCI_MARKERDEFINE, SC_MARKNUM_FOLDEREND, SC_MARK_BOXPLUSCONNECTED);
            editor.SendMessage(SCI_MARKERDEFINE, SC_MARKNUM_FOLDEROPENMID, SC_MARK_BOXMINUSCONNECTED);
            editor.SendMessage(SCI_MARKERDEFINE, SC_MARKNUM_FOLDERMIDTAIL, SC_MARK_TCORNER);
            editor.SendMessage(SCI_MARKERDEFINE, SC_MARKNUM_FOLDERSUB, SC_MARK_VLINE);
            editor.SendMessage(SCI_MARKERDEFINE, SC_MARKNUM_FOLDERTAIL, SC_MARK_LCORNER);

            // Collapse Label Color
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDER, 0x0);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDEROPEN, 0x0);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDEREND, 0x0);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDEROPENMID, 0x0);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERMIDTAIL, 0x0);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERSUB, 0x0);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERTAIL, 0x0);

            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDER, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDEROPEN, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDEREND, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDEROPENMID, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDERMIDTAIL, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDERSUB, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDERTAIL, 0xFFFFFF);



            editor.SendMessage(SCI_SETFOLDFLAGS, 16 | 4, 0); //Display a horizontal line above and below the line after folding 
            editor.FoldingEnabled = true;

        }

        private static void HighlightText(ScintillaEditor editor, int highlighterNumber, int start, int length)
        {
            editor.SendMessage(SCI_SETINDICATORCURRENT, highlighterNumber, IntPtr.Zero);
            editor.SendMessage(SCI_INDICATORFILLRANGE, start, length);
        }

        /// <summary>
        /// Removes an indicator from a range of text
        /// </summary>
        /// <param name="editor">The ScintillaEditor to remove the indicator from</param>
        /// <param name="highlighterNumber">The highlighter number</param>
        /// <param name="start">The start position of the text</param>
        /// <param name="length">The length of the text</param>
        public static void RemoveIndicator(ScintillaEditor editor, Indicator indicator)
        {
            int indicatorNumber = 0;
            switch (indicator.Type)
            {
                case IndicatorType.HIGHLIGHTER:
                    indicatorNumber = editor.GetHighlighter(indicator.Color);
                    break;
                case IndicatorType.SQUIGGLE:
                    indicatorNumber = editor.GetSquiggle(indicator.Color);
                    break;
                case IndicatorType.TEXTCOLOR:
                    indicatorNumber = editor.GetTextColor(indicator.Color);
                    break;
            }


            editor.SendMessage(SCI_SETINDICATORCURRENT, indicatorNumber, IntPtr.Zero);
            editor.SendMessage(SCI_INDICATORCLEARRANGE, indicator.Start, indicator.Length);

            editor.ActiveIndicators.Remove(indicator);
        }


        /// <summary>
        /// Clears all indicators from the document
        /// </summary>
        /// <param name="editor">The ScintillaEditor to clear all indicators from</param>
        public static void ClearAllIndicators(ScintillaEditor editor)
        {
            for (var x = 0; x < editor.ActiveIndicators.Count; x++) 
            {
                RemoveIndicator(editor, editor.ActiveIndicators[x]);
            }

            // Clear the active indicators list
            //editor.ActiveIndicators.Clear();
        }

        public static void InitAnnotationStyles(ScintillaEditor editor)
        {
            try
            {
                if (editor.AnnotationStyleOffset == IntPtr.Zero)
                {
                    const int EXTRA_STYLES = 64; // Reduced from 256 to a more reasonable number
                    var newStyleOffset = editor.SendMessage(SCI_ALLOCATEEXTENDEDSTYLES, EXTRA_STYLES, IntPtr.Zero);
                    editor.AnnotationStyleOffset = newStyleOffset;

                    // Set the annotation style offset
                    editor.SendMessage(SCI_ANNOTATIONSETSTYLEOFFSET, newStyleOffset, IntPtr.Zero);
                }


                // Enable annotations and set visibility mode first
                editor.SendMessage(SCI_ANNOTATIONSETVISIBLE, ANNOTATION_BOXED, IntPtr.Zero);

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
                editor.SendMessage(SCI_STYLESETFORE, editor.AnnotationStyleOffset + (int)AnnotationStyle.Gray, GRAY_FORE);
                editor.SendMessage(SCI_STYLESETBACK, editor.AnnotationStyleOffset + (int)AnnotationStyle.Gray, GRAY_BACK);

                // Configure Yellow style
                editor.SendMessage(SCI_STYLESETFORE, editor.AnnotationStyleOffset + (int)AnnotationStyle.Yellow, YELLOW_FORE);
                editor.SendMessage(SCI_STYLESETBACK, editor.AnnotationStyleOffset + (int)AnnotationStyle.Yellow, YELLOW_BACK);

                // Configure Red style
                editor.SendMessage(SCI_STYLESETFORE, editor.AnnotationStyleOffset + (int)AnnotationStyle.Red, RED_FORE);
                editor.SendMessage(SCI_STYLESETBACK, editor.AnnotationStyleOffset + (int)AnnotationStyle.Red, RED_BACK);

                // Store initialization state
                editor.AnnotationsInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize annotations: {ex.Message}");
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
            
            // Free autocompletion pointer if exists
            if (editor.AutoCompletionPointer != IntPtr.Zero)
            {
                VirtualFreeEx(editor.hProc, editor.AutoCompletionPointer, 0, MEM_RELEASE);
                editor.AutoCompletionPointer = IntPtr.Zero;
            }
            
            // Free user list pointer if exists
            if (editor.UserListPointer != IntPtr.Zero)
            {
                VirtualFreeEx(editor.hProc, editor.UserListPointer, 0, MEM_RELEASE);
                editor.UserListPointer = IntPtr.Zero;
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
            editor.SendMessage(SCI_FOLDALL, 0, 0);
        }
        public static void ExpandTopLevel(ScintillaEditor editor)
        {
            editor.SendMessage(SCI_FOLDALL, 1, 0);
        }

        internal static void SetCurrentLineFoldStatus(ScintillaEditor activeEditor, bool folded)
        {
            // Get current cursor position
            int cursorPos = (int)activeEditor.SendMessage(SCI_GETCURRENTPOS, IntPtr.Zero, IntPtr.Zero);

            // Get line number from position
            int lineNum = (int)activeEditor.SendMessage(SCI_LINEFROMPOSITION, cursorPos, IntPtr.Zero);

            // Check if the line is a fold point and toggle it
            int foldLevel = (int)activeEditor.SendMessage(SCI_GETFOLDLEVEL, lineNum, IntPtr.Zero);
            int currentLineLevel = foldLevel & SC_FOLDLEVELNUMBERMASK;
            // Check if the line is a fold header (can be folded)
            if ((foldLevel & SC_FOLDLEVELHEADERFLAG) != 0)
            {
                // Toggle the fold (will collapse if expanded)
                activeEditor.SendMessage(SCI_FOLDLINE, lineNum, folded ? 0 : 1);
            }
            else
            {
                /* If the line is not a fold header, try to find the fold header */
                int parentLine = lineNum - 1;
                while (parentLine >= 0)
                {
                    foldLevel = (int)activeEditor.SendMessage(SCI_GETFOLDLEVEL, parentLine, IntPtr.Zero);
                    if ((foldLevel & SC_FOLDLEVELHEADERFLAG) != 0 && (foldLevel & SC_FOLDLEVELNUMBERMASK) < currentLineLevel)
                    {
                        activeEditor.SendMessage(SCI_FOLDLINE, parentLine, folded ? 0 : 1);
                        /* Set cursor to the end of the parent line */
                        activeEditor.SendMessage(SCI_GOTOPOS, activeEditor.SendMessage(SCI_GETLINEENDPOSITION, parentLine, IntPtr.Zero), IntPtr.Zero);
                        break;
                    }
                    parentLine--;
                }


            }
        }

        internal static (int Level, bool Header) GetCurrentLineFoldLevel(ScintillaEditor editor, int lineNum)
        {
            // Check if the line is a fold point and toggle it
            int foldLevel = (int)editor.SendMessage(SCI_GETFOLDLEVEL, lineNum, IntPtr.Zero);
            int currentLineLevel = foldLevel & SC_FOLDLEVELNUMBERMASK;

            // Check if the line is a fold header (can be folded)
            bool isHeader = (foldLevel & SC_FOLDLEVELHEADERFLAG) != 0;
            return (currentLineLevel, isHeader);

        }

        /* Assumes that lineNum is known to be a fold header already */
        internal static bool IsLineFolded(ScintillaEditor editor, int lineNum)
        {
            // Check if the line is a fold point and toggle it
            int foldLevelExpanded = (int)editor.SendMessage(SCI_GETFOLDEXPANDED, lineNum, IntPtr.Zero);

            return foldLevelExpanded == 0;
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
            editor.SendMessage(SCI_STYLESETBACK, 32, 0x1A1A1A);
            editor.SendMessage(SCI_STYLESETBACK, 33, 0x1A1A1A);
            // Brighter text for better readability
            editor.SendMessage(SCI_STYLESETFORE, 32, 0xE8E8E8);
            editor.SendMessage(SCI_STYLECLEARALL, 0, 0);

            // Keywords (orange in Visual Studio Code style)
            editor.SendMessage(SCI_STYLESETFORE, 3, 0x2C8BE2);
            // Strings (green)
            editor.SendMessage(SCI_STYLESETFORE, 4, 0x64D356);
            // Secondary keywords (cyan/light blue)
            editor.SendMessage(SCI_STYLESETFORE, 10, 0xD5AA6F);
            // Tertiary keywords (softer blue/teal)
            editor.SendMessage(SCI_STYLESETFORE, 11, 0xC7953B);
            // Comments (green-gray)
            editor.SendMessage(SCI_STYLESETFORE, 1, 0x6A9955);
            // Block comments (same as line comments)
            editor.SendMessage(SCI_STYLESETFORE, 24, 0x6A9955);
            // Numbers (purplish blue)
            editor.SendMessage(SCI_STYLESETFORE, 2, 0xB682AA);

            // Set white cursor color (hex FFFFFF)
            editor.SendMessage(SCI_SETCARETFORE, 0xFFFFFF, IntPtr.Zero);

            // Make cursor slightly wider for better visibility
            editor.SendMessage(SCI_SETCARETWIDTH, 2, IntPtr.Zero);

            // Set selection colors: navy blue background with white text
            editor.SendMessage(SCI_SETELEMENTCOLOUR, SC_ELEMENT_SELECTION_BACK, new IntPtr(0xC19429FF)); // Navy blue in BGR (800000 = RGB 0,0,128)
            editor.SendMessage(SCI_SETELEMENTCOLOUR, SC_ELEMENT_SELECTION_TEXT, new IntPtr(0xFF00FFFF)); // White text (hex FFFFFF)

            /* fold margin colors */
            // Collapse Label Style
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDER, 0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDEROPEN, 0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDEREND, 0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDEROPENMID, 0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERMIDTAIL, 0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERSUB, 0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARKNUM_FOLDERTAIL, 0x1A1A1A);

            // Collapse Label Color
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARK_FULLRECT, 0x1A1A1A);
            editor.SendMessage(SCI_MARKERSETBACK, SC_MARK_BACKGROUND, 0x1A1A1A);

            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDER, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDEROPEN, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDEREND, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDEROPENMID, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDERMIDTAIL, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDERSUB, 0xFFFFFF);
            editor.SendMessage(SCI_MARKERSETFORE, SC_MARKNUM_FOLDERTAIL, 0xFFFFFF);

            // Set dark mode flag
            editor.IsDarkMode = true;

            // Also update annotation styles for dark mode
            InitAnnotationStyles(editor);

        }

        public static bool IsEditorClean(ScintillaEditor editor)
        {
            return editor.SendMessage(SCI_GETMODIFY, 0, 0) == 0;
        }


        internal static int GetContentHashFromString(string content)
        {
            /* make crc32 hash of content string */
            return (int)Crc32.HashToUInt32(Encoding.UTF8.GetBytes(content));
        }

        internal static void SetFoldRegions(ScintillaEditor editor)
        {

            if (editor.ContentString == null) return;

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
                editor.SendMessage(SCI_SETFOLDLEVEL, line, level);
            }
        }

        internal static void SetExplicitFoldRegion(ScintillaEditor editor, int startLine, int endLine, bool collapseByDefault)
        {
            const int AppRefinerRegionOffset = 20; /* Some amount of indentation that's unlikely to actually occur */
            // Set the fold level for the start line to indicate a fold header.
            editor.SendMessage(SCI_SETFOLDLEVEL, startLine, SC_FOLDLEVELHEADERFLAG | (SC_FOLDLEVELBASE + AppRefinerRegionOffset));
            // Set the fold level for the end line to indicate a fold end.
            editor.SendMessage(SCI_SETFOLDLEVEL, endLine, SC_FOLDLEVELBASE + AppRefinerRegionOffset);

            /* increment the fold level of every line in between */
            for (int i = startLine + 1; i < endLine; i++)
            {
                int foldLevel = (int)editor.SendMessage(SCI_GETFOLDLEVEL, i, IntPtr.Zero);
                editor.SendMessage(SCI_SETFOLDLEVEL, i, (AppRefinerRegionOffset+foldLevel + 1));
            }

            if (collapseByDefault)
            {
                // Collapse the region if specified
                SetLineFoldStatus(editor, startLine, true);
            }

        }

        /// <summary>
        /// Calculates the "indent" amount for a given line.
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

        internal static void ForceSQLFormat(ScintillaEditor editor)
        {
            if (editor.Type != EditorType.SQL)
            {
                return;
            }

            editor.ContentString ??= GetScintillaText(editor);
            var cursorPosition = ScintillaManager.GetCursorPosition(editor);
            var contentWithMarker = editor.ContentString?.Insert(cursorPosition, "--AppRefiner--");
            var formatted = SqlFormatter.Of(Dialect.StandardSql)
                .Extend(cfg => cfg.PlusSpecialWordChars("%").PlusNamedPlaceholderTypes(new string[] { ":" }).PlusOperators(new string[] { "%Concat" }))
                .Format(contentWithMarker, formatConfig).Replace("\n", "\r\n");

            var newCursorPosition = formatted.IndexOf("--AppRefiner--");
            if (newCursorPosition > 0)
            {
                formatted = formatted.Remove(newCursorPosition, "--AppRefiner--".Length);
            }

            editor.ContentString = formatted;
            SetScintillaText(editor, formatted);
            SetCursorPosition(editor, newCursorPosition);
        }

        internal static void ApplyBetterSQL(ScintillaEditor editor)
        {
            editor.ContentString ??= GetScintillaText(editor);

            var cursorPosition = ScintillaManager.GetCursorPosition(editor);

            var formatted = SqlFormatter.Of(Dialect.StandardSql)
                .Extend(cfg => cfg.PlusSpecialWordChars("%").PlusNamedPlaceholderTypes(new string[] { ":" }).PlusOperators(new string[] { "%Concat" }))
                .Format(editor.ContentString, formatConfig).Replace("\n", "\r\n");
            if (string.IsNullOrEmpty(formatted))
            {
                return;
            }

            if (editor.ContentString?.SequenceEqual(formatted) ?? false)
            {
                return;
            }
            editor.ContentString = formatted;
            SetScintillaText(editor, formatted);
            editorsExpectingSavePoint.Add(editor.hWnd);
            editor.SendMessage(SCI_SETSAVEPOINT, 0, 0);

            /* update local content hash */
        }

        public static void SetSavePoint(ScintillaEditor editor, bool editorExpectingSave = false)
        {
            if (editorExpectingSave)
            {
                editorsExpectingSavePoint.Add(editor.hWnd);
            }
            editor.SendMessage(SCI_SETSAVEPOINT, 0, 0);
        }

        internal static void ShowCallTip(ScintillaEditor editor, IntPtr position)
        {
            try 
            {
                // Validate editor
                if (editor == null || !editor.IsValid())
                {
                    Debug.LogError("Cannot show call tip - editor is null or invalid");
                    return;
                }
            
                // Use the tooltip provider system to show tooltips
                TooltipProviders.TooltipManager.ShowTooltip(editor, position.ToInt32());
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error showing call tip: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a call tip with the specified text at the given position
        /// </summary>
        /// <param name="editor">The editor to show the call tip in</param>
        /// <param name="position">The position to show the call tip at</param>
        /// <param name="text">The text to display in the call tip</param>
        internal static void ShowCallTipWithText(ScintillaEditor editor, int position, string text)
        {
            try 
            {
                // Validate editor
                if (editor == null || !editor.IsValid())
                {
                    Debug.LogError("Cannot show call tip - editor is null or invalid");
                    return;
                }
                
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }
                
                // Clean up existing call tip if any
                if (editor.CallTipPointer != IntPtr.Zero)
                {
                    VirtualFreeEx(editor.hProc, editor.CallTipPointer, 0, MEM_RELEASE);
                    editor.CallTipPointer = IntPtr.Zero;
                }
                
                // Allocate memory for new call tip text
                var textBytes = Encoding.Default.GetBytes(text);
                var neededSize = textBytes.Length + 1;
                var remoteBuffer = VirtualAllocEx(editor.hProc, IntPtr.Zero, (uint)neededSize, MEM_COMMIT, PAGE_READWRITE);
                
                if (remoteBuffer == IntPtr.Zero)
                {
                    Debug.LogError($"Failed to allocate memory for call tip: {Marshal.GetLastWin32Error()}");
                    return;
                }
                
                if (!WriteProcessMemory(editor.hProc, remoteBuffer, textBytes, neededSize, out int bytesWritten) || bytesWritten != neededSize)
                {
                    VirtualFreeEx(editor.hProc, remoteBuffer, 0, MEM_RELEASE);
                    Debug.LogError($"Failed to write call tip text to memory: {Marshal.GetLastWin32Error()}");
                    return;
                }
                
                editor.CallTipPointer = remoteBuffer;
                editor.SendMessage(SCI_CALLTIPSHOW, new IntPtr(position), remoteBuffer);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error showing call tip with text: {ex.Message}");
            }
        }

        internal static void HideCallTip(ScintillaEditor editor)
        {
            try
            {
                // Validate editor
                if (editor == null || !editor.IsValid())
                {
                    return;
                }
                
                // Cancel the call tip in Scintilla
                editor.SendMessage(SCI_CALLTIPCANCEL, IntPtr.Zero, IntPtr.Zero);
                
                // Free memory used by the call tip
                if (editor.CallTipPointer != IntPtr.Zero)
                {
                    VirtualFreeEx(editor.hProc, editor.CallTipPointer, 0, MEM_RELEASE);
                    editor.CallTipPointer = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error hiding call tip: {ex.Message}");
            }
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
                    editor.SendMessage(SCI_ANNOTATIONSETSTYLE, line, (int)style);
                    editor.SendMessage(SCI_ANNOTATIONSETTEXT, line, pointer);
                    return;
                }

                // Allocate memory for new annotation text
                var textBytes = Encoding.Default.GetBytes(text);
                var neededSize = textBytes.Length + 1;
                var remoteBuffer = VirtualAllocEx(editor.hProc, IntPtr.Zero, (uint)neededSize, MEM_COMMIT, PAGE_READWRITE);

                if (remoteBuffer == IntPtr.Zero)
                {
                    Debug.LogError($"Failed to allocate memory for annotation: {Marshal.GetLastWin32Error()}");
                    return;
                }

                if (!WriteProcessMemory(editor.hProc, remoteBuffer, textBytes, neededSize, out int bytesWritten) || bytesWritten != neededSize)
                {
                    VirtualFreeEx(editor.hProc, remoteBuffer, 0, MEM_RELEASE);
                    Debug.LogError($"Failed to write annotation text to memory: {Marshal.GetLastWin32Error()}");
                    return;
                }

                editor.AnnotationPointers[text] = remoteBuffer;
                editor.SendMessage(SCI_ANNOTATIONSETSTYLE, line, (int)style);
                editor.SendMessage(SCI_ANNOTATIONSETTEXT, line, remoteBuffer);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error setting annotation: {ex.Message}");
            }
        }

        public static int GetSelectionLength(ScintillaEditor editor)
        {
            var selectionStart = editor.SendMessage(SCI_GETSELECTIONSTART, IntPtr.Zero, IntPtr.Zero);
            var selectionEnd = editor.SendMessage(SCI_GETSELECTIONEND, IntPtr.Zero, IntPtr.Zero);
            return (int)selectionEnd - (int)selectionStart;
        }

        /// <summary>
        /// Gets the currently selected text in the Scintilla editor
        /// </summary>
        /// <param name="editor">The editor to get the selected text from</param>
        /// <returns>The selected text as a string, or null if no text is selected</returns>
        public static (string?, int, int) GetSelectedText(ScintillaEditor editor)
        {
            var selectionStart = (int)editor.SendMessage(SCI_GETSELECTIONSTART, IntPtr.Zero, IntPtr.Zero);
            var selectionEnd = (int)editor.SendMessage(SCI_GETSELECTIONEND, IntPtr.Zero, IntPtr.Zero);
            
            // Return null if there's no selection
            if (selectionStart == selectionEnd)
            {
                return (null, 0, 0);
            }
            
            // Get the full text
            var fullText = GetScintillaText(editor);
            if (fullText == null)
            {
                return (null, 0, 0);
            }
            
            // Extract the selected portion
            int length = selectionEnd - selectionStart;
            if (selectionStart < 0 || length <= 0 || selectionStart + length > fullText.Length)
            {
                return (null, 0, 0);
            }
            
            return (fullText.Substring(selectionStart, length), selectionStart, selectionEnd);
        }

        /// <summary>
        /// Sets the selection range in the Scintilla editor
        /// </summary>
        /// <param name="editor">The editor to set the selection in</param>
        /// <param name="startIndex">The starting position of the selection</param>
        /// <param name="endIndex">The ending position of the selection</param>
        public static void SetSelection(ScintillaEditor editor, int startIndex, int endIndex)
        {
            editor.SendMessage(SCI_SETSEL, startIndex, endIndex);
        }

        public static void ClearAnnotations(ScintillaEditor editor)
        {
            if (editor == null) return;

            // Clear text
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
                    var styleValue = (byte)(int)styles[i];

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
                editor.SendMessage(SCI_ANNOTATIONSETTEXT, line, remoteTextBuffer);
                editor.SendMessage(SCI_ANNOTATIONSETSTYLES, line, remoteStyleBuffer);

                // Store buffers for cleanup
                editor.AnnotationPointers[combinedText] = remoteTextBuffer;
                editor.AnnotationPointers[combinedText + "_styles"] = remoteStyleBuffer;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error setting annotations: {ex.Message}");
                throw;
            }
        }

        internal static void ResetStyles(ScintillaEditor activeEditor)
        {
            // Clear document styles
            activeEditor.SendMessage(SCI_CLEARDOCUMENTSTYLE, 0, 0);
            
            // Clear all indicators
            ClearAllIndicators(activeEditor);

            ClearAnnotations(activeEditor);

            // Recolorize the document
            var docLength = activeEditor.SendMessage(SCI_GETLENGTH, 0, 0);
            activeEditor.SendMessage(SCI_COLOURISE, 0, docLength);
        }

        internal static int GetCursorPosition(ScintillaEditor? activeEditor)
        {
            return activeEditor == null ? -1 : (int)activeEditor.SendMessage(SCI_GETCURRENTPOS, 0, 0);
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
            editor.SendMessage(SCI_GOTOPOS, position, IntPtr.Zero);

            // Ensure the position is visible by scrolling to it
            editor.SendMessage(SCI_SCROLLCARET, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Sets the cursor position in the Scintilla editor without scrolling to it
        /// </summary>
        /// <param name="editor">The editor to set the cursor position in</param>
        /// <param name="position">The position to place the cursor</param>
        public static void SetCursorPositionWithoutScroll(ScintillaEditor editor, int position)
        {
            if (editor == null) return;
            
            // Set the cursor position without scrolling
            editor.SendMessage(SCI_GOTOPOS, position, IntPtr.Zero);
        }

        internal static void ExpandCurrent(ScintillaEditor activeEditor)
        {
            // Get current cursor position
            int cursorPos = (int)activeEditor.SendMessage(SCI_GETCURRENTPOS, IntPtr.Zero, IntPtr.Zero);

            // Get line number from position
            int lineNum = (int)activeEditor.SendMessage(SCI_LINEFROMPOSITION, cursorPos, IntPtr.Zero);

            // Check if the line is a fold point and toggle it
            int foldLevel = (int)activeEditor.SendMessage(SCI_GETFOLDLEVEL, lineNum, IntPtr.Zero);

            // Check if the line is a fold header (can be folded)
            if ((foldLevel & SC_FOLDLEVELHEADERFLAG) != 0)
            {
                // Toggle the fold (will expand if collapsed)
                activeEditor.SendMessage(SCI_TOGGLEFOLD, lineNum, IntPtr.Zero);
            }
        }

        /* Method to get line number from position */
        public static int GetLineFromPosition(ScintillaEditor editor, int position)
        {
            if (editor == null) return -1;
            return (int)editor.SendMessage(SCI_LINEFROMPOSITION, position, IntPtr.Zero);
        }

        /// <summary>
        /// Gets the start index of the previous line.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance</param>
        /// <param name="line">The current line number</param>
        /// <returns>The start index of the previous line, or -1 if there is no previous line</returns>
        public static int GetLineStartIndex(ScintillaEditor editor, int line)
        {
            if (editor == null || line < 1)
            {
                return -1; // No previous line for line 0 or invalid inputs
            }

            return (int)editor.SendMessage(SCI_POSITIONFROMLINE, line, IntPtr.Zero);
        }


        public static int GetCurrentLineNumber(ScintillaEditor editor)
        {
            return editor == null ? -1 : (int)editor.SendMessage(SCI_LINEFROMPOSITION, editor.SendMessage(SCI_GETCURRENTPOS, 0, 0), 0);
        }

        public static string GetLineText(ScintillaEditor editor, int lineNumber)
        {
            if (editor == null) return string.Empty;
            int start = (int)editor.SendMessage(SCI_POSITIONFROMLINE, lineNumber, IntPtr.Zero);
            int end = (int)editor.SendMessage(SCI_GETLINEENDPOSITION, lineNumber, IntPtr.Zero);

            /* Get fresh copy of content */
            editor.ContentString = GetScintillaText(editor);


            return editor.ContentString?.Substring(start, end - start) ?? string.Empty;
        }

        public static string GetCurrentLineText(ScintillaEditor editor)
        {
            if (editor == null) return string.Empty;
            int lineNumber = GetCurrentLineNumber(editor);
            return GetLineText(editor, lineNumber);
        }

        /// <summary>
        /// Creates a new highlighter with the specified BGRA color
        /// </summary>
        /// <param name="editor">The ScintillaEditor to create the highlighter for</param>
        /// <param name="color">The BGRA color value</param>
        /// <returns>The highlighter number</returns>
        public static int CreateHighlighter(ScintillaEditor editor, uint bgraColor)
        {
            int highlighterNumber = editor.NextIndicatorNumber;
            
            var bgrColor = (bgraColor & 0xFFFFFF00) >> 8; // Remove alpha
            var alpha = bgraColor & 0x000000FF; // Extract alpha
            editor.SendMessage(SCI_INDICSETSTYLE, highlighterNumber, INDIC_FULLBOX);
            editor.SendMessage(SCI_INDICSETFORE, highlighterNumber, new IntPtr(bgrColor));
            editor.SendMessage(SCI_INDICSETALPHA, highlighterNumber, new IntPtr(alpha));
            editor.SendMessage(SCI_INDICSETUNDER, highlighterNumber, 1);
            
            editor.ColorToHighlighterMap[bgraColor] = highlighterNumber;
            editor.NextIndicatorNumber++;
            
            return highlighterNumber;
        }

        /// <summary>
        /// Gets the first visible line in the Scintilla editor
        /// </summary>
        /// <param name="editor">The editor to get the first visible line from</param>
        /// <returns>The first visible line number</returns>
        public static int GetFirstVisibleLine(ScintillaEditor editor)
        {
            if (editor == null) return 0;
            return (int)editor.SendMessage(SCI_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Sets the first visible line in the Scintilla editor
        /// </summary>
        /// <param name="editor">The editor to set the first visible line in</param>
        /// <param name="line">The line number to make the first visible line</param>
        public static void SetFirstVisibleLine(ScintillaEditor editor, int line)
        {
            if (editor == null) return;
            editor.SendMessage(SCI_SETFIRSTVISIBLELINE, line, IntPtr.Zero);
        }

        internal static void SetMouseDwellTime(ScintillaEditor activeEditor, int v)
        {
            activeEditor.SendMessage(SCI_SETMOUSEDWELLTIME, v, 0);
        }

        internal static object GetThreadId(ScintillaEditor activeEditor)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new squiggle indicator with the specified BGRA color
        /// </summary>
        /// <param name="editor">The ScintillaEditor to create the squiggle for</param>
        /// <param name="color">The BGRA color value</param>
        /// <returns>The squiggle indicator number</returns>
        public static int CreateSquiggle(ScintillaEditor editor, uint bgraColor)
        {
            int squiggleNumber = editor.NextIndicatorNumber;
            
            var bgrColor = (bgraColor & 0xFFFFFF00) >> 8; // Remove alpha
            editor.SendMessage(SCI_INDICSETSTYLE, squiggleNumber, INDIC_SQUIGGLE);
            editor.SendMessage(SCI_INDICSETFORE, squiggleNumber, new IntPtr(bgrColor));
            
            editor.ColorToSquiggleMap[bgraColor] = squiggleNumber;
            editor.NextIndicatorNumber++;
            
            return squiggleNumber;
        }

        /// <summary>
        /// Creates a new text color indicator with the specified BGRA color
        /// </summary>
        /// <param name="editor">The ScintillaEditor to create the text color for</param>
        /// <param name="bgraColor">The BGRA color value</param>
        /// <returns>The text color indicator number</returns>
        public static int CreateTextColor(ScintillaEditor editor, uint bgraColor)
        {
            int textColorNumber = editor.NextIndicatorNumber;

            var bgrColor = (bgraColor & 0xFFFFFF00) >> 8; // Remove alpha
            editor.SendMessage(SCI_INDICSETSTYLE, textColorNumber, INDIC_TEXTFORE);
            editor.SendMessage(SCI_INDICSETFORE, textColorNumber, new IntPtr(bgrColor));

            editor.ColorToTextColorMap[bgraColor] = textColorNumber;
            editor.NextIndicatorNumber++;

            return textColorNumber;
        }


        public static void AddIndicator(ScintillaEditor editor, Indicator indicator)
        {

            switch (indicator.Type)
            {
                case IndicatorType.HIGHLIGHTER:
                    int highlighterNumber = editor.GetHighlighter(indicator.Color);
                    HighlightText(editor, highlighterNumber, indicator.Start, indicator.Length);
                    break;
                case IndicatorType.SQUIGGLE:
                    int squiggleNumber = editor.GetSquiggle(indicator.Color);
                    HighlightText(editor, squiggleNumber, indicator.Start, indicator.Length);
                    break;
                case IndicatorType.TEXTCOLOR:
                    int textColorNumber = editor.GetTextColor(indicator.Color);
                    HighlightText(editor, textColorNumber, indicator.Start, indicator.Length);
                    break;
            }

            editor.ActiveIndicators.Add(indicator);

        }


        /// <summary>
        /// Sets the separator character used for autocompletion lists
        /// </summary>
        /// <param name="editor">The editor to set the separator for</param>
        /// <param name="separator">The separator character</param>
        public static void SetAutoCompletionSeparator(ScintillaEditor editor, char separator)
        {
            if (editor == null || !editor.IsValid())
            {
                return;
            }

            editor.SendMessage(SCI_AUTOCSETSEPARATOR, new IntPtr(separator), IntPtr.Zero);
        }

        /// <summary>
        /// Shows a user list with the provided options
        /// </summary>
        /// <param name="editor">The editor to show the user list in</param>
        /// <param name="listType">The list type (1-9)</param>
        /// <param name="position">The position to show the list at</param>
        /// <param name="options">List of strings to show as user list options</param>
        /// <returns>True if the user list was shown successfully</returns>
        public static bool ShowUserList(ScintillaEditor editor, UserListType listType, int position, List<string> options, bool acceptSingle = true)
        {
            if (editor == null || editor.hProc == IntPtr.Zero || options == null || options.Count == 0)
            {
                Debug.Log("ShowUserList: Invalid parameters");
                return false;
            }
            SetAutoCompletionSeparator(editor, '/');
            var sortOrder = editor.SendMessage(SCI_AUTOCGETORDER, 0, 0);
            editor.SendMessage(SCI_AUTOCSETORDER, SC_ORDER_PERFORMSORT, 0);
            editor.SendMessage(SCI_AUTOCSETIGNORECASE, 1, 0);
            try
            {
                // Create a string with all options separated by spaces
                string optionsString = string.Join("/", options);
                
                // Get the required buffer size - including null terminator
                int bufferSize = Encoding.UTF8.GetByteCount(optionsString) + 1; // +1 for null terminator
                
                // Allocate memory in the target process for storing the options
                IntPtr remoteBuffer = VirtualAllocEx(
                    editor.hProc,
                    IntPtr.Zero, 
                    (uint)bufferSize,
                    MEM_COMMIT, 
                    PAGE_READWRITE);
                
                if (remoteBuffer == IntPtr.Zero)
                {
                    Debug.Log("ShowUserList: Failed to allocate memory in remote process");
                    return false;
                }
                
                // Store the pointer for later cleanup
                editor.UserListPointer = remoteBuffer;
                
                // Convert the string to a byte array with a null terminator
                byte[] buffer = new byte[bufferSize];
                Encoding.UTF8.GetBytes(optionsString, 0, optionsString.Length, buffer, 0);
                buffer[bufferSize - 1] = 0; // Ensure null termination
                
                // Write the options to the remote process memory
                int bytesWritten;
                bool result = WriteProcessMemory(
                    editor.hProc,
                    remoteBuffer,
                    buffer,
                    bufferSize,
                    out bytesWritten);
                    
                if (!result || bytesWritten != bufferSize)
                {
                    Debug.Log($"ShowUserList: Failed to write to remote process memory. Written {bytesWritten} of {bufferSize} bytes");
                    VirtualFreeEx(editor.hProc, remoteBuffer, 0, MEM_RELEASE);
                    editor.UserListPointer = IntPtr.Zero;
                    return false;
                }
                
                // Send the Scintilla message to show the user list
                IntPtr ret = editor.SendMessage(SCI_USERLISTSHOW, (IntPtr)listType, remoteBuffer);
                SetAutoCompletionSeparator(editor, ' '); // Reset separator to default
                return ret != IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Debug.Log($"ShowUserList: Exception: {ex.Message}");
                
                // Clean up on exception
                if (editor.UserListPointer != IntPtr.Zero)
                {
                    VirtualFreeEx(editor.hProc, editor.UserListPointer, 0, MEM_RELEASE);
                    editor.UserListPointer = IntPtr.Zero;
                }
                
                return false;
            }
        }

        /// <summary>
        /// Cancels any active user list in the editor
        /// </summary>
        /// <param name="editor">The editor to cancel the user list in</param>
        public static void CancelUserList(ScintillaEditor editor)
        {
            try
            {
                if (editor == null || !editor.IsValid())
                {
                    return;
                }

                // Cancel the autocompletion, which also cancels user lists
                editor.SendMessage(SCI_AUTOCCANCEL, IntPtr.Zero, IntPtr.Zero);

                // Free the memory used for user list options
                if (editor.UserListPointer != IntPtr.Zero)
                {
                    VirtualFreeEx(editor.hProc, editor.UserListPointer, 0, MEM_RELEASE);
                    editor.UserListPointer = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error canceling user list: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads a UTF-8 string from memory in the editor's process
        /// </summary>
        /// <param name="editor">The ScintillaEditor instance</param>
        /// <param name="address">Memory address to read from</param>
        /// <param name="maxLength">Maximum number of bytes to read</param>
        /// <returns>The UTF-8 decoded string, or null if reading failed</returns>
        public static string? ReadUtf8FromMemory(ScintillaEditor editor, IntPtr address, int maxLength)
        {
            if (editor == null || address == IntPtr.Zero || maxLength <= 0)
                return null;

            try
            {
                // Get process handle for the editor
                if (editor.hProc == IntPtr.Zero)
                {
                    Debug.Log($"Cannot read memory: Process handle is null");
                    return null;
                }

                // Allocate buffer for the string
                byte[] buffer = new byte[maxLength];
                int bytesRead = 0;

                // Read memory from the process
                if (!ReadProcessMemory(editor.hProc, address, buffer, maxLength, out bytesRead))
                {
                    int error = Marshal.GetLastWin32Error();
                    Debug.Log($"ReadProcessMemory failed with error code: {error}");
                    return null;
                }

                if (bytesRead <= 0)
                {
                    Debug.Log("No bytes read from process memory");
                    return null;
                }

                // Find the actual string length (until null terminator)
                int stringLength = 0;
                while (stringLength < bytesRead && buffer[stringLength] != 0)
                {
                    stringLength++;
                }

                // Decode the UTF-8 bytes
                return Encoding.UTF8.GetString(buffer, 0, stringLength);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error reading UTF-8 from memory: {ex.Message}");
                return null;
            }
        }

        internal static int GetLineLength(ScintillaEditor activeEditor, int lineNumber)
        {
            if (activeEditor == null) return 0;
            return (int)activeEditor.SendMessage(SCI_LINELENGTH, lineNumber, IntPtr.Zero);
        }

        internal static void SetLineFoldStatus(ScintillaEditor activeEditor, int lineNumber, bool folded)
        {
            activeEditor.SendMessage(SCI_FOLDLINE, lineNumber, folded ? 0 : 1);
        }

        internal static bool InsertTextAtLocation(ScintillaEditor editor, int location, string text)
        {
            if (editor == null || string.IsNullOrEmpty(text))
                return false;

            // Convert the text into a byte array using the default encoding.
            // We need to include an extra byte for the terminating null.
            byte[] textBytes = Encoding.Default.GetBytes(text);
            int neededSize = textBytes.Length + 1; // +1 for the null terminator

            var remoteBuffer = GetProcessBuffer(editor, (uint)neededSize);

            // Create a buffer that includes a terminating null.
            byte[] buffer = new byte[neededSize];
            Buffer.BlockCopy(textBytes, 0, buffer, 0, textBytes.Length);
            buffer[neededSize - 1] = 0;  // Ensure null termination.

            // Write the text into the remote process's memory.
            if (!WriteProcessMemory(editor.hProc, remoteBuffer, buffer, neededSize, out int bytesWritten) || bytesWritten != neededSize)
                return false;

            // Use SCI_REPLACESEL to insert text at the current cursor position
            // SCI_REPLACESEL(0, pointer to null-terminated string)
            editor.SendMessage(SCI_INSERTTEXT, location, remoteBuffer);

            return true;
        }

        internal static int GetLineCount(ScintillaEditor activeEditor)
        {
            return (int)activeEditor.SendMessage(SCI_GETLINECOUNT, 0, 0);
        }

        #region Better Find/Replace Methods

        /// <summary>
        /// Shows the Better Find dialog for the specified editor
        /// </summary>
        /// <param name="editor">The editor to show the find dialog for</param>
        /// <param name="enableReplaceMode">Whether to enable replace mode on startup</param>
        public static void ShowBetterFindDialog(ScintillaEditor editor, bool enableReplaceMode = false)
        {
            // Check if dialog already exists for this editor
            if (_activeDialogs.TryGetValue(editor, out Dialogs.BetterFindDialog existingDialog))
            {
                if (!existingDialog.IsDisposed && existingDialog.Visible)
                {
                    existingDialog.Invoke((MethodInvoker)delegate
                    {
                        // If dialog is already open, just bring it to front

                        existingDialog.BringToFront();
                        existingDialog.Focus();

                        // If replace mode requested and current dialog is find-only, switch modes
                        if (enableReplaceMode && !existingDialog.IsReplaceMode)
                        {
                            existingDialog.EnableReplaceMode();
                        }
                    });
                    return;
                }
                else
                {
                    // Clean up stale reference
                    _activeDialogs.TryRemove(editor, out _);
                }
            }

            // Create new dialog
            Task.Delay(100).ContinueWith(_ =>
            {
                try
                {
                    var mainHandle = Process.GetProcessById((int)editor.ProcessId).MainWindowHandle;
                    var handleWrapper = new WindowWrapper(0);
                    
                    var dialog = new Dialogs.BetterFindDialog(editor, 0, enableReplaceMode);
                    
                    // Track this dialog
                    _activeDialogs[editor] = dialog;

                    // Auto-cleanup when dialog closes
                    BetterFindDialog? v = null;
                    dialog.FormClosed += (s, e) => _activeDialogs.TryRemove(editor, out v);
                    
                    WindowHelper.CenterFormOnWindow(dialog, mainHandle);

                    // Make the dialog always on top
                    dialog.MakeAlwaysOnTop();

                    // Show as non-modal dialog
                    dialog.ShowDialog();

                    /* focus dialog window */
                    dialog.Focus();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error showing Better Find dialog: {ex.Message}");
                    // Clean up tracking on error
                    BetterFindDialog? v = null;
                    _activeDialogs.TryRemove(editor,out v);
                }
            }, TaskScheduler.Default);
        }

        /// <summary>
        /// Finds the next occurrence of the search term in the editor
        /// </summary>
        /// <param name="editor">The editor to search in</param>
        /// <returns>True if text was found, false otherwise</returns>
        public static bool FindNext(ScintillaEditor editor)
        {
            if (!editor.SearchState.HasValidSearch)
            {
                ShowBetterFindDialog(editor);
                return false;
            }

            return PerformSearch(editor, forward: true);
        }

        /// <summary>
        /// Finds the previous occurrence of the search term in the editor
        /// </summary>
        /// <param name="editor">The editor to search in</param>
        /// <returns>True if text was found, false otherwise</returns>
        public static bool FindPrevious(ScintillaEditor editor)
        {
            if (!editor.SearchState.HasValidSearch)
            {
                ShowBetterFindDialog(editor);
                return false;
            }

            return PerformSearch(editor, forward: false);
        }

        /// <summary>
        /// Gets the start and end range of the current method at the cursor position
        /// Uses ANTLR parsing to identify method boundaries including method headers, function definitions, getters, and setters
        /// </summary>
        /// <param name="editor">The ScintillaEditor instance</param>
        /// <param name="cursorPosition">Current cursor position</param>
        /// <returns>A tuple containing (start, end) positions of the current method, or (0, program_length) if no method found</returns>
        private static (int start, int end) GetCurrentMethodRange(ScintillaEditor editor, int cursorPosition)
        {
            try
            {
                // Get the program text
                string? programText = GetScintillaText(editor);
                if (string.IsNullOrEmpty(programText))
                {
                    return (0, programText?.Length ?? 0);
                }

                // Parse the program using ANTLR
                var parseTree = ProgramParser.Parse(programText);
                
                // Create a listener to find methods/functions containing the cursor position
                var methodFinder = new MethodRangeFinder(cursorPosition);
                ParseTreeWalker.Default.Walk(methodFinder, parseTree);
                
                // Return the found range or full document if no method found
                return methodFinder.FoundRange ?? (0, programText.Length);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error in GetCurrentMethodRange: {ex.Message}");
                string? programText = GetScintillaText(editor);
                return (0, programText?.Length ?? 0);
            }
        }

        /// <summary>
        /// ANTLR listener to find method boundaries containing a specific position
        /// </summary>
        private class MethodRangeFinder : PeopleCodeParserBaseListener
        {
            private readonly int _targetPosition;
            public (int start, int end)? FoundRange { get; private set; }

            public MethodRangeFinder(int targetPosition)
            {
                _targetPosition = targetPosition;
            }

            public override void EnterMethod(PeopleCode.PeopleCodeParser.MethodContext context)
            {
                if (FoundRange.HasValue) return; // Already found a method

                int startPos = context.Start.ByteStartIndex();
                int endPos = context.Stop.ByteStopIndex() + 1;

                if (startPos <= _targetPosition && _targetPosition <= endPos)
                {
                    FoundRange = (startPos, endPos);
                }
            }

            public override void EnterFunctionDefinition(PeopleCode.PeopleCodeParser.FunctionDefinitionContext context)
            {
                if (FoundRange.HasValue) return; // Already found a method

                int startPos = context.Start.ByteStartIndex();
                int endPos = context.Stop.ByteStopIndex() + 1;

                if (startPos <= _targetPosition && _targetPosition <= endPos)
                {
                    FoundRange = (startPos, endPos);
                }
            }

            public override void EnterGetter(PeopleCode.PeopleCodeParser.GetterContext context)
            {
                if (FoundRange.HasValue) return; // Already found a method

                int startPos = context.Start.ByteStartIndex();
                int endPos = context.Stop.ByteStopIndex() + 1;

                if (startPos <= _targetPosition && _targetPosition <= endPos)
                {
                    FoundRange = (startPos, endPos);
                }
            }

            public override void EnterSetter(PeopleCode.PeopleCodeParser.SetterContext context)
            {
                if (FoundRange.HasValue) return; // Already found a method

                int startPos = context.Start.ByteStartIndex();
                int endPos = context.Stop.ByteStopIndex() + 1;

                if (startPos <= _targetPosition && _targetPosition <= endPos)
                {
                    FoundRange = (startPos, endPos);
                }
            }
        }

        /// <summary>
        /// Performs a search operation in the specified direction
        /// </summary>
        /// <param name="editor">The editor to search in</param>
        /// <param name="forward">True to search forward, false to search backward</param>
        /// <returns>True if text was found, false otherwise</returns>
        private static bool PerformSearch(ScintillaEditor editor, bool forward)
        {
            var searchState = editor.SearchState;
            var searchFlags = BuildSearchFlags(searchState);
            
            int docLength = (int)editor.SendMessage(SCI_GETLENGTH, IntPtr.Zero, IntPtr.Zero);
            
            // Determine search range based on scope preference
            int rangeStart, rangeEnd;
            if (searchState.SearchInSelection && searchState.HasValidSelection)
            {
                // Use stored selection range
                rangeStart = searchState.SelectionStart;
                rangeEnd = searchState.SelectionEnd;
            }
            else
            {
                if (searchState.SearchInMethod)
                {
                    // Limit to current method/function/getter/setter if possible, else whole document.
                    (rangeStart, rangeEnd) = ScintillaManager.GetCurrentMethodRange(editor, GetCursorPosition(editor));
                } else
                {
                    // Search whole document
                    rangeStart = 0;
                    rangeEnd = docLength;
                }
                    
                
            }
            
            // Get current position - use selection start if there's a selection, otherwise cursor position
            int currentPos;
            var (currentSelText, currentSelStart, currentSelEnd) = GetSelectedText(editor);
            if (!string.IsNullOrEmpty(currentSelText))
            {
                // Use selection start for backward search, selection end for forward search
                currentPos = forward ? currentSelEnd : currentSelStart;
            }
            else
            {
                currentPos = GetCursorPosition(editor);
            }
            
            // Set search target based on direction and current position within range
            int targetStart, targetEnd;
            if (forward)
            {
                targetStart = Math.Max(currentPos, rangeStart);
                targetEnd = rangeEnd;
            }
            else
            {
                // For backward search, targetStart > targetEnd to find last match
                targetStart = Math.Min(currentPos, rangeEnd);
                targetEnd = rangeStart;
            }

            // Set the target range
            editor.SendMessage(SCI_SETTARGETRANGE, targetStart, targetEnd);
            
            // Set search flags
            editor.SendMessage(SCI_SETSEARCHFLAGS, searchFlags, IntPtr.Zero);
            
            // Perform the search using marshalled string
            bool found = PerformSearchInTarget(editor, searchState.LastSearchTerm);
            
            if (!found && searchState.WrapSearch)
            {
                // Try wrapping around within the same scope
                if (forward)
                {
                    // Search from beginning of range to current position
                    editor.SendMessage(SCI_SETTARGETRANGE, rangeStart, Math.Min(currentPos, rangeEnd));
                }
                else
                {
                    // For backward search wrapping, targetStart > targetEnd to find last match
                    // Search from end of range to current position
                    editor.SendMessage(SCI_SETTARGETRANGE, rangeEnd, Math.Max(currentPos, rangeStart));
                }
                
                found = PerformSearchInTarget(editor, searchState.LastSearchTerm);
            }
            
            if (found)
            {
                // Get the found text position
                int foundStart = (int)editor.SendMessage(SCI_GETTARGETSTART, IntPtr.Zero, IntPtr.Zero);
                int foundEnd = (int)editor.SendMessage(SCI_GETTARGETEND, IntPtr.Zero, IntPtr.Zero);

                /* Debugging, get the capture groups 1 through 9 */
                /*var lineBuffer = GetProcessBuffer(editor, (uint)editor.SendMessage(SCI_GETLENGTH,0,0));
                for (var x = 1; x <= 9; x++)
                {
                    var tagResp = editor.SendMessage(SCI_GETTAG, x, lineBuffer);

                    byte[] lineData = new byte[tagResp];
                    if (ReadProcessMemory(editor.hProc, lineBuffer, lineData, (int)tagResp, out int lineRead) && lineRead == tagResp)
                    {
                        var lineText = Encoding.Default.GetString(lineData).TrimEnd('\r', '\n');
                    }
                }*/

                // Select the found text and scroll to it
                SetSelection(editor, foundStart, foundEnd);
                //SetCursorPosition(editor, foundEnd);
                
                // Update last match position
                searchState.LastMatchPosition = foundStart;
            }
            
            return found;
        }

        

        /// <summary>
        /// Clears all search highlight indicators from the editor
        /// </summary>
        /// <param name="editor">The editor to clear highlights from</param>
        public static void ClearSearchHighlights(ScintillaEditor editor)
        {
            uint highlightColor = 0x80FFFF00; // Same color used for search highlights
            
            // Remove all indicators of this color
            var indicatorsToRemove = editor.ActiveIndicators
                .Where(i => i.Type == IndicatorType.HIGHLIGHTER && i.Color == highlightColor)
                .ToList();

            foreach (var indicator in indicatorsToRemove)
            {
                RemoveIndicator(editor, indicator);
            }
        }

        /// <summary>
        /// Performs the actual search in the target using marshalled strings
        /// </summary>
        /// <param name="editor">The editor to search in</param>
        /// <param name="searchTerm">The term to search for</param>
        /// <returns>True if text was found, false otherwise</returns>
        private static bool PerformSearchInTarget(ScintillaEditor editor, string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return false;

            // Convert search term to bytes and marshall to remote process
            byte[] searchBytes = Encoding.Default.GetBytes(searchTerm);
            int neededSize = searchBytes.Length + 1; // +1 for null terminator

            var remoteBuffer = GetProcessBuffer(editor, (uint)neededSize);

            // Create buffer with null terminator
            byte[] buffer = new byte[neededSize];
            Buffer.BlockCopy(searchBytes, 0, buffer, 0, searchBytes.Length);
            buffer[neededSize - 1] = 0; // Ensure null termination

            // Write to remote process memory
            if (!WriteProcessMemory(editor.hProc, remoteBuffer, buffer, neededSize, out int bytesWritten) || bytesWritten != neededSize)
                return false;

            // Perform the search
            int result = (int)editor.SendMessage(SCI_SEARCHINTARGET, searchBytes.Length, remoteBuffer);
            
            return result != -1;
        }

        /// <summary>
        /// Builds search flags from the search state
        /// </summary>
        /// <param name="searchState">The search state containing options</param>
        /// <returns>Combined search flags</returns>
        private static int BuildSearchFlags(SearchState searchState)
        {
            int flags = SCFIND_NONE;
            
            if (searchState.MatchCase)
                flags |= SCFIND_MATCHCASE;
            
            if (searchState.WholeWord)
                flags |= SCFIND_WHOLEWORD;
            
            if (searchState.WordStart)
                flags |= SCFIND_WORDSTART;
            
            if (searchState.UseRegex)
            {
                flags |= SCFIND_REGEXP;
                flags |= SCFIND_POSIX;
            }
            
            return flags;
        }

        /// <summary>
        /// Replaces the current selection with the replacement text
        /// </summary>
        /// <param name="editor">The editor to perform replacement in</param>
        /// <param name="replaceText">The text to replace with</param>
        /// <returns>True if replacement was successful, false otherwise</returns>
        public static bool ReplaceSelection(ScintillaEditor editor, string replaceText)
        {
            if (string.IsNullOrEmpty(replaceText))
                replaceText = string.Empty;

            var searchState = editor.SearchState;
            
            // Check if we have a selection
            var (selectedText, start, end) = GetSelectedText(editor);
            if (selectedText == null)
                return false;

            // Set target to current selection
            editor.SendMessage(SCI_SETTARGETRANGE, start, end);
            
            // Marshall replacement text
            byte[] replaceBytes = Encoding.Default.GetBytes(replaceText);
            int neededSize = replaceBytes.Length + 1;
            
            var remoteBuffer = GetProcessBuffer(editor, (uint)neededSize);
            
            byte[] buffer = new byte[neededSize];
            Buffer.BlockCopy(replaceBytes, 0, buffer, 0, replaceBytes.Length);
            buffer[neededSize - 1] = 0;
            
            if (!WriteProcessMemory(editor.hProc, remoteBuffer, buffer, neededSize, out int bytesWritten) || bytesWritten != neededSize)
                return false;

            var replacementLength = replaceText.Length;

            // Perform replacement
            if (searchState.UseRegex)
            {
                replacementLength = (int)editor.SendMessage(SCI_REPLACETARGETRE, replaceBytes.Length, remoteBuffer);
            }
            else
            {
                replacementLength = (int)editor.SendMessage(SCI_REPLACETARGET, replaceBytes.Length, remoteBuffer);
            }

            /* move to the end of the replacement */
            editor.SendMessage(SCI_GOTOPOS, start + replacementLength,0);

            return true;
        }

        public static (bool Success,int Length) ReplaceRange(ScintillaEditor editor, int start, int end, int replaceTextLength, IntPtr replaceTextBuffer)
        {
            var searchState = editor.SearchState;
            var replacementLength = replaceTextLength;

            editor.SendMessage(SCI_SETTARGETRANGE, start, end);

            // Perform replacement
            if (searchState.UseRegex)
            {
                replacementLength = (int)editor.SendMessage(SCI_REPLACETARGETRE, replaceTextLength, replaceTextBuffer);
            }
            else
            {
                replacementLength = (int)editor.SendMessage(SCI_REPLACETARGET, replaceTextLength, replaceTextBuffer);
            }

            return (true,replacementLength);

        }

        /// <summary>
        /// Replaces all occurrences of the search term with the replacement text
        /// </summary>
        /// <param name="editor">The editor to perform replacements in</param>
        /// <param name="searchTerm">The term to search for</param>
        /// <param name="replaceText">The text to replace with</param>
        /// <returns>Number of replacements made</returns>
        public static int ReplaceAll(ScintillaEditor editor, string searchTerm, string replaceText)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return 0;

            var searchState = editor.SearchState;
            var searchFlags = BuildSearchFlags(searchState);
            int replaceCount = 0;

            // Determine search range based on scope preference
            int rangeStart, rangeEnd;
            if (searchState.SearchInSelection && searchState.HasValidSelection)
            {
                rangeStart = searchState.SelectionStart;
                rangeEnd = searchState.SelectionEnd;
            }
            else
            {
                rangeStart = 0;
                rangeEnd = (int)editor.SendMessage(SCI_GETLENGTH, IntPtr.Zero, IntPtr.Zero);
            }

            // Set search flags
            editor.SendMessage(SCI_SETSEARCHFLAGS, searchFlags, IntPtr.Zero);

            // Marshall search string
            byte[] searchBytes = Encoding.Default.GetBytes(searchTerm);
            int searchSize = searchBytes.Length + 1;

            var searchBuffer = GetProcessBuffer(editor, (uint)searchSize);

            byte[] searchBufferData = new byte[searchSize];
            Buffer.BlockCopy(searchBytes, 0, searchBufferData, 0, searchBytes.Length);
            searchBufferData[searchSize - 1] = 0;

            if (!WriteProcessMemory(editor.hProc, searchBuffer, searchBufferData, searchSize, out int searchBytesWritten) ||
                searchBytesWritten != searchSize)
                return 0;

            // Find and mark all matches
            int currentPos = rangeStart;

            /* Set up separate buffer with the replacement text */
            if (string.IsNullOrEmpty(replaceText))
                replaceText = string.Empty;

            // Marshall replacement text
            byte[] replaceBytes = Encoding.Default.GetBytes(replaceText);
            int neededSize = replaceBytes.Length + 1;

            var replaceTextBuffer = GetStandaloneProcessBuffer(editor, (uint)neededSize);

            byte[] buffer = new byte[neededSize];
            Buffer.BlockCopy(replaceBytes, 0, buffer, 0, replaceBytes.Length);
            buffer[neededSize - 1] = 0;

            if (!WriteProcessMemory(editor.hProc, replaceTextBuffer, buffer, neededSize, out int bytesWritten) || bytesWritten != neededSize)
                return ( 0);

            try
            {
                editor.SendMessage(SCI_BEGINUNDOACTION, 0, 0);
                while (true)
                {
                    editor.SendMessage(SCI_SETTARGETRANGE, currentPos, rangeEnd);

                    int result = (int)editor.SendMessage(SCI_SEARCHINTARGET, searchBytes.Length, searchBuffer);
                    if (result == -1)
                        break;

                    int targetStart = (int)editor.SendMessage(SCI_GETTARGETSTART, IntPtr.Zero, IntPtr.Zero);
                    int targetEnd = (int)editor.SendMessage(SCI_GETTARGETEND, IntPtr.Zero, IntPtr.Zero);

                    if (targetStart < rangeStart || targetStart >= rangeEnd)
                        break;

                    var (success, length) = ReplaceRange(editor, targetStart, targetEnd, replaceText.Length, replaceTextBuffer);
                    if (success)
                    {
                        currentPos = targetStart + length;
                        replaceCount++;
                    }
                    else
                    {
                        currentPos = targetEnd;
                    }
                }
            }
            catch (Exception) { }
            finally
            {
                editor.SendMessage(SCI_ENDUNDOACTION, 0, 0);
                FreeStandaloneProcessBuffer(editor, replaceTextBuffer);
            }

            return replaceCount;
        }

        /// <summary>
        /// Counts all matches of the search term in the editor
        /// </summary>
        /// <param name="editor">The editor to count matches in</param>
        /// <param name="searchTerm">The term to search for</param>
        /// <returns>Number of matches found</returns>
        public static int CountMatches(ScintillaEditor editor, string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return 0;

            var searchState = editor.SearchState;
            var searchFlags = BuildSearchFlags(searchState);
            int matchCount = 0;
            
            // Determine search range based on scope preference
            int rangeStart, rangeEnd;
            if (searchState.SearchInSelection && searchState.HasValidSelection)
            {
                rangeStart = searchState.SelectionStart;
                rangeEnd = searchState.SelectionEnd;
            }
            else
            {
                rangeStart = 0;
                rangeEnd = (int)editor.SendMessage(SCI_GETLENGTH, IntPtr.Zero, IntPtr.Zero);
            }
            
            // Set search flags
            editor.SendMessage(SCI_SETSEARCHFLAGS, searchFlags, IntPtr.Zero);
            
            // Marshall search string
            byte[] searchBytes = Encoding.Default.GetBytes(searchTerm);
            int searchSize = searchBytes.Length + 1;
            
            var searchBuffer = GetProcessBuffer(editor, (uint)searchSize);
            
            byte[] searchBufferData = new byte[searchSize];
            Buffer.BlockCopy(searchBytes, 0, searchBufferData, 0, searchBytes.Length);
            searchBufferData[searchSize - 1] = 0;
            
            if (!WriteProcessMemory(editor.hProc, searchBuffer, searchBufferData, searchSize, out int searchBytesWritten) || 
                searchBytesWritten != searchSize)
                return 0;

            // Count matches
            int currentPos = rangeStart;
            while (true)
            {
                editor.SendMessage(SCI_SETTARGETRANGE, currentPos, rangeEnd);
                
                int result = (int)editor.SendMessage(SCI_SEARCHINTARGET, searchBytes.Length, searchBuffer);
                if (result == -1)
                    break;
                
                int targetStart = (int)editor.SendMessage(SCI_GETTARGETSTART, IntPtr.Zero, IntPtr.Zero);
                int targetEnd = (int)editor.SendMessage(SCI_GETTARGETEND, IntPtr.Zero, IntPtr.Zero);
                
                if (targetStart < rangeStart || targetStart >= rangeEnd)
                    break;
                
                matchCount++;
                currentPos = targetEnd;
            }
            
            return matchCount;
        }

        /// <summary>
        /// Creates indicators for all matches of the search term ("Mark All" functionality)
        /// </summary>
        /// <param name="editor">The editor to mark matches in</param>
        /// <param name="searchTerm">The term to search for</param>
        /// <returns>Number of matches marked</returns>
        public static int MarkAllMatches(ScintillaEditor editor, string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return 0;

            var searchState = editor.SearchState;
            var searchFlags = BuildSearchFlags(searchState);
            int matchCount = 0;
            
            // Clear previous search indicators
            ClearSearchIndicators(editor);
            
            // Use distinct color for mark all (light blue)
            uint markColor = 0xFFFF0060;
            int highlighter = editor.GetHighlighter(markColor);
            
            // Determine search range based on scope preference
            int rangeStart, rangeEnd;
            if (searchState.SearchInSelection && searchState.HasValidSelection)
            {
                rangeStart = searchState.SelectionStart;
                rangeEnd = searchState.SelectionEnd;
            }
            else
            {
                rangeStart = 0;
                rangeEnd = (int)editor.SendMessage(SCI_GETLENGTH, IntPtr.Zero, IntPtr.Zero);
            }
            
            // Set search flags
            editor.SendMessage(SCI_SETSEARCHFLAGS, searchFlags, IntPtr.Zero);
            
            // Marshall search string
            byte[] searchBytes = Encoding.Default.GetBytes(searchTerm);
            int searchSize = searchBytes.Length + 1;
            
            var searchBuffer = GetProcessBuffer(editor, (uint)searchSize);
            
            byte[] searchBufferData = new byte[searchSize];
            Buffer.BlockCopy(searchBytes, 0, searchBufferData, 0, searchBytes.Length);
            searchBufferData[searchSize - 1] = 0;
            
            if (!WriteProcessMemory(editor.hProc, searchBuffer, searchBufferData, searchSize, out int searchBytesWritten) || 
                searchBytesWritten != searchSize)
                return 0;

            // Find and mark all matches
            int currentPos = rangeStart;
            while (true)
            {
                editor.SendMessage(SCI_SETTARGETRANGE, currentPos, rangeEnd);
                
                int result = (int)editor.SendMessage(SCI_SEARCHINTARGET, searchBytes.Length, searchBuffer);
                if (result == -1)
                    break;
                
                int targetStart = (int)editor.SendMessage(SCI_GETTARGETSTART, IntPtr.Zero, IntPtr.Zero);
                int targetEnd = (int)editor.SendMessage(SCI_GETTARGETEND, IntPtr.Zero, IntPtr.Zero);
                
                if (targetStart < rangeStart || targetStart >= rangeEnd)
                    break;
                
                // Add search indicator
                var indicator = new Indicator()
                {
                    Type = IndicatorType.HIGHLIGHTER,
                    Color = markColor,
                    Start = targetStart,
                    Length = targetEnd - targetStart
                };
                AddIndicator(editor, indicator);
                editor.SearchIndicators.Add(indicator);
                
                matchCount++;
                currentPos = targetEnd;
            }
            
            return matchCount;
        }

        /// <summary>
        /// Finds all matches of the search term and returns detailed information about each match
        /// </summary>
        /// <param name="editor">The editor to search in</param>
        /// <param name="searchTerm">The term to search for</param>
        /// <returns>List of SearchMatch objects containing position and line information</returns>
        public static List<SearchMatch> FindAllMatches(ScintillaEditor editor, string searchTerm)
        {
            var matches = new List<SearchMatch>();

            if (string.IsNullOrEmpty(searchTerm))
                return matches;

            var searchState = editor.SearchState;
            var searchFlags = BuildSearchFlags(searchState);

            // Determine search range based on scope preference
            int rangeStart, rangeEnd;
            if (searchState.SearchInSelection && searchState.HasValidSelection)
            {
                rangeStart = searchState.SelectionStart;
                rangeEnd = searchState.SelectionEnd;
            }
            else
            {
                rangeStart = 0;
                rangeEnd = (int)editor.SendMessage(SCI_GETLENGTH, IntPtr.Zero, IntPtr.Zero);
            }

            // Set search flags
            editor.SendMessage(SCI_SETSEARCHFLAGS, searchFlags, IntPtr.Zero);

            // Marshall search string
            byte[] searchBytes = Encoding.Default.GetBytes(searchTerm);
            int searchSize = searchBytes.Length + 1;

            var searchBuffer = GetProcessBuffer(editor, (uint)searchSize);

            byte[] searchBufferData = new byte[searchSize];
            Buffer.BlockCopy(searchBytes, 0, searchBufferData, 0, searchBytes.Length);
            searchBufferData[searchSize - 1] = 0;

            if (!WriteProcessMemory(editor.hProc, searchBuffer, searchBufferData, searchSize, out int searchBytesWritten) ||
                searchBytesWritten != searchSize)
                return matches;

            // Count matches
            int currentPos = rangeStart;
            while (true)
            {
                editor.SendMessage(SCI_SETTARGETRANGE, currentPos, rangeEnd);

                int result = (int)editor.SendMessage(SCI_SEARCHINTARGET, searchBytes.Length, searchBuffer);
                if (result == -1)
                    break;

                int targetStart = (int)editor.SendMessage(SCI_GETTARGETSTART, IntPtr.Zero, IntPtr.Zero);
                int targetEnd = (int)editor.SendMessage(SCI_GETTARGETEND, IntPtr.Zero, IntPtr.Zero);

                if (targetStart < rangeStart || targetStart >= rangeEnd)
                    break;

                currentPos = targetEnd;


                /* Add match */
                // Get line information
                int lineNumber = (int)editor.SendMessage(SCI_LINEFROMPOSITION, targetStart, IntPtr.Zero);

                // Create SearchMatch
                var match = new SearchMatch
                {
                    Position = targetStart,
                    Length = targetEnd - targetStart,
                    LineNumber = lineNumber
                };

                matches.Add(match);


            }

            /* Now that the process buffer is free to use, retrieve line information (line text) */
            foreach(var match in matches)
            {
                int lineStart = (int)editor.SendMessage(SCI_POSITIONFROMLINE, match.LineNumber, IntPtr.Zero);
                int lineEnd = (int)editor.SendMessage(SCI_GETLINEENDPOSITION, match.LineNumber, IntPtr.Zero);

                // Get line text
                string lineText = string.Empty;
                if (lineEnd > lineStart)
                {
                    editor.SendMessage(SCI_SETTARGETRANGE, lineStart, lineEnd);
                    int lineLength = lineEnd - lineStart;

                    if (lineLength > 0)
                    {
                        var lineBuffer = GetProcessBuffer(editor, (uint)(lineLength + 1));

                        if (lineBuffer != IntPtr.Zero)
                        {
                            int lineResult = (int)editor.SendMessage(SCI_GETTARGETTEXT, IntPtr.Zero, lineBuffer);
                            if (lineResult > 0)
                            {
                                byte[] lineData = new byte[lineLength];
                                if (ReadProcessMemory(editor.hProc, lineBuffer, lineData, lineLength, out int lineRead) && lineRead == lineLength)
                                {
                                    lineText = Encoding.Default.GetString(lineData).TrimEnd('\r', '\n');
                                    match.LineText = lineText;
                                }
                            }
                        }
                    }
                }
            }

            return matches;
        }

        /// <summary>
        /// Clears all search indicators created by the "Mark All" feature
        /// </summary>
        /// <param name="editor">The editor to clear indicators from</param>
        public static void ClearSearchIndicators(ScintillaEditor editor)
        {
            // Remove all search indicators
            foreach (var indicator in editor.SearchIndicators)
            {
                RemoveIndicator(editor, indicator);
            }
            
            // Clear the list
            editor.SearchIndicators.Clear();
        }

        /// <summary>
        /// Places a bookmark at the current cursor position
        /// </summary>
        /// <param name="editor">The editor to place the bookmark in</param>
        /// <returns>True if bookmark was placed successfully, false otherwise</returns>
        public static bool PlaceBookmark(ScintillaEditor editor)
        {
            if (editor == null) return false;

            try
            {
                // Get current cursor position
                int currentPosition = (int)editor.SendMessage(SCI_GETCURRENTPOS, IntPtr.Zero, IntPtr.Zero);
                
                // Get current first visible line for viewport restoration
                int firstVisibleLine = GetFirstVisibleLine(editor);

                // Get the current line to highlight the entire line
                int currentLine = (int)editor.SendMessage(SCI_LINEFROMPOSITION, currentPosition, IntPtr.Zero);
                int lineStart = (int)editor.SendMessage(SCI_POSITIONFROMLINE, currentLine, IntPtr.Zero);
                int lineEnd = (int)editor.SendMessage(SCI_GETLINEENDPOSITION, currentLine, IntPtr.Zero);

                // Create bookmark indicator with distinct color (gold) for entire line
                uint bookmarkColor = 0x04FFCFFF; // Gold color for bookmarks
                var bookmarkIndicator = new Indicator()
                {
                    Type = IndicatorType.HIGHLIGHTER,
                    Color = bookmarkColor,
                    Start = lineStart,
                    Length = lineEnd - lineStart // Highlight entire line
                };

                // Add indicator to Scintilla and lists
                AddIndicator(editor, bookmarkIndicator);
                editor.BookmarkIndicators.Add(bookmarkIndicator);

                // Create bookmark and push to stack
                var bookmark = new Bookmark
                {
                    Position = currentPosition,
                    FirstVisibleLine = firstVisibleLine,
                    BookmarkIndicator = bookmarkIndicator
                };

                editor.BookmarkStack.Push(bookmark);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Goes to the previous bookmark and removes it from the stack
        /// </summary>
        /// <param name="editor">The editor to navigate in</param>
        /// <returns>True if navigation was successful, false if no bookmarks available</returns>
        public static bool GoToPreviousBookmark(ScintillaEditor editor)
        {
            if (editor == null || editor.BookmarkStack.Count == 0) return false;

            try
            {
                // Pop the most recent bookmark
                var bookmark = editor.BookmarkStack.Pop();

                // Remove the visual indicator
                RemoveIndicator(editor, bookmark.BookmarkIndicator);
                editor.BookmarkIndicators.Remove(bookmark.BookmarkIndicator);

                // Restore cursor position
                editor.SendMessage(SCI_GOTOPOS, bookmark.Position, IntPtr.Zero);

                // Restore viewport (first visible line)
                int currentFirstVisible = GetFirstVisibleLine(editor);
                int lineDifference = bookmark.FirstVisibleLine - currentFirstVisible;
                if (lineDifference != 0)
                {
                    editor.SendMessage(SCI_LINESCROLL, IntPtr.Zero, new IntPtr(lineDifference));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clears all bookmarks and their indicators from the editor
        /// </summary>
        /// <param name="editor">The editor to clear bookmarks from</param>
        public static void ClearBookmarkIndicators(ScintillaEditor editor)
        {
            if (editor == null) return;

            // Remove all bookmark indicators
            foreach (var indicator in editor.BookmarkIndicators)
            {
                RemoveIndicator(editor, indicator);
            }

            // Clear the lists and stack
            editor.BookmarkIndicators.Clear();
            editor.BookmarkStack.Clear();
        }

        /// <summary>
        /// Gets the screen coordinates of a text position in the editor
        /// </summary>
        /// <param name="editor">The editor to get coordinates from</param>
        /// <param name="textPosition">The text position to convert</param>
        /// <returns>Point representing screen coordinates, or Point.Empty if failed</returns>
        public static Point GetTextScreenCoordinates(ScintillaEditor editor, int textPosition)
        {
            try
            {
                // Get relative coordinates within the Scintilla control
                int x = (int)editor.SendMessage(SCI_POINTXFROMPOSITION, IntPtr.Zero, textPosition);
                int y = (int)editor.SendMessage(SCI_POINTYFROMPOSITION, IntPtr.Zero, textPosition);
                
                // Convert to screen coordinates using WindowHelper
                return WindowHelper.ClientToScreen(editor.hWnd, new Point(x, y));
            }
            catch
            {
                return Point.Empty;
            }
        }

        /// <summary>
        /// Gets the screen rectangle occupied by a range of text
        /// </summary>
        /// <param name="editor">The editor to get coordinates from</param>
        /// <param name="startPos">Start position of text range</param>
        /// <param name="endPos">End position of text range</param>
        /// <returns>Rectangle representing screen area of text, or Rectangle.Empty if failed</returns>
        public static Rectangle GetTextScreenRect(ScintillaEditor editor, int startPos, int endPos)
        {
            try
            {
                Point startPoint = GetTextScreenCoordinates(editor, startPos);
                Point endPoint = GetTextScreenCoordinates(editor, endPos);
                
                if (startPoint.IsEmpty || endPoint.IsEmpty)
                    return Rectangle.Empty;
                
                // Create rectangle encompassing the text range
                // Add some padding for better visibility
                int padding = 5;
                int left = Math.Min(startPoint.X, endPoint.X) - padding;
                int top = Math.Min(startPoint.Y, endPoint.Y) - padding;
                int right = Math.Max(startPoint.X, endPoint.X) + padding;
                int bottom = Math.Max(startPoint.Y, endPoint.Y) + padding;
                
                // Estimate text height (using font metrics would be more accurate, but this is simpler)
                int estimatedTextHeight = 20; // Default text height
                bottom = Math.Max(bottom, top + estimatedTextHeight);
                
                return new Rectangle(left, top, right - left, bottom - top);
            }
            catch
            {
                return Rectangle.Empty;
            }
        }

        #endregion
    }
}
