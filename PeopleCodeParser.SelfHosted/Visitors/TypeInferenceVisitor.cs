using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using System;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// AST visitor that performs type inference by propagating TypeInfo through the AST.
/// Stores inferred types in node.Attributes["TypeInfo"] for later retrieval.
/// </summary>
/// <remarks>
/// This visitor resolves types from:
/// - Builtin types via PeopleCodeTypeDatabase
/// - Custom types via ITypeMetadataResolver
/// - Local/imported functions from the program AST
///
/// Handles polymorphic return types ($same, $element, etc.) and union types.
/// </remarks>
public class TypeInferenceVisitor : ScopedAstVisitor<object>
{
    private readonly ProgramNode _program;
    private readonly TypeMetadata _programMetadata;
    private readonly ITypeMetadataResolver _typeResolver;
    private readonly TypeCache _typeCache;
    private readonly string? _defaultRecordName;
    private readonly string? _defaultFieldName;
    private TypeInferenceVisitor(
        ProgramNode program,
        TypeMetadata programMetadata,
        ITypeMetadataResolver typeResolver,
        TypeCache typeCache,
        string? defaultRecordName = null,
        string? defaultFieldName = null)
    {
        _program = program;
        _programMetadata = programMetadata;
        _typeResolver = typeResolver;
        _typeCache = typeCache;
        _defaultRecordName = defaultRecordName;
        _defaultFieldName = defaultFieldName;
    }

    /// <summary>
    /// Run type inference on a program AST
    /// </summary>
    public static TypeInferenceVisitor Run(
        ProgramNode program,
        TypeMetadata programMetadata,
        ITypeMetadataResolver typeResolver,
        string? defaultRecordName = null,
        string? defaultFieldName = null)
    {
        var visitor = new TypeInferenceVisitor(program, programMetadata, typeResolver, typeResolver.Cache, defaultRecordName, defaultFieldName);

        // Pre-resolve all declared functions for efficient lookup
        visitor.ProcessDeclaredFunctions();

        program.Accept(visitor);
        return visitor;
    }

    /// <summary>
    /// Pre-processes all declared external functions and caches their FunctionInfo.
    /// This makes the metadata available for both type inference and tooltips.
    /// </summary>
    private void ProcessDeclaredFunctions()
    {
        foreach (var func in _program.Functions.Where(f => f.IsDeclaration))
        {
            try
            {
                // Build qualified name from declaration metadata
                string qualifiedName = $"{func.RecordName}.{func.FieldName}.{func.RecordEvent}";

                // Try cache first, then resolver
                TypeMetadata? sourceMetadata = _typeCache.Get(qualifiedName);
                if (sourceMetadata == null && _typeResolver != null)
                {
                    sourceMetadata = _typeResolver.GetTypeMetadata(qualifiedName);
                    if (sourceMetadata != null)
                        _typeCache.Set(qualifiedName, sourceMetadata);
                }

                // Look up the function in source program's methods
                if (sourceMetadata != null &&
                    sourceMetadata.Methods.TryGetValue(func.Name, out var importedFunc))
                {
                    // Store FunctionInfo on the declaration node for reuse
                    func.SetFunctionInfo(importedFunc);
                }
            }
            catch (Exception)
            {
                // If we can't resolve a declared function, continue processing others
                // The function will be unknown at call sites but won't break the entire inference
                continue;
            }
        }
    }

    /// <summary>
    /// Get the inferred type for any AST node
    /// </summary>
    public TypeInfo? GetInferredType(AstNode node)
    {
        return node.GetInferredType();
    }

    /// <summary>
    /// Store inferred type on an AST node.
    /// Normalizes Integer to Number to match PeopleCode's runtime behavior
    /// where integer and number are essentially the same type.
    /// </summary>
    private void SetInferredType(AstNode node, TypeInfo type)
    {
        // Normalize Integer to Number - PeopleCode doesn't meaningfully distinguish them
        if (type.PeopleCodeType == PeopleCodeType.Integer)
        {
            type = new PrimitiveTypeInfo("number",PeopleCodeType.Number) { IsAssignable = type.IsAssignable };
        }

        node.SetInferredType(type);
    }

    #region Type Conversion Helpers

    /// <summary>
    /// Convert AST TypeNode to type system TypeInfo
    /// </summary>
    private TypeInfo ConvertTypeNodeToTypeInfo(TypeNode? typeNode)
    {
        if (typeNode == null)
            return UnknownTypeInfo.Instance;

        return typeNode switch
        {
            BuiltInTypeNode builtin => TypeInfo.FromPeopleCodeType(builtin.Type),
            ArrayTypeNode array => new ArrayTypeInfo(
                array.Dimensions,
                array.ElementType != null
                    ? ConvertTypeNodeToTypeInfo(array.ElementType)
                    : TypeInfo.FromPeopleCodeType(PeopleCodeType.Any)),
            AppClassTypeNode appClass => AppClassTypeInfo.CreateWithInheritanceChain(
                string.Join(":", appClass.PackagePath.Append(appClass.ClassName)), _typeResolver, _typeCache),
            _ => UnknownTypeInfo.Instance
        };
    }

    /// <summary>
    /// Convert Functions.TypeWithDimensionality to Types.TypeInfo
    /// </summary>
    private TypeInfo ConvertTypeWithDimensionalityToTypeInfo(TypeWithDimensionality twd)
    {
        TypeInfo baseType = twd.IsAppClass
            ? AppClassTypeInfo.CreateWithInheritanceChain(twd.AppClassPath!, _typeResolver, _typeCache)
            : TypeInfo.FromPeopleCodeType(twd.Type);

        return twd.IsArray
            ? new ArrayTypeInfo(twd.ArrayDimensionality, baseType)
            : baseType;
    }

    /// <summary>
    /// Convert FunctionInfo to return TypeInfo (handling polymorphic types)
    /// </summary>
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

    /// <summary>
    /// Convert PropertyInfo to TypeInfo
    /// </summary>
    private TypeInfo ConvertPropertyInfoToTypeInfo(PropertyInfo prop)
    {
        return ConvertTypeWithDimensionalityToTypeInfo(prop.ToTypeWithDimensionality());
    }

    /// <summary>
    /// Convert a type name string to TypeInfo
    /// </summary>
    private TypeInfo ConvertTypeNameToTypeInfo(string typeName)
    {
        // Handle "any" type (used for auto-declared variables)
        if (typeName.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return AnyTypeInfo.Instance;
        }

        // Handle array types - look for "array of" pattern
        if (typeName.ToLowerInvariant().Contains("array of"))
        {
            // Parse array type (e.g., "array of string", "array of array of number")
            var parts = typeName.Split(new[] { " of " }, StringSplitOptions.RemoveEmptyEntries);
            int dimensions = 0;
            foreach (var part in parts)
            {
                if (part.Trim().ToLowerInvariant() == "array")
                    dimensions++;
            }
            var elementTypeName = "any";

            if (parts.Length > dimensions)
            {
                /* they used something like "array of array" which has an implicit "any" */
                elementTypeName = parts[^1].Trim();
            }

            var elementType = ConvertTypeNameToTypeInfo(elementTypeName);

            return new ArrayTypeInfo(dimensions, elementType);
        }

        if (typeName.ToLowerInvariant().Equals("array"))
        {
            return new ArrayTypeInfo(1, AnyTypeInfo.Instance);
        }

        // Try to parse as a builtin type
        var peopleCodeType = BuiltinTypeExtensions.FromString(typeName);
        if (peopleCodeType != PeopleCodeType.Unknown)
        {
            return TypeInfo.FromPeopleCodeType(peopleCodeType);
        }

        // Could be an app class - try to resolve
        // For now, create an AppClassTypeInfo with the name
        // The resolver will handle it if it's queried later
        if (typeName.Contains(':'))
        {
            return AppClassTypeInfo.CreateWithInheritanceChain(typeName, _typeResolver, _typeCache);
        }

        // Unknown type
        return new UnknownTypeInfo(typeName);
    }

    #endregion

    #region Array Dimensionality Helpers

    /// <summary>
    /// Reduce array dimensionality by 1 (for array indexing)
    /// </summary>
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

    /// <summary>
    /// Increase array dimensionality by 1
    /// </summary>
    private TypeInfo IncreaseDimensionality(TypeInfo type)
    {
        return new ArrayTypeInfo(1, type);
    }

    #endregion

    #region Resolution Methods

   

    /// <summary>
    /// Resolve return type for member access (property or method call)
    /// </summary>
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
                {
                    return ConvertPropertyInfoToTypeInfo(prop);
                }
            }

            // Special handling for Row and Record types when member not found
            // These types have implicit property/method transformations
            if (objectType.PeopleCodeType.HasValue)
            {
                // Row property access acts as GetRecord()
                // Example: &row.PSADSRELATION acts as &row.GetRecord(Record.PSADSRELATION) → returns Record
                if (objectType.PeopleCodeType.Value == PeopleCodeType.Row && !isMethodCall)
                {
                    return TypeInfo.FromPeopleCodeType(PeopleCodeType.Record);
                }

                // Record property access acts as GetField()
                // Example: &rec.FIELDNAME acts as &rec.GetField(Field.FIELDNAME) → returns Field
                if (objectType.PeopleCodeType.Value == PeopleCodeType.Record && !isMethodCall)
                {
                    return TypeInfo.FromPeopleCodeType(PeopleCodeType.Field);
                }

                // Row method call to non-existent method acts as GetRowset().GetRow()
                // Example: &row.TEST(3) acts as &row.GetRowset(Scroll.TEST).GetRow(3) → returns Row
                if (objectType.PeopleCodeType.Value == PeopleCodeType.Row && isMethodCall)
                {
                    return TypeInfo.FromPeopleCodeType(PeopleCodeType.Row);
                }
            }
        }

        // Custom types (AppClass/Interface)
        else if (objectType is AppClassTypeInfo appClassType)
        {
            if (isMethodCall)
            {
                // Walk up the inheritance chain to find the method
                var method = LookupMethodInInheritanceChain(appClassType, memberName);
                if (method != null)
                    return ConvertFunctionInfoToTypeInfo(method, null, parameterTypes);
            }
            else
            {
                // Walk up the inheritance chain to find the property
                var property = LookupPropertyInInheritanceChain(appClassType, memberName);
                if (property != null)
                    return ConvertPropertyInfoToTypeInfo(property);

                /* %This.foo can reference a private variable &foo */
                property = LookupPrivateInstanceVariable(appClassType, memberName);
                if (property != null)
                    return ConvertPropertyInfoToTypeInfo(property);
            }
        }

        return AnyTypeInfo.Instance;
    }


    #endregion

    #region Inheritance Chain Helpers

    /// <summary>
    /// Look up a method in an AppClass, walking up the inheritance chain if necessary.
    /// </summary>
    /// <param name="appClassType">The type to start searching from</param>
    /// <param name="methodName">The method name to find</param>
    /// <returns>The FunctionInfo for the method, or null if not found</returns>
    private FunctionInfo? LookupMethodInInheritanceChain(AppClassTypeInfo appClassType, string methodName)
    {
        var currentClassName = appClassType.QualifiedName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentClassName };

        while (!string.IsNullOrEmpty(currentClassName))
        {
            // Get metadata for the current class
            TypeMetadata? metadata = null;

            // Special case: if this is the current class, use _programMetadata directly
            if (currentClassName.Equals(_programMetadata.QualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                metadata = _programMetadata;
            }
            else
            {
                // Try cache first, then resolver
                metadata = _typeCache.Get(currentClassName);
                if (metadata == null && _typeResolver != null)
                {
                    metadata = _typeResolver.GetTypeMetadata(currentClassName);
                    if (metadata != null)
                        _typeCache.Set(currentClassName, metadata);
                }
            }

            // If we have metadata, check for the method
            if (metadata != null && metadata.Methods.TryGetValue(methodName, out var method))
            {
                return method;
            }

            // Check if we've reached a builtin base class
            if (metadata != null && metadata.IsBaseClassBuiltin && metadata.BuiltinBaseType.HasValue)
            {
                // Look up method in the builtin type database
                var builtinTypeName = metadata.BuiltinBaseType.Value.GetTypeName();
                var builtinMethod = PeopleCodeTypeDatabase.GetMethod(builtinTypeName, methodName);
                if (builtinMethod != null)
                {
                    return builtinMethod;
                }
                break; // Builtin types don't have further inheritance to traverse
            }

            // Move to the next class in hierarchy - check both BaseClassName and InterfaceName
            // (PeopleCode allows either extends or implements, but not both, and they're interchangeable)
            string? nextClassName = null;
            if (metadata != null)
            {
                if (!string.IsNullOrEmpty(metadata.BaseClassName))
                {
                    nextClassName = metadata.BaseClassName;
                }
                else if (!string.IsNullOrEmpty(metadata.InterfaceName))
                {
                    nextClassName = metadata.InterfaceName;
                }
            }

            if (nextClassName == null)
            {
                break; // No more base classes/interfaces
            }

            // Circular inheritance detection
            if (!visited.Add(nextClassName))
            {
                break; // Detected circular inheritance
            }

            currentClassName = nextClassName;
        }

        return null; // Method not found in the hierarchy
    }

    private PropertyInfo? LookupPrivateInstanceVariable(AppClassTypeInfo classType, string propertyName)
    {

        // If we have metadata, check for the property
        if (_programMetadata != null && _programMetadata.InstanceVariables.TryGetValue($"&{propertyName}", out var property))
        {
            return property;
        }
        return null;
    }

    /// <summary>
    /// Look up a property in an AppClass, walking up the inheritance chain if necessary.
    /// </summary>
    /// <param name="appClassType">The type to start searching from</param>
    /// <param name="propertyName">The property name to find</param>
    /// <returns>The PropertyInfo for the property, or null if not found</returns>
    private PropertyInfo? LookupPropertyInInheritanceChain(AppClassTypeInfo appClassType, string propertyName)
    {
        var currentClassName = appClassType.QualifiedName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentClassName };

        while (!string.IsNullOrEmpty(currentClassName))
        {
            // Get metadata for the current class
            TypeMetadata? metadata = null;

            // Special case: if this is the current class, use _programMetadata directly
            if (currentClassName.Equals(_programMetadata.QualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                metadata = _programMetadata;
            }
            else
            {
                // Try cache first, then resolver
                metadata = _typeCache.Get(currentClassName);
                if (metadata == null && _typeResolver != null)
                {
                    metadata = _typeResolver.GetTypeMetadata(currentClassName);
                    if (metadata != null)
                        _typeCache.Set(currentClassName, metadata);
                }
            }

            // If we have metadata, check for the property
            if (metadata != null && metadata.Properties.TryGetValue(propertyName, out var property))
            {
                return property;
            }

            // Check if we've reached a builtin base class
            if (metadata != null && metadata.IsBaseClassBuiltin && metadata.BuiltinBaseType.HasValue)
            {
                // Look up property in the builtin type database
                var builtinTypeName = metadata.BuiltinBaseType.Value.GetTypeName();
                var builtinProperty = PeopleCodeTypeDatabase.GetProperty(builtinTypeName, propertyName);
                if (builtinProperty != null)
                {
                    return builtinProperty;
                }
                break; // Builtin types don't have further inheritance to traverse
            }

            // Move to the next class in hierarchy - check both BaseClassName and InterfaceName
            // (PeopleCode allows either extends or implements, but not both, and they're interchangeable)
            string? nextClassName = null;
            if (metadata != null)
            {
                if (!string.IsNullOrEmpty(metadata.BaseClassName))
                {
                    nextClassName = metadata.BaseClassName;
                }
                else if (!string.IsNullOrEmpty(metadata.InterfaceName))
                {
                    nextClassName = metadata.InterfaceName;
                }
            }

            if (nextClassName == null)
            {
                break; // No more base classes/interfaces
            }

            // Circular inheritance detection
            if (!visited.Add(nextClassName))
            {
                break; // Detected circular inheritance
            }

            currentClassName = nextClassName;
        }

        return null; // Property not found in the hierarchy
    }

    #endregion

    private FunctionInfo? ResolveFunctionInfo(FunctionCallNode node, TypeInfo[] parameterTypes)
    {
        if (node.Function is IdentifierNode identifier)
        {
            var func = _program.Functions.FirstOrDefault(f => f.Name.Equals(identifier.Name, StringComparison.OrdinalIgnoreCase));
            if (func != null)
            {
                if (func.IsImplementation)
                {
                    _programMetadata.Methods.TryGetValue(identifier.Name, out var method);
                    return method;
                }
                else if (func.IsDeclaration)
                {
                    // FunctionInfo should already be cached on the declaration node by ProcessDeclaredFunctions
                    var cachedFuncInfo = func.GetFunctionInfo();
                    if (cachedFuncInfo != null)
                        return cachedFuncInfo;

                    // Fallback: resolve on-demand (shouldn't normally happen after ProcessDeclaredFunctions)
                    string qualifiedName = $"{func.RecordName}.{func.FieldName}.{func.RecordEvent}";
                    var sourceMetadata = _typeCache.Get(qualifiedName) ?? _typeResolver?.GetTypeMetadata(qualifiedName);
                    if (sourceMetadata != null && sourceMetadata.Methods.TryGetValue(identifier.Name, out var importedFunc))
                    {
                        return importedFunc;
                    }
                }
            }

            var builtinFunc = PeopleCodeTypeDatabase.GetFunction(identifier.Name);
            if (builtinFunc != null) return builtinFunc;

            var identifierType = GetInferredType(identifier);
            if (identifierType != null && identifierType.Kind == TypeKind.BuiltinObject)
            {
                var obj = PeopleCodeTypeDatabase.GetObject(identifierType.Name);
                if (obj is not null && obj?.DefaultMethodHash != 0)
                {
                    return obj.LookupMethodByHash(obj.DefaultMethodHash);
                }
            }
        }
        else if (node.Function is MemberAccessNode memberAccess)
        {
            var objectType = GetInferredType(memberAccess.Target);
            if (objectType != null)
            {
                if (objectType.Kind == TypeKind.BuiltinObject || objectType.Kind == TypeKind.Primitive)
                {
                    return PeopleCodeTypeDatabase.GetMethod(objectType.Name, memberAccess.MemberName);
                }
                else if (objectType is AppClassTypeInfo appClassType)
                {
                    // Walk up the inheritance chain to find the method
                    return LookupMethodInInheritanceChain(appClassType, memberAccess.MemberName);
                } else if (objectType.Kind == TypeKind.Array)
                {
                    return PeopleCodeTypeDatabase.GetMethod("Array", memberAccess.MemberName);
                }
            }
        }
        else
        {
            var targetType = GetInferredType(node.Function);
            if (targetType != null && targetType.Kind == TypeKind.BuiltinObject)
            {
                var obj = PeopleCodeTypeDatabase.GetObject(targetType.Name);
                if (obj?.DefaultMethodHash != 0)
                {
                    return obj.LookupMethodByHash(obj.DefaultMethodHash);
                }
            }
        }

        return null;
    }

    #region Statement Visitors

    /// <summary>
    /// Visit assignment and update auto-declared variable types on first assignment
    /// </summary>
    public override void VisitAssignment(AssignmentNode node)
    {
        // Let base class handle the assignment (reference tracking, etc.)
        base.VisitAssignment(node);

        // Check if the assignment target is an identifier (simple variable assignment)
        if (node.Target is IdentifierNode identifier)
        {
            // Find the variable in scope
            var variable = FindVariable(identifier.Name);

            // If it's an auto-declared variable with type "any", update its type from the right-hand side
            if (variable != null && variable.IsAutoDeclared && variable.Type.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                // Infer type from the right-hand side expression
                var rightHandType = GetInferredType(node.Value);
                if (rightHandType != null && rightHandType.Name != null && !rightHandType.Name.Equals("any", StringComparison.OrdinalIgnoreCase))
                {
                    rightHandType.IsAutoDeclared = true;
                    // Update the variable's type
                    variable.SetInferredType(rightHandType);
                }
            }
        }
    }

    #endregion

    #region Expression Visitors

    /// <summary>
    /// Visits class constant references like those used in %metadata:Key
    /// </summary>
    /// <param name="node"></param>
    public override void VisitClassConstant(ClassConstantNode node)
    {
        base.VisitClassConstant(node);
        SetInferredType(node, AnyTypeInfo.Instance);
    }

    /// <summary>
    /// Visit literal expression and infer type from literal value
    /// </summary>
    public override void VisitLiteral(LiteralNode node)
    {
        base.VisitLiteral(node);

        TypeInfo inferredType = node.LiteralType switch
        {
            LiteralType.String => PrimitiveTypeInfo.String,
            LiteralType.Integer => PrimitiveTypeInfo.Integer,
            LiteralType.Decimal => PrimitiveTypeInfo.Number,
            LiteralType.Boolean => PrimitiveTypeInfo.Boolean,
            LiteralType.Null => AnyTypeInfo.Instance,  // Null can be any type
            _ => UnknownTypeInfo.Instance
        };

        SetInferredType(node, inferredType);
    }

    /// <summary>
    /// Visit identifier and resolve type from variable tracking
    /// </summary>
    public override void VisitIdentifier(IdentifierNode node)
    {
        base.VisitIdentifier(node);

        if (node.Name.Equals("^"))
        {
            if (_defaultRecordName != null && _defaultFieldName != null)
            {
                var fieldType = new FieldTypeInfo(_defaultRecordName, _defaultFieldName, _typeResolver);
                SetInferredType(node, fieldType);
                return;
            }
        }

        // Special handling for %This - resolve to current class type
        if (node.Name.Equals("%This", StringComparison.OrdinalIgnoreCase))
        {
            // %This refers to the current instance of the class being analyzed
           if (_programMetadata.Kind == ProgramKind.AppClass || _programMetadata.Kind == ProgramKind.Interface)
            {
                var thisType = AppClassTypeInfo.CreateWithInheritanceChain(_programMetadata.QualifiedName, _typeResolver, _typeCache);
                SetInferredType(node, thisType);
            }
            else
            {
                // %This only makes sense in class context
                SetInferredType(node, UnknownTypeInfo.Instance);
            }
        }
        // Special handling for %Super - resolve to base class type
        else if (node.Name.Equals("%Super", StringComparison.OrdinalIgnoreCase))
        {
            // %Super refers to the base class of the current class
            if ((_programMetadata.Kind == ProgramKind.AppClass || _programMetadata.Kind == ProgramKind.Interface)
                && !string.IsNullOrEmpty(_programMetadata.BaseClassName))
            {
                // Check if the base class is a builtin type
                if (_programMetadata.IsBaseClassBuiltin && _programMetadata.BuiltinBaseType.HasValue)
                {
                    // %Super refers to a builtin type - use the builtin type directly
                    var superType = TypeInfo.FromPeopleCodeType(_programMetadata.BuiltinBaseType.Value);
                    SetInferredType(node, superType);
                }
                else
                {
                    // %Super refers to another AppClass
                    var superType = AppClassTypeInfo.CreateWithInheritanceChain(_programMetadata.BaseClassName, _typeResolver, _typeCache);
                    SetInferredType(node, superType);
                }
            }
            else
            {
                // %Super only makes sense when there's a base class
                SetInferredType(node, UnknownTypeInfo.Instance);
            }
        }
        else
        {
            var variable = FindVariable(node.Name);

            if (variable != null)
            {
                // Convert type name string to TypeInfo
                var inferredType = variable.InferredType ?? ConvertTypeNameToTypeInfo(variable.Type);
                var isAutoDeclared = variable.IsAutoDeclared;

                // Variables (identifiers starting with &) are assignable
                // Create a new instance if needed to avoid modifying singletons
                if (node.Name.StartsWith("&"))
                {
                    if (inferredType is PrimitiveTypeInfo primitive)
                    {
                        // Create new instance for primitives to avoid modifying singletons
                        inferredType = new PrimitiveTypeInfo(primitive.Name, primitive.PeopleCodeType);
                        inferredType.IsAssignable = true;
                        inferredType.IsAutoDeclared = isAutoDeclared;
                    }
                    else if (inferredType is ArrayTypeInfo arrayType)
                    {
                        // Arrays are created fresh, can modify directly
                        inferredType.IsAssignable = true;
                        inferredType.IsAutoDeclared = isAutoDeclared;
                    }
                    else if (inferredType is BuiltinObjectTypeInfo || inferredType is AppClassTypeInfo)
                    {
                        // These are typically created fresh or can be marked assignable
                        inferredType.IsAssignable = true;
                        inferredType.IsAutoDeclared = isAutoDeclared;
                    }
                    // For other types (Any, Unknown, etc.), leave as-is since they're singletons
                }

                SetInferredType(node, inferredType);
            }
            else
            {

                // Not a declared variable, check if it's a system variable.
                // System variables (like %UserId) are not prefixed with '&'.
                var systemVar = PeopleCodeTypeDatabase.GetSystemVariable(node.Name);
                if (systemVar != null)
                {
                    SetInferredType(node, ConvertPropertyInfoToTypeInfo(systemVar));
                }
                // If not found and has no prefix, assume it's a Field identifier with unknown record context
                // Pattern: [recordname.]fieldname where recordname is inferred at runtime
                // Example: START_DT (no & or %) → Field type with empty record name
                else if (!node.Name.StartsWith("&") && !node.Name.StartsWith("%"))
                {
                    if (_defaultRecordName != null)
                    {
                        /* We're in a record field PPC and have a default record name */
                        var fieldType = new FieldTypeInfo(_defaultRecordName, node.Name, _typeResolver);
                        SetInferredType(node, fieldType);
                    }
                    else
                    {
                        // Create RecordTypeInfo
                        var recordType = new RecordTypeInfo(node.Name, _typeResolver);
                        SetInferredType(node, recordType);
                    }
                }
                else
                {
                    SetInferredType(node, UnknownTypeInfo.Instance);
                }
            }
        }
    }

    /// <summary>
    /// Visit array access and reduce dimensionality
    /// </summary>
    public override void VisitArrayAccess(ArrayAccessNode node)
    {
        base.VisitArrayAccess(node);

        // Get the array type and reduce dimensionality
        var arrayType = GetInferredType(node.Array);
        if (arrayType != null)
        {
            // Check if we're trying to index a non-array type
            // If invalid, record error on THIS node and propagate Unknown (not Invalid)
            if (arrayType is not ArrayTypeInfo && arrayType is not AnyTypeInfo && arrayType is not UnknownTypeInfo)
            {
                var invalid = new InvalidTypeInfo(
                    $"Cannot index type '{arrayType}' - indexing requires an array type");
                node.SetTypeError(new TypeError(invalid.Reason, node));
                SetInferredType(node, UnknownTypeInfo.Instance);
                return;
            }

            // Each index reduces dimensionality by 1
            var elementType = arrayType;
            for (int i = 0; i < node.Indices.Count; i++)
            {
                elementType = ReduceDimensionality(elementType);
            }

            // Array access is assignable if the base array is assignable
            // e.g., &arr[1] is assignable if &arr is a variable
            // Create a new instance to avoid modifying singletons
            if (arrayType.IsAssignable)
            {
                if (elementType is PrimitiveTypeInfo primitive)
                {
                    elementType = new PrimitiveTypeInfo(primitive.Name, primitive.PeopleCodeType);
                    elementType.IsAssignable = true;
                }
                else if (elementType is ArrayTypeInfo || elementType is BuiltinObjectTypeInfo || elementType is AppClassTypeInfo)
                {
                    // These types can be marked assignable (they're either fresh or safe to modify)
                    elementType.IsAssignable = true;
                }
                // For singletons like Any, Unknown, leave as-is
            }

            SetInferredType(node, elementType);
        }
        else
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
        }
    }

    /// <summary>
    /// Visit binary operation and apply type promotion rules
    /// </summary>
    public override void VisitBinaryOperation(BinaryOperationNode node)
    {
        base.VisitBinaryOperation(node);

        var leftType = GetInferredType(node.Left);
        var rightType = GetInferredType(node.Right);

        if (leftType == null || rightType == null)
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
            return;
        }

        // Validate operation compatibility before inferring result type
        // If invalid, record error on THIS node and propagate Unknown (not Invalid)
        if (IsArithmeticOperator(node.Operator))
        {
            if (!CanPerformArithmetic(leftType) || !CanPerformArithmetic(rightType))
            {
                var invalid = new InvalidTypeInfo(
                    $"Cannot perform arithmetic operation '{OperatorToString(node.Operator)}' on types '{leftType}' and '{rightType}'");
                node.SetTypeError(new TypeError(invalid.Reason, node));
                SetInferredType(node, UnknownTypeInfo.Instance);
                return;
            }
        }
        else if (IsLogicalOperator(node.Operator))
        {
            if (!CanPerformLogical(leftType) || !CanPerformLogical(rightType))
            {
                var invalid = new InvalidTypeInfo(
                    $"Cannot perform logical operation '{OperatorToString(node.Operator)}' on types '{leftType}' and '{rightType}'");
                node.SetTypeError(new TypeError(invalid.Reason, node));
                SetInferredType(node, UnknownTypeInfo.Instance);
                return;
            }
        }
        // Concatenation operator (|) accepts any type and coerces to string at runtime
        // No type checking needed for BinaryOperator.Concatenate

        // Special handling for date/time arithmetic (Add and Subtract only)
        TypeInfo resultType;
        if (node.Operator is BinaryOperator.Add or BinaryOperator.Subtract)
        {
            resultType = InferDateTimeArithmetic(node, leftType, rightType);
        }
        else
        {
            // Non-date/time operators use standard logic
            resultType = node.Operator switch
            {
                // Comparison operators always return boolean
                BinaryOperator.Equal or BinaryOperator.NotEqual or
                BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual or
                BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual
                    => PrimitiveTypeInfo.Boolean,

                // Logical operators return boolean
                BinaryOperator.And or BinaryOperator.Or
                    => PrimitiveTypeInfo.Boolean,

                // String concatenation (pipe operator) always returns string
                BinaryOperator.Concatenate
                    => PrimitiveTypeInfo.String,

                // Other arithmetic operators - apply type promotion
                BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Power
                    => leftType.GetCommonType(rightType),

                _ => leftType.GetCommonType(rightType)
            };
        }

        SetInferredType(node, resultType);
    }

    /// <summary>
    /// Visit unary operation and infer result type
    /// </summary>
    public override void VisitUnaryOperation(UnaryOperationNode node)
    {
        base.VisitUnaryOperation(node);

        var operandType = GetInferredType(node.Operand);
        if (operandType == null)
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
            return;
        }

        TypeInfo resultType = node.Operator switch
        {
            UnaryOperator.Not => PrimitiveTypeInfo.Boolean,
            UnaryOperator.Negate => operandType,  // Preserve numeric type
            UnaryOperator.Reference => new ReferenceTypeInfo(PeopleCodeType.Any, "Dynamic Reference", "Dynamic Reference"), 
            _ => operandType
        };

        SetInferredType(node, resultType);
    }

    /// <summary>
    /// Visit member access (property access, not method call)
    /// </summary>
    public override void VisitMemberAccess(MemberAccessNode node)
    {
        base.VisitMemberAccess(node);

        // Check for special reference keywords (Record.X, Field.Y, etc.)
        // These create ReferenceTypeInfo, not builtin types
        if (node.Target is IdentifierNode identifier &&
            !identifier.Name.StartsWith("&") &&
            !identifier.Name.StartsWith("%") &&
            ReferenceTypeInfo.IsSpecialReferenceKeyword(identifier.Name))
        {
            // Special keyword like Record.MY_RECORD, Field.MY_FIELD
            var referenceCategory = ReferenceTypeInfo.GetReferenceCategoryType(identifier.Name);
            var fullReference = $"{identifier.Name}.{node.MemberName}";
            var refType = new ReferenceTypeInfo(referenceCategory, node.MemberName, fullReference);
            SetInferredType(node, refType);
            return;
        }

        // Normal member access - get target type and resolve member
        var objectType = GetInferredType(node.Target);
        if (objectType != null)
        {
            // Special case: Field with empty record name in member access position
            // This means RECORD.FIELD pattern where RECORD was inferred as Field with empty record
            // Treat the field name as the record name for this access
            if (objectType is FieldTypeInfo fieldWithEmptyRecord &&
                string.IsNullOrEmpty(fieldWithEmptyRecord.RecordName) &&
                node.Target is IdentifierNode targetIdentifier)
            {
                // Create FieldTypeInfo with record context: RECORD.FIELD
                // This is assignable (can be used for out parameters)
                var memberType = new FieldTypeInfo(targetIdentifier.Name, node.MemberName, _typeResolver);
                memberType.IsAssignable = true;
                SetInferredType(node, memberType);
                return;
            }

            if (objectType is FieldTypeInfo fieldType && node.MemberName == "Value")
            {
                // field.Value is assignable (can be used for out parameters)
                // Get the field's data type and create a new instance to set IsAssignable
                var underlyingType = fieldType.GetFieldDataType();
                TypeInfo memberType;

                if (underlyingType is PrimitiveTypeInfo primitive)
                {
                    // Create a new PrimitiveTypeInfo instance so we don't modify singletons
                    memberType = new PrimitiveTypeInfo(primitive.Name, primitive.PeopleCodeType);
                }
                else
                {
                    // For other types, use the instance directly
                    // (builtin objects, app classes, etc. should be assignable as-is)
                    memberType = underlyingType;
                }

                memberType.IsAssignable = true;
                SetInferredType(node, memberType);
                return;
            }

            // Resolve member as property (not method call)
            var memberType2 = ResolveMemberAccessReturnType(objectType, node.MemberName, isMethodCall: false, parameterTypes: null);
            memberType2.IsAssignable = true;
            // If result is Field type and target is Record, create FieldTypeInfo for implicit .Value support
            if (memberType2.PeopleCodeType == PeopleCodeType.Field &&
                objectType.PeopleCodeType == PeopleCodeType.Record &&
                node.Target is IdentifierNode recordIdentifier)
            {
                // Create FieldTypeInfo with record/field context for data type resolution
                // This is assignable (can be used for out parameters)
                memberType2 = new FieldTypeInfo(recordIdentifier.Name, node.MemberName, _typeResolver);
                memberType2.IsAssignable = true;
            }



            /* This handles the pattern &rowset.GetRow(1).RECORD_NAME) */
            if (objectType.PeopleCodeType == PeopleCodeType.Row && memberType2.PeopleCodeType == PeopleCodeType.Record)
            {
                // Create a new Record instance that can be marked as assignable
                memberType2 = new BuiltinObjectTypeInfo("Record", PeopleCodeType.Record);
                // Record/Row/Field can be used for out parameters
                memberType2.IsAssignable = true;
            }

            SetInferredType(node, memberType2);
        }
        else
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
        }
    }

    /// <summary>
    /// Visit function call and resolve return type
    /// </summary>
    public override void VisitFunctionCall(FunctionCallNode node)
    {
        base.VisitFunctionCall(node);

        var parameterTypes = new TypeInfo[node.Arguments.Count];
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            parameterTypes[i] = GetInferredType(node.Arguments[i]) ?? UnknownTypeInfo.Instance;
        }

        var functionInfo = ResolveFunctionInfo(node, parameterTypes);
        if (functionInfo != null)
        {
            node.SetFunctionInfo(functionInfo);

            TypeInfo returnType;
            if (node.Function is MemberAccessNode memberAccess)
            {
                var objectType = GetInferredType(memberAccess.Target);
                returnType = ConvertFunctionInfoToTypeInfo(functionInfo, objectType, parameterTypes);
            }
            else
            {
                /* Special case to support CreateArray() with no args */
                if (node.Function is IdentifierNode ident && ident.Name.ToLower().Equals("createarray") && parameterTypes.Length == 0)
                {
                    /* Artificially put in an "any" as the parameter type */
                    parameterTypes = [AnyTypeInfo.Instance];
                }
                returnType = ConvertFunctionInfoToTypeInfo(functionInfo, null, parameterTypes);
            }
            SetInferredType(node, returnType);
        }
        else
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
        }
    }

    /// <summary>
    /// Visit object creation expression and infer type from the class being created
    /// </summary>
    public override void VisitObjectCreation(ObjectCreationNode node)
    {
        // Let the base visitor handle traversal and any other logic first.
        base.VisitObjectCreation(node);

        // The type of the expression is the type of the object being created.
        var inferredType = ConvertTypeNodeToTypeInfo(node.Type);
        SetInferredType(node, inferredType);

        //var qualifiedName = node.Type
        if (node.Type is AppClassTypeNode act)
        {
            var qualifiedName = act.QualifiedName;

            // Try cache first, then resolver
            TypeMetadata? sourceMetadata = _typeCache.Get(qualifiedName);
            if (sourceMetadata == null && _typeResolver != null)
            {
                sourceMetadata = _typeResolver.GetTypeMetadata(qualifiedName);
                if (sourceMetadata != null)
                    _typeCache.Set(qualifiedName, sourceMetadata);
            }

            // Look up the constructor 
            if (sourceMetadata != null &&
                sourceMetadata.Constructor != null && sourceMetadata.Constructor.Name == act.ClassName)
            {
                node.SetFunctionInfo(sourceMetadata.Constructor);
            }

        }

    }

    /// <summary>
    /// Visit parenthesized expression and propagate inner type
    /// </summary>
    public override void VisitParenthesized(ParenthesizedExpressionNode node)
    {
        base.VisitParenthesized(node);

        // Propagate the inner expression's type to the parenthesized node
        var innerType = GetInferredType(node.Expression);
        if (innerType != null)
        {
            SetInferredType(node, innerType);
        }
        else
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
        }
    }

    /// <summary>
    /// Visit type cast (AS) expression and infer the target type
    /// </summary>
    public override void VisitTypeCast(TypeCastNode node)
    {
        base.VisitTypeCast(node);

        // The result type of a cast is always the target type
        var targetType = ConvertTypeNodeToTypeInfo(node.TargetType);
        SetInferredType(node, targetType);
    }

    #endregion

    #region Operation Validation Helpers

    /// <summary>
    /// Check if an operator is an arithmetic operator
    /// </summary>
    private bool IsArithmeticOperator(BinaryOperator op) =>
        op is BinaryOperator.Add or BinaryOperator.Subtract or
              BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Power;

    /// <summary>
    /// Check if an operator is a logical operator
    /// </summary>
    private bool IsLogicalOperator(BinaryOperator op) =>
        op is BinaryOperator.And or BinaryOperator.Or;

    /// <summary>
    /// Check if a type can be used in arithmetic operations
    /// Includes numeric types (Number, Integer) and date/time types (Date, Time, DateTime)
    /// </summary>
    private bool CanPerformArithmetic(TypeInfo type) =>
        type is PrimitiveTypeInfo { PeopleCodeType:
            PeopleCodeType.Number or
            PeopleCodeType.Integer or
            PeopleCodeType.Date or
            PeopleCodeType.Time or
            PeopleCodeType.DateTime } ||
        type is AnyTypeInfo ||
        type is UnknownTypeInfo;

    /// <summary>
    /// Check if a type can be used in logical operations
    /// </summary>
    private bool CanPerformLogical(TypeInfo type) =>
        type is PrimitiveTypeInfo { PeopleCodeType: PeopleCodeType.Boolean } ||
        type is AnyTypeInfo ||
        type is UnknownTypeInfo;

    /// <summary>
    /// Infer the result type for date/time arithmetic operations
    /// Handles special rules for Add and Subtract with Date, Time, and DateTime types
    /// </summary>
    private TypeInfo InferDateTimeArithmetic(BinaryOperationNode node, TypeInfo leftType, TypeInfo rightType)
    {
        var left = (leftType as PrimitiveTypeInfo)?.PeopleCodeType;
        var right = (rightType as PrimitiveTypeInfo)?.PeopleCodeType;

        // Handle Any and Unknown types - they can participate in date/time arithmetic
        if (leftType is AnyTypeInfo || rightType is AnyTypeInfo ||
            leftType is UnknownTypeInfo || rightType is UnknownTypeInfo)
        {
            // If one side is a known date/time type and the other is any/unknown,
            // we can't infer a specific result type
            if (left.HasValue || right.HasValue)
            {
                return leftType.GetCommonType(rightType);
            }
        }

        // Both must be primitive types for date/time arithmetic
        if (!left.HasValue || !right.HasValue)
        {
            return leftType.GetCommonType(rightType);
        }

        // Apply date/time arithmetic rules
        var isAdd = node.Operator == BinaryOperator.Add;
        var isSubtract = node.Operator == BinaryOperator.Subtract;

        // time + number => time (both orders)
        if (isAdd && ((left == PeopleCodeType.Time && right == PeopleCodeType.Number) ||
                      (left == PeopleCodeType.Number && right == PeopleCodeType.Time)))
        {
            return PrimitiveTypeInfo.Time;
        }

        // time - number => time (only left to right)
        if (isSubtract && left == PeopleCodeType.Time && right == PeopleCodeType.Number)
        {
            return PrimitiveTypeInfo.Time;
        }

        // date + number => date (both orders)
        if (isAdd && ((left == PeopleCodeType.Date && right == PeopleCodeType.Number) ||
                      (left == PeopleCodeType.Number && right == PeopleCodeType.Date)))
        {
            return PrimitiveTypeInfo.Date;
        }

        // date - number => date (only left to right)
        if (isSubtract && left == PeopleCodeType.Date && right == PeopleCodeType.Number)
        {
            return PrimitiveTypeInfo.Date;
        }

        // date - date => number
        if (isSubtract && left == PeopleCodeType.Date && right == PeopleCodeType.Date)
        {
            return PrimitiveTypeInfo.Number;
        }

        // time - time => number
        if (isSubtract && left == PeopleCodeType.Time && right == PeopleCodeType.Time)
        {
            return PrimitiveTypeInfo.Number;
        }

        // date + time => datetime (both orders)
        if (isAdd && ((left == PeopleCodeType.Date && right == PeopleCodeType.Time) ||
                      (left == PeopleCodeType.Time && right == PeopleCodeType.Date)))
        {
            return PrimitiveTypeInfo.DateTime;
        }

        // datetime - datetime => number
        if (isSubtract && left == PeopleCodeType.DateTime && right == PeopleCodeType.DateTime)
        {
            return PrimitiveTypeInfo.Number;
        }

        // Invalid combinations - generate type errors

        // datetime - time is not allowed
        if (isSubtract && left == PeopleCodeType.DateTime && right == PeopleCodeType.Time)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract time from datetime - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // time - datetime is not allowed
        if (isSubtract && left == PeopleCodeType.Time && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract datetime from time - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // datetime + datetime is not allowed
        if (isAdd && left == PeopleCodeType.DateTime && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add datetime to datetime - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // datetime + time is not allowed (date + time is allowed, but datetime + time is not)
        if (isAdd && left == PeopleCodeType.DateTime && right == PeopleCodeType.Time)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add time to datetime - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // time + datetime is not allowed
        if (isAdd && left == PeopleCodeType.Time && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add datetime to time - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // datetime + date is not allowed
        if (isAdd && left == PeopleCodeType.DateTime && right == PeopleCodeType.Date)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add date to datetime - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // date + datetime is not allowed
        if (isAdd && left == PeopleCodeType.Date && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add datetime to date - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // datetime - date is not allowed
        if (isSubtract && left == PeopleCodeType.DateTime && right == PeopleCodeType.Date)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract date from datetime - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // date - datetime is not allowed
        if (isSubtract && left == PeopleCodeType.Date && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract datetime from date - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // date - time is not allowed
        if (isSubtract && left == PeopleCodeType.Date && right == PeopleCodeType.Time)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract time from date - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // time - date is not allowed
        if (isSubtract && left == PeopleCodeType.Time && right == PeopleCodeType.Date)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract date from time - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // datetime + number is not allowed
        if (isAdd && left == PeopleCodeType.DateTime && right == PeopleCodeType.Number)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add number to datetime - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // number + datetime is not allowed
        if (isAdd && left == PeopleCodeType.Number && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add datetime to number - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // datetime - number is not allowed
        if (isSubtract && left == PeopleCodeType.DateTime && right == PeopleCodeType.Number)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract number from datetime - this operation is not supported");
            node.SetTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // If we get here, it's a normal numeric operation
        return leftType.GetCommonType(rightType);
    }

    /// <summary>
    /// Convert a binary operator to its string representation
    /// </summary>
    private string OperatorToString(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Power => "**",
        BinaryOperator.Concatenate => "|",
        BinaryOperator.And => "And",
        BinaryOperator.Or => "Or",
        _ => op.ToString()
    };

    #endregion
}
