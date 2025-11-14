using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;

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
    /// Application class or interface definition (if this is a class or interface program)
    /// </summary>
    public AppClassNode? AppClass { get; set; }

    /// <summary>
    /// Function declarations
    /// </summary>
    public List<FunctionNode> Functions { get; } = new();

    /// <summary>
    /// Component and global variable declarations (does not include local variables)
    /// </summary>
    public List<ProgramVariableNode> ComponentAndGlobalVariables { get; } = new();

    /// <summary>
    /// Program-level local variable declarations (includes both LocalVariableDeclarationNode and LocalVariableDeclarationWithAssignmentNode)
    /// </summary>
    public List<StatementNode> LocalVariables { get; } = new();

    public List<VariableInfo> AutoDeclaredVariables { get; } = new();

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
    public bool IsClassProgram => AppClass != null && !AppClass.IsInterface;

    /// <summary>
    /// True if this program defines an interface
    /// </summary>
    public bool IsInterfaceProgram => AppClass?.IsInterface ?? false;


    public List<SourceSpan> SkippedDirectiveSpans { get; set; } = new();

    public Dictionary<int, int> StatementNumberMap { get; } = new();

    /// <summary>
    /// Maps line numbers to the first statement node that starts on that line
    /// Used for efficient navigation to statements at specific line numbers
    /// </summary>
    public Dictionary<int, AstNode> LineToStatementMap { get; } = new();

    private int _statementCounter = 0;

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

    public void AddConstant(ConstantNode constant)
    {
        Constants.Add(constant);
        AddChild(constant);
    }

    public void AddComponentAndGlobalVariable(ProgramVariableNode variable)
    {
        ComponentAndGlobalVariables.Add(variable);
        AddChild(variable);
    }

    public void AddLocalVariable(StatementNode variable)
    {
        LocalVariables.Add(variable);
        AddChild(variable);
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
        if (AppClass != null)
            RemoveChild(AppClass);

        AppClass = appClass;
        if (appClass != null)
            AddChild(appClass);
    }

    public void SetInterface(AppClassNode interfaceNode)
    {
        if (AppClass != null)
            RemoveChild(AppClass);

        AppClass = interfaceNode;
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
        if (AppClass != null)
        {
            var typeKind = AppClass.IsInterface ? "Interface" : "Class";
            return $"Program ({typeKind}: {AppClass.Name})";
        }
        return "Program";
    }

    public void SetStatementNumber(int line)
    {
        StatementNumberMap[_statementCounter++] = line;
    }

    public int GetLineForStatement(int statement)
    {
        if (StatementNumberMap.TryGetValue(statement, out int line))
        {
            return line;
        }
        return -1;
    }

    /// <summary>
    /// Gets the first statement node that starts on the specified line number
    /// </summary>
    /// <param name="line">One-based line number</param>
    /// <returns>StatementNode that starts on the specified line, or null if not found</returns>
    public AstNode? GetStatementAtLine(int line)
    {
        if (LineToStatementMap.TryGetValue(line, out AstNode? statement))
        {
            return statement;
        }
        return null;
    }

    /// <summary>
    /// Registers a statement node at a specific line for efficient lookup
    /// This is called during parsing to build the line-to-statement mapping
    /// </summary>
    /// <param name="line">One-based line number where the statement starts</param>
    /// <param name="statement">The statement node that starts on this line</param>
    public void RegisterPPCStatementAtLine(int line, AstNode statement)
    {
        // Only register the first statement on a line to avoid overwriting
        // In cases where multiple statements start on the same line, the first one is typically the most relevant
        if (!LineToStatementMap.ContainsKey(line))
        {
            LineToStatementMap[line] = statement;
        }
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
/// Application class or interface definition node
/// </summary>
public class AppClassNode : AstNode
{
    /// <summary>
    /// Class or interface name
    /// </summary>
    public string Name { get; }

    public Token NameToken { get; }

    /// <summary>
    /// True if this node represents an interface, false if it's a class
    /// </summary>
    public bool IsInterface { get; }

    /// <summary>
    /// Token for the 'protected' keyword, if present (classes only)
    /// </summary>
    public Token? ProtectedToken { get; set; }

    /// <summary>
    /// Token for the 'private' keyword, if present (classes only)
    /// </summary>
    public Token? PrivateToken { get; set; }

    /// <summary>
    /// Base type - can be from EXTENDS or IMPLEMENTS clause
    /// For classes: can extend another class or implement an interface
    /// For interfaces: can extend another interface
    /// Null if no base type
    /// </summary>
    public TypeNode? BaseType { get; set; }

    /// <summary>
    /// Method declarations in the class header
    /// </summary>
    public List<MethodNode> Methods { get; } = new();

    public List<MethodImplNode> OrphanedMethodImpls { get; } = new();

    /// <summary>
    /// Property declarations
    /// </summary>
    public List<PropertyNode> Properties { get; } = new();

    public List<PropertyImplNode> OrphanedPropertyImpls { get; } = new();

    /// <summary>
    /// Instance variable declarations
    /// </summary>
    public List<ProgramVariableNode> InstanceVariables { get; } = new();

    /// <summary>
    /// Constant declarations
    /// </summary>
    public List<ConstantNode> Constants { get; } = new();

    /// <summary>
    /// Method implementations (outside the class declaration)
    /// </summary>
    public List<MethodNode> MethodImplementations { get; } = new();

    /// <summary>
    /// Visibility sections
    /// </summary>
    public Dictionary<VisibilityModifier, List<AstNode>> VisibilitySections { get; } = new()
    {
        { VisibilityModifier.Public, new List<AstNode>() },
        { VisibilityModifier.Protected, new List<AstNode>() },
        { VisibilityModifier.Private, new List<AstNode>() }
    };

    public AppClassNode(string name, Token nameToken, bool isInterface = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NameToken = nameToken;
        IsInterface = isInterface;
    }

    public void SetBaseType(TypeNode baseType)
    {
        if (BaseType != null)
            RemoveChild(BaseType);

        BaseType = baseType;
        if (baseType != null)
            AddChild(baseType);
    }

    public void AddOrphanedMethodImplementation(MethodImplNode methodImplementation)
    {
        OrphanedMethodImpls.Add(methodImplementation);
        AddChild(methodImplementation);
    }

    public void AddOrphanedPropertyImplementation(PropertyImplNode node)
    {
        OrphanedPropertyImpls.Add(node);
        AddChild(node);
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
                Properties.Add(property);
                break;
            case ProgramVariableNode variable:
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
        var typeKind = IsInterface ? "Interface" : "Class";
        var extendsOrImplements = BaseType != null ? $" extends {BaseType}" : "";
        return $"{typeKind} {Name}{extendsOrImplements}";
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