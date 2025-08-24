using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Scoped.Utilities;

public static class AstTypeExtractor
{
    public static string GetTypeFromNode(TypeNode? typeNode)
    {
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
        return builtInType.Type.ToKeyword();
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
            MethodCallNode => "any",
            FunctionCallNode => "any",
            PropertyAccessNode => "any",
            ArrayAccessNode =>  "any",
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
        return BuiltInTypeExtensions.TryParseKeyword(typeName) != null;
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
        
        if (BuiltInTypeExtensions.TryParseKeyword(normalized) is BuiltInType builtIn)
        {
            return builtIn.ToKeyword();
        }

        return normalized;
    }
}