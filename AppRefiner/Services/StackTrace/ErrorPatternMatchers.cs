using System.Text.RegularExpressions;
using AppRefiner.Models;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted;

namespace AppRefiner.Services.StackTrace
{
    /// <summary>
    /// Error pattern matcher for "First operand of . is NULL" errors
    /// </summary>
    public class FirstOperandOfDotIsNullMatcher : IErrorPatternMatcher
    {
        private static readonly Regex Pattern = new(@"First operand of \. is NULL, so cannot access member (.*?)\.", RegexOptions.Compiled);
        
        public string PatternName => "FirstOperandOfDotIsNull";
        
        public bool Matches(string stackTrace)
        {
            return Pattern.IsMatch(stackTrace);
        }
        
        public Dictionary<string, string> ExtractData(string stackTrace)
        {
            var match = Pattern.Match(stackTrace);
            if (match.Success)
            {
                return new Dictionary<string, string>
                {
                    ["MemberName"] = match.Groups[1].Value
                };
            }
            return new Dictionary<string, string>();
        }
        
        public SourceSpan? GetEnhancedSelection(ProgramNode program, int lineNumber, Dictionary<string, string> errorData)
        {
            if (!errorData.TryGetValue("MemberName", out var memberName))
                return null;
                
            var visitor = new MemberAccessSearchVisitor(memberName);
            var statementNode = program.GetStatementAtLine(lineNumber);
            
            if (statementNode != null)
            {
                statementNode.Accept(visitor);
                return visitor.ExpressionSpan;
            }
            
            return null;
        }
    }

    /// <summary>
    /// Registry for error pattern matchers
    /// </summary>
    public static class ErrorPatternMatcherRegistry
    {
        private static readonly List<IErrorPatternMatcher> _matchers = new()
        {
            new FirstOperandOfDotIsNullMatcher()
        };
        
        /// <summary>
        /// Gets all registered error pattern matchers
        /// </summary>
        public static IReadOnlyList<IErrorPatternMatcher> GetMatchers() => _matchers.AsReadOnly();
        
        /// <summary>
        /// Registers a new error pattern matcher
        /// </summary>
        /// <param name="matcher">The matcher to register</param>
        public static void RegisterMatcher(IErrorPatternMatcher matcher)
        {
            if (!_matchers.Any(m => m.PatternName == matcher.PatternName))
            {
                _matchers.Add(matcher);
            }
        }
        
        /// <summary>
        /// Attempts to match the stack trace against all registered patterns
        /// </summary>
        /// <param name="stackTrace">The full stack trace text</param>
        /// <returns>The first matching pattern or null if no matches</returns>
        public static ErrorPatternMatch? MatchPattern(string stackTrace)
        {
            foreach (var matcher in _matchers)
            {
                if (matcher.Matches(stackTrace))
                {
                    var data = matcher.ExtractData(stackTrace);
                    return new ErrorPatternMatch
                    {
                        PatternName = matcher.PatternName,
                        ExtractedData = data
                    };
                }
            }
            return null;
        }
        
        /// <summary>
        /// Gets enhanced selection for a matched pattern
        /// </summary>
        /// <param name="patternMatch">The matched pattern</param>
        /// <param name="program">The parsed program</param>
        /// <param name="lineNumber">The line number where the error occurred</param>
        /// <returns>Enhanced selection span or null for default behavior</returns>
        public static SourceSpan? GetEnhancedSelection(ErrorPatternMatch patternMatch, ProgramNode program, int lineNumber)
        {
            var matcher = _matchers.FirstOrDefault(m => m.PatternName == patternMatch.PatternName);
            return matcher?.GetEnhancedSelection(program, lineNumber, patternMatch.ExtractedData);
        }
    }
    
    /// <summary>
    /// Internal visitor for finding member access expressions at specific lines
    /// Used for stack trace navigation to highlight problematic member accesses
    /// </summary>
    internal class MemberAccessSearchVisitor : AstVisitorBase
    {
        private readonly string _memberName;
        public SourceSpan? ExpressionSpan { get; private set; }
        
        public MemberAccessSearchVisitor(string memberName)
        {
            _memberName = memberName;
            ExpressionSpan = null;
        }

        public override void VisitMemberAccess(MemberAccessNode node)
        {
            base.VisitMemberAccess(node);

            if (node.MemberName.Equals(_memberName))
            {
                ExpressionSpan = node.Target.SourceSpan;
            }
        }
    }
}