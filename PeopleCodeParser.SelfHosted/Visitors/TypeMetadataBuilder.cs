using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// AST visitor that extracts TypeMetadata from a parsed PeopleCode program.
/// Builds metadata for application classes, interfaces, and function libraries.
/// </summary>
/// <remarks>
/// This visitor walks a ProgramNode AST and extracts type information including:
/// - Class/interface declarations with their signatures
/// - Methods and properties with parameter/return type information
/// - Function declarations in function library programs
///
/// The resulting TypeMetadata can be used by type inference systems without
/// requiring the inference system to have a dependency on the parser.
/// </remarks>
public class TypeMetadataBuilder : AstVisitorBase
{
    private readonly Dictionary<string, FunctionInfo> _methods = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PropertyInfo> _properties = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PropertyInfo> _instanceVariables = new(StringComparer.OrdinalIgnoreCase);
    private FunctionInfo? _constructor;
    private string _qualifiedName = string.Empty;
    private string _name = string.Empty;
    private string? _packageName;
    private ProgramKind _kind = ProgramKind.FunctionLibrary;
    private string? _baseClassName;
    private bool _isBaseClassBuiltin;
    private PeopleCodeType? _builtinBaseType;
    private string? _interfaceName;

    /// <summary>
    /// Extracts TypeMetadata from a parsed ProgramNode.
    /// </summary>
    /// <param name="program">The parsed program AST</param>
    /// <param name="qualifiedName">
    /// Optional qualified name override (e.g., "PKG:Class").
    /// If not provided, will be inferred from the program structure.
    /// </param>
    /// <returns>TypeMetadata containing the extracted type information</returns>
    public static TypeMetadata ExtractMetadata(ProgramNode program, string? qualifiedName = null)
    {
        var builder = new TypeMetadataBuilder();
        program.Accept(builder);

        // Use provided qualified name or build from program
        if (!string.IsNullOrEmpty(qualifiedName))
        {
            builder._qualifiedName = qualifiedName;
            builder.ParseQualifiedName(qualifiedName);
        }

        return new TypeMetadata
        {
            QualifiedName = builder._qualifiedName,
            Name = builder._name,
            PackageName = builder._packageName,
            Kind = builder._kind,
            BaseClassName = builder._baseClassName,
            IsBaseClassBuiltin = builder._isBaseClassBuiltin,
            BuiltinBaseType = builder._builtinBaseType,
            InterfaceName = builder._interfaceName,
            Methods = builder._methods,
            Properties = builder._properties,
            InstanceVariables = builder._instanceVariables,
            Constructor = builder._constructor
        };
    }

    /// <summary>
    /// Parse a qualified name to extract name and package components.
    /// </summary>
    private void ParseQualifiedName(string qualifiedName)
    {
        var parts = qualifiedName.Split(':');
        if (parts.Length > 1)
        {
            _name = parts[^1]; // Last part is the name
            _packageName = string.Join(":", parts[..^1]); // Everything before is the package
        }
        else
        {
            _name = qualifiedName;
            _packageName = null;
        }
    }

    public override void VisitProgram(ProgramNode node)
    {
        // Determine program kind and extract basic info
        if (node.AppClass != null)
        {
            _kind = ProgramKind.AppClass;
            _name = node.AppClass.Name;
            _qualifiedName = _name; // Will be updated if package is known
            node.AppClass.Accept(this);
        }
        else if (node.Interface != null)
        {
            _kind = ProgramKind.Interface;
            _name = node.Interface.Name;
            _qualifiedName = _name; // Will be updated if package is known
            node.Interface.Accept(this);
        }
        else
        {
            // Function library - extract top-level function declarations
            _kind = ProgramKind.FunctionLibrary;
            _name = "FunctionLibrary"; // Generic name for function libraries
            _qualifiedName = _name;

            // Only visit function declarations (not implementations)
            foreach (var function in node.Functions)
            {
                function.Accept(this);
            }
        }
    }

    public override void VisitAppClass(AppClassNode node)
    {
        // Extract base class and interface information
        if (node.BaseClass != null)
        {
            _baseClassName = ExtractTypeName(node.BaseClass);

            // Check if base class is a builtin type
            if (node.BaseClass is BuiltInTypeNode builtInNode)
            {
                _isBaseClassBuiltin = true;
                _builtinBaseType = BuiltinTypeExtensions.FromString(builtInNode.TypeName);
            }
        }

        if (node.ImplementedInterface != null)
        {
            _interfaceName = ExtractTypeName(node.ImplementedInterface);
        }

        // Visit all method signatures
        // For AppClasses, we extract metadata from all methods (both declared and implemented)
        // since the method signatures in the class declaration are what matter for type checking
        foreach (var method in node.Methods)
        {
            var functionInfo = BuildFunctionInfo(method);

            if (method.IsConstructor)
            {
                _constructor = functionInfo;
            }
            else
            {
                _methods[method.Name] = functionInfo;
            }
        }

        // Visit property declarations
        foreach (var property in node.Properties)
        {
            var propertyInfo = BuildPropertyInfo(property);
            _properties[property.Name] = propertyInfo;
        }

        foreach(var instanceVar in node.InstanceVariables)
        {
            var propertyInfo = BuildPropertyInfo(instanceVar);
            // Handle all variable names in multi-variable declarations
            foreach (var nameInfo in instanceVar.NameInfos)
            {
                _instanceVariables[nameInfo.Name] = propertyInfo;
            }
        }
    }

    private PropertyInfo BuildPropertyInfo(ProgramVariableNode instanceVar)
    {
        var typeWithDim = BuildTypeWithDimensionality(instanceVar.Type);
        return PropertyInfo.FromTypeWithDimensionality(typeWithDim);
    }

    public override void VisitInterface(InterfaceNode node)
    {
        // Extract base interface information
        if (node.BaseInterface != null)
        {
            _baseClassName = ExtractTypeName(node.BaseInterface); // Reuse base class field for interface inheritance
        }

        // Visit method signatures
        foreach (var method in node.Methods)
        {
            var functionInfo = BuildFunctionInfo(method);
            _methods[method.Name] = functionInfo;
        }

        // Visit property signatures
        foreach (var property in node.Properties)
        {
            var propertyInfo = BuildPropertyInfo(property);
            _properties[property.Name] = propertyInfo;
        }
    }

    public override void VisitFunction(FunctionNode node)
    {
        var functionInfo = BuildFunctionInfo(node);
        _methods[node.Name] = functionInfo;
    }

    /// <summary>
    /// Build FunctionInfo from a MethodNode.
    /// </summary>
    private FunctionInfo BuildFunctionInfo(MethodNode method)
    {
        var functionInfo = new FunctionInfo
        {
            Name = method.Name,
            ReturnType = method.ReturnType != null
                ? BuildTypeWithDimensionality(method.ReturnType)
                : new TypeWithDimensionality(PeopleCodeType.Void),
            Parameters = method.Parameters.Select(BuildParameter).ToList()
        };

        return functionInfo;
    }

    /// <summary>
    /// Build FunctionInfo from a FunctionNode.
    /// </summary>
    private FunctionInfo BuildFunctionInfo(FunctionNode function)
    {
        var functionInfo = new FunctionInfo
        {
            Name = function.Name,
            ReturnType = function.ReturnType != null
                ? BuildTypeWithDimensionality(function.ReturnType)
                : new TypeWithDimensionality(PeopleCodeType.Void),
            Parameters = function.Parameters.Select(BuildParameter).ToList()
        };

        return functionInfo;
    }

    /// <summary>
    /// Build PropertyInfo from a PropertyNode.
    /// </summary>
    private PropertyInfo BuildPropertyInfo(PropertyNode property)
    {
        var typeWithDim = BuildTypeWithDimensionality(property.Type);
        return PropertyInfo.FromTypeWithDimensionality(typeWithDim);
    }

    /// <summary>
    /// Build Parameter from ParameterNode.
    /// </summary>
    private Parameter BuildParameter(ParameterNode paramNode)
    {
        var typeWithDim = BuildTypeWithDimensionality(paramNode.Type);

        if (paramNode.IsOut)
        {
            typeWithDim.MustBeVariable = true;
        }

        return new SingleParameter(typeWithDim, paramNode.Name);
    }

    /// <summary>
    /// Build TypeWithDimensionality from a TypeNode.
    /// </summary>
    private TypeWithDimensionality BuildTypeWithDimensionality(TypeNode? typeNode)
    {
        if (typeNode == null)
        {
            return new TypeWithDimensionality(PeopleCodeType.Any, 0);
        }

        switch (typeNode)
        {
            case BuiltInTypeNode builtIn:
                return new TypeWithDimensionality(BuiltinTypeExtensions.FromString(builtIn.TypeName), 0);

            case ArrayTypeNode array:
                // Handle both explicit and implicit (null) element types
                // When ElementType is null, default to Any (e.g., "array &arr" without "of type")
                var elementTypeWithDim = array.ElementType != null
                    ? BuildTypeWithDimensionality(array.ElementType)
                    : new TypeWithDimensionality(PeopleCodeType.Any, 0);

                return new TypeWithDimensionality(
                    elementTypeWithDim.Type,
                    (byte)(elementTypeWithDim.ArrayDimensionality + array.Dimensions),
                    elementTypeWithDim.AppClassPath
                );

            case AppClassTypeNode appClass:
                var appClassPath = ExtractTypeName(appClass);
                return new TypeWithDimensionality(PeopleCodeType.AppClass, 0, appClassPath);

            default:
                // Unknown type - default to Any
                return new TypeWithDimensionality(PeopleCodeType.Any, 0);
        }
    }

    /// <summary>
    /// Extract type name from TypeNode (handles app class qualified names).
    /// </summary>
    private string ExtractTypeName(TypeNode typeNode)
    {
        switch (typeNode)
        {
            case AppClassTypeNode appClass:
                // Build qualified name from package path and class name
                if (appClass.PackagePath.Count > 0)
                {
                    return string.Join(":", appClass.PackagePath) + ":" + appClass.ClassName;
                }
                return appClass.ClassName;

            case BuiltInTypeNode builtIn:
                return builtIn.TypeName;

            default:
                return typeNode.ToString() ?? "Unknown";
        }
    }
}
