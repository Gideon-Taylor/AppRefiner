using Antlr4.Runtime.Tree;
using AppRefiner.Linters;
using AppRefiner.PeopleCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class UnusedLocalVariableListener : BaseStyler
    {
        class VariableInfo
        {
            public string Name { get; }
            public bool Used { get; set; }
            public uint Line { get; }
            public uint StartIndex { get; }
            public uint StopIndex { get; }

            public VariableInfo(string name, uint line, uint start, uint stop)
            {
                Name = name;
                Used = false;
                Line = line;
                StartIndex = start;
                StopIndex = stop;
            }
        }


        // A stack of scopes. Each scope maps a variable name to its information.
        private readonly Stack<Dictionary<string, VariableInfo>> scopeStack = new Stack<Dictionary<string, VariableInfo>>();

        public UnusedLocalVariableListener()
        {
            // Start with a global scope.
            scopeStack.Push(new Dictionary<string, VariableInfo>());
            Description = "Grays out unused local variables.";
            Active = true;
        }

        // When entering a new method (or any block that introduces a new local scope), push a new dictionary.
        public override void EnterMethod(MethodContext context)
        {
            scopeStack.Push(new Dictionary<string, VariableInfo>());
        }

        public override void EnterFunctionDefinition(FunctionDefinitionContext context)
        {
            scopeStack.Push(new Dictionary<string, VariableInfo>());
        }

        // When leaving a function, check for unused variables in the function’s scope.
        public override void ExitFunctionDefinition(FunctionDefinitionContext context)
        {
            var scope = scopeStack.Pop();
            foreach (var variable in scope.Values)
            {
                if (!variable.Used)
                {
                    Colors?.Add(new CodeColor()
                    {
                        Color = FontColor.Gray,
                        Start = variable.StartIndex,
                        Length = variable.StopIndex - variable.StartIndex + 1
                    });

                }
            }
        }

        public override void EnterGetter(GetterContext context)
        {
            scopeStack.Push(new Dictionary<string, VariableInfo>());
        }

        public override void ExitGetter(GetterContext context)
        {
            var scope = scopeStack.Pop();
            foreach (var variable in scope.Values)
            {
                if (!variable.Used)
                {
                    Highlights?.Add(new CodeHighlight()
                    {
                        Color = HighlightColor.Gray,
                        Start = variable.StartIndex,
                        Length = variable.StopIndex - variable.StartIndex + 1
                    });
                }
            }
        }

        // Add scope handling for setters
        public override void EnterSetter(SetterContext context)
        {
            scopeStack.Push(new Dictionary<string, VariableInfo>());
        }

        public override void ExitSetter(SetterContext context)
        {
            var scope = scopeStack.Pop();
            foreach (var variable in scope.Values)
            {
                if (!variable.Used)
                {
                    Highlights?.Add(new CodeHighlight()
                    {
                        Color = HighlightColor.Gray,
                        Start = variable.StartIndex,
                        Length = variable.StopIndex - variable.StartIndex + 1
                    });
                }
            }
        }

        // When leaving a method, check for unused variables in the method’s scope.
        public override void ExitMethod(MethodContext context)
        {
            var scope = scopeStack.Pop();
            foreach (var variable in scope.Values)
            {
                if (!variable.Used)
                {
                    Highlights?.Add(new CodeHighlight()
                    {
                        Color = HighlightColor.Gray,
                        Start = variable.StartIndex,
                        Length = variable.StopIndex - variable.StartIndex + 1
                    });

                }
            }
        }

        // For local variable definitions like: Local string &foo, &bar;
        public override void EnterLocalVariableDefinition(LocalVariableDefinitionContext context)
        {
            var currentScope = scopeStack.Peek();
            foreach (ITerminalNode varToken in context.USER_VARIABLE())
            {
                string varName = varToken.GetText();
                // Save the location using token symbol properties.
                int line = varToken.Symbol.Line;
                if (!currentScope.ContainsKey(varName))
                {
                    currentScope[varName] = new VariableInfo(varName, (uint)line, (uint)varToken.Symbol.StartIndex, (uint)varToken.Symbol.StopIndex);
                }
            }
        }

        // For local variable declarations with assignment like: Local string &x = "Hello";
        public override void EnterLocalVariableDeclAssignment(LocalVariableDeclAssignmentContext context)
        {
            /* get absolute position from the start of the file */

            var currentScope = scopeStack.Peek();
            ITerminalNode varToken = context.USER_VARIABLE();
            string varName = varToken.GetText();
            int line = varToken.Symbol.Line;
            int column = varToken.Symbol.Column;
            int pos = varToken.Symbol.StartIndex;
            if (!currentScope.ContainsKey(varName))
            {
                currentScope[varName] = new VariableInfo(varName, (uint)line, (uint)varToken.Symbol.StartIndex, (uint)varToken.Symbol.StopIndex);
            }
        }

        // When an identifier is encountered, mark it as used if it is a local variable.
        public override void EnterIdentUserVariable(IdentUserVariableContext context)
        {
            string identText = context.GetText();
            // Assume local variables start with '&'
            // Search from innermost to outer scopes.
            foreach (var scope in scopeStack)
            {
                if (scope.TryGetValue(identText, out VariableInfo varInfo))
                {
                    varInfo.Used = true;
                    break;
                }
            }
        }

        // Optionally, check the global scope when exiting the program.
        public override void ExitProgram(ProgramContext context)
        {
            if (scopeStack.Count > 0)
            {
                var globalScope = scopeStack.Peek();
                foreach (var variable in globalScope.Values)
                {
                    if (!variable.Used)
                    {
                        Highlights?.Add(new CodeHighlight()
                        {
                            Color = HighlightColor.Gray,
                            Start = variable.StartIndex,
                            Length = variable.StopIndex - variable.StartIndex + 1
                        });
                    }
                }
            }
        }

        public override void Reset()
        {
            while (scopeStack.Count > 1)
            {
                var dict = scopeStack.Pop();
                dict.Clear();
            }
        }
    }
}
