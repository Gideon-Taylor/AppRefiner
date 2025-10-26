
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using PeopleCodeTypeInfo.Validation;
using System.Collections.Generic;
using System.Linq;

namespace PeopleCodeParser.SelfHosted.Visitors
{
    public class TypeError
    {
        public string Message { get; }
        public AstNode Node { get; }

        public TypeError(string message, AstNode node)
        {
            Message = message;
            Node = node;
        }

        public override string ToString() => $"{Message} at {Node.SourceSpan}";
    }

    public class TypeCheckerVisitor : ScopedAstVisitor<object>
    {
        private readonly ITypeMetadataResolver _typeResolver;
        private readonly TypeCache _typeCache;

        private TypeCheckerVisitor(ITypeMetadataResolver typeResolver, TypeCache typeCache)
        {
            _typeResolver = typeResolver;
            _typeCache = typeCache;
        }

        public static TypeCheckerVisitor Run(ProgramNode program, ITypeMetadataResolver typeResolver, TypeCache typeCache)
        {
            var visitor = new TypeCheckerVisitor(typeResolver, typeCache);
            program.Accept(visitor);
            return visitor;
        }

        public override void VisitAssignment(AssignmentNode node)
        {
            base.VisitAssignment(node);

            var leftType = node.Target.GetInferredType();
            var rightType = node.Value.GetInferredType();

            if (leftType != null && rightType != null)
            {
                if (!AreTypesCompatible(leftType, rightType))
                {
                    RecordTypeError($"Cannot assign type '{rightType}' to '{leftType}'", node);
                }
            }
        }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            base.VisitLocalVariableDeclarationWithAssignment(node);

            var leftType = ConvertTypeNodeToTypeInfo(node.Type);
            var rightType = node.InitialValue.GetInferredType();

            if (leftType != null && rightType != null)
            {
                if (!AreTypesCompatible(leftType, rightType))
                {
                    RecordTypeError($"Cannot assign type '{rightType}' to '{leftType}'", node);
                }
            }
        }

        public override void VisitExpressionStatement(ExpressionStatementNode node)
        {
            base.VisitExpressionStatement(node);
            var typeInfo = node.Expression.GetInferredType();
            if (typeInfo == null) { return; }

            if (typeInfo.PeopleCodeType != PeopleCodeType.Void && typeInfo.PeopleCodeType != PeopleCodeType.Unknown)
            {

                if (node.Expression is FunctionCallNode fcn && fcn.GetFunctionInfo() is FunctionInfo fi && fi is not null)
                {
                    RecordTypeError($"Return values must assigned to a variable. Function {fi.Name}() returns type '{fi.ReturnType}.", node);
                }
                else
                {
                    /* This expression statement returns a value, but its ignored */
                    RecordTypeError($"Expression values must be assigned to a variable.", node);
                }
            }

        }
        public override void VisitFunctionCall(FunctionCallNode node)
        {
            base.VisitFunctionCall(node);

            // Get the function info that was stored by TypeInferenceVisitor
            var functionInfo = node.GetFunctionInfo();
            if (functionInfo == null)
            {
                // Can't validate without function metadata
                return;
            }

            // Build arguments with type and variable tracking
            var arguments = new ArgumentInfo[node.Arguments.Count];
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                var argNode = node.Arguments[i];
                var argType = argNode.GetInferredType() ?? UnknownTypeInfo.Instance;
                bool isVariable = IsVariableReference(argNode);
                arguments[i] = new ArgumentInfo(argType, isVariable);
            }

            // Validate the call
            var validator = new FunctionCallValidator(_typeResolver);
            var result = validator.Validate(functionInfo, arguments);

            if (!result.IsValid)
            {
                // Record the error on the specific argument that failed, if available
                AstNode errorNode = node; // Default to the whole function call

                if (result.FailedAtArgumentIndex >= 0 && result.FailedAtArgumentIndex < node.Arguments.Count)
                {
                    // Record error on the specific argument that failed
                    errorNode = node.Arguments[result.FailedAtArgumentIndex];
                }

                RecordTypeError(result.GetDetailedError(), errorNode);
            }
        }

        /// <summary>
        /// Record a type error on the given AST node
        /// </summary>
        private void RecordTypeError(string message, AstNode node)
        {
            node.SetTypeError(new TypeError(message, node));
        }

        private bool AreTypesCompatible(TypeInfo targetType, TypeInfo valueType)
        {
            // Fast path: exact match or Any/Unknown types (always compatible)
            if (targetType.Equals(valueType) || targetType is AnyTypeInfo || valueType is AnyTypeInfo || targetType is UnknownTypeInfo || valueType is UnknownTypeInfo)
            {
                return true;
            }

            // Integer and Number are bidirectionally compatible
            if ((targetType.PeopleCodeType == PeopleCodeType.Integer && valueType.PeopleCodeType == PeopleCodeType.Number) ||
                (targetType.PeopleCodeType == PeopleCodeType.Number && valueType.PeopleCodeType == PeopleCodeType.Integer))
            {
                return true;
            }

            // Use the TypeInfo's IsAssignableFrom method for proper type compatibility
            // This handles Object accepting AppClass, arrays, builtins, etc.
            if (targetType.IsAssignableFrom(valueType))
            {
                return true;
            }

            // AppClass inheritance support: B extends A, so B can be assigned to A
            if (targetType is AppClassTypeInfo targetAppClass && valueType is AppClassTypeInfo valueAppClass)
            {
                return IsAppClassCompatible(targetAppClass, valueAppClass);
            }

            return false;
        }

        /// <summary>
        /// Check if a value of valueType can be assigned to a variable of targetType,
        /// considering the AppClass inheritance hierarchy.
        /// </summary>
        /// <param name="targetType">The target type (e.g., variable type)</param>
        /// <param name="valueType">The value type being assigned</param>
        /// <returns>True if valueType or any of its base classes match targetType</returns>
        private bool IsAppClassCompatible(AppClassTypeInfo targetType, AppClassTypeInfo valueType)
        {
            // Direct match already handled by caller, but check again for safety
            if (valueType.QualifiedName.Equals(targetType.QualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Walk up the inheritance chain of valueType to see if we find targetType
            var currentClassName = valueType.QualifiedName;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentClassName };

            while (!string.IsNullOrEmpty(currentClassName))
            {
                // Try to get metadata for the current class
                var metadata = _typeResolver.GetTypeMetadata(currentClassName);
                if (metadata == null)
                {
                    // Can't resolve metadata, assume incompatible
                    break;
                }

                // Check if this class has a base class
                if (string.IsNullOrEmpty(metadata.BaseClassName))
                {
                    // Reached the top of the hierarchy without finding targetType
                    break;
                }

                // Check if the base class matches our target
                if (metadata.BaseClassName.Equals(targetType.QualifiedName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Move to the base class for next iteration
                currentClassName = metadata.BaseClassName;

                // Circular inheritance detection
                if (!visited.Add(currentClassName))
                {
                    // Detected circular inheritance, bail out
                    break;
                }
            }

            return false;
        }

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
                    string.Join(":", appClass.PackagePath.Concat(new [] { appClass.ClassName }))),
                _ => UnknownTypeInfo.Instance
            };
        }

        /// <summary>
        /// Determines if an expression node represents a variable reference (assignable location).
        /// Returns true for identifiers, field access, and array access - things that can be assigned to.
        /// Returns false for literals, function calls, and other expressions.
        /// </summary>
        private bool IsVariableReference(AstNode node)
        {
            return node switch
            {
                // Simple variable identifier
                IdentifierNode => true,

                // Property access (e.g., obj.Property)
                PropertyAccessNode => true,

                // Member access (e.g., obj.Member)
                MemberAccessNode => true,

                // Array element access (e.g., arr[0])
                ArrayAccessNode => true,

                // Everything else: literals, function calls, operators, etc.
                _ => false
            };
        }
    }
}
