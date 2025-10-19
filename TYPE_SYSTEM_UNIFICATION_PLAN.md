# Plan: Create PeopleCodeTypeInfo Project and Unify Type Systems

## Overview
Create new **PeopleCodeTypeInfo** project in AppRefiner solution. Copy type system from external PeopleCodeTypeInfo.Core (excluding validation). Move type inference from Parser into this new project. Parser becomes pure parsing.

## Phase 1: Create New Project Structure

**1.1 Create PeopleCodeTypeInfo Project**
- New .NET 8 class library project
- Location: `AppRefiner/PeopleCodeTypeInfo/PeopleCodeTypeInfo.csproj`

**1.2 Project Structure**
```
PeopleCodeTypeInfo/
  Types/
    PeopleCodeType.cs          (copied from Core)
    TypeInfo.cs                (all TypeInfo classes from Core)
    PolymorphicTypeInfo.cs     (copied from Core)
  Functions/
    FunctionInfo.cs            (copied from Core)
    Parameters.cs              (copied from Core)
    PropertyInfo.cs            (copied from Core)
    ObjectMember.cs            (copied from Core)
    BuiltinObjectInfo.cs       (copied from Core)
  Database/
    PeopleCodeTypeDatabase.cs  (copied from Core - query interface only)
    IObjectInfo.cs             (copied from Core)
  Inference/
    TypeInferenceEngine.cs     (moved from Parser)
    TypeInferenceContext.cs    (moved from Parser)
    TypeInferenceResult.cs     (moved from Parser)
    SimpleTypeInferenceVisitor.cs (moved from Parser)
    ITypeService.cs            (moved from Parser)
    ITypeInferenceEngine.cs    (moved from Parser)
    IProgramSourceProvider.cs  (new skeleton)
    IProgramResolver.cs        (moved from Parser)
  Analysis/
    ClassMetadataBuilder.cs    (moved from Parser)
    ClassTypeInfo.cs           (moved from Parser)
```

## Phase 2: Copy Type System from PeopleCodeTypeInfo.Core

**2.1 Copy Core Type Files**
- `Types/PeopleCodeType.cs` - The PeopleCodeType enum and extension methods
- `Types/TypeInfo.cs` - All TypeInfo classes:
  - PrimitiveTypeInfo, StringTypeInfo, NumberTypeInfo
  - BuiltinObjectTypeInfo
  - AppClassTypeInfo
  - ArrayTypeInfo
  - AnyTypeInfo, VoidTypeInfo, UnknownTypeInfo
  - ReferenceTypeInfo
  - PolymorphicTypeInfo, SameAsObjectTypeInfo, ElementOfObjectTypeInfo, etc.
  - UnionReturnTypeInfo

**2.2 Copy Function System Files**
- `Functions/TypeWithDimensionality.cs` - Struct for type + array dimensions
- `Functions/Parameters.cs` - Parameter, SingleParameter, UnionParameter, ParameterGroup, VariableParameter
- `Functions/FunctionInfo.cs` - Function signature with return type resolution
- `Functions/PropertyInfo.cs` - Property information
- `Functions/ObjectMember.cs` - Object member info
- `Functions/BuiltinObjectInfo.cs` - Builtin object metadata

**2.3 Create Database Stub**
- Copy `PeopleCodeTypeDatabase.cs` interface/query methods
- **Exclude** validation logic (FunctionCallValidator)
- **Exclude** HashTable reading/writing (can add later when we load actual builtin data)
- For now: Methods return empty/null to compile

## Phase 3: Move Inference from Parser

**3.1 Move Inference Files**
Move from `PeopleCodeParser.SelfHosted/TypeSystem/` to `PeopleCodeTypeInfo/Inference/`:
- TypeInferenceEngine.cs
- TypeInferenceContext.cs
- TypeInferenceResult.cs
- SimpleTypeInferenceVisitor.cs
- ITypeService.cs
- ITypeInferenceEngine.cs
- IProgramResolver.cs

**3.2 Move Analysis Files**
Move from `PeopleCodeParser.SelfHosted/TypeSystem/Analysis/` to `PeopleCodeTypeInfo/Analysis/`:
- ClassMetadataBuilder.cs
- ClassTypeInfo.cs (if not already in copied TypeInfo.cs)

**3.3 Update Namespaces**
- Change all `PeopleCodeParser.SelfHosted.TypeSystem` → `PeopleCodeTypeInfo.Inference`
- Change `PeopleCodeParser.SelfHosted.TypeSystem.Analysis` → `PeopleCodeTypeInfo.Analysis`

## Phase 4: Remove Type System from Parser

**4.1 Delete Old TypeSystem Folder**
Delete entire `PeopleCodeParser.SelfHosted/TypeSystem/` directory

**4.2 Update Parser Dependencies**
- Add project reference: PeopleCodeParser.SelfHosted → PeopleCodeTypeInfo
- Update using statements throughout parser to use new namespaces

## Phase 5: Strip Validation from Inference

**5.1 Clean SimpleTypeInferenceVisitor**
Remove all validation logic (error reporting for type mismatches):
```csharp
// REMOVE all instances of:
_context.ReportError(...);  // when it's about type mismatches

// KEEP only:
_context.RecordTypeInference(node, typeInfo);
_context.RecordUnresolvedType(node, reason);
```

**5.2 Update TypeInferenceContext**
- Keep error/warning infrastructure (we'll use it later for validation)
- Remove type mismatch detection from inference flow
- Focus on just tracking what type each node has

**5.3 Simplify Function Call Handling**
In `DetermineFunctionReturnType()`:
```csharp
private TypeInfo DetermineFunctionReturnType(FunctionCallNode functionCall) {
    // Query PeopleCodeTypeDatabase for function
    var functionInfo = _database.GetFunction(functionName);
    if (functionInfo == null) {
        return UnknownTypeInfo.Instance; // Not an error, just unknown
    }

    // Get argument types (for polymorphic resolution)
    var argTypes = functionCall.Arguments
        .Select(arg => arg.GetInferredType())
        .Where(t => t != null)
        .ToArray();

    // Resolve return type (handles polymorphic types)
    var returnTypes = functionInfo.ResolveReturnTypes(objectType: null, argTypes);

    // For now, just return first type (union handling later)
    return returnTypes.FirstOrDefault()
        ?? TypeInfo.FromPeopleCodeType(functionInfo.ReturnType.Type);
}
```

## Phase 6: Create Skeleton Implementations

**6.1 IProgramSourceProvider**
```csharp
namespace PeopleCodeTypeInfo.Inference;

public interface IProgramSourceProvider {
    Task<(bool found, string? source)> TryGetProgramSourceAsync(string qualifiedClassName);
}

// Skeleton implementation
public class NullProgramSourceProvider : IProgramSourceProvider {
    public Task<(bool, string?)> TryGetProgramSourceAsync(string qualifiedClassName) {
        return Task.FromResult((false, (string?)null));
    }
}
```

**6.2 PeopleCodeTypeDatabase Stub**
```csharp
public class PeopleCodeTypeDatabase {
    public FunctionInfo? GetFunction(string name) => null; // TODO: Load from data
    public BuiltinObjectInfo? GetBuiltinObject(string name) => null; // TODO
    public PropertyInfo? GetProperty(string objectType, string propName) => null; // TODO
}
```

## Phase 7: Update Integration Points

**7.1 Update AstNode Extensions**
- Ensure `GetInferredType()` returns `PeopleCodeTypeInfo.Types.TypeInfo`
- Update extension methods to use new type system

**7.2 Update AppRefiner Integration**
- AppRefiner references both PeopleCodeParser.SelfHosted AND PeopleCodeTypeInfo
- Services that use type inference update to new namespaces

## Phase 8: Clean Up Type Registry

**8.1 Update PeopleCodeTypeRegistry**
Move from Parser to PeopleCodeTypeInfo if it exists, or ensure it uses the unified type system.

## Success Criteria

✅ Parser project has NO type system code (pure parsing)
✅ All type system code lives in PeopleCodeTypeInfo project
✅ Inference uses PeopleCodeTypeInfo's advanced types everywhere
✅ No validation logic in inference (pure type resolution)
✅ Solution compiles and existing tests pass
✅ Can infer return types for builtin functions (even if database returns null for now)

## Migration Order

1. Create PeopleCodeTypeInfo project
2. Copy type system files from external Core
3. Move inference files from Parser
4. Update namespaces and references
5. Remove old TypeSystem folder from Parser
6. Strip validation from inference
7. Add skeleton implementations
8. Test compilation

## Notes

- NO validation logic copied/implemented yet
- Database methods are stubs (will populate later)
- ProgramSourceProvider is skeleton (implement when needed)
- Focus: Get type inference working with advanced type system
- Parser becomes pure parsing, no type knowledge

## Key Decisions Made

1. **Use PeopleCodeTypeInfo types everywhere** - No maintaining separate simple types
2. **Pure inference only** - No validation during type inference pass
3. **Direct dependency** - PeopleCodeParser.SelfHosted references PeopleCodeTypeInfo directly
4. **Defer validation** - All validation happens in separate pass (to be implemented later)
5. **New project in solution** - Copy relevant pieces from external PeopleCodeTypeInfo.Core into AppRefiner solution

## Future Work (Not in This Plan)

- Add actual builtin function/object database loading
- Implement validation visitor using FunctionCallValidator
- Populate ProgramSourceProvider for external class resolution
- Add HashTable support for efficient builtin lookups
- Implement complete type checking as separate pass
