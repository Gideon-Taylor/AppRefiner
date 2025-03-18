using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Parses and tracks linter suppression directives in PeopleCode comments.
    /// 
    /// Suppression Comment Formats:
    /// 1. Global Suppression (above imports/class declaration):
    ///    /* #AppRefiner suppress (LINTER_ID:ReportNumber, ANOTHER_ID:Number) */
    ///    // Applies to entire program file
    /// 
    /// 2. Scope Suppression (above method/function/block):
    ///    /* #AppRefiner suppress (LINTER_ID:ReportNumber) */
    ///    Method MyMethod()
    ///    {
    ///        // All lines in this method will suppress the specified linter report
    ///    }
    /// 
    /// 3. Line-specific Suppression (immediately above a line):
    ///    /* #AppRefiner suppress (LINTER_ID:ReportNumber) */
    ///    var x = SomeMethod(); // This specific line is suppressed
    /// 
    /// Usage Examples:
    /// 
    /// /* #AppRefiner suppress (CODE_STYLE:1, NAMING:2) */
    /// import PTCS_PORTAL:*;
    /// 
    /// class MyClass 
    /// {
    ///     /* #AppRefiner suppress (COMPLEXITY:3) */
    ///     method ComplexMethod()
    ///         /* AppRefiner suppress (PERFORMANCE:4) */
    ///         Local number &result = ExpensiveOperation();
    ///     end-method;
    /// }
    /// </summary>
    public class LinterSuppressionListener : PeopleCodeParserBaseListener
    {
        private readonly struct SuppressionInfo
        {
            public string LinterId { get; }
            public int ReportNumber { get; }

            public SuppressionInfo(string linterId, int reportNumber)
            {
                LinterId = linterId;
                ReportNumber = reportNumber;
            }

            public override bool Equals(object? obj)
            {
                return obj is SuppressionInfo info &&
                       LinterId == info.LinterId &&
                       ReportNumber == info.ReportNumber;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(LinterId, ReportNumber);
            }
        }

        private readonly ITokenStream _tokenStream;
        private readonly IList<IToken> _comments;
        private readonly HashSet<SuppressionInfo> _globalSuppressions = new();
        private readonly Dictionary<int, HashSet<SuppressionInfo>> _lineSpecificSuppressions = new();
        private readonly Stack<HashSet<SuppressionInfo>> _scopeSuppressionStack = new();
    
        private static readonly Regex _suppressionRegex = new(
            @"#AppRefiner\s+suppress\s+\(([\w\:_\s,]+)\)",
            RegexOptions.Compiled);

        public LinterSuppressionListener(ITokenStream tokenStream, List<IToken> comments)
        {
            _tokenStream = tokenStream ?? throw new ArgumentNullException(nameof(tokenStream));
        
            // Extract all comments from the token stream
            _comments = comments ?? throw new ArgumentNullException(nameof(comments));
        
            // Clear any existing suppressions
            Reset();
        
            // Process global suppressions (above imports block)
            ProcessGlobalSuppressions();
        
            // Process line-specific suppressions for all comments
            ProcessAllLineSpecificSuppressions();
        }
    
        /// <summary>
        /// Resets the listener state, clearing all suppression information
        /// </summary>
        public void Reset()
        {
            _globalSuppressions.Clear();
            _lineSpecificSuppressions.Clear();
        
            // Clear the scope stack
            _scopeSuppressionStack.Clear();
        }
        
        /// <summary>
        /// Processes all comments to find line-specific suppressions
        /// Each suppression comment applies to the next line of code
        /// </summary>
        private void ProcessAllLineSpecificSuppressions()
        {
            // Sort comments by line
            var sortedComments = _comments.OrderBy(c => c.Line).ToList();
            
            // Process each comment
            for (int i = 0; i < sortedComments.Count; i++)
            {
                var comment = sortedComments[i];
                
                // Find the next line with actual code after this comment
                int nextCodeLine = FindNextCodeLine(comment.Line);
                
                if (nextCodeLine > 0)
                {
                    if (!_lineSpecificSuppressions.TryGetValue(nextCodeLine, out var suppressions))
                    {
                        suppressions = new HashSet<SuppressionInfo>();
                        _lineSpecificSuppressions[nextCodeLine] = suppressions;
                    }
                    
                    ProcessSuppressionComment(comment, suppressions);
                }
            }
        }
        
        /// <summary>
        /// Finds the next line containing code after the given line
        /// </summary>
        private int FindNextCodeLine(int commentLine)
        {
            int nextLine = int.MaxValue;
            
            for (int i = 0; i < _tokenStream.Size; i++)
            {
                var token = _tokenStream.Get(i);
                
                // Skip tokens on or before the comment line or on comment channel
                if (token.Line <= commentLine || token.Channel == PeopleCodeLexer.COMMENTS)
                    continue;
                
                // Skip insignificant tokens like semicolons
                if (token.Type == PeopleCodeParser.SEMI)
                    continue;
                
                // Found the next code token
                if (token.Line < nextLine)
                {
                    nextLine = token.Line;
                    break;
                }
            }
            
            return nextLine != int.MaxValue ? nextLine : -1;
        }

        private void ProcessGlobalSuppressions()
        {
            // Find the imports block or class declaration to determine global comment range
            int importsLineStart = int.MaxValue;
            for (int i = 0; i < _tokenStream.Size; i++)
            {
                var token = _tokenStream.Get(i);
                if (token.Type == PeopleCodeParser.IMPORT || 
                    token.Type == PeopleCodeParser.CLASS || 
                    token.Type == PeopleCodeParser.INTERFACE)
                {
                    importsLineStart = token.Line;
                    break;
                }
            }

            // Process suppressions above the imports block
            foreach (var comment in _comments)
            {
                if (comment.Line < importsLineStart)
                {
                    ProcessSuppressionComment(comment, _globalSuppressions);
                }
            }
        }

        private void ProcessSuppressionComment(IToken comment, HashSet<SuppressionInfo> targetCollection)
        {
            var match = _suppressionRegex.Match(comment.Text);
            if (match.Success)
            {
                string suppressionList = match.Groups[1].Value;
                foreach (var item in suppressionList.Split(','))
                {
                    var parts = item.Trim().Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int reportNumber))
                    {
                        targetCollection.Add(new SuppressionInfo(parts[0], reportNumber));
                    }
                }
            }
        }

        #region Method/Function Scope Handling
        
        public override void EnterMethod([NotNull] PeopleCodeParser.MethodContext context)
        {
            ProcessScopeEntrySuppressions(context.Start.Line);
        }

        public override void ExitMethod([NotNull] PeopleCodeParser.MethodContext context)
        {
            PopScopeSuppressions();
        }

        public override void EnterFunctionDefinition([NotNull] PeopleCodeParser.FunctionDefinitionContext context)
        {
            ProcessScopeEntrySuppressions(context.Start.Line);
        }

        public override void ExitFunctionDefinition([NotNull] PeopleCodeParser.FunctionDefinitionContext context)
        {
            PopScopeSuppressions();
        }

        public override void EnterGetter([NotNull] PeopleCodeParser.GetterContext context)
        {
            ProcessScopeEntrySuppressions(context.Start.Line);
        }

        public override void ExitGetter([NotNull] PeopleCodeParser.GetterContext context)
        {
            PopScopeSuppressions();
        }

        public override void EnterSetter([NotNull] PeopleCodeParser.SetterContext context)
        {
            ProcessScopeEntrySuppressions(context.Start.Line);
        }

        public override void ExitSetter([NotNull] PeopleCodeParser.SetterContext context)
        {
            PopScopeSuppressions();
        }
        
        #endregion

        #region Statement Block Scope Handling
        
        public override void EnterIfStatement([NotNull] PeopleCodeParser.IfStatementContext context)
        {
            ProcessScopeEntrySuppressions(context.Start.Line);
        }

        public override void ExitIfStatement([NotNull] PeopleCodeParser.IfStatementContext context)
        {
            PopScopeSuppressions();
        }

        public override void EnterForStatement([NotNull] PeopleCodeParser.ForStatementContext context)
        {
            ProcessScopeEntrySuppressions(context.Start.Line);
        }

        public override void ExitForStatement([NotNull] PeopleCodeParser.ForStatementContext context)
        {
            PopScopeSuppressions();
        }

        public override void EnterWhileStatement([NotNull] PeopleCodeParser.WhileStatementContext context)
        {
            ProcessScopeEntrySuppressions(context.Start.Line);
        }

        public override void ExitWhileStatement([NotNull] PeopleCodeParser.WhileStatementContext context)
        {
            PopScopeSuppressions();
        }

        public override void EnterRepeatStatement([NotNull] PeopleCodeParser.RepeatStatementContext context)
        {
            ProcessScopeEntrySuppressions(context.Start.Line);
        }

        public override void ExitRepeatStatement([NotNull] PeopleCodeParser.RepeatStatementContext context)
        {
            PopScopeSuppressions();
        }

        public override void EnterEvaluateStatement([NotNull] PeopleCodeParser.EvaluateStatementContext context)
        {
            ProcessScopeEntrySuppressions(context.Start.Line);
        }

        public override void ExitEvaluateStatement([NotNull] PeopleCodeParser.EvaluateStatementContext context)
        {
            PopScopeSuppressions();
        }

        public override void EnterTryCatchBlock([NotNull] PeopleCodeParser.TryCatchBlockContext context)
        {
            ProcessScopeEntrySuppressions(context.Start.Line);
        }

        public override void ExitTryCatchBlock([NotNull] PeopleCodeParser.TryCatchBlockContext context)
        {
            PopScopeSuppressions();
        }
        
        #endregion

        private void ProcessScopeEntrySuppressions(int scopeStartLine)
        {
            var scopeSuppressions = new HashSet<SuppressionInfo>();
        
            // Find comments immediately above this scope
            var scopeComments = _comments
                .Where(c => c.Line < scopeStartLine)
                .OrderByDescending(c => c.Line)
                .ToList();

            // Process only comments that are directly above (no code in between)
            bool foundGap = false;
            foreach (var comment in scopeComments)
            {
                // Check if there's code between comments
                for (int i = 0; i < _tokenStream.Size; i++)
                {
                    var token = _tokenStream.Get(i);
                    if (token.Line > comment.Line && token.Line < scopeStartLine && 
                        token.Channel == Lexer.DefaultTokenChannel && 
                        token.Type != PeopleCodeParser.SEMI)
                    {
                        foundGap = true;
                        break;
                    }
                }

                if (!foundGap)
                {
                    ProcessSuppressionComment(comment, scopeSuppressions);
                }
                else
                {
                    break;
                }
            }

            // Push the suppression set onto the stack
            _scopeSuppressionStack.Push(scopeSuppressions);
        }

        private void PopScopeSuppressions()
        {
            if (_scopeSuppressionStack.Count > 0)
            {
                _scopeSuppressionStack.Pop();
            }
        }

        /// <summary>
        /// Checks if a specific lint report should be suppressed
        /// </summary>
        /// <param name="linterId">The ID of the linter</param>
        /// <param name="reportNumber">The report number</param>
        /// <param name="line">The line number the report is for</param>
        /// <returns>True if the report should be suppressed, false otherwise</returns>
        public bool IsSuppressed(string linterId, int reportNumber, int line)
        {
            var suppressionInfo = new SuppressionInfo(linterId, reportNumber);
        
            // 1. Check global suppressions first
            if (_globalSuppressions.Contains(suppressionInfo))
            {
                return true;
            }
        
            // 2. Check active scope suppressions from the stack
            foreach (var scopeSuppressions in _scopeSuppressionStack)
            {
                if (scopeSuppressions.Contains(suppressionInfo))
                {
                    return true;
                }
            }
        
            // 3. Check line-specific suppressions
            if (_lineSpecificSuppressions.TryGetValue(line, out var lineSuppressions) && 
                lineSuppressions.Contains(suppressionInfo))
            {
                return true;
            }
        
            return false;
        }

        /// <summary>
        /// Gets all suppressions that apply to a specific line
        /// </summary>
        /// <param name="line">The line number</param>
        /// <returns>A list of suppressed linter IDs and report numbers</returns>
        public List<(string LinterId, int ReportNumber)> GetSuppressionsForLine(int line)
        {
            var result = new List<(string, int)>();
        
            // Add global suppressions
            foreach (var suppression in _globalSuppressions)
            {
                result.Add((suppression.LinterId, suppression.ReportNumber));
            }
        
            // Add active scope suppressions from the stack
            foreach (var scopeSuppressions in _scopeSuppressionStack)
            {
                foreach (var suppression in scopeSuppressions)
                {
                    result.Add((suppression.LinterId, suppression.ReportNumber));
                }
            }
        
            // Add line-specific suppressions
            if (_lineSpecificSuppressions.TryGetValue(line, out var lineSuppressions))
            {
                foreach (var suppression in lineSuppressions)
                {
                    result.Add((suppression.LinterId, suppression.ReportNumber));
                }
            }
        
            return result;
        }
    }
}
