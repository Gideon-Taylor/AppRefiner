# Refactor Porting Todo List

This document tracks the progress of porting refactors from the ANTLR-based system to the self-hosted parser.

**Status Legend:**
- ❌ **Not Started** - No work has begun on this refactor
- 🟡 **In Progress** - Porting work has started but not completed
- ✅ **Completed** - Refactor has been successfully ported and tested
- 🔄 **Needs Review** - Ported but requires code review or testing
- ⏸️ **Blocked** - Cannot proceed due to dependencies or issues

---

## Refactor Porting Status

| Refactor Name | Status | Complexity | Notes | Estimated Effort |
|---------------|--------|------------|-------|------------------|
| **AddFlowerBox** | ❌ Not Started | Low | Simple text insertion at program start | 2-3 hours |
| **AddImport** | ❌ Not Started | Medium | Import statement management and analysis | 4-6 hours |
| **ConcatAutoComplete** | ❌ Not Started | Medium | Auto-completion for string concatenation | 3-5 hours |
| **CreateAutoComplete** | ❌ Not Started | High | Complex type detection and code generation | 6-8 hours |
| **LocalVariableCollectorRefactor** | ❌ Not Started | High | Multi-scope variable collection and reorganization | 8-10 hours |
| **MsgBoxAutoComplete** | ❌ Not Started | Medium | MessageBox parameter auto-completion | 3-4 hours |
| **RenameLocalVariable** | ❌ Not Started | Very High | Complex scope-aware renaming with dialogs | 10-12 hours |
| **ResolveImports** | ❌ Not Started | Medium | Import resolution and cleanup | 4-6 hours |
| **SortMethods** | ❌ Not Started | High | Method and property reordering | 6-8 hours |
| **SuppressReportRefactor** | ❌ Not Started | Low | Linter suppression comment insertion | 2-3 hours |

---

## Complexity Assessment

### **Low Complexity (2-4 hours each)**
- **AddFlowerBox**: Simple text insertion using `BaseRefactor`
- **SuppressReportRefactor**: Comment insertion at specific locations

**Characteristics:**
- Single AST node processing
- No scope awareness needed
- Minimal user interaction
- Direct text insertion/replacement

### **Medium Complexity (3-6 hours each)**
- **AddImport**: Import statement analysis and insertion
- **ConcatAutoComplete**: Type-aware auto-completion
- **MsgBoxAutoComplete**: Parameter suggestion with validation
- **ResolveImports**: Import dependency resolution

**Characteristics:**
- Multi-node AST processing
- Basic type analysis
- Moderate user interaction
- Context-aware text generation

### **High Complexity (6-10 hours each)**
- **CreateAutoComplete**: Complex type detection with constructor analysis
- **LocalVariableCollectorRefactor**: Multi-scope variable management
- **SortMethods**: Complex reordering with comment preservation

**Characteristics:**
- Complex AST traversal
- Advanced scope management
- Sophisticated dialog interactions
- Multi-step text transformations

### **Very High Complexity (10+ hours each)**
- **RenameLocalVariable**: Comprehensive scope-aware renaming

**Characteristics:**
- Deep scope analysis
- Complex user interaction flows
- Multiple AST node types
- Critical position accuracy requirements

---

## Porting Order Recommendations

### **Phase 1: Foundation (Week 1)**
1. **AddFlowerBox** ✨ *Start here - simplest pattern*
2. **SuppressReportRefactor** ✨ *Build confidence with another simple refactor*

**Rationale:** These establish the basic porting patterns and build familiarity with the new AST system.

### **Phase 2: Medium Complexity (Week 2-3)**
3. **AddImport** 🎯 *Critical functionality*
4. **ResolveImports** 🎯 *Pairs well with AddImport*
5. **MsgBoxAutoComplete** 
6. **ConcatAutoComplete**

**Rationale:** Import management is frequently used, and auto-completion refactors share similar patterns.

### **Phase 3: High Complexity (Week 4-5)**
7. **SortMethods** 🎯 *High value, manageable complexity*
8. **CreateAutoComplete** 🎯 *Complex but frequently used*
9. **LocalVariableCollectorRefactor**

**Rationale:** These provide significant user value and help refine complex AST processing patterns.

### **Phase 4: Ultimate Challenge (Week 6)**
10. **RenameLocalVariable** 🏆 *Most complex, save for last*

**Rationale:** This is the most complex refactor requiring all learned patterns and techniques.

---

## Implementation Notes

### **Shared Patterns Identified**
- **Simple Insertion**: AddFlowerBox, SuppressReportRefactor
- **Auto-completion**: CreateAutoComplete, ConcatAutoComplete, MsgBoxAutoComplete
- **Import Management**: AddImport, ResolveImports  
- **Scope-Aware Processing**: LocalVariableCollectorRefactor, RenameLocalVariable
- **Code Reorganization**: SortMethods

### **Common Dependencies**
- All refactors need base AST visitor patterns
- Auto-completion refactors share type detection logic
- Scope-aware refactors need `ScopedRefactor` base class
- Dialog-heavy refactors need proper user input validation

### **Testing Requirements**
Each ported refactor needs:
- [ ] Unit tests for core functionality
- [ ] Integration tests with real PeopleCode samples
- [ ] Dialog interaction testing
- [ ] Performance validation (2x faster than ANTLR)
- [ ] Position accuracy verification

---

## Risk Assessment

### **High Risk Items**
- **RenameLocalVariable**: Critical functionality, complex scope management
- **CreateAutoComplete**: Frequently used, complex type analysis
- **LocalVariableCollectorRefactor**: Complex multi-scope processing

### **Medium Risk Items**
- **SortMethods**: Complex text manipulation with comment preservation
- **AddImport**: Critical for import management workflow

### **Low Risk Items**
- **AddFlowerBox**: Simple, well-understood pattern
- **SuppressReportRefactor**: Straightforward comment insertion

---

## Success Criteria

For each refactor to be considered complete:

✅ **Functional Parity**
- All original functionality preserved
- Same user experience and workflows
- Identical output for same inputs

✅ **Performance Requirements**
- 2x faster execution than ANTLR version
- 70% of ANTLR memory usage
- Responsive UI interactions

✅ **Code Quality**
- Clean, maintainable implementation
- Proper error handling and validation
- Comprehensive documentation

✅ **Testing Coverage**
- Unit tests for all core functionality
- Integration tests with sample code
- Edge case handling verification

---

## Resources

### **Reference Materials**
- [RefactorPortingGuide.md](RefactorPortingGuide.md) - Comprehensive porting guidance
- [StylerPortingGuide.md](StylerPortingGuide.md) - Pattern reference from styler ports
- `AppRefiner\Refactors\*.cs` - Original ANTLR implementations
- `PeopleCodeParser.SelfHosted\Nodes\*.cs` - AST node definitions

### **Support Files**
- `ParserPorting\Refactors\BaseRefactor.cs` - Base refactor class (to be created)
- `ParserPorting\Refactors\ScopedRefactor.cs` - Scoped refactor class (to be created)
- Test samples in existing codebase for validation

---

**Total Estimated Effort: 60-80 hours**  
**Target Completion: 6 weeks**  
**Priority: High** - Critical for self-hosted parser adoption

*Last Updated: 2025-01-26*