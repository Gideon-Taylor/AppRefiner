using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;

namespace AppRefiner.Refactors
{
    public enum SuppressReportMode
    {
        LINE, NEAREST_BLOCK, METHOD_OR_FUNC, GLOBAL
    }

    public class SuppressReportRefactor : BaseRefactor
    {

        private ScintillaEditor editor;
        private int line;
        private SuppressReportMode type;
        private bool changeGenerated = false;
        private enum ScopeType
        {
            BLOCK, METHOD, FUNCTION, GLOBAL
        }
        private readonly Stack<(ParserRuleContext Context, ScopeType Type)> scopeStack = new();

        public SuppressReportRefactor(ScintillaEditor editor, int line, SuppressReportMode type)
        {
            this.editor = editor;
            this.line = line;
            this.type = type;
        }

        public override void EnterMethod([NotNull] PeopleCodeParser.MethodContext context)
        {
            ProcessScopeEntry(context, ScopeType.METHOD);
        }

        public override void ExitMethod([NotNull] PeopleCodeParser.MethodContext context)
        {
            ProcessScopeExit(context);
        }

        public override void EnterFunctionDefinition([NotNull] PeopleCodeParser.FunctionDefinitionContext context)
        {
            ProcessScopeEntry(context, ScopeType.FUNCTION);
        }

        public override void ExitFunctionDefinition([NotNull] PeopleCodeParser.FunctionDefinitionContext context)
        {
            ProcessScopeExit(context);
        }

        public override void EnterGetter([NotNull] PeopleCodeParser.GetterContext context)
        {
            ProcessScopeEntry(context, ScopeType.FUNCTION);
        }

        public override void ExitGetter([NotNull] PeopleCodeParser.GetterContext context)
        {
            ProcessScopeExit(context);
        }

        public override void EnterSetter([NotNull] PeopleCodeParser.SetterContext context)
        {
            ProcessScopeEntry(context, ScopeType.FUNCTION);
        }

        public override void ExitSetter([NotNull] PeopleCodeParser.SetterContext context)
        {
            ProcessScopeExit(context);
        }

        public override void EnterImportsBlock([NotNull] PeopleCodeParser.ImportsBlockContext context)
        {
            ProcessScopeEntry(context, ScopeType.GLOBAL);
        }

        public override void EnterIfStatement([NotNull] PeopleCodeParser.IfStatementContext context)
        {
            ProcessScopeEntry(context, ScopeType.BLOCK);
        }

        public override void ExitIfStatement([NotNull] PeopleCodeParser.IfStatementContext context)
        {
            ProcessScopeExit(context);
        }

        public override void EnterForStatement([NotNull] PeopleCodeParser.ForStatementContext context)
        {
            ProcessScopeEntry(context, ScopeType.BLOCK);
        }

        public override void ExitForStatement([NotNull] PeopleCodeParser.ForStatementContext context)
        {
            ProcessScopeExit(context);
        }

        public override void EnterWhileStatement([NotNull] PeopleCodeParser.WhileStatementContext context)
        {
            ProcessScopeEntry(context, ScopeType.BLOCK);
        }

        public override void ExitWhileStatement([NotNull] PeopleCodeParser.WhileStatementContext context)
        {
            ProcessScopeExit(context);
        }

        public override void EnterRepeatStatement([NotNull] PeopleCodeParser.RepeatStatementContext context)
        {
            ProcessScopeEntry(context, ScopeType.BLOCK);
        }

        public override void ExitRepeatStatement([NotNull] PeopleCodeParser.RepeatStatementContext context)
        {
            ProcessScopeExit(context);
        }

        public override void EnterEvaluateStatement([NotNull] PeopleCodeParser.EvaluateStatementContext context)
        {
            ProcessScopeEntry(context, ScopeType.BLOCK);
        }

        public override void ExitEvaluateStatement([NotNull] PeopleCodeParser.EvaluateStatementContext context)
        {
            ProcessScopeExit(context);
        }

        public override void EnterTryCatchBlock([NotNull] PeopleCodeParser.TryCatchBlockContext context)
        {
            ProcessScopeEntry(context, ScopeType.BLOCK);
        }

        public override void ExitTryCatchBlock([NotNull] PeopleCodeParser.TryCatchBlockContext context)
        {
            ProcessScopeExit(context);
        }

        public override void ExitProgram([NotNull] PeopleCodeParser.ProgramContext context)
        {
            base.ExitProgram(context);

            GenerateChange();
        }

        private void GenerateChange()
        {
            if (changeGenerated) return;


            if (editor.LineToReports.TryGetValue(line, out var reports))
            {
                var newSuppressLine = $"/* #AppRefiner suppress ({string.Join(",", reports.Select(r => r.GetFullId()))}) */\r\n";
                ParserRuleContext? contextToInsertBefore = null;

                if (type == SuppressReportMode.LINE)
                {
                    var startIndex = ScintillaManager.GetLineStartIndex(editor, line);
                    if (startIndex == -1)
                    {
                        startIndex = 0;
                    }

                    InsertText(startIndex, newSuppressLine, "Suppress report");
                    changeGenerated = true;
                    return;
                }

                if (type == SuppressReportMode.NEAREST_BLOCK)
                {
                    contextToInsertBefore = scopeStack.Pop().Context;
                }
                else if (type == SuppressReportMode.METHOD_OR_FUNC)
                {
                    /* pop until we find a scope type method or func or run out */
                    /* if we run out, SetFailure "unable to find method or function" */
                    while (scopeStack.Count > 0)
                    {
                        var scope = scopeStack.Pop();
                        if (scope.Type == ScopeType.METHOD || scope.Type == ScopeType.FUNCTION)
                        {
                            contextToInsertBefore = scope.Context;
                            break;
                        }
                    }
                    if (contextToInsertBefore == null)
                    {
                        SetFailure("Unable to find method or function scope.");
                        return;
                    }

                }
                else if (type == SuppressReportMode.GLOBAL)
                {
                    /* Pop until we find the global scope */
                    while (scopeStack.Count > 0)
                    {
                        var scope = scopeStack.Pop();
                        if (scope.Type == ScopeType.GLOBAL)
                        {
                            contextToInsertBefore = scope.Context;
                            break;
                        }
                    }
                    if (contextToInsertBefore == null)
                    {
                        SetFailure("Unable to find global scope start.");
                        return;
                    }
                }

                if (contextToInsertBefore != null)
                {
                    var insertPos = ScintillaManager.GetLineStartIndex(editor, contextToInsertBefore.Start.Line - 1 > 0 ? contextToInsertBefore.Start.Line - 1 : 1);
                    InsertText(insertPos, newSuppressLine, "Suppress report");
                    changeGenerated = true;
                }
                else
                {
                    SetFailure("Unable to find line to insert suppress report.");
                }

            }
            else
            {
                SetFailure("No report found at cursor position. Please place cursor on a line with a report.");
            }
        }

        private void ProcessScopeEntry(ParserRuleContext context, ScopeType type)
        {
            // Push the suppression set onto the stack
            scopeStack.Push((context, type));
        }

        private void ProcessScopeExit(ParserRuleContext context)
        {
            if (context.Start.Line <= line + 1 && context.Stop.Line >= line + 1)
            {
                GenerateChange();
            }
            else
            {
                if (scopeStack.Count > 0)
                    scopeStack.Pop();
            }
        }

    }
}
