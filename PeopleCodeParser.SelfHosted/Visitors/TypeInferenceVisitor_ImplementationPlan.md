# Type Inference Visitor Implementation Plan

## Overview
Create a `TypeInferenceVisitor` that propagates TypeInfo through the AST by storing inferred types in `node.Attributes["TypeInfo"]`. The visitor inherits from `ScopedAstVisitor<object>` to leverage existing variable tracking.

## File Location
`PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs`

## Class Structure

```csharp
public class TypeInferenceVisitor : ScopedAstVisitor<object>
{
    private readonly ProgramNode _program;
    private readonly TypeMetadata _programMetadata;
    private readonly ITypeMetadataResolver _typeResolver;
    private readonly TypeCache _typeCache;

    private TypeInferenceVisitor(
        ProgramNode program,
        TypeMetadata programMetadata,
        ITypeMetadataResolver typeResolver,
        TypeCache typeCache)
    {
        _program = program;
        _programMetadata = programMetadata;
        _typeResolver = typeResolver;
        _typeCache = typeCache;
    }

    public static TypeInferenceVisitor Run(...)
    public TypeInfo? GetInferredType(AstNode node)
    private void SetInferredType(AstNode node, TypeInfo type)
}
```

## Constructor Parameters

1. **ProgramNode program** - The AST being analyzed (for accessing Functions list)
2. **TypeMetadata programMetadata** - Current program's metadata (for properties, constructor)
3. **ITypeMetadataResolver typeResolver** - For resolving custom types
4. **TypeCache typeCache** - For caching loaded TypeMetadata

## Core Resolution Logic

### Standalone Function Call Resolution (Simplified!)

```csharp
private TypeInfo ResolveFunctionCallReturnType(string functionName, TypeInfo[]? parameterTypes)
{
    // 1. Check program's Functions list (works for all program types)
    //    - AppClass programs: contains imported functions (IsDeclaration)
    //    - Non-class programs: contains imported (IsDeclaration) + implemented (IsImplementation)
    var func = _program.Functions
        .FirstOrDefault(f => f.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));

    if (func != null)
    {
        if (func.IsImplementation)
        {
            // Return type is in the AST as TypeNode - convert directly
            return ConvertTypeNodeToTypeInfo(func.ReturnType);
        }
        else if (func.IsDeclaration)
        {
            // Need to load source program to get return type
            string qualifiedName = $"{func.RecordName}.{func.FieldName}.{func.RecordEvent}";

            // Try cache first
            var sourceMetadata = _typeCache.Get(qualifiedName);
            if (sourceMetadata == null)
            {
                sourceMetadata = _typeResolver.GetTypeMetadata(qualifiedName);
                if (sourceMetadata != null)
                    _typeCache.Set(qualifiedName, sourceMetadata);
            }

            // Get function info from source program
            if (sourceMetadata?.Methods.TryGetValue(functionName, out var importedFunc) == true)
            {
                return ConvertFunctionInfoToTypeInfo(importedFunc, null, parameterTypes);
            }
        }
    }

    // 2. Fallback to builtin function
    var builtinFunc = PeopleCodeTypeDatabase.GetFunction(functionName);
    if (builtinFunc != null)
    {
        return ConvertFunctionInfoToTypeInfo(builtinFunc, null, parameterTypes);
    }

    // 3. Could not resolve
    return UnknownTypeInfo.Instance;
}
```

### Member Access Resolution

```csharp
private TypeInfo ResolveMemberAccessReturnType(
    TypeInfo objectType,
    string memberName,
    bool isMethodCall,
    TypeInfo[]? parameterTypes)
{
    // Builtin types (primitives and builtin objects)
    if (objectType.Kind == TypeKind.BuiltinObject || objectType.Kind == TypeKind.Primitive)
    {
        if (isMethodCall)
        {
            var method = PeopleCodeTypeDatabase.GetMethod(objectType.Name, memberName);
            if (method != null)
                return ConvertFunctionInfoToTypeInfo(method, objectType, parameterTypes);
        }
        else
        {
            var prop = PeopleCodeTypeDatabase.GetProperty(objectType.Name, memberName);
            if (prop != null)
                return ConvertPropertyInfoToTypeInfo(prop);
        }
    }

    // Custom types (AppClass/Interface)
    else if (objectType is AppClassTypeInfo appClassType)
    {
        // Try cache first
        var metadata = _typeCache.Get(appClassType.QualifiedName);

        // If not cached, resolve and cache
        if (metadata == null)
        {
            metadata = _typeResolver.GetTypeMetadata(appClassType.QualifiedName);
            if (metadata != null)
                _typeCache.Set(appClassType.QualifiedName, metadata);
        }

        if (metadata != null)
        {
            if (isMethodCall && metadata.Methods.TryGetValue(memberName, out var method))
                return ConvertFunctionInfoToTypeInfo(method, null, parameterTypes);

            if (!isMethodCall && metadata.Properties.TryGetValue(memberName, out var prop))
                return ConvertPropertyInfoToTypeInfo(prop);
        }
    }

    return UnknownTypeInfo.Instance;
}
```

### Default Method Handling

```csharp
// In VisitFunctionCall when calling directly on expression: &rowset(1) or GetLevel0()(1)
// Pattern: FunctionCallNode where Function is an expression (not just an identifier)

private TypeInfo ResolveDefaultMethodCall(TypeInfo targetType, TypeInfo[]? parameterTypes)
{
    // Only builtin types support default methods
    if (targetType.Kind == TypeKind.BuiltinObject)
    {
        var obj = PeopleCodeTypeDatabase.GetObject(targetType.Name);
        if (obj?.DefaultMethodHash != 0)
        {
            var defaultMethod = obj.LookupMethodByHash(obj.DefaultMethodHash);
            if (defaultMethod != null)
            {
                return ConvertFunctionInfoToTypeInfo(defaultMethod, targetType, parameterTypes);
            }
        }
    }

    return UnknownTypeInfo.Instance;
}
```

## Key Visitor Methods

### Expression Visitors

1. **VisitLiteral(LiteralNode node)**
   - Map `node.LiteralType` to TypeInfo
   - String → StringTypeInfo, Number → NumberTypeInfo/IntegerTypeInfo, Boolean → BooleanTypeInfo

2. **VisitIdentifier(IdentifierNode node)**
   - Use `FindVariable(node.Name)` to get VariableInfo
   - Convert `variableInfo.TypeName` string to TypeInfo
   - Store result

3. **VisitArrayAccess(ArrayAccessNode node)**
   - Visit array expression, get its type
   - Call `ReduceDimensionality(arrayType)`
   - Store result

4. **VisitMemberAccess(MemberAccessNode node)**
   - Visit target expression, get object type
   - Call `ResolveMemberAccessReturnType()` with isMethodCall=false
   - Store result

5. **VisitFunctionCall(FunctionCallNode node)**
   - Determine call pattern:
     - **Standalone call**: `Len(&var)` → node.Function is IdentifierNode
     - **Member call**: `&obj.Method()` → node.Function is MemberAccessNode
     - **Default method call**: `&rowset(1)` → node.Function is some other expression
   - Visit all arguments to get parameter types
   - Resolve return type based on pattern
   - Store result

6. **VisitBinaryOperation(BinaryOperationNode node)**
   - Visit both operands
   - Apply type promotion (e.g., Integer + Number → Number)
   - Store result

7. **VisitUnaryOperation(UnaryOperationNode node)**
   - Visit operand
   - Return appropriate type (e.g., NOT → Boolean, - → Number)

### Optional Declaration Visitors

8. **VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)**
   - Convert node.Type to TypeInfo
   - Store on node (for consistency, though ScopedAstVisitor already tracks this)

## Helper Methods

### Type Conversion

```csharp
// Convert AST TypeNode to type system TypeInfo
private TypeInfo ConvertTypeNodeToTypeInfo(TypeNode? typeNode)
{
    if (typeNode == null) return UnknownTypeInfo.Instance;

    return typeNode switch
    {
        BuiltInTypeNode builtin => TypeInfo.FromPeopleCodeType(builtin.Type),
        ArrayTypeNode array => new ArrayTypeInfo(
            array.Dimensions,
            ConvertTypeNodeToTypeInfo(array.ElementType)),
        AppClassTypeNode appClass => new AppClassTypeInfo(
            string.Join(":", appClass.PackagePath.Append(appClass.ClassName))),
        _ => UnknownTypeInfo.Instance
    };
}

// Convert Functions.TypeWithDimensionality to Types.TypeInfo
private TypeInfo ConvertTypeWithDimensionalityToTypeInfo(TypeWithDimensionality twd)
{
    TypeInfo baseType = twd.IsAppClass
        ? new AppClassTypeInfo(twd.AppClassPath!)
        : TypeInfo.FromPeopleCodeType(twd.Type);

    return twd.IsArray
        ? new ArrayTypeInfo(twd.ArrayDimensionality, baseType)
        : baseType;
}

// Convert FunctionInfo to return TypeInfo (handling polymorphic types)
private TypeInfo ConvertFunctionInfoToTypeInfo(
    FunctionInfo func,
    TypeInfo? objectType,
    TypeInfo[]? parameterTypes)
{
    if (func.IsUnionReturn)
    {
        // Return union of all possible types
        return UnionReturnTypeInfo.FromTypeWithDimensionality(func.ReturnUnionTypes!);
    }
    else if (func.IsPolymorphicReturn)
    {
        // Resolve polymorphic type using context
        var resolved = func.ResolveReturnType(objectType, parameterTypes);
        return ConvertTypeWithDimensionalityToTypeInfo(resolved);
    }
    else
    {
        // Simple type
        return ConvertTypeWithDimensionalityToTypeInfo(func.ReturnType);
    }
}

// Convert PropertyInfo to TypeInfo
private TypeInfo ConvertPropertyInfoToTypeInfo(Functions.PropertyInfo prop)
{
    return ConvertTypeWithDimensionalityToTypeInfo(prop.Type);
}
```

### Array Dimensionality

```csharp
private TypeInfo ReduceDimensionality(TypeInfo type)
{
    if (type is ArrayTypeInfo arrayType)
    {
        if (arrayType.Dimensions == 1)
            return arrayType.ElementType ?? AnyTypeInfo.Instance;
        else
            return new ArrayTypeInfo(arrayType.Dimensions - 1, arrayType.ElementType);
    }

    // Non-array type indexed like array - treat as Any
    return AnyTypeInfo.Instance;
}

private TypeInfo IncreaseDimensionality(TypeInfo type)
{
    return new ArrayTypeInfo(1, type);
}
```

### Storage and Retrieval

```csharp
private void SetInferredType(AstNode node, TypeInfo type)
{
    node.Attributes["TypeInfo"] = type;
}

public TypeInfo? GetInferredType(AstNode node)
{
    return node.Attributes.TryGetValue("TypeInfo", out var t) ? (TypeInfo)t : null;
}
```

## Polymorphic Type Resolution

The `FunctionInfo.ResolveReturnType()` method handles all polymorphic types:
- **SameAsObject**: Returns objectType parameter
- **ElementOfObject**: Reduces objectType dimensionality by 1
- **SameAsFirstParameter**: Returns parameterTypes[0]
- **ArrayOfFirstParameter**: Returns Array of parameterTypes[0]

We just need to call it with the right context and convert the result.

## Error Handling Strategy

- **Cannot resolve type** → Return `UnknownTypeInfo.Instance`
- **ITypeMetadataResolver returns null** → Return `UnknownTypeInfo.Instance`
- **Declared function source not found** → Return `UnknownTypeInfo.Instance`
- **Property/method not found** → Return `UnknownTypeInfo.Instance`
- **Never throw exceptions** - validation pass will handle these later

## Public API

```csharp
/// <summary>
/// Run type inference on a program AST
/// </summary>
public static TypeInferenceVisitor Run(
    ProgramNode program,
    TypeMetadata programMetadata,
    ITypeMetadataResolver typeResolver,
    TypeCache typeCache)
{
    var visitor = new TypeInferenceVisitor(program, programMetadata, typeResolver, typeCache);
    program.Accept(visitor);
    return visitor;
}

/// <summary>
/// Get the inferred type for any AST node
/// </summary>
public TypeInfo? GetInferredType(AstNode node)
{
    return node.Attributes.TryGetValue("TypeInfo", out var t) ? (TypeInfo)t : null;
}
```

## Implementation Order

1. Class skeleton with constructor and public API
2. Helper methods (type conversion, array dimensionality)
3. Simple visitors: literals, identifiers
4. Array access
5. Binary and unary operations
6. Member access (builtin types, then custom types)
7. Function calls (standalone, then member calls, then default methods)
8. Test with sample programs

## Dependencies

- `PeopleCodeTypeInfo.Database.PeopleCodeTypeDatabase` (static)
- `PeopleCodeTypeInfo.Contracts.ITypeMetadataResolver`
- `PeopleCodeTypeInfo.Inference.TypeMetadata`
- `PeopleCodeTypeInfo.Inference.TypeCache`
- `PeopleCodeTypeInfo.Types.*` (TypeInfo, ArrayTypeInfo, AppClassTypeInfo, etc.)
- `PeopleCodeTypeInfo.Functions.*` (FunctionInfo, PropertyInfo, TypeWithDimensionality)
- `PeopleCodeParser.SelfHosted.Visitors.ScopedAstVisitor<T>`
- `PeopleCodeParser.SelfHosted.Nodes.*`
