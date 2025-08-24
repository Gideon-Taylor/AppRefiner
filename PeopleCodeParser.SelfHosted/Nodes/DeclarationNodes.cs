using PeopleCodeParser.SelfHosted;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Base class for all declaration nodes
/// </summary>
public abstract class DeclarationNode : AstNode
{
    /// <summary>
    /// Name of the declared item
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// True if this Declaration had a semicolon in the source code
    /// This is used for style checking, as PeopleCode allows but doesn't require
    /// semicolons after the final declaration in a block
    /// </summary>
    public bool HasSemicolon { get; set; } = false;

    /// <summary>
    /// Visibility modifier
    /// </summary>
    public VisibilityModifier Visibility { get; set; } = VisibilityModifier.Public;

    protected DeclarationNode(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}

/// <summary>
/// Method declaration or implementation
/// </summary>
public class MethodNode : DeclarationNode
{
    /// <summary>
    /// Method parameters
    /// </summary>
    public List<ParameterNode> Parameters { get; } = new();

    /// <summary>
    /// Return type (null for constructors and procedures)
    /// </summary>
    public TypeNode? ReturnType { get; set; }

    /// <summary>
    /// Method body (null for declarations)
    /// </summary>
    public BlockNode? Body { get; set; }

    /// <summary>
    /// True if this method is abstract
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// True if this is a constructor
    /// </summary>
    public bool IsConstructor => Name.Equals("constructor", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True if this is a method implementation (has a body)
    /// </summary>
    public bool IsImplementation => Body != null;

    /// <summary>
    /// True if this is a declaration only (no body)
    /// </summary>
    public bool IsDeclaration => Body == null;

    /// <summary>
    /// Class this method belongs to (for implementations outside class)
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Documentation string (from DOC annotation)
    /// </summary>
    public string? Documentation { get; set; }
    
    /// <summary>
    /// Implemented interfaces (from EXTENDS/IMPLEMENTS annotation)
    /// </summary>
    public List<TypeNode> ImplementedInterfaces { get; } = new();
    
    /// <summary>
    /// Implemented method name (from EXTENDS/IMPLEMENTS annotation)
    /// </summary>
    public string? ImplementedMethodName { get; set; }

    public MethodNode(string name) : base(name)
    {
    }
    
    public void AddImplementedInterface(TypeNode interfaceType)
    {
        ImplementedInterfaces.Add(interfaceType);
        AddChild(interfaceType);
    }

    public void AddParameter(ParameterNode parameter)
    {
        Parameters.Add(parameter);
        AddChild(parameter);
    }

    public void SetReturnType(TypeNode returnType)
    {
        if (ReturnType != null)
            RemoveChild(ReturnType);

        ReturnType = returnType;
        if (returnType != null)
            AddChild(returnType);
    }

    public void SetBody(BlockNode body)
    {
        if (Body != null)
            RemoveChild(Body);

        Body = body;
        if (body != null)
            AddChild(body);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitMethod(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitMethod(this);
    }

    public override string ToString()
    {
        var className = ClassName != null ? $"{ClassName}." : "";
        var returnType = ReturnType != null ? $" RETURNS {ReturnType}" : "";
        var paramStr = string.Join(", ", Parameters);
        var impl = IsImplementation ? " (impl)" : "";
        return $"{className}{Name}({paramStr}){returnType}{impl}";
    }
}

/// <summary>
/// Property declaration or implementation
/// </summary>
public class PropertyNode : DeclarationNode
{
    /// <summary>
    /// Property type
    /// </summary>
    public TypeNode Type { get; }

    /// <summary>
    /// True if property has a getter
    /// </summary>
    public bool HasGet { get; set; } = true;

    /// <summary>
    /// True if property has a setter
    /// </summary>
    public bool HasSet { get; set; } = true;

    /// <summary>
    /// True if property is read-only
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// True if property is abstract
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// Class or Interface this property implements
    /// </summary>
    public TypeNode ImplementedInterface { get; set; }

    /// <summary>
    /// Gets or sets the name of the implemented property.
    /// </summary>
    public string ImplementedPropertyName { get; set; }

    /// <summary>
    /// Property getter body (for implementations)
    /// </summary>
    public BlockNode? GetterBody { get; set; }

    /// <summary>
    /// Property setter body (for implementations)
    /// </summary>
    public BlockNode? SetterBody { get; set; }

    /// <summary>
    /// True if this is a getter implementation
    /// </summary>
    public bool IsGetter => GetterBody != null && SetterBody == null;

    /// <summary>
    /// True if this is a setter implementation
    /// </summary>
    public bool IsSetter => SetterBody != null && GetterBody == null;

    /// <summary>
    /// True if this is a property implementation (has getter or setter body)
    /// </summary>
    public bool IsImplementation => GetterBody != null || SetterBody != null;

    /// <summary>
    /// Class this property belongs to (for implementations outside class)
    /// </summary>
    public string? ClassName { get; set; }

    public PropertyNode(string name, TypeNode type) : base(name)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        AddChild(type);
    }

    public void SetGetterBody(BlockNode getterBody)
    {
        if (GetterBody != null)
            RemoveChild(GetterBody);

        GetterBody = getterBody;
        if (getterBody != null)
            AddChild(getterBody);
    }

    public void SetSetterBody(BlockNode setterBody)
    {
        if (SetterBody != null)
            RemoveChild(SetterBody);

        SetterBody = setterBody;
        if (setterBody != null)
            AddChild(setterBody);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitProperty(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitProperty(this);
    }

    public override string ToString()
    {
        var className = ClassName != null ? $"{ClassName}." : "";
        var access = (HasGet, HasSet, IsReadOnly) switch
        {
            (true, true, false) => " GET SET",
            (true, false, _) => " GET",
            (false, true, false) => " SET",
            (true, _, true) => " READONLY",
            _ => ""
        };
        var impl = IsImplementation ? " (impl)" : "";
        return $"{className}{Type} {Name}{access}{impl}";
    }
}

/// <summary>
/// Variable declaration
/// </summary>
public class VariableNode : DeclarationNode
{
    /// <summary>
    /// Variable type
    /// </summary>
    public TypeNode Type { get; }

    /// <summary>
    /// Variable scope
    /// </summary>
    public VariableScope Scope { get; }

    /// <summary>
    /// Initial value expression (optional)
    /// </summary>
    public ExpressionNode? InitialValue { get; set; }

    /// <summary>
    /// Additional variable names (for multi-variable declarations like LOCAL string &a, &b, &c)
    /// </summary>
    public List<string> AdditionalNames { get; } = new();
    
    /// <summary>
    /// Variable name information including tokens (main name + additional names)
    /// </summary>
    public List<VariableNameInfo> NameInfos { get; } = new();

    public VariableNode(string name, TypeNode type, VariableScope scope) : base(name)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Scope = scope;
        AddChild(type);
    }

    public void SetInitialValue(ExpressionNode initialValue)
    {
        if (InitialValue != null)
            RemoveChild(InitialValue);

        InitialValue = initialValue;
        if (initialValue != null)
            AddChild(initialValue);
    }

    public void AddName(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Variable name cannot be empty", nameof(name));
        
        AdditionalNames.Add(name);
    }

    public IEnumerable<string> AllNames => new[] { Name }.Concat(AdditionalNames);

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitVariable(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitVariable(this);
    }

    public override string ToString()
    {
        var names = string.Join(", ", AllNames.Select(n => $"&{n}"));
        var init = InitialValue != null ? $" = {InitialValue}" : "";
        return $"{Scope} {Type} {names}{init}";
    }
}

/// <summary>
/// Constant declaration
/// </summary>
public class ConstantNode : DeclarationNode
{
    /// <summary>
    /// Constant value
    /// </summary>
    public ExpressionNode Value { get; }

    public ConstantNode(string name, ExpressionNode value) : base(name)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        AddChild(value);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitConstant(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitConstant(this);
    }

    public override string ToString()
    {
        return $"CONSTANT &{Name} = {Value}";
    }
}

/// <summary>
/// Function declaration or definition
/// </summary>
public class FunctionNode : DeclarationNode
{
    /// <summary>
    /// Function parameters
    /// </summary>
    public List<ParameterNode> Parameters { get; } = new();

    /// <summary>
    /// Return type (null for procedures)
    /// </summary>
    public TypeNode? ReturnType { get; set; }

    /// <summary>
    /// Function body (null for declarations)
    /// </summary>
    public BlockNode? Body { get; set; }

    /// <summary>
    /// Function type (PeopleCode or DLL)
    /// </summary>
    public FunctionType FunctionType { get; }

    /// <summary>
    /// Documentation string (from DOC annotation)
    /// </summary>
    public string? Documentation { get; set; }

    /// <summary>
    /// True if this is a function implementation (has a body)
    /// </summary>
    public bool IsImplementation => Body != null;

    /// <summary>
    /// True if this is a declaration only (no body)
    /// </summary>
    public bool IsDeclaration => Body == null;

    // For PeopleCode function declarations
    /// <summary>
    /// Record name (for PeopleCode function declarations)
    /// </summary>
    public string? RecordName { get; set; }

    /// <summary>
    /// Field name (for PeopleCode function declarations)
    /// </summary>
    public string? FieldName { get; set; }

    /// <summary>
    /// Record event (for PeopleCode function declarations)
    /// </summary>
    public string? RecordEvent { get; set; }

    // For DLL function declarations
    /// <summary>
    /// Library name (for DLL function declarations)
    /// </summary>
    public string? LibraryName { get; set; }

    /// <summary>
    /// Alias name (for DLL function declarations)
    /// </summary>
    public string? AliasName { get; set; }

    public FunctionNode(string name, FunctionType functionType) : base(name)
    {
        FunctionType = functionType;
    }

    public void AddParameter(ParameterNode parameter)
    {
        Parameters.Add(parameter);
        AddChild(parameter);
    }

    public void SetReturnType(TypeNode returnType)
    {
        if (ReturnType != null)
            RemoveChild(ReturnType);

        ReturnType = returnType;
        if (returnType != null)
            AddChild(returnType);
    }

    public void SetBody(BlockNode body)
    {
        if (Body != null)
            RemoveChild(Body);

        Body = body;
        if (body != null)
            AddChild(body);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitFunction(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitFunction(this);
    }

    public override string ToString()
    {
        var returnType = ReturnType != null ? $" RETURNS {ReturnType}" : "";
        var paramStr = string.Join(", ", Parameters);
        var impl = IsImplementation ? " (impl)" : "";
        
        return FunctionType switch
        {
            FunctionType.PeopleCode => $"FUNCTION {Name}({paramStr}){returnType} PEOPLECODE {RecordName}.{FieldName} {RecordEvent}{impl}",
            FunctionType.Library => $"FUNCTION {Name}({paramStr}){returnType} LIBRARY {LibraryName}{impl}",
            FunctionType.UserDefined => $"FUNCTION {Name}({paramStr}){returnType}{impl}",
            _ => $"FUNCTION {Name}({paramStr}){returnType}{impl}"
        };
    }
}

/// <summary>
/// Method or function parameter
/// </summary>
public class ParameterNode : AstNode
{
    /// <summary>
    /// Parameter name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Parameter type
    /// </summary>
    public TypeNode Type { get; set; }

    /// <summary>
    /// True if parameter is passed by reference (OUT parameter)
    /// </summary>
    public bool IsOut { get; set; }

    /// <summary>
    /// Parameter passing mode (for DLL functions)
    /// </summary>
    public ParameterMode Mode { get; set; } = ParameterMode.Value;

    public ParameterNode(string name, TypeNode type)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        AddChild(type);
    }

    public override void Accept(IAstVisitor visitor)
    {
        // Parameters are handled by their parent nodes
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return default!;
    }

    public override string ToString()
    {
        var name = Name.StartsWith("&") ? Name : $"&{Name}";
        var mode = Mode != ParameterMode.Value ? $" {Mode.ToString().ToUpper()}" : "";
        var outStr = IsOut ? " OUT" : "";
        return $"{name} AS {Type}{mode}{outStr}";
    }
}

/// <summary>
/// Variable scopes
/// </summary>
public enum VariableScope
{
    Local,
    Global,
    Component,
    Instance
}

/// <summary>
/// Function types
/// </summary>
public enum FunctionType
{
    /// <summary>
    /// User-defined function
    /// </summary>
    UserDefined,

    /// <summary>
    /// PeopleCode function declaration
    /// </summary>
    PeopleCode,

    /// <summary>
    /// Library (DLL) function declaration
    /// </summary>
    Library
}

/// <summary>
/// Parameter passing modes (for DLL functions)
/// </summary>
public enum ParameterMode
{
    Value,
    Reference
}