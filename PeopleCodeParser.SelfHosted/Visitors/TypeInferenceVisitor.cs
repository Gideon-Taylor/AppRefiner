using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

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
            type = PrimitiveTypeInfo.Number;
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
                ConvertTypeNodeToTypeInfo(array.ElementType)),
            AppClassTypeNode appClass => new AppClassTypeInfo(
                string.Join(":", appClass.PackagePath.Append(appClass.ClassName))),
            _ => UnknownTypeInfo.Instance
        };
    }

    /// <summary>
    /// Convert Functions.TypeWithDimensionality to Types.TypeInfo
    /// </summary>
    private TypeInfo ConvertTypeWithDimensionalityToTypeInfo(TypeWithDimensionality twd)
    {
        TypeInfo baseType = twd.IsAppClass
            ? new AppClassTypeInfo(twd.AppClassPath!)
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

            var elementTypeName = parts.Length > 0 ? parts[^1].Trim() : "any";
            var elementType = ConvertTypeNameToTypeInfo(elementTypeName);

            return new ArrayTypeInfo(dimensions, elementType);
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
            return new AppClassTypeInfo(typeName);
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
                if (prop.HasValue)
                {
                    return ConvertPropertyInfoToTypeInfo(prop.Value);
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
                if (property.HasValue)
                    return ConvertPropertyInfoToTypeInfo(property.Value);
            }
        }

        return UnknownTypeInfo.Instance;
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

            // Move to the base class
            if (metadata == null || string.IsNullOrEmpty(metadata.BaseClassName))
            {
                break; // No more base classes
            }

            currentClassName = metadata.BaseClassName;

            // Circular inheritance detection
            if (!visited.Add(currentClassName))
            {
                break; // Detected circular inheritance
            }
        }

        return null; // Method not found in the hierarchy
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

            // Move to the base class
            if (metadata == null || string.IsNullOrEmpty(metadata.BaseClassName))
            {
                break; // No more base classes
            }

            currentClassName = metadata.BaseClassName;

            // Circular inheritance detection
            if (!visited.Add(currentClassName))
            {
                break; // Detected circular inheritance
            }
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

    #region Expression Visitors

    

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

        // Special handling for %This - resolve to current class type
        if (node.Name.Equals("%This", StringComparison.OrdinalIgnoreCase))
        {
            // %This refers to the current instance of the class being analyzed
            if (_programMetadata.Kind == ProgramKind.AppClass || _programMetadata.Kind == ProgramKind.Interface)
            {
                var thisType = new AppClassTypeInfo(_programMetadata.QualifiedName);
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
                var superType = new AppClassTypeInfo(_programMetadata.BaseClassName);
                SetInferredType(node, superType);
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
                var inferredType = ConvertTypeNameToTypeInfo(variable.Type);
                SetInferredType(node, inferredType);
            }
            else
            {

                // Not a declared variable, check if it's a system variable.
                // System variables (like %UserId) are not prefixed with '&'.
                var systemVar = PeopleCodeTypeDatabase.GetSystemVariable(node.Name);
                if (systemVar.HasValue)
                {
                    SetInferredType(node, ConvertPropertyInfoToTypeInfo(systemVar.Value));
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
        else if (node.Operator == BinaryOperator.Concatenate)
        {
            if (!CanConcatenate(leftType) || !CanConcatenate(rightType))
            {
                var invalid = new InvalidTypeInfo(
                    $"Cannot concatenate types '{leftType}' and '{rightType}' - concatenation requires string types");
                node.SetTypeError(new TypeError(invalid.Reason, node));
                SetInferredType(node, UnknownTypeInfo.Instance);
                return;
            }
        }

        // Existing type inference logic
        TypeInfo resultType = node.Operator switch
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

            // Arithmetic operators - apply type promotion
            BinaryOperator.Add or BinaryOperator.Subtract or
            BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Power
                => leftType.GetCommonType(rightType),

            _ => leftType.GetCommonType(rightType)
        };

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

        // DETECT REFERENCE PATTERN: Identifier.Member (no & or % prefix)
        if (node.Target is IdentifierNode identifier &&
            !identifier.Name.StartsWith("&") &&
            !identifier.Name.StartsWith("%"))
        {
            // This is a reference pattern
            var leftSide = identifier.Name;
            var rightSide = node.MemberName;

            // Determine reference category
            PeopleCodeType referenceCategory;
            if (ReferenceTypeInfo.IsSpecialReferenceKeyword(leftSide))
            {
                // Special keyword like Record.MY_RECORD, Field.MY_FIELD
                referenceCategory = ReferenceTypeInfo.GetReferenceCategoryType(leftSide);
            }
            else
            {
                // Non-keyword like MY_RECORD.MY_FIELD -> defaults to Field reference
                referenceCategory = PeopleCodeType.Field;
            }

            var fullReference = $"{leftSide}.{rightSide}";
            var refType = new ReferenceTypeInfo(referenceCategory, rightSide, fullReference);
            SetInferredType(node, refType);
            return;
        }

        // NORMAL MEMBER ACCESS (existing logic)
        var objectType = GetInferredType(node.Target);
        if (objectType != null)
        {
            // Resolve member as property (not method call)
            var memberType = ResolveMemberAccessReturnType(objectType, node.MemberName, isMethodCall: false, parameterTypes: null);
            SetInferredType(node, memberType);
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
    /// </summary>
    private bool CanPerformArithmetic(TypeInfo type) =>
        type is PrimitiveTypeInfo { PeopleCodeType: PeopleCodeType.Number or PeopleCodeType.Integer } ||
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
    /// Check if a type can be concatenated (string concatenation)
    /// </summary>
    private bool CanConcatenate(TypeInfo type) =>
        type is PrimitiveTypeInfo { PeopleCodeType: PeopleCodeType.String } ||
        type is AnyTypeInfo ||
        type is UnknownTypeInfo;

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
