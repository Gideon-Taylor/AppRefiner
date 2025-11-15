using PeopleCodeParser.SelfHosted.Nodes;
using System;
using System.Linq;
using System.Xml.Linq;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that declares a function if it is not already declared.
    /// </summary>
    public class DeclareFunction : BaseRefactor
    {
        public new static string RefactorName => "Declare Function";
        public new static string RefactorDescription => "Declares a function if not already declared.";

        /// <summary>
        /// This refactor should not have a keyboard shortcut.
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from discovery.
        /// </summary>
        public new static bool IsHidden => true;

        private readonly FunctionSearchResult _functionToDeclare;
        private ProgramNode? _programNode;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeclareFunction"/> class with a specific function to declare.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        /// <param name="functionToDeclare">The function search result to declare.</param>
        public DeclareFunction(AppRefiner.ScintillaEditor editor, FunctionSearchResult functionToDeclare) : base(editor)
        {
            if (functionToDeclare == null)
                throw new ArgumentException("Function to declare cannot be null", nameof(functionToDeclare));

            _functionToDeclare = functionToDeclare;
        }

        public override void VisitProgram(ProgramNode node)
        {
            _programNode = node;

            if (_programNode != null)
            {
                bool alreadyDeclared = _programNode.Functions.Any(f => f.IsDeclaration && 
                    string.Equals(f.Name, _functionToDeclare.FunctionName, StringComparison.OrdinalIgnoreCase));
                var insertIndex = 0;
                var declarationString = "";
                if (!alreadyDeclared)
                {
                    var lastDeclaration = node.Functions.OrderBy(f => f.SourceSpan.Start.Line).LastOrDefault<FunctionNode>(f => f.IsDeclaration);
                    var firstFuncImpl = node.Functions.Where(f => f.IsImplementation).OrderBy(f => f.SourceSpan.Start.Line).FirstOrDefault();

                    var padding = "";
                    var insertLine = -1;
                    if (lastDeclaration != null)
                    {
                        /* get the padding of this declarations line */
                        insertLine = lastDeclaration.SourceSpan.Start.Line + 1;
                        var paddingCount = CountLeadingSpaces(ScintillaManager.GetLineText(Editor, lastDeclaration.SourceSpan.Start.Line));
                        padding = new string(' ', paddingCount);
                    }
                    else if (node.AppClass != null)
                    {
                        insertLine = node.AppClass.SourceSpan.End.Line + 1;
                    }
                    else if (node.Imports.Count > 0)
                    {
                        insertLine = node.Imports.Last().SourceSpan.Start.Line + 1;
                    }
                    else if (firstFuncImpl != null)
                    {
                        insertLine = firstFuncImpl.SourceSpan.Start.Line - 1;
                        var firstLeadingComment = firstFuncImpl.GetLeadingComments().FirstOrDefault();
                        if (firstLeadingComment != null)
                        {
                            insertLine = firstLeadingComment.SourceSpan.Start.Line - 1;
                        }
                    }

                    if (insertLine < 0) insertLine = 0;

                    insertIndex = ScintillaManager.GetLineStartIndex(Editor, insertLine);


                    declarationString = $"{padding}{_functionToDeclare.ToDeclaration()}{Environment.NewLine}";
                }

                if (insertIndex >= 0)
                {
                    var funcCallIndex = CurrentPosition;
                    var exampleCall = _functionToDeclare.GetExampleCall();
                    if (CurrentPosition == insertIndex)
                    {
                        InsertText(funcCallIndex, exampleCall, "Insert example call of function");
                        InsertText(insertIndex, declarationString, "Insert function declaration");

                    }
                    else
                    {
                        InsertText(insertIndex, declarationString, "Insert function declaration");
                        InsertText(funcCallIndex, exampleCall, "Insert example call of function");
                    }
                }
            }
        }

        static int CountLeadingSpaces(string str)
        {
            if (string.IsNullOrEmpty(str))
                return 0;

            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] != ' ')
                    return i;
            }
            return str.Length; // All characters are spaces
        }
    }
}