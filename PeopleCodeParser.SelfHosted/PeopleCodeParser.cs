using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection.Metadata;

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
    private ProgramNode? _workingProgram;

    // Error recovery settings
    private const int MaxErrorRecoveryAttempts = 10;
    private int _errorRecoveryCount = 0;

    // Compiler directive support - defaults to 99.99.99 for "newest version" policy
    public static ToolsVersion ToolsRelease = new("99.99.99");

    // Synchronization tokens for error recovery
    private static readonly HashSet<TokenType> StatementSyncTokens = new()
    {
        TokenType.Semicolon,
        TokenType.If,
        TokenType.Else,
        TokenType.For,
        TokenType.While,
        TokenType.Repeat,
        TokenType.Try,
        TokenType.Return,
        TokenType.Break,
        TokenType.Continue,
        TokenType.Exit,
        TokenType.Evaluate,
        TokenType.When,
        TokenType.WhenOther,
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
        TokenType.EndInterface,
    };

    // Store original tokens for directive reprocessing
    private readonly List<Token> _originalTokens;
    private List<SourceSpan> _skippedDirectiveSpans = new();
    public PeopleCodeParser(IEnumerable<Token> tokens)
    {
        _originalTokens = tokens?.ToList() ?? throw new ArgumentNullException(nameof(tokens));
        _tokens = new();
        _skippedDirectiveSpans = PreProcessDirectives();
    }

    /// <summary>
    /// Reprocess directives with the current ToolsRelease setting
    /// </summary>
    private List<SourceSpan> PreProcessDirectives()
    {
        // Clear existing errors from previous preprocessing
        _errors.RemoveAll(e => e.Message.Contains("directive") || e.Message.Contains("Directive"));

        // Pass 1: Process directives with all tokens (including trivia)
        var preprocessor = new DirectivePreprocessor(_originalTokens, ToolsRelease);
        var processedTokens = preprocessor.ProcessDirectives();

        // Add any preprocessing errors to our error list
        foreach (var error in preprocessor.Errors)
        {
            _errors.Add(new ParseError(error, new SourceSpan(new SourcePosition(0), new SourcePosition(0)), ParseErrorSeverity.Error, "Directive preprocessing"));
        }
        processedTokens = processedTokens?.Where(t => !t.Type.IsTrivia()).ToList() ?? throw new ArgumentException(nameof(processedTokens));
        // Update tokens list
        _tokens.Clear();
        _tokens.AddRange(processedTokens);
        _position = 0;

        return preprocessor.SkippedSpans;
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
    /// Previous token that was processed
    /// </summary>
    private Token Previous => _position > 0 && _position - 1 < _tokens.Count ? _tokens[_position - 1] :
                           Token.CreateEof(new SourcePosition(0));

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
    /// Create an error expression node with proper token boundaries
    /// </summary>
    private ExpressionNode CreateErrorExpression()
    {
        var token = Current;
        return new IdentifierNode("<error>", IdentifierType.Generic)
        {
            FirstToken = token,
            LastToken = token
        };
    }

    /// <summary>
    /// Ensure an expression node has proper token boundaries, using fallback tokens if needed
    /// </summary>
    private ExpressionNode? EnsureTokenBoundaries(ExpressionNode? node, Token? fallbackFirst = null, Token? fallbackLast = null)
    {
        if (node != null)
        {
            node.FirstToken ??= fallbackFirst;
            node.LastToken ??= fallbackLast;
        }
        return node;
    }

    /// <summary>
    /// Report a parse error at current token position (for immediate context errors)
    /// </summary>
    private void ReportError(string message)
    {
        var context = _ruleStack.Count > 0 ? string.Join(" -> ", _ruleStack.Reverse()) : "unknown";

        _errors.Add(new ParseError(
            message,
            Current.SourceSpan,
            ParseErrorSeverity.Error,
            context
        ));

        Debug.WriteLine($"Parse Error at {Current.SourceSpan}: {message} (Context: {context})");
    }

    /// <summary>
    /// Report a parse error highlighting a specific token (for structural errors)
    /// </summary>
    private void ReportError(string message, Token highlightToken)
    {
        var context = _ruleStack.Count > 0 ? string.Join(" -> ", _ruleStack.Reverse()) : "unknown";

        _errors.Add(new ParseError(
            message,
            highlightToken.SourceSpan,
            ParseErrorSeverity.Error,
            context
        ));

        Debug.WriteLine($"Parse Error at {highlightToken.SourceSpan}: {message} (Context: {context})");
    }

    /// <summary>
    /// Report a parse error with explicit span (for range-based errors)
    /// </summary>
    private void ReportError(string message, SourceSpan location)
    {
        var context = _ruleStack.Count > 0 ? string.Join(" -> ", _ruleStack.Reverse()) : "unknown";

        _errors.Add(new ParseError(
            message,
            location,
            ParseErrorSeverity.Error,
            context
        ));

        Debug.WriteLine($"Parse Error at {location}: {message} (Context: {context})");
    }

    /// <summary>
    /// Report a parse error highlighting a token range (for construct-based errors)
    /// </summary>
    private void ReportError(string message, Token startToken, Token endToken)
    {
        var span = new SourceSpan(startToken.SourceSpan.Start, endToken.SourceSpan.End);
        ReportError(message, span);
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
    /// Smart recovery that attempts to find the next valid statement boundary
    /// Does NOT generate additional error messages - assumes parsing errors were already reported
    /// </summary>
    /// <returns>True if a statement boundary was found, false if recovery failed</returns>
    private bool SmartStatementRecover()
    {
        if (_errorRecoveryCount >= MaxErrorRecoveryAttempts)
        {
            ReportError("Too many parse errors, stopping recovery attempts");
            return false;
        }


        _errorRecoveryCount++;

        int tokensSkipped = 0;

        // Skip tokens until we find a statement synchronization point
        while (!IsAtEnd && !StatementSyncTokens.Contains(Current.Type))
        {
            _position++;
            tokensSkipped++;

            // Prevent infinite loops
            if (tokensSkipped > 100)
            {
                ReportError("Recovery failed: skipped too many tokens without finding statement boundary");
                return false;
            }
        }

        if (!IsAtEnd && StatementSyncTokens.Contains(Current.Type))
        {
            if (tokensSkipped > 0)
            {
                ReportWarning($"Skipped {tokensSkipped} tokens to recover at '{Current.Type}' statement boundary");
            }
            return true;
        }

        return false; // End of input reached
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
       // Initialize with preprocessed tokens
       var program = new ProgramNode();
        _workingProgram = program;
        program.SkippedDirectiveSpans = _skippedDirectiveSpans;
        program.FirstToken = Current;
        try
        {
            EnterRule("program");
            _errorRecoveryCount = 0;

            // Collect all comments from the token stream
            CollectComments(program);


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
                // This is an AppClassProgram - use two-phase parsing
                // Phase 1: Parse class header only
                var appClass = ParseClassHeader();
                if (appClass != null)
                {
                    program.SetAppClass(appClass);
                }

                // Phase 2: Parse preambles (external declarations) at program level
                ParseProgramPreambles(program);

                // Phase 3: Parse class body if app class exists
                if (program.AppClass != null && !IsAtEnd && !Check(TokenType.EndOfFile))
                {
                    ParseClassBody(program.AppClass);
                }

                // Consume any trailing semicolons
                while (Match(TokenType.Semicolon)) { }
                program.LastToken = Previous;
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
                program.LastToken = Previous;
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
                    {
                        var block = new BlockNode();
                        var firstStatementToken = Current;

                        while (!IsAtEnd && !Check(TokenType.EndOfFile))
                        {
                            var statement = ParseStatement();
                            if (statement != null)
                            {
                                block.AddStatement(statement);
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

                        // Set source span for the main block
                        block.FirstToken = firstStatementToken;
                        block.LastToken = Previous;

                        program.SetMainBlock(block);


                        block.RegisterStatementNumbers(this, program);

                    }
                }

                // Parse final optional semicolons before EOF
                while (Match(TokenType.Semicolon)) { }
                program.LastToken = Previous;
                return program;
            }
            catch (Exception ex)
            {
                ReportError($"Unexpected error in program parsing: {ex.Message}");
                PanicRecover(StatementSyncTokens.Union(BlockSyncTokens).ToHashSet());
                program.LastToken = Previous;
                return program;
            }
        }
        finally
        {
            if (Current.Type != TokenType.EndOfFile)
            {
                ReportError("Finished parsing program but not at end of file. Got: " + Current.Type + " == " + Current.Text);
                //Console.WriteLine(this.PrintAstStructure(program));
            }
            ExitRule();

            if (_errors.Count > 0)
            {
                Debugger.Break();
            }

            foreach (var error in _errors)
            {

            }

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
                            continue; // Skip the semicolon check for function definitions
                        }
                    }
                    else if (Check(TokenType.Global, TokenType.Component))
                    {
                        var variable = ParseVariableDeclaration();
                        if (variable != null)
                            program.AddComponentAndGlobalVariable(variable);

                    }
                    else if (Check(TokenType.Local))
                    {
                        var startPosition = _position;
                        var variable = ParseLocalVariableStatement();
                        if (variable != null)
                        {
                            // Add to LocalVariables list (program-level locals)
                            program.AddLocalVariable(variable);
                        }
                        else
                        {
                            _position = startPosition; // Reset position if parsing failed
                            break;
                        }
                    }
                    else if (Check(TokenType.Constant))
                    {
                        var constant = ParseConstantDeclaration();
                        if (constant != null)
                            program.AddConstant(constant);
                    }
                    else
                    {
                        break; // No more preamble items
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
            var firstToken = Previous;
            // Parse import path (expecting package:path format)
            var pathParts = new List<string>();
            bool consumedColon = false;

            // First segment: METADATA or generic identifier
            if (Check(TokenType.Metadata))
            {
                pathParts.Add(Current.Text);
                _position++;
            }
            else
            {

                if (Check(TokenType.Caret))
                {
                    var caretCount = 1;
                    while (Peek(caretCount).Type == TokenType.Caret)
                    {
                        caretCount++;
                    }

                    if (caretCount > 3)
                    {
                        var caretSourceSpan = new SourceSpan(Current.SourceSpan.Start, Peek(caretCount-1).SourceSpan.End);
                        ReportError("Relative path imports cannot have more than 3 carets.", caretSourceSpan);
                    }

                    pathParts.Add(new string('^', caretCount));
                    _position += (caretCount);
                }
                else
                {

                    var firstId = ParseGenericId();
                    if (firstId != null)
                    {
                        pathParts.Add(firstId);
                    }
                    else
                    {
                        ReportError("Expected package path after 'IMPORT'");
                        return null;
                    }
                }
            }

            // Parse subsequent segments separated by colons
            while (Match(TokenType.Colon))
            {
                consumedColon = true;

                var nextId = ParseGenericId();
                if (nextId != null)
                {
                    pathParts.Add(nextId);
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

            // Require at least one semicolon, allow additional semicolons (SEMI+ per grammar)
            if (!Match(TokenType.Semicolon))
            {
                ReportError("Expected ';' after import declaration");
            }
            while (Match(TokenType.Semicolon)) { }

            // Grammar requires at least one colon and at least two parts (package:class or package:*)
            if (consumedColon && pathParts.Count >= 2)
            {
                var lastToken = Previous;

                var importNode = new ImportNode(string.Join(":", pathParts));
                importNode.FirstToken = firstToken;
                importNode.LastToken = lastToken;
                
                // Set token boundaries on the imported type node
                importNode.ImportedType.FirstToken = firstToken;
                importNode.ImportedType.LastToken = lastToken;
                
                return importNode;
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
    /// Parse class header according to ANTLR grammar (Phase 1 of app class parsing):
    /// classDeclaration: CLASS genericID EXTENDS superclass SEMI* classHeader END_CLASS        #ClassDeclarationExtension
    ///                 | CLASS genericID IMPLEMENTS appClassPath SEMI* classHeader END_CLASS   #ClassDeclarationImplementation  
    ///                 | CLASS genericID SEMI* classHeader END_CLASS                           #ClassDeclarationPlain
    /// </summary>
    private AppClassNode? ParseClassHeader()
    {
        try
        {
            EnterRule("classHeader");
            var startToken = Current;
            if (!Match(TokenType.Class))
            {
                ReportError("Expected 'CLASS' keyword");
                return null;
            }

            // Parse class name
            var className = ParseGenericId();
            var nameToken = Previous;
            if (className == null)
            {
                ReportError("Expected class name after 'CLASS'");
                return null;
            }

            var classNode = new AppClassNode(className, nameToken, isInterface: false);

            // Check for EXTENDS or IMPLEMENTS clause
            if (Match(TokenType.Extends))
            {
                var superclass = ParseSuperclass();
                if (superclass != null)
                {
                    classNode.SetBaseType(superclass);
                }
                else
                {
                    ReportError("Expected superclass after 'EXTENDS'");
                }
            }
            else if (Match(TokenType.Implements))
            {
                var implementedInterface = ParseSuperclass();
                if (implementedInterface != null)
                {
                    classNode.SetBaseType(implementedInterface);
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

            // Optional semicolons before class header
            while (Match(TokenType.Semicolon)) { }
            classNode.FirstToken = startToken;
            classNode.LastToken = Previous;
            return classNode;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing class header: {ex.Message}");
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
                classNode.ProtectedToken = Previous; // Capture the 'protected' token
                while (Match(TokenType.Semicolon)) { } // Optional semicolons
                ParseVisibilitySection(classNode, VisibilityModifier.Protected);
            }

            // Parse private section if present
            if (Match(TokenType.Private))
            {
                classNode.PrivateToken = Previous; // Capture the 'private' token
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
                        if (member != null && member is MethodNode method)
                        {
                            method.IsConstructor = (method.Name == classNode.Name);
                        }

                    }
                    else if (Check(TokenType.Property))
                    {
                        // Property declarations are allowed in all visibility sections
                        member = ParsePropertyDeclaration(visibility);
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
                    else
                    {
                        // Unknown member type or reached end of section
                        break;
                    }

                    if (member != null)
                    {
                        classNode.AddMember(member, visibility);
                    }

                    if (Match(TokenType.Semicolon) && member is DeclarationNode d)
                    {
                        d.HasSemicolon = true;
                    }

                    // Consume any semicolons
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

            // Could be an app class path or simple type
            // Try to parse as app class path first (with colons)
            var appClassPath = ParseAppClassPath();
            if (appClassPath != null)
            {
                return appClassPath;
            }

            // Fall back to simple type
            var before = _position;
            var simpleType = ParseSimpleType();
            if (simpleType == null)
            {
                _position = before; // Reset position if both attempts failed
                ReportError("Expected AppClass or Builtin type as extending superclass.");
                    
            }
            return simpleType;

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
                if (builtInType.SourceSpan.Start.Index == 0 && builtInType.SourceSpan.End.Index == 0)
                {
                }
                return builtInType;
            }

            if (Check(TokenType.Array))
            {
                var arrayToken = Current;
                _position++;
                var arrayType = new ArrayTypeNode(1);
                arrayType.FirstToken = arrayToken;
                arrayType.LastToken = arrayToken;
                return arrayType;
            }

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

        // Try to parse as a builtin type using the type extension method
        try
        {
            var parsedType = BuiltinTypeExtensions.FromString(Current.Text);

            // If we get Unknown back, it's not a builtin type - fall through
            if (parsedType == PeopleCodeType.Unknown)
            {
                return null;
            }

            var token = Current;
            _position++;
            return new BuiltInTypeNode(parsedType)
            {
                FirstToken = token,
                LastToken = token
            };
        }
        catch
        {
            // Not a builtin type, fall through
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

            var type = ParseTypeReference();
            if (type == null)
            {
                ReportError("Expected type name after 'AS'");
                return null;
            }
            return type;
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
            var firstToken = Current;
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

            var methodNode = new MethodNode(methodName, Previous) { Visibility = visibility };
            methodNode.FirstToken = firstToken;
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
            methodNode.LastToken = Previous;

            /* Save it separately since we will overwrite the First/Last tokens when parsing the body */
            methodNode.HeaderSpan = new SourceSpan(methodNode.FirstToken.SourceSpan.Start, methodNode.LastToken.SourceSpan.End);
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
            var nameToken = Current;
            _position++;

            TypeNode? paramType = new BuiltInTypeNode(PeopleCodeType.Any)
            {
                FirstToken = Current,
                LastToken = Current
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
                    paramType = new BuiltInTypeNode(PeopleCodeType.Any)
                    {
                        FirstToken = nameToken,
                        LastToken = Previous
                    };
                }
            }

            var parameter = new ParameterNode(paramName, nameToken, paramType) { FirstToken = nameToken, LastToken = Previous };

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
    private PropertyNode? ParsePropertyDeclaration(VisibilityModifier visibility)
    {
        try
        {
            EnterRule("propertyDeclaration");

            // Capture the first token for positioning
            var firstToken = Current;
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

            var propertyNode = new PropertyNode(propertyName, Previous, propertyType) { Visibility = visibility };
            var lastToken = Previous; // Start with the property name as the last token

            // Parse property modifiers
            if (Match(TokenType.Get))
            {
                propertyNode.HasGet = true;
                lastToken = Previous; // Update last token

                if (Match(TokenType.Set))
                {
                    propertyNode.HasSet = true;
                    lastToken = Previous; // Update last token
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
                    lastToken = Previous; // Update last token
                }

                if (Match(TokenType.ReadOnly))
                {
                    propertyNode.IsReadOnly = true;
                    propertyNode.HasSet = false;
                    lastToken = Previous; // Update last token
                }
            }

            // Set the token positioning information for accurate SourceSpan calculation
            propertyNode.FirstToken = firstToken;
            propertyNode.LastToken = lastToken;

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
    private ProgramVariableNode? ParseInstanceDeclaration()
    {
        try
        {
            EnterRule("instanceDeclaration");

            // Capture the first token for positioning
            var firstToken = Current;
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
            var firstVarToken = Current;
            var lastToken = Current; // Track the last token for positioning
            _position++;

            var variableNode = new ProgramVariableNode(firstVarName, firstVarToken, variableType, VariableScope.Instance);
            variableNode.UpdateVariableNode(firstVarName, firstVarToken);

            // Parse additional variable names separated by commas
            while (Match(TokenType.Comma))
            {
                if (Check(TokenType.UserVariable))
                {
                    var additionalName = Current.Text;
                    var additionalToken = Current;
                    variableNode.AddNameWithToken(additionalName, additionalToken);
                    lastToken = Current; // Update last token as we parse more variables
                    _position++;
                }
                else
                {
                    break; // Trailing comma is allowed
                }
            }

            // Set the token positioning information for accurate SourceSpan calculation
            variableNode.FirstToken = firstToken;
            variableNode.LastToken = lastToken;

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

            /* try for simple/builtin type first */

            var result = ParseSimpleType();
            if (result != null)
            {
                return result;
            }

            // Try to parse as app class path, accept just a generic ID with no colon for classes
            // that are imported already. Tools allows you to just do Local ClassName &c = ... and it 
            // expands it on save.
            result = ParseAppClassPath();
            if (result != null)
            {
                return result;
            }

            ReportError($"Expected type reference.");
            return null;
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
                    elementType = new BuiltInTypeNode(PeopleCodeType.Any);
                }
            }

            var arrayNode = new ArrayTypeNode(dimensions, elementType);
            arrayNode.FirstToken = startToken;
            arrayNode.LastToken = elementType?.LastToken ?? Previous;
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
                elementType = new BuiltInTypeNode(PeopleCodeType.Any);
            }

            var arrayNode = new ArrayTypeNode(dimensions, elementType);
            arrayNode.FirstToken = startToken;
            arrayNode.LastToken = elementType?.LastToken ?? Previous;
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
            else if (Check(TokenType.GenericId))
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
                endToken = Previous; // Update end token to last parsed identifier
                if (nextId != null)
                {
                    pathParts.Add(nextId);
                }
                else
                {
                    break;
                }
            }

            // Last component is the class name, everything else is package path
            var className = pathParts[^1];
            var packagePath = pathParts.Take(pathParts.Count - 1);

            return new AppClassTypeNode(packagePath, className) { FirstToken = startToken, LastToken = endToken };
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
            if (true)
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

    private void AddToMatchingClassMember(AppClassNode appClass, AstNode node)
    {
        if (node is MethodImplNode methodImpl)
        {
            var matchingMethod = appClass.Methods.Where(m => m.Name == methodImpl.Name).FirstOrDefault();
            if (matchingMethod != null)
            {
                matchingMethod.SetImplementation(methodImpl);
            }
            else
            {
                ReportError($"Method implementation '{methodImpl.Name}' has no matching declaration");
                appClass.AddOrphanedMethodImplementation(methodImpl);
            }
        }

        else if (node is PropertyImplNode propImplNode)
        {
            var matchingProperty = appClass.Properties.Where(p => p.Name == propImplNode.Name).FirstOrDefault();
            if (matchingProperty != null)
            {
                if (propImplNode.IsGetter)
                {
                    matchingProperty.Getter = propImplNode;
                }
                else if (propImplNode.IsSetter)
                {
                    matchingProperty.SetSetterImplementation(propImplNode);
                }
            }
            else
            {
                ReportError($"Property getter '{propImplNode.Name}' has no matching declaration");
                appClass.AddOrphanedPropertyImplementation(propImplNode);
            }
        }
    }

    /// <summary>
    /// Parse class body and add members to existing app class (Phase 2 of app class parsing)
    /// </summary>
    private void ParseClassBody(AppClassNode appClass)
    {
        try
        {
            EnterRule("classBody");

            // Parse first class member
            var firstMember = ParseClassMember();

            if (firstMember != null)
            {
                AddToMatchingClassMember(appClass, firstMember);
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
                    AddToMatchingClassMember(appClass, member);
                }
                else
                {
                    // No more members or reached end
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing class body: {ex.Message}");
            PanicRecover(BlockSyncTokens);
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
            else if (Check(TokenType.Get) || Check(TokenType.Set))
            {
                return ParsePropertyImplementation();
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
    private MethodImplNode? ParseMethodImplementation()
    {
        try
        {
            EnterRule("methodImplementation");
            var firstToken = Current;
            if (!Match(TokenType.Method))
            {
                ReportError("Expected 'METHOD' keyword");
                return null;
            }

            /* Register the "get" statement #*/
            _workingProgram!.SetStatementNumber( firstToken.SourceSpan.Start.Line);


            // Parse method name
            var methodName = ParseGenericId();
            var nameToken = Previous;
            if (methodName == null)
            {
                ReportError("Expected method name after 'METHOD'");
                return null;
            }

            // Optional semicolons
            while (Match(TokenType.Semicolon)) { }

            // Track body start position (after annotations)
            var bodyStartToken = Current;

            // Create a temporary MethodNode for parsing annotations (for compatibility)
            var tempMethodNode = new MethodNode(methodName, nameToken);

            // Parse method annotations (parameter and return type annotations)
            ParseMethodAnnotations(tempMethodNode);

            // Update body start token after annotations
            bodyStartToken = Current;

            // Parse method body statements
            BlockNode body;
            if (!Check(TokenType.EndMethod))
            {
                body = ParseStatementList(TokenType.EndMethod);
            }
            else
            {
                /* Create an empty body node */
                body = new BlockNode();
            }

            body.RegisterStatementNumbers(this, _workingProgram!);


            var bodyEndToken = Previous;

            // Expect END-METHOD
            Consume(TokenType.EndMethod, "Expected 'END-METHOD' after method implementation");
            var lastToken = Previous;

            /* register the end-method if blank or last statement had semicolon */

            if (body.Statements.Count == 0 || body.Statements.Last().HasSemicolon)
            {
                _workingProgram!.SetStatementNumber( lastToken.SourceSpan.Start.Line);
            }

            // Create MethodImplNode with all the parsed information
            var methodImpl = new MethodImplNode(methodName, nameToken, body)
            {
                FirstToken = firstToken,
                LastToken = lastToken,
                BodyStartToken = bodyStartToken,
                BodyEndToken = bodyEndToken
            };

            // Transfer annotations from temp method node
            foreach (var paramAnnotation in tempMethodNode.ParameterAnnotations)
            {
                methodImpl.AddParameterAnnotation(paramAnnotation);
            }

            if (tempMethodNode.ReturnType != null)
            {
                methodImpl.SetReturnTypeAnnotation(tempMethodNode.ReturnType);
            }

            foreach (var implementedInterface in tempMethodNode.ImplementedInterfaces)
            {
                methodImpl.AddImplementedInterface(implementedInterface);
            }

            methodImpl.ImplementedMethodName = tempMethodNode.ImplementedMethodName;

            return methodImpl;
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
    private PropertyImplNode? ParsePropertyImplementation()
    {
        try
        {
            EnterRule("propertyImplementation");
            bool isGetter = Check(TokenType.Get);
            bool isSetter = Check(TokenType.Set);

            if (!isGetter && !isSetter)
            {
                ReportError("Expected 'GET' or 'SET' keyword");
                return null;
            }
            _position++;

            /* Register the "get"/"set" statement #*/
            _workingProgram!.SetStatementNumber( Previous.SourceSpan.Start.Line);

            // Parse property name
            var propertyName = ParseGenericId();
            if (propertyName == null)
            {
                ReportError("Expected property name after 'GET' or 'SET'");
                return null;
            }

            var propImplNode = new PropertyImplNode() { Name = propertyName, NameToken = Previous, IsGetter = isGetter, IsSetter = isSetter };
            // Parse method return annotation (contains the actual property type)
            // Try to parse a return annotation - it's fine if there isn't one
            int startPosition = _position;
            if (isGetter && !ParseMethodReturnAnnotation(propImplNode))
            {
                _position = startPosition;
            }

            startPosition = _position;
            if (isSetter && !ParseMethodParameterAnnotation(propImplNode))
            {
                _position = startPosition;
            }

            startPosition = _position;
            if (!ParsePropertyExtendsAnnotation(propImplNode))
            {
                _position = startPosition;
            }


            // Optional semicolons
            while (Match(TokenType.Semicolon)) { }

            // Parse getter body
            var getterBody = ParseStatementList(TokenType.EndGet,TokenType.EndSet);
            getterBody.RegisterStatementNumbers(this, _workingProgram!);
            propImplNode.SetBody(getterBody);

            // Expect END-GET
            if (isGetter)
            {
                Consume(TokenType.EndGet, "Expected 'END-GET' after getter implementation");
            } else if (isSetter)
            {
                Consume(TokenType.EndSet, "Expected 'END-SET' after setter implementation");
            }

            /* register the end-set if blank or last statement had semicolon */
            if (getterBody.Statements.Count == 0 || getterBody.Statements.Last().HasSemicolon)
            {
                _workingProgram!.SetStatementNumber(Previous.SourceSpan.Start.Line);
            }


            return propImplNode;
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

            /* for some reason PeopleCode allows a trailing ; after the annotations */
            while (Match(TokenType.Semicolon)) { }
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
                methodNode.AddParameterAnnotation(parameter);
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
    private bool ParseMethodParameterAnnotation(PropertyImplNode propImplNode)
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
            if (parameter != null)
            {
                propImplNode.AddParameterAnnotation(parameter);
            }
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
    private bool ParseMethodReturnAnnotation(PropertyImplNode getterNode)
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

            // Parse class type (app class path or built-in class)
            var classType = ParseAppClassPath() ?? ParseSimpleType();
            if (classType == null)
            {
                ReportError("Expected class name after 'IMPLEMENTS' in method annotation");
            }
            else
            {
                // Store the implemented interface in the method node
                methodNode.AddImplementedInterface(classType);
            }

            // Expect DOT
            if (!Match(TokenType.Dot))
            {
                ReportError("Expected '.' after app class path in method annotation");
            }

            // Expect genericID (method name in interface)
            var firstId = ParseGenericId();
            if (firstId != null)
            {
                string methodName = Current.Text;
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
    /// Parse method extends annotation: SLASH_PLUS EXTENDS DIV IMPLEMENTS appClassPath DOT genericID PLUS_SLASH
    /// </summary>
    /// <returns>True if an extends annotation was successfully parsed, false otherwise</returns>
    private bool ParsePropertyExtendsAnnotation(PropertyImplNode getterNode)
    {
        try
        {
            EnterRule("propertyExtendsAnnotations");

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

            // Parse class type (app class path or built-in class)
            var classType = ParseAppClassPath() ?? ParseSimpleType();
            if (classType == null)
            {
                ReportError("Expected class name after 'IMPLEMENTS' in method annotation");
            }
            else
            {
                // Store the implemented interface in the method node
                getterNode.SetImplementationType(classType);
            }

            // Expect DOT
            if (!Match(TokenType.Dot))
            {
                ReportError("Expected '.' after app class path in method annotation");
            }

            var firstId = ParseGenericId();
            if (firstId != null)
            {
                string propertyName = Current.Text;
                // Store the implemented method name in the property node
                getterNode.ImplementedPropertyName = propertyName;
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
            var startToken = Current;
            // Parse parameter name
            if (!Check(TokenType.UserVariable))
            {
                ReportError("Expected parameter name (&variable) in annotation");
                return null;
            }

            var paramName = Current.Text;
            var nameToken = Current;
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

            bool isOut = false;
            // Parse optional OUT modifier
            if (Match(TokenType.Out))
            {
                isOut = true;
            }

            var endToken = Previous;
            var parameter = new ParameterNode(paramName, nameToken, paramType)
            {
                FirstToken = startToken,
                LastToken = endToken,
                IsOut = isOut
            };

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
    private AppClassNode? ParseInterface()
    {
        try
        {
            EnterRule("interfaceDeclaration");
            var startToken = Current;
            if (!Match(TokenType.Interface))
                return null;

            var name = ParseGenericId();
            var nameToken = Previous;
            if (name == null)
            {
                ReportError("Expected interface name after 'INTERFACE'");
                return null;
            }
            var iface = new AppClassNode(name, nameToken, isInterface: true);

            // Optional EXTENDS base interface (apparently you can say implements here too...)
            if (Check(TokenType.Extends) || Check(TokenType.Implements))
            {
                _position++;
                var baseType = ParseAppClassPath() ?? ParseSimpleType();
                if (baseType != null)
                {
                    iface.SetBaseType(baseType);
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
            while (Match(TokenType.Semicolon)) { }
            iface.FirstToken = startToken;
            iface.LastToken = Previous;
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
    private void ParseInterfaceHeader(AppClassNode interfaceNode)
    {
        try
        {
            EnterRule("interfaceHeader");
            var hasEnteredProtected = false;
            // In interfaces, all methods are implicitly public
            // Parse method headers until END-INTERFACE
            while (!IsAtEnd && !Check(TokenType.EndInterface))
            {
                if (Check(TokenType.Method))
                {
                    var methodHeader = ParseMethodHeader(!hasEnteredProtected ? VisibilityModifier.Public : VisibilityModifier.Protected);
                    if (methodHeader is MethodNode methodNode)
                    {
                        // All interface methods are abstract by definition
                        methodNode.IsAbstract = true;

                        methodNode.IsConstructor = methodNode.Name == interfaceNode.Name;

                        interfaceNode.AddMember(methodNode, !hasEnteredProtected ? VisibilityModifier.Public : VisibilityModifier.Protected);
                    }

                    while (Match(TokenType.Semicolon)) { }
                }
                else if (Check(TokenType.Property))
                {
                    // Property declarations are allowed in all visibility sections
                    var propertyDeclaration = ParsePropertyDeclaration(VisibilityModifier.Public);
                    if (propertyDeclaration is PropertyNode propertyNode)
                    {
                        interfaceNode.AddMember(propertyNode, VisibilityModifier.Public);
                    }
                }
                else if (Match(TokenType.Semicolon))
                {
                    // Allow stray semicolons
                }
                else if (Match(TokenType.Protected))
                {
                    hasEnteredProtected = true;
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

            var firstToken = Current; // Capture the very first token

            // Handle optional prefixes (e.g., PEOPLECODE) before FUNCTION if present
            if (Match(TokenType.PeopleCode))
            {
                // Accept and continue; next should be FUNCTION
            }

            if (Match(TokenType.Declare))
            {
                // DECLARE FUNCTION variant (PEOPLECODE or LIBRARY)
                Consume(TokenType.Function, "Expected 'FUNCTION' after 'DECLARE'");

                var declName = ParseGenericId();
                var declNameToken = Previous;
                if (declName == null)
                {
                    ReportError("Expected function name after 'DECLARE FUNCTION'");
                    return null;
                }
                var declNode = new FunctionNode(declName, Previous, FunctionType.UserDefined);

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
                    declNode = new FunctionNode(declName, declNameToken, FunctionType.PeopleCode)
                    {
                        RecordName = declNode.RecordName,
                        FieldName = declNode.FieldName,
                        RecordEvent = declNode.RecordEvent,
                        ReturnType = declNode.ReturnType,
                        FirstToken = firstToken,
                        LastToken = Previous
                    };
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

                    declNode = new FunctionNode(declName, declNameToken, FunctionType.Library)
                    {
                        LibraryName = declNode.LibraryName,
                        AliasName = declNode.AliasName,
                        ReturnType = declNode.ReturnType,
                        FirstToken = firstToken,
                        LastToken = Previous
                    };
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
            var functionName = ParseGenericId();
            if (functionName == null)
            {
                ReportError("Expected function name after 'FUNCTION'");
                return null;
            }

            var functionNode = new FunctionNode(functionName, Previous, FunctionType.UserDefined);

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
                    while (Match(TokenType.Comma) && !Check(TokenType.RightParen)); // Allow trailing comma
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

            // Handle semicolons (SEMI*) 
            while (Match(TokenType.Semicolon)) { }

            // Function definition body until END-FUNCTION
            var body = ParseStatementList(TokenType.EndFunction);
            Consume(TokenType.EndFunction, "Expected 'END-FUNCTION' after function body");
            var lastToken = Previous; // Capture the END-FUNCTION token

            while (Match(TokenType.Semicolon))
            {
                lastToken = Previous; // Update to semicolon if present
            }

            functionNode.SetBody(body);
            functionNode.FirstToken = firstToken;
            functionNode.LastToken = lastToken;
            functionNode.RegisterStatementNumbers(this, _workingProgram!);
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

                // Skip to next comma or closing parenthesis
                while (!IsAtEnd && !(Check(TokenType.Comma) || Check(TokenType.RightParen)))
                    _position++;

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

            var paramName = ParseGenericId();
            var nameToken = Previous;
            // Parse parameter name (must be a generic ID)
            if (paramName == null)
            {
                ReportError("Expected parameter name in DLL argument");
                return null;
            }


            // Create parameter with default type (Any)
            var parameter = new ParameterNode(paramName, nameToken, new BuiltInTypeNode(PeopleCodeType.Any));

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
                    if (builtInType.SourceSpan.Start.Index == 0 && builtInType.SourceSpan.End.Index == 0)
                    {
                    }
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
                    if (builtInType.SourceSpan.Start.Index == 0 && builtInType.SourceSpan.End.Index == 0)
                    {
                    }
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
                var builtInType = TryParseBuiltInType();


                /* We can have an "As" here in the case where someone does 
                 * Returns integer as number; */

                if (Check(TokenType.As))
                {
                    _position++;
                    builtInType = TryParseBuiltInType();
                }

                return builtInType;
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

        // Parse record name using ParseGenericId to allow keywords like STEP
        var tempRecordName = ParseGenericId();
        if (tempRecordName == null) return false;
        recordName = tempRecordName;

        if (!Match(TokenType.Dot)) return false;

        // Parse field name using ParseGenericId to allow keywords like STEP
        var tempFieldName = ParseGenericId();
        if (tempFieldName == null) return false;
        fieldName = tempFieldName;

        return true;
    }

    /// <summary>
    /// Parse program variable declaration according to grammar:
    /// nonLocalVarDeclaration: (COMPONENT | GLOBAL) typeT USER_VARIABLE (COMMA USER_VARIABLE)* COMMA?
    ///                       | (COMPONENT | GLOBAL) typeT  // compiles yet is meaningless
    /// Note: LOCAL variables in preamble are handled by ParseLocalVariableDeclaration()
    /// </summary>
    private ProgramVariableNode? ParseVariableDeclaration()
    {
        try
        {
            EnterRule("nonLocalVarDeclaration");

            // Capture the first token for positioning
            var firstToken = Current;
            VariableScope scope;
            if (Match(TokenType.Global)) scope = VariableScope.Global;
            else if (Match(TokenType.Component)) scope = VariableScope.Component;
            else if (Match(TokenType.Local))
            {
                // LOCAL variables should be handled separately as LocalVariableDeclarationNode
                // Rewind and return null to let ParseLocalVariableDeclaration handle it
                _position--;
                return null;
            }
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
                var emptyVariable = new ProgramVariableNode("", Peek(), varType, scope);
                // Set token positioning for empty variable
                emptyVariable.FirstToken = firstToken;
                emptyVariable.LastToken = Previous; // Last token consumed was the type

                Match(TokenType.Semicolon); // optional
                return emptyVariable;
            }

            var firstName = Current.Text;
            var firstNameToken = Current;
            var lastToken = Current; // Track the last token for positioning
            _position++;

            var variable = new ProgramVariableNode(firstName, firstNameToken, varType, scope);
            variable.UpdateVariableNode(firstName, firstNameToken);

            // Additional names
            while (Match(TokenType.Comma))
            {
                if (Check(TokenType.UserVariable))
                {
                    var additionalName = Current.Text;
                    var additionalNameToken = Current;
                    variable.AddNameWithToken(additionalName, additionalNameToken);
                    lastToken = Current; // Update last token as we parse more variables
                    _position++;
                }
                else
                {
                    break; // tolerate trailing comma
                }
            }

            if (Check(TokenType.Equal))
            {
                /* This isn't a declaration that belongs in the preamble */
                return null;
            }

            // Set the token positioning information for accurate SourceSpan calculation
            variable.FirstToken = firstToken;
            variable.LastToken = lastToken;

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
            var nameToken = Current;
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
                return new ConstantNode(name, nameToken, valueExpr)
                {
                    FirstToken = nameToken,
                    LastToken = nameToken
                };
            }

            // Parse literal value
            var literalValue = ParseLiteral();
            if (literalValue == null)
            {
                ReportError("Expected literal value for constant");
                return null;
            }

            return new ConstantNode(name, nameToken, literalValue)
            {
                FirstToken = nameToken,
                LastToken = literalValue.LastToken
            };
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
            var startToken = Current; // Capture the starting token 
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
                // Set the HasSemicolon flag if a semicolon is present
                statement.HasSemicolon = Match(TokenType.Semicolon);

                // Consume any additional semicolons (allowed by the grammar)
                while (Match(TokenType.Semicolon))
                {
                    // Each additional semicolon is just ignored
                }
                var endToken = statement.LastToken ?? Previous;
                statement.FirstToken = startToken;
                statement.LastToken = endToken;
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
            var ifToken = Current; // Capture IF token for structural error reporting

            if (!Match(TokenType.If))
                return null;

            var condition = ParseExpressionForCondition();
            if (condition == null)
            {
                ReportError("Expected condition after 'IF'");

                // Error recovery: try to synchronize to THEN token
                if (SynchronizeToToken(TokenType.Then))
                {
                    // Create a placeholder condition so we can continue parsing the THEN block
                    condition = new LiteralNode(true, LiteralType.Boolean)
                    {
                        FirstToken = Current,
                        LastToken = Current
                    };
                }
                else
                {
                    // No THEN found, cannot recover - highlight the IF token
                    ReportError("IF statement is missing 'THEN' - cannot recover", ifToken);
                    return null;
                }
            }

            if (!Match(TokenType.Then))
            {
                ReportError("Expected 'THEN' after IF condition");

                // Error recovery: try to synchronize to THEN token
                if (!SynchronizeToToken(TokenType.Then))
                {
                    // No THEN found - highlight the IF token for structure error
                    ReportError("IF statement is missing 'THEN' token", ifToken);
                }
            }

            // Handle optional semicolons after THEN (SEMI*)
            while (Match(TokenType.Semicolon)) { }

            var thenStatements = ParseStatementList(TokenType.EndIf, TokenType.Else);

            BlockNode? elseStatements = null;
            Token? elseToken = null;
            if (Match(TokenType.Else))
            {
                elseToken = Previous;

                // Handle optional semicolons after ELSE (SEMI*)
                while (Match(TokenType.Semicolon)) { }

                elseStatements = ParseStatementList(TokenType.EndIf);
            }

            // Use custom logic instead of Consume to highlight IF token for missing END-IF
            if (!Match(TokenType.EndIf))
            {
                ReportError("IF statement is missing 'END-IF'", ifToken);
                // Use smart recovery to find next statement boundary, not just END-IF
                SmartStatementRecover();
            }

            var ifNode = new IfStatementNode(condition, thenStatements);
            if (elseToken != null && elseStatements != null)
            {
                ifNode.SetElseBlock(elseToken, elseStatements);
            }
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
            var forToken = Current; // Capture FOR token for structural error reporting

            if (!Match(TokenType.For))
                return null;

            // Parse iterator: USER_VARIABLE or RECORD.FIELD
            ExpressionNode? iterator = null;
            var iteratorToken = Current;

            if (Check(TokenType.UserVariable))
            {
                // User variable: &i
                var variableName = Current.Value?.ToString() ?? "";
                iterator = new IdentifierNode(variableName, IdentifierType.UserVariable);
                _position++;
            }
            else if (Check(TokenType.GenericId))
            {
                // Try RECORD.FIELD pattern
                var recordName = ParseGenericId();
                if (recordName != null && Match(TokenType.Dot))
                {
                    var fieldName = ParseGenericId();
                    if (fieldName != null)
                    {
                        var recordNode = new IdentifierNode(recordName, IdentifierType.Generic);
                        iterator = new MemberAccessNode(recordNode, fieldName, Previous.SourceSpan);
                    }
                    else
                    {
                        ReportError("Expected field name after '.' in FOR statement");
                        return null;
                    }
                }
                else
                {
                    ReportError("FOR statement requires user variable (&var) or record.field");
                    return null;
                }
            }
            else
            {
                ReportError("Expected user variable or record.field after 'FOR'");
                return null;
            }

            if (iterator == null)
            {
                ReportError("Failed to parse FOR iterator");
                return null;
            }

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

            // Use custom logic instead of Consume to highlight FOR token for missing END-FOR
            if (!Match(TokenType.EndFor))
            {
                ReportError("FOR statement is missing 'END-FOR'", forToken);
                // Use smart recovery to find next statement boundary, not just END-FOR
                SmartStatementRecover();
            }

            var forNode = new ForStatementNode(iterator, iteratorToken, start, end, body);
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
            var whileToken = Current; // Capture WHILE token for structural error reporting

            if (!Match(TokenType.While))
                return null;

            var condition = ParseExpressionForCondition();
            if (condition == null)
            {
                ReportError("Expected condition after 'WHILE'");
                return null;
            }

            // Handle optional semicolons after condition (SEMI*)
            while (Match(TokenType.Semicolon)) { }

            var body = ParseStatementList(TokenType.EndWhile);

            // Use custom logic instead of Consume to highlight WHILE token for missing END-WHILE
            if (!Match(TokenType.EndWhile))
            {
                ReportError("WHILE statement is missing 'END-WHILE'", whileToken);
                // Use smart recovery to find next statement boundary, not just END-WHILE
                SmartStatementRecover();
            }

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

            var condition = ParseExpressionForCondition();
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
            var tryStartToken = Current; // Capture the starting token
            if (!Match(TokenType.Try))
                return null;

            // Handle optional semicolons after TRY (SEMI*)
            while (Match(TokenType.Semicolon)) { }

            var tryBlock = ParseStatementList(TokenType.Catch, TokenType.EndTry);

            var tryEndToken = Previous; // Capture the last token of the try block
            List<CatchStatementNode> catchClauses = new();
            while (Match(TokenType.Catch))
            {
                var catchStartToken = Previous;
                // According to grammar: CATCH (EXCEPTION | appClassPath) USER_VARIABLE SEMI* statementBlock?
                TypeNode? exceptionType = null;

                // Parse exception type (EXCEPTION or appClassPath)
                if (Match(TokenType.Exception))
                {
                    exceptionType = new BuiltInTypeNode(PeopleCodeType.Exception);
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
                CatchStatementNode? catchNode = null;
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
                    catchNode = new CatchStatementNode(exceptionVariable, catchBlock, exceptionType);
                    catchClauses.Add(catchNode);
                }

                // Handle optional semicolons between catch clauses (SEMI*)
                while (Match(TokenType.Semicolon)) { }
                var catchEndToken = Previous; // Capture the last token of the catch clause
                if (catchNode != null)
                {
                    catchNode.FirstToken = catchStartToken;
                    catchNode.LastToken = catchEndToken;
                }
            }

            // Handle optional semicolons before END-TRY (SEMI*)
            while (Match(TokenType.Semicolon)) { }

            // Use custom logic instead of Consume to highlight TRY token for missing END-TRY
            if (!Match(TokenType.EndTry))
            {
                ReportError("TRY statement is missing 'END-TRY'", tryStartToken);
                // Use smart recovery to find next statement boundary, not just END-TRY
                SmartStatementRecover();
            }
            
            return new TryStatementNode(tryBlock, catchClauses)
            {
                FirstToken = tryStartToken,
                LastToken = Previous
            };
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
        if (!Check(TokenType.Semicolon) && !StatementSyncTokens.Contains(Current.Type) && !BlockSyncTokens.Contains(Current.Type))
        {
            value = ParseExpression();
        }
        
        return new ReturnStatementNode(value);
    }

    private StatementNode? ParseBreakStatement()
    {
        if (!Check(TokenType.Break))
            return null;

        var token = Current;
        _position++;
        return new BreakStatementNode()
        {
            FirstToken = token,
            LastToken = token
        };
    }

    private StatementNode? ParseContinueStatement()
    {
        if (!Check(TokenType.Continue))
            return null;

        var token = Current;
        _position++;
        return new ContinueStatementNode()
        {
            FirstToken = token,
            LastToken = token
        };
    }

    private StatementNode? ParseExitStatement()
    {
        if (!Check(TokenType.Exit))
            return null;

        var token = Current;
        _position++;

        ExpressionNode? exitCode = null;
        if (Check(TokenType.LeftParen))
        {
            exitCode = ParseExpression();
        } else
        {
            if (StatementSyncTokens.Contains(Current.Type) || BlockSyncTokens.Contains(Current.Type) || Check(TokenType.Semicolon))
            {
                /* leave early since it looks like the next thing isn't an expression? */
                return new ExitStatementNode(exitCode)
                {
                    FirstToken = token,
                    LastToken = token
                };
            }

            var backup = _position;

            exitCode = ParseExpression();
            if (exitCode == null)
            {
                _position = backup;
                exitCode = null;
            }
        }

        return new ExitStatementNode(exitCode)
        {
            FirstToken = token,
            LastToken = token
        };


    }

    private StatementNode? ParseErrorStatement()
    {
        if (!Check(TokenType.Error))
            return null;

        var firstToken = Current;
        _position++;

        var message = ParseExpression();
        if (message == null)
        {
            ReportError("Expected message after 'ERROR'");
            message = new LiteralNode("Error", LiteralType.String);
        }

        return new ErrorStatementNode(message)
        {
            FirstToken = firstToken,
            LastToken = message.LastToken ?? firstToken
        };
    }

    private StatementNode? ParseWarningStatement()
    {
        if (!Check(TokenType.Warning))
            return null;

        var firstToken = Current;
        _position++;

        var message = ParseExpression();
        if (message == null)
        {
            ReportError("Expected message after 'WARNING'");
            message = new LiteralNode("Warning", LiteralType.String);
        }

        return new WarningStatementNode(message)
        {
            FirstToken = firstToken,
            LastToken = message.LastToken ?? firstToken
        };
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
            var localToken = Current; // Capture LOCAL token for range-based error reporting

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
                // Highlight from LOCAL token to end of type for incomplete declaration
                ReportError("Expected variable name (&variable) after type", localToken, Previous);
                return null;
            }

            var firstVariableName = Current.Text;
            var firstVarToken = Current;
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

                var nameInfo = new VariableNameInfo(firstVariableName, firstVarToken);
                var node = new LocalVariableDeclarationWithAssignmentNode(variableType, nameInfo, initialValue) { FirstToken = localToken, LastToken = Previous };
                return node;
            }
            else
            {
                // This is localVariableDefinition: LOCAL type &var1, &var2, ...
                var node = new LocalVariableDeclarationNode(variableType, new List<(string, Token)> { (firstVariableName, firstVarToken) }) { FirstToken = localToken };

                // Parse additional variable names separated by commas
                while (Match(TokenType.Comma))
                {
                    if (Check(TokenType.UserVariable))
                    {
                        var additionalName = Current.Text;
                        var additionalToken = Current;
                        node.AddVariableNameWithToken(additionalName, additionalToken);
                        _position++;
                    }
                    else
                    {
                        // Trailing comma is allowed, so this is OK
                        break;
                    }
                }
                node.LastToken = Previous;
                return node;
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
        var firstToken = Current;
        if (endTokens.Contains(Current.Type))
        {
            /* handle empty blocks */
            var prevSpan = Previous.SourceSpan;
            block.SourceSpan = new SourceSpan(prevSpan.End.Index,prevSpan.End.ByteIndex,prevSpan.End.Index, prevSpan.End.ByteIndex, prevSpan.End.Line, prevSpan.End.Column, prevSpan.End.Line, prevSpan.End.Column);
            return block;
        }


        while (!IsAtEnd && !endTokens.Contains(Current.Type))
        {
            var statementStartToken = Current; // Capture the token where the statement attempt begins
            var statement = ParseStatement();
            if (statement != null)
            {
                block.AddStatement(statement);
            }
            else
            {
                // Smart recovery: try to synchronize to next statement boundary
                // Note: ParseStatement() should have already reported specific parsing errors
                if (SmartStatementRecover())
                {
                    // Successfully found a statement boundary, continue parsing from here
                    continue;
                }
                else
                {
                    // Recovery failed, advance one token to prevent infinite loop
                    _position++;
                }
            }
        }
        block.FirstToken = firstToken;
        block.LastToken = Previous;
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
            var evaluateToken = Current; // Capture EVALUATE token for structural error reporting

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
                    /* You are allowed to have any number of "Not" operators before the BinaryOperator */
                    int notCount = 0;
                    while(Check(TokenType.Not))
                    {
                        _position++;
                        notCount++;
                    }

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

                    /* Invert the operator for any Not's that were present */
                    if (op.HasValue)
                    {
                        for (int i = 0; i < notCount; i++)
                        {
                            op = op.Value.InvertRelop();
                        }
                    }

                    // Required single expression
                    var condition = ParseExpressionForCondition();
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
                    var whenOtherToken = Previous;
                    // SEMI*
                    while (Match(TokenType.Semicolon)) { }

                    var otherBody = ParseStatementList(TokenType.EndEvaluate);
                    evalNode.SetWhenOtherBlock(whenOtherToken, otherBody);
                }
                else
                {
                    // Unexpected token; sync to next boundary
                    ReportError($"Unexpected token in EVALUATE: {Current.Type}");
                    while (!IsAtEnd && !(Check(TokenType.When) || Check(TokenType.WhenOther) || Check(TokenType.EndEvaluate)))
                        _position++;
                }
            }

            // Use custom logic instead of Consume to highlight EVALUATE token for missing END-EVALUATE
            if (!Match(TokenType.EndEvaluate))
            {
                ReportError("EVALUATE statement is missing 'END-EVALUATE'", evaluateToken);
                // Use smart recovery to find next statement boundary, not just END-EVALUATE
                SmartStatementRecover();
            }
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
    /// Parse expression in condition context (comparisons, not assignments)
    /// </summary>
    private ExpressionNode? ParseExpressionForCondition()
    {
        try
        {
            EnterRule("condition_expression");
            return ParseOrExpression(allowAssignmentEqual: false);
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing condition expression: {ex.Message}");
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
        var expr = ParseOrExpression(allowAssignmentEqual: true);
        if (expr == null) return null;

        if (Current.Type.IsAssignmentOperator())
        {
            var op = GetAssignmentOperator(Current.Type);
            var opToken = Current;
            _position++;

            if (op is AssignmentOperator.AddAssign ||
                    op is AssignmentOperator.SubtractAssign ||
                    op is AssignmentOperator.ConcatenateAssign)
            {
                /* The parser gets triggered as soon as these are entered, so you will almost *never* have a valid right hand expression here.*/
                /* We shouldn't fail the parse here and should make an empty expression for the right side so we can still return an assignment */
                /* node. */
                return new PartialShortHandAssignmentNode(expr, op) { FirstToken = expr.FirstToken, LastToken = opToken };
            }

            var right = ParseAssignmentExpression(); // Right associative - allow assignment equals in right side too
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
                FirstToken = expr.FirstToken,
                LastToken = right.LastToken
            };
        }

        return expr;
    }

    /// <summary>
    /// Parse logical OR expressions
    /// </summary>
    private ExpressionNode? ParseOrExpression(bool allowAssignmentEqual = false)
    {
        var left = ParseAndExpression(allowAssignmentEqual);
        if (left == null) return null;

        while (Match(TokenType.Or))
        {
            var right = ParseAndExpression(allowAssignmentEqual);
            if (right == null)
            {
                ReportError("Expected expression after 'OR'");
                break;
            }

            left = new BinaryOperationNode(left, BinaryOperator.Or, false, right)
            {
                FirstToken = left.FirstToken,
                LastToken = right.LastToken
            };
        }

        return left;
    }

    /// <summary>
    /// Parse logical AND expressions
    /// </summary>
    private ExpressionNode? ParseAndExpression(bool allowAssignmentEqual = false)
    {
        var left = ParseEqualityExpression(allowAssignmentEqual);
        if (left == null) return null;

        while (Match(TokenType.And))
        {
            var right = ParseEqualityExpression(allowAssignmentEqual);
            if (right == null)
            {
                ReportError("Expected expression after 'AND'");
                break;
            }

            left = new BinaryOperationNode(left, BinaryOperator.And, false, right)
            {
                FirstToken = left.FirstToken,
                LastToken = right.LastToken
            };
        }

        return left;
    }

    /// <summary>
    /// Parse equality expressions (=, <>, !=)
    /// </summary>
    private ExpressionNode? ParseEqualityExpression(bool allowAssignmentEqual = false)
    {
        var left = ParseRelationalExpression(allowAssignmentEqual);
        if (left == null) return null;

        bool notFlag = false;
        if (Check(TokenType.Not))
        {
            notFlag = true;
            _position++;
        }

        // If we're allowing assignment equals, don't consume TokenType.Equal as comparison
        while ((allowAssignmentEqual && Current.Type == TokenType.NotEqual) || 
               (!allowAssignmentEqual && Current.Type is TokenType.Equal or TokenType.NotEqual))
        {
            var op = Current.Type == TokenType.Equal ? BinaryOperator.Equal : BinaryOperator.NotEqual;
            _position++;

            var right = ParseRelationalExpression(allowAssignmentEqual);
            if (right == null)
            {
                ReportError("Expected expression after equality operator");
                break;
            }

            left = new BinaryOperationNode(left, op, notFlag, right)
            {
                FirstToken = left.FirstToken,
                LastToken = right.LastToken
            };
        }

        return left;
    }

    /// <summary>
    /// Parse relational expressions (<, <=, >, >=)
    /// </summary>
    private ExpressionNode? ParseRelationalExpression(bool allowAssignmentEqual = false)
    {
        var left = ParseTypeCastExpression(allowAssignmentEqual);
        if (left == null) return null;

        bool notFlag = false;
        if (Check(TokenType.Not))
        {
            notFlag = true;
            _position++;
        }

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

            left = new BinaryOperationNode(left, op, notFlag, right)
            {
                FirstToken = left.FirstToken,
                LastToken = right.LastToken
            };
        }

        return left;
    }

    /// <summary>
    /// Parse type cast expressions (expr AS Type)
    /// </summary>
    private ExpressionNode? ParseTypeCastExpression(bool allowAssignmentEqual = false)
    {
        try
        {
            EnterRule("typeCastExpression");

            var expr = ParseConcatenationExpression(allowAssignmentEqual);
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
                    FirstToken = expr.FirstToken,
                    LastToken = typeSpec.LastToken
                };
            }

            return expr;
        }
        catch (Exception ex)
        {
            ReportError($"Error parsing type cast expression: {ex.Message}");
            // Return null to break recursion instead of calling ParseConcatenationExpression()
            return null;
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse string concatenation expressions (|)
    /// </summary>
    private ExpressionNode? ParseConcatenationExpression(bool allowAssignmentEqual = false)
    {
        var left = ParseNotExpression(allowAssignmentEqual);
        if (left == null) return null;

        while (Match(TokenType.Pipe))
        {
            var right = ParseNotExpression(allowAssignmentEqual);
            if (right == null)
            {
                ReportError("Expected expression after '|'");
                break;
            }

            left = new BinaryOperationNode(left, BinaryOperator.Concatenate, false, right)
            {
                FirstToken = left.FirstToken,
                LastToken = right.LastToken
            };
        }

        return left;
    }

    /// <summary>
    /// Parse NOT expressions (NOT expression)
    /// </summary>
    private ExpressionNode? ParseNotExpression(bool allowAssignmentEqual = false)
    {
        if (Check(TokenType.Not))
        {
            var notToken = Current;
            _position++; // Advance past NOT token

            var operand = ParseAdditiveExpression(allowAssignmentEqual);
            if (operand == null)
            {
                ReportError("Expected expression after 'NOT'");
                // Error recovery for NOT expressions - sync to appropriate expression boundaries
                var syncTokens = new HashSet<TokenType> {
                    TokenType.Semicolon, TokenType.Then, TokenType.EndIf,
                    TokenType.And, TokenType.Or, TokenType.RightParen,
                    TokenType.RightBracket, TokenType.Comma
                };
                PanicRecover(syncTokens);
                return null;
            }

            return new UnaryOperationNode(UnaryOperator.Not, operand)
            {
                FirstToken = notToken,
                LastToken = operand.LastToken
            };
        }

        return ParseAdditiveExpression(allowAssignmentEqual);
    }

    /// <summary>
    /// Parse additive expressions (+, -)
    /// </summary>
    private ExpressionNode? ParseAdditiveExpression(bool allowAssignmentEqual = false)
    {
        var left = ParseMultiplicativeExpression(allowAssignmentEqual);
        if (left == null) return null;

        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Current.Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
            _position++;

            var right = ParseMultiplicativeExpression(allowAssignmentEqual);
            if (right == null)
            {
                ReportError("Expected expression after additive operator");
                break;
            }

            left = new BinaryOperationNode(left, op, false, right)
            {
                FirstToken = left.FirstToken,
                LastToken = right.LastToken
            };
        }

        return left;
    }

    /// <summary>
    /// Parse multiplicative expressions (*, /)
    /// </summary>
    private ExpressionNode? ParseMultiplicativeExpression(bool allowAssignmentEqual = false)
    {
        var left = ParseExponentialExpression(allowAssignmentEqual);
        if (left == null) return null;

        while (Current.Type is TokenType.Star or TokenType.Div)
        {
            var op = Current.Type == TokenType.Star ? BinaryOperator.Multiply : BinaryOperator.Divide;
            _position++;

            var right = ParseExponentialExpression(allowAssignmentEqual);
            if (right == null)
            {
                ReportError("Expected expression after multiplicative operator");
                break;
            }

            left = new BinaryOperationNode(left, op, false, right)
            {
                FirstToken = left.FirstToken,
                LastToken = right.LastToken
            };
        }

        return left;
    }

    /// <summary>
    /// Parse exponential expressions (**)
    /// </summary>
    private ExpressionNode? ParseExponentialExpression(bool allowAssignmentEqual = false)
    {
        var left = ParseUnaryExpression(allowAssignmentEqual);
        if (left == null) return null;

        if (Match(TokenType.Power))
        {
            // Right associative
            var right = ParseExponentialExpression(allowAssignmentEqual);
            if (right == null)
            {
                ReportError("Expected expression after '**'");
                return left;
            }

            return new BinaryOperationNode(left, BinaryOperator.Power, false, right)
            {
                FirstToken = left.FirstToken,
                LastToken = right.LastToken
            };
        }

        return left;
    }

    /// <summary>
    /// Parse unary expressions (-, @)
    /// </summary>
    private ExpressionNode? ParseUnaryExpression(bool allowAssignmentEqual = false)
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

            var operand = ParseUnaryExpression(allowAssignmentEqual); // Right associative
            if (operand == null)
            {
                ReportError("Expected expression after unary operator");
                return null;
            }

            return new UnaryOperationNode(op, operand)
            {
                FirstToken = opToken,
                LastToken = operand.LastToken
            };
        }

        return ParsePostfixExpression(allowAssignmentEqual);
    }

    /// <summary>
    /// Parse postfix expressions (function calls, array access, property access)
    /// </summary>
    private ExpressionNode? ParsePostfixExpression(bool allowAssignmentEqual = false)
    {
        var expr = ParsePrimaryExpression(allowAssignmentEqual);
        if (expr == null) return null;

        // Defensive validation: ensure primary expression has token boundaries
        expr = EnsureTokenBoundaries(expr, Current, Current);
        if (expr?.FirstToken == null || expr?.LastToken == null)
        {
            ReportError("Internal error: Primary expression missing token boundaries");
            return expr;
        }

        while (true)
        {
            if (Match(TokenType.LeftBracket))
            {
                // Array access - supports both single index and comma-separated indices
                var indices = ParseArrayIndices();
                Consume(TokenType.RightBracket, "Expected ']' after array index");

                expr = new ArrayAccessNode(expr, indices)
                {
                    FirstToken = expr.FirstToken,
                    LastToken = Previous
                };
            }
            else if (Match(TokenType.LeftParen))
            {
                // Check for empty function call first (no arguments)
                if (Check(TokenType.RightParen))
                {
                    // Empty function call like CreateArray()
                    var args = new List<ExpressionNode>();
                    Consume(TokenType.RightParen, "Expected ')' after function arguments");

                    expr = new FunctionCallNode(expr, args)
                    {
                        FirstToken = expr.FirstToken,
                        LastToken = Previous
                    };
                }
                else
                {
                    // Function call - PeopleCode doesn't have implicit subindex expressions
                    // Array access uses [] brackets, function calls use () parentheses
                    var args = ParseArgumentList();
                    Consume(TokenType.RightParen, "Expected ')' after function arguments");

                    expr = new FunctionCallNode(expr, args)
                    {
                        FirstToken = expr.FirstToken,
                        LastToken = Previous
                    };
                }
            }
            else if (Match(TokenType.Dot))
            {
                // Property/method access
                var member = ParseGenericId();
                var memberNameSpan = Previous.SourceSpan;
                if (member != null)
                {
                    var memberToken = Previous; // Previous token after ParseGenericId consumed it

                    // Create member access node
                    expr = new MemberAccessNode(expr, member, memberNameSpan)
                    {
                        FirstToken = expr.FirstToken,
                        LastToken = memberToken
                    };

                    // Check for method call after dot access
                    if (Match(TokenType.LeftParen))
                    {
                        // Method call
                        var args = ParseArgumentList();
                        Consume(TokenType.RightParen, "Expected ')' after method arguments");

                        expr = new FunctionCallNode(expr, args)
                        {
                            FirstToken = expr.FirstToken,
                            LastToken = Previous
                        };
                    }
                }
                else if (Check(TokenType.StringLiteral))
                {
                    // Dynamic member access with string
                    var stringMember = Current.Value?.ToString() ?? "";
                    var memberToken = Current;
                    _position++;

                    expr = new MemberAccessNode(expr, stringMember, memberToken.SourceSpan, isDynamic: true)
                    {
                        FirstToken = expr.FirstToken,
                        LastToken = memberToken
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
    /// Check if current position has a colon-separated identifier pattern (Class:Constant)
    /// </summary>
    private bool IsColonSeparatedIdentifier()
    {
        // Must be: Identifier : Identifier
        if (!(Current.Type.IsIdentifier() && Peek().Type == TokenType.Colon))
            return false;

        // Check if the token after the colon is an identifier
        if (_position + 2 >= _tokens.Count)
            return false;

        var afterColon = _tokens[_position + 2];
        return afterColon.Type.IsIdentifier();
    }

    /// <summary>
    /// Parse class constant expression (ClassName:ConstantName)
    /// </summary>
    private ClassConstantNode? ParseClassConstant()
    {
        try
        {
            EnterRule("classConstant");

            var startToken = Current;

            // Parse class name
            var className = ParseGenericId();
            if (className == null)
            {
                ReportError("Expected class name in class constant");
                return null;
            }

            // Consume colon
            if (!Match(TokenType.Colon))
            {
                ReportError("Expected ':' after class name in class constant");
                return null;
            }

            // Parse constant name
            var constantName = ParseGenericId();
            if (constantName == null)
            {
                ReportError("Expected constant name after ':' in class constant");
                return null;
            }

            var endToken = Previous;

            return new ClassConstantNode(className, constantName)
            {
                FirstToken = startToken,
                LastToken = endToken
            };
        }
        finally
        {
            ExitRule();
        }
    }

    /// <summary>
    /// Parse primary expressions (literals, identifiers, parenthesized expressions)
    /// </summary>
    private ExpressionNode? ParsePrimaryExpression(bool allowAssignmentEqual = false)
    {
        // Interpolated strings (handle before general literals)
        if (Current.Type == TokenType.InterpStringStart)
        {
            return ParseInterpolatedString();
        }

        // Literals
        if (Current.Type.IsLiteral())
        {
            return ParseLiteral();
        }

        // Class constants (ClassName:ConstantName) - must come before regular identifiers
        if (IsColonSeparatedIdentifier())
        {
            return ParseClassConstant();
        }

        // Identifiers
        if (Current.Type.IsIdentifier())
        {
            return ParseIdentifier();
        }

        // Special case for %Super (TokenType.Super)
        if (Current.Type == TokenType.Super)
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
                FirstToken = token,
                LastToken = token
            };
        }

        // Parenthesized expression
        if (Match(TokenType.LeftParen))
        {
            var leftParenToken = Previous; // Capture opening paren
            // Parentheses always create condition context (comparisons, not assignments)
            var expr = ParseOrExpression(allowAssignmentEqual: false);
            if (expr == null)
            {
                ReportError("Expected expression inside parentheses");
            }

            Consume(TokenType.RightParen, "Expected ')' after expression");
            var rightParenToken = Previous; // Capture closing paren

            return new ParenthesizedExpressionNode(expr ?? CreateErrorExpression())
            {
                FirstToken = leftParenToken,
                LastToken = rightParenToken
            };
        }

        // Object creation
        if (Match(TokenType.Create))
        {
          if (Check(TokenType.LeftParen) && Peek().Type == TokenType.RightParen)
            {
                /* We have a create shorthand! */
                var shortHandNode = new ObjectCreateShortHand() { FirstToken = Previous, LastToken = Peek(1) };
                
                /* Skip the right paren */
                _position += 2;
                return shortHandNode;
            }
            else
            {
                return ParseObjectCreation();
            }
        }

        // App class path (metadata expression)
        if ((Check(TokenType.Metadata) || Check(TokenType.GenericId)) && Peek().Type == TokenType.Colon)
        {
            var appClassPath = ParseAppClassPath();
            if (appClassPath != null)
            {
                return new MetadataExpressionNode(appClassPath)
                {
                    FirstToken = appClassPath.FirstToken,
                    LastToken = appClassPath.LastToken
                };
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
            || type == TokenType.Float
            || type == TokenType.Component;
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
            var positionBackup = _position;
            var arg = ParseExpression();

            /* Heuristic to try and handle when we are parsing incomplete code, like someone is in the 
             * middle of writing a function call, to not accidentally consume the next statement as an argument */

            if (Check(TokenType.Semicolon) || StatementSyncTokens.Contains(Current.Type))
            {
                /* NOTE: special casing here for if the arg is *just* a & sign. this is common when variable auto
                 * suggest is enabled in app refiner. in this case we *do* want the & to be considered an argument so
                 * that its parent is properly set to the function call */
                if (arg is not IdentifierNode || (arg is IdentifierNode id && id.Name != "&"))
                {
                    _position = positionBackup;
                    break;
                }
            }
            if (arg != null)
            {
                args.Add(arg);
            }
        } while (Match(TokenType.Comma));

        return args;
    }

    /// <summary>
    /// Parse array indices (comma-separated list of expressions inside brackets)
    /// </summary>
    private List<ExpressionNode> ParseArrayIndices()
    {
        EnterRule("arrayIndices");
        try
        {
            var indices = new List<ExpressionNode>();

            do
            {
                var positionBackup = _position;
                var index = ParseExpression();
                if (index == null)
                {
                    ReportError("Expected expression for array index");
                    // Add dummy index for error recovery
                    indices.Add(new LiteralNode(0, LiteralType.Integer));
                    break;
                }

                /* Heuristic to try and handle when we are parsing incomplete code, like someone is in the 
                 * middle of writing a an array access, to not accidentally consume the next statement as the 
                 * indices expression 
                 */
                if (Check(TokenType.Semicolon) || StatementSyncTokens.Contains(Current.Type))
                {
                    _position = positionBackup;
                    break;
                }

                indices.Add(index);
            } while (Match(TokenType.Comma));

            if (indices.Count == 0)
            {
                ReportError("Expected at least one array index");
                // Add dummy index for recovery
                indices.Add(new LiteralNode(0, LiteralType.Integer));
            }

            return indices;
        }
        finally
        {
            ExitRule();
        }
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
                    FirstToken = token,
                    LastToken = token
                },
                TokenType.DecimalLiteral => new LiteralNode(token.Value!, LiteralType.Decimal)
                {
                    FirstToken = token,
                    LastToken = token
                },
                TokenType.StringLiteral => new LiteralNode(token.Value!, LiteralType.String)
                {
                    FirstToken = token,
                    LastToken = token
                },
                TokenType.BooleanLiteral => new LiteralNode(token.Value!, LiteralType.Boolean)
                {
                    FirstToken = token,
                    LastToken = token
                },
                TokenType.Null => new LiteralNode(null!, LiteralType.Null)
                {
                    FirstToken = token,
                    LastToken = token
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
    /// Parse interpolated string expressions: $"Hello, {&name}!"
    /// </summary>
    private InterpolatedStringNode? ParseInterpolatedString()
    {
        try
        {
            EnterRule("interpolated_string");

            var parts = new List<InterpolatedStringPart>();
            var hasErrors = false;
            var firstToken = Current;
            Token? lastToken = firstToken;

            // Consume INTERP_STRING_START
            var startToken = Current;
            _position++;

            // Add the leading string fragment (may be empty)
            if (!string.IsNullOrEmpty(startToken.Value?.ToString()))
            {
                var fragment = new StringFragment(startToken.Value!.ToString()!)
                {
                    FirstToken = startToken,
                    LastToken = startToken
                };
                parts.Add(fragment);
            }

            // Parse interpolations and remaining content
            // InterpStringStart is returned when:
            // 1. We hit a { (content before it is in the token value) - common case
            // 2. We hit a closing " with no interpolations (entire string in token value) - edge case

            if (Check(TokenType.LeftBrace))
            {
                // We have interpolations to parse
                while (Check(TokenType.LeftBrace))
                {
                        // Consume {
                        Consume(TokenType.LeftBrace, "Expected '{'");

                        // Parse the expression (may be empty or incomplete)
                        ExpressionNode? expr = null;
                        bool interpHasError = false;

                        if (Check(TokenType.RightBrace))
                        {
                            // Empty interpolation {}
                            interpHasError = true;
                            ReportError("Empty interpolation expression");
                        }
                        else if (Check(TokenType.InterpStringUnterminated))
                        {
                            // Hit EOL/EOF without closing brace
                            interpHasError = true;
                            hasErrors = true;
                            // Don't consume - let the loop exit
                        }
                        else
                        {
                            // Parse expression
                            expr = ParseExpression();
                            if (expr == null)
                            {
                                interpHasError = true;
                            }
                        }

                        // Create interpolation node
                        var interpolation = new Interpolation(expr, interpHasError);
                        if (expr != null)
                        {
                            interpolation.FirstToken = expr.FirstToken;
                            interpolation.LastToken = expr.LastToken ?? expr.FirstToken;
                        }
                        parts.Add(interpolation);

                        // Check for closing brace or error recovery
                        if (Check(TokenType.InterpStringUnterminated))
                        {
                            // EOL hit - recovery point
                            lastToken = Current;
                            _position++;
                            hasErrors = true;
                            break;
                        }

                        if (Check(TokenType.RightBrace))
                        {
                            var closeBrace = Current;
                            _position++;
                            lastToken = closeBrace;

                            if (interpolation.FirstToken == null)
                            {
                                interpolation.FirstToken = closeBrace;
                            }
                            if (interpolation.LastToken == null || interpHasError)
                            {
                                interpolation.LastToken = closeBrace;
                            }

                            // After }, we should get either InterpStringMid or InterpStringEnd
                            if (Check(TokenType.InterpStringMid))
                            {
                                // Middle fragment
                                var midToken = Current;
                                _position++;
                                lastToken = midToken;

                                if (!string.IsNullOrEmpty(midToken.Value?.ToString()))
                                {
                                    var fragment = new StringFragment(midToken.Value!.ToString()!)
                                    {
                                        FirstToken = midToken,
                                        LastToken = midToken
                                    };
                                    parts.Add(fragment);
                                }

                                // Continue to next interpolation
                                continue;
                            }
                            else if (Check(TokenType.InterpStringEnd))
                            {
                                // End fragment
                                var endToken = Current;
                                _position++;
                                lastToken = endToken;

                                if (!string.IsNullOrEmpty(endToken.Value?.ToString()))
                                {
                                    var fragment = new StringFragment(endToken.Value!.ToString()!)
                                    {
                                        FirstToken = endToken,
                                        LastToken = endToken
                                    };
                                    parts.Add(fragment);
                                }

                                // Done with interpolated string
                                break;
                            }
                            else if (Check(TokenType.InterpStringUnterminated))
                            {
                                // Error recovery
                                lastToken = Current;
                                _position++;
                                hasErrors = true;
                                break;
                            }
                            else
                            {
                                // Unexpected token
                                ReportError($"Expected string content or '}}' after interpolation, got {Current.Type}");
                                hasErrors = true;
                                break;
                            }
                        }
                        else
                        {
                            // Missing closing brace
                            ReportError("Expected '}' after interpolation expression");
                            hasErrors = true;
                            break;
                        }
                    }
            }
            // No interpolations - check if string already ended or has error
            else if (Check(TokenType.InterpStringEnd))
            {
                // String ended immediately (e.g., $"" or $"text")
                // The content is already in the START token, nothing more to do
                // This happens when lexer returns InterpStringStart then immediately InterpStringEnd
                // (though based on lexer logic, this may not actually occur)
                lastToken = Current;
                _position++;
            }
            else if (Check(TokenType.InterpStringUnterminated))
            {
                // Error recovery - string was unterminated
                lastToken = Current;
                _position++;
                hasErrors = true;
            }
            // else: no more tokens means InterpStringStart contained everything (no interpolations, string ended)

            return new InterpolatedStringNode(parts, hasErrors)
            {
                FirstToken = firstToken,
                LastToken = lastToken
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
                TokenType.Caret => IdentifierType.Generic,
                TokenType.Super => IdentifierType.Super,
                _ => IdentifierType.Generic
            };

            return new IdentifierNode(token.Text, identifierType)
            {
                FirstToken = token,
                LastToken = token
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
        var firstToken = Previous;

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

        return new ObjectCreationNode(appClassPath, args)
        {
            FirstToken = firstToken,
            LastToken = Previous
        };
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

    // ===== COMPILER DIRECTIVE PARSING =====


    /// <summary>
    /// Set the PeopleTools version for directive evaluation
    /// </summary>
    /// <param name="version">Version string in format "major.minor[.patch]" or null to unset</param>
    public void SetToolsRelease(string? version)
    {
        ToolsRelease = string.IsNullOrEmpty(version) ? new ToolsVersion("99.99.99") : new ToolsVersion(version);

        // Reprocess directives with the new ToolsRelease setting
        _skippedDirectiveSpans = PreProcessDirectives();
    }

    /// <summary>
    /// Collects all comments from the token stream and adds them to the program node
    /// </summary>
    private void CollectComments(ProgramNode program)
    {
        // Traverse all tokens and collect comments from both the main tokens and their trivia
        foreach (var token in _tokens)
        {
            // Check if the token itself is a comment
            if (token.Type.IsCommentType())
            {
                program.AddComment(token);
            }

            // Check leading trivia for comments
            foreach (var leadingTrivia in token.LeadingTrivia)
            {
                if (leadingTrivia.Type.IsCommentType())
                {
                    program.AddComment(leadingTrivia);
                }
            }

            // Check trailing trivia for comments
            foreach (var trailingTrivia in token.TrailingTrivia)
            {
                if (trailingTrivia.Type.IsCommentType())
                {
                    program.AddComment(trailingTrivia);
                }
            }
        }
    }

    #region Type Checking Integration

    // NOTE: Old type inference methods have been removed during type system unification.
    // The new type inference system is simpler - use TypeInferenceVisitor directly.

    #endregion
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