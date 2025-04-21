using Antlr4.Runtime;
using AppRefiner.Database;
using AppRefiner.Linters;
using AppRefiner.PeopleCode;
using AppRefiner.Stylers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner
{
    public enum EditorType
    {
        PeopleCode, HTML, SQL, CSS, Other
    }

    public class ScintillaEditor
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        public IntPtr hWnd;
        public IntPtr hProc;
        public uint ProcessId;
        public uint ThreadID;
        public string? Caption = null;
        public bool FoldEnabled = false;
        public bool HasLexilla = false;

        public EditorType Type;

        public IntPtr CallTipPointer = IntPtr.Zero;
        public IntPtr AutoCompletionPointer = IntPtr.Zero;
        public IntPtr UserListPointer = IntPtr.Zero;

        public Dictionary<string, IntPtr> AnnotationPointers = new();
        public List<IntPtr> PropertyBuffers = new();

        // Content hash for caching purposes
        private int contentHash;
        // Cached parsed program
        private PeopleCodeParser.ProgramContext? parsedProgram;
        // Cached token stream
        private CommonTokenStream? tokenStream;
        // Collection of comments from the token stream
        private List<IToken>? comments;

        public string? ContentString = null;
        public bool AnnotationsInitialized { get; set; } = false;
        public bool IsDarkMode { get; set; } = false;
        public IntPtr AnnotationStyleOffset = IntPtr.Zero;
        public IDataManager? DataManager = null;

        // Database name associated with this editor
        public string? DBName { get; set; }

        // Relative path to the file in the Git repository
        public string? RelativePath { get; set; }

        public Dictionary<int, List<Report>> LineToReports = new();

        public Dictionary<uint, int> ColorToHighlighterMap = new();
        public Dictionary<uint, int> ColorToSquiggleMap = new();
        public Dictionary<uint, int> ColorToTextColorMap = new();
        public int NextIndicatorNumber = 0;

        /// <summary>
        /// Stores highlight tooltips created by stylers for displaying when hovering over highlighted text
        /// </summary>
        public Dictionary<(int Start, int Length), string> HighlightTooltips { get; set; } = new Dictionary<(int Start, int Length), string>();

        /// <summary>
        /// Stores the currently active indicators in the editor
        /// </summary>
        public List<Indicator> ActiveIndicators { get; set; } = new List<Indicator>();


        /* static var to capture total time spent parsing */
        public static Stopwatch TotalParseTime { get; set; } = new Stopwatch();

        /// <summary>
        /// Gets or creates a parsed program tree for the current editor content.
        /// Uses caching to avoid re-parsing unchanged content.
        /// </summary>
        /// <param name="forceReparse">Force a new parse regardless of content hash</param>
        /// <returns>A tuple containing the parsed program, token stream, and comments</returns>
        public (PeopleCodeParser.ProgramContext Program, CommonTokenStream TokenStream, List<IToken> Comments) GetParsedProgram(bool forceReparse = false)
        {
            // Ensure we have the current content
            ContentString = ScintillaManager.GetScintillaText(this);

            // Calculate hash of current content
            int newHash = ContentString?.GetHashCode() ?? 0;
            Debug.Log("New content hash: " + newHash);
            // If content hasn't changed and we have a cached parse tree, return it
            if (!forceReparse && newHash == contentHash && parsedProgram != null && tokenStream != null && comments != null)
            {
                return (parsedProgram, tokenStream, comments);
            }

            // Content has changed or we don't have a cached parse tree, parse it

            var currentStart = TotalParseTime.Elapsed;
            TotalParseTime.Start();
            // Create lexer and parser
            PeopleCodeLexer lexer = new(new AntlrInputStream(ContentString));
            tokenStream = new CommonTokenStream(lexer);

            // Get all tokens including those on hidden channels
            tokenStream.Fill();

            // Collect all comments from both comment channels
            comments = tokenStream.GetTokens()
                .Where(token => token.Channel == PeopleCodeLexer.COMMENTS || token.Channel == PeopleCodeLexer.API_COMMENTS)
                .ToList();

            PeopleCodeParser parser = new(tokenStream);
            parsedProgram = parser.program();

            TotalParseTime.Stop();
            Debug.Log($"Parse time: {TotalParseTime.Elapsed - currentStart}");
            Debug.Log($"Total parse time: {TotalParseTime.Elapsed}");
            // Clean up resources
            parser.Interpreter.ClearDFA();
            GC.Collect();

            // Update the content hash
            contentHash = newHash;

            return (parsedProgram, tokenStream, comments);
        }

        /// <summary>
        /// Gets a highlighter number for the specified BGRA color.
        /// If the color doesn't exist in the dictionary, it creates a new highlighter.
        /// </summary>
        /// <param name="color">The BGRA color value</param>
        /// <returns>The highlighter number</returns>
        public int GetHighlighter(uint color)
        {
            if (ColorToHighlighterMap.TryGetValue(color, out int value))
            {
                return value;
            }
            else
            {
                return ScintillaManager.CreateHighlighter(this, color);
            }
        }

        /// <summary>
        /// Gets a squiggle indicator number for the specified BGRA color.
        /// If the color doesn't exist in the dictionary, it creates a new squiggle.
        /// </summary>
        /// <param name="color">The BGRA color value</param>
        /// <returns>The squiggle indicator number</returns>
        public int GetSquiggle(uint color)
        {
            if (ColorToSquiggleMap.TryGetValue(color, out int value))
            {
                return value;
            }
            else
            {
                return ScintillaManager.CreateSquiggle(this, color);
            }
        }

        /// <summary>
        /// Gets a text color indicator number for the specified BGRA color.
        /// If the color doesn't exist in the dictionary, it creates a new text color indicator.
        /// </summary>
        /// <param name="color">The BGRA color value</param>
        /// <returns>The text color indicator number</returns>
        public int GetTextColor(uint color)
        {
            if (ColorToTextColorMap.TryGetValue(color, out int value))
            {
                return value;
            }
            else
            {
                return ScintillaManager.CreateTextColor(this, color);
            }
        }

        public ScintillaEditor(IntPtr hWnd, uint procID, uint threadID, string caption)
        {
            this.hWnd = hWnd;
            ProcessId = procID;
            ThreadID = threadID;
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
            else
            {
                Type = caption.Contains("(StyleSheet)") ? EditorType.CSS : EditorType.Other;
            }

            PopulateEditorDBName();
            DetermineRelativeFilePath();
        }

        public IntPtr SendMessage(int Msg, IntPtr wParam, IntPtr lParam)
        {
            //Debug.Log($"Sending message {Msg} to {hWnd:X} - {wParam:X} -- {lParam:X}");
            return SendMessage(hWnd, Msg, wParam, lParam);
        }

        public void SetLinterReports(List<Report> reports)
        {
            /* clear out any old lists and then clear the dictionary */
            foreach (var list in LineToReports.Values)
            {
                list.Clear();
            }
            LineToReports.Clear();

            /* for each report, add it to the editor's LineToReports dictionary */
            foreach (var report in reports)
            {
                if (!LineToReports.ContainsKey(report.Line))
                {
                    LineToReports[report.Line] = new List<Report>();
                }
                LineToReports[report.Line].Add(report);
            }
        }

        public bool IsValid()
        {
            return IsWindow(hWnd);
        }

        /// <summary>
        /// Populates the DBName property of a ScintillaEditor based on its context
        /// </summary>
        /// <param name="editor">The editor to populate the DBName for</param>
        private void PopulateEditorDBName()
        {
            // Start with the editor's handle
            IntPtr hwnd = this.hWnd;

            // Walk up the parent chain until we find the Application Designer window
            while (hwnd != IntPtr.Zero)
            {
                StringBuilder caption = new StringBuilder(256);
                NativeMethods.GetWindowText(hwnd, caption, caption.Capacity); // Use NativeMethods
                string windowTitle = caption.ToString();

                // Check if this is the Application Designer window
                if (windowTitle.StartsWith("Application Designer"))
                {
                    // Split the title by " - " and get the second part (DB name)
                    string[] parts = windowTitle.Split(new[] { " - " }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        this.DBName = parts[1].Trim();
                        Debug.Log($"Set editor DBName to: {this.DBName}");

                        // Now determine the relative file path based on the database name
                        break;
                    }
                }

                // Get the parent window
                hwnd = NativeMethods.GetParent(hwnd); // Use NativeMethods
            }
        }

        /// <summary>
        /// Determines the relative file path for an editor based on its window hierarchy and caption
        /// </summary>
        /// <param name="editor">The editor to determine the relative path for</param>
        private void DetermineRelativeFilePath()
        {
            try
            {
                // Start with the editor's handle
                IntPtr hwnd = this.hWnd;

                // Get the grandparent window to examine its caption
                IntPtr parentHwnd = NativeMethods.GetParent(hwnd); // Use NativeMethods
                if (parentHwnd != IntPtr.Zero)
                {
                    IntPtr grandparentHwnd = NativeMethods.GetParent(parentHwnd); // Use NativeMethods
                    if (grandparentHwnd != IntPtr.Zero)
                    {
                        StringBuilder caption = new StringBuilder(512);
                        NativeMethods.GetWindowText(grandparentHwnd, caption, caption.Capacity); // Use NativeMethods
                        string windowTitle = caption.ToString().Trim();

                        Debug.Log($"Editor grandparent window title: {windowTitle}");

                        // Determine editor type and generate appropriate relative path
                        string? relativePath = DetermineRelativePathFromCaption(windowTitle, this.DBName);

                        if (!string.IsNullOrEmpty(relativePath))
                        {
                            this.RelativePath = relativePath;
                            Debug.Log($"Set editor RelativePath to: {relativePath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error determining relative path: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines the relative file path based on the window caption and database name
        /// </summary>
        /// <param name="caption">The window caption</param>
        /// <param name="dbName">The database name</param>
        /// <returns>The relative file path or null if can't be determined</returns>
        private string? DetermineRelativePathFromCaption(string caption, string? dbName)
        {
            // This handles specific parsing logic for different PeopleCode editor types
            // as indicated by the (type) suffix in the caption

            if (string.IsNullOrEmpty(caption))
                return null;

            Debug.Log($"Determining path from caption: {caption}");

            // Check for PeopleCode type in caption - usually appears as (PeopleCode) or some other type suffix
            int typeStartIndex = caption.LastIndexOf('(');
            int typeEndIndex = caption.LastIndexOf(')');

            if (typeStartIndex >= 0 && typeEndIndex > typeStartIndex)
            {
                string editorType = caption.Substring(typeStartIndex + 1, typeEndIndex - typeStartIndex - 1);
                string captionWithoutType = caption.Substring(0, typeStartIndex).Trim();

                Debug.Log($"Editor type: {editorType}, Caption without type: {captionWithoutType}");

                // Different logic based on editor type
                switch (editorType)
                {
                    case "App Engine Program PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "app_engine");
                    case "Application Package PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "app_package");
                    case "Component Interface PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "comp_intfc");
                    case "Menu PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "menu");
                    case "Message PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "message");
                    case "Page PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "page");
                    case "Record PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "record");
                    case "Component PeopleCode":
                        return DeterminePeopleCodePath(captionWithoutType, dbName, "component");

                    case "SQL Definition":
                        return DetermineSqlDefinitionPath(captionWithoutType, dbName);

                    case "HTML":
                        return DetermineHtmlPath(captionWithoutType, dbName);

                    case "StyleSheet":
                    case "Style Sheet":
                        return DetermineStyleSheetPath(captionWithoutType, dbName);

                    default:
                        // If it contains "PeopleCode" anywhere, treat it as PeopleCode
                        if (editorType.Contains("PeopleCode"))
                        {
                            return DeterminePeopleCodePath(captionWithoutType, dbName, "peoplecode");
                        }

                        // Generic handling for unknown types
                        return $"unknown/{editorType.ToLower().Replace(" ", "_")}/{captionWithoutType.Replace(" ", "_").ToLower()}.txt";
                }
            }

            // No recognized type in caption
            return null;
        }

        /// <summary>
        /// Determines the relative file path for PeopleCode based on editor caption
        /// </summary>
        private string? DeterminePeopleCodePath(string captionWithoutType, string? dbName, string relativeRoot)
        {
            // DB name should always be present at this point, but default if not
            string db = dbName ?? "unknown_db";

            // Clean up the caption
            captionWithoutType = captionWithoutType.Trim();

            // PeopleCode paths follow a specific format
            string pcPath = $"{db.ToLower()}/peoplecode/{relativeRoot}/{captionWithoutType.Replace(".", "/")}.pcode";

            Debug.Log($"PeopleCode path: {pcPath}");

            return pcPath;
        }

        /// <summary>
        /// Determines the relative file path for SQL definitions
        /// </summary>
        private string? DetermineSqlDefinitionPath(string captionWithoutType, string? dbName)
        {
            // DB name should always be present at this point, but default if not
            string db = dbName ?? "unknown_db";

            // Clean up the caption
            captionWithoutType = captionWithoutType.Trim();

            // SQL objects use a specific suffix format like .0 or .2
            string sqlType = "sql_object";

            // Check for SQL type indicators at the end (.0, .1, .2, etc.)
            // Format is typically NAME.NUMBER
            int lastDotPos = captionWithoutType.LastIndexOf('.');
            if (lastDotPos >= 0 && lastDotPos < captionWithoutType.Length - 1)
            {
                // Try to parse the suffix after the last dot
                string suffix = captionWithoutType.Substring(lastDotPos + 1);

                // If the suffix is a number, determine the SQL type
                if (int.TryParse(suffix, out int sqlTypeNumber))
                {
                    switch (sqlTypeNumber)
                    {
                        case 0:
                            sqlType = "sql_object";
                            break;
                        case 2:
                            sqlType = "sql_view";
                            break;
                        default:
                            sqlType = $"sql_type_{sqlTypeNumber}";
                            break;
                    }

                    // Remove the suffix for the path
                    captionWithoutType = captionWithoutType.Substring(0, lastDotPos);
                }
            }

            // Build the full path
            string fullPath = $"{db.ToLower()}/sql/{sqlType}/{captionWithoutType}.sql";

            Debug.Log($"SQL path: {fullPath}");

            return fullPath;
        }

        /// <summary>
        /// Determines the relative file path for an HTML editor
        /// </summary>
        private string? DetermineHtmlPath(string captionWithoutType, string? dbName)
        {
            // DB name should always be present at this point, but default if not
            string db = dbName ?? "unknown_db";

            // HTML paths follow a specific format
            string htmlPath = $"{db.ToLower()}/html/{captionWithoutType.Replace(".", "/")}.html";

            Debug.Log($"HTML path: {htmlPath}");

            return htmlPath;
        }

        /// <summary>
        /// Determines the relative file path for a stylesheet editor
        /// </summary>
        private string? DetermineStyleSheetPath(string captionWithoutType, string? dbName)
        {
            // DB name should always be present at this point, but default if not
            string db = dbName ?? "unknown_db";

            // Stylesheet paths follow a specific format
            string cssPath = $"{db.ToLower()}/stylesheet/{captionWithoutType.Replace(".", "/")}.css";

            Debug.Log($"Stylesheet path: {cssPath}");

            return cssPath;
        }

    }
}
