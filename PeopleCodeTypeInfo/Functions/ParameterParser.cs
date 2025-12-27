using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using System.Text;
using System.Text.RegularExpressions;


namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Parser for complex PeopleCode function parameter structures.
/// Handles patterns like:
/// - [@(SCROLL)...{0-2}], (@(FIELD), &string)+
/// - [&string [, &any...]]
/// - &rowset [, @(RECORD), @(RECORD)]
///
/// This class focuses purely on parsing the parameter portion of function signatures.
/// For complete signature parsing, use SignatureParser.
/// </summary>
public static class ParameterParser
{
    /// <summary>
    /// Parse parameters string into list of Parameter objects
    /// </summary>
    public static List<Parameter> ParseParameters(string paramsStr)
    {
        if (string.IsNullOrWhiteSpace(paramsStr))
            return new List<Parameter>();

        var tokens = TokenizeParameters(paramsStr);
        return ParseParameterTokens(tokens);
    }

    /// <summary>
    /// Parse tokenized parameters into Parameter objects
    /// </summary>
    private static List<Parameter> ParseParameterTokens(List<string> tokens)
    {
        var parameters = new List<Parameter>();
        int i = 0;

        while (i < tokens.Count)
        {
            var (parameter, consumed) = ParseSingleParameterToken(tokens, i);
            parameters.Add(parameter);
            i += consumed;
        }

        return parameters;
    }

    /// <summary>
    /// Parse a single parameter token and return the parameter plus number of tokens consumed
    /// </summary>
    private static (Parameter parameter, int tokensConsumed) ParseSingleParameterToken(List<string> tokens, int startIndex)
    {
        var token = tokens[startIndex];

        // First check if this is a parameter group (could be named or unnamed)
        // Extract potential name to check the remaining token for group syntax
        var (potentialName, remainingToken) = ExtractParameterName(token);

        // Handle parameter groups: (...) or name: (...) with optional repetition
        if (token.StartsWith('(') || remainingToken.StartsWith('('))
        {
            var (group, repetition) = ParseParameterGroup(token);

            if (repetition != null)
            {
                return (new VariableParameter(group, repetition.Value.min, repetition.Value.max), 1);
            }
            else
            {
                return (group, 1);
            }
        }

        // Handle variable parameters with constraints: type{n-m}, type+, type*, etc.
        var (baseParam, varConstraints) = ParseVariableConstraints(token);

        if (varConstraints != null)
        {
            // Extract name for the variable parameter
            var (name, _) = ExtractParameterName(token);
            return (new VariableParameter(baseParam, varConstraints.Value.min, varConstraints.Value.max, name), 1);
        }

        // Handle union types: type1|type2|type3
        if (token.Contains('|'))
        {
            return (ParseUnionParameter(token), 1);
        }

        // Handle simple parameter
        return (ParseSimpleParameter(token), 1);
    }

    /// <summary>
    /// Parse a parameter group with optional repetition and naming
    /// </summary>
    private static (ParameterGroup group, (int min, int max)? repetition) ParseParameterGroup(string groupToken)
    {
        // Extract name first (name: (...) syntax)
        var (name, tokenWithoutName) = ExtractParameterName(groupToken);

        // Extract repetition suffix if present
        var (content, repetitionStr) = ExtractRepetitionSuffix(tokenWithoutName);

        // Remove outer parentheses
        if (content.StartsWith('(') && content.EndsWith(')'))
            content = content.Substring(1, content.Length - 2);

        var innerTokens = TokenizeParameters(content);
        var innerParameters = ParseParameterTokens(innerTokens);

        var group = new ParameterGroup(innerParameters, name);

        var repetition = ParseRepetitionString(repetitionStr);
        return (group, repetition);
    }

    /// <summary>
    /// Parse variable constraints from a parameter token
    /// </summary>
    private static (Parameter baseParam, (int min, int max)? constraints) ParseVariableConstraints(string token)
    {
        var (name, tokenWithoutName) = ExtractParameterName(token);
        var (baseToken, repetitionStr) = ExtractRepetitionSuffix(tokenWithoutName);

        var baseParam = baseToken.Contains('|') ? ParseUnionParameter(baseToken) : ParseSimpleParameter(baseToken);
        var constraints = ParseRepetitionString(repetitionStr);

        // Apply the name to the variable parameter wrapper, not the inner parameter
        if (!string.IsNullOrEmpty(name))
        {
            // Clear the inner parameter name since the name applies to the variable parameter
            SetParameterName(baseParam, "");
        }

        return (baseParam, constraints);
    }

    /// <summary>
    /// Parse a union parameter (name: type1|type2|... or type1|type2|...)
    /// </summary>
    private static UnionParameter ParseUnionParameter(string unionToken)
    {
        var (name, typeStr) = ExtractParameterName(unionToken);
        var types = new List<TypeWithDimensionality>();
        var parts = typeStr.Split('|');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                types.Add(TypeWithDimensionality.Parse(trimmed));
            }
        }

        return new UnionParameter(types, name);
    }

    /// <summary>
    /// Parse a simple parameter, handling optional name: syntax
    /// </summary>
    private static Parameter ParseSimpleParameter(string token)
    {
        var (name, typeStr) = ExtractParameterName(token);
        // If this is a reference token (starts with '@'), use ReferenceParameter
        var trimmed = typeStr.Trim();
        if (trimmed.StartsWith("@"))
        {
            var refStr = trimmed.Substring(1).Trim();
            // Resolve the reference category using builtin type parser
            var cat = BuiltinTypeExtensions.FromString(refStr);
            var rp = new ReferenceParameter(cat);
            // Attach name if provided
            if (!string.IsNullOrEmpty(name)) rp.Name = name;
            return rp;
        }

        var typeWithDim = TypeWithDimensionality.Parse(typeStr);
        return new SingleParameter(typeWithDim, name);
    }

    /// <summary>
    /// Extract repetition suffix from a token (e.g., "type+" -> ("type", "+"), "type{0-2}" -> ("type", "{0-2}"))
    /// </summary>
    private static (string baseToken, string repetitionSuffix) ExtractRepetitionSuffix(string token)
    {
        // Look for repetition patterns at the end
        var patterns = new[] { "+", "*", "?" };

        foreach (var pattern in patterns)
        {
            if (token.EndsWith(pattern))
            {
                return (token.Substring(0, token.Length - pattern.Length), pattern);
            }
        }

        // Look for brace patterns: {n}, {n-}, {n-m}
        var braceMatch = Regex.Match(token, @"^(.*?)\{([^}]+)\}$");
        if (braceMatch.Success)
        {
            return (braceMatch.Groups[1].Value, "{" + braceMatch.Groups[2].Value + "}");
        }

        return (token, "");
    }

    /// <summary>
    /// Parse repetition string into min/max constraints
    /// </summary>
    private static (int min, int max)? ParseRepetitionString(string repetitionStr)
    {
        if (string.IsNullOrEmpty(repetitionStr))
            return null;

        return repetitionStr switch
        {
            "+" => (1, int.MaxValue),
            "*" => (0, int.MaxValue),
            "?" => (0, 1),
            _ when repetitionStr.StartsWith('{') && repetitionStr.EndsWith('}') => ParseBraceRepetition(repetitionStr),
            _ => null
        };
    }

    /// <summary>
    /// Parse brace repetition like {n}, {n-}, {n-m}
    /// </summary>
    private static (int min, int max) ParseBraceRepetition(string braceRepetition)
    {
        var content = braceRepetition.Substring(1, braceRepetition.Length - 2);

        if (content.EndsWith('-'))
        {
            // {n-} pattern: n or more
            var n = int.Parse(content.Substring(0, content.Length - 1));
            return (n, int.MaxValue);
        }
        else if (content.Contains('-'))
        {
            // {n-m} pattern: n to m range
            var parts = content.Split('-');
            var min = int.Parse(parts[0]);
            var max = int.Parse(parts[1]);
            return (min, max);
        }
        else
        {
            // {n} pattern: exactly n
            var n = int.Parse(content);
            return (n, n);
        }
    }

    /// <summary>
    /// Tokenize parameters string, handling nested parentheses and repetition modifiers
    /// </summary>
    private static List<string> TokenizeParameters(string paramsStr)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        int parenDepth = 0;

        for (int i = 0; i < paramsStr.Length; i++)
        {
            char c = paramsStr[i];

            switch (c)
            {
                case '(':
                    parenDepth++;
                    current.Append(c);
                    break;

                case ')':
                    parenDepth--;
                    current.Append(c);
                    // Check for repetition modifier after closing paren
                    if (parenDepth == 0 && i + 1 < paramsStr.Length)
                    {
                        var remainingStr = paramsStr.Substring(i + 1);
                        var repetitionMatch = Regex.Match(remainingStr, @"^([+*?]|\{[^}]+\})");
                        if (repetitionMatch.Success)
                        {
                            // Include the repetition modifier as part of this token
                            var modifierLength = repetitionMatch.Length;
                            current.Append(remainingStr.Substring(0, modifierLength));
                            i += modifierLength;
                        }
                    }
                    break;

                case ',':
                    if (parenDepth == 0)
                    {
                        AddToken(tokens, current);
                        continue;
                    }
                    current.Append(c);
                    break;

                case ' ':
                case '\t':
                case '\n':
                case '\r':
                    // Preserve internal whitespace within tokens (e.g., space after colon in "Name: Type")
                    // Only skip if we haven't started building a token yet
                    if (parenDepth == 0 && current.Length == 0)
                    {
                        continue;
                    }
                    current.Append(c);
                    break;

                default:
                    current.Append(c);
                    break;
            }
        }

        AddToken(tokens, current);
        return tokens;
    }

    /// <summary>
    /// Add token to list if not empty
    /// </summary>
    private static void AddToken(List<string> tokens, StringBuilder current)
    {
        var token = current.ToString().Trim();
        if (!string.IsNullOrEmpty(token))
        {
            tokens.Add(token);
        }
        current.Clear();
    }

    /// <summary>
    /// Extract parameter name from token using name: syntax
    /// Returns (name, remainingToken) where name is empty if no name found
    ///
    /// Key rule: Space after colon distinguishes named parameters from AppClass paths
    /// - "Name: Type" (with space) → named parameter
    /// - "PACKAGE:Class" (no space) → AppClass path
    /// </summary>
    public static (string name, string remainingToken) ExtractParameterName(string token)
    {
        token = token.Trim();

        // Look for name: pattern at the start
        var colonIndex = token.IndexOf(':');
        if (colonIndex > 0)
        {
            var potentialName = token.Substring(0, colonIndex).Trim();

            // Check if there's a space after the colon (before trimming)
            // This is the key distinction between "Name: Type" and "PACKAGE:Class"
            bool hasSpaceAfterColon = colonIndex + 1 < token.Length &&
                                       char.IsWhiteSpace(token[colonIndex + 1]);

            // Validate that it's a valid identifier and has space after colon
            if (IsValidParameterName(potentialName) && hasSpaceAfterColon)
            {
                var remainingToken = token.Substring(colonIndex + 1).Trim();
                return (potentialName, remainingToken);
            }
        }

        // No valid name found (either no colon, no space after colon, or invalid name)
        return ("", token);
    }

    /// <summary>
    /// Check if a string is a valid parameter name (identifier)
    /// </summary>
    private static bool IsValidParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Must start with letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        // Rest must be letters, digits, or underscores
        for (int i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Check if a token looks like an AppClass path (PACKAGE:Class) rather than a named parameter
    /// </summary>
    private static bool IsAppClassPath(string token)
    {
        // AppClass paths typically have uppercase identifiers and NO type prefixes
        // Examples: "PACKAGE:Class", "PACKAGE:SubPackage:Class"
        // Named parameters look like: "name: &string", "data: &string|&number"
        var colonIndex = token.IndexOf(':');
        if (colonIndex <= 0) return false;

        var beforeColon = token.Substring(0, colonIndex).Trim();
        var afterColon = token.Substring(colonIndex + 1).Trim();

        // AppClass characteristics:
        // 1. Before colon is typically uppercase (PACKAGE)
        // 2. After colon is an identifier (Class), NOT a type with & or @
        // 3. No type prefixes or modifiers after the colon
        return afterColon.Length > 0 &&
               char.IsUpper(beforeColon[0]) &&  // Package names typically start uppercase
               char.IsUpper(afterColon[0]) &&   // Class names typically start uppercase
               !afterColon.StartsWith("@") &&   // Not a reference type
               !afterColon.Contains("|") &&     // Not a union type
               !afterColon.Contains("(");       // Not a group
    }

    /// <summary>
    /// Helper to set parameter name on different parameter types
    /// </summary>
    private static void SetParameterName(Parameter parameter, string name)
    {
        switch (parameter)
        {
            case SingleParameter single:
                single.Name = name;
                break;
            case UnionParameter union:
                union.Name = name;
                break;
            case ParameterGroup group:
                group.Name = name;
                break;
            case VariableParameter variable:
                variable.Name = name;
                break;
        }
    }

    /// <summary>
    /// Create FNV1a hash for function names (case-insensitive)
    /// </summary>
    public static uint FNV1a32Hash(string str)
    {
        uint hash = 0x811c9dc5;

        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            // Convert uppercase ASCII to lowercase
            if (c >= 'A' && c <= 'Z')
                c = (char)(c | 0x20);

            hash ^= (byte)c;
            hash *= 0x01000193;
        }

        return hash;
    }
}
