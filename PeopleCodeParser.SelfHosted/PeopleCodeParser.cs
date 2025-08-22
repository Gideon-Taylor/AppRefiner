using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Diagnostics;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Self-hosted recursive descent parser for PeopleCode with advanced error recovery.
/// Designed to handle incomplete and malformed code during live editing.
/// </summary>
public class PeopleCodeParser
{
    private readonly List<Token> _tokens;
    private int _position;
    private readonly List<ParseError> _errors = new();
    private readonly Stack<string> _ruleStack = new(); // For debugging and error context
    
    // Statement counter for tracking statement execution order
    private int _statementCounter = 0;

    // Error recovery settings
    private const int MaxErrorRecoveryAttempts = 10;
    private int _errorRecoveryCount = 0;

    // Synchronization tokens for error recovery
    private static readonly HashSet<TokenType> StatementSyncTokens = new()
    {
        TokenType.Semicolon,
        TokenType.If,
        TokenType.For,
        TokenType.While,
        TokenType.Repeat,
        TokenType.Try,
        TokenType.Return,
        TokenType.Break,
        TokenType.Continue,
        TokenType.Exit,
        TokenType.Evaluate,
        TokenType.Local,
        TokenType.Global,
        TokenType.Component,
        TokenType.EndIf,
        TokenType.EndFor,
        TokenType.EndWhile,
        TokenType.EndTry,
        TokenType.EndEvaluate
    };

    private static readonly HashSet<TokenType> BlockSyncTokens = new()
    {
        TokenType.EndFunction,
        TokenType.EndMethod,
        TokenType.EndGet,
        TokenType.EndSet,
        TokenType.EndClass,
        TokenType.EndInterface
    };

    public PeopleCodeParser(IEnumerable<Token> tokens)
    {
        _tokens = tokens?.Where(t => !t.Type.IsTrivia()).ToList() 
                 ?? throw new ArgumentNullException(nameof(tokens));
        _position = 0;
    }

    /// <summary>
    /// Parse errors encountered during parsing
    /// </summary>
    public IReadOnlyList<ParseError> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Current token being processed
    /// </summary>
    private Token Current => _position < _tokens.Count ? _tokens[_position] : 
                           Token.CreateEof(new SourcePosition(_tokens.LastOrDefault()?.SourceSpan.End.Index ?? 0));

    /// <summary>
    /// Look ahead at the next token without consuming it
    /// </summary>
    private Token Peek(int offset = 1) => 
        _position + offset < _tokens.Count ? _tokens[_position + offset] : 
        Token.CreateEof(new SourcePosition(_tokens.LastOrDefault()?.SourceSpan.End.Index ?? 0));

    /// <summary>
    /// Check if current token matches expected type
    /// </summary>
    private bool Check(TokenType expected) => Current.Type == expected;

    /// <summary>
    /// Check if current token matches any of the expected types
    /// </summary>
    private bool Check(params TokenType[] expected) => expected.Contains(Current.Type);

    /// <summary>
    /// Consume current token if it matches expected type
    /// </summary>
    private bool Match(TokenType expected)
    {
        if (Check(expected))
        {
            _position++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Consume current token if it matches any expected type
    /// </summary>
    private bool Match(params TokenType[] expected)
    {
        if (Check(expected))
        {
            _position++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Consume expected token or report error
    /// </summary>
    private Token Consume(TokenType expected, string message)
    {
        if (Check(expected))
        {
            var token = Current;
            _position++;
            return token;
        }

        ReportError(message, Current.SourceSpan);
        return CreateMissingToken(expected);
    }

    /// <summary>
    /// Check if we're at the end of the token stream
    /// </summary>
    private bool IsAtEnd => Current.Type == TokenType.EndOfFile;

    /// <summary>
    /// Create a synthetic token for error recovery
    /// </summary>
    private Token CreateMissingToken(TokenType type)
    {
        var position = Current.SourceSpan.Start;
        return new Token(type, type.GetText(), new SourceSpan(position, position));
    }

    /// <summary>
    /// Report a parse error
    /// </summary>
    private void ReportError(string message, SourceSpan? location = null)
    {
        location ??= Current.SourceSpan;
        var context = _ruleStack.Count > 0 ? string.Join(" -> ", _ruleStack.Reverse()) : "unknown";
        
        _errors.Add(new ParseError(
            message,
            location.Value,
            ParseErrorSeverity.Error,
            context
        ));

        Debug.WriteLine($"Parse Error at {location}: {message} (Context: {context})");
    }

    /// <summary>
    /// Report a parse warning
    /// </summary>
    private void ReportWarning(string message, SourceSpan? location = null)
    {
        location ??= Current.SourceSpan;
        var context = _ruleStack.Count > 0 ? string.Join(" -> ", _ruleStack.Reverse()) : "unknown";
        
        _errors.Add(new ParseError(
            message,
            location.Value,
            ParseErrorSeverity.Warning,
            context
        ));
    }

    /// <summary>
    /// Enter a parsing rule for debugging and error context
    /// </summary>
    private void EnterRule(string ruleName)
    {
        _ruleStack.Push(ruleName);
    }

    /// <summary>
    /// Exit a parsing rule
    /// </summary>
    private void ExitRule()
    {
        if (_ruleStack.Count > 0)
            _ruleStack.Pop();
    }

    /// <summary>
    /// Perform panic mode recovery by skipping tokens until a synchronization point
    /// </summary>
    private void PanicRecover(HashSet<TokenType> syncTokens)
    {
        if (_errorRecoveryCount >= MaxErrorRecoveryAttempts)
        {
            ReportError("Too many parse errors, stopping recovery attempts");
            return;
        }

        _errorRecoveryCount++;

        // Skip tokens until we find a synchronization point
        int tokensSkipped = 0;
        while (!IsAtEnd && !syncTokens.Contains(Current.Type))
        {
            _position++;
            tokensSkipped++;

            // Prevent infinite loops
            if (tokensSkipped > 100)
            {
                ReportError("Recovery failed: skipped too many tokens");
                break;
            }
        }

        if (tokensSkipped > 0)
        {
            ReportWarning($"Skipped {tokensSkipped} tokens during error recovery");
        }
    }

    /// <summary>
    /// Synchronize to a specific token for targeted error recovery
    /// </summary>
    /// <param name="targetToken">The token to synchronize to</param>
    /// <returns>True if the target token was found, false otherwise</returns>
    private bool SynchronizeToToken(TokenType targetToken)
    {
        if (_errorRecoveryCount >= MaxErrorRecoveryAttempts)
        {
            ReportError("Too many parse errors, stopping recovery attempts");
            return false;
        }

        _errorRecoveryCount++;

        // Skip tokens until we find the target token
        int tokensSkipped = 0;
        while (!IsAtEnd && Current.Type != targetToken)
        {
            _position++;
            tokensSkipped++;

            // Prevent infinite loops
            if (tokensSkipped > 50)
            {
                ReportError($"Recovery failed: could not find '{targetToken}' token");
                return false;
            }
        }

        if (Current.Type == targetToken)
        {
            if (tokensSkipped > 0)
            {
                ReportWarning($"Skipped {tokensSkipped} tokens to synchronize to '{targetToken}'");
            }
            return true;
        }

        return false; // End of input reached without finding target
    }

    /// <summary>
    /// Main entry point: Parse a complete PeopleCode program according to ANTLR grammar:
    /// program: appClass | importsBlock programPreambles? SEMI* statements? SEMI* EOF
    /// 
    /// Where appClass: importsBlock classDeclaration (SEMI+ classExternalDeclaration)* (SEMI* classBody)? SEMI* EOF  #AppClassProgram
    ///              | importsBlock interfaceDeclaration SEMI* EOF                                                    #InterfaceProgram
    /// </summary>
    public ProgramNode ParseProgram()
    {
        try
        {
            EnterRule("program");
            _errorRecoveryCount = 0;
            _statementCounter = 0; // Reset statement counter for a new program

            var program = new ProgramNode();

            // Parse imports block first
            while (Check(TokenType.Import) && !IsAtEnd)
            {
                var import = ParseImport();
                if (import != null)
                    program.AddImport(import);
            }

            // Check if this is an appClass program or a regular program
            if (Check(TokenType.Class))
            {
                // This is an AppClassProgram
                var appClass = ParseAppClass();
                if (appClass != null)
                {
                    program.SetAppClass(appClass);
                }
                return program;
            }
            else if (Check(TokenType.Interface))
            {
                // This is an InterfaceProgram
                var interfaceNode = ParseInterface();
                if (interfaceNode != null)
                {
                    program.SetInterface(interfaceNode);
                }
                
                // Consume any trailing semicolons
                while (Match(TokenType.Semicolon)) { }
                
                return program;
            }

            // This is a regular program: programPreambles? SEMI* statements? SEMI* EOF
            try
            {
                // Parse optional program preambles (functions, variables, constants)
                ParseProgramPreambles(program);

                // Parse optional semicolons
                while (Match(TokenType.Semicolon)) { }

                // Parse optional statements
                if (!IsAtEnd && !Check(TokenType.EndOfFile))
                {
                    if (program.MainBlock == null)
                        program.SetMainBlock(new BlockNode());

                    while (!IsAtEnd && !Check(TokenType.EndOfFile))
                    {
                        var statement = ParseStatement();
                        if (statement != null)
                        {
                            program.MainBlock?.AddStatement(statement);
                        }
                        else
                        {
                            // If we couldn't parse a statement, skip the current token to prevent infinite loop
                            ReportError($"Unexpected token: {Current.Type}");
                            _position++;
                        }

                        // Handle optional semicolons between statements
                        while (Match(TokenType.Semicolon)) { }
                    }
                }

                // Parse final optional semicolons before EOF
                while (Match(TokenType.Semicolon)) { }

                return program;
            }
            catch (Exception ex)
            {
                ReportError($"Unexpected error in program parsing: {ex.Message}");
                PanicRecover(StatementSyncTokens.Union(BlockSyncTokens).ToHashSet());
                return program;
            }
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse program preambles according to ANTLR grammar:
    /// programPreambles: programPreamble (SEMI+ programPreamble)*
    /// </summary>
    private void ParseProgramPreambles(ProgramNode program)
    {
        try
        {
            EnterRule("programPreambles");

            // Check if we have any preamble constructs
            while (!IsAtEnd && IsProgramPreambleToken())
            {
                try
                {
                    if (Check(TokenType.Function, TokenType.PeopleCode, TokenType.Library, TokenType.Declare))
                    {
                        // This could be either a function declaration or a function definition
                        // ParseFunction() handles both cases correctly
                        var function = ParseFunction();
                        if (function != null)
                        {
                            program.AddFunction(function);
                            
                            // If this was a function definition (with body), we don't expect a semicolon
                            // as ParseFunction() already consumed the optional semicolon after END-FUNCTION
                            if (function.Body != null)
                            {
                                continue; // Skip the semicolon check for function definitions
                            }
                        }
                    }
                    else if (Check(TokenType.Global, TokenType.Component))
                    {
                        var variable = ParseVariableDeclaration();
                        if (variable != null)
                            program.AddVariable(variable);
                    }
                    else if (Check(TokenType.Constant))
                    {
                        var constant = ParseConstantDeclaration();
                        if (constant != null)
                            program.AddConstant(constant);
                    }
                    else if (Check(TokenType.Local))
                    {
                        // Local variable definition as preamble
                        var localVar = ParseVariableDeclaration();
                        if (localVar != null)
                            program.AddVariable(localVar);
                    }
                    else
                    {
                        break; // No more preamble items
                    }

                    // Consume required semicolons after preamble item
                    // (except for function definitions, which were handled above)
                    if (!Match(TokenType.Semicolon))
                    {
                        break; // No semicolon means we're done with preambles
                    }
                    
                    // Consume any additional semicolons
                    while (Match(TokenType.Semicolon)) { }
                }
                catch (Exception ex)
                {
                    ReportError($"Error parsing program preamble: {ex.Message}");
                    PanicRecover(StatementSyncTokens);
                }
            }
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Check if current token can start a program preamble
    /// </summary>
    private bool IsProgramPreambleToken()
    {
        return Check(TokenType.Function, TokenType.PeopleCode, TokenType.Library, TokenType.Declare,
                    TokenType.Global, TokenType.Component, TokenType.Constant, TokenType.Local);
    }

    /// <summary>
    /// Parse import declaration: IMPORT package:path:*;
    /// </summary>
    private ImportNode? ParseImport()
    {
        try
        {
            EnterRule("import");

            if (!Match(TokenType.Import))
                return null;

            // Parse import path (expecting package:path format)
            var pathParts = new List<string>();
            bool consumedColon = false;

            // First segment: METADATA or generic identifier
            if (Check(TokenType.Metadata) || Check(TokenType.GenericId))
            {
                pathParts.Add(Current.Text);
                _position++;

                // Parse subsequent segments separated by colons
                while (Match(TokenType.Colon))
                {
                    consumedColon = true;

                    if (Check(TokenType.GenericId))
                    {
                        pathParts.Add(Current.Text);
                        _position++;
                    }
                    else if (Check(TokenType.Star))
                    {
                        // Wildcard import terminator (appPackageAll)
                        pathParts.Add(Current.Text);
                        _position++;
                        break;
                    }
                    else
                    {
                        ReportError("Expected class name or '*' after ':'");
                        break;
                    }
                }
            }
            else
            {
                ReportError("Expected package path after 'IMPORT'");
            }

            // Require at least one semicolon, allow additional semicolons (SEMI+ per grammar)
            if (!Match(TokenType.Semicolon))
            {
                ReportError("Expected ';' after import declaration");
            }
            while (Match(TokenType.Semicolon)) { }

            // Grammar requires at least one colon and at least two parts (package:class or package:*)
            if (consumedColon && pathParts.Count >= 2)
            {
                return new ImportNode(string.Join(":", pathParts));
            }

            return null;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing import: {ex.Message}");
            PanicRecover(StatementSyncTokens);
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse application class definition according to ANTLR grammar:
    /// appClass: importsBlock classDeclaration (SEMI+ classExternalDeclaration)* (SEMI* classBody)? SEMI* EOF
    /// </summary>
    private AppClassNode? ParseAppClass()
    {
        try
        {
            EnterRule("appClass");

            if (!Check(TokenType.Class))
            {
                ReportError("Expected 'CLASS' keyword");
                return null;
            }

            // Parse the class declaration (CLASS name [EXTENDS|IMPLEMENTS] ... END-CLASS)
            var classDeclaration = ParseClassDeclaration();
            if (classDeclaration == null)
                return null;

            // Parse external declarations (functions and variables between class and body)
            while (!IsAtEnd && Check(TokenType.Semicolon))
            {
                // Consume semicolons
                while (Match(TokenType.Semicolon)) { }

                // Check for external declarations
                if (Check(TokenType.Function, TokenType.Declare))
                {
                    var function = ParseFunction();
                    if (function != null)
                    {
                        classDeclaration.AddMember(function, VisibilityModifier.Public);
                    }
                }
                else if (Check(TokenType.Component, TokenType.Global))
                {
                    var variable = ParseVariableDeclaration();
                    if (variable != null)
                    {
                        classDeclaration.AddMember(variable, VisibilityModifier.Public);
                    }
                }
                else
                {
                    break; // No more external declarations
                }
            }

            // Parse class body (method implementations)
            // Optional semicolons before class body
            while (Match(TokenType.Semicolon)) { }

            if (!IsAtEnd && !Check(TokenType.EndOfFile))
            {
                var classBody = ParseClassBody();
                if (classBody != null)
                {
                    // Add method implementations to the class
                    foreach (var member in classBody)
                    {
                        classDeclaration.AddMember(member, VisibilityModifier.Public);
                    }
                }
            }

            // Consume any trailing semicolons
            while (Match(TokenType.Semicolon)) { }

            return classDeclaration;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing application class: {ex.Message}");
            PanicRecover(BlockSyncTokens);
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse class declaration according to ANTLR grammar:
    /// classDeclaration: CLASS genericID EXTENDS superclass SEMI* classHeader END_CLASS        #ClassDeclarationExtension
    ///                 | CLASS genericID IMPLEMENTS appClassPath SEMI* classHeader END_CLASS   #ClassDeclarationImplementation  
    ///                 | CLASS genericID SEMI* classHeader END_CLASS                           #ClassDeclarationPlain
    /// </summary>
    private AppClassNode? ParseClassDeclaration()
    {
        try
        {
            EnterRule("classDeclaration");

            if (!Match(TokenType.Class))
            {
                ReportError("Expected 'CLASS' keyword");
                return null;
            }

            // Parse class name
            var className = ParseGenericId();
            if (className == null)
            {
                ReportError("Expected class name after 'CLASS'");
                return null;
            }

            var classNode = new AppClassNode(className);

            // Check for EXTENDS or IMPLEMENTS clause
            if (Match(TokenType.Extends))
            {
                var superclass = ParseSuperclass();
                if (superclass != null)
                {
                    classNode.SetBaseClass(superclass);
                }
                else
                {
                    ReportError("Expected superclass after 'EXTENDS'");
                }
            }
            else if (Match(TokenType.Implements))
            {
                var interfaceType = ParseAppClassPath();
                if (interfaceType != null)
                {
                    classNode.SetImplementedInterface(interfaceType);
                }
                else
                {
                    ReportError("Expected interface path after 'IMPLEMENTS'");
                }
            }

            // Optional semicolons before class header
            while (Match(TokenType.Semicolon)) { }

            // Parse class header (public/protected/private sections)
            ParseClassHeader(classNode);

            // Expect END-CLASS
            Consume(TokenType.EndClass, "Expected 'END-CLASS' after class definition");

            return classNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing class declaration: {ex.Message}");
            PanicRecover(new HashSet<TokenType> { TokenType.EndClass });
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse class header according to ANTLR grammar:
    /// classHeader: publicHeader? (PROTECTED SEMI* protectedHeader?)? (PRIVATE SEMI* privateHeader?)?
    /// </summary>
    private void ParseClassHeader(AppClassNode classNode)
    {
        try
        {
            EnterRule("classHeader");

            // Parse public section (default, no keyword needed)
            ParseVisibilitySection(classNode, VisibilityModifier.Public);

            // Parse protected section if present
            if (Match(TokenType.Protected))
            {
                while (Match(TokenType.Semicolon)) { } // Optional semicolons
                ParseVisibilitySection(classNode, VisibilityModifier.Protected);
            }

            // Parse private section if present
            if (Match(TokenType.Private))
            {
                while (Match(TokenType.Semicolon)) { } // Optional semicolons
                ParseVisibilitySection(classNode, VisibilityModifier.Private);
            }
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing class header: {ex.Message}");
            PanicRecover(new HashSet<TokenType> { TokenType.EndClass, TokenType.Protected, TokenType.Private });
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse a visibility section (public, protected, or private members)
    /// </summary>
    private void ParseVisibilitySection(AppClassNode classNode, VisibilityModifier visibility)
    {
        try
        {
            EnterRule($"{visibility}Section");

            // Continue parsing members until we hit an end token or different visibility
            while (!IsAtEnd && 
                   !Check(TokenType.EndClass) && 
                   !Check(TokenType.Protected, TokenType.Private))
            {
                try
                {
                    AstNode? member = null;

                    // Parse members based on visibility section according to grammar:
                    // nonPrivateMember: methodHeader | propertyDeclaration
                    // privateMember: methodHeader | instanceDeclaration | constantDeclaration
                    if (Check(TokenType.Method))
                    {
                        // Method headers are allowed in all visibility sections
                        member = ParseMethodHeader(visibility);
                    }
                    else if (Check(TokenType.Property))
                    {
                        // Property declarations are allowed in all visibility sections
                        member = ParsePropertyDeclaration();
                    }
                    else if (Check(TokenType.Instance))
                    {
                        // Instance declarations are only allowed in private section
                        if (visibility == VisibilityModifier.Private)
                        {
                            member = ParseInstanceDeclaration();
                        }
                        else
                        {
                            ReportError($"Instance declarations are only allowed in PRIVATE section, not in {visibility} section");
                            // Skip this token to avoid infinite loop
                            _position++;
                            continue;
                        }
                    }
                    else if (Check(TokenType.Constant))
                    {
                        // Constant declarations are only allowed in private section
                        if (visibility == VisibilityModifier.Private)
                        {
                            member = ParseConstantDeclaration();
                        }
                        else
                        {
                            ReportError($"Constant declarations are only allowed in PRIVATE section, not in {visibility} section");
                            // Skip this token to avoid infinite loop
                            _position++;
                            continue;
                        }
                    }
                    else if (Check(TokenType.Semicolon))
                    {
                        // Skip semicolons
                        while (Match(TokenType.Semicolon)) { }
                        continue;
                    }
                    else
                    {
                        // Unknown member type or reached end of section
                        break;
                    }

                    if (member != null)
                    {
                        classNode.AddMember(member, visibility);
                    }

                    // Consume required semicolons after member
                    if (!Match(TokenType.Semicolon))
                    {
                        ReportError("Expected ';' after class member");
                    }
                    // Consume any additional semicolons
                    while (Match(TokenType.Semicolon)) { }
                }
                catch (Exception ex)
                {
                    ReportError($"Error parsing class member: {ex.Message}");
                    // Skip to next semicolon or section boundary
                    while (!IsAtEnd && 
                           !Check(TokenType.Semicolon) && 
                           !Check(TokenType.Protected, TokenType.Private, TokenType.EndClass))
                    {
                        _position++;
                    }
                    Match(TokenType.Semicolon); // Consume semicolon if present
                }
            }
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse superclass according to ANTLR grammar:
    /// superclass: EXCEPTION          #ExceptionSuperClass
    ///           | appClassPath        #AppClassSuperClass  
    ///           | simpleType          #SimpleTypeSuperclass
    /// </summary>
    private TypeNode? ParseSuperclass()
    {
        try
        {
            EnterRule("superclass");

            if (Match(TokenType.Exception))
            {
                // Special built-in exception type
                return new BuiltInTypeNode(BuiltInType.Exception)
                {
                    SourceSpan = Current.SourceSpan
                };
            }
            else if (Current.Type.IsIdentifier() || Check(TokenType.GenericId))
            {
                // Could be an app class path or simple type
                // Try to parse as app class path first (with colons)
                var appClassPath = ParseAppClassPath();
                if (appClassPath != null)
                {
                    return appClassPath;
                }

                // Fall back to simple type
                return ParseSimpleType();
            }
            else
            {
                ReportError("Expected superclass type after 'EXTENDS'");
                return null;
            }
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing superclass: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse simple type according to ANTLR grammar:
    /// simpleType: builtInType         #SimpleBuiltInType
    ///           | GENERIC_ID_LIMITED  #SimpleGenericID
    /// </summary>
    private TypeNode? ParseSimpleType()
    {
        try
        {
            EnterRule("simpleType");

            // Check for built-in types
            var builtInType = TryParseBuiltInType();
            if (builtInType != null)
            {
                return builtInType;
            }

            // Check for generic identifier
            if (Check(TokenType.GenericId, TokenType.GenericIdLimited))
            {
                var typeName = Current.Text;
                var token = Current;
                _position++;
                
                // Could be a simple class name without package
                return new AppClassTypeNode(typeName)
                {
                    SourceSpan = token.SourceSpan
                };
            }

            ReportError("Expected type name");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Try to parse a built-in type keyword (primitive or object type)
    /// </summary>
    private BuiltInTypeNode? TryParseBuiltInType()
    {
        // First check for primitive type tokens
        var builtInType = Current.Type switch
        {
            TokenType.Any => BuiltInType.Any,
            TokenType.Boolean => BuiltInType.Boolean,
            TokenType.Date => BuiltInType.Date,
            TokenType.DateTime => BuiltInType.DateTime,
            TokenType.Exception => BuiltInType.Exception,
            TokenType.Float => BuiltInType.Float,
            TokenType.Integer => BuiltInType.Integer,
            TokenType.Number => BuiltInType.Number,
            TokenType.String => BuiltInType.String,
            TokenType.Time => BuiltInType.Time,
            _ => (BuiltInType?)null
        };

        if (builtInType.HasValue)
        {
            var token = Current;
            _position++;
            return new BuiltInTypeNode(builtInType.Value)
            {
                SourceSpan = token.SourceSpan
            };
        }

        // Check if current token is a generic identifier that might be a built-in object type
        if (Check(TokenType.GenericId, TokenType.GenericIdLimited))
        {
            var parsedType = BuiltInTypeExtensions.TryParseKeyword(Current.Text);
            if (parsedType.HasValue)
            {
                var token = Current;
                _position++;
                return new BuiltInTypeNode(parsedType.Value)
                {
                    SourceSpan = token.SourceSpan
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Parse type specifier for type casting: (appClassPath | genericID)
    /// Used in expressions like: expr AS MyPackage:MyClass or expr AS String
    /// </summary>
    private TypeNode? ParseTypeSpecifier()
    {
        try
        {
            EnterRule("typeSpecifier");

            // First try to parse as app class path (qualified name with colons)
            var appClassType = ParseAppClassPath();
            if (appClassType != null)
            {
                return appClassType;
            }

            // If not an app class path, try to parse as simple type (built-in or unqualified class name)
            var simpleType = ParseSimpleType();
            if (simpleType != null)
            {
                return simpleType;
            }

            ReportError("Expected type name after 'AS'");
            return null;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing type specifier: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse method header according to ANTLR grammar:
    /// methodHeader: METHOD genericID LPAREN methodArguments? RPAREN (RETURNS typeT)? ABSTRACT?
    /// </summary>
    private MethodNode? ParseMethodHeader(VisibilityModifier visibility)
    {
        try
        {
            EnterRule("methodHeader");

            if (!Match(TokenType.Method))
            {
                ReportError("Expected 'METHOD' keyword");
                return null;
            }

            // Parse method name
            var methodName = ParseGenericId();
            if (methodName == null)
            {
                ReportError("Expected method name after 'METHOD'");
                return null;
            }

            var methodNode = new MethodNode(methodName) { Visibility = visibility };

            // Parse parameter list
            if (!Match(TokenType.LeftParen))
            {
                ReportError("Expected '(' after method name");
            }
            else
            {
                // Parse method arguments
                if (!Check(TokenType.RightParen))
                {
                    ParseMethodArguments(methodNode);
                }

                Consume(TokenType.RightParen, "Expected ')' after method parameters");
            }

            // Parse optional return type
            if (Match(TokenType.Returns))
            {
                var returnType = ParseTypeReference();
                if (returnType != null)
                {
                    methodNode.SetReturnType(returnType);
                }
                else
                {
                    ReportError("Expected return type after 'RETURNS'");
                }
            }

            // Parse optional ABSTRACT modifier
            if (Match(TokenType.Abstract))
            {
                methodNode.IsAbstract = true;
            }

            return methodNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing method header: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse method arguments: methodArgument (COMMA methodArgument)* COMMA?
    /// </summary>
    private void ParseMethodArguments(MethodNode methodNode)
    {
        try
        {
            EnterRule("methodArguments");

            // Parse first parameter
            var parameter = ParseMethodArgument();
            if (parameter != null)
            {
                methodNode.AddParameter(parameter);
            }

            // Parse additional parameters separated by commas
            while (Match(TokenType.Comma))
            {
                // Check if this is a trailing comma (next token is right paren)
                if (Check(TokenType.RightParen))
                {
                    // This is a trailing comma, which is allowed by the grammar
                    break;
                }

                parameter = ParseMethodArgument();
                if (parameter != null)
                {
                    methodNode.AddParameter(parameter);
                }
                else
                {
                    // If parameter parsing failed, break to avoid infinite loop
                    break;
                }
            }
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse single method argument: USER_VARIABLE AS typeT OUT?
    /// </summary>
    private ParameterNode? ParseMethodArgument()
    {
        try
        {
            EnterRule("methodArgument");

            // Parse parameter name (must be a user variable &name)
            if (!Check(TokenType.UserVariable))
            {
                ReportError("Expected parameter name (&variable)");
                return null;
            }

            var paramName = Current.Text;
            _position++;

            TypeNode? paramType = new BuiltInTypeNode(BuiltInType.Any)
            {
                SourceSpan = Current.SourceSpan
            }; // Default to ANY if no type specified

            // Optional AS typeT
            if (Match(TokenType.As))
            {
                // Parse parameter type
                paramType = ParseTypeReference();
                if (paramType == null)
                {
                    ReportError("Expected parameter type after 'AS'");
                    // Use default ANY type if we couldn't parse the specified type
                    paramType = new BuiltInTypeNode(BuiltInType.Any)
                    {
                        SourceSpan = Current.SourceSpan
                    };
                }
            }

            var parameter = new ParameterNode(paramName, paramType);

            // Parse optional OUT modifier
            if (Match(TokenType.Out))
            {
                parameter.IsOut = true;
            }

            return parameter;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse property declaration according to ANTLR grammar:
    /// propertyDeclaration: PROPERTY typeT genericID GET SET?           #PropertyGetSet
    ///                    | PROPERTY typeT genericID ABSTRACT? READONLY? #PropertyDirect
    /// </summary>
    private PropertyNode? ParsePropertyDeclaration()
    {
        try
        {
            EnterRule("propertyDeclaration");

            if (!Match(TokenType.Property))
            {
                ReportError("Expected 'PROPERTY' keyword");
                return null;
            }

            // Parse property type
            var propertyType = ParseTypeReference();
            if (propertyType == null)
            {
                ReportError("Expected property type after 'PROPERTY'");
                return null;
            }

            // Parse property name
            var propertyName = ParseGenericId();
            if (propertyName == null)
            {
                ReportError("Expected property name");
                return null;
            }

            var propertyNode = new PropertyNode(propertyName, propertyType);

            // Parse property modifiers
            if (Match(TokenType.Get))
            {
                propertyNode.HasGet = true;
                
                if (Match(TokenType.Set))
                {
                    propertyNode.HasSet = true;
                }
                else
                {
                    propertyNode.HasSet = false;
                }
            }
            else
            {
                // Check for ABSTRACT and READONLY modifiers
                if (Match(TokenType.Abstract))
                {
                    propertyNode.IsAbstract = true;
                }

                if (Match(TokenType.ReadOnly))
                {
                    propertyNode.IsReadOnly = true;
                    propertyNode.HasSet = false;
                }
            }

            return propertyNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing property declaration: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse instance declaration according to ANTLR grammar:
    /// instanceDeclaration: INSTANCE typeT USER_VARIABLE (COMMA USER_VARIABLE)* COMMA? #InstanceDecl
    ///                    | INSTANCE typeT                                             #EmptyInstanceDecl
    /// </summary>
    private VariableNode? ParseInstanceDeclaration()
    {
        try
        {
            EnterRule("instanceDeclaration");

            if (!Match(TokenType.Instance))
            {
                ReportError("Expected 'INSTANCE' keyword");
                return null;
            }

            // Parse variable type
            var variableType = ParseTypeReference();
            if (variableType == null)
            {
                ReportError("Expected variable type after 'INSTANCE'");
                return null;
            }

            // Parse first variable name (optional - empty instance declaration is allowed)
            if (!Check(TokenType.UserVariable))
            {
                // Empty instance declaration - just return null, it's valid but meaningless
                return null;
            }

            var firstVarName = Current.Text;
            _position++;

            var variableNode = new VariableNode(firstVarName, variableType, VariableScope.Instance);

            // Parse additional variable names separated by commas
            while (Match(TokenType.Comma))
            {
                if (Check(TokenType.UserVariable))
                {
                    variableNode.AddName(Current.Text);
                    _position++;
                }
                else
                {
                    break; // Trailing comma is allowed
                }
            }

            return variableNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing instance declaration: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse type reference according to ANTLR grammar:
    /// typeT: ARRAY (OF ARRAY)* (OF typeT)?  #ArrayType
    ///      | EXCEPTION                      #BaseExceptionType  
    ///      | appClassPath                   #AppClassType
    ///      | simpleType                     #SimpleTypeType
    /// </summary>
    private TypeNode? ParseTypeReference()
    {
        try
        {
            EnterRule("typeT");

            // Check for array types (only ARRAY token in normal type references)
            if (Check(TokenType.Array))
            {
                return ParseArrayType();
            }

            // Check for EXCEPTION type
            if (Match(TokenType.Exception))
            {
                return new BuiltInTypeNode(BuiltInType.Exception)
                {
                    SourceSpan = Current.SourceSpan
                };
            }

            // Try to parse as app class path or simple type
            var result = ParseAppClassPath();
            if (result != null)
            {
                return result;
            }

            return ParseSimpleType();
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing type reference: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse array type for normal type references according to ANTLR grammar:
    /// ARRAY (OF ARRAY)* (OF typeT)?
    /// Examples: ARRAY, ARRAY OF STRING, ARRAY OF ARRAY OF INTEGER
    /// </summary>
    private ArrayTypeNode? ParseArrayType()
    {
        try
        {
            EnterRule("arrayType");

            // Must start with single ARRAY token (not ARRAY2-ARRAY9)
            if (!Match(TokenType.Array))
            {
                ReportError("Expected 'ARRAY' token");
                return null;
            }

            var dimensions = 1;
            var startToken = Current;

            // Parse additional dimensions: (OF ARRAY)*
            // Key fix: Use lookahead to distinguish between dimension building and element type
            while (Check(TokenType.Of) && Peek().Type == TokenType.Array)
            {
                Match(TokenType.Of);    // Now safe to consume OF
                Match(TokenType.Array); // Then consume ARRAY
                dimensions++;

                // Prevent infinite arrays
                if (dimensions > 9)
                {
                    ReportError("Array dimensions cannot exceed 9");
                    break;
                }
            }

            // Parse optional element type: (OF typeT)?
            TypeNode? elementType = null;
            if (Match(TokenType.Of))
            {
                elementType = ParseTypeReference();
                if (elementType == null)
                {
                    ReportError("Expected type after 'OF' in array declaration");
                    // Create a default ANY type for error recovery
                    elementType = new BuiltInTypeNode(BuiltInType.Any);
                }
            }

            var arrayNode = new ArrayTypeNode(dimensions, elementType);
            arrayNode.SourceSpan = new SourceSpan(startToken.SourceSpan.Start, 
                                                 elementType?.SourceSpan.End ?? Current.SourceSpan.End);
            return arrayNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing array type: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse array type for method annotations according to ANTLR grammar:
    /// ARRAY2 OF typeT | ARRAY3 OF typeT | ... | ARRAY OF typeT
    /// Examples: ARRAY OF STRING, ARRAY2 OF INTEGER, ARRAY5 OF MyClass
    /// </summary>
    private ArrayTypeNode? ParseAnnotationArrayType()
    {
        try
        {
            EnterRule("annotationArrayType");

            // Check for explicit dimension tokens
            var dimensions = Current.Type switch
            {
                TokenType.Array => 1,
                TokenType.Array2 => 2,
                TokenType.Array3 => 3,
                TokenType.Array4 => 4,
                TokenType.Array5 => 5,
                TokenType.Array6 => 6,
                TokenType.Array7 => 7,
                TokenType.Array8 => 8,
                TokenType.Array9 => 9,
                _ => 0
            };

            if (dimensions == 0)
            {
                // Not an array type in annotation context
                return null;
            }

            var startToken = Current;
            _position++; // Consume the array dimension token

            // Expect OF keyword after dimension token
            if (!Match(TokenType.Of))
            {
                ReportError($"Expected 'OF' after '{startToken.Text}' in method annotation");
                return null;
            }

            // Parse the element type
            var elementType = ParseTypeReference();
            if (elementType == null)
            {
                ReportError("Expected type after 'OF' in array annotation");
                // Create a default ANY type for error recovery
                elementType = new BuiltInTypeNode(BuiltInType.Any);
            }

            var arrayNode = new ArrayTypeNode(dimensions, elementType);
            arrayNode.SourceSpan = new SourceSpan(startToken.SourceSpan.Start, 
                                                 elementType?.SourceSpan.End ?? Current.SourceSpan.End);
            return arrayNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing annotation array type: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse app class path: appPackagePath COLON genericID
    /// appPackagePath: (METADATA | genericID) (COLON genericID (COLON genericID)?)?
    /// </summary>
    private AppClassTypeNode? ParseAppClassPath()
    {
        try
        {
            EnterRule("appClassPath");

            var pathParts = new List<string>();
            var startToken = Current;
            var endToken = startToken;
            // Parse first component (could be METADATA or genericID)
            if (Check(TokenType.Metadata) && Peek().Type == TokenType.Colon)
            {
                pathParts.Add(Current.Text);
                _position++;
            }
            else if (Check(TokenType.GenericId) && Peek().Type == TokenType.Colon)
            {
                var identifier = ParseGenericId();
                if (identifier != null)
                {
                    pathParts.Add(identifier);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null; // Not an app class path
            }

            // Parse additional path components separated by colons
            while (Match(TokenType.Colon))
            {
                var nextId = ParseGenericId();
                endToken = Current; // Update end token to last parsed identifier
                if (nextId != null)
                {
                    pathParts.Add(nextId);
                }
                else
                {
                    break;
                }
            }

            // Need at least package:class format
            if (pathParts.Count < 2)
            {
                ReportError("Error parsing app class path: must be at least 'package:class'", new SourceSpan(startToken.SourceSpan.Start, startToken.SourceSpan.End));
                return null;
            }

            // Last component is the class name, everything else is package path
            var className = pathParts[^1];
            var packagePath = pathParts.Take(pathParts.Count - 1);

            return new AppClassTypeNode(packagePath, className);
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse generic identifier with keyword flexibility
    /// </summary>
    private string? ParseGenericId()
    {
        try
        {
            EnterRule("genericId");

            // In PeopleCode, many keywords can be used as identifiers in certain contexts
            if (Current.Type switch
            {
                TokenType.Catch => true,
                TokenType.Class => true,
                TokenType.Continue => true,
                TokenType.Create => true,
                TokenType.Date => true,
                TokenType.Extends => true,
                TokenType.Get => true,
                TokenType.Import => true,
                TokenType.Instance => true,
                TokenType.Integer => true,
                TokenType.Interface => true,
                TokenType.Method => true,
                TokenType.Out => true,
                TokenType.Private => true,
                TokenType.Property => true,
                TokenType.ReadOnly => true,
                TokenType.Set => true,
                TokenType.String => true,
                TokenType.Throw => true,
                TokenType.Time => true,
                TokenType.Try => true,
                TokenType.Value => true,
                TokenType.GenericId => true,
                TokenType.GenericIdLimited => true,
                _ when Current.Type.IsIdentifier() => true,
                _ => false
            })
            {
                var result = Current.Text;
                _position++;
                return result;
            }

            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse class body according to ANTLR grammar:
    /// classBody: classMember (SEMI+ classMember)*
    /// </summary>
    private List<AstNode>? ParseClassBody()
    {
        try
        {
            EnterRule("classBody");

            var members = new List<AstNode>();

            // Parse first class member
            var firstMember = ParseClassMember();
            if (firstMember != null)
            {
                members.Add(firstMember);
            }

            // Parse additional members separated by semicolons
            while (!IsAtEnd && Check(TokenType.Semicolon))
            {
                // Consume required semicolons
                if (!Match(TokenType.Semicolon))
                {
                    ReportError("Expected ';' between class members");
                }
                
                // Consume any additional semicolons
                while (Match(TokenType.Semicolon)) { }

                // Check for end of class body
                if (IsAtEnd || Check(TokenType.EndOfFile))
                {
                    break;
                }

                // Parse next member
                var member = ParseClassMember();
                if (member != null)
                {
                    members.Add(member);
                }
                else
                {
                    // No more members or reached end
                    break;
                }
            }

            return members;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing class body: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse class member according to ANTLR grammar:
    /// classMember: method  #MethodImplementation
    ///            | getter  #GetterImplementation  
    ///            | setter  #SetterImplementation
    /// </summary>
    private AstNode? ParseClassMember()
    {
        try
        {
            EnterRule("classMember");

            if (Check(TokenType.Method))
            {
                return ParseMethodImplementation();
            }
            else if (Check(TokenType.Get))
            {
                return ParseGetterImplementation();
            }
            else if (Check(TokenType.Set))
            {
                return ParseSetterImplementation();
            }
            else
            {
                // No valid class member found
                return null;
            }
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing class member: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse method implementation according to ANTLR grammar:
    /// method: METHOD genericID SEMI* methodAnnotations statements? END_METHOD
    /// </summary>
    private MethodNode? ParseMethodImplementation()
    {
        try
        {
            EnterRule("methodImplementation");

            if (!Match(TokenType.Method))
            {
                ReportError("Expected 'METHOD' keyword");
                return null;
            }

            // Parse method name
            var methodName = ParseGenericId();
            if (methodName == null)
            {
                ReportError("Expected method name after 'METHOD'");
                return null;
            }

            var methodNode = new MethodNode(methodName);

            // Optional semicolons
            while (Match(TokenType.Semicolon)) { }

            // Parse method annotations (parameter and return type annotations)
            ParseMethodAnnotations(methodNode);

            // Parse method body statements
            if (!Check(TokenType.EndMethod))
            {
                var body = ParseStatementList(TokenType.EndMethod);
                methodNode.SetBody(body);
            }

            // Expect END-METHOD
            Consume(TokenType.EndMethod, "Expected 'END-METHOD' after method implementation");

            return methodNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing method implementation: {ex.Message}");
            PanicRecover(new HashSet<TokenType> { TokenType.EndMethod });
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse getter implementation according to ANTLR grammar:
    /// getter: GET genericID methodReturnAnnotation SEMI* statements END_GET
    /// </summary>
    private PropertyNode? ParseGetterImplementation()
    {
        try
        {
            EnterRule("getterImplementation");

            if (!Match(TokenType.Get))
            {
                ReportError("Expected 'GET' keyword");
                return null;
            }

            // Parse property name
            var propertyName = ParseGenericId();
            if (propertyName == null)
            {
                ReportError("Expected property name after 'GET'");
                return null;
            }

            // Create property node with unknown type for now (will be inferred)
            var propertyNode = new PropertyNode(propertyName, new BuiltInTypeNode(BuiltInType.Any));

            // Parse method return annotation (contains the actual property type)
            // Try to parse a return annotation - it's fine if there isn't one
            int startPosition = _position;
            if (!ParseMethodReturnAnnotation(propertyNode))
            {
                _position = startPosition;
            }

            // Optional semicolons
            while (Match(TokenType.Semicolon)) { }

            // Parse getter body
            var getterBody = ParseStatementList(TokenType.EndGet);
            propertyNode.SetGetterBody(getterBody);

            // Expect END-GET
            Consume(TokenType.EndGet, "Expected 'END-GET' after getter implementation");

            return propertyNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing getter implementation: {ex.Message}");
            PanicRecover(new HashSet<TokenType> { TokenType.EndGet });
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse setter implementation according to ANTLR grammar:
    /// setter: SET genericID methodParameterAnnotation SEMI* statements? END_SET
    /// </summary>
    private PropertyNode? ParseSetterImplementation()
    {
        try
        {
            EnterRule("setterImplementation");

            if (!Match(TokenType.Set))
            {
                ReportError("Expected 'SET' keyword");
                return null;
            }

            // Parse property name
            var propertyName = ParseGenericId();
            if (propertyName == null)
            {
                ReportError("Expected property name after 'SET'");
                return null;
            }

            // Create property node with unknown type for now
            var propertyNode = new PropertyNode(propertyName, new BuiltInTypeNode(BuiltInType.Any));

            // Parse method parameter annotation (contains the property type)
            // Try to parse a parameter annotation - it's fine if there isn't one
            int startPosition = _position;
            if (!ParseMethodParameterAnnotation(propertyNode))
            {
                _position = startPosition;
            }

            // Optional semicolons
            while (Match(TokenType.Semicolon)) { }

            // Parse setter body (optional)
            if (!Check(TokenType.EndSet))
            {
                var setterBody = ParseStatementList(TokenType.EndSet);
                propertyNode.SetSetterBody(setterBody);
            }

            // Expect END-SET
            Consume(TokenType.EndSet, "Expected 'END-SET' after setter implementation");

            return propertyNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing setter implementation: {ex.Message}");
            PanicRecover(new HashSet<TokenType> { TokenType.EndSet });
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse method annotations: methodParameterAnnotation* methodReturnAnnotation? methodExtendsAnnotation?
    /// </summary>
    private void ParseMethodAnnotations(MethodNode methodNode)
    {
        try
        {
            EnterRule("methodAnnotations");

            // Parse all annotations (/+ ... +/)
            while (Check(TokenType.SlashPlus))
            {
                int startPosition = _position;
                
                // Try to parse as a parameter annotation
                if (ParseMethodParameterAnnotation(methodNode))
                {
                    continue;
                }
                
                // Reset position and try to parse as a return annotation
                _position = startPosition;
                if (ParseMethodReturnAnnotation(methodNode))
                {
                    continue;
                }
                
                // Reset position and try to parse as an extends annotation
                _position = startPosition;
                if (ParseMethodExtendsAnnotation(methodNode))
                {
                    continue;
                }
                
                // If we get here, we couldn't parse the annotation, so skip it
                Match(TokenType.SlashPlus);
                while (!IsAtEnd && !Check(TokenType.PlusSlash))
                {
                    _position++;
                }
                Match(TokenType.PlusSlash);
                ReportError("Unrecognized method annotation");
            }
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse method parameter annotation: SLASH_PLUS methodAnnotationArgument COMMA? PLUS_SLASH
    /// </summary>
    /// <returns>True if a parameter annotation was successfully parsed, false otherwise</returns>
    private bool ParseMethodParameterAnnotation(MethodNode methodNode)
    {
        try
        {
            EnterRule("methodParameterAnnotation");

            if (!Match(TokenType.SlashPlus))
            {
                return false;
            }

            // Check if this is a parameter annotation (starts with a user variable)
            if (!Check(TokenType.UserVariable))
            {
                // Not a parameter annotation
                return false;
            }

            // Parse parameter annotation
            var parameter = ParseMethodAnnotationArgument();
            if (parameter != null)
            {
                methodNode.AddParameter(parameter);
            }

            // Optional comma
            Match(TokenType.Comma);

            // Expect closing annotation
            Consume(TokenType.PlusSlash, "Expected '+/' to close method parameter annotation");
            return true;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse method parameter annotation for property setter
    /// </summary>
    /// <returns>True if a parameter annotation was successfully parsed, false otherwise</returns>
    private bool ParseMethodParameterAnnotation(PropertyNode propertyNode)
    {
        try
        {
            EnterRule("methodParameterAnnotation");

            if (!Match(TokenType.SlashPlus))
            {
                return false;
            }

            // Check if this is a parameter annotation (starts with a user variable)
            if (!Check(TokenType.UserVariable))
            {
                // Not a parameter annotation
                return false;
            }

            // Parse parameter - for setter this gives us the property type
            var parameter = ParseMethodAnnotationArgument();
            // Could update property type based on parameter type here

            // Optional comma
            Match(TokenType.Comma);

            // Expect closing annotation
            Consume(TokenType.PlusSlash, "Expected '+/' to close method parameter annotation");
            return true;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse method return annotation: SLASH_PLUS RETURNS annotationType PLUS_SLASH
    /// </summary>
    /// <returns>True if a return annotation was successfully parsed, false otherwise</returns>
    private bool ParseMethodReturnAnnotation(MethodNode methodNode)
    {
        try
        {
            EnterRule("methodReturnAnnotation");

            if (!Match(TokenType.SlashPlus))
            {
                return false;
            }

            if (!Match(TokenType.Returns))
            {
                // Not a return annotation, back up
                _position--;
                return false;
            }

            // Parse return type
            var returnType = ParseAnnotationType();
            if (returnType != null)
            {
                methodNode.SetReturnType(returnType);
            }

            // Expect closing annotation
            Consume(TokenType.PlusSlash, "Expected '+/' to close method return annotation");
            return true;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse method return annotation for property getter
    /// </summary>
    /// <returns>True if a return annotation was successfully parsed, false otherwise</returns>
    private bool ParseMethodReturnAnnotation(PropertyNode propertyNode)
    {
        try
        {
            EnterRule("methodReturnAnnotation");

            if (!Match(TokenType.SlashPlus))
            {
                return false;
            }

            if (!Match(TokenType.Returns))
            {
                // Not a return annotation, back up
                _position--;
                return false;
            }

            // Parse return type - this becomes the property type
            var returnType = ParseAnnotationType();
            // Could update property type here based on return type

            // Expect closing annotation
            Consume(TokenType.PlusSlash, "Expected '+/' to close method return annotation");
            return true;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse method extends annotation: SLASH_PLUS EXTENDS DIV IMPLEMENTS appClassPath DOT genericID PLUS_SLASH
    /// </summary>
    /// <returns>True if an extends annotation was successfully parsed, false otherwise</returns>
    private bool ParseMethodExtendsAnnotation(MethodNode methodNode)
    {
        try
        {
            EnterRule("methodExtendsAnnotation");

            if (!Match(TokenType.SlashPlus))
            {
                return false;
            }

            if (!Match(TokenType.Extends))
            {
                // Not an extends annotation, back up
                _position--;
                return false;
            }

            // Expect DIV (forward slash)
            if (!Match(TokenType.Div))
            {
                ReportError("Expected '/' after 'EXTENDS' in method annotation");
            }

            // Expect IMPLEMENTS keyword
            if (!Match(TokenType.Implements))
            {
                ReportError("Expected 'IMPLEMENTS' after 'EXTENDS/' in method annotation");
            }

            // Parse app class path
            var appClassPath = ParseAppClassPath();
            if (appClassPath == null)
            {
                ReportError("Expected app class path after 'IMPLEMENTS' in method annotation");
            }
            else
            {
                // Store the implemented interface in the method node
                methodNode.AddImplementedInterface(appClassPath);
            }

            // Expect DOT
            if (!Match(TokenType.Dot))
            {
                ReportError("Expected '.' after app class path in method annotation");
            }

            // Expect genericID (method name in interface)
            if (Check(TokenType.GenericId) || Check(TokenType.GenericIdLimited))
            {
                string methodName = Current.Text;
                _position++;
                // Store the implemented method name in the method node
                methodNode.ImplementedMethodName = methodName;
            }
            else
            {
                ReportError("Expected method name after '.' in method annotation");
            }

            // Expect closing annotation
            Consume(TokenType.PlusSlash, "Expected '+/' to close method extends annotation");
            return true;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse method annotation argument: USER_VARIABLE AS annotationType OUT?
    /// </summary>
    private ParameterNode? ParseMethodAnnotationArgument()
    {
        try
        {
            EnterRule("methodAnnotationArgument");

            // Parse parameter name
            if (!Check(TokenType.UserVariable))
            {
                ReportError("Expected parameter name (&variable) in annotation");
                return null;
            }

            var paramName = Current.Text;
            _position++;

            // Expect AS keyword
            if (!Match(TokenType.As))
            {
                ReportError("Expected 'AS' after parameter name in annotation");
            }

            // Parse parameter type
            var paramType = ParseAnnotationType();
            if (paramType == null)
            {
                ReportError("Expected parameter type after 'AS' in annotation");
                return null;
            }

            var parameter = new ParameterNode(paramName, paramType);

            // Parse optional OUT modifier
            if (Match(TokenType.Out))
            {
                parameter.IsOut = true;
            }

            return parameter;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse annotation type according to ANTLR grammar:
    /// annotationType: ARRAY2 OF typeT  #AnnotationArray2Type
    ///               | ARRAY3 OF typeT  #AnnotationArray3Type
    ///               | ...
    ///               | ARRAY OF typeT   #AnnotationArray1Type
    ///               | typeT            #AnnotationBaseType
    /// </summary>
    private TypeNode? ParseAnnotationType()
    {
        try
        {
            EnterRule("annotationType");

            // Try to parse as annotation array type first
            var arrayType = ParseAnnotationArrayType();
            if (arrayType != null)
            {
                return arrayType;
            }

            // Fall back to base type parsing
            return ParseTypeReference();
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing annotation type: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse interface declaration:
    /// INTERFACE Name (EXTENDS appClassPath)? SEMI* (methodHeader SEMI+)* END-INTERFACE
    /// Only method headers are allowed; no bodies.
    /// </summary>
    private InterfaceNode? ParseInterface()
    {
        try
        {
            EnterRule("interfaceDeclaration");

            if (!Match(TokenType.Interface))
                return null;

            if (!(Check(TokenType.GenericId) || Check(TokenType.GenericIdLimited)))
            {
                ReportError("Expected interface name after 'INTERFACE'");
                return null;
            }

            var name = Current.Text;
            _position++;
            var iface = new InterfaceNode(name);

            // Optional EXTENDS base interface
            if (Match(TokenType.Extends))
            {
                var baseType = ParseAppClassPath() ?? ParseSimpleType();
                if (baseType != null)
                {
                    iface.SetBaseInterface(baseType);
                }
                else
                {
                    ReportError("Expected base interface type after 'EXTENDS'");
                }
            }

            // SEMI*
            while (Match(TokenType.Semicolon)) { }

            // Parse class header (according to grammar)
            // For interfaces, we'll only process method headers in the public section
            ParseInterfaceHeader(iface);

            Consume(TokenType.EndInterface, "Expected 'END-INTERFACE' to close interface");
            return iface;
        }
        finally
        {
            ExitRule();
        }
    }
    
    /// <summary>
    /// Parse interface header according to grammar (similar to classHeader but only allowing method headers)
    /// </summary>
    private void ParseInterfaceHeader(InterfaceNode interfaceNode)
    {
        try
        {
            EnterRule("interfaceHeader");
            
            // In interfaces, all methods are implicitly public
            // Parse method headers until END-INTERFACE
            while (!IsAtEnd && !Check(TokenType.EndInterface))
            {
                if (Check(TokenType.Method))
                {
                    var methodHeader = ParseMethodHeader(VisibilityModifier.Public);
                    if (methodHeader is MethodNode methodNode)
                    {
                        // All interface methods are abstract by definition
                        methodNode.IsAbstract = true;
                        interfaceNode.AddMethod(methodNode);
                    }
                    
                    // Require at least one semicolon after header
                    if (!Match(TokenType.Semicolon))
                    {
                        ReportError("Expected ';' after interface method header");
                    }
                    while (Match(TokenType.Semicolon)) { }
                }
                else if (Match(TokenType.Semicolon))
                {
                    // Allow stray semicolons
                }
                else
                {
                    // Unexpected token; sync to next semicolon or END-INTERFACE
                    ReportError($"Unexpected token in INTERFACE: {Current.Type}");
                    while (!IsAtEnd && !Check(TokenType.Semicolon) && !Check(TokenType.EndInterface))
                        _position++;
                    while (Match(TokenType.Semicolon)) { }
                }
            }
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse function declaration or definition.
    /// Supports PeopleCode user-defined functions:
    ///   FUNCTION name (params?) [RETURNS type]? ;                // declaration
    ///   FUNCTION name (params?) [RETURNS type]? statements END-FUNCTION ; // definition
    ///
    /// DLL/Library declarations via DECLARE FUNCTION are not implemented yet.
    /// </summary>
    private FunctionNode? ParseFunction()
    {
        try
        {
            EnterRule("function");

            // Handle optional prefixes (e.g., PEOPLECODE) before FUNCTION if present
            if (Match(TokenType.PeopleCode))
            {
                // Accept and continue; next should be FUNCTION
            }

            if (Match(TokenType.Declare))
            {
                // DECLARE FUNCTION variant (PEOPLECODE or LIBRARY)
                Consume(TokenType.Function, "Expected 'FUNCTION' after 'DECLARE'");

                if (!(Check(TokenType.GenericId) || Check(TokenType.GenericIdLimited)))
                {
                    ReportError("Expected function name after 'DECLARE FUNCTION'");
                    return null;
                }

                var declName = Current.Text;
                _position++;
                var declNode = new FunctionNode(declName, FunctionType.UserDefined);

                if (Match(TokenType.PeopleCode))
                {
                    // DECLARE FUNCTION name PEOPLECODE Record.Field RecordEvent (params?) RETURNS type ;
                    if (!TryParseRecordField(out var rec, out var fld))
                    {
                        ReportError("Expected Record.Field after 'PEOPLECODE'");
                    }
                    else
                    {
                        declNode.RecordName = rec;
                        declNode.FieldName = fld;
                    }

                    // Record event token (FIELDCHANGE, FIELDEDIT, etc.)
                    if (Check(TokenType.RecordEvent))
                    {
                        declNode.RecordEvent = Current.Text;
                        _position++;
                    }
                    else
                    {
                        ReportError("Expected record event after Record.Field in PEOPLECODE declaration");
                    }

                    // Optional parameter list
                    if (Match(TokenType.LeftParen))
                    {
                        if (!Check(TokenType.RightParen))
                        {
                            do
                            {
                                var param = ParseMethodArgument();
                                if (param != null) declNode.AddParameter(param);
                                else
                                {
                                    while (!IsAtEnd && !(Check(TokenType.Comma) || Check(TokenType.RightParen)))
                                        _position++;
                                }
                            } while (Match(TokenType.Comma));
                        }
                        Consume(TokenType.RightParen, "Expected ')' after parameters");
                    }

                    if (Match(TokenType.Returns))
                    {
                        var r = ParseTypeReference();
                        if (r != null) declNode.SetReturnType(r);
                        else ReportError("Expected return type after 'RETURNS'");
                    }

                    Match(TokenType.Semicolon);
                    declNode = new FunctionNode(declName, FunctionType.PeopleCode)
                    {
                        RecordName = declNode.RecordName,
                        FieldName = declNode.FieldName,
                        RecordEvent = declNode.RecordEvent,
                        ReturnType = declNode.ReturnType
                    };
                    foreach (var p in declNode.Parameters.ToList()) { /* already attached */ }
                    return declNode;
                }
                else if (Match(TokenType.Library))
                {
                    // DECLARE FUNCTION name LIBRARY "lib" [ALIAS "alias"] (params?) RETURNS type ;
                    if (!Check(TokenType.StringLiteral))
                    {
                        ReportError("Expected library name string after 'LIBRARY'");
                    }
                    else
                    {
                        declNode.LibraryName = Current.Value?.ToString();
                        _position++;
                    }

                    if (Match(TokenType.Alias))
                    {
                        // According to grammar, alias should only be a string literal
                        if (Check(TokenType.StringLiteral))
                        {
                            declNode.AliasName = Current.Value?.ToString();
                            _position++;
                        }
                        else
                        {
                            ReportError("Expected string literal after 'ALIAS'");
                        }
                    }

                    if (Match(TokenType.LeftParen))
                    {
                        // Parse DLL arguments according to grammar
                        ParseDllArguments(declNode);
                        Consume(TokenType.RightParen, "Expected ')' after parameters");
                    }

                    if (Match(TokenType.Returns))
                    {
                        // Parse DLL return type according to grammar
                        var returnType = ParseDllReturnType();
                        if (returnType != null)
                        {
                            declNode.SetReturnType(returnType);
                        }
                        else
                        {
                            ReportError("Expected return type after 'RETURNS'");
                        }
                    }

                    Match(TokenType.Semicolon);
                    declNode = new FunctionNode(declName, FunctionType.Library)
                    {
                        LibraryName = declNode.LibraryName,
                        AliasName = declNode.AliasName,
                        ReturnType = declNode.ReturnType
                    };
                    foreach (var p in declNode.Parameters.ToList()) { /* already attached */ }
                    return declNode;
                }
                else
                {
                    ReportError("Expected PEOPLECODE or LIBRARY after 'DECLARE FUNCTION name'");
                    while (!IsAtEnd && !Check(TokenType.Semicolon)) _position++;
                    Match(TokenType.Semicolon);
                    return null;
                }
            }

            if (!Match(TokenType.Function))
                return null;

            // Parse function name (allow generic identifiers)
            if (!(Check(TokenType.GenericId) || Check(TokenType.GenericIdLimited)))
            {
                ReportError("Expected function name after 'FUNCTION'");
                return null;
            }

            var functionName = Current.Text;
            _position++;

            var functionNode = new FunctionNode(functionName, FunctionType.UserDefined);

            // Parameters
            if (Match(TokenType.LeftParen))
            {
                // Empty parameter list is allowed
                if (!Check(TokenType.RightParen))
                {
                    do
                    {
                        var param = ParseMethodArgument();
                        if (param != null)
                        {
                            functionNode.AddParameter(param);
                        }
                        else
                        {
                            // Parameter parse failed: attempt to recover by skipping to ',' or ')'
                            while (!IsAtEnd && !(Check(TokenType.Comma) || Check(TokenType.RightParen)))
                                _position++;
                        }
                    }
                    while (Match(TokenType.Comma));
                }

                Consume(TokenType.RightParen, "Expected ')' after function parameters");
            }

            // Optional RETURNS type
            if (Match(TokenType.Returns))
            {
                var returnType = ParseTypeReference();
                if (returnType == null)
                {
                    ReportError("Expected return type after 'RETURNS'");
                }
                else
                {
                    functionNode.SetReturnType(returnType);
                }
            }
            
            // Optional documentation comment (DOC StringLiteral)
            if (Match(TokenType.Doc))
            {
                if (Check(TokenType.StringLiteral))
                {
                    functionNode.Documentation = Current.Value?.ToString();
                    _position++;
                }
                else
                {
                    ReportError("Expected string literal after 'DOC'");
                }
            }

            // Declaration or definition?
            if (Match(TokenType.Semicolon))
            {
                // Declaration only (no body)
                return functionNode;
            }
            
            // Handle optional semicolons (SEMI*) before statements
            while (Match(TokenType.Semicolon)) { }

            // Function definition body until END-FUNCTION
            var body = ParseStatementList(TokenType.EndFunction);
            Consume(TokenType.EndFunction, "Expected 'END-FUNCTION' after function body");

            functionNode.SetBody(body);
            return functionNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing function: {ex.Message}");
            // Attempt to recover by skipping to END-FUNCTION or semicolon
            while (!IsAtEnd && !(Check(TokenType.EndFunction) || Check(TokenType.Semicolon)))
                _position++;
            if (Check(TokenType.EndFunction))
            {
                _position++; // consume END-FUNCTION
                Match(TokenType.Semicolon);
            }
            else if (Check(TokenType.Semicolon))
            {
                _position++;
            }
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse DLL arguments according to grammar: dllArgument (COMMA dllArgument)*
    /// </summary>
    private void ParseDllArguments(FunctionNode functionNode)
    {
        try
        {
            EnterRule("dllArguments");

            if (Check(TokenType.RightParen))
            {
                // Empty argument list
                return;
            }

            do
            {
                var param = ParseDllArgument();
                if (param != null)
                {
                    functionNode.AddParameter(param);
                }
                else
                {
                    // Skip to next comma or closing parenthesis
                    while (!IsAtEnd && !(Check(TokenType.Comma) || Check(TokenType.RightParen)))
                        _position++;
                }
            } while (Match(TokenType.Comma));
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse DLL argument according to grammar: genericID (REF | VALUE)? (AS builtInType)?
    /// </summary>
    private ParameterNode? ParseDllArgument()
    {
        try
        {
            EnterRule("dllArgument");

            // Parse parameter name (must be a generic ID)
            if (!(Check(TokenType.GenericId) || Check(TokenType.GenericIdLimited)))
            {
                ReportError("Expected parameter name in DLL argument");
                return null;
            }

            var paramName = Current.Text;
            _position++;

            // Create parameter with default type (Any)
            var parameter = new ParameterNode(paramName, new BuiltInTypeNode(BuiltInType.Any));

            // Parse optional REF or VALUE modifier
            if (Match(TokenType.Ref))
            {
                parameter.IsOut = true; // Use IsOut for REF parameters
            }
            else if (Match(TokenType.Value))
            {
                parameter.IsOut = false; // Explicitly mark as not out for VALUE parameters
            }

            // Parse optional AS builtInType
            if (Match(TokenType.As))
            {
                var builtInType = TryParseBuiltInType();
                if (builtInType != null)
                {
                    parameter.Type = builtInType;
                }
                else
                {
                    ReportError("Expected built-in type after 'AS' in DLL argument");
                }
            }

            return parameter;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse DLL return type according to grammar:
    /// dllReturnType: genericID AS builtInType | builtInType
    /// </summary>
    private TypeNode? ParseDllReturnType()
    {
        try
        {
            EnterRule("dllReturnType");

            // Check for first variant: genericID AS builtInType
            if ((Check(TokenType.GenericId) || Check(TokenType.GenericIdLimited)) && Peek().Type == TokenType.As)
            {
                // Consume the generic ID (return value name)
                _position++;

                // Consume the AS keyword
                Match(TokenType.As);

                // Parse built-in type
                var builtInType = TryParseBuiltInType();
                if (builtInType != null)
                {
                    return builtInType;
                }
                else
                {
                    ReportError("Expected built-in type after 'AS' in DLL return type");
                    return null;
                }
            }
            // Check for second variant: builtInType
            else
            {
                return TryParseBuiltInType();
            }
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse Record.Field pair
    /// </summary>
    private bool TryParseRecordField(out string recordName, out string fieldName)
    {
        recordName = ""; fieldName = "";
        if (!(Check(TokenType.GenericId) || Check(TokenType.GenericIdLimited))) return false;
        recordName = Current.Text;
        _position++;
        if (!Match(TokenType.Dot)) return false;
        if (!(Check(TokenType.GenericId) || Check(TokenType.GenericIdLimited))) return false;
        fieldName = Current.Text;
        _position++;
        return true;
    }

    /// <summary>
    /// Parse non-local variable declaration according to grammar:
    /// nonLocalVarDeclaration: (COMPONENT | GLOBAL) typeT USER_VARIABLE (COMMA USER_VARIABLE)* COMMA?
    ///                       | (COMPONENT | GLOBAL) typeT  // compiles yet is meaningless
    /// </summary>
    private VariableNode? ParseVariableDeclaration()
    {
        try
        {
            EnterRule("nonLocalVarDeclaration");

            VariableScope scope;
            if (Match(TokenType.Global)) scope = VariableScope.Global;
            else if (Match(TokenType.Component)) scope = VariableScope.Component;
            else return null;

            var varType = ParseTypeReference();
            if (varType == null)
            {
                ReportError("Expected variable type after scope");
                return null;
            }

            // According to grammar, variable names are optional (though meaningless without them)
            if (!Check(TokenType.UserVariable))
            {
                // This is the second variant: (COMPONENT | GLOBAL) typeT
                // Create a variable node with empty name for AST consistency
                var emptyVariable = new VariableNode("", varType, scope);
                Match(TokenType.Semicolon); // optional
                return emptyVariable;
            }

            var firstName = Current.Text;
            _position++;

            var variable = new VariableNode(firstName, varType, scope);

            // Additional names
            while (Match(TokenType.Comma))
            {
                if (Check(TokenType.UserVariable))
                {
                    variable.AddName(Current.Text);
                    _position++;
                }
                else
                {
                    break; // tolerate trailing comma
                }
            }

            Match(TokenType.Semicolon); // optional
            return variable;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse constant declaration: CONSTANT USER_VARIABLE EQ expression SEMI?
    /// </summary>
    private ConstantNode? ParseConstantDeclaration()
    {
        try
        {
            EnterRule("constantDeclaration");

            if (!Match(TokenType.Constant))
                return null;

            if (!Check(TokenType.UserVariable))
            {
                ReportError("Expected constant name (&NAME) after 'CONSTANT'");
                return null;
            }

            var name = Current.Text;
            _position++;

            if (!Match(TokenType.Equal))
            {
                ReportError("Expected '=' after constant name");
            }

            // According to grammar, constant values must be literals
            if (!Current.Type.IsLiteral())
            {
                ReportError("Expected literal value for constant (NULL, number, string, or boolean)");
                // Try to parse expression anyway for error recovery
                var valueExpr = ParseExpression();
                if (valueExpr == null)
                {
                    return null;
                }
                Match(TokenType.Semicolon); // optional
                return new ConstantNode(name, valueExpr);
            }

            // Parse literal value
            var literalValue = ParseLiteral();
            if (literalValue == null)
            {
                ReportError("Expected literal value for constant");
                return null;
            }

            Match(TokenType.Semicolon); // optional
            return new ConstantNode(name, literalValue);
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse statement with error recovery
    /// </summary>
    private StatementNode? ParseStatement()
    {
        try
        {
            EnterRule("statement");

            // Handle various statement types
            StatementNode? statement = Current.Type switch
            {
                TokenType.If => ParseIfStatement(),
                TokenType.For => ParseForStatement(),
                TokenType.While => ParseWhileStatement(),
                TokenType.Repeat => ParseRepeatStatement(),
                TokenType.Try => ParseTryStatement(),
                TokenType.Evaluate => ParseEvaluateStatement(),
                TokenType.Return => ParseReturnStatement(),
                TokenType.Break => ParseBreakStatement(),
                TokenType.Continue => ParseContinueStatement(),
                TokenType.Exit => ParseExitStatement(),
                TokenType.Error => ParseErrorStatement(),
                TokenType.Warning => ParseWarningStatement(),
                TokenType.Throw => ParseThrowStatement(),
                TokenType.Local => ParseLocalVariableStatement(),
                _ => ParseExpressionStatement()
            };
            
            // If we have a valid statement, assign a statement number and check for a semicolon
            if (statement != null)
            {
                // Increment statement counter and assign to this statement
                _statementCounter++;
                statement.StatementNumber = _statementCounter;
                
                // Set the HasSemicolon flag if a semicolon is present
                statement.HasSemicolon = Match(TokenType.Semicolon);
                
                // Consume any additional semicolons (allowed by the grammar)
                while (Match(TokenType.Semicolon))
                {
                    // Each additional semicolon is just ignored
                }
            }
            
            return statement;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing statement: {ex.Message}");
            PanicRecover(StatementSyncTokens);
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse IF statement: IF condition THEN statements [ELSE statements] END-IF;
    /// </summary>
    private IfStatementNode? ParseIfStatement()
    {
        try
        {
            EnterRule("if-statement");

            if (!Match(TokenType.If))
                return null;

            var condition = ParseExpression();
            if (condition == null)
            {
                ReportError("Expected condition after 'IF'");
                
                // Error recovery: try to synchronize to THEN token
                if (SynchronizeToToken(TokenType.Then))
                {
                    // Create a placeholder condition so we can continue parsing the THEN block
                    condition = new LiteralNode(true, LiteralType.Boolean)
                    {
                        SourceSpan = Current.SourceSpan
                    };
                }
                else
                {
                    // No THEN found, cannot recover
                    return null;
                }
            }

            if (!Match(TokenType.Then))
            {
                ReportError("Expected 'THEN' after IF condition");
                
                // Error recovery: try to synchronize to THEN token
                if (!SynchronizeToToken(TokenType.Then))
                {
                    // No THEN found, but continue with what we have
                    ReportError("Could not find 'THEN' token for error recovery");
                }
            }

        // Handle optional semicolons after THEN (SEMI*)
        while (Match(TokenType.Semicolon)) { }

        var thenStatements = ParseStatementList(TokenType.EndIf, TokenType.Else);
            
            BlockNode? elseStatements = null;
                    if (Match(TokenType.Else))
        {
            // Handle optional semicolons after ELSE (SEMI*)
            while (Match(TokenType.Semicolon)) { }
            
            elseStatements = ParseStatementList(TokenType.EndIf);
        }

                    Consume(TokenType.EndIf, "Expected 'END-IF' after IF statement");

            var ifNode = new IfStatementNode(condition, thenStatements);
            if (elseStatements != null)
                ifNode.SetElseBlock(elseStatements);
            return ifNode;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse FOR statement: FOR USER_VARIABLE EQ expression TO expression (STEP expression)? SEMI* statementBlock? END_FOR
    /// </summary>
    private ForStatementNode? ParseForStatement()
    {
        try
        {
            EnterRule("for-statement");

            if (!Match(TokenType.For))
                return null;

            // Parse USER_VARIABLE (not expression)
            if (!Check(TokenType.UserVariable))
            {
                ReportError("Expected user variable after 'FOR'");
                return null;
            }
            
            var variableToken = Current;
            var variableName = variableToken.Value?.ToString() ?? "";
            _position++; // Consume the user variable token

            if (!Match(TokenType.Equal))
            {
                ReportError("Expected '=' after FOR variable");
            }

            var start = ParseExpression();
            if (start == null)
            {
                ReportError("Expected start value after '='");
                return null;
            }

            if (!Match(TokenType.To))
            {
                ReportError("Expected 'TO' after start value");
            }

            var end = ParseExpression();
            if (end == null)
            {
                ReportError("Expected end value after 'TO'");
                return null;
            }

            ExpressionNode? step = null;
            if (Match(TokenType.Step))
            {
                step = ParseExpression();
                if (step == null)
                {
                    ReportError("Expected step value after 'STEP'");
                }
            }

            // Handle optional semicolons (SEMI*)
            while (Match(TokenType.Semicolon)) { }

            // Parse optional statementBlock
            var body = new BlockNode();
            if (!Check(TokenType.EndFor))
            {
                body = ParseStatementList(TokenType.EndFor);
            }

            Consume(TokenType.EndFor, "Expected 'END-FOR' after FOR statement");

            var forNode = new ForStatementNode(variableName, start, end, body);
            if (step != null)
                forNode.SetStepValue(step);
            return forNode;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse WHILE statement: WHILE condition statements END-WHILE;
    /// </summary>
    private WhileStatementNode? ParseWhileStatement()
    {
        try
        {
            EnterRule("while-statement");

            if (!Match(TokenType.While))
                return null;

                    var condition = ParseExpression();
        if (condition == null)
        {
            ReportError("Expected condition after 'WHILE'");
            return null;
        }

        // Handle optional semicolons after condition (SEMI*)
        while (Match(TokenType.Semicolon)) { }

        var body = ParseStatementList(TokenType.EndWhile);

                    Consume(TokenType.EndWhile, "Expected 'END-WHILE' after WHILE statement");

            return new WhileStatementNode(condition, body);
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse REPEAT statement: REPEAT statements UNTIL condition;
    /// </summary>
    private RepeatStatementNode? ParseRepeatStatement()
    {
        try
        {
            EnterRule("repeat-statement");

                    if (!Match(TokenType.Repeat))
            return null;

        // Handle optional semicolons after REPEAT (SEMI*)
        while (Match(TokenType.Semicolon)) { }

        var body = ParseStatementList(TokenType.Until);

            Consume(TokenType.Until, "Expected 'UNTIL' after REPEAT statements");

            var condition = ParseExpression();
            if (condition == null)
            {
                ReportError("Expected condition after 'UNTIL'");
                return null;
                    }

        return new RepeatStatementNode(body, condition);
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse TRY statement: TRY statements CATCH var statements END-TRY;
    /// </summary>
    private TryStatementNode? ParseTryStatement()
    {
        try
        {
            EnterRule("try-statement");

            if (!Match(TokenType.Try))
                return null;
                
            // Handle optional semicolons after TRY (SEMI*)
            while (Match(TokenType.Semicolon)) { }

            var tryBlock = ParseStatementList(TokenType.Catch, TokenType.EndTry);

            List<CatchClauseNode> catchClauses = new();
            while (Match(TokenType.Catch))
            {
                // According to grammar: CATCH (EXCEPTION | appClassPath) USER_VARIABLE SEMI* statementBlock?
                TypeNode? exceptionType = null;
                
                // Parse exception type (EXCEPTION or appClassPath)
                if (Match(TokenType.Exception))
                {
                    exceptionType = new BuiltInTypeNode(BuiltInType.Exception);
                }
                else
                {
                    // Try to parse as app class path
                    exceptionType = ParseAppClassPath();
                    
                    // If not an app class path, report error
                    if (exceptionType == null)
                    {
                        ReportError("Expected 'EXCEPTION' or app class path after 'CATCH'");
                    }
                }
                
                // Parse user variable
                if (!Check(TokenType.UserVariable))
                {
                    ReportError("Expected user variable after exception type in CATCH clause");
                }
                else
                {
                    var exceptionVariable = new IdentifierNode(Current.Text, IdentifierType.UserVariable);
                    _position++;
                    
                    // Handle optional semicolons (SEMI*)
                    while (Match(TokenType.Semicolon)) { }
                    
                    var catchBlock = ParseStatementList(TokenType.Catch, TokenType.EndTry);
                    catchClauses.Add(new CatchClauseNode(exceptionVariable, catchBlock, exceptionType));
                }
                
                // Handle optional semicolons between catch clauses (SEMI*)
                while (Match(TokenType.Semicolon)) { }
            }
            
            // Handle optional semicolons before END-TRY (SEMI*)
            while (Match(TokenType.Semicolon)) { }

            Consume(TokenType.EndTry, "Expected 'END-TRY' after TRY statement");

            return new TryStatementNode(tryBlock, catchClauses);
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse simple control flow statements (RETURN, BREAK, CONTINUE, EXIT)
    /// </summary>
    private StatementNode? ParseReturnStatement()
    {
        if (!Match(TokenType.Return))
            return null;

        ExpressionNode? value = null;
        if (!Check(TokenType.Semicolon) && !StatementSyncTokens.Contains(Current.Type))
        {
            value = ParseExpression();
        }

        return new ReturnStatementNode(value);
    }

    private StatementNode? ParseBreakStatement()
    {
        if (!Match(TokenType.Break))
            return null;
        return new BreakStatementNode();
    }

    private StatementNode? ParseContinueStatement()
    {
        if (!Match(TokenType.Continue))
            return null;
        return new ContinueStatementNode();
    }

    private StatementNode? ParseExitStatement()
    {
        if (!Match(TokenType.Exit))
            return null;
        return new ExitStatementNode();
    }

    private StatementNode? ParseErrorStatement()
    {
        if (!Match(TokenType.Error))
            return null;

        var message = ParseExpression();
        if (message == null)
        {
            ReportError("Expected message after 'ERROR'");
            message = new LiteralNode("Error", LiteralType.String);
        }
        return new ErrorStatementNode(message);
    }

    private StatementNode? ParseWarningStatement()
    {
        if (!Match(TokenType.Warning))
            return null;

        var message = ParseExpression();
        if (message == null)
        {
            ReportError("Expected message after 'WARNING'");
            message = new LiteralNode("Warning", LiteralType.String);
        }
        return new WarningStatementNode(message);
    }

    private StatementNode? ParseThrowStatement()
    {
        if (!Match(TokenType.Throw))
            return null;

        var exception = ParseExpression();
        if (exception == null)
        {
            ReportError("Expected exception after 'THROW'");
            exception = new LiteralNode("Exception", LiteralType.String);
        }
        return new ThrowStatementNode(exception);
    }

    /// <summary>
    /// Parse local variable declaration according to ANTLR grammar:
    /// localVariableDeclaration: localVariableDefinition | localVariableDeclAssignment
    /// localVariableDefinition: LOCAL typeT USER_VARIABLE (COMMA USER_VARIABLE)* COMMA?
    /// localVariableDeclAssignment: LOCAL typeT USER_VARIABLE EQ expression
    /// </summary>
    private StatementNode? ParseLocalVariableStatement()
    {
        try
        {
            EnterRule("localVariableDeclaration");

            if (!Match(TokenType.Local))
                return null;

            // Parse variable type
            var variableType = ParseTypeReference();
            if (variableType == null)
            {
                ReportError("Expected variable type after 'LOCAL'");
                return null;
            }

            // Parse first variable name (required)
            if (!Check(TokenType.UserVariable))
            {
                ReportError("Expected variable name (&variable) after type");
                return null;
            }

            var firstVariableName = Current.Text;
            _position++;

            // Check if this is assignment or definition
            if (Match(TokenType.Equal))
            {
                // This is localVariableDeclAssignment: LOCAL type &var = expression
                var initialValue = ParseExpression();
                if (initialValue == null)
                {
                    ReportError("Expected expression after '=' in local variable assignment");
                    return null;
                }

                return new LocalVariableDeclarationWithAssignmentNode(variableType, firstVariableName, initialValue);
            }
            else
            {
                // This is localVariableDefinition: LOCAL type &var1, &var2, ...
                var variableNames = new List<string> { firstVariableName };

                // Parse additional variable names separated by commas
                while (Match(TokenType.Comma))
                {
                    if (Check(TokenType.UserVariable))
                    {
                        variableNames.Add(Current.Text);
                        _position++;
                    }
                    else
                    {
                        // Trailing comma is allowed, so this is OK
                        break;
                    }
                }

                return new LocalVariableDeclarationNode(variableType, variableNames);
            }
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing local variable declaration: {ex.Message}");
            PanicRecover(StatementSyncTokens);
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse expression statement (assignment or function call)
    /// </summary>
    private ExpressionStatementNode? ParseExpressionStatement()
    {
        var expr = ParseExpression();
        if (expr == null)
            return null;

        return new ExpressionStatementNode(expr);
    }

    /// <summary>
    /// Parse statement list until end token(s)
    /// </summary>
    private BlockNode ParseStatementList(params TokenType[] endTokens)
    {
        var block = new BlockNode();

        while (!IsAtEnd && !endTokens.Contains(Current.Type))
        {
            var statement = ParseStatement();
            if (statement != null)
            {
                block.AddStatement(statement);
            }
            else
            {
                // Recovery: skip problematic token
                _position++;
            }
        }

        return block;
    }

    /// <summary>
    /// Parse EVALUATE statement with WHEN/WHEN-OTHER clauses.
    /// Grammar (per ANTLR):
    /// EVALUATE expression SEMI* whenClauses? whenOther? END_EVALUATE
    /// whenClauses: whenClause (SEMI* whenClause)*
    /// whenClause: WHEN comparisonOperator? expression SEMI* statementBlock?
    /// whenOther: WHEN_OTHER SEMI* statementBlock?
    /// </summary>
    private StatementNode? ParseEvaluateStatement()
    {
        try
        {
            EnterRule("evaluate-statement");

            if (!Match(TokenType.Evaluate))
                return null;

            var evalExpr = ParseExpression();
            if (evalExpr == null)
            {
                ReportError("Expected expression after 'EVALUATE'");
                evalExpr = new LiteralNode("0", LiteralType.Integer);
            }

            var evalNode = new EvaluateStatementNode(evalExpr);

            // EVALUATE expression SEMI*
            while (Match(TokenType.Semicolon)) { }

            // whenClauses? whenOther?
            while (!IsAtEnd && !Check(TokenType.EndEvaluate))
            {
                // Allow SEMI* between clauses
                while (Match(TokenType.Semicolon)) { }

                if (Match(TokenType.When))
                {
                    // Optional comparison operator
                    BinaryOperator? op = null;
                    if (Check(TokenType.Equal) || Check(TokenType.NotEqual) ||
                        Check(TokenType.LessThan) || Check(TokenType.LessThanOrEqual) ||
                        Check(TokenType.GreaterThan) || Check(TokenType.GreaterThanOrEqual))
                    {
                        var opToken = Current.Type;
                        _position++;
                        op = opToken switch
                        {
                            TokenType.Equal => BinaryOperator.Equal,
                            TokenType.NotEqual => BinaryOperator.NotEqual,
                            TokenType.LessThan => BinaryOperator.LessThan,
                            TokenType.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
                            TokenType.GreaterThan => BinaryOperator.GreaterThan,
                            TokenType.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                            _ => null
                        };
                    }

                    // Required single expression
                    var condition = ParseExpression();
                    if (condition == null)
                    {
                        ReportError("Expected condition expression after 'WHEN'");
                        // Sync to next clause boundary
                        while (!IsAtEnd && !(Check(TokenType.When) || Check(TokenType.WhenOther) || Check(TokenType.EndEvaluate)))
                            _position++;
                        continue;
                    }

                    // SEMI*
                    while (Match(TokenType.Semicolon)) { }

                    // Optional statementBlock? until WHEN/WHEN_OTHER/END_EVALUATE
                    var body = ParseStatementList(TokenType.When, TokenType.WhenOther, TokenType.EndEvaluate);
                    evalNode.AddWhenClause(new WhenClause(condition, body, op));
                }
                else if (Match(TokenType.WhenOther))
                {
                    // SEMI*
                    while (Match(TokenType.Semicolon)) { }

                    var otherBody = ParseStatementList(TokenType.EndEvaluate);
                    evalNode.SetWhenOtherBlock(otherBody);
                }
                else
                {
                    // Unexpected token; sync to next boundary
                    ReportError($"Unexpected token in EVALUATE: {Current.Type}");
                    while (!IsAtEnd && !(Check(TokenType.When) || Check(TokenType.WhenOther) || Check(TokenType.EndEvaluate)))
                        _position++;
                }
            }

                    Consume(TokenType.EndEvaluate, "Expected 'END-EVALUATE' to close EVALUATE");
        return evalNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing EVALUATE statement: {ex.Message}");
            while (!IsAtEnd && !Check(TokenType.EndEvaluate))
                _position++;
            Match(TokenType.EndEvaluate);
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    // ===== EXPRESSION PARSING =====

    /// <summary>
    /// Parse expression with full operator precedence
    /// </summary>
    public ExpressionNode? ParseExpression()
    {
        try
        {
            EnterRule("expression");
            return ParseAssignmentExpression();
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing expression: {ex.Message}");
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse assignment expressions (=, +=, -=, |=)
    /// </summary>
    private ExpressionNode? ParseAssignmentExpression()
    {
        var expr = ParseOrExpression();
        if (expr == null) return null;

        if (Current.Type.IsAssignmentOperator())
        {
            var op = GetAssignmentOperator(Current.Type);
            var opToken = Current;
            _position++;

            var right = ParseAssignmentExpression(); // Right associative
            if (right == null)
            {
                ReportError("Expected expression after assignment operator");
                return expr;
            }

            if (!expr.IsLValue)
            {
                ReportError("Invalid assignment target", opToken.SourceSpan);
            }

            return new AssignmentNode(expr, op, right)
            {
                SourceSpan = new SourceSpan(expr.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return expr;
    }

    /// <summary>
    /// Parse logical OR expressions
    /// </summary>
    private ExpressionNode? ParseOrExpression()
    {
        var left = ParseAndExpression();
        if (left == null) return null;

        while (Match(TokenType.Or))
        {
            var right = ParseAndExpression();
            if (right == null)
            {
                ReportError("Expected expression after 'OR'");
                break;
            }

            left = new BinaryOperationNode(left, BinaryOperator.Or, right)
            {
                SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return left;
    }

    /// <summary>
    /// Parse logical AND expressions
    /// </summary>
    private ExpressionNode? ParseAndExpression()
    {
        var left = ParseEqualityExpression();
        if (left == null) return null;

        while (Match(TokenType.And))
        {
            var right = ParseEqualityExpression();
            if (right == null)
            {
                ReportError("Expected expression after 'AND'");
                break;
            }

            left = new BinaryOperationNode(left, BinaryOperator.And, right)
            {
                SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return left;
    }

    /// <summary>
    /// Parse equality expressions (=, <>, !=)
    /// </summary>
    private ExpressionNode? ParseEqualityExpression()
    {
        var left = ParseRelationalExpression();
        if (left == null) return null;

        while (Current.Type is TokenType.Equal or TokenType.NotEqual)
        {
            var op = Current.Type == TokenType.Equal ? BinaryOperator.Equal : BinaryOperator.NotEqual;
            _position++;

            var right = ParseRelationalExpression();
            if (right == null)
            {
                ReportError("Expected expression after equality operator");
                break;
            }

            left = new BinaryOperationNode(left, op, right)
            {
                SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return left;
    }

    /// <summary>
    /// Parse relational expressions (<, <=, >, >=)
    /// </summary>
    private ExpressionNode? ParseRelationalExpression()
    {
        var left = ParseTypeCastExpression();
        if (left == null) return null;

        while (Current.Type is TokenType.LessThan or TokenType.LessThanOrEqual or 
                               TokenType.GreaterThan or TokenType.GreaterThanOrEqual)
        {
            var op = Current.Type switch
            {
                TokenType.LessThan => BinaryOperator.LessThan,
                TokenType.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
                TokenType.GreaterThan => BinaryOperator.GreaterThan,
                TokenType.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                _ => throw new InvalidOperationException("Unexpected relational operator")
            };
            _position++;

            var right = ParseTypeCastExpression();
            if (right == null)
            {
                ReportError("Expected expression after relational operator");
                break;
            }

            left = new BinaryOperationNode(left, op, right)
            {
                SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return left;
    }

    /// <summary>
    /// Parse type cast expressions (expr AS Type)
    /// </summary>
    private ExpressionNode? ParseTypeCastExpression()
    {
        try
        {
            EnterRule("typeCastExpression");
            
            var expr = ParseConcatenationExpression();
            if (expr == null) return null;
            
            // Handle type casting (expr AS Type) - PeopleCode only supports single casts, not chains
            if (Match(TokenType.As))
            {
                var typeSpec = ParseTypeSpecifier();
                if (typeSpec == null)
                {
                    ReportError("Expected type specifier after 'AS'");
                    // Error recovery: try to sync to expression boundaries
                    var syncTokens = new HashSet<TokenType> { 
                        TokenType.Semicolon, TokenType.Comma, TokenType.RightParen, 
                        TokenType.RightBracket, TokenType.Then, TokenType.EndIf,
                        TokenType.And, TokenType.Or, TokenType.Equal, TokenType.NotEqual
                    };
                    PanicRecover(syncTokens);
                    return expr;
                }

                expr = new TypeCastNode(expr, typeSpec)
                {
                    SourceSpan = new SourceSpan(expr.SourceSpan.Start, typeSpec.SourceSpan.End)
                };
            }

            return expr;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing type cast expression: {ex.Message}");
            // Return partial result for better recovery
            return ParseConcatenationExpression();
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse string concatenation expressions (|)
    /// </summary>
    private ExpressionNode? ParseConcatenationExpression()
    {
        var left = ParseNotExpression();
        if (left == null) return null;

        while (Match(TokenType.Pipe))
        {
            var right = ParseNotExpression();
            if (right == null)
            {
                ReportError("Expected expression after '|'");
                break;
            }

            left = new BinaryOperationNode(left, BinaryOperator.Concatenate, right)
            {
                SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return left;
    }
    
    /// <summary>
    /// Parse NOT expressions (NOT expression)
    /// </summary>
    private ExpressionNode? ParseNotExpression()
    {
        if (Match(TokenType.Not))
        {
            var opToken = Current;
            _position--;  // Go back to the NOT token for source span
            
            var operand = ParseAdditiveExpression();
            if (operand == null)
            {
                ReportError("Expected expression after 'NOT'");
                return null;
            }
            
            _position++;  // Move past the NOT token
            
            return new UnaryOperationNode(UnaryOperator.Not, operand)
            {
                SourceSpan = new SourceSpan(opToken.SourceSpan.Start, operand.SourceSpan.End)
            };
        }
        
        return ParseAdditiveExpression();
    }

    /// <summary>
    /// Parse additive expressions (+, -)
    /// </summary>
    private ExpressionNode? ParseAdditiveExpression()
    {
        var left = ParseMultiplicativeExpression();
        if (left == null) return null;

        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Current.Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
            _position++;

            var right = ParseMultiplicativeExpression();
            if (right == null)
            {
                ReportError("Expected expression after additive operator");
                break;
            }

            left = new BinaryOperationNode(left, op, right)
            {
                SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return left;
    }

    /// <summary>
    /// Parse multiplicative expressions (*, /)
    /// </summary>
    private ExpressionNode? ParseMultiplicativeExpression()
    {
        var left = ParseExponentialExpression();
        if (left == null) return null;

        while (Current.Type is TokenType.Star or TokenType.Div)
        {
            var op = Current.Type == TokenType.Star ? BinaryOperator.Multiply : BinaryOperator.Divide;
            _position++;

            var right = ParseExponentialExpression();
            if (right == null)
            {
                ReportError("Expected expression after multiplicative operator");
                break;
            }

            left = new BinaryOperationNode(left, op, right)
            {
                SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return left;
    }

    /// <summary>
    /// Parse exponential expressions (**)
    /// </summary>
    private ExpressionNode? ParseExponentialExpression()
    {
        var left = ParseUnaryExpression();
        if (left == null) return null;

        if (Match(TokenType.Power))
        {
            // Right associative
            var right = ParseExponentialExpression();
            if (right == null)
            {
                ReportError("Expected expression after '**'");
                return left;
            }

            return new BinaryOperationNode(left, BinaryOperator.Power, right)
            {
                SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return left;
    }

    /// <summary>
    /// Parse unary expressions (-, @)
    /// </summary>
    private ExpressionNode? ParseUnaryExpression()
    {
        if (Current.Type is TokenType.Minus or TokenType.At)
        {
            var op = Current.Type switch
            {
                TokenType.Minus => UnaryOperator.Negate,
                TokenType.At => UnaryOperator.Reference,
                _ => throw new InvalidOperationException("Unexpected unary operator")
            };
            var opToken = Current;
            _position++;

            var operand = ParseUnaryExpression(); // Right associative
            if (operand == null)
            {
                ReportError("Expected expression after unary operator");
                return null;
            }

            return new UnaryOperationNode(op, operand)
            {
                SourceSpan = new SourceSpan(opToken.SourceSpan.Start, operand.SourceSpan.End)
            };
        }

        return ParsePostfixExpression();
    }

    /// <summary>
    /// Parse postfix expressions (function calls, array access, property access)
    /// </summary>
    private ExpressionNode? ParsePostfixExpression()
    {
        var expr = ParsePrimaryExpression();
        if (expr == null) return null;

        while (true)
        {
            if (Match(TokenType.LeftBracket))
            {
                // Array access
                var index = ParseExpression();
                if (index == null)
                {
                    ReportError("Expected expression for array index");
                }
                
                Consume(TokenType.RightBracket, "Expected ']' after array index");
                
                expr = new ArrayIndexNode(expr, index!)
                {
                    SourceSpan = new SourceSpan(expr.SourceSpan.Start, Current.SourceSpan.End)
                };
            }
            else if (Match(TokenType.LeftParen))
            {
                // Check if this is an implicit subindex expression (expression(expression))
                // or a function call (expression(args...))
                
                // Peek ahead to see if there's a single expression or an argument list
                var startPos = _position;
                var singleExpr = ParseExpression();
                
                // If we parsed a single expression and the next token is ')',
                // this is an implicit subindex expression
                if (singleExpr != null && Check(TokenType.RightParen))
                {
                    _position++; // Consume the ')'
                    expr = new ArrayIndexNode(expr, singleExpr) // Reuse ArrayIndexNode for implicit subindex
                    {
                        SourceSpan = new SourceSpan(expr.SourceSpan.Start, Current.SourceSpan.End)
                    };
                }
                else
                {
                    // Reset position and parse as a normal function call
                    _position = startPos;
                    
                    // Function call
                    var args = ParseArgumentList();
                    Consume(TokenType.RightParen, "Expected ')' after function arguments");
                    
                    expr = new FunctionCallNode(expr, args)
                    {
                        SourceSpan = new SourceSpan(expr.SourceSpan.Start, Current.SourceSpan.End)
                    };
                }
            }
            else if (Match(TokenType.Dot))
            {
                                    // Property/method access
                    if (Check(TokenType.GenericId))
                    {
                        var member = Current.Text;
                        var memberToken = Current;
                        _position++;
                        
                        // Create member access node
                        expr = new MemberAccessNode(expr, member)
                        {
                            SourceSpan = new SourceSpan(expr.SourceSpan.Start, memberToken.SourceSpan.End)
                        };
                        
                        // Check for method call after dot access
                        if (Match(TokenType.LeftParen))
                        {
                            // Method call
                            var args = ParseArgumentList();
                            Consume(TokenType.RightParen, "Expected ')' after method arguments");
                            
                            expr = new FunctionCallNode(expr, args)
                            {
                                SourceSpan = new SourceSpan(expr.SourceSpan.Start, Current.SourceSpan.End)
                            };
                        }
                    }
                else if (Check(TokenType.StringLiteral))
                {
                    // Dynamic member access with string
                    var member = Current.Value?.ToString() ?? "";
                    var memberToken = Current;
                    _position++;
                    
                    expr = new MemberAccessNode(expr, member, isDynamic: true)
                    {
                        SourceSpan = new SourceSpan(expr.SourceSpan.Start, memberToken.SourceSpan.End)
                    };
                }
                else
                {
                    ReportError("Expected member name after '.'");
                    break;
                }
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    /// <summary>
    /// Parse primary expressions (literals, identifiers, parenthesized expressions)
    /// </summary>
    private ExpressionNode? ParsePrimaryExpression()
    {
        // Literals
        if (Current.Type.IsLiteral())
        {
            return ParseLiteral();
        }

        // Identifiers
        if (Current.Type.IsIdentifier())
        {
            return ParseIdentifier();
        }

        // Built-in function keywords that are callable like identifiers
        if (IsBuiltinFunctionKeyword(Current.Type))
        {
            var token = Current;
            _position++;
            return new IdentifierNode(token.Text, IdentifierType.Generic)
            {
                SourceSpan = token.SourceSpan
            };
        }

        // Parenthesized expression
        if (Match(TokenType.LeftParen))
        {
            var expr = ParseExpression();
            if (expr == null)
            {
                ReportError("Expected expression inside parentheses");
            }
            
            Consume(TokenType.RightParen, "Expected ')' after expression");
            return expr;
        }

        // Object creation
        if (Match(TokenType.Create))
        {
            return ParseObjectCreation();
        }
        
        // App class path (metadata expression)
        if ((Check(TokenType.Metadata) || Check(TokenType.GenericId)) && Peek().Type == TokenType.Colon)
        {
            var appClassPath = ParseAppClassPath();
            if (appClassPath != null)
            {
                return new MetadataExpressionNode(appClassPath);
            }
        }

        // Type cast is handled in ParseCastExpression()

        ReportError($"Unexpected token in expression: {Current.Type}");
        return null;
    }

    // Keywords that are built-in functions usable as identifiers in expressions
    private static bool IsBuiltinFunctionKeyword(TokenType type)
    {
        return type == TokenType.Value
            || type == TokenType.Date
            || type == TokenType.DateTime
            || type == TokenType.Time
            || type == TokenType.Number
            || type == TokenType.String
            || type == TokenType.Integer
            || type == TokenType.Float;
    }

    /// <summary>
    /// Parse argument list for function calls
    /// </summary>
    private List<ExpressionNode> ParseArgumentList()
    {
        var args = new List<ExpressionNode>();

        if (Check(TokenType.RightParen))
        {
            return args; // Empty argument list
        }

        do
        {
            var arg = ParseExpression();
            if (arg != null)
            {
                args.Add(arg);
            }
        } while (Match(TokenType.Comma));

        return args;
    }

    /// <summary>
    /// Parse literal expressions
    /// </summary>
    private LiteralNode? ParseLiteral()
    {
        try
        {
            EnterRule("literal");

            var token = Current;
            _position++;

            return token.Type switch
            {
                TokenType.IntegerLiteral => new LiteralNode(token.Value!, LiteralType.Integer)
                {
                    SourceSpan = token.SourceSpan
                },
                TokenType.DecimalLiteral => new LiteralNode(token.Value!, LiteralType.Decimal)
                {
                    SourceSpan = token.SourceSpan
                },
                TokenType.StringLiteral => new LiteralNode(token.Value!, LiteralType.String)
                {
                    SourceSpan = token.SourceSpan
                },
                TokenType.BooleanLiteral => new LiteralNode(token.Value!, LiteralType.Boolean)
                {
                    SourceSpan = token.SourceSpan
                },
                TokenType.Null => new LiteralNode(null!, LiteralType.Null)
                {
                    SourceSpan = token.SourceSpan
                },
                _ => throw new InvalidOperationException($"Unexpected literal type: {token.Type}")
            };
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse identifier expressions
    /// </summary>
    private IdentifierNode? ParseIdentifier()
    {
        try
        {
            EnterRule("identifier");

            var token = Current;
            _position++;

            var identifierType = token.Type switch
            {
                TokenType.GenericId => IdentifierType.Generic,
                TokenType.GenericIdLimited => IdentifierType.Generic,
                TokenType.UserVariable => IdentifierType.UserVariable,
                TokenType.SystemVariable => IdentifierType.SystemVariable,
                TokenType.SystemConstant => IdentifierType.SystemConstant,
                TokenType.Super => IdentifierType.Super,
                _ => IdentifierType.Generic
            };

            return new IdentifierNode(token.Text, identifierType)
            {
                SourceSpan = token.SourceSpan
            };
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse object creation expressions (CREATE package:class(...))
    /// </summary>
    private ObjectCreationNode? ParseObjectCreation()
    {
        // CREATE already consumed
        var appClassPath = ParseAppClassPath();
        if (appClassPath == null)
        {
            ReportError("Expected app class path after 'CREATE'");
            return null;
        }

        List<ExpressionNode> args = new();
        if (Match(TokenType.LeftParen))
        {
            args = ParseArgumentList();
            Consume(TokenType.RightParen, "Expected ')' after constructor arguments");
        }

        return new ObjectCreationNode(appClassPath, args);
    }

    // Note: Type casting is handled in ParseCastExpression(), this method is not used

    /// <summary>
    /// Convert token type to assignment operator
    /// </summary>
    private AssignmentOperator GetAssignmentOperator(TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.Equal => AssignmentOperator.Assign,
            TokenType.PlusEqual => AssignmentOperator.AddAssign,
            TokenType.MinusEqual => AssignmentOperator.SubtractAssign,
            TokenType.PipeEqual => AssignmentOperator.ConcatenateAssign,
            _ => throw new ArgumentException($"Invalid assignment operator: {tokenType}")
        };
    }

    /// <summary>
    /// Print the AST structure hierarchy showing node types without text content
    /// </summary>
    /// <param name="root">The root AST node to print</param>
    /// <param name="useTreeCharacters">Whether to use tree characters (├── └──) or simple indentation</param>
    /// <returns>A formatted string showing the AST hierarchy</returns>
    public string PrintAstStructure(AstNode root, bool useTreeCharacters = true)
    {
        if (root == null)
            return "null";

        var result = new System.Text.StringBuilder();
        PrintAstStructureRecursive(root, result, "", true, useTreeCharacters);
        return result.ToString();
    }

    /// <summary>
    /// Recursive helper method for printing AST structure
    /// </summary>
    private void PrintAstStructureRecursive(AstNode node, System.Text.StringBuilder result, string prefix, bool isLast, bool useTreeCharacters)
    {
        if (node == null) return;

        // Build the current line
        var nodeName = node.GetType().Name;
        var childCount = node.Children.Count;
        var childInfo = childCount > 0 ? $" ({childCount} child{(childCount == 1 ? "" : "ren")})" : "";

        if (useTreeCharacters)
        {
            var connector = isLast ? "└── " : "├── ";
            result.AppendLine($"{prefix}{connector}{nodeName}{childInfo}");
        }
        else
        {
            result.AppendLine($"{prefix}{nodeName}{childInfo}");
        }

        // Prepare prefix for children
        var childPrefix = useTreeCharacters 
            ? prefix + (isLast ? "    " : "│   ")
            : prefix + "  ";

        // Recursively print children
        for (int i = 0; i < node.Children.Count; i++)
        {
            var isLastChild = i == node.Children.Count - 1;
            PrintAstStructureRecursive(node.Children[i], result, childPrefix, isLastChild, useTreeCharacters);
        }
    }
}

/// <summary>
/// Represents a parse error
/// </summary>
public class ParseError
{
    public string Message { get; }
    public SourceSpan Location { get; }
    public ParseErrorSeverity Severity { get; }
    public string Context { get; }

    public ParseError(string message, SourceSpan location, ParseErrorSeverity severity, string context)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Location = location;
        Severity = severity;
        Context = context ?? "";
    }

    public override string ToString()
    {
        return $"{Severity} at {Location}: {Message}";
    }
}

/// <summary>
/// Parse error severity levels
/// </summary>
public enum ParseErrorSeverity
{
    Warning,
    Error,
    Fatal
}