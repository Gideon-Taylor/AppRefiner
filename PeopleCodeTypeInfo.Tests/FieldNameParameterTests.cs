using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using PeopleCodeTypeInfo.Validation;
using System.Text;
using Xunit;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Field-name parameters (signature prefix '*'), verified against the PeopleCode compiler
/// (see docs/architecture/field-slot-compiler-results.md):
/// - accepted: buffer field reference (REC.FIELD, bare FIELD in record PeopleCode), Field.X
/// - rejected ("field name required"): variables, Field objects, other expressions
/// - '*string' additionally requires a buffer reference's data type to be character;
///   Field.X is not data-type checked.
/// Bare REC.FIELD stays typed as its data type and carries a BufferField marker.
/// </summary>
public class FieldNameParameterTests
{
    private sealed class FieldTypeResolver : ITypeMetadataResolver
    {
        private readonly Dictionary<string, TypeInfo> _fieldTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["IS_AT_COMMENT_ID"] = PrimitiveTypeInfo.Number,
            ["OPRID"] = PrimitiveTypeInfo.String,
        };

        protected override TypeMetadata? GetTypeMetadataCore(string qualifiedName) => null;

        protected override Task<TypeMetadata?> GetTypeMetadataCoreAsync(string qualifiedName)
            => Task.FromResult<TypeMetadata?>(null);

        protected override TypeInfo GetFieldTypeCore(string fieldName)
            => _fieldTypes.TryGetValue(fieldName, out var t) ? t : AnyTypeInfo.Instance;

        protected override Task<TypeInfo> GetFieldTypeCoreAsync(string fieldName)
            => Task.FromResult(GetFieldTypeCore(fieldName));

        protected override List<string> GetClassesInPackageCore(string packagePath) => new();
    }

    private static readonly FieldTypeResolver Resolver = new();

    private const string GetNextNumberWithGapsSig =
        "GetNextNumberWithGaps(record_field: *any, max_number: number, increment: number, where_clause: string?, params: any*) -> number";

    private const string SetDBFieldLengthSig = "SetDBFieldLength(field: *string, length: number) -> number";

    private static ProgramNode Infer(string source, string? defaultRecord = null, string? defaultField = null,
        bool inferAutoDeclaredTypes = false)
    {
        var lexer = new PeopleCodeLexer(source);
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(lexer.TokenizeAll());
        var program = parser.ParseProgram();
        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");
        TypeInferenceVisitor.Run(program, metadata, Resolver, defaultRecord, defaultField, inferAutoDeclaredTypes);
        return program;
    }

    private static FunctionCallNode FindCall(ProgramNode program, string name) =>
        program.FindDescendants<FunctionCallNode>().First(fc =>
            (fc.Function is IdentifierNode id && id.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            (fc.Function is MemberAccessNode ma && ma.MemberName.Equals(name, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Validate the first call to the signature's function using inferred argument types.</summary>
    private static ValidationResult ValidateCall(string signature, string body, string? defaultRecord = null)
    {
        var function = SignatureParser.ParseFunctionSignature(signature);
        var program = Infer(body, defaultRecord);
        var call = FindCall(program, function.Name);
        var args = call.Arguments.Select(a => a.GetInferredType() ?? UnknownTypeInfo.Instance).ToArray();
        return new FunctionCallValidator(Resolver).Validate(function, args);
    }

    private static string Wrap(string statements) => $@"
Local any &anyVar, &r;
Local number &testNumber;
Local Field &fld;
{statements}
";

    #region Signature syntax

    [Theory]
    [InlineData("*any", PeopleCodeType.Any)]
    [InlineData("*string", PeopleCodeType.String)]
    public void Parse_StarPrefix_SetsMustBeFieldName(string token, PeopleCodeType expectedType)
    {
        var t = TypeWithDimensionality.Parse(token);

        Assert.True(t.MustBeFieldName);
        Assert.Equal(expectedType, t.Type);
        Assert.False(t.IsReference);
        Assert.False(t.MustBeVariable);
    }

    [Fact]
    public void Parse_WithoutStar_IsNotFieldName()
    {
        Assert.False(TypeWithDimensionality.Parse("field").MustBeFieldName);
    }

    [Fact]
    public void ToString_RoundTripsStarPrefix()
    {
        Assert.Equal("*string", TypeWithDimensionality.Parse("*string").ToString());
    }

    [Fact]
    public void FieldNameFlag_ParticipatesInEquality()
    {
        Assert.NotEqual(TypeWithDimensionality.Parse("*string"), TypeWithDimensionality.Parse("string"));
    }

    [Fact]
    public void SignatureParser_StarPrefixWithRepetition_ParsesAsRepeatedFieldName()
    {
        var function = SignatureParser.ParseFunctionSignature("SetDataYAxis(y_fields: *any+) -> void");

        var variable = Assert.IsType<VariableParameter>(Assert.Single(function.Parameters));
        var single = Assert.IsType<SingleParameter>(variable.InnerParameter);
        Assert.True(single.ParameterType.MustBeFieldName);
        Assert.Equal(1, variable.MinCount);
    }

    [Fact]
    public void Reader_FlagBit0x04_SetsMustBeFieldName()
    {
        // type, array dim, empty app class path, flags, legacy length-prefixed name
        var name = Encoding.UTF8.GetBytes("record_field");
        var bytes = new List<byte> { (byte)PeopleCodeType.Any, 0, 0, 0x04, (byte)name.Length };
        bytes.AddRange(name);
        using var reader = new BinaryReader(new MemoryStream(bytes.ToArray()));

        var param = FunctionReaderStrategy.ReadSingleParameter(reader, nameTable: null);

        Assert.True(param.ParameterType.MustBeFieldName);
        Assert.False(param.ParameterType.IsReference);
        Assert.False(param.ParameterType.MustBeVariable);
    }

    #endregion

    #region Inference: BufferField marker

    [Fact]
    public void QualifiedBufferField_IsDataTypeWithBufferFieldMarker()
    {
        var program = Infer(Wrap("&r = IS_AT_COMMENTS.IS_AT_COMMENT_ID;"));
        var access = program.FindDescendants<MemberAccessNode>().First();

        var type = access.GetInferredType()!;
        Assert.Equal(PeopleCodeType.Number, type.PeopleCodeType);
        Assert.NotNull(type.BufferField);
        Assert.Equal("IS_AT_COMMENTS", type.BufferField!.RecordName);
        Assert.Equal("IS_AT_COMMENT_ID", type.BufferField.FieldName);
    }

    [Fact]
    public void BareFieldInRecordPeopleCode_CarriesBufferFieldMarker()
    {
        var program = Infer(Wrap("&r = IS_AT_COMMENT_ID;"), defaultRecord: "IS_AT_COMMENTS");
        var ident = program.FindDescendants<IdentifierNode>().First(i => i.Name == "IS_AT_COMMENT_ID");

        var type = ident.GetInferredType()!;
        Assert.Equal(PeopleCodeType.Number, type.PeopleCodeType);
        Assert.Equal("IS_AT_COMMENTS", type.BufferField?.RecordName);
        Assert.Equal("IS_AT_COMMENT_ID", type.BufferField?.FieldName);
    }

    [Fact]
    public void Caret_CarriesBufferFieldMarkerForCurrentField()
    {
        var program = Infer(Wrap("&r = ^;"), defaultRecord: "IS_AT_COMMENTS", defaultField: "IS_AT_COMMENT_ID");
        var caret = program.FindDescendants<IdentifierNode>().First(i => i.Name == "^");

        var type = caret.GetInferredType()!;
        Assert.Equal(PeopleCodeType.Number, type.PeopleCodeType);
        Assert.Equal("IS_AT_COMMENT_ID", type.BufferField?.FieldName);
    }

    [Fact]
    public void BufferFieldMarker_DoesNotFlowThroughExpressions()
    {
        var program = Infer(Wrap("&r = IS_AT_COMMENTS.IS_AT_COMMENT_ID + 1;"));
        var binary = program.FindDescendants<BinaryOperationNode>().First();

        Assert.Null(binary.GetInferredType()!.BufferField);
    }

    [Fact]
    public void BufferFieldMarker_DoesNotFlowIntoAutoDeclaredVariable()
    {
        var program = Infer(@"
&x = IS_AT_COMMENTS.IS_AT_COMMENT_ID;
&y = &x;
", inferAutoDeclaredTypes: true);
        var xRead = program.FindDescendants<AssignmentNode>().Last().Value;

        var type = xRead.GetInferredType()!;
        Assert.Equal(PeopleCodeType.Number, type.PeopleCodeType);
        Assert.Null(type.BufferField);
    }

    [Fact]
    public void BufferFieldMarker_SurvivesAssignableWrapping()
    {
        var marked = PrimitiveTypeInfo.Number.WithBufferField(new BufferFieldReference("R", "F"));

        var assignable = marked.WithAssignable(true).WithAutoDeclared(true);

        Assert.Equal("F", assignable.BufferField?.FieldName);
        Assert.Null(PrimitiveTypeInfo.Number.BufferField);
    }

    #endregion

    #region Validation: compiler-verified acceptance rules

    [Fact]
    public void FieldNameSlot_AcceptsBufferField()
    {
        var result = ValidateCall(GetNextNumberWithGapsSig,
            Wrap("&r = GetNextNumberWithGaps(IS_AT_COMMENTS.IS_AT_COMMENT_ID, 999999, 1);"));

        Assert.True(result.IsValid, result.GetDetailedError());
    }

    [Fact]
    public void FieldNameSlot_AcceptsBareFieldInRecordPeopleCode()
    {
        var result = ValidateCall(GetNextNumberWithGapsSig,
            Wrap("&r = GetNextNumberWithGaps(IS_AT_COMMENT_ID, 999999, 1);"), defaultRecord: "IS_AT_COMMENTS");

        Assert.True(result.IsValid, result.GetDetailedError());
    }

    [Fact]
    public void FieldNameSlot_AcceptsFieldDefinitionReference()
    {
        var result = ValidateCall(GetNextNumberWithGapsSig,
            Wrap("&r = GetNextNumberWithGaps(Field.IS_AT_COMMENT_ID, 999999, 1);"));

        Assert.True(result.IsValid, result.GetDetailedError());
    }

    [Theory]
    [InlineData("&testNumber")]
    [InlineData("&anyVar")]
    [InlineData("&fld")]
    [InlineData("1")]
    public void FieldNameSlot_RejectsNonFieldNameWithFieldNameRequired(string argument)
    {
        var result = ValidateCall(GetNextNumberWithGapsSig,
            Wrap($"&r = GetNextNumberWithGaps({argument}, 999999, 1);"));

        Assert.False(result.IsValid);
        Assert.Equal(0, result.FailedAtArgumentIndex);
        Assert.Contains("field name", result.GetDetailedError(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FieldNameSlot_RejectsNonBufferRecordMembers()
    {
        // Record.REC.FIELD and &rec.FIELD are Field objects, not field names
        var result = ValidateCall(GetNextNumberWithGapsSig,
            Wrap("&r = GetNextNumberWithGaps(Record.IS_AT_COMMENTS.IS_AT_COMMENT_ID, 999999, 1);"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void StringFieldNameSlot_AcceptsCharacterBufferField()
    {
        var result = ValidateCall(SetDBFieldLengthSig, Wrap("&r = SetDBFieldLength(PSOPRDEFN.OPRID, 10);"));

        Assert.True(result.IsValid, result.GetDetailedError());
    }

    [Fact]
    public void StringFieldNameSlot_RejectsNumberBufferField()
    {
        var result = ValidateCall(SetDBFieldLengthSig,
            Wrap("&r = SetDBFieldLength(IS_AT_COMMENTS.IS_AT_COMMENT_ID, 10);"));

        Assert.False(result.IsValid);
        Assert.Equal(0, result.FailedAtArgumentIndex);
    }

    [Fact]
    public void StringFieldNameSlot_AcceptsFieldReferenceWithoutDataTypeCheck()
    {
        var result = ValidateCall(SetDBFieldLengthSig, Wrap("&r = SetDBFieldLength(Field.IS_AT_COMMENT_ID, 10);"));

        Assert.True(result.IsValid, result.GetDetailedError());
    }

    [Fact]
    public void StringFieldNameSlot_AcceptsBufferFieldOfUnknownDataType()
    {
        var result = ValidateCall(SetDBFieldLengthSig, Wrap("&r = SetDBFieldLength(SOME_REC.UNKNOWN_FLD, 10);"));

        Assert.True(result.IsValid, result.GetDetailedError());
    }

    [Fact]
    public void StringFieldNameSlot_RejectsStringVariable()
    {
        var result = ValidateCall(SetDBFieldLengthSig, Wrap(@"Local string &s;
&r = SetDBFieldLength(&s, 10);"));

        Assert.False(result.IsValid);
        Assert.Contains("field name", result.GetDetailedError(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepeatedFieldNameSlot_ValidatesEveryRepetition()
    {
        const string sig = "SetDataYAxis(y_fields: *any+) -> void";

        Assert.True(ValidateCall(sig, Wrap(@"Local Chart &chart;
&chart.SetDataYAxis(IS_AT_COMMENTS.IS_AT_COMMENT_ID, PSOPRDEFN.OPRID);")).IsValid);
        Assert.False(ValidateCall(sig, Wrap(@"Local Chart &chart;
&chart.SetDataYAxis(IS_AT_COMMENTS.IS_AT_COMMENT_ID, &fld);")).IsValid);
    }

    [Fact]
    public void UnionWithFieldNameAlternative_AcceptsBufferFieldAndString()
    {
        const string sig = "UnionProbe(x: *any|string) -> void";

        Assert.True(ValidateCall(sig, Wrap("UnionProbe(IS_AT_COMMENTS.IS_AT_COMMENT_ID);")).IsValid);
        Assert.True(ValidateCall(sig, Wrap("UnionProbe(\"text\");")).IsValid);
        Assert.False(ValidateCall(sig, Wrap("UnionProbe(&fld);")).IsValid);
    }

    [Fact]
    public void AllowedNextTypes_UnionFieldNameAlternative_StopsAfterNonFieldName()
    {
        // Autocomplete path (GetAllowedNextTypes) has its own union prefix matcher
        var function = SignatureParser.ParseFunctionSignature("UnionProbe(x: *any|number, y: string) -> void");
        var validator = new FunctionCallValidator(Resolver);
        var bufferArg = PrimitiveTypeInfo.Number.WithBufferField(new BufferFieldReference("R", "F"));
        var fieldObject = new FieldTypeInfo("R", "F", Resolver);

        Assert.NotEmpty(validator.GetAllowedNextTypes(function, [bufferArg]));
        Assert.Empty(validator.GetAllowedNextTypes(function, [fieldObject]));
    }

    [Fact]
    public void FieldObjectSlot_RejectsBufferField()
    {
        // '-- C' group (e.g. SetControlValue): REC.FIELD is never a Field object
        const string sig = "TakesFieldObject(f: field) -> void";

        Assert.False(ValidateCall(sig, Wrap("TakesFieldObject(PSOPRDEFN.OPRID);")).IsValid);
        Assert.True(ValidateCall(sig, Wrap("TakesFieldObject(&fld);")).IsValid);
    }

    #endregion

    #region The original report (lines 1-6), end to end through the type checker

    [Fact]
    public void OriginalReport_OnlyLines3And6Error()
    {
        var program = Infer(@"
local number &commentID = IS_AT_COMMENTS.IS_AT_COMMENT_ID;
local number &commentID2 = IS_AT_COMMENTS.IS_AT_COMMENT_ID.Value;
local field &commentIDField = IS_AT_COMMENTS.IS_AT_COMMENT_ID;
GetNextNumberWithGaps(IS_AT_COMMENTS.IS_AT_COMMENT_ID,999999,1);
local number &testNumber;
GetNextNumberWithGaps(&testNumber,999999,1);
");
        var signature = SignatureParser.ParseFunctionSignature(GetNextNumberWithGapsSig);
        var calls = program.FindDescendants<FunctionCallNode>().ToList();
        Assert.Equal(2, calls.Count);
        foreach (var call in calls)
            call.SetFunctionInfo(signature);

        TypeCheckerVisitor.Run(program, Resolver, Resolver.Cache);

        var declarations = program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>().ToList();
        Assert.Null(declarations[0].GetTypeError());
        Assert.Null(declarations[1].GetTypeError());
        Assert.NotNull(declarations[2].GetTypeError());                // line 3: number into field
        Assert.Null(calls[0].Arguments[0].GetTypeError());              // line 4: buffer field name
        Assert.NotNull(calls[1].Arguments[0].GetTypeError());           // line 6: field name required
        Assert.Contains("field name", calls[1].Arguments[0].GetTypeError()!.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region MARKET is not a definition-reference keyword

    [Fact]
    public void Market_IsNotSpecialReferenceKeyword()
    {
        // The compiler parses Market.GBL as REC.FIELD
        Assert.False(ReferenceTypeInfo.IsSpecialReferenceKeyword("MARKET"));
    }

    #endregion
}
