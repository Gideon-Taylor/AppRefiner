using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Visitors.Utilities;

public static class AstTypeExtractor
{
    public static string GetTypeFromNode(TypeNode? typeNode)
    {
        // Check if TypeInferenceVisitor already resolved this type
        if (typeNode != null && typeNode.Attributes.TryGetValue(AstNode.ResolvedTypeInfoAttributeKey, out var resolvedTypeInfo))
        {
            if (resolvedTypeInfo is TypeInfo typeInfo)
            {
                return typeInfo.Name; // Return the qualified name
            }
        }

        // Fall back to existing logic
        return typeNode switch
        {
            null => "any",
            BuiltInTypeNode builtIn => GetBuiltInTypeName(builtIn),
            ArrayTypeNode array => GetArrayTypeName(array),
            AppClassTypeNode appClass => appClass.QualifiedName,
            _ => "any"
        };
    }

    private static string GetBuiltInTypeName(BuiltInTypeNode builtInType)
    {
        return builtInType.Type.GetTypeName();
    }

    private static string GetArrayTypeName(ArrayTypeNode arrayType)
    {
        if (arrayType.ElementType == null)
        {
            if (arrayType.Dimensions == 1)
            {
                return "array";
            }

            var untypedResult = "array";
            for (int i = 1; i < arrayType.Dimensions; i++)
            {
                untypedResult = $"array of {untypedResult}";
            }
            return untypedResult;
        }

        var elementTypeName = GetTypeFromNode(arrayType.ElementType);
        if (arrayType.Dimensions == 1)
        {
            return $"array of {elementTypeName}";
        }

        var typedResult = $"array of {elementTypeName}";
        for (int i = 1; i < arrayType.Dimensions; i++)
        {
            typedResult = $"array of {typedResult}";
        }
        return typedResult;
    }

    public static string GetDefaultTypeForExpression(AstNode? expressionNode)
    {
        return expressionNode switch
        {
            LiteralNode literal => GetLiteralType(literal),
            IdentifierNode => "any",
            BinaryOperationNode => "any",
            UnaryOperationNode => "any",
            FunctionCallNode => "any",
            PropertyAccessNode => "any",
            ArrayAccessNode => "any",
            ObjectCreationNode => "any",
            TypeCastNode cast => GetTypeFromNode(cast.TargetType),
            _ => "any"
        };

    }

    private static string GetLiteralType(LiteralNode literal)
    {
        return literal.Value switch
        {
            bool => "boolean",
            int or long => "integer",
            float or double or decimal => "number",
            string => "string",
            null => "any",
            _ => $"UNKNOWN LITERAL TYPE: {literal.Value.GetType().Name} "
        };
    }

    public static bool IsBuiltInType(string typeName)
    {
        try
        {
            BuiltinTypeExtensions.FromString(typeName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsArrayType(string typeName)
    {
        return typeName.StartsWith("array", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("array of", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAppClassType(string typeName)
    {
        return !IsBuiltInType(typeName) &&
               !IsArrayType(typeName) &&
               !string.Equals(typeName, "any", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return "any";

        var normalized = typeName.Trim();

        try
        {
            var peopleCodeType = BuiltinTypeExtensions.FromString(normalized);
            return peopleCodeType.GetTypeName();
        }
        catch
        {
            // Not a builtin type, continue with app class handling
        }

        return normalized;
    }
}