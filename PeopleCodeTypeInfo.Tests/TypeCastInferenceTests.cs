using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Tests for type cast (AS) expression type inference.
/// Type casts should infer to the target type of the cast.
/// </summary>
public class TypeCastInferenceTests : IDisposable
{
    private readonly TypeCache _cache;

    public TypeCastInferenceTests()
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

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TypeCastTest");
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        return (program, visitor);
    }

    /// <summary>
    /// Helper to find a type cast node in an assignment to a specific variable
    /// </summary>
    private TypeCastNode? FindTypeCastInAssignmentTo(ProgramNode program, string varName)
    {
        var finder = new TypeCastInAssignmentFinder(varName);
        program.Accept(finder);
        return finder.FoundTypeCast;
    }

    [Fact]
    public void TypeCast_ToPrimitiveString_InfersString()
    {
        var source = @"
local any &value = 123;
local string &str = (&value as string);
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the type cast: (&value as string)
        var typeCast = FindTypeCastInAssignmentTo(program, "&str");
        Assert.NotNull(typeCast);

        var inferredType = typeCast.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as string
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.String, inferredType.PeopleCodeType);

        Console.WriteLine($"(&value as string) inferred as: {inferredType.Name}");
    }

    [Fact]
    public void TypeCast_WithoutParentheses_InfersRowset()
    {
        // This is the user's example: type cast WITHOUT parentheses
        var source = @"
local Rowset &r = &autoDeclared as Rowset;
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the type cast: &autoDeclared as Rowset (no parentheses)
        var typeCast = FindTypeCastInAssignmentTo(program, "&r");
        Assert.NotNull(typeCast);

        var inferredType = typeCast.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as Rowset
        Assert.Equal(TypeKind.BuiltinObject, inferredType.Kind);
        Assert.Equal(PeopleCodeType.Rowset, inferredType.PeopleCodeType);

        Console.WriteLine($"&autoDeclared as Rowset inferred as: {inferredType.Name}");
    }

    [Fact]
    public void TypeCast_ToPrimitiveNumber_InfersNumber()
    {
        var source = @"
local any &value = ""123"";
local number &num = (&value as number);
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the type cast: (&value as number)
        var typeCast = FindTypeCastInAssignmentTo(program, "&num");
        Assert.NotNull(typeCast);

        var inferredType = typeCast.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as number
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.Number, inferredType.PeopleCodeType);

        Console.WriteLine($"(&value as number) inferred as: {inferredType.Name}");
    }

    [Fact]
    public void TypeCast_ToBuiltinObject_InfersRecord()
    {
        var source = @"
local any &value;
local Record &rec = (&value as Record);
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the type cast: (&value as Record)
        var typeCast = FindTypeCastInAssignmentTo(program, "&rec");
        Assert.NotNull(typeCast);

        var inferredType = typeCast.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as Record
        Assert.Equal(TypeKind.BuiltinObject, inferredType.Kind);
        Assert.Equal(PeopleCodeType.Record, inferredType.PeopleCodeType);

        Console.WriteLine($"(&value as Record) inferred as: {inferredType.Name}");
    }

    [Fact]
    public void TypeCast_ToAppClass_InfersAppClass()
    {
        var source = @"
local any &value;
local SCM_OM_PRICER:SalesOrderObject:SOLine &soLine = (&value as SCM_OM_PRICER:SalesOrderObject:SOLine);
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the type cast: (&value as SCM_OM_PRICER:SalesOrderObject:SOLine)
        var typeCast = FindTypeCastInAssignmentTo(program, "&soLine");
        Assert.NotNull(typeCast);

        var inferredType = typeCast.GetInferredType();

        // This is the key test - type cast should NOT be null
        Assert.NotNull(inferredType);

        // Should infer as the app class type
        Assert.Equal(TypeKind.AppClass, inferredType.Kind);
        Assert.IsType<AppClassTypeInfo>(inferredType);

        var appClassType = (AppClassTypeInfo)inferredType;
        Assert.Equal("SCM_OM_PRICER:SalesOrderObject:SOLine", appClassType.QualifiedName);

        Console.WriteLine($"(&value as SCM_OM_PRICER:SalesOrderObject:SOLine) inferred as: {inferredType.Name}");
    }

    [Fact]
    public void TypeCast_ArrayAccessWithCast_InfersAppClass()
    {
        // This is the real-world example from the user's code
        var source = @"
local array of array of any &arrSOLine;
local SCM_OM_PRICER:SalesOrderObject:SOLine &soLine = (&arrSOLine[1][2] as SCM_OM_PRICER:SalesOrderObject:SOLine);
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the type cast: (&arrSOLine[1][2] as SCM_OM_PRICER:SalesOrderObject:SOLine)
        var typeCast = FindTypeCastInAssignmentTo(program, "&soLine");
        Assert.NotNull(typeCast);

        var inferredType = typeCast.GetInferredType();

        // This is the failing case the user reported
        Assert.NotNull(inferredType);

        // Should infer as the app class type, NOT the array element type
        Assert.Equal(TypeKind.AppClass, inferredType.Kind);
        Assert.IsType<AppClassTypeInfo>(inferredType);

        var appClassType = (AppClassTypeInfo)inferredType;
        Assert.Equal("SCM_OM_PRICER:SalesOrderObject:SOLine", appClassType.QualifiedName);

        Console.WriteLine($"(&arrSOLine[1][2] as SCM_OM_PRICER:SalesOrderObject:SOLine) inferred as: {inferredType.Name}");
    }

    [Fact]
    public void TypeCast_InFunctionCall_InfersAppClass()
    {
        // This is the exact pattern from the user's code: CreateArray((&expr AS Type))
        var source = @"
local array of array of any &arrSOLine;
local array of any &result = CreateArray((&arrSOLine[1][2] as SCM_OM_PRICER:SalesOrderObject:SOLine));
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the CreateArray function call
        var createArrayFinder = new FunctionCallFinder("CreateArray");
        program.Accept(createArrayFinder);
        var createArrayCall = createArrayFinder.FoundCall;
        Assert.NotNull(createArrayCall);

        // The first argument is a parenthesized expression containing the type cast
        Assert.NotEmpty(createArrayCall.Arguments);
        var firstArg = createArrayCall.Arguments[0];
        Assert.IsType<ParenthesizedExpressionNode>(firstArg);

        var parenExpr = (ParenthesizedExpressionNode)firstArg;
        Assert.IsType<TypeCastNode>(parenExpr.Expression);

        var typeCast = (TypeCastNode)parenExpr.Expression;
        var inferredType = typeCast.GetInferredType();

        // This should NOT be null - this is what's causing the crash
        Assert.NotNull(inferredType);

        // Should infer as the app class type
        Assert.Equal(TypeKind.AppClass, inferredType.Kind);
        Assert.IsType<AppClassTypeInfo>(inferredType);

        var appClassType = (AppClassTypeInfo)inferredType;
        Assert.Equal("SCM_OM_PRICER:SalesOrderObject:SOLine", appClassType.QualifiedName);

        Console.WriteLine($"Type cast in CreateArray() inferred as: {inferredType.Name}");
    }

    [Fact]
    public void TypeCast_ToArrayType_InfersArrayType()
    {
        var source = @"
local any &value;
local array of string &arr = (&value as array of string);
";

        var (program, visitor) = ParseAndInfer(source);

        // Find the type cast: (&value as array of string)
        var typeCast = FindTypeCastInAssignmentTo(program, "&arr");
        Assert.NotNull(typeCast);

        var inferredType = typeCast.GetInferredType();
        Assert.NotNull(inferredType);

        // Should infer as array of string
        Assert.Equal(TypeKind.Array, inferredType.Kind);
        Assert.IsType<ArrayTypeInfo>(inferredType);

        var arrayType = (ArrayTypeInfo)inferredType;
        Assert.Equal(1, arrayType.Dimensions);
        Assert.NotNull(arrayType.ElementType);
        Assert.Equal(PeopleCodeType.String, arrayType.ElementType.PeopleCodeType);

        Console.WriteLine($"(&value as array of string) inferred as: {inferredType.Name}");
    }

    /// <summary>
    /// Visitor that finds a TypeCastNode that is assigned to a specific variable
    /// </summary>
    private class TypeCastInAssignmentFinder : AstVisitorBase
    {
        private readonly string _targetVarName;
        public TypeCastNode? FoundTypeCast { get; private set; }

        public TypeCastInAssignmentFinder(string targetVarName)
        {
            _targetVarName = targetVarName;
        }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            // Check if this is the variable we're looking for
            if (node.VariableName.Equals(_targetVarName, StringComparison.OrdinalIgnoreCase))
            {
                // The initial value should be (or contain) a TypeCastNode
                FoundTypeCast = FindTypeCastInExpression(node.InitialValue);
            }
            base.VisitLocalVariableDeclarationWithAssignment(node);
        }

        public override void VisitAssignment(AssignmentNode node)
        {
            // Check if the target is the variable we're looking for
            if (node.Target is IdentifierNode identifier &&
                identifier.Name.Equals(_targetVarName, StringComparison.OrdinalIgnoreCase))
            {
                FoundTypeCast = FindTypeCastInExpression(node.Value);
            }
            base.VisitAssignment(node);
        }

        private TypeCastNode? FindTypeCastInExpression(ExpressionNode? expr)
        {
            if (expr == null) return null;

            // Direct type cast
            if (expr is TypeCastNode typeCast)
            {
                return typeCast;
            }

            // Type cast inside parentheses: (&value as Type)
            if (expr is ParenthesizedExpressionNode paren && paren.Expression is TypeCastNode parenthesizedCast)
            {
                return parenthesizedCast;
            }

            return null;
        }
    }

    /// <summary>
    /// Visitor to find a function call by name
    /// </summary>
    private class FunctionCallFinder : AstVisitorBase
    {
        private readonly string _functionName;
        public FunctionCallNode? FoundCall { get; private set; }

        public FunctionCallFinder(string functionName)
        {
            _functionName = functionName;
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            if (FoundCall == null && node.Function is IdentifierNode identifier &&
                identifier.Name.Equals(_functionName, StringComparison.OrdinalIgnoreCase))
            {
                FoundCall = node;
            }
            base.VisitFunctionCall(node);
        }
    }
}
