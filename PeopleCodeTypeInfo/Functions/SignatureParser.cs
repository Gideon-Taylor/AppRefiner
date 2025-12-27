using PeopleCodeTypeInfo.Types;
using System.Text.RegularExpressions;

namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Parser for single PeopleCode method and property signatures.
/// Provides clean API for parsing individual signature strings into FunctionInfo or PropertyInfo.
/// </summary>
public static class SignatureParser
{
    private static readonly Regex FunctionSignatureRegex = new(
        @"^(?<default>\*default\*\s+)?(?<name>\w+)\s*\((?<params>.*?)\)\s*->\s*(?<return>.*?)$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PropertySignatureRegex = new(
        @"^(?<name>%?\w+)\s*->\s*(?<type>.*?)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parse a function signature string into a FunctionInfo object.
    /// Example: "GetField(@(RECORD), @(FIELD)) -> &field"
    /// </summary>
    /// <param name="signature">The function signature string</param>
    /// <returns>FunctionInfo object representing the parsed function</returns>
    /// <exception cref="ArgumentException">Thrown when signature format is invalid</exception>
    public static FunctionInfo ParseFunctionSignature(string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            throw new ArgumentException("Signature cannot be null or empty", nameof(signature));

        signature = signature.Trim();

        var match = FunctionSignatureRegex.Match(signature);
        if (!match.Success)
            throw new ArgumentException($"Invalid function signature format: {signature}");

        return ParseMethodFromMatch(match);
    }

    /// <summary>
    /// Parse a property signature string into a PropertyInfo object.
    /// Example: "ActiveRowCount -> &number"
    /// </summary>
    /// <param name="signature">The property signature string</param>
    /// <returns>PropertyInfo object representing the parsed property</returns>
    /// <exception cref="ArgumentException">Thrown when signature format is invalid</exception>
    public static PropertyInfo ParsePropertySignature(string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            throw new ArgumentException("Signature cannot be null or empty", nameof(signature));

        signature = signature.Trim();

        var match = PropertySignatureRegex.Match(signature);
        if (!match.Success)
            throw new ArgumentException($"Invalid property signature format: {signature}");

        return ParsePropertyFromMatch(match);
    }

    /// <summary>
    /// Determine if a signature string represents a property (rather than a method).
    /// Uses the presence of parentheses to distinguish: methods have them, properties don't.
    /// </summary>
    /// <param name="signature">The signature string to check</param>
    /// <returns>True if the signature appears to be a property, false if it appears to be a method</returns>
    public static bool IsPropertySignature(string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return false;

        signature = signature.Trim();

        // Property signatures don't have parentheses (except potentially in type names)
        // Check if it matches property pattern and not function pattern
        return PropertySignatureRegex.IsMatch(signature) && !signature.Contains('(');
    }

    private static PropertyInfo ParsePropertyFromMatch(Match match)
    {
        var name = match.Groups["name"].Value;
        var typeStr = match.Groups["type"].Value;

        // Check for optional return suffix (?)
        bool isOptionalReturn = false;
        if (typeStr.EndsWith("?"))
        {
            isOptionalReturn = true;
            typeStr = typeStr.Substring(0, typeStr.Length - 1).Trim();
        }

        PropertyInfo propertyInfo;

        // Handle union property types
        if (typeStr.Contains('|'))
        {
            var unionTypes = ParseUnionReturnType(typeStr);
            propertyInfo = PropertyInfo.CreateUnion(unionTypes);
        }
        else
        {
            var typeWithDim = TypeWithDimensionality.Parse(typeStr);
            propertyInfo = new PropertyInfo(typeWithDim.Type, typeWithDim.ArrayDimensionality, typeWithDim.AppClassPath);
        }

        // Set name and optional return flag
        propertyInfo.Name = name;
        propertyInfo.IsOptionalReturn = isOptionalReturn;

        return propertyInfo;
    }

    private static FunctionInfo ParseMethodFromMatch(Match match)
    {
        var isDefault = !string.IsNullOrEmpty(match.Groups["default"].Value);
        var name = match.Groups["name"].Value;
        var paramsStr = match.Groups["params"].Value;
        var returnStr = match.Groups["return"].Value;

        // Check for optional return suffix (?)
        bool isOptionalReturn = false;
        if (returnStr.EndsWith("?"))
        {
            isOptionalReturn = true;
            returnStr = returnStr.Substring(0, returnStr.Length - 1).Trim();
        }

        // Delegate parameter parsing to ParameterParser
        var parameters = ParameterParser.ParseParameters(paramsStr);
        var builder = new FunctionBuilder(name);

        // Handle union return types
        if (returnStr.Contains('|'))
        {
            var unionTypes = ParseUnionReturnType(returnStr);
            builder.ReturnsUnion(unionTypes);
        }
        else
        {
            var returnTypeWithDim = TypeWithDimensionality.Parse(returnStr);
            builder.Returns(returnTypeWithDim.Type, returnTypeWithDim.ArrayDimensionality);
        }

        if (isDefault)
            builder.AsDefault();

        if (isOptionalReturn)
            builder.WithOptionalReturn();

        foreach (var parameter in parameters)
        {
            builder.AddParameter(parameter);
        }

        return builder.Build();
    }

    /// <summary>
    /// Parse union return types from a string like "Collection|Compound|Primitive"
    /// </summary>
    private static TypeWithDimensionality[] ParseUnionReturnType(string unionReturnStr)
    {
        var typeStrings = unionReturnStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var unionTypes = new List<TypeWithDimensionality>();

        foreach (var typeStr in typeStrings)
        {
            var trimmedType = typeStr.Trim();
            var typeWithDim = TypeWithDimensionality.Parse(trimmedType);
            unionTypes.Add(typeWithDim);
        }

        if (unionTypes.Count == 0)
        {
            throw new ArgumentException($"Invalid union return type format: {unionReturnStr}");
        }

        return unionTypes.ToArray();
    }
}
