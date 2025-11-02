using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;

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

    public Token NameToken { get; }

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

    protected DeclarationNode(string name, Token nameToken)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NameToken = nameToken;
    }

    public abstract void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode);
}

/// <summary>
/// Method declaration or implementation
/// </summary>
public class MethodNode : DeclarationNode
{
    /* Stores the source span for the method header since the base AstNode.SourceSpan
     * covers the method implementation */
    public SourceSpan HeaderSpan { get; set; }

    /// <summary>
    /// Method parameters
    /// </summary>
    public List<ParameterNode> Parameters { get; } = new();

    public List<ParameterNode> ParameterAnnotations { get; set; } = new();

    /// <summary>
    /// Return type (null for constructors and procedures)
    /// </summary>
    public TypeNode? ReturnType { get; set; }

    /// <summary>
    /// Method implementation (null for declarations)
    /// </summary>
    public MethodImplNode? Implementation { get; set; }

    /// <summary>
    /// Method body (compatibility property - returns Implementation?.Body)
    /// </summary>
    public BlockNode? Body => Implementation?.Body;

    /// <summary>
    /// True if this method is abstract
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// True if this is a constructor
    /// </summary>
    public bool IsConstructor { get; set; }

    /// <summary>
    /// True if this is a method implementation (has an implementation)
    /// </summary>
    public bool IsImplementation => Implementation != null;

    /// <summary>
    /// True if this is a declaration only (no implementation)
    /// </summary>
    public bool IsDeclaration => Implementation == null;

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

    public MethodNode(string name, Token nameToken) : base(name, nameToken)
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

    public void SetImplementation(MethodImplNode implementation)
    {
        if (Implementation != null)
        {
            RemoveChild(Implementation);
            Implementation.Declaration = null; // Clear the back-reference
        }

        Implementation = implementation;
        if (implementation != null)
        {
            AddChild(implementation);
            implementation.Declaration = this; // Establish the back-reference
        }
    }

    public void SetBody(BlockNode body)
    {
        // For backward compatibility, create a MethodImplNode with the body
        if (body != null)
        {
            var methodImpl = new MethodImplNode(Name, NameToken, body);
            SetImplementation(methodImpl);
        }
        else
        {
            SetImplementation(null!);
        }
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
        var returnType = ReturnType != null ? $" Returns {ReturnType}" : "";
        var paramStr = string.Join(", ", Parameters);
        var impl = IsImplementation ? " (impl)" : "";
        return $"{className}{Name}({paramStr}){returnType}{impl}";
    }

    internal void AddParameterAnnotation(ParameterNode parameter)
    {
        ParameterAnnotations.Add(parameter);
        AddChild(parameter);
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        /* We don't register anything here, since they are registered at ParseClassMember() time */
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
    public bool HasGet { get; set; } = false;

    /// <summary>
    /// True if property has a setter
    /// </summary>
    public bool HasSet { get; set; } = false;

    /// <summary>
    /// True if property is read-only
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// True if property is abstract
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// Property getter implementation (for implementations)
    /// </summary>
    public PropertyImplNode? Getter { get; set; } 

    /// <summary>
    /// Property setter implementation (for implementations)
    /// </summary>

    public PropertyImplNode? Setter { get; set; }

    public BlockNode? GetterBody { get { return Getter?.Body ?? null; } }
    public BlockNode? SetterBody { get { return Setter?.Body ?? null; } }

    /// <summary>
    /// Class this property belongs to (for implementations outside class)
    /// </summary>
    public string? ClassName { get; set; }

    public PropertyNode(string name, Token nameToken, TypeNode type) : base(name, nameToken)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        AddChild(type);
    }

    public void SetGetterImplementation(PropertyImplNode getterImplementation)
    {
        if (Getter != null)
            RemoveChild(Getter);

        Getter = getterImplementation;
        if (getterImplementation != null)
            AddChild(getterImplementation);

        
    }

    public void SetSetterImplementation(PropertyImplNode setterImplementation)
    {
        if (Setter != null)
            RemoveChild(Setter);

        Setter = setterImplementation;
        if (setterImplementation != null)
            AddChild(setterImplementation);
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
            (true, true, false) => " get set",
            (true, false, _) => " get",
            (false, true, false) => " set",
            (true, _, true) => " readonly",
            _ => ""
        };
        return $"{className}{Type} {Name}{access}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        /* Property statements are registerd when the getter/setter are parsed since
         * they can appear in different locations in the code 
         */
    }
}

public class PropertyImplNode : AstNode
{
    public string Name { get; set; }
    public Token NameToken { get; set; }

    public bool IsGetter { get; set; } = false;
    public bool IsSetter { get; set; } = false;

    public List<ParameterNode> ParameterAnnotations { get; } = new();

    public void AddParameterAnnotation(ParameterNode annotation)
    {
        ParameterAnnotations.Add(annotation);
        AddChild(annotation);
    }
    public void SetBody(BlockNode body)
    {
        if (Body != null)
        {
            RemoveChild(Body);
        }
        Body = body;
        AddChild(Body);
    }

    public void SetImplementationType(TypeNode type)
    {
        if (ImplementedInterface != null)
        {
            RemoveChild(ImplementedInterface);
        }
        ImplementedInterface = type;
    }
    /// <summary>
    /// Class or Interface this property implements
    /// </summary>
    public TypeNode? ImplementedInterface { get; set; }

    /// <summary>
    /// Gets or sets the name of the implemented property.
    /// </summary>
    public string? ImplementedPropertyName { get; set; }

    public BlockNode? Body { get; set; }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitPropertyImpl(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitPropertyImpl(this);
    }
}
/// <summary>
/// Program-level variable declaration (Component, Global, or Instance scope)
/// Does not include local variables - use LocalVariableDeclarationNode for those
/// </summary>
public class ProgramVariableNode : DeclarationNode
{
    /// <summary>
    /// Variable type
    /// </summary>
    public TypeNode Type { get; }

    /// <summary>
    /// Variable scope (Component, Global, or Instance only)
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

    public ProgramVariableNode(string name, Token nameToken, TypeNode type, VariableScope scope) : base(name, nameToken)
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
        visitor.VisitProgramVariable(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitProgramVariable(this);
    }

    public override string ToString()
    {
        var names = string.Join(", ", AllNames.Select(n => $"{n}"));
        var init = InitialValue != null ? $" = {InitialValue}" : "";
        return $"{Scope} {Type} {names}{init}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        /* Variable declarations are in the preamble and not counted as statements */
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


    public ConstantNode(string name, Token nameToken, ExpressionNode value) : base(name, nameToken)
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
        return $"Constant &{Name} = {Value}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        /* Constants are in the preamble and not counted as statements */
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

    public FunctionNode(string name, Token nameToken, FunctionType functionType) : base(name, nameToken)
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
        var returnType = ReturnType != null ? $" Returns {ReturnType}" : "";
        var paramStr = string.Join(", ", Parameters);
        var impl = IsImplementation ? " (impl)" : "";

        return FunctionType switch
        {
            FunctionType.PeopleCode => $"Function {Name}({paramStr}){returnType} PeopleCode {RecordName}.{FieldName} {RecordEvent}{impl}",
            FunctionType.Library => $"Function {Name}({paramStr}){returnType} Library {LibraryName}{impl}",
            FunctionType.UserDefined => $"Function {Name}({paramStr}){returnType}{impl}",
            _ => $"Function {Name}({paramStr}){returnType}{impl}"
        };
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        if (IsDeclaration) return;

        /* registering the Function line */
        programNode.SetStatementNumber(SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine(SourceSpan.Start.Line, this);

        if (Body == null) return;

        Body.RegisterStatementNumbers(parser, programNode);


        /* Registering the end-function line */
        if (Body.Statements.Count > 0 && Body.Statements.Last().HasSemicolon)
        {
            programNode.SetStatementNumber( SourceSpan.End.Line);
        }

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

    public Token NameToken { get; }

    /// <summary>
    /// Parameter type
    /// </summary>
    private TypeNode _type;
    public TypeNode Type
    {
        get => _type;
        set
        {
            if (_type != null)
                RemoveChild(_type);
            _type = value;
            if (_type != null)
                AddChild(_type);
        }
    }

    /// <summary>
    /// True if parameter is passed by reference (OUT parameter)
    /// </summary>
    public bool IsOut { get; set; }

    /// <summary>
    /// Parameter passing mode (for DLL functions)
    /// </summary>
    public ParameterMode Mode { get; set; } = ParameterMode.Value;

    public ParameterNode(string name, Token nameToken, TypeNode type)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NameToken = nameToken;
        Type = type ?? throw new ArgumentNullException(nameof(type));
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
        var mode = Mode != ParameterMode.Value ? $" {Mode.ToString()}" : "";
        var outStr = IsOut ? " out" : "";
        return $"{name} As {Type}{mode}{outStr}";
    }
}

/// <summary>
/// Method implementation node - captures the complete method implementation structure 
/// from 'method' to 'end-method' including annotations and body
/// </summary>
public class MethodImplNode : AstNode
{
    /// <summary>
    /// Method name from the implementation
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Method name token from the implementation
    /// </summary>
    public Token NameToken { get; }

    /// <summary>
    /// Parameter annotations parsed from /+ ... +/ comments in method implementation
    /// </summary>
    public List<ParameterNode> ParameterAnnotations { get; } = new();

    /// <summary>
    /// Return type annotation from implementation (if specified in annotations)
    /// </summary>
    public TypeNode? ReturnTypeAnnotation { get; set; }

    /// <summary>
    /// The actual method body (statement block)
    /// </summary>
    public BlockNode Body { get; }

    /// <summary>
    /// Token marking the start of the method body statements (after annotations)
    /// </summary>
    public Token? BodyStartToken { get; set; }

    /// <summary>
    /// Token marking the end of the method body statements (before end-method)
    /// </summary>
    public Token? BodyEndToken { get; set; }

    /// <summary>
    /// Implemented interfaces (from EXTENDS/IMPLEMENTS annotation)
    /// </summary>
    public List<TypeNode> ImplementedInterfaces { get; } = new();

    /// <summary>
    /// Implemented method name (from EXTENDS/IMPLEMENTS annotation)
    /// </summary>
    public string? ImplementedMethodName { get; set; }

    /// <summary>
    /// Reference back to the method declaration that this implementation belongs to
    /// </summary>
    public MethodNode? Declaration { get; set; }

    public MethodImplNode(string name, Token nameToken, BlockNode body)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NameToken = nameToken;
        Body = body ?? throw new ArgumentNullException(nameof(body));
        AddChild(body);
    }

    public void AddParameterAnnotation(ParameterNode parameter)
    {
        ParameterAnnotations.Add(parameter);
        AddChild(parameter);
    }

    public void SetReturnTypeAnnotation(TypeNode returnType)
    {
        if (ReturnTypeAnnotation != null)
            RemoveChild(ReturnTypeAnnotation);

        ReturnTypeAnnotation = returnType;
        if (returnType != null)
            AddChild(returnType);
    }

    public void AddImplementedInterface(TypeNode interfaceType)
    {
        ImplementedInterfaces.Add(interfaceType);
        AddChild(interfaceType);
    }

    /// <summary>
    /// Convenience property to get method parameters from the declaration
    /// </summary>
    public IReadOnlyList<ParameterNode> Parameters => Declaration?.Parameters ?? new List<ParameterNode>();

    /// <summary>
    /// Convenience property to get method return type from the declaration
    /// </summary>
    public TypeNode? ReturnType => Declaration?.ReturnType;

    /// <summary>
    /// Convenience property to get method visibility from the declaration
    /// </summary>
    public VisibilityModifier Visibility => Declaration?.Visibility ?? VisibilityModifier.Public;

    /// <summary>
    /// Convenience property to check if this is a constructor from the declaration
    /// </summary>
    public bool IsConstructor => Declaration?.IsConstructor ?? false;

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitMethodImpl(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitMethodImpl(this);
    }

    public override string ToString()
    {
        // Use declaration info if available, fall back to annotation info
        var parameters = Declaration?.Parameters ?? new List<ParameterNode>();
        var returnType = Declaration?.ReturnType ?? ReturnTypeAnnotation;
        var visibility = Declaration?.Visibility ?? VisibilityModifier.Public;
        
        var paramStr = parameters.Count > 0 ? $"({string.Join(", ", parameters)})" : "()";
        var returnStr = returnType != null ? $" Returns {returnType}" : "";
        var visStr = visibility != VisibilityModifier.Public ? $"{visibility} " : "";
        var annotationCount = ParameterAnnotations.Count > 0 ? $" [{ParameterAnnotations.Count} annotations]" : "";
        
        return $"{visStr}Method {Name}{paramStr}{returnStr} impl{annotationCount}";
    }
}

/// <summary>
/// Variable scopes for ProgramVariableNode
/// Note: Local variables use LocalVariableDeclarationNode instead
/// </summary>
public enum VariableScope
{
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