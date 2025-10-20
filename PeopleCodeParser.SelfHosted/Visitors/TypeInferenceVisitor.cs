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

        node.Attributes["TypeInfo"] = type;
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
    /// Resolve return type for a standalone function call
    /// </summary>
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
            TypeMetadata? metadata = null;

            // Special case: if this is the current class (%This), use _programMetadata directly
            if (appClassType.QualifiedName.Equals(_programMetadata.QualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                metadata = _programMetadata;
            }
            else
            {
                // Try cache first
                metadata = _typeCache.Get(appClassType.QualifiedName);

                // If not cached, resolve and cache
                if (metadata == null)
                {
                    metadata = _typeResolver.GetTypeMetadata(appClassType.QualifiedName);
                    if (metadata != null)
                        _typeCache.Set(appClassType.QualifiedName, metadata);
                }
            }

            if (metadata != null)
            {
                if (isMethodCall && metadata.Methods.TryGetValue(memberName, out var method))
                    return ConvertFunctionInfoToTypeInfo(method, null, parameterTypes);

                if (!isMethodCall && metadata.Properties.TryGetValue(memberName, out var metaProp))
                    return ConvertPropertyInfoToTypeInfo(metaProp);
            }
        }

        return UnknownTypeInfo.Instance;
    }

    /// <summary>
    /// Resolve return type for default method call (e.g., &rowset(1))
    /// </summary>
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

    #endregion

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
            UnaryOperator.Reference => operandType,  // & prefix doesn't change type
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

        // Get parameter types from already-visited arguments
        var parameterTypes = new TypeInfo[node.Arguments.Count];
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            parameterTypes[i] = GetInferredType(node.Arguments[i]) ?? UnknownTypeInfo.Instance;
        }

        TypeInfo returnType;

        // Determine the call pattern
        if (node.Function is IdentifierNode identifier)
        {
            // Could be either:
            // 1. Standalone function call: Len(&var), MyFunc(1, 2)
            // 2. Default method call on variable: &rowset(1)

            // Try resolving as a function first
            returnType = ResolveFunctionCallReturnType(identifier.Name, parameterTypes);

            // If not found as a function, check if it's a variable being called as default method
            if (returnType is UnknownTypeInfo)
            {
                var identifierType = GetInferredType(identifier);

                if (identifierType != null && !(identifierType is UnknownTypeInfo))
                {
                    // It's a variable with a known type, treat as default method call
                    returnType = ResolveDefaultMethodCall(identifierType, parameterTypes);
                }
            }
        }
        else if (node.Function is MemberAccessNode memberAccess)
        {
            // Member method call: &obj.Method()
            var objectType = GetInferredType(memberAccess.Target);

            if (objectType != null)
            {
                returnType = ResolveMemberAccessReturnType(objectType, memberAccess.MemberName, isMethodCall: true, parameterTypes);
            }
            else
            {
                returnType = UnknownTypeInfo.Instance;
            }
        }
        else
        {
            // Default method call: GetLevel0()(1)
            var targetType = GetInferredType(node.Function);

            if (targetType != null)
            {
                returnType = ResolveDefaultMethodCall(targetType, parameterTypes);
            }
            else
            {
                returnType = UnknownTypeInfo.Instance;
            }
        }

        SetInferredType(node, returnType);
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
}
