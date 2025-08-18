using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using System.Collections.Generic;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Styler that highlights dead code (code after a return statement) in PeopleCode blocks
    /// </summary>
    public class DeadCodeStyler : BaseStyler
    {
        // The color to use for highlighting dead code (BGRA format: 0x73737380 - semi-transparent gray)
        private const uint DEAD_CODE_COLOR = 0x73737380;
        
        // Class to track block information
        private class BlockInfo
        {
            public int StartPosition { get; set; }
            public int EndPosition { get; set; }
            public int? ReturnStatementEndPosition { get; set; }
            public bool HasReturn => ReturnStatementEndPosition.HasValue;
        }
        
        // Stack to track nested blocks
        private readonly Stack<BlockInfo> _blockStack = new();
        
        private BlockInfo skipBlock = new BlockInfo
        {
            StartPosition = 0,
            EndPosition = 0
        };


        public DeadCodeStyler()
        {
            Description = "Highlights unreachable code that appears after a return statement.";
            Active = true;
        }
        
        public override void Reset()
        {
            base.Reset();
            _blockStack.Clear();
        }
        
        // Helper method to mark a region of code as dead using byte indexes
        private void MarkDeadCode(int byteStart, int byteEnd)
        {
            if (byteEnd > byteStart && Indicators != null)
            {
                Indicators.Add(new Indicator
                {
                    Start = byteStart,
                    Length = byteEnd - byteStart + 1,
                    Color = DEAD_CODE_COLOR,
                    Type = IndicatorType.TEXTCOLOR,
                    Tooltip = "Unreachable code (after return/exit/throw statement)",
                    QuickFixes = new List<(Type, string)>()
                });
            }
        }
        
        #region Block Entry/Exit Methods
        
        // Function blocks
        public override void EnterFunctionDefinition([NotNull] FunctionDefinitionContext context)
        {
            var statements = context.statements();
            if (statements == null) {
                _blockStack.Push(skipBlock);
                return;
            } 

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
            
        }
        
        public override void ExitFunctionDefinition([NotNull] FunctionDefinitionContext context)
        {
            ProcessBlockExit();
        }
        
        // Method blocks
        public override void EnterMethod([NotNull] MethodContext context)
        {
            var statements = context.statements();
            if (statements == null) {
                _blockStack.Push(skipBlock);
                return;
            } 

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
        }
        
        public override void ExitMethod([NotNull] MethodContext context)
        {
            ProcessBlockExit();
        }
        
        // Getter blocks
        public override void EnterGetter([NotNull] GetterContext context)
        {
            var statements = context.statements();
            if (statements == null) {
                _blockStack.Push(skipBlock);
                return;
            } 

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
        }
        
        public override void ExitGetter([NotNull] GetterContext context)
        {
            ProcessBlockExit();
        }
        
        // Setter blocks
        public override void EnterSetter([NotNull] SetterContext context)
        {
            var statements = context.statements();
            if (statements == null) {
                _blockStack.Push(skipBlock);
                return;
            }

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
        }
        
        public override void ExitSetter([NotNull] SetterContext context)
        {
            ProcessBlockExit();
        }
        
        // If statement blocks
        public override void EnterIfStatement([NotNull] IfStatementContext context)
        {
            var statements = context.statementBlock();
            if (statements == null) {
                _blockStack.Push(skipBlock);
                return;
            }

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
        }
        
        public override void ExitIfStatement([NotNull] IfStatementContext context)
        {
            ProcessBlockExit();
        }

        public override void EnterElseStatement([NotNull] ElseStatementContext context)
        {
            var statements = context.statementBlock();
            if (statements == null)
            {
                _blockStack.Push(skipBlock);
                return;
            }

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
        }

        public override void ExitElseStatement([NotNull] ElseStatementContext context)
        {
            ProcessBlockExit();
        }

        // For statement blocks
        public override void EnterForStatement([NotNull] ForStatementContext context)
        {
            var statements = context.statementBlock();
            if (statements == null)
            {
                _blockStack.Push(skipBlock);
                return;
            }

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
        }
        
        public override void ExitForStatement([NotNull] ForStatementContext context)
        {
            ProcessBlockExit();
        }
        
        // While statement blocks
        public override void EnterWhileStatement([NotNull] WhileStatementContext context)
        {
            var statements = context.statementBlock();
            if (statements == null)
            {
                _blockStack.Push(skipBlock);
                return;
            }

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
        }
        
        public override void ExitWhileStatement([NotNull] WhileStatementContext context)
        {
            ProcessBlockExit();
        }
        
        // Repeat statement blocks
        public override void EnterRepeatStatement([NotNull] RepeatStatementContext context)
        {
            var statements = context.statementBlock();
            if (statements == null)
            {
                _blockStack.Push(skipBlock);
                return;
            }

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
        }
        
        public override void ExitRepeatStatement([NotNull] RepeatStatementContext context)
        {
            ProcessBlockExit();
        }
        
        // Evaluate statement blocks
        public override void EnterWhenClause([NotNull] WhenClauseContext context)
        {
            var statements = context.statementBlock();
            if (statements == null)
            {
                _blockStack.Push(skipBlock);
                return;
            }

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
        }

        public override void ExitWhenClause([NotNull] WhenClauseContext context)
        {
            ProcessBlockExit();
        }

        public override void EnterWhenOther([NotNull] WhenOtherContext context)
        {
            var statements = context.statementBlock();
            if (statements == null)
            {
                _blockStack.Push(skipBlock);
                return;
            }

            _blockStack.Push(new BlockInfo
            {
                StartPosition = statements.Start.ByteStartIndex(),
                EndPosition = statements.Stop.ByteStopIndex()
            });
        }

        public override void ExitWhenOther([NotNull] WhenOtherContext context)
        {
            ProcessBlockExit();
        }

        #endregion

        // Track return statements
        public override void ExitReturnStmt([NotNull] ReturnStmtContext context)
        {
            if (_blockStack.Count > 0)
            {
                var currentBlock = _blockStack.Peek();
                // If we haven't seen a return in this block yet, record the position of this one
                if (!currentBlock.HasReturn)
                {
                    currentBlock.ReturnStatementEndPosition = context.Stop.ByteStopIndex();
                }
                else
                {
                    if (context.Stop.ByteStopIndex() < currentBlock.ReturnStatementEndPosition)
                    {
                        // If the return statement is before the throw/exit statements, we need to adjust the return statement position
                        currentBlock.ReturnStatementEndPosition = context.Stop.ByteStopIndex();
                    }
                }
            }
        }

        public override void ExitExitStmt([NotNull] ExitStmtContext context)
        {
            if (_blockStack.Count > 0)
            {
                var currentBlock = _blockStack.Peek();
                // If we haven't seen a return in this block yet, record the position of this one
                if (!currentBlock.HasReturn)
                {
                    currentBlock.ReturnStatementEndPosition = context.Stop.ByteStopIndex();
                }
                else
                {
                    if (context.Stop.ByteStopIndex() < currentBlock.ReturnStatementEndPosition)
                    {
                        // If the exit statement is before the return/throw statements, we need to adjust the return statement position
                        currentBlock.ReturnStatementEndPosition = context.Stop.ByteStopIndex();

                    }
                }
            }

        }

        public override void ExitThrowStmt([NotNull] ThrowStmtContext context)
        {
            if (_blockStack.Count > 0)
            {
                var currentBlock = _blockStack.Peek();
                // If we haven't seen a return in this block yet, record the position of this one
                if (!currentBlock.HasReturn)
                {
                    currentBlock.ReturnStatementEndPosition = context.Stop.ByteStopIndex();
                } else
                {
                    if (context.Stop.ByteStopIndex() < currentBlock.ReturnStatementEndPosition)
                    {
                        // If the throw statement is before the exit/return statement, we need to adjust the return statement position
                        currentBlock.ReturnStatementEndPosition = context.Stop.ByteStopIndex();
                    }
                }
            }
        }

        // Process block exit - check for dead code and mark it
        private void ProcessBlockExit()
        {
            if (_blockStack.Count > 0)
            {
                var block = _blockStack.Pop();
                
                // If there was a return statement, highlight any code after it using byte positions
                if (block != null && block.HasReturn && block.ReturnStatementEndPosition.HasValue)
                {
                    int deadCodeByteStart = block.ReturnStatementEndPosition.Value + 1;
                    int deadCodeByteEnd = block.EndPosition - 1; // Exclude the end token
                    
                    if (deadCodeByteEnd > deadCodeByteStart)
                    {
                        MarkDeadCode(deadCodeByteStart, deadCodeByteEnd);
                    }
                }
            }
        }
    }
} 