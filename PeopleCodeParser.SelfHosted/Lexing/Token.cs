using PeopleCodeParser.SelfHosted;

namespace PeopleCodeParser.SelfHosted.Lexing;

/// <summary>
/// Represents a token in the PeopleCode source
/// </summary>
public class Token
{
    /// <summary>
    /// Token type
    /// </summary>
    public TokenType Type { get; }

    /// <summary>
    /// Token text as it appears in source
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Token value (parsed value for literals, same as Text for others)
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Source location of this token
    /// </summary>
    public SourceSpan SourceSpan { get; }

    /// <summary>
    /// Leading trivia (whitespace, comments, etc.)
    /// </summary>
    public List<Token> LeadingTrivia { get; } = new();

    /// <summary>
    /// Trailing trivia (whitespace, comments, etc.)
    /// </summary>
    public List<Token> TrailingTrivia { get; } = new();

    public Token(TokenType type, string text, SourceSpan sourceSpan, object? value = null)
    {
        Type = type;
        Text = text ?? throw new ArgumentNullException(nameof(text));
        SourceSpan = sourceSpan;
        Value = value ?? text;
    }

    public void AddLeadingTrivia(Token trivia)
    {
        LeadingTrivia.Add(trivia);
    }

    public void AddTrailingTrivia(Token trivia)
    {
        TrailingTrivia.Add(trivia);
    }

    public override string ToString()
    {
        if (Type.IsLiteral())
        {
            return $"{Type}: \"{Text}\" = {Value}";
        }
        return $"{Type}: \"{Text}\"";
    }

    /// <summary>
    /// Create an end-of-file token
    /// </summary>
    public static Token CreateEof(SourcePosition position)
    {
        return new Token(TokenType.EndOfFile, "", new SourceSpan(position, position));
    }

    /// <summary>
    /// Create a keyword token
    /// </summary>
    public static Token CreateKeyword(TokenType type, string text, SourceSpan sourceSpan)
    {
        if (!type.IsKeyword())
            throw new ArgumentException($"Token type {type} is not a keyword", nameof(type));
        
        return new Token(type, text, sourceSpan);
    }

    /// <summary>
    /// Create an operator token
    /// </summary>
    public static Token CreateOperator(TokenType type, string text, SourceSpan sourceSpan)
    {
        if (!type.IsOperator())
            throw new ArgumentException($"Token type {type} is not an operator", nameof(type));
        
        return new Token(type, text, sourceSpan);
    }

    /// <summary>
    /// Create a literal token
    /// </summary>
    public static Token CreateLiteral(TokenType type, string text, SourceSpan sourceSpan, object value)
    {
        if (!type.IsLiteral())
            throw new ArgumentException($"Token type {type} is not a literal", nameof(type));
        
        return new Token(type, text, sourceSpan, value);
    }

    /// <summary>
    /// Create an identifier token
    /// </summary>
    public static Token CreateIdentifier(TokenType type, string text, SourceSpan sourceSpan)
    {
        if (!type.IsIdentifier())
            throw new ArgumentException($"Token type {type} is not an identifier", nameof(type));
        
        return new Token(type, text, sourceSpan);
    }

    /// <summary>
    /// Create a trivia token
    /// </summary>
    public static Token CreateTrivia(TokenType type, string text, SourceSpan sourceSpan)
    {
        if (!type.IsTrivia())
            throw new ArgumentException($"Token type {type} is not trivia", nameof(type));
        
        return new Token(type, text, sourceSpan);
    }
}

/// <summary>
/// All token types in PeopleCode
/// </summary>
public enum TokenType
{
    // Special tokens
    EndOfFile,
    Invalid,

    // Keywords (70+ tokens from ANTLR grammar)
    Abstract,
    Add,                  // +
    Alias,
    And,
    Any,
    Array,
    Array2,
    Array3,
    Array4,
    Array5,
    Array6,
    Array7,
    Array8,
    Array9,
    As,
    Boolean,
    Break,
    Catch,
    Class,
    Component,
    Constant,
    Continue,
    Create,
    Date,
    DateTime,
    Declare,
    Doc,
    Else,
    Error,
    Evaluate,
    Exception,
    Exit,
    Extends,
    False,
    Float,
    For,
    Function,
    EndFunction,
    Get,
    Global,
    If,
    Implements,
    Import,
    Instance,
    Integer,
    Interface,
    Library,
    Local,
    Method,
    Not,
    Null,
    Number,
    Of,
    Or,
    Out,
    PeopleCode,
    Private,
    Property,
    Protected,
    ReadOnly,
    Ref,
    Repeat,
    Return,
    Returns,
    Set,
    Step,
    String,
    Super,
    Then,
    Throw,
    Time,
    To,
    True,
    Try,
    Until,
    Value,
    Warning,
    When,
    WhenOther,
    While,

    // END keywords
    EndClass,
    EndEvaluate,
    EndFor,
    EndGet,
    EndIf,
    EndInterface,
    EndMethod,
    EndSet,
    EndTry,
    EndWhile,

    // Operators and punctuation
    Plus,                 // +
    Minus,                // -
    Star,                 // *
    Div,                  // /
    Power,                // **
    Equal,                // =
    NotEqual,             // <> or !=
    LessThan,             // <
    LessThanOrEqual,      // <=
    GreaterThan,          // >
    GreaterThanOrEqual,   // >=
    Pipe,                 // | (concatenation)
    At,                   // @
    // Assignment operators
    PlusEqual,            // +=
    MinusEqual,           // -=
    PipeEqual,            // |=

    // Punctuation
    LeftParen,            // (
    RightParen,           // )
    LeftBracket,          // [
    RightBracket,         // ]
    Semicolon,            // ;
    Comma,                // ,
    Dot,                  // .
    Colon,                // :

    // Special operators for annotations
    SlashPlus,            // /+
    PlusSlash,            // +/

    // Literals
    IntegerLiteral,
    DecimalLiteral,
    StringLiteral,
    BooleanLiteral,

    // Identifiers
    GenericId,
    GenericIdLimited,
    UserVariable,         // &variable
    SystemVariable,       // %USERID, %DATE, etc.
    SystemConstant,       // %THIS, etc.
    
    // Special identifiers
    Metadata,             // %METADATA

    // Record Events
    RecordEvent,          // FIELDDEFAULT, FIELDEDIT, etc.

    // Trivia
    Whitespace,
    LineComment,          // REM comment ;
    BlockComment,         // /* comment */
    NestedComment,        // <* comment *>
    ApiComment,           // /** API comment */

    // Preprocessor directives
    DirectiveIf,          // #IF
    DirectiveElse,        // #ELSE
    DirectiveEndIf,       // #END IF
    DirectiveThen,        // #THEN
    DirectiveToolsRel,    // #TOOLSREL
    DirectiveAnd,         // && (directive logical AND)
    DirectiveOr,          // || (directive logical OR)
    DirectiveAtom         // Other directive content
}

/// <summary>
/// Extension methods for TokenType
/// </summary>
public static class TokenTypeExtensions
{
    /// <summary>
    /// True if this token type is a keyword
    /// </summary>
    public static bool IsKeyword(this TokenType type)
    {
        return type >= TokenType.Abstract && type <= TokenType.EndWhile;
    }

    /// <summary>
    /// True if this token type is an operator
    /// </summary>
    public static bool IsOperator(this TokenType type)
    {
        return (type >= TokenType.Plus && type <= TokenType.PlusSlash) ||
               type == TokenType.DirectiveAnd ||
               type == TokenType.DirectiveOr;
    }

    /// <summary>
    /// True if this token type is a literal
    /// </summary>
    public static bool IsLiteral(this TokenType type)
    {
        return type == TokenType.Null || 
               (type >= TokenType.IntegerLiteral && type <= TokenType.BooleanLiteral);
    }

    /// <summary>
    /// True if this token type is an identifier
    /// </summary>
    public static bool IsIdentifier(this TokenType type)
    {
        return type >= TokenType.GenericId && type <= TokenType.RecordEvent;
    }

    /// <summary>
    /// True if this token type is trivia (whitespace, comments)
    /// </summary>
    public static bool IsTrivia(this TokenType type)
    {
        return type >= TokenType.Whitespace && type <= TokenType.DirectiveAtom;
    }

    /// <summary>
    /// True if this is an assignment operator
    /// </summary>
    public static bool IsAssignmentOperator(this TokenType type)
    {
        return type is TokenType.Equal or TokenType.PlusEqual or TokenType.MinusEqual or TokenType.PipeEqual;
    }

    /// <summary>
    /// True if this is a comparison operator
    /// </summary>
    public static bool IsComparisonOperator(this TokenType type)
    {
        return type is TokenType.Equal or TokenType.NotEqual or TokenType.LessThan or 
               TokenType.LessThanOrEqual or TokenType.GreaterThan or TokenType.GreaterThanOrEqual;
    }

    /// <summary>
    /// True if this is a binary operator
    /// </summary>
    public static bool IsBinaryOperator(this TokenType type)
    {
        return type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Div or TokenType.Power or
               TokenType.Equal or TokenType.NotEqual or TokenType.LessThan or TokenType.LessThanOrEqual or
               TokenType.GreaterThan or TokenType.GreaterThanOrEqual or TokenType.And or TokenType.Or or TokenType.Pipe;
    }

    /// <summary>
    /// True if this is a unary operator
    /// </summary>
    public static bool IsUnaryOperator(this TokenType type)
    {
        return type is TokenType.Minus or TokenType.Not or TokenType.At;
    }

    /// <summary>
    /// Get the text representation of this token type
    /// </summary>
    public static string GetText(this TokenType type)
    {
        return type switch
        {
            // Keywords
            TokenType.Abstract => "ABSTRACT",
            TokenType.Add => "+",
            TokenType.Alias => "ALIAS",
            TokenType.And => "AND",
            TokenType.Any => "ANY",
            TokenType.Array => "ARRAY",
            TokenType.Array2 => "ARRAY2",
            TokenType.Array3 => "ARRAY3",
            TokenType.Array4 => "ARRAY4",
            TokenType.Array5 => "ARRAY5",
            TokenType.Array6 => "ARRAY6",
            TokenType.Array7 => "ARRAY7",
            TokenType.Array8 => "ARRAY8",
            TokenType.Array9 => "ARRAY9",
            TokenType.As => "AS",
            TokenType.At => "@",
            TokenType.Boolean => "BOOLEAN",
            TokenType.Break => "BREAK",
            TokenType.Catch => "CATCH",
            TokenType.Class => "CLASS",
            TokenType.Component => "COMPONENT",
            TokenType.Constant => "CONSTANT",
            TokenType.Continue => "CONTINUE",
            TokenType.Create => "CREATE",
            TokenType.Date => "DATE",
            TokenType.DateTime => "DATETIME",
            TokenType.Declare => "DECLARE",
            TokenType.Else => "ELSE",
            TokenType.Error => "ERROR",
            TokenType.Evaluate => "EVALUATE",
            TokenType.Exception => "EXCEPTION",
            TokenType.Exit => "EXIT",
            TokenType.Extends => "EXTENDS",
            TokenType.False => "FALSE",
            TokenType.Float => "FLOAT",
            TokenType.For => "FOR",
            TokenType.Function => "FUNCTION",
            TokenType.EndFunction => "END FUNCTION",
            TokenType.Get => "GET",
            TokenType.Global => "GLOBAL",
            TokenType.If => "IF",
            TokenType.Implements => "IMPLEMENTS",
            TokenType.Import => "IMPORT",
            TokenType.Instance => "INSTANCE",
            TokenType.Integer => "INTEGER",
            TokenType.Interface => "INTERFACE",
            TokenType.Library => "LIBRARY",
            TokenType.Local => "LOCAL",
            TokenType.Method => "METHOD",
            TokenType.Not => "NOT",
            TokenType.Null => "NULL",
            TokenType.Number => "NUMBER",
            TokenType.Of => "OF",
            TokenType.Or => "OR",
            TokenType.Out => "OUT",
            TokenType.PeopleCode => "PEOPLECODE",
            TokenType.Private => "PRIVATE",
            TokenType.Property => "PROPERTY",
            TokenType.Protected => "PROTECTED",
            TokenType.ReadOnly => "READONLY",
            TokenType.Ref => "REF",
            TokenType.Repeat => "REPEAT",
            TokenType.Return => "RETURN",
            TokenType.Returns => "RETURNS",
            TokenType.Set => "SET",
            TokenType.Step => "STEP",
            TokenType.String => "STRING",
            TokenType.Super => "%SUPER",
            TokenType.Then => "THEN",
            TokenType.Throw => "THROW",
            TokenType.Time => "TIME",
            TokenType.To => "TO",
            TokenType.True => "TRUE",
            TokenType.Try => "TRY",
            TokenType.Until => "UNTIL",
            TokenType.Value => "VALUE",
            TokenType.Warning => "WARNING",
            TokenType.When => "WHEN",
            TokenType.WhenOther => "WHEN-OTHER",
            TokenType.While => "WHILE",

            // END keywords
            TokenType.EndClass => "END-CLASS",
            TokenType.EndEvaluate => "END-EVALUATE",
            TokenType.EndFor => "END-FOR",
            TokenType.EndGet => "END-GET",
            TokenType.EndIf => "END-IF",
            TokenType.EndInterface => "END-INTERFACE",
            TokenType.EndMethod => "END-METHOD",
            TokenType.EndSet => "END-SET",
            TokenType.EndTry => "END-TRY",
            TokenType.EndWhile => "END-WHILE",

            // Operators
            TokenType.Plus => "+",
            TokenType.Minus => "-",
            TokenType.Star => "*",
            TokenType.Div => "/",
            TokenType.Power => "**",
            TokenType.Equal => "=",
            TokenType.NotEqual => "<>",
            TokenType.LessThan => "<",
            TokenType.LessThanOrEqual => "<=",
            TokenType.GreaterThan => ">",
            TokenType.GreaterThanOrEqual => ">=",
            TokenType.Pipe => "|",
            TokenType.PlusEqual => "+=",
            TokenType.MinusEqual => "-=",
            TokenType.PipeEqual => "|=",

            // Punctuation
            TokenType.LeftParen => "(",
            TokenType.RightParen => ")",
            TokenType.LeftBracket => "[",
            TokenType.RightBracket => "]",
            TokenType.Semicolon => ";",
            TokenType.Comma => ",",
            TokenType.Dot => ".",
            TokenType.Colon => ":",
            TokenType.SlashPlus => "/+",
            TokenType.PlusSlash => "+/",

            // Directive operators
            TokenType.DirectiveAnd => "&&",
            TokenType.DirectiveOr => "||",

            // Special
            TokenType.EndOfFile => "<EOF>",
            TokenType.Invalid => "<INVALID>",

            _ => type.ToString().ToUpper()
        };
    }

    /// <summary>
    /// Get the operator precedence for this token type
    /// </summary>
    public static int GetPrecedence(this TokenType type)
    {
        return type switch
        {
            TokenType.Or => 1,
            TokenType.And => 2,
            TokenType.Equal or TokenType.NotEqual => 3,
            TokenType.LessThan or TokenType.LessThanOrEqual or 
            TokenType.GreaterThan or TokenType.GreaterThanOrEqual => 4,
            TokenType.Pipe => 5,
            TokenType.Plus or TokenType.Minus => 6,
            TokenType.Star or TokenType.Div => 7,
            TokenType.Power => 8,
            _ => 0
        };
    }

    /// <summary>
    /// True if this operator is right-associative
    /// </summary>
    public static bool IsRightAssociative(this TokenType type)
    {
        return type is TokenType.Power;
    }
}