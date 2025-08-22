# Parser Validation Todo List

This file contains a comprehensive list of all parser rules from PeopleCodeParser.g4 that need validation testing. Each rule should be tested to ensure proper parsing behavior, error handling, and AST generation.

## Grammar Rules to Validate

### Core Program Structure


### Package and Class Paths

### Class and Interface Declarations

### Class Structure

### Method Definitions

### Type System
- [ ] **simpleType** - built-in types and generic IDs
- [ ] **builtInType** - primitive PeopleCode types (ANY, BOOLEAN, DATE, etc.)
- [ ] **typeT** - complete type specifications including arrays and exceptions
- [ ] **annotationType** - method annotation types including Array2-Array9

### Property and Variable Declarations
- [ ] **propertyDeclaration** - property declarations with GET/SET and modifiers
- [ ] **instanceDeclaration** - instance variable declarations
- [ ] **constantDeclaration** - constant variable declarations with literals
- [ ] **literal** - literal values (NULL, decimals, integers, strings, booleans)

### External Declarations
- [ ] **classExternalDeclaration** - external function and variable declarations
- [ ] **programPreambles** - multiple program preamble declarations
- [ ] **programPreamble** - various declaration types at program start

### Function Declarations
- [ ] **functionDeclaration** - PeopleCode and library function declarations
- [ ] **functionDeclarationPCode** - PeopleCode function declarations
- [ ] **recordField** - record field references (record.field)
- [ ] **functionDeclarationDLL** - external DLL function declarations
- [ ] **dllArguments** - DLL function parameter lists
- [ ] **dllArgument** - individual DLL parameters with modifiers
- [ ] **dllReturnType** - DLL function return type specifications
- [ ] **nonLocalVarDeclaration** - COMPONENT and GLOBAL variable declarations

### Class Implementation
- [ ] **classBody** - class implementation body with members
- [ ] **classMember** - method, getter, and setter implementations
- [ ] **method** - method implementations with annotations and statements
- [ ] **getter** - property getter implementations
- [ ] **setter** - property setter implementations

### Statements and Control Flow
- [ ] **statements** - sequences of executable statements
- [ ] **statementBlock** - scoped statement blocks
- [ ] **statement** - all statement types (assignment, control flow, declarations, etc.)

### Variable Declarations
- [ ] **localVariableDeclaration** - local variable declarations and assignments
- [ ] **localVariableDefinition** - local variable type definitions
- [ ] **localVariableDeclAssignment** - local variable declaration with assignment

### Control Flow Statements
- [ ] **ifStatement** - IF-THEN-ELSE conditional statements
- [ ] **elseStatement** - ELSE clause of IF statements
- [ ] **forStatement** - FOR loops with optional STEP clause
- [ ] **whileStatement** - WHILE loop statements
- [ ] **repeatStatement** - REPEAT-UNTIL loop statements

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