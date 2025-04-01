using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using AppRefiner;
using AppRefiner.PeopleCode;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips showing the containing scope hierarchy for a line of code.
    /// Shows function/method, if/else, for/while, and evaluate blocks that contain the current line.
    /// </summary>
    public class ScopeTooltipProvider : ParseTreeTooltipProvider
    {
        private List<ScopeInfo> containingScopes = new List<ScopeInfo>();
        private string? currentContent;
        private string[]? contentLines;
        private bool initialized = false;
        
        // Map from line number to first token on that line
        private Dictionary<int, IToken> firstTokensOnLine = new Dictionary<int, IToken>();
        
        // Map from method name to parameter signature
        private Dictionary<string, string> methodParameterMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public override string Name => "Scope Context";

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public override string Description => "Shows containing scope hierarchy when hovering at the beginning of a line";

        /// <summary>
        /// Medium-high priority
        /// </summary>
        public override int Priority => 80;

        /// <summary>
        /// This provider accepts all token types, but filters by position during processing
        /// </summary>
        public override int[]? TokenTypes => null;

        /// <summary>
        /// Prepares the content and initializes the provider before walking the parse tree
        /// </summary>
        public void InitializeWithContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return;
            
            PrepareContent(content);
            initialized = true;
        }

        /// <summary>
        /// Resets the internal state of the tooltip provider.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            containingScopes.Clear();
            firstTokensOnLine.Clear();
            methodParameterMap.Clear();
            currentContent = null;
            contentLines = null;
            initialized = false;
        }

        /// <summary>
        /// When a token stream is set in TooltipManager, determine all the first tokens on each line
        /// </summary>
        public void InitializeWithTokenStream(CommonTokenStream tokenStream)
        {
            if (tokenStream == null || LineNumber <= 0)
                return;
                
            // Make sure we have all tokens
            tokenStream.Fill();
            var tokens = tokenStream.GetTokens();
            
            // Track the current line and the first token on that line
            int currentLine = -1;
            
            // Process all tokens to find first tokens on each line
            // Only need to track tokens up to and including our target LineNumber
            foreach (var token in tokens)
            {
                // Skip tokens on lines after our target line number - we don't need them
                if (token.Line > LineNumber)
                    break;
                    
                // Skip hidden channel tokens (comments, whitespace)
                if (token.Channel == Lexer.Hidden)
                    continue;
                    
                // Skip EOF token
                if (token.Type == TokenConstants.EOF)
                    continue;
                    
                // If this token is on a new line, it's the first token on that line
                if (token.Line > currentLine)
                {
                    currentLine = token.Line;
                    firstTokensOnLine[currentLine] = token;
                }
            }
            
            Debug.Log($"Found {firstTokensOnLine.Count} first tokens on lines (up to line {LineNumber})");
        }

        /// <summary>
        /// Processes a specific token to determine if it should trigger a scope tooltip
        /// </summary>
        public override bool CanProvideTooltipForToken(IToken token, int position)
        {
            // Only process if it's the first token on the line
            return token != null && firstTokensOnLine.ContainsValue(token);
        }

        /// <summary>
        /// Processes the content string to initialize line information
        /// </summary>
        private void PrepareContent(string content)
        {
            currentContent = content;
            contentLines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        /// <summary>
        /// Handles function definitions
        /// </summary>
        public override void EnterFunctionDefinition([NotNull] PeopleCodeParser.FunctionDefinitionContext context)
        {
            ProcessScope(context, "function");
        }

        /// <summary>
        /// Handle class method declarations to capture parameter signatures
        /// </summary>
        public override void EnterMethodHeader([NotNull] PeopleCodeParser.MethodHeaderContext context)
        {
            if (context == null || context.Start == null || contentLines == null)
                return;
                
            // Extract method name
            string methodName = "";
            if (context.genericID() != null)
            {
                methodName = context.genericID().GetText();
            }
            
            if (string.IsNullOrEmpty(methodName))
                return;
                
            // Get the full line of the method declaration to extract the parameter list
            int lineIndex = context.Start.Line - 1; // 0-based
            if (lineIndex < 0 || lineIndex >= contentLines.Length)
                return;
                
            string declarationLine = contentLines[lineIndex].Trim();
            
            // Extract parameter list from declaration line
            int openParenIndex = declarationLine.IndexOf('(');
            int closeParenIndex = declarationLine.LastIndexOf(')');
            
            if (openParenIndex >= 0 && closeParenIndex > openParenIndex)
            {
                string parameters = declarationLine.Substring(openParenIndex, closeParenIndex - openParenIndex + 1);
                
                // Store the method name and its parameter list
                methodParameterMap[methodName] = parameters;
                
                Debug.Log($"Recorded method parameters for {methodName}: {parameters}");
            }
        }

        /// <summary>
        /// Handles method implementations
        /// </summary>
        public override void EnterMethod([NotNull] PeopleCodeParser.MethodContext context)
        {
            ProcessScope(context, "method");
        }

        /// <summary>
        /// Handles if statements
        /// </summary>
        public override void EnterIfStatement([NotNull] PeopleCodeParser.IfStatementContext context)
        {
            // Skip if this scope starts after our target line
            if (context.Start.Line > LineNumber)
                return;
                
            ProcessScope(context, "If");
            
            // Check for Else branch and add it as a separate scope if it contains our line
            if (context.children != null)
            {
                bool foundElse = false;
                ParserRuleContext? elseBlock = null;
                int elseStartLine = -1;
                
                // Look for the ELSE token
                for (int i = 0; i < context.children.Count; i++)
                {
                    var child = context.children[i];
                    
                    // Found the ELSE token
                    if (child.GetText().Equals("else", StringComparison.OrdinalIgnoreCase))
                    {
                        foundElse = true;
                        
                        if (child is Antlr4.Runtime.Tree.ITerminalNode elseNode)
                        {
                            elseStartLine = elseNode.Symbol.Line;
                        }
                        
                        // The next child after ELSE should be the else block
                        if (i + 1 < context.children.Count && context.children[i + 1] is ParserRuleContext elseContent)
                        {
                            elseBlock = elseContent;
                            break;
                        }
                    }
                }
                
                // If we found an ELSE block, process it if it contains our target line
                if (foundElse && elseBlock != null && elseBlock.Stop != null)
                {
                    // If elseStartLine wasn't found, use the elseBlock's start line
                    if (elseStartLine == -1 && elseBlock.Start != null)
                    {
                        elseStartLine = elseBlock.Start.Line;
                    }
                    
                    // Check if our target line is within the Else block
                    if (elseStartLine <= LineNumber && LineNumber <= elseBlock.Stop.Line)
                    {
                        var elseScope = new ScopeInfo
                        {
                            ScopeType = "Else",
                            StartLine = elseStartLine,
                            EndLine = elseBlock.Stop.Line,
                            Context = elseBlock,
                            HeaderText = "Else", // Simple header for Else
                            ParentScopeStartLine = context.Start.Line, // Link to parent If
                            IsElseBlock = true
                        };
                        
                        // Add this scope to our list if it contains our target line
                        containingScopes.Add(elseScope);
                    }
                }
            }
        }

        /// <summary>
        /// Handles for statements
        /// </summary>
        public override void EnterForStatement([NotNull] PeopleCodeParser.ForStatementContext context)
        {
            ProcessScope(context, "For");
        }

        /// <summary>
        /// Handles while statements
        /// </summary>
        public override void EnterWhileStatement([NotNull] PeopleCodeParser.WhileStatementContext context)
        {
            ProcessScope(context, "While");
        }

        /// <summary>
        /// Handles evaluate statements
        /// </summary>
        public override void EnterEvaluateStatement([NotNull] PeopleCodeParser.EvaluateStatementContext context)
        {
            // Skip if this scope starts after our target line
            if (context.Start.Line > LineNumber)
                return;
                
            ProcessScope(context, "Evaluate");
            
            // Process each WHEN clause as a nested scope within Evaluate if it contains our line
            if (context.children != null)
            {
                foreach (var child in context.children)
                {
                    if (child is PeopleCodeParser.WhenClauseContext whenClause && whenClause.Start != null && whenClause.Stop != null)
                    {
                        // Only process if our line is within this When clause
                        if (whenClause.Start.Line <= LineNumber && LineNumber <= whenClause.Stop.Line)
                        {
                            // Create a scope info object for the when clause
                            var scopeInfo = new ScopeInfo
                            {
                                ScopeType = "When",
                                StartLine = whenClause.Start.Line,
                                EndLine = whenClause.Stop.Line,
                                Context = whenClause,
                                // Since When statements are always 1 line, just use the raw text
                                HeaderText = GetSingleLineText(whenClause)
                            };
                            
                            // Add this scope to our list
                            containingScopes.Add(scopeInfo);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets the text for a single-line statement
        /// </summary>
        private string GetSingleLineText(ParserRuleContext context)
        {
            if (context == null || context.Start == null || contentLines == null)
                return string.Empty;
                
            int lineIndex = context.Start.Line - 1; // 0-based
            if (lineIndex < 0 || lineIndex >= contentLines.Length)
                return string.Empty;
                
            return contentLines[lineIndex].Trim();
        }

        /// <summary>
        /// Common handler for scope contexts
        /// </summary>
        private void ProcessScope(ParserRuleContext context, string scopeType)
        {
            if (context == null || context.Start == null || context.Stop == null || !initialized)
                return;
                
            // Skip if this scope starts after our target line
            if (context.Start.Line > LineNumber)
                return;
                
            // Skip if this scope ends before our target line
            if (context.Stop.Line < LineNumber)
                return;

            // Create a scope info object only if our target line is within this scope
            var scopeInfo = new ScopeInfo
            {
                ScopeType = scopeType,
                StartLine = context.Start.Line,
                EndLine = context.Stop.Line,
                Context = context
            };

            // Get the text for this scope's header line
            string headerLine = GetContextHeaderLine(context);
            if (!string.IsNullOrEmpty(headerLine))
            {
                scopeInfo.HeaderText = headerLine.Trim();
            }

            // Add this scope to our list
            containingScopes.Add(scopeInfo);
        }

        /// <summary>
        /// Gets the header line text for a context
        /// </summary>
        private string GetContextHeaderLine(ParserRuleContext context)
        {
            if (context == null || context.Start == null || contentLines == null)
                return string.Empty;

            // Handle different context types to capture the complete header
            if (context is PeopleCodeParser.IfStatementContext ifContext)
            {
                var expression = ifContext.expression();
                return GetMultiLineExpressionForContext(ifContext, "If", "Then");
            }
            else if (context is PeopleCodeParser.ForStatementContext forContext)
            {
                // For statements include the variable, range, and step
                return GetMultiLineExpressionForContext(forContext, "For", "Step");
            }
            else if (context is PeopleCodeParser.WhileStatementContext whileContext)
            {
                return GetMultiLineExpressionForContext(whileContext, "While", null);
            }
            else if (context is PeopleCodeParser.EvaluateStatementContext evalContext)
            {
                // For Evaluate statements, we assume they're always one line
                return GetSingleLineText(evalContext);
            }
            else if (context is PeopleCodeParser.MethodContext methodContext)
            {
                // Get the first line of the method declaration (the METHOD keyword line)
                int lineIndex = context.Start.Line - 1;
                if (lineIndex < 0 || lineIndex >= contentLines.Length)
                    return string.Empty;
                    
                string methodLine = contentLines[lineIndex].Trim();
                
                // Extract method name without parameters from the implementation 
                string methodName = ExtractMethodNameFromImplementation(methodLine);
                
                // Check if we have parameter information for this method
                if (!string.IsNullOrEmpty(methodName) && methodParameterMap.TryGetValue(methodName, out var parameters))
                {
                    // If we found parameters, append them to the method name
                    return methodLine + parameters;
                }
                
                return methodLine;
            }
            else if (context is PeopleCodeParser.FunctionDefinitionContext funcContext)
            {
                // Get the first line of the function declaration (the FUNCTION keyword line)
                int lineIndex = context.Start.Line - 1;
                if (lineIndex < 0 || lineIndex >= contentLines.Length)
                    return string.Empty;
                    
                return contentLines[lineIndex].Trim();
            }
            else
            {
                // Default case - just get the first line
                int lineIndex = context.Start.Line - 1;
                if (lineIndex < 0 || lineIndex >= contentLines.Length)
                    return string.Empty;
                    
                return contentLines[lineIndex].Trim();
            }
        }

        /// <summary>
        /// Extracts method name from method implementation line
        /// </summary>
        private string ExtractMethodNameFromImplementation(string methodLine)
        {
            if (string.IsNullOrEmpty(methodLine))
                return string.Empty;
                
            // The method line typically has format "method ClassName.MethodName"
            // or just "method MethodName"
            string[] parts = methodLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return string.Empty;
                
            // Get the part after "method" keyword which should be the method name
            // (potentially with class prefix)
            string methodNamePart = parts[1];
            
            // Return the full name part (either "ClassName.MethodName" or just "MethodName")
            return methodNamePart;
        }

        /// <summary>
        /// Gets a multi-line expression for a context by analyzing its structure
        /// </summary>
        private string GetMultiLineExpressionForContext(ParserRuleContext context, string prefix, string? suffix)
        {
            if (context == null || context.Start == null || contentLines == null)
                return string.Empty;
                
            // Get start and end tokens based on context type and its structure
            IToken startToken = context.Start;
            IToken endToken;
            
            // Each context type has a different structure - find the appropriate end token
            if (context is PeopleCodeParser.IfStatementContext ifContext) 
            {
                // For If statements, find the THEN token
                var children = context.children.ToList();
                var thenTokenIndex = children.FindIndex(c => c.GetText().Equals("then", StringComparison.OrdinalIgnoreCase));
                if (thenTokenIndex > 0)
                {
                    // Get the token before THEN which is the last token of the condition
                    endToken = ((ParserRuleContext)children[thenTokenIndex - 1]).Stop;
                }
                else
                {
                    endToken = startToken; // Fallback
                }
            }
            else if (context is PeopleCodeParser.ForStatementContext forContext)
            {
                // For For statements, get up to the STEP or the last expression
                var children = context.children.ToList();
                var stepTokenIndex = children.FindIndex(c => c.GetText().Equals("step", StringComparison.OrdinalIgnoreCase));
                if (stepTokenIndex > 0)
                {
                    // Get the token before STEP which is the last token of the TO expression
                    endToken = ((ParserRuleContext)children[stepTokenIndex - 1]).Stop;
                }
                else
                {
                    // If no STEP, find the TO token and its expression
                    var toTokenIndex = children.FindIndex(c => c.GetText().Equals("to", StringComparison.OrdinalIgnoreCase));
                    if (toTokenIndex > 0 && toTokenIndex + 1 < children.Count)
                    {
                        // Get the expression after TO
                        var toExpression = children[toTokenIndex + 1] as ParserRuleContext;
                        if (toExpression != null && toExpression.Stop != null)
                        {
                            endToken = toExpression.Stop;
                        }
                        else
                        {
                            // If we can't find the TO expression, use default behavior
                            endToken = context.children.OfType<ParserRuleContext>()
                                .Where(c => c.Stop != null && c.Stop.Line > startToken.Line)
                                .OrderBy(c => c.Stop.Line)
                                .Select(c => c.Stop)
                                .LastOrDefault() ?? startToken;
                        }
                    }
                    else
                    {
                        // If we can't find the TO token, use default behavior
                        endToken = context.children.OfType<ParserRuleContext>()
                            .Where(c => c.Stop != null && c.Stop.Line > startToken.Line)
                            .OrderBy(c => c.Stop.Line)
                            .Select(c => c.Stop)
                            .LastOrDefault() ?? startToken;
                    }
                }
            }
            else if (context is PeopleCodeParser.WhileStatementContext whileContext)
            {
                // Since we're assuming single-line statements for structured statements,
                // just use the first line for While statements too
                return GetSingleLineText(context);
            }
            else
            {
                // Default handling - just use the first line
                return contentLines[startToken.Line - 1].Trim();
            }
            
            return ExtractTextBetweenTokens(startToken, endToken, prefix, suffix);
        }

        /// <summary>
        /// Extracts text between two tokens with formatting
        /// </summary>
        private string ExtractTextBetweenTokens(IToken startToken, IToken endToken, string prefix, string? suffix)
        {
            if (startToken == null || endToken == null || contentLines == null)
                return string.Empty;
                
            int startLine = startToken.Line - 1; // 0-based
            int endLine = endToken.Line - 1; // 0-based
                
            if (startLine < 0 || startLine >= contentLines.Length || 
                endLine < 0 || endLine >= contentLines.Length ||
                startLine > endLine)
            {
                return string.Empty;
            }
                
            // If expression is on a single line, just return that line
            if (startLine == endLine)
            {
                return contentLines[startLine].Trim();
            }
                
            // For multi-line expressions, build a simplified version that shows the structure
            var expressionBuilder = new StringBuilder();
            
            // Extract text from start line to end line
            for (int i = startLine; i <= endLine; i++)
            {
                if (i > startLine)
                {
                    expressionBuilder.Append(' ');
                }
                expressionBuilder.Append(contentLines[i].Trim());
            }
            
            string expressionText = expressionBuilder.ToString();
            
            // If the expression is very long, truncate it
            if (expressionText.Length > 100)
            {
                expressionText = expressionText.Substring(0, 97) + "...";
            }
            
            // Add the suffix if provided
            if (!string.IsNullOrEmpty(suffix) && !expressionText.EndsWith(suffix))
            {
                expressionText += " " + suffix;
            }
            
            return expressionText;
        }

        /// <summary>
        /// Attempts to get a tooltip for the current position in the editor.
        /// </summary>
        public override string? GetTooltip(ScintillaEditor editor, int position)
        {
            if (editor == null || !editor.IsValid())
                return null;

            // No need to re-prepare content if we already have it
            if (!initialized)
            {
                string content = editor.ContentString ?? ScintillaManager.GetScintillaText(editor);
                if (!string.IsNullOrEmpty(content))
                {
                    // We're initializing too late - the parse tree has already been walked
                    // This will only help for future tooltip requests, not the current one
                    PrepareContent(content);
                    initialized = true;
                }
                else
                {
                    return null;
                }
            }

            // Always use the provided line number from the base class
            if (LineNumber <= 0)
                return null;

            // Check if the cursor is at the beginning of a line by checking if it's inside or adjacent to
            // a first token on a line
            if (!IsPositionAtFirstToken(position))
                return null;

            // Check if we have any scopes containing our target line
            if (containingScopes.Count == 0)
                return null;

            // Skip scopes that start on the current line
            var relevantScopes = containingScopes
                .Where(s => s.StartLine != LineNumber)
                .ToList();

            // No scopes to display
            if (relevantScopes.Count == 0)
                return null;
                
            // First sort by scope type (methods/functions first), then by nesting level
            var methodScopes = relevantScopes
                .Where(s => s.ScopeType.Equals("method", StringComparison.OrdinalIgnoreCase) || 
                            s.ScopeType.Equals("function", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.StartLine)
                .ToList();
                
            // Then get all control flow scopes
            var controlScopes = relevantScopes
                .Where(s => !s.ScopeType.Equals("method", StringComparison.OrdinalIgnoreCase) && 
                            !s.ScopeType.Equals("function", StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            // Process Else blocks to reorganize them after their parent If blocks
            var processedControlScopes = ReorganizeControlScopes(controlScopes);

            // Create the tooltip with indentation
            var tooltipBuilder = new StringBuilder();
            int currentIndent = 0;

            // First add method/function scopes which are at the outermost level
            foreach (var scope in methodScopes)
            {
                tooltipBuilder.AppendLine(scope.HeaderText);
            }

            // Then add control flow scopes with proper indentation
            foreach (var scope in processedControlScopes)
            {
                string indentation = new string(' ', scope.IndentLevel * 2);
                tooltipBuilder.AppendLine(indentation + scope.HeaderText);
            }

            return tooltipBuilder.ToString().TrimEnd();
        }
        
        /// <summary>
        /// Reorganizes control flow scopes to handle nested If/Else structures correctly
        /// </summary>
        private List<ScopeInfo> ReorganizeControlScopes(List<ScopeInfo> controlScopes)
        {
            // First sort all scopes by their starting line
            var sortedScopes = controlScopes.OrderBy(s => s.StartLine).ToList();
            
            // Initialize indent levels based on nesting
            int currentIndent = 0;
            int latestEndLine = 0;
            
            // Process each scope to set its indent level
            for (int i = 0; i < sortedScopes.Count; i++)
            {
                var scope = sortedScopes[i];
                
                // When we see an Else, we don't increase the indent level from its parent If
                if (scope.IsElseBlock)
                {
                    // Find the parent If scope to match its indent level
                    var parentIf = sortedScopes.FirstOrDefault(s => 
                        s.StartLine == scope.ParentScopeStartLine && s.ScopeType == "If");
                    
                    if (parentIf != null)
                    {
                        scope.IndentLevel = parentIf.IndentLevel;
                    }
                    else
                    {
                        // If parent not found, just use current indent
                        scope.IndentLevel = currentIndent;
                    }
                }
                else
                {
                    // Normal flow - adjust indent based on nesting
                    if (scope.StartLine > latestEndLine)
                    {
                        // This scope starts after the previous one ends, so we need to reduce indent
                        while (currentIndent > 0)
                        {
                            bool foundParent = false;
                            
                            // Look for a parent scope whose end line is >= this scope's start line
                            for (int j = i - 1; j >= 0; j--)
                            {
                                if (sortedScopes[j].EndLine >= scope.StartLine)
                                {
                                    foundParent = true;
                                    break;
                                }
                            }
                            
                            if (foundParent)
                                break;
                                
                            currentIndent--;
                        }
                    }
                    
                    scope.IndentLevel = currentIndent;
                    
                    // If this is an If, increase the indent for child scopes
                    if (scope.ScopeType == "If" || scope.ScopeType == "For" || 
                        scope.ScopeType == "While" || scope.ScopeType == "Evaluate")
                    {
                        currentIndent++;
                    }
                }
                
                // Keep track of the latest end line
                latestEndLine = Math.Max(latestEndLine, scope.EndLine);
            }
            
            return sortedScopes;
        }

        /// <summary>
        /// Checks if the given position is at or near a first token on a line
        /// </summary>
        private bool IsPositionAtFirstToken(int position)
        {
            // If we're in an empty file or have no first tokens, return false
            if (firstTokensOnLine.Count == 0 || LineNumber <= 0)
                return false;
            
            // Since we only tracked tokens up to the target line, we should
            // only have one relevant token to check - the first token on the current line
            if (firstTokensOnLine.TryGetValue(LineNumber, out var firstToken))
            {
                // Check if position is at or before the first non-whitespace token
                return (position >= firstToken.StartIndex && position <= firstToken.StopIndex + 1);
            }
            
            // No first token on current line, check if position is in the indentation
            // before where a token would be (whitespace at beginning of line)
            if (contentLines != null && LineNumber <= contentLines.Length)
            {
                string lineContent = contentLines[LineNumber - 1];
                
                // Calculate where this position is in the line
                int positionInCurrentLine = CalculatePositionInLine(position);
                
                // Check if we're in the whitespace part of the line
                for (int i = 0; i < positionInCurrentLine && i < lineContent.Length; i++)
                {
                    if (!char.IsWhiteSpace(lineContent[i]))
                    {
                        // Found a non-whitespace character before our position, so we're not at start
                        return false;
                    }
                }
                
                // If we only saw whitespace or are at the beginning, we're good
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Calculate the column position within the current line
        /// </summary>
        private int CalculatePositionInLine(int position)
        {
            if (contentLines == null || LineNumber <= 0 || LineNumber > contentLines.Length)
                return 0;
                
            // Since LineNumber is now our target line and we know it's valid,
            // we can optimize by directly calculating the position in line
            
            // Calculate position of the start of the current line
            int startPosition = 0;
            for (int i = 0; i < LineNumber - 1; i++)
            {
                startPosition += contentLines[i].Length + Environment.NewLine.Length;
            }
            
            // Position in line is current position minus start of line position
            // Make sure it's not negative
            int columnPosition = position - startPosition;
            return Math.Max(0, columnPosition);
        }

        /// <summary>
        /// Stores information about a scope for generating tooltips
        /// </summary>
        private class ScopeInfo
        {
            public string ScopeType { get; set; } = "";
            public int StartLine { get; set; }
            public int EndLine { get; set; }
            public string HeaderText { get; set; } = "";
            public ParserRuleContext? Context { get; set; }
            
            /// <summary>
            /// For Else blocks, stores the start line of the parent If statement
            /// This helps us link Else blocks to their parent If statements
            /// </summary>
            public int ParentScopeStartLine { get; set; } = -1;
            
            /// <summary>
            /// Flag to indicate if this is an Else block
            /// </summary>
            public bool IsElseBlock { get; set; } = false;
            
            /// <summary>
            /// Indentation level for displaying in the tooltip
            /// </summary>
            public int IndentLevel { get; set; } = 0;
        }
    }
} 