using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner
{
    // FoldLevelInfo struct and related methods will be removed.

    public class FoldingManager
    {
        private struct PathBuilderNode
        {
            public int Level { get; } // Scintilla fold level
            public int CurrentChildIndex { get; set; } // 0-based index for the next child of THIS node

            public PathBuilderNode(int level)
            {
                Level = level;
                CurrentChildIndex = 0;
            }
        }

        public static List<List<int>> GetCollapsedFoldPathsDirectly(ScintillaEditor editor)
        {
            List<List<int>> collapsedPaths = new List<List<int>>();
            if (editor == null) return collapsedPaths;

            Stack<PathBuilderNode> hierarchyStack = new Stack<PathBuilderNode>();
            List<int> currentPath = new List<int>(); // Stores the 0-based indices forming the current path

            int lineCount = ScintillaManager.GetLineCount(editor);
            int rootFoldCount = 0; // To assign indices to top-level folds

            for (int i = 0; i < lineCount; i++)
            {
                var (numericLevel, isHeader) = ScintillaManager.GetCurrentLineFoldLevel(editor, i);

                if (isHeader)
                {
                    // Pop from stack while new level is not a child of the stack top
                    // or if it's a sibling (same level) or an uncle (lower level).
                    while (hierarchyStack.Count > 0 && numericLevel <= hierarchyStack.Peek().Level)
                    {
                        hierarchyStack.Pop();
                        if (currentPath.Count > 0)
                        {
                            currentPath.RemoveAt(currentPath.Count - 1);
                        }
                    }

                    int assignedIndex;
                    if (hierarchyStack.Count == 0)
                    {
                        // This is a root-level fold
                        assignedIndex = rootFoldCount;
                        rootFoldCount++;
                    }
                    else
                    {
                        // This is a child of the fold at the top of the stack
                        PathBuilderNode parentNode = hierarchyStack.Pop(); // Pop to update CurrentChildIndex
                        assignedIndex = parentNode.CurrentChildIndex;
                        parentNode.CurrentChildIndex++;
                        hierarchyStack.Push(parentNode); // Push updated parent back
                    }

                    currentPath.Add(assignedIndex);
                    hierarchyStack.Push(new PathBuilderNode(numericLevel));

                    bool isCollapsed = ScintillaManager.IsLineFolded(editor, i);
                    if (isCollapsed)
                    {
                        collapsedPaths.Add(new List<int>(currentPath));
                    }
                }
            }
            return collapsedPaths;
        }

        public static void ProcessFolding(ScintillaEditor editor)
        {
            if (editor.FoldingEnabled && (!editor.HasLexilla || editor.Type == EditorType.SQL || editor.Type == EditorType.Other))
            {
                // Ensure the activeEditor is not null before proceeding
                if (editor != null)
                {
                    ScintillaManager.SetFoldRegions(editor);
                }
            }
        }

        public static void FoldAppRefinerRegions(ScintillaEditor editor)
        {

            if (editor == null || editor.ContentString == null) return;

            var content = editor.ContentString;
            if (!content.Contains("/* #region")) return; // No regions to fold

            var lines = content.Split('\n', StringSplitOptions.None);
            var regionStart = 0;
            var regionEnd = 0;
            var collapseByDefault = false;
            for (var x = 0; x < lines.Length; x++)
            {
                var line = lines[x].TrimStart();

                if (line.StartsWith("/* #region"))
                {
                    /* If the next character is a - we will default this region to collapsed */
            if (line.Length > 10 && line[10] == '-')
                    {
                        collapseByDefault = true;
                    }

                    // Fold the line
                    regionStart = x;

                }
                else if (line.StartsWith("/* #endregion"))
                {
                    // Unfold the line
                    regionEnd = x;


                    if (regionStart != 0 && regionEnd != 0 && regionEnd > regionStart)
                    {
                        // Unfold the line
                        ScintillaManager.SetExplicitFoldRegion(editor, regionStart, regionEnd, collapseByDefault);

                    }
                }

            }
        }

        public static void PrintCollapsedFoldPathsDebug(List<List<int>> collapsedPaths)
        {
            Debug.Log("---- Start Collapsed Fold Paths ----");
            if (collapsedPaths == null || collapsedPaths.Count == 0)
            {
                Debug.Log("No collapsed folds found.");
            }
            else
            {
                foreach (var path in collapsedPaths)
                {
                    Debug.Log("Path: " + string.Join(" -> ", path));
                }
            }
            Debug.Log("---- End Collapsed Fold Paths ----");
        }

        private static string PathToString(List<int> path)
        {
            return string.Join("_", path);
        }

        public static void ApplyCollapsedFoldPaths(ScintillaEditor editor, List<List<int>> pathsToCollapse)
        {
            if (editor == null || pathsToCollapse == null || pathsToCollapse.Count == 0)
            {
                return;
            }

            // For efficient lookup
            HashSet<string> collapseTargetPaths = new HashSet<string>(pathsToCollapse.Select(PathToString));

            Stack<PathBuilderNode> hierarchyStack = new Stack<PathBuilderNode>();
            List<int> currentPath = new List<int>(); // Stores the 0-based indices forming the current path

            int lineCount = ScintillaManager.GetLineCount(editor);
            int rootFoldCount = 0; // To assign 0-based indices to top-level folds

            for (int currentLineNumber = 0; currentLineNumber < lineCount; currentLineNumber++)
            {
                var (numericLevel, isHeader) = ScintillaManager.GetCurrentLineFoldLevel(editor, currentLineNumber);

                if (isHeader)
                {
                    // Pop from stack while new level is not a child of the stack top
                    // or if it's a sibling (same level) or an uncle (lower level).
                    while (hierarchyStack.Count > 0 && numericLevel <= hierarchyStack.Peek().Level)
                    {
                        hierarchyStack.Pop();
                        if (currentPath.Count > 0)
                        {
                            currentPath.RemoveAt(currentPath.Count - 1);
                        }
                    }

                    int assignedIndex;
                    if (hierarchyStack.Count == 0)
                    {
                        // This is a root-level fold
                        assignedIndex = rootFoldCount;
                        rootFoldCount++;
                    }
                    else
                    {
                        // This is a child of the fold at the top of the stack
                        PathBuilderNode parentNode = hierarchyStack.Pop(); // Pop to update CurrentChildIndex
                        assignedIndex = parentNode.CurrentChildIndex;
                        parentNode.CurrentChildIndex++;
                        hierarchyStack.Push(parentNode); // Push updated parent back
                    }

                    currentPath.Add(assignedIndex);
                    hierarchyStack.Push(new PathBuilderNode(numericLevel)); // Level and CurrentChildIndex=0 for the new node

                    string currentPathStr = PathToString(currentPath);
                    if (collapseTargetPaths.Contains(currentPathStr))
                    {
                        ScintillaManager.SetLineFoldStatus(editor, currentLineNumber, true);
                    }
                }
            }
        }
    }
}
