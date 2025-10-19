using System;
using System.Collections.Generic;
using System.Linq;
using PeopleCodeParser.SelfHosted.Extensions;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Simplified AST visitor that performs basic type inference
/// This is a foundational implementation that can be expanded over time
/// </summary>
public class SimpleTypeInferenceVisitor : ScopedAstVisitor<TypeInfo>
{
    private readonly TypeInferenceContext _context;
    private const string METHOD_INFO_CONTEXT_KEY = "TypeSystem.MethodInfo";
    private readonly Dictionary<Guid, Dictionary<string, TypeInfo>> _scopeLocalTypes = new();

    public SimpleTypeInferenceVisitor(TypeInferenceContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Program Structure

    public override void VisitProgram(ProgramNode program)
    {
        _context.EnterResolution("Program");
        _context.Stats.IncrementNodesAnalyzed();

        try
        {
            // Process imports
            foreach (var import in program.Imports)
            {
                import.Accept(this);
            }

            // Process class/interface definitions
            if (program.AppClass != null)
            {
                program.AppClass.Accept(this);
            }

            if (program.Interface != null)
            {
                program.Interface.Accept(this);
            }

            // Process global and component variables
            foreach (var variable in program.ComponentAndGlobalVariables)
            {
                variable.Accept(this);
            }

            foreach (var constant in program.Constants)
            {
                constant.Accept(this);
            }

            // Process functions
            foreach (var function in program.Functions)
            {
                function.Accept(this);
            }

            // Process main block
            if (program.MainBlock != null)
            {
                program.MainBlock.Accept(this);
            }
        }
        finally
        {
            _context.ExitResolution();
        }
    }

    #endregion

    #region Basic Expressions

    public override void VisitLiteral(LiteralNode literal)
    {
        _context.Stats.IncrementNodesAnalyzed();

        TypeInfo literalType = literal.LiteralType switch
        {
            LiteralType.String => PrimitiveTypeInfo.String,
            LiteralType.Integer => PrimitiveTypeInfo.Integer,
            LiteralType.Decimal => PrimitiveTypeInfo.Number,
            LiteralType.Boolean => PrimitiveTypeInfo.Boolean,
            LiteralType.Null => AnyTypeInfo.Instance, // Null can be any type
            _ => AnyTypeInfo.Instance
        };

        _context.RecordTypeInference(literal, literalType);
    }

    public override void VisitIdentifier(IdentifierNode identifier)
    {
        _context.Stats.IncrementNodesAnalyzed();

        var identifierType = ResolveIdentifierType(identifier) ?? AnyTypeInfo.Instance;

        _context.RecordTypeInference(identifier, identifierType);
    }

    public override void VisitBinaryOperation(BinaryOperationNode binaryOp)
    {
        _context.Stats.IncrementNodesAnalyzed();

        // Visit operands
        binaryOp.Left.Accept(this);
        binaryOp.Right.Accept(this);

        // Simple type inference for binary operations
        var leftType = binaryOp.Left.GetInferredType();
        var rightType = binaryOp.Right.GetInferredType();

        TypeInfo resultType;

        // Comparison operations return boolean
        if (IsComparisonOperator(binaryOp.Operator))
        {
            resultType = PrimitiveTypeInfo.Boolean;
        }
        // Arithmetic operations
        else if (IsArithmeticOperator(binaryOp.Operator))
        {
            resultType = DetermineArithmeticResultType(leftType, rightType);
        }
        // String concatenation
        else if (binaryOp.Operator == BinaryOperator.Add && (IsStringType(leftType) || IsStringType(rightType)))
        {
            resultType = PrimitiveTypeInfo.String;
        }
        else
        {
            // Default case - return Any
            resultType = AnyTypeInfo.Instance;
        }

        _context.RecordTypeInference(binaryOp, resultType);
    }

    public override void VisitFunctionCall(FunctionCallNode functionCall)
    {
        _context.Stats.IncrementNodesAnalyzed();

        // Visit the function expression to determine callable type information
        functionCall.Function.Accept(this);

        // Visit arguments
        foreach (var argument in functionCall.Arguments)
        {
            argument.Accept(this);
        }

        // Determine return type
        var returnType = DetermineFunctionReturnType(functionCall);
        _context.RecordTypeInference(functionCall, returnType);
    }

    public override void VisitAssignment(AssignmentNode assignment)
    {
        _context.Stats.IncrementNodesAnalyzed();

        // Visit the value being assigned first
        assignment.Value.Accept(this);

        // Visit the target of assignment
        assignment.Target.Accept(this);

        // Check if the value can be assigned
        if (!assignment.Value.IsAssignable())
        {
            var errorReason = assignment.Value.GetAssignmentErrorReason();
            var assignedType = assignment.Value.GetInferredType();
            var errorKind = assignedType?.Kind == TypeKind.Void ? TypeErrorKind.VoidAssignment : TypeErrorKind.TypeMismatch;

            _context.ReportError(
                $"Cannot assign value: {errorReason}",
                assignment,
                errorKind,
                expectedType: AnyTypeInfo.Instance,
                actualType: assignedType);
        }

        // The assignment itself returns the type of the assigned value
        // (unless it's void or unknown, in which case the assignment is problematic)
        var valueType = assignment.Value.GetInferredType();
        var targetType = assignment.Target.GetInferredType();
        if (targetType != null && valueType != null &&
            !targetType.IsAssignableFrom(valueType) &&
            !ShouldIgnoreTypeMismatch(targetType, valueType))
        {
            var targetDescription = assignment.Target switch
            {
                MemberAccessNode member => $"property '{member.MemberName}'",
                IdentifierNode identifier => $"identifier '{identifier.Name}'",
                _ => "target"
            };

            _context.ReportError(
                $"Cannot assign value of type '{valueType.Name}' to {targetDescription} of type '{targetType.Name}'",
                assignment,
                TypeErrorKind.TypeMismatch,
                targetType,
                valueType);
        }
        var assignmentType = assignment.Value.IsAssignable() ? (valueType ?? AnyTypeInfo.Instance) : UnknownTypeInfo.Instance;
        _context.RecordTypeInference(assignment, assignmentType);
    }

    public override void VisitMemberAccess(MemberAccessNode memberAccess)
    {
        _context.Stats.IncrementNodesAnalyzed();

        base.VisitMemberAccess(memberAccess);

        var resolvedType = ResolveMemberAccessType(memberAccess);
        if (resolvedType != null)
        {
            _context.RecordTypeInference(memberAccess, resolvedType);
        }
    }

    public override void VisitUnaryOperation(UnaryOperationNode node)
    {
        _context.Stats.IncrementNodesAnalyzed();

        base.VisitUnaryOperation(node);

        var resolvedType = ResolveUnaryOperationType(node);
        if (resolvedType != null)
        {
            _context.RecordTypeInference(node, resolvedType);
        }
    }

    public override void VisitParenthesized(ParenthesizedExpressionNode node)
    {
        _context.Stats.IncrementNodesAnalyzed();

        node.Expression.Accept(this);

        var innerType = node.Expression.GetInferredType();
        if (innerType != null)
        {
            _context.RecordTypeInference(node, innerType);
        }
    }

    public override void VisitObjectCreation(ObjectCreationNode node)
    {
        _context.Stats.IncrementNodesAnalyzed();

        var createdType = ConvertTypeNodeToTypeInfo(node.Type) ?? AnyTypeInfo.Instance;
        if (createdType is AppClassTypeInfo appClass)
        {
            var classInfo = ResolveClassInfo(appClass.QualifiedName);
            var constructor = classInfo?.Constructor;

            for (int i = 0; i < node.Arguments.Count; i++)
            {
                var argument = node.Arguments[i];
                argument.Accept(this);

                if (constructor == null)
                {
                    continue;
                }

                if (i >= constructor.ParameterTypes.Count)
                {
                    _context.ReportError(
                        $"Constructor for '{appClass.QualifiedName}' accepts at most {constructor.ParameterTypes.Count} argument(s) but {node.Arguments.Count} were provided.",
                        argument,
                        TypeErrorKind.ArgumentCountMismatch);
                    continue;
                }

                var expectedType = constructor.ParameterTypes.Count > i
                    ? constructor.ParameterTypes[i]
                    : AnyTypeInfo.Instance;
                ValidateArgumentType(argument, expectedType, appClass.QualifiedName + " constructor");
            }

            if (constructor != null && node.Arguments.Count < constructor.ParameterTypes.Count)
            {
                _context.ReportError(
                    $"Constructor for '{appClass.QualifiedName}' expects {constructor.ParameterTypes.Count} argument(s) but {node.Arguments.Count} were provided.",
                    node,
                    TypeErrorKind.ArgumentCountMismatch);
            }
        }
        else
        {
            foreach (var argument in node.Arguments)
            {
                argument.Accept(this);
            }
        }

        _context.RecordTypeInference(node, createdType);
    }

    public override void VisitTypeCast(TypeCastNode node)
    {
        _context.Stats.IncrementNodesAnalyzed();

        node.Expression.Accept(this);

        var targetType = ConvertTypeNodeToTypeInfo(node.TargetType) ?? AnyTypeInfo.Instance;
        if (targetType is AppClassTypeInfo appClass)
        {
            ResolveClassInfo(appClass.QualifiedName);
        }

        _context.RecordTypeInference(node, targetType);
    }

    #endregion

    #region Statements

    public override void VisitBlock(BlockNode block)
    {
        _context.Stats.IncrementNodesAnalyzed();

        foreach (var statement in block.Statements)
        {
            statement.Accept(this);
        }
    }

    public override void VisitIf(IfStatementNode ifStatement)
    {
        _context.Stats.IncrementNodesAnalyzed();

        // Visit condition
        ifStatement.Condition.Accept(this);

        // Visit branches
        ifStatement.ThenBlock.Accept(this);

        if (ifStatement.ElseBlock != null)
        {
            ifStatement.ElseBlock.Accept(this);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Determines function return type using builtin registry
    /// </summary>
    private TypeInfo DetermineFunctionReturnType(FunctionCallNode functionCall)
    {
        // Function return type resolution will be handled by external function resolution system
        // For now, return Unknown to indicate the type needs to be resolved externally
        return UnknownTypeInfo.Instance;
    }

    /// <summary>
    /// Extracts function name from a function call node
    /// This handles the actual structure of FunctionCallNode
    /// </summary>
    private string ExtractFunctionName(FunctionCallNode functionCall)
    {
        return "";
    }

    /// <summary>
    /// Determines result type for arithmetic operations
    /// </summary>
    private TypeInfo DetermineArithmeticResultType(TypeInfo? left, TypeInfo? right)
    {
        if (left == null || right == null)
        {
            return AnyTypeInfo.Instance;
        }

        // If either is number, result is number
        if (left.Name == "number" || right.Name == "number")
        {
            return PrimitiveTypeInfo.Number;
        }

        // If both are integer, result is integer
        if (left.Name == "integer" && right.Name == "integer")
        {
            return PrimitiveTypeInfo.Integer;
        }

        // Default to number for arithmetic
        return PrimitiveTypeInfo.Number;
    }

    /// <summary>
    /// Checks if an operator is a comparison operator
    /// </summary>
    private bool IsComparisonOperator(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Equal or
            BinaryOperator.NotEqual or
            BinaryOperator.LessThan or
            BinaryOperator.LessThanOrEqual or
            BinaryOperator.GreaterThan or
            BinaryOperator.GreaterThanOrEqual => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if an operator is an arithmetic operator
    /// </summary>
    private bool IsArithmeticOperator(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add or
            BinaryOperator.Subtract or
            BinaryOperator.Multiply or
            BinaryOperator.Divide or
            BinaryOperator.Power => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a type is a string type
    /// </summary>
    private bool IsStringType(TypeInfo? type)
    {
        return type?.Name == "string";
    }

    private TypeInfo? ResolveMemberAccessType(MemberAccessNode node)
    {
        if (node.Target is IdentifierNode)
        {
            var referenceType = ReferenceTypeInfo.FromMemberAccess(node);
            if (referenceType != null)
            {
                return referenceType;
            }
        }

        var targetType = node.Target.GetInferredType();
        if (targetType == null)
        {
            return null;
        }

        return targetType switch
        {
            AppClassTypeInfo appClassType => ResolveAppClassMemberAccess(node, appClassType),
            _ => null
        };
    }

    private TypeInfo? ResolveAppClassMemberAccess(MemberAccessNode node, AppClassTypeInfo appClassType)
    {
        var classInfo = ResolveClassInfo(appClassType.QualifiedName);
        if (classInfo?.IsInterface == true)
        {
            return ResolveInterfaceMemberAccess(node, classInfo);
        }

        var currentClassType = GetCurrentAppClassType() as AppClassTypeInfo;
        var isCurrentClass = currentClassType != null &&
                             string.Equals(appClassType.QualifiedName, currentClassType.QualifiedName, StringComparison.OrdinalIgnoreCase);

        var targetIdentifier = node.Target as IdentifierNode;
        var isSuperAccess = targetIdentifier != null && targetIdentifier.Name.Equals("%Super", StringComparison.OrdinalIgnoreCase);

        var includePrivate = isCurrentClass && !isSuperAccess;
        var includeProtected = isCurrentClass || isSuperAccess;

        var propertyInfo = FindPropertyInfo(appClassType, node.MemberName, includeProtected, includePrivate);
        if (propertyInfo != null)
        {
            return propertyInfo.Type;
        }

        var methodInfo = FindMethodInfo(appClassType, node.MemberName, includeProtected, includePrivate);
        if (methodInfo != null)
        {
            node.SetResolutionContextValue(METHOD_INFO_CONTEXT_KEY, methodInfo);
            return methodInfo.ReturnType;
        }

        if (classInfo != null)
        {
            return ResolveInterfaceMemberAccess(node, classInfo);
        }

        return null;
    }

    private TypeInfo? ResolveInterfaceMemberAccess(MemberAccessNode node, ClassTypeInfo interfaceInfo)
    {
        if (!interfaceInfo.IsInterface)
        {
            return null;
        }

        var propertyInfo = FindInterfacePropertyInfo(interfaceInfo.QualifiedName, node.MemberName);
        if (propertyInfo != null)
        {
            return propertyInfo.Type;
        }

        var methodInfo = FindInterfaceMethodInfo(interfaceInfo.QualifiedName, node.MemberName);
        if (methodInfo != null)
        {
            node.SetResolutionContextValue(METHOD_INFO_CONTEXT_KEY, methodInfo);
            return methodInfo.ReturnType;
        }

        return null;
    }

    private TypeInfo? ResolveUnaryOperationType(UnaryOperationNode node)
    {
        if (node.Operator == UnaryOperator.Reference)
        {
            return ReferenceTypeInfo.FromUnaryOperation(node);
        }

        return null;
    }

    private ClassTypeInfo? ResolveClassInfo(string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            return null;
        }

        var classInfo = _context.ResolveClassInfoAsync(qualifiedName).GetAwaiter().GetResult();

        if (!string.IsNullOrWhiteSpace(classInfo?.BaseClassName))
        {
            ResolveClassInfo(classInfo!.BaseClassName!);
        }

        return classInfo;
    }

    private ClassPropertyInfo? FindPropertyInfo(AppClassTypeInfo classType, string propertyName, bool includeProtected, bool includePrivate)
    {
        var visitedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedInterfaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return FindPropertyInfoRecursive(
            classType.QualifiedName,
            propertyName,
            includeProtected,
            includePrivate,
            visitedClasses,
            visitedInterfaces);
    }

    private ClassPropertyInfo? FindPropertyInfoRecursive(
        string className,
        string propertyName,
        bool includeProtected,
        bool includePrivate,
        HashSet<string> visitedClasses,
        HashSet<string> visitedInterfaces)
    {
        if (!visitedClasses.Add(className))
        {
            return null;
        }

        var classInfo = ResolveClassInfo(className);
        if (classInfo == null)
        {
            return null;
        }

        if (classInfo.Properties.TryGetValue(propertyName, out var propertyInfo) &&
            IsAccessible(propertyInfo.Accessibility, includeProtected, includePrivate))
        {
            return propertyInfo;
        }

        foreach (var interfaceName in classInfo.ImplementedInterfaces)
        {
            var interfaceProperty = FindInterfacePropertyInfoRecursive(interfaceName, propertyName, visitedInterfaces);
            if (interfaceProperty != null)
            {
                return interfaceProperty;
            }
        }

        if (!string.IsNullOrWhiteSpace(classInfo.BaseClassName))
        {
            return FindPropertyInfoRecursive(classInfo.BaseClassName!, propertyName, includeProtected, false, visitedClasses, visitedInterfaces);
        }

        return null;
    }

    private ClassMethodInfo? FindMethodInfo(AppClassTypeInfo classType, string methodName, bool includeProtected, bool includePrivate)
    {
        var visitedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedInterfaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return FindMethodInfoRecursive(
            classType.QualifiedName,
            methodName,
            includeProtected,
            includePrivate,
            visitedClasses,
            visitedInterfaces);
    }

    private ClassMethodInfo? FindMethodInfoRecursive(
        string className,
        string methodName,
        bool includeProtected,
        bool includePrivate,
        HashSet<string> visitedClasses,
        HashSet<string> visitedInterfaces)
    {
        if (!visitedClasses.Add(className))
        {
            return null;
        }

        var classInfo = ResolveClassInfo(className);
        if (classInfo == null)
        {
            return null;
        }

        if (classInfo.Methods.TryGetValue(methodName, out var methodInfo) &&
            IsAccessible(methodInfo.Accessibility, includeProtected, includePrivate))
        {
            return methodInfo;
        }

        foreach (var interfaceName in classInfo.ImplementedInterfaces)
        {
            var interfaceMethod = FindInterfaceMethodInfoRecursive(interfaceName, methodName, visitedInterfaces);
            if (interfaceMethod != null)
            {
                return interfaceMethod;
            }
        }

        if (!string.IsNullOrWhiteSpace(classInfo.BaseClassName))
        {
            return FindMethodInfoRecursive(classInfo.BaseClassName!, methodName, includeProtected, false, visitedClasses, visitedInterfaces);
        }

        return null;
    }

    private ClassPropertyInfo? FindInterfacePropertyInfo(string interfaceName, string propertyName)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return FindInterfacePropertyInfoRecursive(interfaceName, propertyName, visited);
    }

    private ClassPropertyInfo? FindInterfacePropertyInfoRecursive(string interfaceName, string propertyName, HashSet<string> visited)
    {
        if (string.IsNullOrWhiteSpace(interfaceName) || !visited.Add(interfaceName))
        {
            return null;
        }

        var interfaceInfo = ResolveClassInfo(interfaceName);
        if (interfaceInfo == null)
        {
            return null;
        }

        if (interfaceInfo.Properties.TryGetValue(propertyName, out var propertyInfo))
        {
            return propertyInfo;
        }

        foreach (var baseInterface in interfaceInfo.ImplementedInterfaces)
        {
            var inherited = FindInterfacePropertyInfoRecursive(baseInterface, propertyName, visited);
            if (inherited != null)
            {
                return inherited;
            }
        }

        return null;
    }

    private ClassMethodInfo? FindInterfaceMethodInfo(string interfaceName, string methodName)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return FindInterfaceMethodInfoRecursive(interfaceName, methodName, visited);
    }

    private ClassMethodInfo? FindInterfaceMethodInfoRecursive(string interfaceName, string methodName, HashSet<string> visited)
    {
        if (string.IsNullOrWhiteSpace(interfaceName) || !visited.Add(interfaceName))
        {
            return null;
        }

        var interfaceInfo = ResolveClassInfo(interfaceName);
        if (interfaceInfo == null)
        {
            return null;
        }

        if (interfaceInfo.Methods.TryGetValue(methodName, out var methodInfo))
        {
            return methodInfo;
        }

        foreach (var baseInterface in interfaceInfo.ImplementedInterfaces)
        {
            var inherited = FindInterfaceMethodInfoRecursive(baseInterface, methodName, visited);
            if (inherited != null)
            {
                return inherited;
            }
        }

        return null;
    }

    private static bool IsAccessible(MemberAccessibility accessibility, bool includeProtected, bool includePrivate)
    {
        return accessibility switch
        {
            MemberAccessibility.Public => true,
            MemberAccessibility.Protected => includeProtected,
            MemberAccessibility.Private => includePrivate,
            _ => false
        };
    }

    private TypeInfo? ResolveIdentifierType(IdentifierNode identifier)
    {
        switch (identifier.IdentifierType)
        {
            case IdentifierType.SystemVariable:
            case IdentifierType.SystemConstant:
                {
                    // System variable/constant type resolution will be handled by external system
                    // For now, return Unknown to indicate the type needs to be resolved externally
                    return UnknownTypeInfo.Instance;
                }
            case IdentifierType.Super:
                return ResolveSuperIdentifierType(identifier);
            case IdentifierType.UserVariable:
            case IdentifierType.Generic:
                {
                    var scope = GetCurrentScope();
                    var variableType = _context.GetVariableType(identifier.Name, scope);
                    if (variableType != null)
                    {
                        return variableType;
                    }

                    var trackedType = GetTrackedLocalVariableType(scope, identifier.Name);
                    if (trackedType != null)
                    {
                        return trackedType;
                    }

                    var variableInfo = VariableRegistry.FindVariableInScope(identifier.Name, scope);
                    if (variableInfo == null && identifier.Name.StartsWith("&", StringComparison.Ordinal))
                    {
                        variableInfo = VariableRegistry.FindVariableInScope(identifier.Name.TrimStart('&'), scope);
                    }

                    if (variableInfo != null)
                    {
                        var resolvedType = PeopleCodeTypeRegistry.GetTypeByName(variableInfo.Type, variableInfo.Type.Contains(':') ? variableInfo.Type : null)
                            ?? AnyTypeInfo.Instance;
                        return resolvedType;
                    }

                    var declaredType = FindDeclaredVariableType(identifier.Name);
                    if (declaredType != null)
                    {
                        return declaredType;
                    }

                    var propertyType = ResolvePropertyTypeFromCurrentClass(identifier.Name);
                    if (propertyType != null)
                    {
                        return propertyType;
                    }
                    break;
                }
        }

        var builtinType = PeopleCodeTypeRegistry.GetTypeByName(identifier.Name);
        if (builtinType != null)
        {
            return builtinType;
        }

        return null;
    }

    private TypeInfo? ResolvePropertyTypeFromCurrentClass(string identifierName)
    {
        if (string.IsNullOrWhiteSpace(identifierName))
        {
            return null;
        }

        var currentType = GetCurrentAppClassType() as AppClassTypeInfo;
        if (currentType == null)
        {
            return null;
        }

        var propertyName = identifierName.StartsWith("&", StringComparison.OrdinalIgnoreCase)
            ? identifierName.Substring(1)
            : identifierName;

        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        var propertyInfo = FindPropertyInfo(currentType, propertyName, includeProtected: true, includePrivate: true);
        return propertyInfo?.Type;
    }

    private TypeInfo? ResolveSystemIdentifierType(IdentifierNode identifier)
    {
        if (identifier.Name.Equals("%This", StringComparison.OrdinalIgnoreCase))
        {
            var currentClassType = GetCurrentAppClassType();
            if (currentClassType != null)
            {
                return currentClassType;
            }

            _context.ReportError("%This is only valid inside an application class.", identifier, TypeErrorKind.UnknownType);
            return UnknownTypeInfo.Instance;
        }

        if (identifier.Name.Equals("%Super", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveSuperIdentifierType(identifier);
        }

        // System variable type resolution will be handled by external system
        // For now, return Unknown to indicate the type needs to be resolved externally
        return UnknownTypeInfo.Instance;
    }

    private void RegisterLocalVariableType(ScopeContext scope, string variableName, TypeInfo type)
    {
        if (!_scopeLocalTypes.TryGetValue(scope.Id, out var map))
        {
            map = new Dictionary<string, TypeInfo>(StringComparer.OrdinalIgnoreCase);
            _scopeLocalTypes[scope.Id] = map;
        }

        map[variableName] = type;
        if (variableName.StartsWith("&", StringComparison.Ordinal))
        {
            map[variableName.TrimStart('&')] = type;
        }
    }

    private TypeInfo? GetTrackedLocalVariableType(ScopeContext scope, string variableName)
    {
        var current = scope;
        while (current != null)
        {
            if (_scopeLocalTypes.TryGetValue(current.Id, out var map) && map.TryGetValue(variableName, out var type))
            {
                return type;
            }

            if (variableName.StartsWith("&", StringComparison.Ordinal) &&
                map != null &&
                map.TryGetValue(variableName.TrimStart('&'), out type))
            {
                return type;
            }

            current = current.Parent;
        }

        foreach (var map in _scopeLocalTypes.Values)
        {
            if (map.TryGetValue(variableName, out var type))
            {
                return type;
            }

            if (variableName.StartsWith("&", StringComparison.Ordinal) &&
                map.TryGetValue(variableName.TrimStart('&'), out type))
            {
                return type;
            }
        }

        return null;
    }

    private TypeInfo? FindDeclaredVariableType(string identifierName)
    {
        if (string.IsNullOrWhiteSpace(identifierName))
        {
            return null;
        }

        var trimmed = identifierName.TrimStart('&');

        foreach (var declaration in _context.RootProgram.FindDescendants<LocalVariableDeclarationNode>())
        {
            if (declaration.VariableNames.Any(name => name.Equals(identifierName, StringComparison.OrdinalIgnoreCase) ||
                                                     name.TrimStart('&').Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return ConvertTypeNodeToTypeInfo(declaration.Type) ?? AnyTypeInfo.Instance;
            }
        }

        foreach (var declaration in _context.RootProgram.FindDescendants<LocalVariableDeclarationWithAssignmentNode>())
        {
            if (declaration.VariableName.Equals(identifierName, StringComparison.OrdinalIgnoreCase) ||
                declaration.VariableName.TrimStart('&').Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return ConvertTypeNodeToTypeInfo(declaration.Type) ?? AnyTypeInfo.Instance;
            }
        }

        return null;
    }

    private TypeInfo? ResolveSuperIdentifierType(IdentifierNode identifier)
    {
        var appClass = _context.RootProgram.AppClass;
        if (appClass?.BaseClass == null)
        {
            _context.ReportError("%Super is only valid when the class extends a base application class.", identifier, TypeErrorKind.UnknownType);
            return UnknownTypeInfo.Instance;
        }

        var baseType = ConvertTypeNodeToTypeInfo(appClass.BaseClass);
        if (baseType != null)
        {
            return baseType;
        }

        _context.ReportError("Unable to resolve the base application class referenced by %Super.", identifier, TypeErrorKind.UnresolvableReference);
        return UnknownTypeInfo.Instance;
    }

    private TypeInfo? GetCurrentAppClassType()
    {
        var appClass = _context.RootProgram.AppClass;
        if (appClass == null)
        {
            return null;
        }

        var qualifiedName = appClass.NameToken?.Text;
        if (qualifiedName == null)
        {
            return null;
        }

        return new AppClassTypeInfo(qualifiedName);
    }

    #endregion

    #region Local Variable Declarations

    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        var declaredType = ConvertTypeNodeToTypeInfo(node.Type) ?? AnyTypeInfo.Instance;
        var scope = GetCurrentScope();

        foreach (var variableName in node.VariableNames)
        {
            _context.SetVariableType(variableName, declaredType, scope);
            RegisterLocalVariableType(scope, variableName, declaredType);
        }

        // Let the base class handle scope and variable registration
        base.VisitLocalVariableDeclaration(node);

        // Additional type inference logic can be added here if needed
        // For now, the base class handles everything we need
    }

    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        // Now add our type checking logic
        var declaredType = ConvertTypeNodeToTypeInfo(node.Type) ?? AnyTypeInfo.Instance;
        var scope = GetCurrentScope();
        _context.SetVariableType(node.VariableName, declaredType, scope);
        RegisterLocalVariableType(scope, node.VariableName, declaredType);

        // Let the base class handle scope and variable registration
        base.VisitLocalVariableDeclarationWithAssignment(node);

        // Visit the initial value first to infer its type
        TypeInfo? assignedType = null;
        if (node.InitialValue != null)
        {
            node.InitialValue.Accept(this);
            assignedType = node.InitialValue.GetInferredType();
        }

        // Check type compatibility if we have both types
        if (assignedType != null &&
            !declaredType.IsAssignableFrom(assignedType) &&
            !ShouldIgnoreTypeMismatch(declaredType, assignedType))
        {
            _context.ReportError(
                $"Cannot assign value of type '{assignedType.Name}' to variable '{node.VariableName}' of type '{declaredType.Name}'",
                node,
                TypeErrorKind.TypeMismatch,
                declaredType,
                assignedType);
        }
    }

    /// <summary>
    /// Converts a TypeNode from the AST to a TypeInfo instance
    /// </summary>
    private void ValidateMethodCall(FunctionCallNode functionCall, ClassMethodInfo methodInfo)
    {
        // Method signature validation will be provided by external function resolution system
        // For now, we skip detailed validation since function signatures are handled externally
        // TODO: Integrate with external function call validation system when available
        return;
    }

    private void ValidateArgumentType(ExpressionNode argument, TypeInfo expectedType, string methodName)
    {
        var actualType = argument.GetInferredType();
        if (actualType == null)
        {
            return;
        }

        if (!expectedType.IsAssignableFrom(actualType) &&
            !ShouldIgnoreTypeMismatch(expectedType, actualType))
        {
            _context.ReportError(
                $"Argument type mismatch for method '{methodName}': expected '{expectedType.Name}' but found '{actualType.Name}'.",
                argument,
                TypeErrorKind.TypeMismatch,
                expectedType,
                actualType);
        }
    }

    /// <summary>
    /// Converts a TypeNode from the AST to a TypeInfo instance
    /// </summary>
    private TypeInfo? ConvertTypeNodeToTypeInfo(TypeNode? typeNode)
    {
        if (typeNode == null) return null;

        return typeNode switch
        {
            BuiltInTypeNode builtin => ConvertBuiltinType(builtin),
            ArrayTypeNode array => ConvertArrayType(array),
            AppClassTypeNode appClass => new AppClassTypeInfo(appClass.QualifiedName),
            _ => null
        };
    }

    /// <summary>
    /// Converts a builtin TypeNode to TypeInfo
    /// </summary>
    private TypeInfo ConvertBuiltinType(BuiltInTypeNode builtin)
    {
        // Since AST and type system now use the same PeopleCodeType enum,
        // we can directly use the registry for lookup
        return PeopleCodeTypeRegistry.GetTypeByName(builtin.Type.GetTypeName()) ?? AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Converts an array TypeNode to TypeInfo
    /// </summary>
    private TypeInfo ConvertArrayType(ArrayTypeNode array)
    {
        var elementType = array.ElementType != null ? ConvertTypeNodeToTypeInfo(array.ElementType) : null;
        return new ArrayTypeInfo(array.Dimensions, elementType);
    }

    /// <summary>
    /// Checks if a type mismatch should be ignored due to lenient mode settings.
    /// In lenient/IDE mode, unknown types are treated permissively to avoid false positives
    /// when the type system hasn't fully resolved built-in functions yet.
    /// </summary>
    /// <param name="expectedType">The expected type in the assignment or validation</param>
    /// <param name="actualType">The actual type being assigned or validated</param>
    /// <returns>True if the mismatch should be ignored, false otherwise</returns>
    private bool ShouldIgnoreTypeMismatch(TypeInfo? expectedType, TypeInfo? actualType)
    {
        if (!_context.Options.TreatUnknownAsAny)
        {
            return false;
        }

        return expectedType?.Kind == TypeKind.Unknown ||
               actualType?.Kind == TypeKind.Unknown;
    }

    #endregion
}
