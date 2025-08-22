# PeopleCode Parser Development Guidelines

This file provides specific guidance for working on the **PeopleCode recursive descent parser** in the `PeopleCodeParser.SelfHosted` project.

---

## **PARSER PROJECT OVERVIEW**

We are building a **production-grade, error-tolerant, recursive descent parser** for PeopleCode that will replace the existing ANTLR-based parser in AppRefiner. The parser emphasizes performance, error recovery, and maintainability.

### **Current Status**
- **✅ Lexer**: 100% complete (299 token types)
- **✅ AST Infrastructure**: 100% complete (25+ node types)  
- **✅ Core Parser**: 75% complete (67 of 89 grammar rules)
- **🚧 Remaining Work**: 22 grammar rules (see PARSER_TODO.md)

---

## **🔄 DEVELOPMENT WORKFLOW**

### **Before Starting Any Parser Work**

1. **Read Documentation First**:
   - **PARSER_COMPLETED.md** - See what's already implemented
   - **PARSER_TODO.md** - See what needs to be done
   - **GRAMMAR_ELEMENTS_CATALOG.md** - Full grammar reference

2. **Reference Grammar File**:
   - **Always** reference `PeopleCodeParser/PeopleCodeParser.g4` for exact ANTLR grammar rules
   - The self-hosted parser must match the ANTLR grammar exactly
   - Use grammar rule names and structure as implementation guide

3. **Check Existing Patterns**:
   - Study similar implemented rules in the parser
   - Follow existing error recovery patterns
   - Maintain consistency with AST node creation

### **Implementation Priority Order**
Follow **PARSER_TODO.md** priority order:
1. **HIGH PRIORITY**: Local variables, functions, EVALUATE statements
2. **MEDIUM PRIORITY**: Global variables, constants, interfaces
3. **LOW PRIORITY**: DLL functions, record fields

---

## **🏗️ PARSER ARCHITECTURE**

### **Core Principles**
- **Recursive Descent**: Hand-written parser for maximum control and performance
- **Error Recovery**: Prioritize fault tolerance over strict correctness
- **Performance**: Target 2x faster than ANTLR with 70% memory usage
- **Maintainability**: Clear, readable code with comprehensive error handling

### **Parser Class Structure**
```csharp
public class PeopleCodeParser
{
    // Core parsing infrastructure (✅ Complete)
    private void EnterRule(string ruleName)     // Rule context tracking
    private void ExitRule()                     // Rule cleanup
    private bool Match(TokenType expected)      // Token consumption
    private Token Advance()                     // Move to next token
    private void PanicRecover(TokenType[] sync) // Error recovery
    
    // Grammar rule implementations (🚧 75% complete)
    private ProgramNode ParseProgram()          // ✅ Main entry point
    private ExpressionNode ParseExpression()    // ✅ 12-level precedence
    private StatementNode ParseStatement()      // ✅ Most statements
    // ... 67 implemented rules, 22 remaining
}
```

### **Error Recovery Strategy**
```csharp
// Standard error recovery pattern
private AstNode ParseRuleName()
{
    EnterRule("RuleName");
    try
    {
        // Try normal parsing
        if (CurrentToken.Type == EXPECTED_TOKEN)
        {
            return ParseNormalCase();
        }
        
        // Error recovery
        ReportError("Expected ..., found ...");
        PanicRecover(new[] { SEMI, END_IF, END_WHILE });
        return CreateErrorNode();
    }
    finally
    {
        ExitRule();
    }
}
```

---

## **🧪 TESTING REQUIREMENTS**

### **Test Organization**
All tests go in **`PeopleCodeParser.Tests`** project using **xUnit framework**:

```
PeopleCodeParser.Tests/
├── ExpressionTests/         # ✅ Expression parsing tests
├── StatementTests/          # ✅ Statement parsing tests  
├── DeclarationTests/        # 🚧 Class/method/variable tests
├── TypeSystemTests/         # ✅ Type parsing tests
├── ErrorRecoveryTests/      # ✅ Malformed input tests
├── PerformanceTests/        # ✅ Benchmarking tests
└── IntegrationTests/        # ✅ Real PeopleCode files
```

### **Required Test Coverage for Each New Rule**
1. **Success Tests**: Valid syntax variations
   ```csharp
   [Fact]
   public void ParseLocalVariable_ValidDeclaration_Success()
   {
       var result = ParseRule("Local string &myVar;");
       Assert.IsType<LocalVariableDeclarationNode>(result);
   }
   ```

2. **Error Recovery Tests**: Malformed input handling
   ```csharp
   [Fact] 
   public void ParseLocalVariable_MissingSemicolon_RecovesGracefully()
   {
       var result = ParseRule("Local string &myVar");
       Assert.True(HasErrors);
       Assert.NotNull(result); // Should still create AST node
   }
   ```

3. **Integration Tests**: Real-world PeopleCode samples
   ```csharp
   [Fact]
   public void ParseRealPeopleCode_ComplexFunction_Success()
   {
       var code = LoadTestFile("RealWorldSamples/ComplexFunction.pcode");
       var result = Parser.Parse(code);
       Assert.False(HasErrors);
   }
   ```

### **Performance Testing**
- **Benchmark against ANTLR**: Each new rule must maintain 2x speed advantage
- **Memory profiling**: Monitor memory usage growth
- **Large file testing**: Test with 10k+ line PeopleCode files

---

## **🔧 IMPLEMENTATION PATTERNS**

### **AST Node Creation**
Follow existing patterns for AST node construction:

```csharp
private ExpressionNode ParseBinaryExpression()
{
    var left = ParseLeftOperand();
    var operatorToken = Advance(); // Consume operator
    var right = ParseRightOperand();
    
    return new BinaryExpressionNode(left, operatorToken, right)
    {
        SourceSpan = CreateSpan(left.SourceSpan.Start, right.SourceSpan.End),
        Parent = CurrentParent
    };
}
```

### **Error Recovery Points**
Define synchronization tokens for each construct:

```csharp
// Statement-level recovery
private readonly TokenType[] StatementSyncTokens = {
    SEMI, END_IF, END_WHILE, END_FOR, END_TRY, ELSE, CATCH
};

// Expression-level recovery  
private readonly TokenType[] ExpressionSyncTokens = {
    SEMI, COMMA, RPAREN, RBRACKET, THEN, END_IF
};
```

### **Rule Implementation Template**
```csharp
private AstNode ParseNewRule()
{
    EnterRule("NewRule");
    var startPosition = CurrentToken.Position;
    
    try
    {
        // 1. Validate preconditions
        if (!IsExpectedToken())
        {
            ReportError($"Expected {ExpectedToken}, found {CurrentToken}");
            PanicRecover(SynchronizationTokens);
            return CreateErrorNode();
        }
        
        // 2. Parse required elements
        var element1 = ParseElement1();
        ExpectAndConsume(REQUIRED_TOKEN);
        var element2 = ParseElement2();
        
        // 3. Create AST node
        var node = new NewRuleNode(element1, element2)
        {
            SourceSpan = CreateSpan(startPosition, CurrentToken.Position),
            Parent = CurrentParent
        };
        
        return node;
    }
    catch (ParseException ex)
    {
        ReportError(ex.Message);
        PanicRecover(SynchronizationTokens);
        return CreateErrorNode();
    }
    finally
    {
        ExitRule();
    }
}
```

---

## **📚 REFERENCE MATERIALS**

### **Grammar Reference**
- **Primary**: `PeopleCodeParser/PeopleCodeParser.g4` - ANTLR grammar file
- **Implementation Guide**: PARSER_COMPLETED.md - Patterns and examples
- **TODO List**: PARSER_TODO.md - Remaining work with priorities

### **Existing Parser Code**
Study these implemented rules as examples:
- **Expressions**: All 12 precedence levels implemented
- **Statements**: IF, WHILE, FOR, TRY-CATCH patterns
- **Classes**: Declaration and implementation patterns
- **Types**: Built-in and app class type parsing

### **Error Recovery Examples**
- **Expression errors**: See `ParseExpression()` methods
- **Statement errors**: See `ParseStatement()` error handling
- **Block errors**: See `ParseStatementBlock()` recovery

---

## **⚠️ IMPORTANT REMINDERS**

### **Code Quality Standards**
- **No comments unless absolutely necessary** - Code should be self-documenting
- **Consistent naming**: Follow existing parser method naming (`ParseRuleName`)
- **Error messages**: Clear, actionable error descriptions
- **Performance**: Profile any new parsing logic for bottlenecks

### **Grammar Compliance**
- **Exact ANTLR match**: Self-hosted parser must accept same input as ANTLR
- **Token handling**: Use exact token types from lexer
- **AST structure**: Match visitor pattern expectations
- **Error positions**: Accurate source span tracking

### **Testing Requirements**
- **Test-driven**: Write tests before implementing rules
- **Error coverage**: Test both success and failure cases  
- **Performance**: Benchmark all new functionality
- **Integration**: Test with real PeopleCode samples

### **Documentation Updates**
- **Update PARSER_COMPLETED.md** when rules are finished
- **Update PARSER_TODO.md** with progress and discoveries
- **Mark grammar catalog** when rules change status
- **Add test documentation** for complex features

---

## **🚀 GETTING STARTED**

### **Implementing Your First Rule**

1. **Choose from PARSER_TODO.md** (start with HIGH PRIORITY)
2. **Study ANTLR grammar** in `PeopleCodeParser.g4`
3. **Find similar implemented rule** in parser codebase
4. **Write tests first** in appropriate test project
5. **Implement parser method** following patterns
6. **Add error recovery** with appropriate sync tokens
7. **Run full test suite** to ensure no regressions
8. **Update documentation** when complete

### **Example: Implementing Local Variables**

1. **Reference**: `PeopleCodeParser.g4` - `localVariableDeclaration` rule
2. **Tests**: Create `LocalVariableDeclarationTests.cs`
3. **Implementation**: Add `ParseLocalVariableDeclaration()` method
4. **Integration**: Wire into `ParseStatement()` 
5. **Testing**: Verify with real PeopleCode samples
6. **Documentation**: Update PARSER_COMPLETED.md

---

**Remember**: We're building a production parser that will handle millions of lines of PeopleCode. Prioritize correctness, performance, and error recovery over clever optimizations.

*Last Updated: 2025-01-22*