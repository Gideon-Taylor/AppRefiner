using System.Globalization;
using System.Text;

namespace PeopleCodeParser.SelfHosted.Lexing;

/// <summary>
/// Self-hosted lexer for PeopleCode that produces tokens from source text
/// </summary>
public class PeopleCodeLexer
{
    private readonly string _source;
    private readonly int[] _charToByteIndex;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private readonly List<LexError> _errors = new();

    // Keyword mapping for fast lookup
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core keywords
        { "ABSTRACT", TokenType.Abstract },
        { "ALIAS", TokenType.Alias },
        { "AND", TokenType.And },
        { "ANY", TokenType.Any },
        { "ARRAY", TokenType.Array },
        { "ARRAY2", TokenType.Array2 },
        { "ARRAY3", TokenType.Array3 },
        { "ARRAY4", TokenType.Array4 },
        { "ARRAY5", TokenType.Array5 },
        { "ARRAY6", TokenType.Array6 },
        { "ARRAY7", TokenType.Array7 },
        { "ARRAY8", TokenType.Array8 },
        { "ARRAY9", TokenType.Array9 },
        { "AS", TokenType.As },
        { "BOOLEAN", TokenType.Boolean },
        { "BREAK", TokenType.Break },
        { "CATCH", TokenType.Catch },
        { "CLASS", TokenType.Class },
        { "COMPONENT", TokenType.Component },
        { "COMPONENTLIFE", TokenType.Component },
        { "PANELGROUP", TokenType.Component },
        { "CONSTANT", TokenType.Constant },
        { "CONTINUE", TokenType.Continue },
        { "CREATE", TokenType.Create },
        { "DATE", TokenType.Date },
        { "DATETIME", TokenType.DateTime },
        { "DECLARE", TokenType.Declare },
        { "DOC", TokenType.Doc },
        { "ELSE", TokenType.Else },
        { "ERROR", TokenType.Error },
        { "EVALUATE", TokenType.Evaluate },
        { "EXCEPTION", TokenType.Exception },
        { "EXIT", TokenType.Exit },
        { "EXTENDS", TokenType.Extends },
        { "FLOAT", TokenType.Float },
        { "FOR", TokenType.For },
        { "FUNCTION", TokenType.Function },
        { "GET", TokenType.Get },
        { "GLOBAL", TokenType.Global },
        { "IF", TokenType.If },
        { "IMPLEMENTS", TokenType.Implements },
        { "IMPORT", TokenType.Import },
        { "INSTANCE", TokenType.Instance },
        { "INTEGER", TokenType.Integer },
        { "INTERFACE", TokenType.Interface },
        { "LIBRARY", TokenType.Library },
        { "LOCAL", TokenType.Local },
        { "METHOD", TokenType.Method },
        { "NOT", TokenType.Not },
        { "NULL", TokenType.Null },
        { "NUMBER", TokenType.Number },
        { "OF", TokenType.Of },
        { "OR", TokenType.Or },
        { "OUT", TokenType.Out },
        { "PEOPLECODE", TokenType.PeopleCode },
        { "PRIVATE", TokenType.Private },
        { "PROPERTY", TokenType.Property },
        { "PROTECTED", TokenType.Protected },
        { "READONLY", TokenType.ReadOnly },
        { "REF", TokenType.Ref },
        { "REPEAT", TokenType.Repeat },
        { "RETURN", TokenType.Return },
        { "RETURNS", TokenType.Returns },
        { "SET", TokenType.Set },
        { "STEP", TokenType.Step },
        { "STRING", TokenType.String },
        { "THEN", TokenType.Then },
        { "THROW", TokenType.Throw },
        { "TIME", TokenType.Time },
        { "TO", TokenType.To },
        { "TRY", TokenType.Try },
        { "UNTIL", TokenType.Until },
        { "VALUE", TokenType.Value },
        { "WARNING", TokenType.Warning },
        { "WHEN", TokenType.When },
        { "WHEN-OTHER", TokenType.WhenOther },
        { "WHILE", TokenType.While },

        // END keywords
        { "END-CLASS", TokenType.EndClass },
        { "END-EVALUATE", TokenType.EndEvaluate },
        { "END-FOR", TokenType.EndFor },
        { "END-FUNCTION", TokenType.EndFunction },
        { "END-GET", TokenType.EndGet },
        { "END-IF", TokenType.EndIf },
        { "END-INTERFACE", TokenType.EndInterface },
        { "END-METHOD", TokenType.EndMethod },
        { "END-SET", TokenType.EndSet },
        { "END-TRY", TokenType.EndTry },
        { "END-WHILE", TokenType.EndWhile }
    };

    // Record event mapping
    private static readonly HashSet<string> RecordEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "FIELDDEFAULT", "FIELDEDIT", "FIELDCHANGE", "FIELDFORMULA",
        "ROWINIT", "ROWINSERT", "ROWDELETE", "ROWSELECT",
        "SAVEEDIT", "SAVEPRECHANGE", "SAVEPOSTCHANGE",
        "SEARCHINIT", "SEARCHSAVE", "WORKFLOW", "PREPOPUP"
    };

    // System variable patterns (starting with %)
    private static readonly HashSet<string> SystemVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "%ALLOWNOTIFICATION", "%ALLOWRECIPIENTLOOKUP", "%APPLICATIONLOGFENCE", "%ASOFDATE",
        "%AUTHENTICATIONTOKEN", "%BPNAME", "%CLIENTDATE", "%CLIENTTIMEZONE", "%COMPINTFCNAME",
        "%COMPONENT", "%CONTENTID", "%CONTENTTYPE", "%COPYRIGHT", "%CURRENCY", "%DATE",
        "%DATETIME", "%DBNAME", "%DBSERVERNAME", "%DBTYPE", "%EMAILADDRESS", "%EMPLOYEEID",
        "%EXTERNALAUTHINFO", "%FILEPATH", "%HPTABNAME", "%IMPORT", "%INTBROKER",
        "%ISMULTILANGUAGEENABLED", "%LANGUAGE", "%LANGUAGE_BASE", "%LANGUAGE_DATA",
        "%LANGUAGE_USER", "%LOCALNODE", "%MAP_MARKET", "%MARKET", "%MAXMESSAGESIZE",
        "%MAXNBRSEGMENTS", "%MENU", "%MODE", "%NAVIGATORHOMEPERMISSIONLIST", "%NODE",
        "%OPERATORCLASS", "%OPERATORID", "%OPERATORROWLEVELSECURITYCLASS", "%OUTDESTFORMAT",
        "%OUTDESTTYPE", "%PAGE", "%PANEL", "%PANELGROUP", "%PASSWORDEXPIRED", "%PERFTIME",
        "%PERMISSIONLISTS", "%PID", "%PORTAL", "%PRIMARYPERMISSIONLIST",
        "%PROCESSPROFILEPERMISSIONLIST", "%PSAUTHRESULT", "%REQUEST", "%RESPONSE",
        "%RESULTDOCUMENT", "%ROLES", "%ROWSECURITYPERMISSIONLIST", "%RUNNINGINPORTAL",
        "%SERVERTIMEZONE", "%SESSION", "%SIGNONUSERID", "%SIGNONUSERPSWD",
        "%SMTPBLACKBERRYREPLYTO", "%SMTPGUARANTEED", "%SMTPSENDER", "%SQLROWS",
        "%THIS", "%TIME", "%TRANSFORMDATA", "%USERDESCRIPTION", "%USERID",
        "%WLINSTANCEID", "%WLNAME"
    };

    public PeopleCodeLexer(string source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        // Pre-compute character position to byte position mapping for efficient lookup
        _charToByteIndex = new int[source.Length + 1];
        int bytePos = 0;
        for (int charPos = 0; charPos < source.Length; charPos++)
        {
            _charToByteIndex[charPos] = bytePos;
            bytePos += Encoding.UTF8.GetByteCount(source, charPos, 1);
        }
        _charToByteIndex[source.Length] = bytePos; // End position

        _position = 0;
        _line = 0;
        _column = 0;
    }

    /// <summary>
    /// Errors encountered during lexing
    /// </summary>
    public IReadOnlyList<LexError> Errors => _errors.AsReadOnly();

    /// <summary>
    /// True if at end of source
    /// </summary>
    public bool IsAtEnd => _position >= _source.Length;

    /// <summary>
    /// Current character (null if at end)
    /// </summary>
    private char CurrentChar => IsAtEnd ? '\0' : _source[_position];

    /// <summary>
    /// Peek ahead at next character
    /// </summary>
    private char PeekChar(int offset = 1)
    {
        var peekPos = _position + offset;
        return peekPos >= _source.Length ? '\0' : _source[peekPos];
    }

    /// <summary>
    /// Advance to next character and return it
    /// </summary>
    private char Advance()
    {
        if (IsAtEnd) return '\0';

        var ch = _source[_position++];
        if (ch == '\n')
        {
            _line++;
            _column = 1;
        }
        else if (ch != '\r') // Don't advance column for \r in \r\n
        {
            _column++;
        }

        return ch;
    }

    /// <summary>
    /// Get current source position
    /// </summary>
    private SourcePosition CurrentPosition => new(_position, _charToByteIndex[_position], _line, _column);

    /// <summary>
    /// Create a source span from start position to current position
    /// </summary>
    private SourceSpan CreateSpan(SourcePosition start)
    {
        return new SourceSpan(start, CurrentPosition);
    }

    /// <summary>
    /// Add an error
    /// </summary>
    private void AddError(string message, SourcePosition? position = null)
    {
        _errors.Add(new LexError(message, position ?? CurrentPosition));
    }

    /// <summary>
    /// Tokenize the entire source and return all tokens
    /// </summary>
    public List<Token> TokenizeAll()
    {
        var tokens = new List<Token>();

        while (!IsAtEnd)
        {
            var token = NextToken();
            if (token != null)
            {
                // For TokenizeAll(), flatten comment trivia as separate tokens (but not whitespace)
                foreach (var trivia in token.LeadingTrivia)
                {
                    if (trivia.Type != TokenType.Whitespace)
                    {
                        tokens.Add(trivia);
                    }
                }

                tokens.Add(token);
            }
        }

        // Add EOF token
        tokens.Add(Token.CreateEof(CurrentPosition));

        return tokens;
    }

    /// <summary>
    /// Get the next token from the source
    /// </summary>
    public Token? NextToken()
    {
        // Collect leading trivia (whitespace and comments)
        var leadingTrivia = new List<Token>();

        while (!IsAtEnd)
        {
            if (char.IsWhiteSpace(CurrentChar))
            {
                leadingTrivia.Add(ScanWhitespace());
            }
            else if (CurrentChar == '/' && PeekChar() == '*')
            {
                leadingTrivia.Add(ScanBlockComment());
            }
            else if (CurrentChar == '<' && PeekChar() == '*')
            {
                leadingTrivia.Add(ScanNestedComment());
            }
            else if (IsRemComment())
            {
                leadingTrivia.Add(ScanRemComment());
            }
            else
            {
                break; // Found non-trivia token
            }
        }

        if (IsAtEnd)
        {
            // If we have collected trivia but reached EOF, return the last non-whitespace trivia as a token
            // This handles cases like standalone comments at the end of a file
            for (int i = leadingTrivia.Count - 1; i >= 0; i--)
            {
                if (leadingTrivia[i].Type != TokenType.Whitespace)
                {
                    return leadingTrivia[i];
                }
            }
            return null;
        }

        var start = CurrentPosition;
        var ch = CurrentChar;

        // Handle different character types
        Token? token = ch switch
        {
            // Special operators
            '/' when PeekChar() == '+' => ScanSlashPlus(),

            // Operators and punctuation
            '+' when PeekChar() == '/' => ScanPlusSlash(),
            '+' when PeekChar() == '=' => ScanTwoCharOperator(TokenType.PlusEqual),
            '+' => ScanSingleCharOperator(TokenType.Plus),

            '-' when PeekChar() == '=' => ScanTwoCharOperator(TokenType.MinusEqual),
            '-' when char.IsDigit(PeekChar()) => ScanNumber(),
            '-' => ScanSingleCharOperator(TokenType.Minus),

            '*' when PeekChar() == '*' => ScanTwoCharOperator(TokenType.Power),
            '*' => ScanSingleCharOperator(TokenType.Star),

            '/' => ScanSingleCharOperator(TokenType.Div),

            '=' => ScanSingleCharOperator(TokenType.Equal),

            '<' when PeekChar() == '>' => ScanTwoCharOperator(TokenType.NotEqual),
            '<' when PeekChar() == '=' => ScanTwoCharOperator(TokenType.LessThanOrEqual),
            '<' => ScanSingleCharOperator(TokenType.LessThan),

            '>' when PeekChar() == '=' => ScanTwoCharOperator(TokenType.GreaterThanOrEqual),
            '>' => ScanSingleCharOperator(TokenType.GreaterThan),

            '!' when PeekChar() == '=' => ScanTwoCharOperator(TokenType.NotEqual),

            '|' when PeekChar() == '|' => ScanTwoCharOperator(TokenType.DirectiveOr),
            '|' when PeekChar() == '=' => ScanTwoCharOperator(TokenType.PipeEqual),
            '|' => ScanSingleCharOperator(TokenType.Pipe),

            '(' => ScanSingleCharOperator(TokenType.LeftParen),
            ')' => ScanSingleCharOperator(TokenType.RightParen),
            '[' => ScanSingleCharOperator(TokenType.LeftBracket),
            ']' => ScanSingleCharOperator(TokenType.RightBracket),
            ';' => ScanSingleCharOperator(TokenType.Semicolon),
            ',' => ScanSingleCharOperator(TokenType.Comma),
            '.' => ScanSingleCharOperator(TokenType.Dot),
            ':' => ScanSingleCharOperator(TokenType.Colon),
            '@' => ScanSingleCharOperator(TokenType.At),

            // String literals
            '"' => ScanStringLiteral('"'),
            '\'' => ScanStringLiteral('\''),

            // Numbers
            _ when char.IsDigit(ch) => ScanNumber(),

            // Identifiers, keywords, and variables
            '&' when PeekChar() == '&' => ScanTwoCharOperator(TokenType.DirectiveAnd),
            '&' when IsIdentifierChar(PeekChar()) => ScanUserVariable(),
            '%' => ScanSystemIdentifier(),
            '#' when IsDirectiveStart() => ScanDirective(),

            // Letters and identifiers  
            _ when char.IsLetter(ch) || ch == '_' || ch == '$' || ch == '#' => ScanIdentifierOrKeyword(),

            _ => ScanInvalidCharacter()
        };

        // Attach leading trivia to the token
        if (token != null)
        {
            foreach (var trivia in leadingTrivia)
            {
                token.AddLeadingTrivia(trivia);
            }
        }

        return token;
    }

    private Token ScanWhitespace()
    {
        var start = CurrentPosition;
        var sb = new StringBuilder();

        while (!IsAtEnd && char.IsWhiteSpace(CurrentChar))
        {
            sb.Append(Advance());
        }

        return Token.CreateTrivia(TokenType.Whitespace, sb.ToString(), CreateSpan(start));
    }

    private Token ScanSingleCharOperator(TokenType type)
    {
        var start = CurrentPosition;
        var ch = Advance();
        return Token.CreateOperator(type, ch.ToString(), CreateSpan(start));
    }

    private Token ScanTwoCharOperator(TokenType type)
    {
        var start = CurrentPosition;
        var ch1 = Advance();
        var ch2 = Advance();
        return Token.CreateOperator(type, new string(new[] { ch1, ch2 }), CreateSpan(start));
    }

    private Token ScanSlashPlus()
    {
        var start = CurrentPosition;
        Advance(); // '/'
        Advance(); // '+'
        return Token.CreateOperator(TokenType.SlashPlus, "/+", CreateSpan(start));
    }

    private Token ScanPlusSlash()
    {
        var start = CurrentPosition;
        Advance(); // '+'
        Advance(); // '/'
        return Token.CreateOperator(TokenType.PlusSlash, "+/", CreateSpan(start));
    }

    private bool IsInCommentContext()
    {
        // Check if we're at the start of a line or after a semicolon
        if (_position == 0) return true;

        // Look backward for context
        for (int i = _position - 1; i >= 0; i--)
        {
            var ch = _source[i];
            if (ch == '\n' || ch == '\r')
                return true; // Start of line
            if (ch == ';')
                return true; // After statement separator
            if (!char.IsWhiteSpace(ch))
                return false; // Found non-whitespace, non-separator
        }

        return true; // Beginning of file
    }

    private bool IsRemComment()
    {
        // First, check if we're in a context where REM comments are valid
        if (!IsInCommentContext())
            return false;

        // Check if this looks like "REM" or "REMARK"
        if (_position + 2 < _source.Length)
        {
            var text = _source.Substring(_position, 3).ToUpperInvariant();
            if (text == "REM")
            {
                // Must be followed by space, end of input, or 'A' (for REMARK)
                var next = _position + 3 < _source.Length ? _source[_position + 3] : '\0';
                return next == '\0' || char.IsWhiteSpace(next) || char.ToUpperInvariant(next) == 'A' || char.IsSymbol(next) || char.IsPunctuation(next);
            }
        }
        // Also check for "REMARK"
        if (_position + 5 < _source.Length)
        {
            var text = _source.Substring(_position, 6).ToUpperInvariant();
            if (text == "REMARK")
            {
                var next = _position + 6 < _source.Length ? _source[_position + 6] : '\0';
                return next == '\0' || char.IsWhiteSpace(next);
            }
        }
        return false;
    }

    private Token ScanRemComment()
    {
        var start = CurrentPosition;
        var sb = new StringBuilder();

        // Scan "REM" or "REMARK"
        while (!IsAtEnd && char.IsLetter(CurrentChar))
        {
            sb.Append(Advance());
        }

        // Scan until semicolon
        while (!IsAtEnd && CurrentChar != ';')
        {
            sb.Append(Advance());
        }

        if (!IsAtEnd && CurrentChar == ';')
        {
            sb.Append(Advance());
        }

        return Token.CreateTrivia(TokenType.LineComment, sb.ToString(), CreateSpan(start));
    }

    private Token ScanBlockComment()
    {
        var start = CurrentPosition;
        var sb = new StringBuilder();

        Advance(); // '/'
        Advance(); // '*'
        sb.Append("/*");

        // Check if this is an API comment (/** but not /**/)
        var isApiComment = CurrentChar == '*' && PeekChar() != '/';
        if (isApiComment)
        {
            sb.Append(Advance()); // Additional '*'
        }

        while (!IsAtEnd)
        {
            if (CurrentChar == '*' && PeekChar() == '/')
            {
                sb.Append(Advance()); // '*'
                sb.Append(Advance()); // '/'
                break;
            }
            sb.Append(Advance());
        }

        var tokenType = isApiComment ? TokenType.ApiComment : TokenType.BlockComment;
        return Token.CreateTrivia(tokenType, sb.ToString(), CreateSpan(start));
    }

    private Token ScanNestedComment()
    {
        var start = CurrentPosition;
        var sb = new StringBuilder();
        var depth = 0;

        while (!IsAtEnd)
        {
            if (CurrentChar == '<' && PeekChar() == '*')
            {
                depth++;
                sb.Append(Advance()); // '<'
                sb.Append(Advance()); // '*'
            }
            else if (CurrentChar == '*' && PeekChar() == '>')
            {
                sb.Append(Advance()); // '*'
                sb.Append(Advance()); // '>'
                depth--;
                if (depth == 0) break;
            }
            else
            {
                sb.Append(Advance());
            }
        }

        return Token.CreateTrivia(TokenType.NestedComment, sb.ToString(), CreateSpan(start));
    }

    private Token ScanStringLiteral(char quote)
    {
        var start = CurrentPosition;
        var rawText = new StringBuilder();
        var value = new StringBuilder();

        rawText.Append(Advance()); // Opening quote

        while (!IsAtEnd)
        {
            if (CurrentChar == quote)
            {
                if (PeekChar() == quote)
                {
                    // Escaped quote - add both to raw text, one to value
                    rawText.Append(Advance()); // First quote
                    rawText.Append(Advance()); // Second quote
                    value.Append(quote); // Single quote in value
                }
                else
                {
                    // End of string
                    break;
                }
            }
            else
            {
                var ch = Advance();
                rawText.Append(ch);
                value.Append(ch);
            }
        }

        if (IsAtEnd)
        {
            AddError($"Unterminated string literal");
            return Token.CreateLiteral(TokenType.StringLiteral, rawText.ToString(), CreateSpan(start), value.ToString());
        }

        rawText.Append(Advance()); // Closing quote

        return Token.CreateLiteral(TokenType.StringLiteral, rawText.ToString(), CreateSpan(start), value.ToString());
    }

    private Token ScanNumber()
    {
        var start = CurrentPosition;
        var sb = new StringBuilder();
        var hasDecimal = false;

        // Handle negative numbers
        if (CurrentChar == '-')
        {
            sb.Append(Advance());
        }

        while (!IsAtEnd && char.IsDigit(CurrentChar))
        {
            sb.Append(Advance());
        }

        // Check for decimal point
        if (!IsAtEnd && CurrentChar == '.' && char.IsDigit(PeekChar()))
        {
            hasDecimal = true;
            sb.Append(Advance()); // '.'

            while (!IsAtEnd && char.IsDigit(CurrentChar))
            {
                sb.Append(Advance());
            }
        }

        var text = sb.ToString();
        object value;
        TokenType tokenType;

        if (hasDecimal)
        {
            tokenType = TokenType.DecimalLiteral;
            value = decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue)
                ? decimalValue : 0m;
        }
        else
        {
            tokenType = TokenType.IntegerLiteral;
            value = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
                ? intValue : 0;
        }

        return Token.CreateLiteral(tokenType, text, CreateSpan(start), value);
    }

    private Token ScanUserVariable()
    {
        var start = CurrentPosition;
        var sb = new StringBuilder();

        Advance(); // '&'
        sb.Append('&');

        while (!IsAtEnd && IsIdentifierChar(CurrentChar))
        {
            sb.Append(Advance());
        }

        return Token.CreateIdentifier(TokenType.UserVariable, sb.ToString(), CreateSpan(start));
    }

    private Token ScanSystemIdentifier()
    {
        var start = CurrentPosition;
        var sb = new StringBuilder();

        Advance(); // '%'
        sb.Append('%');

        // Special case for %SUPER
        if (MatchText("SUPER"))
        {
            sb.Append("SUPER");
            for (int i = 0; i < 5; i++) Advance();
            return Token.CreateKeyword(TokenType.Super, sb.ToString(), CreateSpan(start));
        }

        // Special case for %METADATA  
        if (MatchText("METADATA"))
        {
            sb.Append("METADATA");
            for (int i = 0; i < 8; i++) Advance();
            return Token.CreateIdentifier(TokenType.Metadata, sb.ToString(), CreateSpan(start));
        }

        // Scan the rest of the identifier
        while (!IsAtEnd && IsIdentifierChar(CurrentChar))
        {
            sb.Append(Advance());
        }

        var text = sb.ToString();

        // Check if it's a known system variable
        if (SystemVariables.Contains(text))
        {
            return Token.CreateIdentifier(TokenType.SystemVariable, text, CreateSpan(start));
        }

        // Otherwise, it's a system constant
        return Token.CreateIdentifier(TokenType.SystemConstant, text, CreateSpan(start));
    }

    private Token ScanDirective()
    {
        var start = CurrentPosition;
        var sb = new StringBuilder();

        Advance(); // '#'
        sb.Append('#');

        // Scan the directive keyword
        while (!IsAtEnd && (char.IsLetter(CurrentChar) || CurrentChar == '-'))
        {
            sb.Append(Advance());
        }

        var directive = sb.ToString().ToUpperInvariant();
        var tokenType = directive switch
        {
            "#IF" => TokenType.DirectiveIf,
            "#ELSE" => TokenType.DirectiveElse,
            "#END-IF" => TokenType.DirectiveEndIf,
            "#THEN" => TokenType.DirectiveThen,
            "#TOOLSREL" => TokenType.DirectiveToolsRel,
            _ => TokenType.DirectiveAtom
        };

        return Token.CreateTrivia(tokenType, sb.ToString(), CreateSpan(start));
    }

    private Token ScanIdentifierOrKeyword()
    {
        var start = CurrentPosition;
        var sb = new StringBuilder();

        while (!IsAtEnd && IsIdentifierChar(CurrentChar))
        {
            sb.Append(Advance());
        }

        var text = sb.ToString();

        // Check for multi-word keywords like "END-CLASS"
        if (text.ToUpperInvariant() == "END" && !IsAtEnd && (CurrentChar == ' ' || CurrentChar == '-'))
        {
            // Look ahead for END keywords
            var endKeyword = ScanEndKeyword(sb, start);
            if (endKeyword != null) return endKeyword;
        }

        // Check for WHEN-OTHER pattern
        if (text.ToUpperInvariant() == "WHEN" && !IsAtEnd && CurrentChar == '-')
        {
            // Look ahead for WHEN-OTHER keyword
            var whenKeyword = ScanWhenKeyword(sb, start);
            if (whenKeyword != null) return whenKeyword;
        }

        // Check for boolean literals first (before keywords)
        if (text.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
        {
            return Token.CreateLiteral(TokenType.BooleanLiteral, text, CreateSpan(start), true);
        }
        if (text.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
        {
            return Token.CreateLiteral(TokenType.BooleanLiteral, text, CreateSpan(start), false);
        }

        // Check if it's a record event
        if (RecordEvents.Contains(text))
        {
            return Token.CreateIdentifier(TokenType.RecordEvent, text, CreateSpan(start));
        }

        // Check if it's a keyword
        if (Keywords.TryGetValue(text, out var keywordType))
        {
            return Token.CreateKeyword(keywordType, text, CreateSpan(start));
        }

        // It's a generic identifier
        return Token.CreateIdentifier(TokenType.GenericId, text, CreateSpan(start));
    }

    private Token? ScanEndKeyword(StringBuilder sb, SourcePosition start)
    {
        var savedPosition = _position;
        var savedLine = _line;
        var savedColumn = _column;

        // Skip whitespace and hyphens
        while (!IsAtEnd && (CurrentChar == ' ' || CurrentChar == '-'))
        {
            sb.Append(Advance());
        }

        // Scan the next word
        var secondWord = new StringBuilder();
        while (!IsAtEnd && char.IsLetter(CurrentChar))
        {
            var ch = Advance();
            sb.Append(ch);
            secondWord.Append(ch);
        }

        var fullKeyword = sb.ToString().Replace(" ", "-").Replace("--", "-").ToUpperInvariant();

        if (Keywords.TryGetValue(fullKeyword, out var keywordType))
        {
            return Token.CreateKeyword(keywordType, sb.ToString(), CreateSpan(start));
        }

        // Not a recognized END keyword, backtrack
        _position = savedPosition;
        _line = savedLine;
        _column = savedColumn;
        return null;
    }

    private Token? ScanWhenKeyword(StringBuilder sb, SourcePosition start)
    {
        var savedPosition = _position;
        var savedLine = _line;
        var savedColumn = _column;

        // Skip hyphens (should be exactly one for WHEN-OTHER)
        if (!IsAtEnd && CurrentChar == '-')
        {
            sb.Append(Advance());
        }

        // Scan the next word
        var secondWord = new StringBuilder();
        while (!IsAtEnd && char.IsLetter(CurrentChar))
        {
            var ch = Advance();
            sb.Append(ch);
            secondWord.Append(ch);
        }

        var fullKeyword = sb.ToString().Replace(" ", "-").ToUpperInvariant();

        if (Keywords.TryGetValue(fullKeyword, out var keywordType))
        {
            return Token.CreateKeyword(keywordType, sb.ToString(), CreateSpan(start));
        }

        // Not a recognized WHEN keyword, backtrack
        _position = savedPosition;
        _line = savedLine;
        _column = savedColumn;
        return null;
    }

    private Token ScanInvalidCharacter()
    {
        var start = CurrentPosition;
        var ch = Advance();
        AddError($"Invalid character: '{ch}'");
        return new Token(TokenType.Invalid, ch.ToString(), CreateSpan(start));
    }

    private bool IsIdentifierChar(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch == '_' || ch == '#' || ch == '$' || ch == '@';
    }

    private bool IsDirectiveStart()
    {
        if (CurrentChar != '#') return false;

        // Check for known directive patterns
        if (MatchText("#IF")) return true;
        if (MatchText("#ELSE")) return true;
        if (MatchText("#THEN")) return true;
        if (MatchText("#END-IF")) return true;
        if (MatchText("#TOOLSREL")) return true;

        return false;
    }

    private bool MatchText(string text)
    {
        if (_position + text.Length > _source.Length)
            return false;

        for (int i = 0; i < text.Length; i++)
        {
            if (char.ToUpperInvariant(_source[_position + i]) != char.ToUpperInvariant(text[i]))
                return false;
        }

        return true;
    }
}

/// <summary>
/// Represents a lexical error
/// </summary>
public class LexError
{
    public string Message { get; }
    public SourcePosition Position { get; }

    public LexError(string message, SourcePosition position)
    {
        Message = message;
        Position = position;
    }

    public override string ToString()
    {
        return $"{Position}: {Message}";
    }
}