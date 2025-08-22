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

### Exception Handling

### Expressions

### Function Definitions

### Method Annotations

## Validation Strategy

For each rule, the validation agent should:

1. **Positive Testing**: Create valid PeopleCode samples that exercise the rule
2. **Negative Testing**: Create invalid samples that should fail parsing
3. **Edge Cases**: Test boundary conditions and unusual but valid syntax
4. **AST Verification**: Ensure the generated AST structure is correct
5. **Error Recovery**: Verify appropriate error messages for invalid syntax

## Reference

All rules are defined in `PeopleCodeParser/PeopleCodeParser.g4`. Always reference this grammar file when implementing validation tests to ensure correct understanding of rule structure and relationships.