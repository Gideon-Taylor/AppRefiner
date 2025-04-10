using Antlr4.Runtime;
using System.Text;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that resolves all class references in the code and creates explicit imports for each one
    /// </summary>
    public class ResolveImports(ScintillaEditor editor) : BaseRefactor(editor)
    {
        public new static string RefactorName => "Resolve Imports";
        public new static string RefactorDescription => "Resolves all class references in the code and creates explicit imports for each one";
        // Tracks unique application class paths used in the code
        private readonly HashSet<string> usedClassPaths = [];

        // The imports block if found
        private ImportsBlockContext? importsBlockContext;

        // Whether we're tracking class references after the imports block
        private bool trackingReferences = false;

        /// <summary>
        /// Gets whether this refactor should register a keyboard shortcut
        /// </summary>
        public new static bool RegisterKeyboardShortcut => true;

        /// <summary>
        /// Gets the modifier keys for the keyboard shortcut
        /// </summary>
        public new static ModifierKeys ShortcutModifiers => ModifierKeys.Control | ModifierKeys.Shift;

        /// <summary>
        /// Gets the key for the keyboard shortcut
        /// </summary>
        public new static Keys ShortcutKey => Keys.I;

        /// <summary>
        /// When entering an app class path, record it as used if we're in tracking mode
        /// </summary>
        public override void EnterAppClassPath(AppClassPathContext context)
        {
            if (!trackingReferences) return;

            string classPath = context.GetText();
            // Only add if it's a fully qualified class path
            if (classPath.Contains(":"))
            {
                usedClassPaths.Add(classPath);
            }
        }

        /// <summary>
        /// When we find the imports block, store it and start tracking class references
        /// </summary>
        public override void ExitImportsBlock(ImportsBlockContext context)
        {
            importsBlockContext = context;
            trackingReferences = true;
        }

        /// <summary>
        /// When entering the program, start tracking if no imports block was found
        /// </summary>
        public override void EnterProgram(ProgramContext context)
        {
            if (importsBlockContext == null)
            {
                trackingReferences = true;
            }
        }

        /// <summary>
        /// When we finish the program, generate the new imports block
        /// </summary>
        public override void ExitProgram(ProgramContext context)
        {
            // Skip if no class references were found
            if (usedClassPaths.Count == 0) return;

            // Generate the new imports block with explicit imports
            var newImports = new StringBuilder();

            // Order the imports by the full path
            var orderedImports = usedClassPaths
                .OrderBy(path => path)
                .ToList();

            // Generate explicit import for each class path
            foreach (var classPath in orderedImports)
            {
                newImports.AppendLine($"import {classPath};");
            }

            // Trim trailing newlines to prevent accumulation of blank lines
            string imports = newImports.ToString().TrimEnd();

            if (importsBlockContext?.importDeclaration().Length == 0)
            {
                imports += "\r\n\r\n";
            }

            if (importsBlockContext != null)
            {
                // Replace the existing imports block
                ReplaceNode(importsBlockContext, imports, "Resolve imports");
            }
            else
            {
                // No existing imports block, so add one at the beginning of the program
                var firstChild = context.GetChild(0);
                if (firstChild != null)
                {
                    // Check if firstChild is a parser rule context
                    if (firstChild is ParserRuleContext firstChildContext)
                    {
                        // Add exactly two newlines after imports (consistent spacing)
                        string insertText = imports + Environment.NewLine + Environment.NewLine;
                        
                        // Check if the first node already contains imports to avoid adding excessive spacing
                        string? firstNodeText = GetOriginalText(firstChildContext);
                        if (firstNodeText != null && firstNodeText.TrimStart().StartsWith("import "))
                        {
                            // If first node already has imports, don't add extra newlines
                            insertText = imports + Environment.NewLine;
                        }
                        
                        InsertBefore(firstChildContext, insertText, "Add missing imports");
                    }
                    else
                    {
                        // Fall back to using InsertText if the cast fails
                        InsertText(context.Start.StartIndex,
                            imports + Environment.NewLine + Environment.NewLine,
                            "Add missing imports");
                    }
                }
                else
                {
                    // Empty program, so just add the imports at the start
                    InsertText(context.Start.StartIndex,
                        imports + Environment.NewLine + Environment.NewLine,
                        "Add missing imports");
                }
            }
        }
    }
}
