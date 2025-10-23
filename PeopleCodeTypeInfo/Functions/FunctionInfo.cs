using PeopleCodeTypeInfo.Types;
using System.Text;

namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Function information using the composable parameter system.
/// Supports complex PeopleCode function signatures with hierarchical parameter structures.
/// </summary>
public class FunctionInfo
{
    /// <summary>
    /// Function name
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// List of parameters using the composable parameter system
    /// </summary>
    public List<Parameter> Parameters { get; set; } = new();

    /// <summary>
    /// Return type of the function with dimensionality and AppClass support
    /// </summary>
    public TypeWithDimensionality ReturnType { get; set; } = new(PeopleCodeType.Void);

    /// <summary>
    /// Union return types when the function can return multiple types
    /// When null or empty, use ReturnType. When populated, this takes precedence.
    /// </summary>
    public List<TypeWithDimensionality>? ReturnUnionTypes { get; set; } = null;

    /// <summary>
    /// Whether this is a default method (can be called without the method name)
    /// Indicated by *default* prefix in signatures
    /// </summary>
    public bool IsDefaultMethod { get; set; } = false;

    /// <summary>
    /// Whether this is a property (appears in property section of signatures)
    /// </summary>
    public bool IsProperty { get; set; } = false;

    /// <summary>
    /// Additional metadata or description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Get the return type with dimensionality information
    /// For union types, returns the first type (for backward compatibility)
    /// </summary>
    public TypeWithDimensionality ReturnTypeWithDimensionality =>
        IsUnionReturn ? ReturnUnionTypes![0] : ReturnType;

    /// <summary>
    /// Whether the function returns void
    /// </summary>
    public bool ReturnsVoid => IsUnionReturn ?
        ReturnUnionTypes!.All(t => t.Type == PeopleCodeType.Void) :
        ReturnType.Type == PeopleCodeType.Void;

    /// <summary>
    /// Whether the function returns an array
    /// </summary>
    public bool ReturnsArray => IsUnionReturn ?
        ReturnUnionTypes!.Any(t => t.ArrayDimensionality > 0) :
        ReturnType.ArrayDimensionality > 0;

    /// <summary>
    /// Get minimum number of arguments required
    /// </summary>
    public int MinArgumentCount => Parameters.Sum(p => p.MinArgumentCount);

    /// <summary>
    /// Get maximum number of arguments that can be accepted
    /// </summary>
    public int MaxArgumentCount
    {
        get
        {
            if (Parameters.Any(p => p.MaxArgumentCount == int.MaxValue))
                return int.MaxValue;
            return Parameters.Sum(p => p.MaxArgumentCount);
        }
    }

    /// <summary>
    /// Whether this function can accept a variable number of arguments
    /// </summary>
    public bool HasVarArgs => Parameters.Any(p => p is VariableParameter);

    /// <summary>
    /// Whether this function has optional parameters
    /// </summary>
    public bool HasOptionalParameters => Parameters.Any(p => p.IsOptional);

    /// <summary>
    /// Whether this function has a polymorphic return type that requires context resolution
    /// </summary>
    public bool IsPolymorphicReturn => IsUnionReturn ?
        ReturnUnionTypes!.Any(t => t.Type.IsPolymorphic()) :
        ReturnType.Type.IsPolymorphic();

    /// <summary>
    /// Whether this function has a union return type (multiple possible return types)
    /// </summary>
    public bool IsUnionReturn => ReturnUnionTypes != null && ReturnUnionTypes.Count > 1;

    /// <summary>
    /// Resolve the return type given the context
    /// For union types, returns all possible resolved types
    /// </summary>
    /// <param name="objectType">The type of the object this method is called on</param>
    /// <param name="parameterTypes">The types of parameters passed to the function</param>
    /// <returns>The resolved return type(s)</returns>
    public List<TypeWithDimensionality> ResolveReturnTypes(TypeInfo? objectType = null, TypeInfo[]? parameterTypes = null)
    {
        if (IsUnionReturn)
        {
            var resolvedTypes = new List<TypeWithDimensionality>();

            foreach (var unionType in ReturnUnionTypes!)
            {
                if (unionType.Type.IsPolymorphic())
                {
                    // Resolve polymorphic type in union
                    var polymorphicTypeInfo = TypeInfo.FromPeopleCodeType(unionType.Type) as Types.PolymorphicTypeInfo;
                    if (polymorphicTypeInfo != null)
                    {
                        var resolvedTypeInfo = polymorphicTypeInfo.Resolve(objectType, parameterTypes);
                        resolvedTypes.Add(ConvertTypeInfoToTypeWithDimensionality(resolvedTypeInfo, unionType.ArrayDimensionality));
                    }
                    else
                    {
                        resolvedTypes.Add(new TypeWithDimensionality(Types.PeopleCodeType.Any, unionType.ArrayDimensionality));
                    }
                }
                else
                {
                    // Non-polymorphic type in union
                    resolvedTypes.Add(unionType);
                }
            }

            return resolvedTypes;
        }
        else if (IsPolymorphicReturn)
        {
            // Single polymorphic type
            var polymorphicTypeInfo = TypeInfo.FromPeopleCodeType(ReturnType.Type) as Types.PolymorphicTypeInfo;
            if (polymorphicTypeInfo == null)
            {
                throw new InvalidOperationException($"Return type {ReturnType.Type} is marked as polymorphic but doesn't resolve to a PolymorphicTypeInfo");
            }

            var resolvedTypeInfo = polymorphicTypeInfo.Resolve(objectType, parameterTypes);
            return new List<TypeWithDimensionality> { ConvertTypeInfoToTypeWithDimensionality(resolvedTypeInfo, ReturnType.ArrayDimensionality) };
        }
        else
        {
            // Single non-polymorphic type
            return new List<TypeWithDimensionality> { ReturnTypeWithDimensionality };
        }
    }

    /// <summary>
    /// Resolve the return type given the context (backward compatibility method)
    /// For union types, returns the first resolved type
    /// </summary>
    /// <param name="objectType">The type of the object this method is called on</param>
    /// <param name="parameterTypes">The types of parameters passed to the function</param>
    /// <returns>The resolved return type</returns>
    public TypeWithDimensionality ResolveReturnType(TypeInfo? objectType = null, TypeInfo[]? parameterTypes = null)
    {
        var resolvedTypes = ResolveReturnTypes(objectType, parameterTypes);
        return resolvedTypes.FirstOrDefault();
    }

    /// <summary>
    /// Convert TypeInfo back to TypeWithDimensionality with proper dimensionality handling
    /// </summary>
    private TypeWithDimensionality ConvertTypeInfoToTypeWithDimensionality(TypeInfo resolvedTypeInfo, byte originalDimensionality)
    {
        if (resolvedTypeInfo is ArrayTypeInfo arrayInfo)
        {
            var elementType = arrayInfo.ElementType?.PeopleCodeType ?? Types.PeopleCodeType.Any;
            return new TypeWithDimensionality(elementType, (byte)arrayInfo.Dimensions);
        }
        else if (resolvedTypeInfo.PeopleCodeType.HasValue)
        {
            // Handle regular types - preserve any existing array dimensionality unless the resolution is explicitly an array operation
            var dimensionality = originalDimensionality;

            // Special handling for ElementOfObject which reduces dimensionality
            if (resolvedTypeInfo.PeopleCodeType.Value == Types.PeopleCodeType.ElementOfObject && originalDimensionality > 0)
            {
                dimensionality = (byte)Math.Max(0, originalDimensionality - 1);
            }

            return new TypeWithDimensionality(resolvedTypeInfo.PeopleCodeType.Value, dimensionality);
        }
        else
        {
            // Fallback to Any
            return new TypeWithDimensionality(Types.PeopleCodeType.Any, originalDimensionality);
        }
    }

    /// <summary>
    /// Get a string representation of the function signature
    /// </summary>
    public string GetSignature()
    {
        var parameterStrings = Parameters.Select(p => p.ToString());
        var parametersStr = string.Join(", ", parameterStrings);

        var returnTypeStr = GetReturnTypeString();

        var prefix = IsDefaultMethod ? "*default* " : "";
        return $"{prefix}{Name}({parametersStr}) -> {returnTypeStr}";
    }

    /// <summary>
    /// Get a property signature string
    /// </summary>
    public string GetPropertySignature()
    {
        var returnTypeStr = GetReturnTypeString();

        return $"{Name} -> {returnTypeStr}";
    }

    /// <summary>
    /// Get the return type string representation (handles union types)
    /// </summary>
    public string GetReturnTypeString()
    {
        if (IsUnionReturn)
        {
            var typeStrings = ReturnUnionTypes!.Select(t => t.ToString());
            return string.Join("|", typeStrings);
        }
        else
        {
            return ReturnTypeWithDimensionality.ToString();
        }
    }

    public override string ToString()
    {
        return IsProperty ? GetPropertySignature() : GetSignature();
    }

    public (string, int, int) ToFunctionCallTip(int argIndex)
    {
        StringBuilder sb = new StringBuilder();
        int paramStart = 0;
        int paramEnd = 0;

        sb.Append($"{Name}(");
        for (var x = 0; x < Parameters.Count; x++)
        {
            if (x == argIndex)
            {
                paramStart = sb.Length;
            }
            sb.Append(Parameters[x].ToString());

            if (x == argIndex)
            {
                paramEnd = sb.Length;
            }
            

            if (x < Parameters.Count-1)
            {
                sb.Append(", ");
            }
        }
        
        var returnTypeStr = GetReturnTypeString();
        sb.Append($") -> {returnTypeStr}");
        return (sb.ToString(), paramStart, paramEnd);
    }
}

/// <summary>
/// Builder class for constructing ComposableFunctionInfo with a fluent API
/// </summary>
public class FunctionBuilder
{
    private readonly FunctionInfo _function = new();

    public FunctionBuilder(string name)
    {
        _function.Name = name;
    }

    /// <summary>
    /// Set the return type
    /// </summary>
    public FunctionBuilder Returns(PeopleCodeType type, byte arrayDimensionality = 0)
    {
        _function.ReturnType = new TypeWithDimensionality(type, arrayDimensionality);
        _function.ReturnUnionTypes = null; // Clear union types
        return this;
    }

    /// <summary>
    /// Set the return type using TypeWithDimensionality
    /// </summary>
    public FunctionBuilder Returns(TypeWithDimensionality returnType)
    {
        _function.ReturnType = returnType;
        _function.ReturnUnionTypes = null; // Clear union types
        return this;
    }

    /// <summary>
    /// Set union return types
    /// </summary>
    public FunctionBuilder ReturnsUnion(params TypeWithDimensionality[] unionTypes)
    {
        if (unionTypes == null || unionTypes.Length == 0)
        {
            throw new ArgumentException("Union types cannot be empty", nameof(unionTypes));
        }

        _function.ReturnUnionTypes = unionTypes.ToList();
        // Set the first type as the primary return type for backward compatibility
        _function.ReturnType = unionTypes[0];
        return this;
    }

    /// <summary>
    /// Set union return types using PeopleCodeType values
    /// </summary>
    public FunctionBuilder ReturnsUnion(params PeopleCodeType[] unionTypes)
    {
        var typeWithDims = unionTypes.Select(t => new TypeWithDimensionality(t, 0)).ToArray();
        return ReturnsUnion(typeWithDims);
    }

    /// <summary>
    /// Set return type to void
    /// </summary>
    public FunctionBuilder ReturnsVoid() => Returns(PeopleCodeType.Void);

    /// <summary>
    /// Mark as default method
    /// </summary>
    public FunctionBuilder AsDefault()
    {
        _function.IsDefaultMethod = true;
        return this;
    }

    /// <summary>
    /// Mark as property
    /// </summary>
    public FunctionBuilder AsProperty()
    {
        _function.IsProperty = true;
        return this;
    }

    /// <summary>
    /// Add a parameter
    /// </summary>
    public FunctionBuilder AddParameter(Parameter parameter)
    {
        _function.Parameters.Add(parameter);
        return this;
    }

    /// <summary>
    /// Add a simple single parameter
    /// </summary>
    public FunctionBuilder AddParameter(PeopleCodeType type, byte arrayDimensionality = 0, string name = "")
    {
        return AddParameter(new SingleParameter(type, arrayDimensionality, name));
    }

    /// <summary>
    /// Add an optional parameter (equivalent to variable parameter with min=0, max=1)
    /// </summary>
    public FunctionBuilder AddOptionalParameter(Parameter parameter)
    {
        return AddParameter(new VariableParameter(parameter, 0, 1));
    }

    /// <summary>
    /// Add a variable parameter (varargs)
    /// </summary>
    public FunctionBuilder AddVariableParameter(Parameter innerParameter, int minCount = 0, int maxCount = int.MaxValue)
    {
        return AddParameter(new VariableParameter(innerParameter, minCount, maxCount));
    }

    /// <summary>
    /// Add a parameter group
    /// </summary>
    public FunctionBuilder AddParameterGroup(params Parameter[] parameters)
    {
        return AddParameter(new ParameterGroup(parameters));
    }

    /// <summary>
    /// Add a union parameter
    /// </summary>
    public FunctionBuilder AddUnionParameter(params TypeWithDimensionality[] types)
    {
        return AddParameter(new UnionParameter(types));
    }

    /// <summary>
    /// Set description
    /// </summary>
    public FunctionBuilder WithDescription(string description)
    {
        _function.Description = description;
        return this;
    }

    /// <summary>
    /// Build the function info
    /// </summary>
    public FunctionInfo Build() => _function;
}
