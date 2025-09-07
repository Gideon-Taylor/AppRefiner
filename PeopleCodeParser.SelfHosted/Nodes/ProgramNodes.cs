using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Root node representing a complete PeopleCode program
/// </summary>
public class ProgramNode : AstNode
{
    /// <summary>
    /// Import declarations at the top of the program
    /// </summary>
    public List<ImportNode> Imports { get; } = new();

    /// <summary>
    /// Application class definition (if this is a class program)
    /// </summary>
    public AppClassNode? AppClass { get; set; }

    /// <summary>
    /// Interface definition (if this is an interface program)
    /// </summary>
    public InterfaceNode? Interface { get; set; }

    /// <summary>
    /// Function declarations
    /// </summary>
    public List<FunctionNode> Functions { get; } = new();

    /// <summary>
    /// Global and component variable declarations
    /// </summary>
    public List<VariableNode> Variables { get; } = new();

    /// <summary>
    /// Constant declarations
    /// </summary>
    public List<ConstantNode> Constants { get; } = new();

    /// <summary>
    /// Main program statements (for non-class programs)
    /// </summary>
    public BlockNode? MainBlock { get; set; }

    /// <summary>
    /// All comments found in the program (both line and block comments)
    /// </summary>
    public List<Lexing.Token> Comments { get; } = new();

    /// <summary>
    /// True if this program defines an application class
    /// </summary>
    public bool IsClassProgram => AppClass != null;

    /// <summary>
    /// True if this program defines an interface
    /// </summary>
    public bool IsInterfaceProgram => Interface != null;


    public List<SourceSpan> SkippedDirectiveSpans { get; set; } = new();

    public ProgramNode()
    {
        // Initialize collections and set up parent relationships
    }

    public void AddImport(ImportNode import)
    {
        Imports.Add(import);
        AddChild(import);
    }

    public void AddFunction(FunctionNode function)
    {
        Functions.Add(function);
        AddChild(function);
    }

    public void AddVariable(VariableNode variable)
    {
        Variables.Add(variable);
        AddChild(variable);
    }

    public void AddConstant(ConstantNode constant)
    {
        Constants.Add(constant);
        AddChild(constant);
    }

    public void AddComment(Lexing.Token comment)
    {
        if (comment.Type.IsCommentType())
        {
            Comments.Add(comment);
        }
    }

    public void SetAppClass(AppClassNode appClass)
    {
        if (Interface != null)
            throw new InvalidOperationException("Program cannot have both class and interface definitions");

        if (AppClass != null)
            RemoveChild(AppClass);

        AppClass = appClass;
        if (appClass != null)
            AddChild(appClass);
    }

    public void SetInterface(InterfaceNode interfaceNode)
    {
        if (AppClass != null)
            throw new InvalidOperationException("Program cannot have both class and interface definitions");

        if (Interface != null)
            RemoveChild(Interface);

        Interface = interfaceNode;
        if (interfaceNode != null)
            AddChild(interfaceNode);
    }

    public void SetMainBlock(BlockNode mainBlock)
    {
        if (MainBlock != null)
            RemoveChild(MainBlock);

        MainBlock = mainBlock;
        if (mainBlock != null)
            AddChild(mainBlock);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitProgram(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitProgram(this);
    }

    public override string ToString()
    {
        if (IsClassProgram)
            return $"Program (Class: {AppClass!.Name})";
        if (IsInterfaceProgram)
            return $"Program (Interface: {Interface!.Name})";
        return "Program";
    }
}

/// <summary>
/// Import declaration node
/// </summary>
public class ImportNode : AstNode
{
    /// <summary>
    /// Package path being imported (e.g., ["MyPackage", "Utilities"] for MyPackage:Utilities:*)
    /// </summary>
    public IReadOnlyList<string> PackagePath { get; }

    /// <summary>
    /// Specific class name being imported, or null for wildcard imports
    /// </summary>
    public string? ClassName { get; }

    /// <summary>
    /// True if this is a wildcard import (package:*)
    /// </summary>
    public bool IsWildcard => ImportedType is AppPackageWildcardTypeNode;

    /// <summary>
    /// Full import path as it appears in source (e.g., "MyPackage:Utilities:*")
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// The imported type node - either AppClassTypeNode for specific class imports or AppPackageWildcardTypeNode for wildcard imports
    /// </summary>
    public TypeNode ImportedType { get; }

    public ImportNode(IEnumerable<string> packagePath, string? className = null)
    {
        var pathList = packagePath?.ToList() ?? throw new ArgumentNullException(nameof(packagePath));
        if (pathList.Count == 0)
            throw new ArgumentException("Package path cannot be empty", nameof(packagePath));

        PackagePath = pathList.AsReadOnly();
        ClassName = className;

        FullPath = string.Join(":", pathList) + (IsWildcard ? ":*" : $":{className}");

        // Create appropriate type node
        if (className == null)
        {
            // Wildcard import
            ImportedType = new AppPackageWildcardTypeNode(pathList);
        }
        else
        {
            // Specific class import
            ImportedType = new AppClassTypeNode(pathList, className);
        }

        AddChild(ImportedType);
    }

    public ImportNode(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentException("Import path cannot be empty", nameof(fullPath));

        FullPath = fullPath;

        var parts = fullPath.Split(':');
        if (parts.Length < 2)
            throw new ArgumentException("Invalid import path format", nameof(fullPath));

        if (parts[^1] == "*")
        {
            // Wildcard import
            PackagePath = parts.Take(parts.Length - 1).ToArray();
            ClassName = null;
            ImportedType = new AppPackageWildcardTypeNode(fullPath);
        }
        else
        {
            // Specific class import
            PackagePath = parts.Take(parts.Length - 1).ToArray();
            ClassName = parts[^1];
            ImportedType = new AppClassTypeNode(fullPath);
        }

        AddChild(ImportedType);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitImport(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitImport(this);
    }

    public override string ToString()
    {
        return $"import {FullPath}";
    }
}

/// <summary>
/// Application class definition node
/// </summary>
public class AppClassNode : AstNode
{
    /// <summary>
    /// Class name
    /// </summary>
    public string Name { get; }

    public Token NameToken { get; }

    /// <summary>
    /// Token for the 'protected' keyword, if present
    /// </summary>
    public Token? ProtectedToken { get; set; }

    /// <summary>
    /// Token for the 'private' keyword, if present
    /// </summary>
    public Token? PrivateToken { get; set; }

    /// <summary>
    /// Base class type (for EXTENDS clause), null if no base class
    /// </summary>
    public TypeNode? BaseClass { get; set; }

    /// <summary>
    /// Implemented interface type (for IMPLEMENTS clause), null if no interface
    /// </summary>
    public TypeNode? ImplementedInterface { get; set; }

    /// <summary>
    /// Method declarations in the class header
    /// </summary>
    public List<MethodNode> Methods { get; } = new();

    /// <summary>
    /// Property declarations
    /// </summary>
    public List<PropertyNode> Properties { get; } = new();

    /// <summary>
    /// Instance variable declarations
    /// </summary>
    public List<VariableNode> InstanceVariables { get; } = new();

    /// <summary>
    /// Constant declarations
    /// </summary>
    public List<ConstantNode> Constants { get; } = new();

    /// <summary>
    /// Method implementations (outside the class declaration)
    /// </summary>
    public List<MethodNode> MethodImplementations { get; } = new();

    /// <summary>
    /// Property getter implementations
    /// </summary>
    public List<PropertyNode> PropertyGetters { get; } = new();

    /// <summary>
    /// Property setter implementations
    /// </summary>
    public List<PropertyNode> PropertySetters { get; } = new();

    /// <summary>
    /// Visibility sections
    /// </summary>
    public Dictionary<VisibilityModifier, List<AstNode>> VisibilitySections { get; } = new()
    {
        { VisibilityModifier.Public, new List<AstNode>() },
        { VisibilityModifier.Protected, new List<AstNode>() },
        { VisibilityModifier.Private, new List<AstNode>() }
    };

    public AppClassNode(string name, Token nameToken)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NameToken = nameToken;
    }

    public void SetBaseClass(TypeNode baseClass)
    {
        if (BaseClass != null)
            RemoveChild(BaseClass);

        BaseClass = baseClass;
        if (baseClass != null)
            AddChild(baseClass);
    }

    public void SetImplementedInterface(TypeNode implementedInterface)
    {
        if (ImplementedInterface != null)
            RemoveChild(ImplementedInterface);

        ImplementedInterface = implementedInterface;
        if (implementedInterface != null)
            AddChild(implementedInterface);
    }

    public void AddMember(AstNode member, VisibilityModifier visibility = VisibilityModifier.Public)
    {
        VisibilitySections[visibility].Add(member);
        AddChild(member);

        // Also add to specific collections
        switch (member)
        {
            case MethodNode method:
                if (method.IsImplementation)
                    MethodImplementations.Add(method);
                else
                    Methods.Add(method);
                break;
            case PropertyNode property:
                if (property.IsGetter)
                    PropertyGetters.Add(property);
                else if (property.IsSetter)
                    PropertySetters.Add(property);
                else
                    Properties.Add(property);
                break;
            case VariableNode variable:
                InstanceVariables.Add(variable);
                break;
            case ConstantNode constant:
                Constants.Add(constant);
                break;
        }
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitAppClass(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitAppClass(this);
    }

    public override string ToString()
    {
        var extends = BaseClass != null ? $" extends {BaseClass}" : "";
        var implements = ImplementedInterface != null ? $" implements {ImplementedInterface}" : "";
        return $"Class {Name}{extends}{implements}";
    }
}

/// <summary>
/// Interface definition node
/// </summary>
public class InterfaceNode : AstNode
{
    /// <summary>
    /// Interface name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Base interface type (for EXTENDS clause), null if no base interface
    /// </summary>
    public TypeNode? BaseInterface { get; set; }

    /// <summary>
    /// Method signatures in the interface
    /// </summary>
    public List<MethodNode> Methods { get; } = new();

    /// <summary>
    /// Gets the collection of property nodes associated with the current object.
    /// </summary>
    public List<PropertyNode> Properties { get; } = new();

    public InterfaceNode(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public void SetBaseInterface(TypeNode baseInterface)
    {
        if (BaseInterface != null)
            RemoveChild(BaseInterface);

        BaseInterface = baseInterface;
        if (baseInterface != null)
            AddChild(baseInterface);
    }

    public void AddMethod(MethodNode method)
    {
        Methods.Add(method);
        AddChild(method);
    }

    public void AddProperty(PropertyNode property)
    {
        Properties.Add(property);
        AddChild(property);
    }
    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitInterface(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitInterface(this);
    }

    public override string ToString()
    {
        var extends = BaseInterface != null ? $" extends {BaseInterface}" : "";
        return $"Interface {Name}{extends}";
    }
}

/// <summary>
/// Visibility modifiers for class members
/// </summary>
public enum VisibilityModifier
{
    Public,
    Protected,
    Private
}