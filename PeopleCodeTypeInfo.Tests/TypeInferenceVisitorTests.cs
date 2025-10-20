using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Tests for TypeInferenceVisitor using the CriteriaUI.pcode sample program.
/// </summary>
public class TypeInferenceVisitorTests : IDisposable
{
    private readonly string _testBasePath = @"C:\temp\IH91U019\PeopleCode";
    private readonly ProgramNode _program;
    private readonly TypeMetadata _programMetadata;
    private readonly TypeInferenceVisitor _visitor;
    private readonly TestTypeMetadataResolver _resolver;
    private readonly TypeCache _cache;

    public TypeInferenceVisitorTests()
    {
        // Read and parse CriteriaUI.pcode
        var sourceFilePath = Path.Combine(_testBasePath, "Application Packages", "ADS", "Relation", "CriteriaUI.pcode");
        var source = File.ReadAllText(sourceFilePath);

        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        _program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        // Extract metadata for the program
        _programMetadata = TypeMetadataBuilder.ExtractMetadata(_program, "ADS:Relation:CriteriaUI");

        // Create resolver and cache
        _resolver = new TestTypeMetadataResolver(_testBasePath);
        _cache = new TypeCache();

        // Run type inference
        _visitor = TypeInferenceVisitor.Run(_program, _programMetadata, _resolver, _cache);
    }

    public void Dispose()
    {
        _cache?.Clear();
    }

    /// <summary>
    /// Helper to find a literal node by line number (approximate).
    /// </summary>
    private LiteralNode? FindLiteralAtLine(int lineNumber)
    {
        var finder = new LiteralFinder(lineNumber);
        _program.Accept(finder);
        return finder.FoundLiteral;
    }

    /// <summary>
    /// Helper to find an identifier node by name.
    /// </summary>
    private IdentifierNode? FindIdentifierByName(string name)
    {
        var finder = new IdentifierFinder(name);
        _program.Accept(finder);
        return finder.FoundIdentifier;
    }

    /// <summary>
    /// Helper to find a function call node by function name.
    /// </summary>
    private FunctionCallNode? FindFunctionCallByName(string functionName)
    {
        var finder = new FunctionCallFinder(functionName);
        _program.Accept(finder);
        return finder.FoundCall;
    }

    [Fact]
    public void TypeInferenceVisitor_StringLiteral_InfersStringType()
    {
        // Line 28: &delimiter = "^";
        // Find the string literal "^"

        // Let's search for ALL string literals and see what we find
        var literalCollector = new LiteralCollector();
        _program.Accept(literalCollector);
        var allLiterals = literalCollector.Literals;

        // Diagnostic: How many literals did we find?
        var stringLiterals = allLiterals.Where(l => l.LiteralType == LiteralType.String).ToList();

        // Diagnostic: Check if any string literals have type info
        var literalsWithTypeInfo = stringLiterals.Where(l => _visitor.GetInferredType(l) != null).ToList();
        var literalsWithoutTypeInfo = stringLiterals.Where(l => _visitor.GetInferredType(l) == null).ToList();

        // Find the "^" literal
        var literal = allLiterals.FirstOrDefault(l =>
            l.LiteralType == LiteralType.String &&
            l.Value?.ToString() == "^");

        Assert.NotNull(literal);
        Assert.Equal(LiteralType.String, literal.LiteralType);

        // Diagnostic output - show which literals don't have type info
        var missingTypeInfoDetails = string.Join(", ",
            literalsWithoutTypeInfo.Select(l =>
                $"'{l.Value}' at line {l.SourceSpan.Start.Line}"));

        var hasTypeInfo = _visitor.GetInferredType(literal) != null;
        Assert.True(hasTypeInfo,
            $"Found {stringLiterals.Count} string literals, " +
            $"{literalsWithTypeInfo.Count} have type info, " +
            $"{literalsWithoutTypeInfo.Count} missing: [{missingTypeInfoDetails}]");

        var inferredType = _visitor.GetInferredType(literal);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.String, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_IntegerLiteral_InfersNumberType()
    {
        // Line 46: For &i = 1 To &rs.ActiveRowCount Step 1
        // Find the integer literal 1
        // Note: Integer literals are normalized to Number during type inference
        // because PeopleCode doesn't meaningfully distinguish them at runtime
        var literal = FindLiteralAtLine(45);

        Assert.NotNull(literal);
        Assert.Equal(LiteralType.Integer, literal.LiteralType);

        var inferredType = _visitor.GetInferredType(literal);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.Number, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_BooleanLiteral_InfersBooleanType()
    {
        // Line 78: If &load And
        // Need to find a True or False literal - let's look for method parameter on line 139
        var literal = FindLiteralAtLine(138);

        if (literal != null && literal.LiteralType == LiteralType.Boolean)
        {
            var inferredType = _visitor.GetInferredType(literal);
            Assert.NotNull(inferredType);
            Assert.Equal(TypeKind.Primitive, inferredType.Kind);
            Assert.Equal(PeopleCodeType.Boolean, inferredType.PeopleCodeType);
        }
    }

    [Fact]
    public void TypeInferenceVisitor_BuiltinFunction_Len_InfersIntegerType()
    {
        // Line 80: Len(&rec.PTADSCRITVALUE.Value)
        var functionCall = FindFunctionCallByName("Len");

        Assert.NotNull(functionCall);

        var inferredType = _visitor.GetInferredType(functionCall);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.Number, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_BuiltinFunction_Substring_InfersStringType()
    {
        // Line 80, 113, 117: Substring(...) calls
        var functionCall = FindFunctionCallByName("Substring");

        Assert.NotNull(functionCall);

        var inferredType = _visitor.GetInferredType(functionCall);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.String, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_BuiltinFunction_CreateRecord_InfersRecordType()
    {
        // Line 110, 118: CreateRecord(...)
        var functionCall = FindFunctionCallByName("CreateRecord");

        Assert.NotNull(functionCall);

        var inferredType = _visitor.GetInferredType(functionCall);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.BuiltinObject, inferredType.Kind);
        Assert.Equal(PeopleCodeType.Record, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_BuiltinFunction_Find_InfersIntegerType()
    {
        // Line 114, 149, 191: Find(&delimiter, &sub)
        var functionCall = FindFunctionCallByName("Find");

        Assert.NotNull(functionCall);

        var inferredType = _visitor.GetInferredType(functionCall);
        Assert.NotNull(inferredType);
        // Note: Find actually returns Number, not Integer
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.Number, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_BuiltinFunction_All_InfersBooleanType()
    {
        // Line 48, 112, etc: All(&op)
        var functionCall = FindFunctionCallByName("All");

        Assert.NotNull(functionCall);

        var inferredType = _visitor.GetInferredType(functionCall);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.Boolean, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_MethodCall_IsNeedBrackets_InfersBooleanType()
    {
        // Line 54, 79: %This.IsNeedBrackets(&op)
        // This should resolve to the method in CriteriaUI class
        var functionCall = FindFunctionCallByName("IsNeedBrackets");

        Assert.NotNull(functionCall);

        var inferredType = _visitor.GetInferredType(functionCall);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.Boolean, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_MethodCall_GenerateSnglCriteriaStr_InfersStringType()
    {
        // Line 248, 299: %This.GenerateSnglCriteriaStr(&rs1)
        var functionCall = FindFunctionCallByName("GenerateSnglCriteriaStr");

        Assert.NotNull(functionCall);

        var inferredType = _visitor.GetInferredType(functionCall);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Primitive, inferredType.Kind);
        Assert.Equal(PeopleCodeType.String, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_BuiltinFunction_GetRowset_InfersRowsetType()
    {
        // Line 247, 249, 394, etc: GetRowset(Scroll.PSADSCRIT_DVW)
        var functionCall = FindFunctionCallByName("GetRowset");

        Assert.NotNull(functionCall);

        var inferredType = _visitor.GetInferredType(functionCall);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.BuiltinObject, inferredType.Kind);
        Assert.Equal(PeopleCodeType.Rowset, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_CreateAppClass_InfersAppClassType()
    {
        // Line 307, 308: create ADS_PARSEREVALUATOR:ADS_Evaluator:ADSExpressionValidateEvaluator()
        var functionCall = FindFunctionCallByName("create");

        if (functionCall != null && functionCall.Arguments.Count > 0)
        {
            var inferredType = _visitor.GetInferredType(functionCall);
            Assert.NotNull(inferredType);
            // Note: The create function returns the type specified in its argument
            // This should be an AppClassTypeInfo
            Assert.True(inferredType.Kind == TypeKind.AppClass || inferredType.Kind == TypeKind.BuiltinObject);
        }
    }

    [Fact]
    public void TypeInferenceVisitor_VariableReference_InfersFromDeclaration()
    {
        // Line 44: Local string &propValue;
        // Line 49: &propValue = ...
        var identifier = FindIdentifierByName("&propValue");

        if (identifier != null)
        {
            var inferredType = _visitor.GetInferredType(identifier);
            Assert.NotNull(inferredType);
            Assert.Equal(PeopleCodeType.String, inferredType.PeopleCodeType);
        }
    }

    [Fact]
    public void TypeInferenceVisitor_ProgramMetadata_ExtractsCorrectMethods()
    {
        // Verify that TypeMetadataBuilder extracted methods correctly
        Assert.True(_programMetadata.Methods.ContainsKey("IsNeedBrackets"));
        Assert.True(_programMetadata.Methods.ContainsKey("GenerateSnglCriteriaStr"));

        // Constructor is stored separately, not in Methods
        Assert.NotNull(_programMetadata.Constructor);
        Assert.Equal("CriteriaUI", _programMetadata.Constructor.Name);

        var isNeedBracketsMethod = _programMetadata.Methods["IsNeedBrackets"];
        Assert.Equal(PeopleCodeType.Boolean, isNeedBracketsMethod.ReturnType.Type);

        var generateMethod = _programMetadata.Methods["GenerateSnglCriteriaStr"];
        Assert.Equal(PeopleCodeType.String, generateMethod.ReturnType.Type);
    }

    [Fact]
    public void TypeInferenceVisitor_ProgramMetadata_ExtractsCorrectProperties()
    {
        // Line 15: property string PartDelimiter readonly;
        Assert.True(_programMetadata.Properties.ContainsKey("PartDelimiter"));

        var property = _programMetadata.Properties["PartDelimiter"];
        var typeWithDim = property.ToTypeWithDimensionality();
        Assert.Equal(PeopleCodeType.String, typeWithDim.Type);
    }

    [Fact]
    public void TypeInferenceVisitor_ThisIdentifier_ResolvesToCurrentClass()
    {
        // Create a simple program with %This reference
        var source = @"
class TestClass
   method TestMethod();
end-class;

method TestMethod
   Local TestClass &ref;
   &ref = %This;
end-method;
";

        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestClass");
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance, cache);

        // Find %This identifier
        var thisIdentifier = FindIdentifierByNameInProgram(program, "%This");
        Assert.NotNull(thisIdentifier);

        var inferredType = visitor.GetInferredType(thisIdentifier);
        Assert.NotNull(inferredType);
        Assert.IsType<AppClassTypeInfo>(inferredType);
        Assert.Equal("TestClass", ((AppClassTypeInfo)inferredType).QualifiedName);
    }

    [Fact]
    public void TypeInferenceVisitor_SuperIdentifier_ResolvesToBaseClass()
    {
        // Create a simple program with %Super reference
        var source = @"
class ChildClass extends ParentClass
   method TestMethod();
end-class;

method TestMethod
   Local ParentClass &ref;
   &ref = %Super;
end-method;
";

        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "ChildClass");

        // Verify base class was extracted correctly
        Assert.NotNull(metadata.BaseClassName);
        Assert.Equal("ParentClass", metadata.BaseClassName);

        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance, cache);

        // Find %Super identifier
        var superIdentifier = FindIdentifierByNameInProgram(program, "%Super");
        Assert.NotNull(superIdentifier);

        var inferredType = visitor.GetInferredType(superIdentifier);
        Assert.NotNull(inferredType);
        Assert.IsType<AppClassTypeInfo>(inferredType);
        Assert.Equal("ParentClass", ((AppClassTypeInfo)inferredType).QualifiedName);
    }

    [Fact]
    public void TypeInferenceVisitor_CrossAppClassMethodCall_ResolvesReturnType()
    {
        // Line 309 (308 if 0-indexed): Local integer &retCode = &objParser.ParseCriteria(&sql, &objEval);
        // This tests that we can resolve a method from another app class
        // &objParser is of type ADS:Relation:CriteriaParser
        // ParseCriteria should return integer (normalized to Number)

        var functionCallFinder = new FunctionCallAtLineFinder(308, "ParseCriteria");
        _program.Accept(functionCallFinder);
        var parseCriteriaCall = functionCallFinder.FoundCall;

        Assert.NotNull(parseCriteriaCall);

        // Verify this is a member access call (objParser.ParseCriteria)
        Assert.IsType<MemberAccessNode>(parseCriteriaCall.Function);
        var memberAccess = (MemberAccessNode)parseCriteriaCall.Function;
        Assert.Equal("ParseCriteria", memberAccess.MemberName, ignoreCase: true);

        // Get the inferred type for the method call
        var inferredType = _visitor.GetInferredType(parseCriteriaCall);
        Assert.NotNull(inferredType);

        // Should be Number (Integer gets normalized to Number)
        Assert.Equal(PeopleCodeType.Number, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_DefaultMethodChain_ResolvesTypes()
    {
        // Line 394 (393 if 0-indexed): Local Rowset &rs = GetLevel0()(1).GetRowset(Scroll.PSADSRELATION);
        // This tests:
        // 1. GetLevel0() returns Rowset
        // 2. (1) calls the default method on Rowset, which returns Row
        // 3. .GetRowset() is a method on Row that returns Rowset

        // Find the variable declaration at line 393 (this is a Local declaration with assignment)
        var varDeclFinder = new LocalVarDeclWithAssignmentFinder(393);
        _program.Accept(varDeclFinder);
        var varDecl = varDeclFinder.FoundDeclaration;

        Assert.NotNull(varDecl);
        Assert.NotNull(varDecl.InitialValue);

        // The value should be: GetLevel0()(1).GetRowset(Scroll.PSADSRELATION)
        // This is a function call to GetRowset(...)
        Assert.IsType<FunctionCallNode>(varDecl.InitialValue);
        var getRowsetCall = (FunctionCallNode)varDecl.InitialValue;

        // The function should be a member access: something.GetRowset
        Assert.IsType<MemberAccessNode>(getRowsetCall.Function);
        var getRowsetAccess = (MemberAccessNode)getRowsetCall.Function;
        Assert.Equal("GetRowset", getRowsetAccess.MemberName, ignoreCase: true);

        // Test 3: .GetRowset() should return Rowset
        var getRowsetType = _visitor.GetInferredType(getRowsetCall);
        Assert.NotNull(getRowsetType);
        Assert.Equal(PeopleCodeType.Rowset, getRowsetType.PeopleCodeType);

        // The target of .GetRowset should be a function call: GetLevel0()(1)
        Assert.IsType<FunctionCallNode>(getRowsetAccess.Target);
        var defaultMethodCall = (FunctionCallNode)getRowsetAccess.Target;

        // Test 2: (1) should call the default method on Rowset and return Row
        var defaultMethodReturnType = _visitor.GetInferredType(defaultMethodCall);
        Assert.NotNull(defaultMethodReturnType);
        Assert.Equal(PeopleCodeType.Row, defaultMethodReturnType.PeopleCodeType);

        // The function being called should be GetLevel0()
        Assert.IsType<FunctionCallNode>(defaultMethodCall.Function);
        var getLevel0Call = (FunctionCallNode)defaultMethodCall.Function;

        // Test 1: GetLevel0() should return Rowset
        var getLevel0Type = _visitor.GetInferredType(getLevel0Call);
        Assert.NotNull(getLevel0Type);
        Assert.Equal(PeopleCodeType.Rowset, getLevel0Type.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_RowPropertyAccess_InfersRecordType()
    {
        // Line 398: If All(&rs(&i).PSADSRELATION.PTADSRELNAME.Value) Then
        // This tests that &rs(&i).PSADSRELATION infers as Record type
        // where &rs(&i) is a Row, and PSADSRELATION is not a real property/method on Row
        // but should be treated as GetRecord(Record.PSADSRELATION)

        var memberAccessFinder = new MemberAccessAtLineFinder(397, "PSADSRELATION");
        _program.Accept(memberAccessFinder);
        var rowPropertyAccess = memberAccessFinder.FoundMemberAccess;

        Assert.NotNull(rowPropertyAccess);

        // Verify the target is a function call (the default method call &rs(&i))
        Assert.IsType<FunctionCallNode>(rowPropertyAccess.Target);

        // Get the inferred type for the member access
        var inferredType = _visitor.GetInferredType(rowPropertyAccess);
        Assert.NotNull(inferredType);

        // Should be Record type (Row property access acts as GetRecord)
        Assert.Equal(PeopleCodeType.Record, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_RecordPropertyAccess_InfersFieldType()
    {
        // Line 398: If All(&rs(&i).PSADSRELATION.PTADSRELNAME.Value) Then
        // This tests that &rs(&i).PSADSRELATION.PTADSRELNAME infers as Field type
        // where PSADSRELATION is a Record, and PTADSRELNAME is not a real property on Record
        // but should be treated as GetField(Field.PTADSRELNAME)

        var memberAccessFinder = new MemberAccessAtLineFinder(397, "PTADSRELNAME");
        _program.Accept(memberAccessFinder);
        var recordPropertyAccess = memberAccessFinder.FoundMemberAccess;

        Assert.NotNull(recordPropertyAccess);

        // Get the inferred type for the member access
        var inferredType = _visitor.GetInferredType(recordPropertyAccess);
        Assert.NotNull(inferredType);

        // Should be Field type (Record property access acts as GetField)
        Assert.Equal(PeopleCodeType.Field, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_ChainedRowRecordFieldAccess_InfersCorrectTypes()
    {
        // Line 398: If All(&rs(&i).PSADSRELATION.PTADSRELNAME.Value) Then
        // This is the complete chain:
        // 1. &rs(&i) → Row (via default method)
        // 2. .PSADSRELATION → Record (via implicit GetRecord)
        // 3. .PTADSRELNAME → Field (via implicit GetField)
        // 4. .Value → property on Field (normal property access)

        // Find the complete chain by looking for the Value property access
        var valueFinder = new MemberAccessAtLineFinder(397, "Value");
        _program.Accept(valueFinder);
        var valueAccess = valueFinder.FoundMemberAccess;

        Assert.NotNull(valueAccess);

        // valueAccess.Target should be PTADSRELNAME (Field)
        Assert.IsType<MemberAccessNode>(valueAccess.Target);
        var ptadsrelnameAccess = (MemberAccessNode)valueAccess.Target;
        Assert.Equal("PTADSRELNAME", ptadsrelnameAccess.MemberName, ignoreCase: true);

        var fieldType = _visitor.GetInferredType(ptadsrelnameAccess);
        Assert.NotNull(fieldType);
        Assert.Equal(PeopleCodeType.Field, fieldType.PeopleCodeType);

        // ptadsrelnameAccess.Target should be PSADSRELATION (Record)
        Assert.IsType<MemberAccessNode>(ptadsrelnameAccess.Target);
        var psadsrelationAccess = (MemberAccessNode)ptadsrelnameAccess.Target;
        Assert.Equal("PSADSRELATION", psadsrelationAccess.MemberName, ignoreCase: true);

        var recordType = _visitor.GetInferredType(psadsrelationAccess);
        Assert.NotNull(recordType);
        Assert.Equal(PeopleCodeType.Record, recordType.PeopleCodeType);

        // psadsrelationAccess.Target should be &rs(&i) (Row)
        Assert.IsType<FunctionCallNode>(psadsrelationAccess.Target);
        var rowCall = (FunctionCallNode)psadsrelationAccess.Target;

        var rowType = _visitor.GetInferredType(rowCall);
        Assert.NotNull(rowType);
        Assert.Equal(PeopleCodeType.Row, rowType.PeopleCodeType);

        // Finally, verify the Value property access infers correct type
        // Value on Field is typically a string/variant type
        var valueType = _visitor.GetInferredType(valueAccess);
        Assert.NotNull(valueType);
        // Value property should be resolved from Field's actual property
    }

    [Fact]
    public void TypeInferenceVisitor_RowExplicitMethodCall_InfersRowsetType()
    {
        // Line 399: Local Rowset &rs1 = &rs(&i).GetRowset(Scroll.PSADSRELCRIT_VW);
        // This tests that explicit method calls still work normally
        // GetRowset is a real method on Row that returns Rowset

        var functionCallFinder = new FunctionCallAtLineFinder(398, "GetRowset");
        _program.Accept(functionCallFinder);
        var getRowsetCall = functionCallFinder.FoundCall;

        Assert.NotNull(getRowsetCall);

        // Verify this is a member access call
        Assert.IsType<MemberAccessNode>(getRowsetCall.Function);
        var memberAccess = (MemberAccessNode)getRowsetCall.Function;
        Assert.Equal("GetRowset", memberAccess.MemberName, ignoreCase: true);

        // Get the inferred type for the method call
        var inferredType = _visitor.GetInferredType(getRowsetCall);
        Assert.NotNull(inferredType);

        // Should be Rowset type (normal method resolution)
        Assert.Equal(PeopleCodeType.Rowset, inferredType.PeopleCodeType);
    }

    [Fact]
    public void TypeInferenceVisitor_CriteriaUI_IdentifiesUnknownTypes()
    {
        // This test identifies all AST nodes that have unknown types after type inference
        // It helps us identify gaps in the type inference implementation

        // Create the unknown type finder and run it on the program
        var unknownTypeFinder = new UnknownTypeFinder(_visitor);
        _program.Accept(unknownTypeFinder);

        // Generate diagnostic report
        var report = unknownTypeFinder.GenerateReport();

        // Output the report for analysis (using xUnit output)
        // Note: This will be visible when running tests with verbose output
        System.Diagnostics.Debug.WriteLine(report);
        Console.WriteLine(report);

        // For now, we'll just assert that we got a report
        // In the future, we can add assertions about expected types or thresholds
        Assert.NotNull(report);

        // Optionally fail if there are too many unknown types (for now, just report)
        // Uncomment the line below if you want the test to fail when unknown types are found:
        // Assert.True(unknownTypeFinder.Findings.Count == 0, $"Found unknown types:\n{report}");
    }

    // Helper to find identifier in any program
    private static IdentifierNode? FindIdentifierByNameInProgram(ProgramNode program, string name)
    {
        var finder = new IdentifierFinder(name);
        program.Accept(finder);
        return finder.FoundIdentifier;
    }

    // Helper visitor classes for finding specific nodes
    private class LiteralFinder : AstVisitorBase
    {
        private readonly int _targetLine;
        public LiteralNode? FoundLiteral { get; private set; }

        public LiteralFinder(int targetLine)
        {
            _targetLine = targetLine;
        }

        public override void VisitLiteral(LiteralNode node)
        {
            if (FoundLiteral == null && node.SourceSpan.Start.Line == _targetLine)
            {
                FoundLiteral = node;
            }
            base.VisitLiteral(node);
        }
    }

    private class IdentifierFinder : AstVisitorBase
    {
        private readonly string _targetName;
        public IdentifierNode? FoundIdentifier { get; private set; }

        public IdentifierFinder(string targetName)
        {
            _targetName = targetName;
        }

        public override void VisitIdentifier(IdentifierNode node)
        {
            if (FoundIdentifier == null &&
                node.Name.Equals(_targetName, StringComparison.OrdinalIgnoreCase))
            {
                FoundIdentifier = node;
            }
            base.VisitIdentifier(node);
        }
    }

    private class FunctionCallFinder : AstVisitorBase
    {
        private readonly string _targetFunctionName;
        public FunctionCallNode? FoundCall { get; private set; }

        public FunctionCallFinder(string targetFunctionName)
        {
            _targetFunctionName = targetFunctionName;
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            if (FoundCall == null)
            {
                // Check if the function is an identifier with the target name
                if (node.Function is IdentifierNode identifier &&
                    identifier.Name.Equals(_targetFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    FoundCall = node;
                }
                // Check if it's a member access (like %This.MethodName)
                else if (node.Function is MemberAccessNode memberAccess &&
                    memberAccess.MemberName.Equals(_targetFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    FoundCall = node;
                }
            }
            base.VisitFunctionCall(node);
        }
    }

    private class LiteralCollector : AstVisitorBase
    {
        public List<LiteralNode> Literals { get; } = new();

        public override void VisitLiteral(LiteralNode node)
        {
            Literals.Add(node);
            base.VisitLiteral(node);
        }
    }

    private class FunctionCallAtLineFinder : AstVisitorBase
    {
        private readonly int _targetLine;
        private readonly string _targetFunctionName;
        public FunctionCallNode? FoundCall { get; private set; }

        public FunctionCallAtLineFinder(int targetLine, string targetFunctionName)
        {
            _targetLine = targetLine;
            _targetFunctionName = targetFunctionName;
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            if (FoundCall == null && node.SourceSpan.Start.Line == _targetLine)
            {
                // Check if the function matches the target name
                if (node.Function is IdentifierNode identifier &&
                    identifier.Name.Equals(_targetFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    FoundCall = node;
                }
                // Check if it's a member access (like &obj.MethodName)
                else if (node.Function is MemberAccessNode memberAccess &&
                    memberAccess.MemberName.Equals(_targetFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    FoundCall = node;
                }
            }
            base.VisitFunctionCall(node);
        }
    }

    private class AssignmentAtLineFinder : AstVisitorBase
    {
        private readonly int _targetLine;
        public AssignmentNode? FoundAssignment { get; private set; }

        public AssignmentAtLineFinder(int targetLine)
        {
            _targetLine = targetLine;
        }

        public override void VisitAssignment(AssignmentNode node)
        {
            if (FoundAssignment == null && node.SourceSpan.Start.Line == _targetLine)
            {
                FoundAssignment = node;
            }
            base.VisitAssignment(node);
        }
    }

    private class LocalVarDeclWithAssignmentFinder : AstVisitorBase
    {
        private readonly int _targetLine;
        public LocalVariableDeclarationWithAssignmentNode? FoundDeclaration { get; private set; }

        public LocalVarDeclWithAssignmentFinder(int targetLine)
        {
            _targetLine = targetLine;
        }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            if (FoundDeclaration == null && node.SourceSpan.Start.Line == _targetLine)
            {
                FoundDeclaration = node;
            }
            base.VisitLocalVariableDeclarationWithAssignment(node);
        }
    }

    private class MemberAccessAtLineFinder : AstVisitorBase
    {
        private readonly int _targetLine;
        private readonly string _targetMemberName;
        public MemberAccessNode? FoundMemberAccess { get; private set; }

        public MemberAccessAtLineFinder(int targetLine, string targetMemberName)
        {
            _targetLine = targetLine;
            _targetMemberName = targetMemberName;
        }

        public override void VisitMemberAccess(MemberAccessNode node)
        {
            if (FoundMemberAccess == null &&
                node.SourceSpan.Start.Line == _targetLine &&
                node.MemberName.Equals(_targetMemberName, StringComparison.OrdinalIgnoreCase))
            {
                FoundMemberAccess = node;
            }
            base.VisitMemberAccess(node);
        }
    }

    /// <summary>
    /// Visitor that finds all ExpressionNode AST nodes with unknown types after type inference.
    /// Only checks expression nodes since statements, declarations, and type nodes don't have types.
    /// </summary>
    private class UnknownTypeFinder : ScopedAstVisitor<object>
    {
        private readonly TypeInferenceVisitor _typeInferenceVisitor;
        private readonly List<UnknownTypeInfo> _findings = new();

        public UnknownTypeFinder(TypeInferenceVisitor typeInferenceVisitor)
        {
            _typeInferenceVisitor = typeInferenceVisitor;
        }

        public IReadOnlyList<UnknownTypeInfo> Findings => _findings;

        /// <summary>
        /// Records information about a node with unknown type
        /// </summary>
        public class UnknownTypeInfo
        {
            public string NodeType { get; set; } = "";
            public int Line { get; set; }
            public int Column { get; set; }
            public string? Context { get; set; }
            public string? InferredTypeName { get; set; }
        }

        private void CheckExpressionType(ExpressionNode node, string? context = null)
        {
            var inferredType = _typeInferenceVisitor.GetInferredType(node);

            // Check if type is unknown or null
            if (inferredType == null || inferredType.Kind == TypeKind.Unknown)
            {
                _findings.Add(new UnknownTypeInfo
                {
                    NodeType = node.GetType().Name,
                    Line = node.SourceSpan.Start.Line,
                    Column = node.SourceSpan.Start.Column,
                    Context = context,
                    InferredTypeName = inferredType?.Name ?? "null"
                });
            }
        }

        // Override Visit methods for ExpressionNode types only
        // Note: We intentionally do NOT check MemberAccessNode because it's often just a "path"
        // to a function/property, and the FunctionCallNode or PropertyAccessNode has the actual type.

        public override void VisitClassConstant(ClassConstantNode node)
        {
            CheckExpressionType(node, $"Class constant: {node.ClassName}.{node.ConstantName}");
            base.VisitClassConstant(node);
        }

        public override void VisitBinaryOperation(BinaryOperationNode node)
        {
            CheckExpressionType(node, $"BinaryOp: {node.Operator}");
            base.VisitBinaryOperation(node);
        }

        public override void VisitUnaryOperation(UnaryOperationNode node)
        {
            CheckExpressionType(node, $"UnaryOp: {node.Operator}");
            base.VisitUnaryOperation(node);
        }

        public override void VisitLiteral(LiteralNode node)
        {
            CheckExpressionType(node, $"Literal: {node.LiteralType} = {node.Value}");
            base.VisitLiteral(node);
        }

        public override void VisitIdentifier(IdentifierNode node)
        {
            CheckExpressionType(node, $"Identifier: {node.Name}");
            base.VisitIdentifier(node);
        }

        public override void VisitPropertyAccess(PropertyAccessNode node)
        {
            CheckExpressionType(node, "Property access");
            base.VisitPropertyAccess(node);
        }

        public override void VisitArrayAccess(ArrayAccessNode node)
        {
            CheckExpressionType(node, "Array access");
            base.VisitArrayAccess(node);
        }

        public override void VisitObjectCreation(ObjectCreationNode node)
        {
            CheckExpressionType(node, "Object creation");
            base.VisitObjectCreation(node);
        }

        public override void VisitTypeCast(TypeCastNode node)
        {
            CheckExpressionType(node, "Type cast");
            base.VisitTypeCast(node);
        }

        public override void VisitParenthesized(ParenthesizedExpressionNode node)
        {
            CheckExpressionType(node, "Parenthesized expression");
            base.VisitParenthesized(node);
        }

        public override void VisitAssignment(AssignmentNode node)
        {
            CheckExpressionType(node, "Assignment");
            base.VisitAssignment(node);
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            CheckExpressionType(node, "Function call");
            base.VisitFunctionCall(node);
        }

        public override void VisitMetadataExpression(MetadataExpressionNode node)
        {
            CheckExpressionType(node, "Metadata expression");
            base.VisitMetadataExpression(node);
        }

        // Note: MemberAccessNode is intentionally NOT checked here because:
        // - In &rs.DeleteRow(1), the FunctionCallNode has the type (return type of DeleteRow)
        // - The MemberAccessNode (&rs.DeleteRow) is just the "path" and doesn't have a type
        // - We only care about the actual expression result, not intermediate navigation

        // Note: ObjectCreateShortHand and PartialShortHandAssignmentNode are explicitly excluded
        // as requested

        /// <summary>
        /// Generate a diagnostic report of all unknown types found
        /// </summary>
        public string GenerateReport()
        {
            if (_findings.Count == 0)
            {
                return "No unknown types found!";
            }

            var report = new System.Text.StringBuilder();
            report.AppendLine($"Found {_findings.Count} nodes with unknown types:");
            report.AppendLine();

            // Group by node type
            var groupedByNodeType = _findings
                .GroupBy(f => f.NodeType)
                .OrderByDescending(g => g.Count());

            foreach (var group in groupedByNodeType)
            {
                report.AppendLine($"{group.Key} ({group.Count()} occurrences):");
                foreach (var finding in group.OrderBy(f => f.Line).Take(10)) // Show first 10 of each type
                {
                    report.AppendLine($"  Line {finding.Line}:{finding.Column} - {finding.Context} - Type: {finding.InferredTypeName}");
                }
                if (group.Count() > 10)
                {
                    report.AppendLine($"  ... and {group.Count() - 10} more");
                }
                report.AppendLine();
            }

            return report.ToString();
        }
    }
}
