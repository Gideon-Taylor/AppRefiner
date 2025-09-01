using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using System.Text.RegularExpressions;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Self-hosted parser implementation for parsing and tracking linter suppression directives in PeopleCode comments.
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
    /// 4. Wildcard Suppression (suppresses all reports from a specific linter):
    ///    /* #AppRefiner suppress (LINTER_ID:*) */
    ///    // Suppresses all reports from the specified linter
    ///
    /// Usage Examples:
    ///
    /// /* #AppRefiner suppress (CODE_STYLE:1, NAMING:2, SQL_EXEC:*) */
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
    public class LinterSuppressionProcessor : BaseLintRule
    {
        private readonly struct SuppressionInfo
        {
            public string LinterId { get; }
            public int ReportNumber { get; }
            public bool IsWildcard { get; }

            public SuppressionInfo(string linterId, int reportNumber, bool isWildcard = false)
            {
                LinterId = linterId;
                ReportNumber = reportNumber;
                IsWildcard = isWildcard;
            }

            public override bool Equals(object? obj)
            {
                return obj is SuppressionInfo info &&
                       LinterId == info.LinterId &&
                       ReportNumber == info.ReportNumber &&
                       IsWildcard == info.IsWildcard;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(LinterId, ReportNumber, IsWildcard);
            }
        }

        public override string LINTER_ID => "SUPPRESSION_PROCESSOR";

        private readonly HashSet<SuppressionInfo> _globalSuppressions = new();
        private readonly Dictionary<int, HashSet<SuppressionInfo>> _lineSpecificSuppressions = new();
        private readonly Stack<HashSet<SuppressionInfo>> _scopeSuppressionStack = new();

        private static readonly Regex _suppressionRegex = new(
            @"#AppRefiner\s+suppress\s+\(([\w\:_\s,\*]+)\)",
            RegexOptions.Compiled);

        public LinterSuppressionProcessor()
        {
            Description = "Processes linter suppression directives";
            Active = true;
            Type = ReportType.Info;
        }

        /// <summary>
        /// Processes the program and extracts all suppression directives
        /// </summary>
        public override void VisitProgram(ProgramNode node)
        {
            Reset();
            base.VisitProgram(node);

            // Process comments to find suppression directives
            ProcessSuppressionComments(node);
        }

        /// <summary>
        /// Resets the processor state, clearing all suppression information
        /// </summary>
        public override void Reset()
        {
            _globalSuppressions.Clear();
            _lineSpecificSuppressions.Clear();

            // Clear the scope stack
            _scopeSuppressionStack.Clear();
        }

        /// <summary>
        /// Processes comments from the ProgramNode to find suppression directives
        /// </summary>
        private void ProcessSuppressionComments(ProgramNode programNode)
        {
            if (programNode.Comments == null)
                return;

            // Sort comments by line number for processing
            var sortedComments = programNode.Comments
                .OrderBy(c => c.SourceSpan.Start.Line)
                .ToList();

            // First pass: Identify global suppressions (above imports/class)
            int globalEndLine = FindGlobalSuppressionBoundary(programNode);
            ProcessGlobalSuppressions(sortedComments.Where(c => c.SourceSpan.Start.Line < globalEndLine));

            // Second pass: Process all line-specific and scope suppressions
            ProcessAllLineSpecificSuppressions(sortedComments, globalEndLine);
        }

        /// <summary>
        /// Finds the line where global suppressions end (imports or class declaration)
        /// </summary>
        private int FindGlobalSuppressionBoundary(ProgramNode programNode)
        {
            // Find the earliest import or class/interface declaration
            int boundaryLine = int.MaxValue;

            // Check for class declaration
            if (programNode.AppClass != null)
            {
                boundaryLine = Math.Min(boundaryLine, programNode.AppClass.SourceSpan.Start.Line);
            }

            // Check for interface declaration
            if (programNode.Interface != null)
            {
                boundaryLine = Math.Min(boundaryLine, programNode.Interface.SourceSpan.Start.Line);
            }

            // Check for imports (look at the program statements)
            foreach (var import in programNode.Imports)
            {
               boundaryLine = Math.Min(boundaryLine, import.SourceSpan.Start.Line);
            }

            return boundaryLine != int.MaxValue ? boundaryLine : 1;
        }

        /// <summary>
        /// Processes suppressions that apply globally to the entire file
        /// </summary>
        private void ProcessGlobalSuppressions(IEnumerable<PeopleCodeParser.SelfHosted.Lexing.Token> globalComments)
        {
            foreach (var comment in globalComments)
            {
                ProcessSuppressionComment(comment, _globalSuppressions);
            }
        }

        /// <summary>
        /// Processes all line-specific and scope suppressions
        /// </summary>
        private void ProcessAllLineSpecificSuppressions(List<PeopleCodeParser.SelfHosted.Lexing.Token> sortedComments, int globalEndLine)
        {
            foreach (var comment in sortedComments)
            {
                // Skip global suppressions already processed
                if (comment.SourceSpan.Start.Line < globalEndLine)
                    continue;

                // Find the next code line after this comment
                int nextCodeLine = FindNextCodeLine(comment, sortedComments);

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
        /// Finds the next line containing code after the given comment
        /// </summary>
        private int FindNextCodeLine(PeopleCodeParser.SelfHosted.Lexing.Token comment, List<PeopleCodeParser.SelfHosted.Lexing.Token> allComments)
        {
            int commentLine = comment.SourceSpan.Start.Line;

            // Look for the next non-comment node or the next comment that could be a code line
            // Since we don't have direct access to all tokens, we'll use a different approach
            // We'll check if this comment is immediately followed by another comment on the next line
            // If not, assume the next line has code

            int nextLine = commentLine + 1;

            // Check if there's a comment on the next line
            bool hasCommentOnNextLine = allComments.Any(c => c.SourceSpan.Start.Line == nextLine);

            if (hasCommentOnNextLine)
            {
                // If there's a comment on the next line, find the first line without a comment
                while (allComments.Any(c => c.SourceSpan.Start.Line == nextLine))
                {
                    nextLine++;
                }
            }

            return nextLine;
        }

        /// <summary>
        /// Processes a single suppression comment and extracts suppression rules
        /// </summary>
        private void ProcessSuppressionComment(PeopleCodeParser.SelfHosted.Lexing.Token comment, HashSet<SuppressionInfo> targetCollection)
        {
            var match = _suppressionRegex.Match(comment.Text);
            if (match.Success)
            {
                string suppressionList = match.Groups[1].Value;
                foreach (var item in suppressionList.Split(','))
                {
                    var parts = item.Trim().Split(':');
                    if (parts.Length == 2)
                    {
                        if (parts[1] == "*")
                        {
                            // Wildcard suppression
                            targetCollection.Add(new SuppressionInfo(parts[0], -1, true));
                        }
                        else if (int.TryParse(parts[1], out int reportNumber))
                        {
                            targetCollection.Add(new SuppressionInfo(parts[0], reportNumber));
                        }
                    }
                }
            }
        }

        #region Scope Handling Methods

        public override void VisitMethod(MethodNode node)
        {
            ProcessScopeEntrySuppressions(node.SourceSpan.Start.Line);
            base.VisitMethod(node);
            PopScopeSuppressions();
        }

        public override void VisitFunction(FunctionNode node)
        {
            ProcessScopeEntrySuppressions(node.SourceSpan.Start.Line);
            base.VisitFunction(node);
            PopScopeSuppressions();
        }

        public override void VisitIf(IfStatementNode node)
        {
            ProcessScopeEntrySuppressions(node.SourceSpan.Start.Line);
            base.VisitIf(node);
            PopScopeSuppressions();
        }

        public override void VisitFor(ForStatementNode node)
        {
            ProcessScopeEntrySuppressions(node.SourceSpan.Start.Line);
            base.VisitFor(node);
            PopScopeSuppressions();
        }

        public override void VisitWhile(WhileStatementNode node)
        {
            ProcessScopeEntrySuppressions(node.SourceSpan.Start.Line);
            base.VisitWhile(node);
            PopScopeSuppressions();
        }

        public override void VisitRepeat(RepeatStatementNode node)
        {
            ProcessScopeEntrySuppressions(node.SourceSpan.Start.Line);
            base.VisitRepeat(node);
            PopScopeSuppressions();
        }

        public override void VisitEvaluate(EvaluateStatementNode node)
        {
            ProcessScopeEntrySuppressions(node.SourceSpan.Start.Line);
            base.VisitEvaluate(node);
            PopScopeSuppressions();
        }

        public override void VisitTry(TryStatementNode node)
        {
            ProcessScopeEntrySuppressions(node.SourceSpan.Start.Line);
            base.VisitTry(node);
            PopScopeSuppressions();
        }

        #endregion

        /// <summary>
        /// Processes suppressions that apply to a specific scope (method, block, etc.)
        /// </summary>
        private void ProcessScopeEntrySuppressions(int scopeStartLine)
        {
            var scopeSuppressions = new HashSet<SuppressionInfo>();

            // Find comments immediately above this scope
            // Since we process comments upfront, we need to find suppressions that were processed
            // and determine if they apply to this scope

            // For now, we'll use a simplified approach - check if there are suppressions
            // on the line immediately before the scope
            if (_lineSpecificSuppressions.TryGetValue(scopeStartLine - 1, out var lineSuppressions))
            {
                // Copy the line suppressions to the scope suppressions
                foreach (var suppression in lineSuppressions)
                {
                    scopeSuppressions.Add(suppression);
                }
            }

            // Push the suppression set onto the stack
            _scopeSuppressionStack.Push(scopeSuppressions);
        }

        /// <summary>
        /// Removes the current scope suppressions from the stack
        /// </summary>
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
            // Create a specific suppression info for exact match check
            var specificSuppression = new SuppressionInfo(linterId, reportNumber);

            // Create a wildcard suppression info for wildcard match check
            var wildcardSuppression = new SuppressionInfo(linterId, -1, true);

            // 1. Check global suppressions first
            if (_globalSuppressions.Contains(specificSuppression) || _globalSuppressions.Contains(wildcardSuppression))
            {
                return true;
            }

            // 2. Check active scope suppressions from the stack
            foreach (var scopeSuppressions in _scopeSuppressionStack)
            {
                if (scopeSuppressions.Contains(specificSuppression) || scopeSuppressions.Contains(wildcardSuppression))
                {
                    return true;
                }
            }

            // 3. Check line-specific suppressions
            return _lineSpecificSuppressions.TryGetValue(line, out var lineSuppressions) &&
                (lineSuppressions.Contains(specificSuppression) || lineSuppressions.Contains(wildcardSuppression));
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
                // For wildcard suppressions, use -1 as a special value
                int reportNum = suppression.IsWildcard ? -1 : suppression.ReportNumber;
                result.Add((suppression.LinterId, reportNum));
            }

            // Add active scope suppressions from the stack
            foreach (var scopeSuppressions in _scopeSuppressionStack)
            {
                foreach (var suppression in scopeSuppressions)
                {
                    int reportNum = suppression.IsWildcard ? -1 : suppression.ReportNumber;
                    result.Add((suppression.LinterId, reportNum));
                }
            }

            // Add line-specific suppressions
            if (_lineSpecificSuppressions.TryGetValue(line, out var lineSuppressions))
            {
                foreach (var suppression in lineSuppressions)
                {
                    int reportNum = suppression.IsWildcard ? -1 : suppression.ReportNumber;
                    result.Add((suppression.LinterId, reportNum));
                }
            }

            return result;
        }

        /// <summary>
        /// Test method to verify suppression functionality
        /// </summary>
        public static void TestSuppressionFunctionality()
        {
            // Test code with suppression comments
            string testCode = @"
/* #AppRefiner suppress (TEST_LINTER:1) */
class TestClass
{
    /* #AppRefiner suppress (TEST_LINTER:2) */
    method TestMethod()
        Local number &x = 1; // This should be suppressed
    end-method;

    method AnotherMethod()
        Local number &y = 2; // This should also be suppressed due to global suppression
    end-method;
}
end-class;
";

            try
            {
                // Parse the test code
                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(testCode);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var program = parser.ParseProgram();

                if (program == null)
                {
                    Console.WriteLine("Failed to parse test code");
                    return;
                }

                // Create suppression processor and process
                var processor = new LinterSuppressionProcessor();
                program.Accept(processor);

                // Test suppressions
                bool suppressed1 = processor.IsSuppressed("TEST_LINTER", 1, 6); // Line with first suppression
                bool suppressed2 = processor.IsSuppressed("TEST_LINTER", 2, 8); // Line with second suppression
                bool suppressed3 = processor.IsSuppressed("TEST_LINTER", 1, 12); // Line that should be suppressed by global

                Console.WriteLine($"Suppression test results:");
                Console.WriteLine($"Line 6 (local suppression): {suppressed1}");
                Console.WriteLine($"Line 8 (scope suppression): {suppressed2}");
                Console.WriteLine($"Line 12 (global suppression): {suppressed3}");

                // Test wildcard suppression
                string wildcardTestCode = @"
/* #AppRefiner suppress (TEST_LINTER:*) */
class TestClass
{
    method TestMethod()
        Local number &x = 1; // This should be suppressed by wildcard
    end-method;
}
end-class;
";

                var lexer2 = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(wildcardTestCode);
                var tokens2 = lexer2.TokenizeAll();
                var parser2 = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens2);
                var program2 = parser2.ParseProgram();

                if (program2 != null)
                {
                    var processor2 = new LinterSuppressionProcessor();
                    program2.Accept(processor2);

                    bool wildcardSuppressed = processor2.IsSuppressed("TEST_LINTER", 999, 5); // Any report number should be suppressed
                    Console.WriteLine($"Wildcard suppression test: {wildcardSuppressed}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed with exception: {ex.Message}");
            }
        }
    }
}
