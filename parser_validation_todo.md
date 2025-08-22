# Parser Validation Todo List

This file contains a comprehensive list of all parser rules from PeopleCodeParser.g4 that need validation testing. Each rule should be tested to ensure proper parsing behavior, error handling, and AST generation.

## Grammar Rules to Validate

### Core Program Structure


### Package and Class Paths

### Class and Interface Declarations

### Class Structure

### Method Definitions

### Type System

### Property and Variable Declarations

### External Declarations

### Function Declarations

### Class Implementation

### Statements and Control Flow

### Variable Declarations

### Control Flow Statements

### Evaluate (Switch) Statements
- [ ] **evaluateStatement** - EVALUATE switch statements
- [ ] **whenClauses** - multiple WHEN clauses in EVALUATE
- [ ] **whenClause** - individual WHEN cases with optional comparison operators
- [ ] **whenOther** - WHEN-OTHER default case in EVALUATE
- [ ] **comparisonOperator** - comparison operators (LE, GE, NEQ, LT, GT, EQ)

### Exception Handling
- [ ] **tryCatchBlock** - TRY-CATCH exception handling blocks
- [ ] **catchClauses** - multiple CATCH clauses in TRY blocks
- [ ] **catchClause** - individual CATCH clauses with exception types

### Expressions
- [ ] **expression** - all expression types with precedence and operators
- [ ] **simpleFunctionCall** - function calls with arguments
- [ ] **dotAccess** - member access with optional method calls
- [ ] **allowableFunctionName** - keywords usable as function names
- [ ] **genericID** - identifiers including keywords usable as IDs
- [ ] **ident** - identifier types (SUPER, system variables/constants, user variables)
- [ ] **expressionList** - comma-separated expression lists
- [ ] **objectCreate** - CREATE object instantiation expressions
- [ ] **functionCallArguments** - function call parameter lists

### Function Definitions
- [ ] **functionDefinitions** - multiple function definitions
- [ ] **functionDefinition** - complete function implementations with optional DOC
- [ ] **functionArguments** - function definition parameter lists
- [ ] **functionArgument** - individual function parameters with optional types

### Method Annotations
- [ ] **methodAnnotations** - method parameter, return, and extends annotations
- [ ] **methodParameterAnnotation** - method parameter type annotations
- [ ] **methodAnnotationArgument** - annotated method arguments with types
- [ ] **methodReturnAnnotation** - method return type annotations
- [ ] **methodExtendsAnnotation** - method extends/implements annotations

## Validation Strategy

For each rule, the validation agent should:

1. **Positive Testing**: Create valid PeopleCode samples that exercise the rule
2. **Negative Testing**: Create invalid samples that should fail parsing
3. **Edge Cases**: Test boundary conditions and unusual but valid syntax
4. **AST Verification**: Ensure the generated AST structure is correct
5. **Error Recovery**: Verify appropriate error messages for invalid syntax

## Reference

All rules are defined in `PeopleCodeParser/PeopleCodeParser.g4`. Always reference this grammar file when implementing validation tests to ensure correct understanding of rule structure and relationships.