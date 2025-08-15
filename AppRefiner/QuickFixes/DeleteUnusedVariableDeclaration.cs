using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using AppRefiner.Linters.Models;
using AppRefiner.PeopleCode;
using AppRefiner.Refactors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using static SqlParser.Ast.Owner;

namespace AppRefiner.QuickFixes
{
    /// <summary>
    /// Refactoring operation that deletes unused local variables, private instance variables,
    /// or function/method parameters.
    /// </summary>
    public class DeleteUnusedVariableDeclaration : ScopedRefactor<List<(int StartIndex, int EndIndex)>>
    {
        /// <summary>
        /// Gets the display name of this refactoring operation.
        /// </summary>
        public new static string RefactorName => "QuickFix: Delete Unused Variable Declaration";

        /// <summary>
        /// Gets the description of this refactoring operation.
        /// </summary>
        public new static string RefactorDescription => "Deletes an unused local variable, private instance variable, or parameter declaration.";

        /// <summary>
        /// This refactor should not have a keyboard shortcut registered.
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from refactoring lists and discovery.
        /// </summary>
        public new static bool IsHidden => true;
        // --------------------------------------------

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteUnusedVariableDeclaration"/> class.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance to use for this refactor.</param>
        public DeleteUnusedVariableDeclaration(ScintillaEditor editor)
            : base(editor)
        {
            // Constructor logic (if any)
        }

        // Track local variable declarations
        public override void EnterLocalVariableDefinition(LocalVariableDefinitionContext context)
        {
            base.EnterLocalVariableDefinition(context);

            var variables = context.USER_VARIABLE();
            foreach (var variable in variables)
            {
                if (variable.Symbol.ByteStartIndex() <= CurrentPosition && CurrentPosition <= variable.Symbol.ByteStopIndex() + 1)
                {
                    /* found the variable node we want to delete... */
                    var argIndex = Array.IndexOf(variables, variable);
                    var totalArgs = variables.Length;
                    if (variables.Length == 1)
                    {
                        /* Delete whole line */
                        var lineStartIndex = ScintillaManager.GetLineStartIndex(Editor, variable.Symbol.Line - 1);
                        var lineEndIndex = lineStartIndex + ScintillaManager.GetLineLength(Editor, variable.Symbol.Line - 1);
                        DeleteText(lineStartIndex, lineEndIndex - 1, "Remove instance variable declaration");
                        return;
                    }
                    if (argIndex == totalArgs - 1)
                    {
                        var commaToken = context.COMMA().Where(c => c.Symbol.ByteStopIndex() < CurrentPosition).Last();
                        DeleteText(commaToken.Symbol.ByteStartIndex(), variable.Symbol.ByteStopIndex(), "Remove method argument");
                    }
                    else
                    {
                        var commaToken = context.COMMA().Where(c => c.Symbol.ByteStartIndex() > CurrentPosition).First();
                        DeleteText(variable.Symbol.ByteStartIndex(), commaToken.Symbol.ByteStopIndex(), "Remove method argument");
                    }
                    break;
                }
            }
        }

        // Track private instance variable declarations
        public override void EnterPrivateProperty(PrivatePropertyContext context)
        {
            base.EnterPrivateProperty(context);

            var instanceDeclContext = context.instanceDeclaration();
            if (instanceDeclContext is InstanceDeclContext instanceDecl)
            {
                var variables = instanceDecl.USER_VARIABLE();
                foreach(var variable in variables)
                {
                    if (variable.Symbol.ByteStartIndex() <= CurrentPosition && CurrentPosition <= variable.Symbol.ByteStopIndex() + 1)
                    {
                        /* found the variable node we want to delete... */
                        var argIndex = Array.IndexOf(variables, variable);
                        var totalArgs = variables.Length;
                        if (variables.Length == 1)
                        {
                            /* Delete whole line */
                            var lineStartIndex = ScintillaManager.GetLineStartIndex(Editor, variable.Symbol.Line - 1);
                            var lineEndIndex = lineStartIndex + ScintillaManager.GetLineLength(Editor, variable.Symbol.Line - 1);
                            DeleteText(lineStartIndex, lineEndIndex - 1, "Remove instance variable declaration");
                            return;
                        }
                        if (argIndex == totalArgs - 1)
                        {
                            var commaToken = instanceDecl.COMMA().Where(c => c.Symbol.ByteStopIndex() < CurrentPosition).Last();
                            DeleteText(commaToken.Symbol.ByteStartIndex(), variable.Symbol.ByteStopIndex(), "Remove method argument");
                        }
                        else
                        {
                            var commaToken = instanceDecl.COMMA().Where(c => c.Symbol.ByteStartIndex() > CurrentPosition).First();
                            DeleteText(variable.Symbol.ByteStartIndex(), commaToken.Symbol.ByteStopIndex(), "Remove method argument");
                        }
                        break;
                    }
                }
            }
        }

        public override void EnterMethodArguments([NotNull] MethodArgumentsContext context)
        {
            base.EnterMethodArguments(context);

            var arguments = context.methodArgument();

            foreach(var arg in arguments)
            {
                var variableNode = arg.USER_VARIABLE();
                if (variableNode.Symbol.ByteStartIndex() <= CurrentPosition && variableNode.Symbol.ByteStopIndex() >= CurrentPosition)
                {
                    /* found the variable node we want to delete... */
                    var argIndex = Array.IndexOf(arguments, arg);
                    var totalArgs = arguments.Length;
                    if (totalArgs == 1)
                    {
                        /* Delete whole line */
                        DeleteText(arg.Start.ByteStartIndex(), arg.Stop.ByteStopIndex(), "Remove method argument");
                        return;
                    }
                    if (argIndex == totalArgs - 1)
                    {
                        var commaToken = context.COMMA().Where(c => c.Symbol.ByteStopIndex() < CurrentPosition).Last();
                        DeleteText(commaToken.Symbol.ByteStartIndex(), arg.Stop.ByteStopIndex(), "Remove method argument");
                    } else
                    {
                        var commaToken = context.COMMA().Where(c => c.Symbol.ByteStartIndex() > CurrentPosition).First();
                        DeleteText(variableNode.Symbol.ByteStartIndex(), commaToken.Symbol.ByteStopIndex(), "Remove method argument");
                    }
                }
            }

        }


        // Track function parameter declarations
        public override void EnterFunctionArguments(FunctionArgumentsContext context)
        {
            base.EnterFunctionArguments(context);

            var arguments = context.functionArgument();

            foreach (var arg in arguments)
            {
                var variableNode = arg.USER_VARIABLE();
                if (variableNode.Symbol.ByteStartIndex() <= CurrentPosition && variableNode.Symbol.ByteStopIndex() >= CurrentPosition)
                {
                    /* found the variable node we want to delete... */
                    var argIndex = Array.IndexOf(arguments, arg);
                    var totalArgs = arguments.Length;

                    if (argIndex == totalArgs - 1)
                    {
                        var commaToken = context.COMMA().Where(c => c.Symbol.ByteStopIndex() < CurrentPosition).Last();
                        DeleteText(commaToken.Symbol.ByteStartIndex(), arg.Stop.ByteStopIndex(), "Remove function argument");
                    }
                    else
                    {
                        var commaToken = context.COMMA().Where(c => c.Symbol.ByteStartIndex() > CurrentPosition).First();
                        DeleteText(variableNode.Symbol.ByteStartIndex(), commaToken.Symbol.ByteStopIndex(), "Remove function argument");
                    }
                }
            }
        }
    }
}