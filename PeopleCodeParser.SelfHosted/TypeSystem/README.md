# TypeSystem Architecture

This document describes the architecture and organization of the TypeSystem module within PeopleCodeParser.SelfHosted, which handles semantic analysis including type inference and type checking.

## Overview

The TypeSystem is organized around a clear separation of concerns while maintaining the practical benefits of colocation with the parser. The system is designed to provide comprehensive semantic analysis capabilities for PeopleCode programs.

## Directory Organization

The TypeSystem is organized into the following subdirectories:

### `/Contracts`
Contains interface definitions that establish clear contracts for consuming applications.

**Key Interfaces:**
- `ITypeInferenceEngine` - Core type inference operations
- `IExtendedTypeInferenceEngine` - Advanced type inference with external resolution
- `ISemanticAnalysisService` - Main entry point for consuming applications
- `ITypeService` - Type service operations
- `IProgramResolver` - External program resolution
- `IProgramSourceProvider` - Program source retrieval

**Purpose:** These interfaces allow consuming applications (like AppRefiner) to work with semantic analysis without depending on implementation details.

### `/Core`
Contains fundamental type system definitions and core data structures.

**Key Components:**
- `TypeInfo.cs` - Base type information hierarchy
- `ClassTypeInfo.cs` - Application class type definitions
- `PeopleCodeType.cs` - PeopleCode type enumeration
- `PeopleCodeTypeRegistry.cs` - Type registration and caching
- `ReferenceTypeIdentifiers.cs` - Type identification utilities
- `ReferenceTypeValidation.cs` - Type validation logic

**Purpose:** Provides the foundational type model that all other components build upon.

### `/Inference`
Contains the type inference engine and related components that perform actual type analysis.

**Key Components:**
- `TypeInferenceEngine.cs` - Main orchestrator for type inference
- `TypeInferenceContext.cs` - State management during inference
- `TypeInferenceResult.cs` - Results and statistics
- `SimpleTypeInferenceVisitor.cs` - AST visitor for type inference

**Purpose:** Implements the core algorithms for inferring types from PeopleCode AST.

### `/Analysis`
Contains higher-level semantic analysis components that build upon type inference.

**Key Components:**
- `TypeService.cs` - Implementation of ITypeService
- `ClassMetadataBuilder.cs` - Builds metadata for classes and interfaces

**Purpose:** Provides comprehensive semantic analysis services beyond basic type inference.

### `/Tests`
Contains comprehensive test suite for type system functionality.

## Architectural Principles

### 1. Intentional Coupling with Parser
**Rationale:** Type inference is fundamentally dependent on AST structure and benefits from tight integration.

**Benefits:**
- Direct AST node modification for type annotation
- Shared visitor patterns and traversal logic
- Consistent source location tracking
- Single dependency management

### 2. Clean Interface Separation
**Implementation:** Well-defined interfaces in `/Contracts` directory.

**Benefits:**
- Clear consumption patterns for AppRefiner
- Testability and mockability
- Future extensibility without breaking changes
- Clean dependency injection setup

### 3. External Function Resolution
**Architecture:** TypeSystem delegates function-related logic to external systems.

**Responsibilities:**
- **External System:** Function signatures, builtin functions, system variables, function call validation
- **TypeSystem:** Core type definitions, type inference algorithms, AST type annotation

### 4. Layered Organization
**Structure:**
```
Core (fundamental types)
  ↓
Inference (type analysis algorithms)
  ↓
Analysis (comprehensive semantic analysis)
  ↓
Contracts (consumer interfaces)
```

### 5. Single Namespace with Physical Organization
**Approach:** All components remain in `PeopleCodeParser.SelfHosted.TypeSystem` namespace but are physically organized in subdirectories.

**Benefits:**
- No circular dependencies
- Simplified imports
- Clear conceptual organization
- Maintains existing compatibility

## Usage Patterns

### For AppRefiner Integration
```csharp
// Primary interface for most operations
ISemanticAnalysisService semanticService = serviceProvider.GetService<ISemanticAnalysisService>();

// Configure analysis
var config = new SemanticAnalysisConfiguration
{
    DefaultTypeInferenceMode = TypeInferenceMode.Quick,
    EnableCaching = true
};
semanticService.UpdateConfiguration(config);

// Perform analysis
var result = await semanticService.AnalyzeProgramAsync(program);
```

### For Advanced Type Inference
```csharp
// Direct engine access for advanced scenarios
ITypeInferenceEngine engine = serviceProvider.GetService<ITypeInferenceEngine>();

var result = await engine.InferTypesAsync(
    program,
    TypeInferenceMode.Thorough,
    programResolver,
    options
);
```

### For Type Checking
```csharp
// Type compatibility checking
bool isValid = semanticService.IsAssignmentValid(sourceType, targetType);

// Get inferred types
TypeInfo? nodeType = semanticService.GetNodeType(astNode);
```

## Extension Points

### Adding New Type Analysis
1. Create new visitor in `/Inference` extending `ScopedAstVisitor<TypeInfo>`
2. Register with `TypeInferenceEngine` through dependency injection
3. Add corresponding interface in `/Contracts` if public API is needed

### Adding Semantic Rules
1. Implement new analysis logic in `/Analysis`
2. Extend `SemanticAnalysisResult` with new error/warning types
3. Update `ISemanticAnalysisService` interface if needed

### Custom Type Providers
1. Implement `IProgramSourceProvider` for external type resolution
2. Register with semantic analysis service
3. Enable thorough mode for cross-program analysis

## Performance Considerations

### Caching Strategy
- **Type Registry:** Global cache for frequently used types
- **Analysis Results:** Per-program caching with invalidation
- **External Resolution:** Cached resolved programs to avoid repeated parsing

### Analysis Modes
- **Quick Mode:** Fast analysis within program boundaries
- **Thorough Mode:** Complete analysis with external resolution
- **Incremental Mode:** Re-analyze only changed nodes

### Threading
- **Concurrent Collections:** Used throughout for thread safety
- **Async/Await:** Full async support for I/O bound operations
- **Cancellation:** Proper cancellation token support for timeouts

## Testing Strategy

The comprehensive test suite in `/Tests` covers:
- **Unit Tests:** Individual component testing
- **Integration Tests:** Full analysis pipeline testing
- **Performance Tests:** Analysis speed and memory usage
- **Regression Tests:** Ensure compatibility across changes

## Migration Path

For future architectural changes:
1. Extend interfaces in `/Contracts` first
2. Implement new functionality alongside existing
3. Update consuming code to use new interfaces
4. Deprecate old interfaces gradually
5. Remove deprecated code in major version updates

This approach ensures backward compatibility while enabling architectural evolution.

## Integration with External Function System

The TypeSystem is designed to work with an external function resolution system that provides:

### External System Responsibilities
1. **Function Signatures** - Common `FunctionInfo` structures for all functions
2. **Function Call Validation** - Validation system that takes `FunctionInfo` and `TypeInfo[]` parameters
3. **System Variables** - Type information for system variables (%variables)
4. **Builtin Functions** - Return types and signatures for builtin functions
5. **Builtin Object Methods** - Method signatures for builtin object types

### TypeSystem Focus
The TypeSystem focuses purely on:
- **Core Type Definitions** - `TypeInfo` hierarchy and type relationships
- **Type Inference Algorithms** - Inferring types from AST context
- **AST Type Annotation** - Storing inferred types on AST nodes
- **Class/Interface Metadata** - Application class structure and member types
- **Type Compatibility** - Checking type assignment compatibility

### Integration Points
When the external function system is integrated:
1. Replace `UnknownTypeInfo.Instance` returns with actual function resolution
2. Hook function call validation into the type inference visitor
3. Connect system variable type lookup to external providers
4. Integrate common `FunctionInfo` structures for class method signatures

This clean separation ensures the TypeSystem handles pure type semantics while the external system manages all function-related logic.