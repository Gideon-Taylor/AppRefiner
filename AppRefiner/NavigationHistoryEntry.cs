using AppRefiner.Database.Models;
using PeopleCodeParser.SelfHosted;
using System;

namespace AppRefiner
{
    /// <summary>
    /// Represents a location in the navigation history for Go To Definition (F12) navigation
    /// </summary>
    public class NavigationHistoryEntry
    {
        /// <summary>
        /// The target that can be opened to navigate to this location
        /// </summary>
        public OpenTarget OpenTarget { get; set; }

        /// <summary>
        /// The cursor position and selection at this location
        /// </summary>
        public SourceSpan SourceSpan { get; set; }

        /// <summary>
        /// The first visible line in the editor for scroll position restoration
        /// </summary>
        public int FirstVisibleLine { get; set; }

        /// <summary>
        /// The editor handle for validation (to check if still valid)
        /// </summary>
        public IntPtr EditorHandle { get; set; }

        public NavigationHistoryEntry(OpenTarget openTarget, SourceSpan sourceSpan, int firstVisibleLine, IntPtr editorHandle)
        {
            OpenTarget = openTarget;
            SourceSpan = sourceSpan;
            FirstVisibleLine = firstVisibleLine;
            EditorHandle = editorHandle;
        }
    }
}
