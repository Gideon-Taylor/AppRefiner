# Parser Implementation - TODO List

This document tracks all **remaining** grammar rules that need to be implemented in the PeopleCode recursive descent parser.

**Status**: 16 of 89 total grammar rules remaining (~18%)

---

## **🚧 HIGH PRIORITY (Core Language Features)**

### 1. Local Variable Declarations
**Status**: ✅ COMPLETED
**Files**: `PeopleCodeParser.cs` (ParseLocalVariableStatement), `StatementNodes.cs` (LocalVariableDeclarationNode, LocalVariableDeclarationWithAssignmentNode)
**Rules**: 
- `localVariableDeclaration` - ✅ Complete local variable parsing
- `localVariableDefinition` - ✅ Variable definition syntax  
- `localVariableDeclAssignment` - ✅ Declaration with immediate assignment

**Implementation Completed**:
```peoplecode
Local string &myVar;
Local integer &count = 10;
Local array of string &names;
```

**Priority**: ✅ COMPLETED - Full support for local variable declarations

### 2. Function Declaration System  
**Status**: ❌ Not Implemented (placeholder only)
**Files**: `FunctionDeclaration.cs`, `FunctionDefinition.cs`
**Rules**:
- `functionDeclaration` - Function signature declarations
- `functionDeclarationPCode` - PeopleCode function declarations  
- `functionDefinition` - Function implementation bodies
- `functionDefinitions` - Multiple function definitions

**Implementation Needed**:
```peoplecode
Function DoSomething(&param As string) Returns boolean;
Function CalculateValue(&num As number) Returns number
   /* implementation */
End-Function;
```

**Priority**: HIGH - Core language feature

### 3. EVALUATE Statement System
**Status**: 🚧 Placeholder Only (parser stub with error and skip to END-EVALUATE)
**Files**: `EvaluateStatement.cs`, `WhenClause.cs`
**Rules**:
- `evaluateStatement` - EVALUATE-WHEN-END-EVALUATE constructs
- `whenClauses` - Collection of WHEN clauses
- `whenClause` - Individual WHEN conditions  
- `whenOther` - WHEN-OTHER (default) clause

**Implementation Needed**:
```peoplecode
Evaluate &status
When = "A"
   /* statements */
When = "B", "C"  
   /* statements */
When-Other
   /* statements */
End-Evaluate;
```

**Priority**: HIGH - Major control flow construct

---

## **🚧 MEDIUM PRIORITY (Advanced Features)**

### 4. Global/Component Variable Declarations
**Status**: ❌ Not Implemented (placeholder only)  
**Files**: `NonLocalVarDeclaration.cs`
**Rules**:
- `nonLocalVarDeclaration` - Global and component variable declarations

**Implementation Needed**:
```peoplecode
Global string &g_GlobalVar;
Component string &c_ComponentVar;
```

**Priority**: MEDIUM - Program structure feature

### 5. Constant Declarations
**Status**: ❌ Not Implemented (placeholder only)
**Files**: `ConstantDeclaration.cs`  
**Rules**:
- `constantDeclaration` - Constant value declarations

**Implementation Needed**:
```peoplecode
Constant &MAX_SIZE = 100;
Constant &DEFAULT_NAME = "Default";
```

**Priority**: MEDIUM - Basic language feature

### 6. Interface Declaration System
**Status**: ❌ Not Implemented (placeholder only)
**Files**: `InterfaceDeclaration.cs`
**Rules**:
- `interfaceDeclaration` - Interface definitions
  - InterfaceDeclarationExtension (extends other interfaces)
  - InterfaceDeclarationPlain (standalone interfaces)

**Implementation Needed**:
```peoplecode
Interface ICalculator Extends IBaseInterface
   Method Calculate(&value As number) Returns number;
End-Interface;
```

**Priority**: MEDIUM - Object-oriented completeness

### 7. Import System Completion
**Status**: ✅ Implemented (imports, wildcards, semicolon handling)
**Files**: `ImportDeclaration.cs`, `ImportsBlock.cs`
**Rules**:
- `importsBlock` - Collection of import statements
- `importDeclaration` - Individual import statements  
- `appPackageAll` - Wildcard package imports (package:*)

**Implementation Needed**:
```peoplecode
import PTAF_UTILITIES:*;
import FUNCLIB_HR:EMPLOYEE_DATA;
```

**Priority**: MEDIUM - Module system feature

### 8. Complete Type Casting
**Status**: ❌ Not Implemented (stub method `ParseTypeCast`)
**Files**: `Expression.cs` (ClassCastExpr)
**Rules**:
- Complete `ClassCastExpr` implementation for AS operator

**Implementation Needed**:
```peoplecode
&obj = &someValue As MyClass;
&result = (&data As array of string)[1];
```

**Priority**: MEDIUM - Expression system completion

---

## **❌ LOW PRIORITY (Legacy/Advanced Features)**

### 9. DLL Function Declaration System
**Status**: ❌ Not Implemented
**Files**: Need to create `DllFunctionDeclaration.cs`
**Rules**:
- `functionDeclarationDLL` - External DLL function declarations
- `dllArguments` - DLL function parameter lists
- `dllArgument` - Individual DLL parameters  
- `dllReturnType` - DLL function return types

**Implementation Needed**:
```peoplecode
Declare Function MessageBox Library "user32.dll"
   (handle As number, text As string, caption As string, type As number)
   Returns number;
```

**Priority**: LOW - Advanced integration feature

### 10. Record Field References  
**Status**: ❌ Not Implemented
**Files**: Need to create `RecordField.cs`
**Rules**:
- `recordField` - RECORD.FIELD syntax parsing

**Implementation Needed**:
```peoplecode
EMPLOYEE.EMPLID = "12345";
&value = PAYROLL_DATA.GROSS_PAY;
```

**Priority**: LOW - Legacy syntax support

---

## **IMPLEMENTATION STRATEGY**

### Phase 1: Core Language Completion (Weeks 1-2)
1. **Local Variable Declarations** - Critical for statement parsing
2. **Function Declaration System** - Core language feature  
3. **EVALUATE Statements** - Major control flow construct

### Phase 2: Program Structure (Weeks 3-4)  
4. **Global/Component Variables** - Program organization
5. **Constant Declarations** - Basic language feature
6. **Complete Type Casting** - Expression system completion

### Phase 3: Advanced Features (Weeks 5-6)
7. **Interface Declarations** - Object-oriented completeness
8. **Import System Completion** - Module system
9. **DLL Function Declarations** - Advanced integration

### Phase 4: Legacy Support (Week 7)
10. **Record Field References** - Legacy syntax support

---

## **TESTING REQUIREMENTS**

Each new implementation must include:

### Unit Tests
- **Successful parsing tests**: Valid syntax variations
- **Error recovery tests**: Malformed input handling
- **Edge case tests**: Boundary conditions and unusual syntax

### Integration Tests  
- **Real PeopleCode samples**: Test with actual code from PeopleSoft
- **Cross-feature tests**: Ensure new rules work with existing parser
- **Performance tests**: Benchmark parsing speed and memory usage

### Test File Locations
```
PeopleCodeParser.Tests/
├── VariableDeclarationTests/     # Variables and constants
├── FunctionSystemTests/          # Function declarations/definitions  
├── ControlFlowTests/            # EVALUATE statements
├── InterfaceTests/              # Interface declarations
├── ImportSystemTests/           # Import statements
├── LegacyFeatureTests/          # Record fields, DLL functions
└── IntegrationTests/            # Cross-feature testing
```

---

## **IMPLEMENTATION PATTERNS**

### Error Recovery Strategy
- **Synchronization tokens**: SEMI, END_*, specific keywords
- **Panic recovery**: Skip to safe parsing points
- **Error repair**: Insert missing tokens where possible
- **Context preservation**: Maintain rule stack for debugging

### AST Node Creation
- **Consistent patterns**: Follow existing node creation methods
- **Parent-child relationships**: Proper AST tree structure
- **Source spans**: Accurate position tracking for all nodes
- **Visitor support**: Ensure all nodes work with visitor pattern

### Parser Method Structure
```csharp
private AstNode ParseRuleName()
{
    EnterRule("RuleName");
    try
    {
        // Implementation logic
        // Error recovery as needed
        return astNode;
    }
    finally
    {
        ExitRule();
    }
}
```

---

## **DEPENDENCIES & BLOCKERS**

### Current Blockers
- **None**: All remaining rules can be implemented independently
- **Testing Infrastructure**: Existing test framework supports new features
- **AST Infrastructure**: All necessary AST nodes already exist

### Implementation Dependencies
- **Local Variables**: Needed by other statement types
- **Functions**: Needed for complete program parsing
- **EVALUATE**: Independent implementation
- **Interfaces**: Depends on existing class system (already complete)

---

**Implementation Target**: Complete all 22 remaining rules within 6-8 weeks
**Next Milestone**: Complete core language features (Local Variables, Functions, EVALUATE)
**Final Goal**: 100% grammar coverage with production-grade error recovery

---

*Last Updated: 2025-01-22*
*22 of 89 grammar rules remaining*