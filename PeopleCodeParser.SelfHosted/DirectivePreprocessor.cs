using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Internal preprocessor that handles PeopleCode compiler directives in a first pass,
/// resolving #If/#Then/#Else/#End-If blocks before main parsing begins.
/// This allows directives to appear anywhere in the token stream, not just at statement boundaries.
/// </summary>
internal class DirectivePreprocessor
{
    private readonly List<Token> _originalTokens;
    private readonly ToolsVersion _toolsVersion;
    private readonly List<string> _errors = new();
    private List<SourceSpan> _skippedSpans = new();

    public List<SourceSpan> SkippedSpans => _skippedSpans;

    /// <summary>
    /// Create a new DirectivePreprocessor with the given tokens and ToolsVersion
    /// </summary>
    /// <param name="tokens">Original token stream including trivia</param>
    /// <param name="toolsVersion">ToolsVersion for directive evaluation</param>
    public DirectivePreprocessor(List<Token> tokens, ToolsVersion toolsVersion)
    {
        _originalTokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _toolsVersion = toolsVersion ?? throw new ArgumentNullException(nameof(toolsVersion));
    }

    /// <summary>
    /// Errors encountered during directive preprocessing
    /// </summary>
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Process all compiler directives and return the filtered token stream
    /// with inactive directive branches removed
    /// </summary>
    /// <returns>Token stream with directives resolved and inactive branches removed</returns>
    public List<Token> ProcessDirectives()
    {
        var result = new List<Token>();
        var directiveStack = new Stack<DirectiveContext>();
        var position = 0;
        DirectiveContext? lastContext = null;

        while (position < _originalTokens.Count)
        {
            var token = _originalTokens[position];

            switch (token.Type)
            {
                case TokenType.DirectiveIf:
                    position = ProcessDirectiveIf(position, directiveStack, result);
                    break;

                case TokenType.DirectiveElse:
                    position = ProcessDirectiveElse(position, directiveStack, result);
                    break;

                case TokenType.DirectiveEndIf:
                    (position, lastContext) = ProcessDirectiveEndIf(position, directiveStack, result);
                    if (lastContext != null)
                    {
                        // Record skipped spans for inactive branches
                        if (lastContext.ConditionResult)
                        {
                            // Condition was true - skip ELSE block if it exists
                            if (lastContext.HasElse)
                            {
                                _skippedSpans.Add(new SourceSpan(lastContext.ElseBlockStart, lastContext.ElseBlockEnd));
                            }
                        }
                        else
                        {
                            // Condition was false - skip IF block
                            _skippedSpans.Add(new SourceSpan(lastContext.IfBlockStart, lastContext.IfBlockEnd));
                        }
                    }
                    break;

                default:
                    // Regular token - add to result if we're in an active branch
                    if (IsTokenInActiveBranch(directiveStack))
                    {
                        result.Add(token);
                    }
                    position++;
                    break;
            }
        }

        // Check for unclosed directives
        if (directiveStack.Count > 0)
        {
            _errors.Add($"Unclosed directive block(s): {directiveStack.Count} remaining");
        }


        return result;
    }

    /// <summary>
    /// Process a #If directive and its condition
    /// </summary>
    private int ProcessDirectiveIf(int position, Stack<DirectiveContext> directiveStack, List<Token> result)
    {
        var startPos = position;
        position++; // Skip #If token

        try
        {
            // Extract condition tokens until #Then
            var conditionTokens = ExtractConditionTokens(position, out int conditionEndPos);
            position = conditionEndPos + 1; // Skip #Then token
            /* optional for there to be 0 or more ; after #Then */
            while (position < _originalTokens.Count && _originalTokens[position].Type == TokenType.Semicolon)
            {
                position++;
            }


            // Parse and evaluate condition
            bool conditionResult = EvaluateDirectiveCondition(conditionTokens);

            // Create directive context
            var context = new DirectiveContext
            {
                ConditionResult = conditionResult,
                HasElse = false,
                IsInElseBranch = false,
                StartPosition = startPos,
                IfBlockStart = _originalTokens[position].SourceSpan.Start,
            };

            directiveStack.Push(context);

            return position;
        }
        catch (Exception ex)
        {
            _errors.Add($"Error processing #If directive at position {startPos}: {ex.Message}");
            // Skip to next statement-level token for recovery
            return RecoverToNextStatement(position);
        }
    }

    /// <summary>
    /// Process a #Else directive
    /// </summary>
    private int ProcessDirectiveElse(int position, Stack<DirectiveContext> directiveStack, List<Token> result)
    {
        if (directiveStack.Count == 0)
        {
            _errors.Add($"#Else without matching #If at position {position}");
            return position + 1;
        }

        var context = directiveStack.Peek();
        context.IfBlockEnd = _originalTokens[position - 1].SourceSpan.End;

        if (context.HasElse)
        {
            _errors.Add($"Multiple #Else blocks in same #If directive at position {position}");
            return position + 1;
        }

        // Switch to else branch
        context.HasElse = true;
        context.IsInElseBranch = true;
        position++; // Skip #Else token
        context.ElseBlockStart = _originalTokens[position].SourceSpan.Start;
        return position;
    }

    /// <summary>
    /// Process a #End-If directive
    /// </summary>
    private (int, DirectiveContext?) ProcessDirectiveEndIf(int position, Stack<DirectiveContext> directiveStack, List<Token> result)
    {
        if (directiveStack.Count == 0)
        {
            _errors.Add($"#End-If without matching #If at position {position}");
            return (position + 1, null);
        }

        var context = directiveStack.Pop();
        context.ElseBlockEnd = _originalTokens[position].SourceSpan.Start;


        /* Skip any trailing semicolons after #End-If */
        position++; // Skip #End-If token
        while (position < _originalTokens.Count && _originalTokens[position].Type == TokenType.Semicolon)
        {
            position++;
        }

        return (position, context); // Skip #End-If token
    }

    /// <summary>
    /// Check if the current token should be included based on directive context
    /// </summary>
    private bool IsTokenInActiveBranch(Stack<DirectiveContext> directiveStack)
    {
        if (directiveStack.Count == 0)
            return true; // No directives, everything is active

        foreach (var context in directiveStack)
        {
            if (context.IsInElseBranch)
            {
                // In ELSE branch - include if original condition was false
                if (context.ConditionResult)
                    return false;
            }
            else
            {
                // In THEN branch - include if condition was true
                if (!context.ConditionResult)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Extract tokens from #If to #Then for condition parsing
    /// </summary>
    private List<Token> ExtractConditionTokens(int startPos, out int endPos)
    {
        var conditionTokens = new List<Token>();
        endPos = startPos;

        while (endPos < _originalTokens.Count && _originalTokens[endPos].Type != TokenType.DirectiveThen)
        {
            var token = _originalTokens[endPos];

            // Include non-trivia tokens and directive-specific tokens
            if (!token.Type.IsTrivia() ||
                token.Type == TokenType.DirectiveToolsRel ||
                token.Type == TokenType.DirectiveAnd ||
                token.Type == TokenType.DirectiveOr)
            {
                conditionTokens.Add(token);
            }

            endPos++;
        }

        if (endPos >= _originalTokens.Count)
        {
            throw new InvalidOperationException("#If directive missing #Then");
        }

        return conditionTokens;
    }

    /// <summary>
    /// Evaluate a directive condition using the DirectiveExpressionParser
    /// </summary>
    private bool EvaluateDirectiveCondition(List<Token> conditionTokens)
    {
        if (conditionTokens.Count == 0)
        {
            _errors.Add("Empty directive condition");
            return false; // Default to false for empty conditions
        }

        var parser = new DirectiveExpressionParser(conditionTokens);
        var expression = parser.ParseExpression();

        if (expression == null)
        {
            _errors.AddRange(parser.Errors);
            return false; // Default to false for parse failures
        }

        try
        {
            return expression.Evaluate(_toolsVersion);
        }
        catch (Exception ex)
        {
            _errors.Add($"Error evaluating directive condition: {ex.Message}");
            return false; // Default to false for evaluation failures
        }
    }

    /// <summary>
    /// Recover from directive parsing errors by finding the next safe position
    /// </summary>
    private int RecoverToNextStatement(int position)
    {
        // Look for statement-level synchronization tokens
        var syncTokens = new[]
        {
            TokenType.Semicolon,
            TokenType.DirectiveIf,
            TokenType.DirectiveElse,
            TokenType.DirectiveEndIf,
            TokenType.If,
            TokenType.For,
            TokenType.While,
            TokenType.Try,
            TokenType.Return
        };

        while (position < _originalTokens.Count)
        {
            if (syncTokens.Contains(_originalTokens[position].Type))
            {
                return position;
            }
            position++;
        }

        return position;
    }
}

/// <summary>
/// Context information for a directive block during preprocessing
/// </summary>
internal class DirectiveContext
{
    /// <summary>
    /// Whether the directive condition evaluated to true
    /// </summary>
    public bool ConditionResult { get; set; }

    /// <summary>
    /// Whether this directive has an #Else block
    /// </summary>
    public bool HasElse { get; set; }

    /// <summary>
    /// Whether we're currently in the #Else branch
    /// </summary>
    public bool IsInElseBranch { get; set; }

    /// <summary>
    /// Starting position in original token stream
    /// </summary>
    public int StartPosition { get; set; }


    public SourcePosition IfBlockStart { get; set; }
    public SourcePosition IfBlockEnd { get; set; }
    public SourcePosition ElseBlockStart { get; set; }
    public SourcePosition ElseBlockEnd { get; set; }


}
