﻿using Antlr4.Runtime;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace AppRefiner
{
    /// <summary>
    /// Represents a bookmark in the editor with position and visual indicator information
    /// </summary>
    public struct Bookmark
    {
        /// <summary>
        /// Character position in the document where the bookmark was placed
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// The first visible line at the time the bookmark was placed (for viewport restoration)
        /// </summary>
        public int FirstVisibleLine { get; set; }

        /// <summary>
        /// The visual indicator associated with this bookmark
        /// </summary>
        public Indicator BookmarkIndicator { get; set; }
    }

    public enum EditorType
    {
        PeopleCode, HTML, SQL, CSS, Other
    }

    public class SearchState
    {
        public string LastSearchTerm { get; set; } = string.Empty;
        public string LastReplaceText { get; set; } = string.Empty;
        public bool MatchCase { get; set; } = false;
        public bool WholeWord { get; set; } = false;
        public bool WordStart { get; set; } = false;
        public bool UseRegex { get; set; } = false;
        public bool UsePosixRegex { get; set; } = false;
        public bool UseCxx11Regex { get; set; } = false;
        public bool WrapSearch { get; set; } = true;
        public bool SearchInSelection { get; set; } = false;
        public bool SearchInMethod { get; set; } = false;
        public int SelectionStart { get; set; } = -1;
        public int SelectionEnd { get; set; } = -1;
        public int LastMatchPosition { get; set; } = -1;
        public int LastSearchFlags { get; set; } = 0;

        // Search history (limited to last 10 searches)
        public List<string> SearchHistory { get; set; } = new();
        public List<string> ReplaceHistory { get; set; } = new();

        // Helper properties
        public bool HasValidSearch => !string.IsNullOrEmpty(LastSearchTerm);
        public bool HasValidSelection => SelectionStart >= 0 && SelectionEnd >= 0 && SelectionStart < SelectionEnd;

        // Helper method to update search history
        public void UpdateSearchHistory(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return;
            
            // Remove if already exists
            SearchHistory.Remove(term);
            // Add to front
            SearchHistory.Insert(0, term);
            // Limit to 10 items
            if (SearchHistory.Count > 10)
                SearchHistory.RemoveAt(10);
        }

        // Helper method to update replace history
        public void UpdateReplaceHistory(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return;
            
            // Remove if already exists
            ReplaceHistory.Remove(term);
            // Add to front
            ReplaceHistory.Insert(0, term);
            // Limit to 10 items
            if (ReplaceHistory.Count > 10)
                ReplaceHistory.RemoveAt(10);
        }
        
        // Helper method to set selection range
        public void SetSelectionRange(int start, int end)
        {
            SelectionStart = start;
            SelectionEnd = end;
        }
        
        // Helper method to clear selection range
        public void ClearSelectionRange()
        {
            SelectionStart = -1;
            SelectionEnd = -1;
        }
    }

    public delegate void CaptionChangedEventHandler(object sender, CaptionChangedEventArgs e);

    public class CaptionChangedEventArgs : EventArgs
    {
        public string? OldCaption { get; }
        public string? NewCaption { get; }

        public CaptionChangedEventArgs(string? oldCaption, string? newCaption)
        {
            OldCaption = oldCaption;
            NewCaption = newCaption;
        }
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
        public EventMapInfo? EventMapInfo = null;
        public string ClassPath = string.Empty;
        private string? _caption = null;

        public event CaptionChangedEventHandler? CaptionChanged;

        protected virtual void OnCaptionChanged(CaptionChangedEventArgs e)
        {
            CaptionChanged?.Invoke(this, e);
        }

        public bool HasCaptionEventHander
        {
            get
            {
                return CaptionChanged != null;
            }
        }

        public string? Caption { 
            get
            {
                return _caption;
            }
            set
            {
                string? previousCaption = _caption; // Capture the caption *before* any change

                // Determine if the caption will actually change.
                bool hasChanged = false;
                if (previousCaption == null)
                {
                    if (value != null)
                    {
                        hasChanged = true;
                    }
                }
                else // previousCaption is not null
                {
                    if (value == null || !previousCaption.Equals(value))
                    {
                        hasChanged = true;
                    }
                }

                _caption = value; // Update the internal field

                // All the logic that depends on the new caption value
                if (_caption != null)
                {
                    if (_caption.Contains("PeopleCode"))
                    {
                        Type = EditorType.PeopleCode;
                    }
                    else if (_caption.Contains("(HTML)"))
                    {
                        Type = EditorType.HTML;
                    }
                    else if (_caption.Contains("(SQL Definition)"))
                    {
                        Type = EditorType.SQL;
                    }
                    else
                    {
                        Type = _caption.Contains("(StyleSheet)") ? EditorType.CSS : EditorType.Other;
                    }
                    DetermineRelativeFilePath();
                    SetEventMapInfo();
                    SetClassPath();
                    
                }

                // If the caption has actually changed, raise the event *after* all other logic.
                if (hasChanged)
                {
                    OnCaptionChanged(new CaptionChangedEventArgs(previousCaption, _caption));
                }
            }
        }
        public bool Initialized = false;
        public bool FoldingEnabled = false;
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
        // Tracks whether the last parse operation was successful (no syntax errors)
        private bool parseSuccessful = true;

        public string? ContentString = null;
        public bool AnnotationsInitialized { get; set; } = false;
        public bool IsDarkMode { get; set; } = false;
        public IntPtr AnnotationStyleOffset = IntPtr.Zero;
        public IDataManager? DataManager = null;

        // Database name associated with this editor
        public string? DBName { get; set; }

        /// <summary>
        /// Gets whether the last parse operation was successful (no syntax errors)
        /// </summary>
        public bool IsParseSuccessful => parseSuccessful;

        // Relative path to the file in the Snapshot database
        public string? RelativePath { get; set; }

        public Dictionary<int, List<Report>> LineToReports = new();

        public Dictionary<uint, int> ColorToHighlighterMap = new();
        public Dictionary<uint, int> ColorToSquiggleMap = new();
        public Dictionary<uint, int> ColorToTextColorMap = new();
        public int NextIndicatorNumber = 0;

        /// <summary>
        /// Stores the currently active indicators in the editor
        /// </summary>
        public List<Indicator> ActiveIndicators { get; set; } = new List<Indicator>();

        /// <summary>
        /// Search state for this editor instance
        /// </summary>
        public SearchState SearchState { get; set; } = new SearchState();

        /// <summary>
        /// Stores the search-related indicators created by the "Mark All" feature
        /// </summary>
        public List<Indicator> SearchIndicators { get; set; } = new List<Indicator>();

        /// <summary>
        /// Stores the bookmark indicators for this editor
        /// </summary>
        public List<Indicator> BookmarkIndicators { get; set; } = new List<Indicator>();

        /// <summary>
        /// Stack of bookmarks for navigation (LIFO - most recent bookmark first)
        /// </summary>
        public Stack<Bookmark> BookmarkStack { get; set; } = new Stack<Bookmark>();

        /* static var to capture total time spent parsing */
        public static Stopwatch TotalParseTime { get; set; } = new Stopwatch();

        public List<List<int>> CollapsedFoldPaths { get; set; } = [];

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

            // Check if parsing was successful (no syntax errors)
            parseSuccessful = parser.NumberOfSyntaxErrors == 0;
            if (!parseSuccessful)
            {
                Debug.Log($"Parse completed with {parser.NumberOfSyntaxErrors} syntax error(s)");
            }

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

            PopulateEditorDBName();

            ProcessId = procID;
            ThreadID = threadID;
            Caption = caption;

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

        private void SetClassPath()
        {
            if (this.Caption == null) return;
            if (this.Caption.EndsWith("(Application Package PeopleCode)"))
            {
                var parts = this.Caption.Replace(" (Application Package PeopleCode)", "").Split('.', StringSplitOptions.None);
                ClassPath = string.Join(":", parts.SkipLast(1));
            } else
            {
                ClassPath = string.Empty;
            }
        }
        private void SetEventMapInfo()
        {
            if (this.Caption == null) return;

            
            if (this.Caption.EndsWith("(Component PeopleCode)"))
            {
                var info = new EventMapInfo();
                var parts = this.Caption.Replace(" (Component PeopleCode)", "").Split('.', StringSplitOptions.None);
                if (parts.Length == 3)
                {
                    info.Type = EventMapType.Component;
                    info.Component = parts[0];
                    info.Segment = parts[1];
                    info.ComponentEvent = EventMapInfo.EventToXlat(parts[2]);
                } else if (parts.Length == 4)
                {
                    info.Type = EventMapType.ComponentRecord;
                    info.Component = parts[0];
                    info.Segment = parts[1];
                    info.Record = parts[2];
                    info.ComponentRecordEvent = EventMapInfo.EventToXlat(parts[3]);
                } else if (parts.Length == 5)
                {
                    info.Type = EventMapType.ComponentRecordField;
                    info.Component = parts[0];
                    info.Segment = parts[1];
                    info.Record = parts[2];
                    info.Field = parts[3];
                    info.ComponentRecordEvent = EventMapInfo.EventToXlat(parts[4]);
                }
                this.EventMapInfo = info;
            } else if (this.Caption.EndsWith(" (Page PeopleCode)"))
            {
                var info = new EventMapInfo();
                var parts = this.Caption.Replace(" (Page PeopleCode)", "").Split('.', StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    info.Type = EventMapType.Page;
                    info.Page = parts[0];
                    info.ComponentRecordEvent = EventMapInfo.EventToXlat(parts[1]);
                }
                this.EventMapInfo = info;
            } else
            {
                this.EventMapInfo = null;
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
