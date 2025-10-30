using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Tests for array access type inference, including single/double indexing and comma syntax.
/// </summary>
public class ArrayAccessInferenceTests : IDisposable
{
    private readonly TypeCache _cache;

    public ArrayAccessInferenceTests()
    {
        _cache = new TypeCache();
    }

    public void Dispose()
    {
        _cache?.Clear();
    }

    /// <summary>
    /// Helper to parse code and run type inference
    /// </summary>
    private (ProgramNode program, TypeInferenceVisitor visitor) ParseAndInfer(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "ArrayAccessTest");
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        return (program, visitor);
    }

    /// <summary>
    /// Helper to find an array access node by looking for assignment to a specific variable
    /// </summary>
    private ArrayAccessNode? FindArrayAccessInAssignmentTo(ProgramNode program, string varName)
    {
        var finder = new ArrayAccessInAssignmentFinder(varName);
        program.Accept(finder);
        return finder.FoundArrayAccess;
    }

    [Fact]
    public void ArrayAccess_SingleIndex_InfersArrayOfString()
    {
        var source = @"
local array of array of string &doubleArray;
local array of string &array = &doubleArray[1];
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the array access: &doubleArray[1]
        var arrayAccess = FindArrayAccessInAssignmentTo(program, "&array");
        Assert.NotNull(arrayAccess);

        var inferredType = arrayAccess.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as array of string (1D array)
        Assert.IsType<ArrayTypeInfo>(inferredType);
        var arrayType = (ArrayTypeInfo)inferredType;
        Assert.Equal(1, arrayType.Dimensions);
        Assert.NotNull(arrayType.ElementType);
        Assert.Equal(PeopleCodeType.String, arrayType.ElementType.PeopleCodeType);

        // Output diagnostic
        Console.WriteLine($"&doubleArray[1] inferred as: {inferredType.Name}");
    }

    [Fact]
    public void ArrayAccess_DoubleIndex_InfersString()
    {
        var source = @"
local array of array of string &doubleArray;
local string &str = &doubleArray[1][1];
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the outermost array access: &doubleArray[1][1]
        var arrayAccess = FindArrayAccessInAssignmentTo(program, "&str");
        Assert.NotNull(arrayAccess);

        var inferredType = arrayAccess.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as string (primitive, not array)
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.String, inferredType.PeopleCodeType);

        // Output diagnostic
        Console.WriteLine($"&doubleArray[1][1] inferred as: {inferredType.Name}");
    }

    [Fact]
    public void ArrayAccess_CommaSyntax_InfersString()
    {
        var source = @"
local array of array of string &doubleArray;
local string &str2 = &doubleArray[1,1];
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the array access with comma syntax: &doubleArray[1,1]
        var arrayAccess = FindArrayAccessInAssignmentTo(program, "&str2");
        Assert.NotNull(arrayAccess);

        var inferredType = arrayAccess.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as string (primitive, not array)
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.String, inferredType.PeopleCodeType);

        // Output diagnostic
        Console.WriteLine($"&doubleArray[1,1] inferred as: {inferredType.Name}");
    }

    [Fact]
    public void ArrayAccess_AutoDeclaredSingleIndex_InfersAny()
    {
        var source = @"
local any &a = &autoDeclared[1];
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the array access: &autoDeclared[1]
        var arrayAccess = FindArrayAccessInAssignmentTo(program, "&a");
        Assert.NotNull(arrayAccess);

        var inferredType = arrayAccess.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as any
        Assert.IsType<AnyTypeInfo>(inferredType);
        Assert.Equal(PeopleCodeType.Any, inferredType.PeopleCodeType);

        // Output diagnostic
        Console.WriteLine($"&autoDeclared[1] inferred as: {inferredType.Name}");
    }

    [Fact]
    public void ArrayAccess_AutoDeclaredDoubleIndex_InfersAny()
    {
        var source = @"
local any &a2 = &autoDeclared[1][1];
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the array access: &autoDeclared[1][1]
        var arrayAccess = FindArrayAccessInAssignmentTo(program, "&a2");
        Assert.NotNull(arrayAccess);

        var inferredType = arrayAccess.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as any
        Assert.IsType<AnyTypeInfo>(inferredType);
        Assert.Equal(PeopleCodeType.Any, inferredType.PeopleCodeType);

        // Output diagnostic
        Console.WriteLine($"&autoDeclared[1][1] inferred as: {inferredType.Name}");
    }

    [Fact]
    public void ArrayAccess_AutoDeclaredCommaSyntax_InfersAny()
    {
        var source = @"
local any &a3 = &autoDeclared[1,1];
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the array access: &autoDeclared[1,1]
        var arrayAccess = FindArrayAccessInAssignmentTo(program, "&a3");
        Assert.NotNull(arrayAccess);

        var inferredType = arrayAccess.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as any
        Assert.IsType<AnyTypeInfo>(inferredType);
        Assert.Equal(PeopleCodeType.Any, inferredType.PeopleCodeType);

        // Output diagnostic
        Console.WriteLine($"&autoDeclared[1,1] inferred as: {inferredType.Name}");
    }

    [Fact]
    public void ArrayAccess_AllCases_Combined()
    {
        var source = @"
local array of array of string &doubleArray;
local array of string &array = &doubleArray[1];
local string &str = &doubleArray[1][1];
local string &str2 = &doubleArray[1,1];

local any &a = &autoDeclared[1];
local any &a2 = &autoDeclared[1][1];
local any &a3 = &autoDeclared[1,1];
";

        var (program, visitor) = ParseAndInfer(source);

        // Test &doubleArray[1] -> array of string
        var array = FindArrayAccessInAssignmentTo(program, "&array");
        Assert.NotNull(array);
        var arrayType = array.GetInferredType();
        Assert.NotNull(arrayType);
        Assert.IsType<ArrayTypeInfo>(arrayType);
        var arrayTypeInfo = (ArrayTypeInfo)arrayType;
        Assert.Equal(1, arrayTypeInfo.Dimensions);
        Assert.NotNull(arrayTypeInfo.ElementType);
        Assert.Equal(PeopleCodeType.String, arrayTypeInfo.ElementType.PeopleCodeType);

        // Test &doubleArray[1][1] -> string
        var str = FindArrayAccessInAssignmentTo(program, "&str");
        Assert.NotNull(str);
        var strType = str.GetInferredType();
        Assert.NotNull(strType);
        Assert.Equal(PeopleCodeType.String, strType.PeopleCodeType);

        // Test &doubleArray[1,1] -> string
        var str2 = FindArrayAccessInAssignmentTo(program, "&str2");
        Assert.NotNull(str2);
        var str2Type = str2.GetInferredType();
        Assert.NotNull(str2Type);
        Assert.Equal(PeopleCodeType.String, str2Type.PeopleCodeType);

        // Test &autoDeclared[1] -> any
        var a = FindArrayAccessInAssignmentTo(program, "&a");
        Assert.NotNull(a);
        var aType = a.GetInferredType();
        Assert.NotNull(aType);
        Assert.IsType<AnyTypeInfo>(aType);

        // Test &autoDeclared[1][1] -> any
        var a2 = FindArrayAccessInAssignmentTo(program, "&a2");
        Assert.NotNull(a2);
        var a2Type = a2.GetInferredType();
        Assert.NotNull(a2Type);
        Assert.IsType<AnyTypeInfo>(a2Type);

        // Test &autoDeclared[1,1] -> any
        var a3 = FindArrayAccessInAssignmentTo(program, "&a3");
        Assert.NotNull(a3);
        var a3Type = a3.GetInferredType();
        Assert.NotNull(a3Type);
        Assert.IsType<AnyTypeInfo>(a3Type);

        // Output diagnostic report
        Console.WriteLine("\n=== Array Access Inference Test Results ===");
        Console.WriteLine($"&doubleArray[1]     -> {arrayType.Name}");
        Console.WriteLine($"&doubleArray[1][1]  -> {strType.Name}");
        Console.WriteLine($"&doubleArray[1,1]   -> {str2Type.Name}");
        Console.WriteLine($"&autoDeclared[1]    -> {aType.Name}");
        Console.WriteLine($"&autoDeclared[1][1] -> {a2Type.Name}");
        Console.WriteLine($"&autoDeclared[1,1]  -> {a3Type.Name}");
    }

    /// <summary>
    /// Visitor that finds an ArrayAccessNode that is assigned to a specific variable
    /// </summary>
    private class ArrayAccessInAssignmentFinder : AstVisitorBase
    {
        private readonly string _targetVarName;
        public ArrayAccessNode? FoundArrayAccess { get; private set; }

        public ArrayAccessInAssignmentFinder(string targetVarName)
        {
            _targetVarName = targetVarName;
        }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            // Check if this is the variable we're looking for
            if (node.VariableName.Equals(_targetVarName, StringComparison.OrdinalIgnoreCase))
            {
                // The initial value should be (or contain) an ArrayAccessNode
                FoundArrayAccess = FindArrayAccessInExpression(node.InitialValue);
            }
            base.VisitLocalVariableDeclarationWithAssignment(node);
        }

        public override void VisitAssignment(AssignmentNode node)
        {
            // Check if the target is the variable we're looking for
            if (node.Target is IdentifierNode identifier &&
                identifier.Name.Equals(_targetVarName, StringComparison.OrdinalIgnoreCase))
            {
                FoundArrayAccess = FindArrayAccessInExpression(node.Value);
            }
            base.VisitAssignment(node);
        }

        private ArrayAccessNode? FindArrayAccessInExpression(ExpressionNode? expr)
        {
            if (expr == null) return null;

            // Direct array access
            if (expr is ArrayAccessNode arrayAccess)
            {
                return arrayAccess;
            }

            // For nested expressions, we want the outermost array access
            // For example, in &doubleArray[1][1], we want the outer [1]
            return expr as ArrayAccessNode;
        }
    }
}
