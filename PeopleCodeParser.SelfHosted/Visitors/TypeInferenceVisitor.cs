using PeopleCodeParser.SelfHosted.Annotations;
using PeopleCodeParser.SelfHosted.ComponentInterfaces;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeParser.SelfHosted.Visitors.Narrowing;
using PeopleCodeParser.SelfHosted.Visitors.Utilities;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using System;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// AST visitor that performs type inference by propagating TypeInfo through the AST.
/// Stores inferred types in node.Attributes["TypeInfo"] for later retrieval.
/// </summary>
/// <remarks>
/// This visitor resolves types from:
/// - Builtin types via PeopleCodeTypeDatabase
/// - Custom types via ITypeMetadataResolver
/// - Local/imported functions from the program AST
///
/// Handles polymorphic return types ($same, $element, etc.) and union types.
/// </remarks>
public class TypeInferenceVisitor : ScopedAstVisitor<object>
{
    private readonly ProgramNode _program;
    private readonly AnnotationIndex _annotations;
    private readonly TypeMetadata _programMetadata;
    private readonly ITypeMetadataResolver _typeResolver;
    private readonly TypeCache _typeCache;
    private readonly string? _defaultRecordName;
    private readonly string? _defaultFieldName;
    private readonly bool _inferAutoDeclaredTypes;
    private Dictionary<string, string> _importMap = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Delegate called when an undefined variable identifier is encountered during type inference.
    /// Allows external systems (like AppRefiner) to provide types for implicit/phantom variables
    /// introduced by language extensions (e.g., &item in &array.Map(&item.Prop)).
    /// </summary>
    /// <param name="node">The identifier node for the undefined variable</param>
    /// <param name="scope">The current scope context where the identifier appears</param>
    /// <param name="registry">The variable registry containing all declared variables</param>
    /// <returns>TypeInfo if the identifier should be typed, null to fall back to default behavior</returns>
    public delegate TypeInfo? UndefinedVariableHandler(IdentifierNode node, ScopeContext? scope, VariableRegistry? registry);

    private readonly UndefinedVariableHandler? _onUndefinedVariable;

    // Usage narrowing pre-pass results (spec §8 "Where it runs"), set by Run before the main pass
    private NarrowingResults? _narrowing;

    private TypeInferenceVisitor(
        ProgramNode program,
        TypeMetadata programMetadata,
        ITypeMetadataResolver typeResolver,
        TypeCache typeCache,
        string? defaultRecordName = null,
        string? defaultFieldName = null,
        bool inferAutoDeclaredTypes = false,
        UndefinedVariableHandler? onUndefinedVariable = null)
    {
        _program = program;
        _annotations = program.Annotations;
        _programMetadata = programMetadata;
        _typeResolver = typeResolver;
        _typeCache = typeCache;
        _defaultRecordName = defaultRecordName;
        _defaultFieldName = defaultFieldName;
        _inferAutoDeclaredTypes = inferAutoDeclaredTypes;
        _onUndefinedVariable = onUndefinedVariable;
    }

    /// <summary>
    /// Run type inference on a program AST
    /// </summary>
    public static TypeInferenceVisitor Run(
        ProgramNode program,
        TypeMetadata programMetadata,
        ITypeMetadataResolver typeResolver,
        string? defaultRecordName = null,
        string? defaultFieldName = null,
        bool inferAutoDeclaredTypes = false,
        UndefinedVariableHandler? onUndefinedVariable = null
        )
    {
        // Stylers re-run inference and checking on the hash-cached tree; drop the previous
        // pass's diagnostics so changed messages don't pile up next to stale ones
        program.ClearTypeDiagnostics();

        var visitor = new TypeInferenceVisitor(
            program,
            programMetadata,
            typeResolver,
            typeResolver.Cache,
            defaultRecordName,
            defaultFieldName,
            inferAutoDeclaredTypes,
            onUndefinedVariable);

        // Pre-resolve all declared functions for efficient lookup
        visitor.ProcessDeclaredFunctions();

        // Build import resolution map for unqualified class names
        visitor.BuildImportMap();

        // Narrowing is syntactic, so it runs first and seeds types the main pass can replace
        visitor._narrowing = UsageNarrowingAnalyzer.Analyze(program);

        program.Accept(visitor);
        return visitor;
    }

    /// <summary>
    /// Pre-processes all declared external functions and caches their FunctionInfo.
    /// This makes the metadata available for both type inference and tooltips.
    /// </summary>
    private void ProcessDeclaredFunctions()
    {
        foreach (var func in _program.Functions.Where(f => f.IsDeclaration))
        {
            try
            {
                // Build qualified name from declaration metadata
                string qualifiedName = $"{func.RecordName}.{func.FieldName}.{func.RecordEvent}";

                // Try cache first, then resolver
                TypeMetadata? sourceMetadata = _typeCache.Get(qualifiedName);
                if (sourceMetadata == null && _typeResolver != null)
                {
                    sourceMetadata = _typeResolver.GetTypeMetadata(qualifiedName);
                    if (sourceMetadata != null)
                        _typeCache.Set(qualifiedName, sourceMetadata);
                }

                // Look up the function in source program's methods
                if (sourceMetadata != null &&
                    sourceMetadata.Methods.TryGetValue(func.Name, out var importedFunc))
                {
                    // Store FunctionInfo on the declaration node for reuse
                    func.SetFunctionInfo(importedFunc);
                }
            }
            catch (Exception)
            {
                // If we can't resolve a declared function, continue processing others
                // The function will be unknown at call sites but won't break the entire inference
                continue;
            }
        }
    }

    /// <summary>
    /// Maps the short class names this program can use to qualified names, then qualifies the
    /// program's own metadata through it, as App Designer does on save.
    /// </summary>
    private void BuildImportMap()
    {
        _importMap = ImportResolution.BuildImportMap(_program, _programMetadata.QualifiedName, _typeResolver);
        ImportResolution.QualifyClassNames(_programMetadata, _importMap);
    }

    /// <summary>
    /// Get the inferred type for any AST node
    /// </summary>
    public TypeInfo? GetInferredType(AstNode node)
    {
        return node.GetInferredType();
    }

    /// <summary>
    /// Store inferred type on an AST node.
    /// Normalizes Integer to Number to match PeopleCode's runtime behavior
    /// where integer and number are essentially the same type.
    /// </summary>
    private void SetInferredType(AstNode node, TypeInfo type)
    {
        // Normalize Integer to Number - PeopleCode doesn't meaningfully distinguish them
        if (type.PeopleCodeType == PeopleCodeType.Integer)
        {
            type = new PrimitiveTypeInfo("number",PeopleCodeType.Number) { IsAssignable = type.IsAssignable }
                .WithBufferField(type.BufferField);
        }

        // Only a bare REC.FIELD / FIELD (optionally parenthesized) is a buffer field reference.
        // Operators, casts, calls ($same_as_first) etc. may hand back an operand's TypeInfo
        // instance — the result is a value, never a field name.
        if (type.BufferField != null &&
            node is not (IdentifierNode or MemberAccessNode or ParenthesizedExpressionNode))
        {
            type = type.WithBufferField(null);
        }

        node.SetInferredType(type);
    }

    /// <summary>
    /// Terminal buffer field access (bare REC.FIELD / bare FIELD in record PeopleCode): the
    /// compiler treats it as the field's value (implicit .Value), and it also satisfies
    /// field-name parameters — so the type is the field data type plus a BufferField marker.
    /// </summary>
    private static TypeInfo BufferFieldValue(FieldTypeInfo field) =>
        field.GetFieldDataType()
            .WithAssignable(true)
            .WithBufferField(new BufferFieldReference(field.RecordName, field.FieldName));

    #region Type Conversion Helpers

    /// <summary>
    /// Convert AST TypeNode to type system TypeInfo
    /// </summary>
    private TypeInfo ConvertTypeNodeToTypeInfo(TypeNode? typeNode)
    {
        if (typeNode == null)
            return UnknownTypeInfo.Instance;

        return typeNode switch
        {
            BuiltInTypeNode builtin => TypeInfo.FromPeopleCodeType(builtin.Type),
            ArrayTypeNode array => new ArrayTypeInfo(
                array.Dimensions,
                array.ElementType != null
                    ? ConvertTypeNodeToTypeInfo(array.ElementType)
                    : TypeInfo.FromPeopleCodeType(PeopleCodeType.Any)),
            AppClassTypeNode appClass => ResolveAppClassType(appClass),
            _ => UnknownTypeInfo.Instance
        };
    }

    /// <summary>
    /// Convert Functions.TypeWithDimensionality to Types.TypeInfo
    /// </summary>
    private TypeInfo ConvertTypeWithDimensionalityToTypeInfo(TypeWithDimensionality twd)
    {
        TypeInfo baseType;
        if (twd.IsAppClass)
            baseType = AppClassTypeInfo.CreateWithInheritanceChain(twd.AppClassPath!, _typeResolver, _typeCache, OwnClassMetadata);
        else if (twd.Type == PeopleCodeType.Record && !string.IsNullOrEmpty(twd.Identity?.RecordName))
            baseType = new RecordTypeInfo(twd.Identity.RecordName, _typeResolver);
        else if (twd.Type == PeopleCodeType.Field && !string.IsNullOrEmpty(twd.Identity?.FieldName))
            baseType = new FieldTypeInfo(twd.Identity.RecordName ?? "", twd.Identity.FieldName, _typeResolver);
        else
            baseType = TypeInfo.FromPeopleCodeType(twd.Type).WithIdentity(twd.Identity);

        return twd.IsArray
            ? new ArrayTypeInfo(twd.ArrayDimensionality, baseType)
            : baseType;
    }

    /// <summary>
    /// Convert FunctionInfo to return TypeInfo (handling polymorphic types)
    /// </summary>
    private TypeInfo ConvertFunctionInfoToTypeInfo(
        FunctionInfo func,
        TypeInfo? objectType,
        TypeInfo[]? parameterTypes,
        IReadOnlyList<ExpressionNode>? argumentNodes = null)
    {
        // A refined return type is authoritative: no union, polymorphic or argument specialization
        if (func.RefinedReturnType is { } refined)
            return ConvertTypeWithDimensionalityToTypeInfo(refined);

        TypeInfo returnType;
        if (func.IsUnionReturn)
        {
            // Return union of all possible types
            returnType = UnionReturnTypeInfo.FromTypeWithDimensionality(func.ReturnUnionTypes!);
        }
        else if (func.IsPolymorphicReturn)
        {
            // Resolve polymorphic type using context
            var resolved = func.ResolveReturnType(objectType, parameterTypes);
            returnType = ConvertTypeWithDimensionalityToTypeInfo(resolved);
        }
        else
        {
            // Simple type
            returnType = ConvertTypeWithDimensionalityToTypeInfo(func.ReturnType);
        }

        // User functions and app class methods return what they declare; argument naming is
        // only meaningful for builtins whose @RECORD / @FIELD parameters select the definition.
        if (!func.IsBuiltin)
            return returnType;

        // Specialize builtin Record/Field returns from @RECORD / @FIELD arguments (and receiver context)
        // e.g. CreateRecord(Record.FOO) → record(FOO), &rec.GetField(Field.BAR) → field(REC.BAR)
        // Also: GetField("OPRID"), CreateRecord(@("Record.NAME"))
        return SpecializeNamedInstanceReturn(returnType, objectType, parameterTypes, argumentNodes);
    }

    /// <summary>
    /// When a function/method returns a bare Record or Field instance, attach definition identity
    /// from reference arguments and/or the receiver so callers get RecordTypeInfo / FieldTypeInfo.
    /// </summary>
    private TypeInfo SpecializeNamedInstanceReturn(
        TypeInfo returnType,
        TypeInfo? objectType,
        TypeInfo[]? parameterTypes,
        IReadOnlyList<ExpressionNode>? argumentNodes = null)
    {
        // Already specialized — keep it
        if (returnType is RecordTypeInfo or FieldTypeInfo)
            return returnType;

        parameterTypes ??= Array.Empty<TypeInfo>();

        if (returnType.PeopleCodeType == PeopleCodeType.Record)
        {
            var recordName = FindRecordNameFromArgs(parameterTypes, argumentNodes)
                ?? (objectType is RecordTypeInfo ort ? ort.RecordName : null);

            // GetRecord() with no args in record PeopleCode → current default record
            if (string.IsNullOrEmpty(recordName) && parameterTypes.Length == 0 && _defaultRecordName != null)
                recordName = _defaultRecordName;

            if (!string.IsNullOrEmpty(recordName))
                return new RecordTypeInfo(recordName, _typeResolver);

            return returnType;
        }

        if (returnType.PeopleCodeType == PeopleCodeType.Field)
        {
            var fieldName = FindFieldNameFromArgs(parameterTypes, argumentNodes);
            if (string.IsNullOrEmpty(fieldName))
                return returnType;

            var recordName = FindRecordNameFromArgs(parameterTypes, argumentNodes)
                ?? (objectType is RecordTypeInfo ort ? ort.RecordName : null)
                ?? _defaultRecordName
                ?? string.Empty;

            return new FieldTypeInfo(recordName, fieldName, _typeResolver).WithAssignable(true);
        }

        // GetCompIntfc(CompIntfc.FOO) → ComponentInterface named FOO. Tagged whatever the lookup
        // says, so identity checks work without a database; members need the definition.
        if (returnType.PeopleCodeType == PeopleCodeType.ComponentInterface && returnType.Identity == null)
        {
            var ciName = FindCompIntfcNameFromArgs(parameterTypes, argumentNodes);
            return string.IsNullOrEmpty(ciName)
                ? returnType
                : returnType.WithIdentity(new TypeIdentity(CompIntfcName: ciName));
        }

        // &coll.Item(n) (ItemByKeys, CurrentItem, GetEffectiveItem, InsertItem) → an item of
        // the same collection path
        if (returnType.PeopleCodeType == PeopleCodeType.DataItem && returnType.Identity == null
            && objectType?.PeopleCodeType == PeopleCodeType.DataCollection
            && !string.IsNullOrEmpty(objectType.Identity?.CompIntfcName))
        {
            return returnType.WithIdentity(objectType.Identity);
        }

        return returnType;
    }

    private static string? FindCompIntfcNameFromArgs(
        TypeInfo[] parameterTypes,
        IReadOnlyList<ExpressionNode>? argumentNodes = null)
    {
        foreach (var p in parameterTypes)
        {
            if (p is ReferenceTypeInfo r
                && r.ReferenceCategory == PeopleCodeType.CompIntfc
                && !string.IsNullOrEmpty(r.ReferencedName)
                && !IsUnresolvedDynamicReference(r))
            {
                return r.ReferencedName;
            }
        }

        // @("CompIntfc.NAME") from the AST when the type path missed it
        if (argumentNodes != null)
        {
            foreach (var arg in argumentNodes)
            {
                if (TryGetDefinitionReferenceFromExpression(arg, out var category, out var name)
                    && category == PeopleCodeType.CompIntfc)
                {
                    return name;
                }
            }
        }

        return null;
    }

    private static string? FindRecordNameFromArgs(
        TypeInfo[] parameterTypes,
        IReadOnlyList<ExpressionNode>? argumentNodes = null)
    {
        foreach (var p in parameterTypes)
        {
            if (p is ReferenceTypeInfo r
                && (r.ReferenceCategory == PeopleCodeType.Record || r.ReferenceCategory == PeopleCodeType.Scroll)
                && !string.IsNullOrEmpty(r.ReferencedName)
                && !IsUnresolvedDynamicReference(r))
            {
                return r.ReferencedName;
            }

            // Already a named record instance passed through
            if (p is RecordTypeInfo rt && !string.IsNullOrEmpty(rt.RecordName))
                return rt.RecordName;
        }

        // Fallback: parse string literals / @("Record.NAME") from AST when type path missed them
        if (argumentNodes != null)
        {
            foreach (var arg in argumentNodes)
            {
                if (TryGetDefinitionReferenceFromExpression(arg, out var category, out var name)
                    && (category == PeopleCodeType.Record || category == PeopleCodeType.Scroll))
                {
                    return name;
                }
            }
        }

        return null;
    }

    private static string? FindFieldNameFromArgs(
        TypeInfo[] parameterTypes,
        IReadOnlyList<ExpressionNode>? argumentNodes = null)
    {
        foreach (var p in parameterTypes)
        {
            if (p is ReferenceTypeInfo r
                && r.ReferenceCategory == PeopleCodeType.Field
                && !string.IsNullOrEmpty(r.ReferencedName)
                && !IsUnresolvedDynamicReference(r))
            {
                return r.ReferencedName;
            }

            if (p is FieldTypeInfo ft && !string.IsNullOrEmpty(ft.FieldName))
                return ft.FieldName;
        }

        // GetField accepts a string field name: GetField("OPRID")
        if (argumentNodes != null)
        {
            foreach (var arg in argumentNodes)
            {
                if (TryGetDefinitionReferenceFromExpression(arg, out var category, out var name)
                    && category == PeopleCodeType.Field)
                {
                    return name;
                }

                // Plain string literal field name (not CATEGORY.NAME form)
                if (TryGetStringLiteral(arg, out var lit)
                    && !string.IsNullOrWhiteSpace(lit)
                    && lit.IndexOf('.') < 0)
                {
                    return lit;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// True for the catch-all dynamic @ reference produced when the target cannot be resolved statically.
    /// </summary>
    private static bool IsUnresolvedDynamicReference(ReferenceTypeInfo r) =>
        r.ReferenceCategory == PeopleCodeType.Any
        || string.Equals(r.ReferencedName, "Dynamic Reference", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Strip parenthesized wrappers so @("Record.X") sees the inner literal.
    /// </summary>
    private static ExpressionNode UnwrapParentheses(ExpressionNode expr)
    {
        while (expr is ParenthesizedExpressionNode paren)
            expr = paren.Expression;
        return expr;
    }

    /// <summary>
    /// Extract a pure string literal value from an expression (no concatenation).
    /// Parentheses are ignored: @("Record.X") and @"Record.X" both work.
    /// </summary>
    private static bool TryGetStringLiteral(ExpressionNode expr, out string value)
    {
        expr = UnwrapParentheses(expr);

        if (expr is LiteralNode lit && lit.LiteralType == LiteralType.String && lit.Value != null)
        {
            value = lit.Value.ToString() ?? string.Empty;
            return value.Length > 0;
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Parse a definition-reference string like "Record.MY_REC" or "Field.OPRID".
    /// Category must be a known special-reference keyword.
    /// </summary>
    private static bool TryParseDefinitionReferenceString(
        string text,
        out PeopleCodeType category,
        out string referencedName)
    {
        category = PeopleCodeType.Any;
        referencedName = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        var dot = text.IndexOf('.');
        if (dot <= 0 || dot >= text.Length - 1)
            return false;

        var categoryName = text.Substring(0, dot).Trim();
        var name = text.Substring(dot + 1).Trim();
        if (string.IsNullOrEmpty(categoryName) || string.IsNullOrEmpty(name))
            return false;

        // Reject multi-segment or concatenated leftovers
        if (name.IndexOf('.') >= 0)
            return false;

        if (!ReferenceTypeInfo.IsSpecialReferenceKeyword(categoryName))
            return false;

        category = ReferenceTypeInfo.GetReferenceCategoryType(categoryName);
        referencedName = name;
        return true;
    }

    /// <summary>
    /// Resolve a static definition reference from:
    /// - @("Record.NAME") / @"Field.OPRID" (unary @ on a pure string literal)
    /// - plain "Record.NAME" string (rare, but same parse)
    /// Dynamic forms like @("Record." | &var) intentionally fail.
    /// </summary>
    private static bool TryGetDefinitionReferenceFromExpression(
        ExpressionNode expr,
        out PeopleCodeType category,
        out string referencedName)
    {
        category = PeopleCodeType.Any;
        referencedName = string.Empty;

        // @("Record.NAME") or @"Record.NAME"
        if (expr is UnaryOperationNode unary && unary.Operator == UnaryOperator.Reference)
        {
            if (TryGetStringLiteral(unary.Operand, out var refText)
                && TryParseDefinitionReferenceString(refText, out category, out referencedName))
            {
                return true;
            }

            return false;
        }

        // Bare string "Record.NAME" (or plain field name handled by caller)
        if (TryGetStringLiteral(expr, out var lit)
            && TryParseDefinitionReferenceString(lit, out category, out referencedName))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// If RHS is a specialized type and the variable is declared as a compatible broader type,
    /// store the specialized type so later uses retain identity/members:
    /// - Record/Field named instances when declared as Record/Field/any
    /// - ApiObject subtypes (Session, ComponentInterface, …) when declared as ApiObject/any
    /// Compares against the declared type, so a later assignment re-refines the variable.
    /// <paramref name="sourceNode"/> (declaration or assignment) is recorded as the refinement source.
    /// A <c>/* as T */</c> after the value replaces its type first (<see cref="ApplyAssignmentAnnotation"/>),
    /// and a variable with a declaration annotation goes through <see cref="RefineAnnotatedVariable"/>
    /// instead, which keeps the annotation authoritative.
    /// </summary>
    private void TryRefineVariableFromExpression(string variableName, ExpressionNode? expression, AstNode sourceNode)
    {
        if (expression == null) return;

        var rhsType = GetInferredType(expression);
        if (rhsType == null) return;

        var variable = FindVariable(variableName);
        if (variable == null) return;

        rhsType = ApplyAssignmentAnnotation(variable, rhsType, expression, out var valueConflict);

        if (variable.AnnotatedType is { } annotated)
        {
            // One diagnostic per assignment: the declaration conflict outranks the value conflict
            bool declarationWarned = RefineAnnotatedVariable(variable, annotated, rhsType, expression,
                initializer: ReferenceEquals(sourceNode, variable.DeclarationNode));
            if (valueConflict != null && !declarationWarned)
                expression.AddTypeWarning(new TypeError(valueConflict, expression));
            return;
        }

        if (valueConflict != null)
            expression.AddTypeWarning(new TypeError(valueConflict, expression));

        var declared = ConvertTypeNameToTypeInfo(variable.Type);
        bool declaredIsAny = declared.Kind == TypeKind.Any
            || variable.Type.Equals("any", StringComparison.OrdinalIgnoreCase);

        if (rhsType is RecordTypeInfo
            && (declaredIsAny
                || declared.PeopleCodeType == PeopleCodeType.Record
                || declared is RecordTypeInfo))
        {
            variable.Refine(rhsType.WithAssignable(true), new RefinementSource(RefinementKind.Assignment, sourceNode));
            return;
        }

        if (rhsType is FieldTypeInfo
            && (declaredIsAny
                || declared.PeopleCodeType == PeopleCodeType.Field
                || declared is FieldTypeInfo))
        {
            variable.Refine(rhsType.WithAssignable(true), new RefinementSource(RefinementKind.Assignment, sourceNode));
            return;
        }

        // Local ApiObject &s = %Session → refine to Session for member resolution
        if (rhsType is BuiltinObjectTypeInfo rhsBuiltin
            && PeopleCodeTypeDatabase.IsApiObjectSubtype(rhsBuiltin.Name))
        {
            bool declaredIsApiObject = declared is BuiltinObjectTypeInfo d
                && (d.IsApiObjectBase
                    || d.Name.Equals("ApiObject", StringComparison.OrdinalIgnoreCase)
                    || d.PeopleCodeType == PeopleCodeType.ApiObject);

            if (declaredIsAny || declaredIsApiObject)
            {
                WarnIfAssignmentContradictsNarrowing(variable, rhsBuiltin, expression, sourceNode);
                variable.Refine(rhsType.WithAssignable(true), new RefinementSource(RefinementKind.Assignment, sourceNode));
            }
        }
    }

    /// <summary>
    /// A <c>/* as T */</c> annotation after an assignment's value (spec §7) replaces the value's type
    /// for refining the variable. Unknown and misfit annotations warn on the value and are ignored;
    /// a concrete value of a different type, or conflicting identity, warns and T wins. Only the
    /// type used for refinement changes: the value node keeps its own inferred type.
    /// The conflict warning is returned in <paramref name="conflictWarning"/> rather than recorded,
    /// so the caller can let a declaration-annotation conflict on the same value take its place.
    /// </summary>
    private TypeInfo ApplyAssignmentAnnotation(VariableInfo variable, TypeInfo rhsType, ExpressionNode expression, out string? conflictWarning)
    {
        conflictWarning = null;
        var annotation = _annotations.ForValue(expression);
        if (annotation == null)
            return rhsType;

        if (annotation.Error != null)
        {
            expression.AddTypeWarning(new TypeError(annotation.Error, expression));
            return rhsType;
        }

        var target = annotation.Target!;
        if (!target.Fits(variable.Type))
        {
            expression.AddTypeWarning(new TypeError(MisfitMessage(target, variable), expression));
            return rhsType;
        }

        WarnIfUnknownCompIntfc(target, expression);
        var annotationType = AnnotationToTypeInfo(target);

        // ApiObject/any values are what annotations are for; a value the declared type can't hold
        // is the checker's "Cannot assign" error, not a conflict
        bool concrete = rhsType.Kind is not (TypeKind.Any or TypeKind.Unknown)
            && rhsType is not BuiltinObjectTypeInfo { IsApiObjectBase: true }
            && ConvertTypeNameToTypeInfo(variable.Type).IsAssignableFrom(rhsType);
        if (concrete)
        {
            bool sameType = annotationType is BuiltinObjectTypeInfo annotatedBuiltin && rhsType is BuiltinObjectTypeInfo rhsObject
                ? annotatedBuiltin.Name.Equals(rhsObject.Name, StringComparison.OrdinalIgnoreCase)
                : annotationType.PeopleCodeType == rhsType.PeopleCodeType;

            if (!sameType || (rhsType.EffectiveIdentity?.ConflictsWith(annotationType.EffectiveIdentity) ?? false))
            {
                var annotated = DescribeType(annotationType);
                conflictWarning = $"The value is {DescribeType(rhsType)}, but it is annotated as {annotated}. Using {annotated}.";
            }
        }

        return annotationType;
    }

    /// <summary>
    /// Assignment to a variable with a declaration annotation (spec §7): the annotation stays
    /// authoritative. A same-type value with more identity detail refines it; ApiObject/any values
    /// and same-type values with less or equal detail change nothing; anything else warns.
    /// Returns whether it recorded a warning.
    /// </summary>
    private bool RefineAnnotatedVariable(VariableInfo variable, TypeInfo annotated, TypeInfo rhsType, ExpressionNode expression, bool initializer)
    {
        if (rhsType.Kind is TypeKind.Any or TypeKind.Unknown
            || rhsType is BuiltinObjectTypeInfo { IsApiObjectBase: true })
            return false;

        // Only values that could refine the variable are compared; the checker handles the rest.
        // An any variable holds every value, so there the checker is silent and any value is compared.
        bool refinable = rhsType is RecordTypeInfo or FieldTypeInfo
            || (rhsType is BuiltinObjectTypeInfo rhsBuiltin && PeopleCodeTypeDatabase.IsApiObjectSubtype(rhsBuiltin.Name));
        var declared = ConvertTypeNameToTypeInfo(variable.Type);
        if (!refinable && declared.Kind != TypeKind.Any)
            return false;

        // A value the declared type can't hold is the checker's "Cannot assign" error, not a conflict
        if (!declared.IsAssignableFrom(rhsType))
            return false;

        bool sameType = annotated is BuiltinObjectTypeInfo annotatedBuiltin && rhsType is BuiltinObjectTypeInfo rhsObject
            ? annotatedBuiltin.Name.Equals(rhsObject.Name, StringComparison.OrdinalIgnoreCase)
            : annotated.PeopleCodeType == rhsType.PeopleCodeType;

        var annotatedIdentity = annotated.EffectiveIdentity ?? new TypeIdentity();
        var rhsIdentity = rhsType.EffectiveIdentity ?? new TypeIdentity();

        if (sameType && rhsIdentity.IsRefinementOf(annotatedIdentity))
        {
            // More detail: keep the annotation as the source, but carry the extra identity
            if (!annotatedIdentity.IsRefinementOf(rhsIdentity))
                variable.Refine(rhsType.WithAssignable(true), variable.RefinementSource!);
            return false;
        }

        // Less detail (plain ComponentInterface after CompIntfc.FOO) doesn't erase the annotation
        if (sameType && annotatedIdentity.IsRefinementOf(rhsIdentity))
            return false;

        var annotatedLine = variable.RefinementSource is { } src ? $" (line {src.Line + 1})" : "";
        expression.AddTypeWarning(new TypeError(
            initializer
                ? $"{variable.Name} is annotated as {DescribeType(annotated)} but is initialized with {DescribeType(rhsType)}."
                : $"{variable.Name} is annotated as {DescribeType(annotated)}{annotatedLine} but is assigned {DescribeType(rhsType)} here.",
            expression));
        return true;
    }

    /// <summary>
    /// A type's name for annotation messages, including its identity in annotation syntax
    /// (Record.JOB, Record.JOB.EMPLID, Field.EMPLID, CompIntfc.FOO, CompIntfc.FOO.Coll[]) when it has one.
    /// </summary>
    private static string DescribeType(TypeInfo type)
    {
        switch (type)
        {
            case RecordTypeInfo record when !string.IsNullOrEmpty(record.RecordName):
                return $"Record.{record.RecordName}";
            case FieldTypeInfo field:
                return string.IsNullOrEmpty(field.RecordName)
                    ? $"Field.{field.FieldName}"
                    : $"Record.{field.RecordName}.{field.FieldName}";
        }

        if (type.PeopleCodeType is PeopleCodeType.ComponentInterface or PeopleCodeType.DataItem or PeopleCodeType.DataCollection
            && TypeAnnotationParser.FormatCompIntfc(type.PeopleCodeType, type.Identity) is { } ci)
            return ci;

        return type.Name;
    }

    /// <summary>
    /// Applies the variable's declaration annotation, then seeds a narrowed type if it has none.
    /// </summary>
    protected override void OnVariableDeclared(VariableInfo variable)
    {
        ApplyDeclarationAnnotation(variable);
        SeedNarrowedType(variable);

        // Parameters are registered inside their routine's scope, so they're recorded here
        if (variable.DeclarationNode is ParameterNode parameter && parameter.NameToken != null)
            StampDeclaredName(parameter, variable, parameter.NameToken.SourceSpan);
    }

    /// <summary>
    /// Seeds the type usage narrowing settled on (spec §4 row 3) as the variable's refined type.
    /// Annotated variables, including parameters with an inherited annotation, keep their annotation.
    /// </summary>
    private void SeedNarrowedType(VariableInfo variable)
    {
        if (variable.AnnotatedType != null
            || _narrowing?.For(variable.DeclarationNode, variable.Name) is not { NarrowedType: { } narrowed, DecidingUse: { } decidingUse })
            return;

        variable.Refine(ConvertTypeNameToTypeInfo(narrowed), new RefinementSource(RefinementKind.Narrowing, decidingUse));
    }

    /// <summary>
    /// N4: uses settled a type, then an assignment gives a different API type. The assignment
    /// still wins (latest wins); this is the single diagnostic for it, the checker's one-type
    /// warning skips narrowed targets. Silent when the deciding use follows the assignment.
    /// </summary>
    private static void WarnIfAssignmentContradictsNarrowing(VariableInfo variable, BuiltinObjectTypeInfo assigned, ExpressionNode expression, AstNode sourceNode)
    {
        if (variable.RefinementSource is not { Kind: RefinementKind.Narrowing, Node: MemberAccessNode decidingUse }
            || variable.InferredType is not BuiltinObjectTypeInfo narrowed
            || narrowed.Name.Equals(assigned.Name, StringComparison.OrdinalIgnoreCase)
            // A deciding use after the assignment sees the assigned type, and gets the precise member error
            || decidingUse.SourceSpan.Start.Index > sourceNode.SourceSpan.Start.Index)
            return;

        bool isMethod = decidingUse.Parent is FunctionCallNode call && ReferenceEquals(call.Function, decidingUse);
        var decider = isMethod ? $"{decidingUse.MemberName}()" : decidingUse.MemberName;
        expression.AddTypeWarning(new TypeError(
            $"Uses of {variable.Name} suggest {narrowed.Name} ({decider} on line {decidingUse.SourceSpan.Start.Line + 1}), " +
            $"but it is assigned {assigned.Name} on line {sourceNode.SourceSpan.Start.Line + 1}.",
            expression));
    }

    /// <summary>
    /// Applies a <c>/* as T */</c> annotation written directly after the declaration's type, or one
    /// a parameter inherits from its base declaration. Every variable of a multi-variable
    /// declaration shares the type node, so each is annotated.
    /// </summary>
    private void ApplyDeclarationAnnotation(VariableInfo variable)
    {
        var declaringNode = variable.DeclarationNode;
        var typeNode = declaringNode switch
        {
            ParameterNode p => p.Type,
            ProgramVariableNode pv => pv.Type,
            PropertyNode prop => prop.Type,
            LocalVariableDeclarationNode local => local.Type,
            LocalVariableDeclarationWithAssignmentNode initialized => initialized.Type,
            _ => null
        };
        if (typeNode == null)
            return;

        var annotation = _annotations.ForType(typeNode);
        if (annotation == null)
        {
            if (declaringNode is ParameterNode parameter && InheritedParameterAnnotation(parameter) is { } inherited)
                variable.Annotate(ConvertTypeWithDimensionalityToTypeInfo(inherited), new RefinementSource(RefinementKind.Annotation, declaringNode));
            return;
        }

        if (annotation.Error != null)
        {
            declaringNode.AddTypeWarning(new TypeError(annotation.Error, declaringNode));
            return;
        }

        var target = annotation.Target!;
        if (!target.Fits(variable.Type))
        {
            declaringNode.AddTypeWarning(new TypeError(MisfitMessage(target, variable), declaringNode));
            return;
        }

        WarnIfUnknownCompIntfc(target, declaringNode);
        variable.Annotate(AnnotationToTypeInfo(target), new RefinementSource(RefinementKind.Annotation, declaringNode));
    }

    private static string MisfitMessage(AnnotationTarget target, VariableInfo variable) =>
        MisfitMessage(target, variable.Name, variable.Type);

    private static string MisfitMessage(AnnotationTarget target, string name, string declaredTypeName)
    {
        var article = target.Category == AnnotationCategory.ApiObject ? "an" : "a";
        return $"'as {target.Written}' needs {article} {target.Category} declaration; {name} is {declaredTypeName}.";
    }

    /// <summary>
    /// Reports an unknown or misfit <c>/* as T */</c> annotation on a method header or
    /// <c>Function</c> line return type. The type itself goes into metadata (TypeMetadataBuilder);
    /// only the warning is raised here, on the declaring node.
    /// </summary>
    private void CheckReturnAnnotation(AstNode declaringNode, string name, TypeNode? returnType)
    {
        var annotation = _annotations.ForType(returnType);
        if (annotation == null)
            return;

        if (annotation.Error != null)
        {
            declaringNode.AddTypeWarning(new TypeError(annotation.Error, declaringNode));
            return;
        }

        var declaredTypeName = AstTypeExtractor.GetTypeFromNode(returnType);
        if (!annotation.Target!.Fits(declaredTypeName))
            declaringNode.AddTypeWarning(new TypeError(MisfitMessage(annotation.Target, name, declaredTypeName), declaringNode));
        else
            WarnIfUnknownCompIntfc(annotation.Target, declaringNode);
    }

    /// <summary>
    /// An annotation naming a CI that doesn't exist, or a collection path not in the CI, warns
    /// (like an unknown type) on the node its other annotation warnings go on. The annotation
    /// still applies. Silent when the lookup doesn't answer (no database, error).
    /// </summary>
    private void WarnIfUnknownCompIntfc(AnnotationTarget target, AstNode warnOn)
    {
        if (CompIntfcTarget.AnnotationWarning(target.Identity, _typeResolver) is { } warning)
            warnOn.AddTypeWarning(new TypeError(warning, warnOn));
    }

    private TypeInfo AnnotationToTypeInfo(AnnotationTarget target)
    {
        var identity = target.Identity;
        if (target.Category == AnnotationCategory.Record && !string.IsNullOrEmpty(identity?.RecordName))
            return new RecordTypeInfo(identity.RecordName, _typeResolver);

        if (target.Category == AnnotationCategory.Field && !string.IsNullOrEmpty(identity?.FieldName))
            return new FieldTypeInfo(identity.RecordName ?? "", identity.FieldName, _typeResolver);

        return TypeInfo.FromPeopleCodeType(target.Type).WithIdentity(identity);
    }

    /// <summary>
    /// Convert PropertyInfo to TypeInfo
    /// </summary>
    private TypeInfo ConvertPropertyInfoToTypeInfo(PropertyInfo prop)
    {
        return ConvertTypeWithDimensionalityToTypeInfo(prop.RefinedType ?? prop.ToTypeWithDimensionality());
    }

    /// <summary>
    /// Convert a type name string to TypeInfo
    /// </summary>
    private TypeInfo ConvertTypeNameToTypeInfo(string typeName)
    {
        // Handle "any" type (used for auto-declared variables)
        if (typeName.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return AnyTypeInfo.Instance;
        }

        // Handle array types - look for "array of" pattern
        if (typeName.ToLowerInvariant().Contains("array of"))
        {
            // Parse array type (e.g., "array of string", "array of array of number")
            var parts = typeName.Split(new[] { " of " }, StringSplitOptions.RemoveEmptyEntries);
            int dimensions = 0;
            foreach (var part in parts)
            {
                if (part.Trim().ToLowerInvariant() == "array")
                    dimensions++;
            }
            var elementTypeName = "any";

            if (parts.Length > dimensions)
            {
                /* they used something like "array of array" which has an implicit "any" */
                elementTypeName = parts[^1].Trim();
            }

            var elementType = ConvertTypeNameToTypeInfo(elementTypeName);

            return new ArrayTypeInfo(dimensions, elementType);
        }

        if (typeName.ToLowerInvariant().Equals("array"))
        {
            return new ArrayTypeInfo(1, AnyTypeInfo.Instance);
        }

        // Builtin enum types, typeinfo.dat objects (Session, …), and polymorphic tokens
        var resolved = PeopleCodeTypeDatabase.ResolveType(typeName);
        if (resolved is not UnknownTypeInfo)
            return resolved;

        // Could be an app class - try to resolve
        // For now, create an AppClassTypeInfo with the name
        // The resolver will handle it if it's queried later
        if (typeName.Contains(':'))
        {
            return AppClassTypeInfo.CreateWithInheritanceChain(typeName, _typeResolver, _typeCache, OwnClassMetadata);
        }

        // Unknown type
        return new UnknownTypeInfo(typeName);
    }

    /// <summary>
    /// Resolves an AppClassTypeNode to a TypeInfo, handling import resolution for unqualified names.
    /// </summary>
    /// <param name="appClass">The AppClassTypeNode to resolve</param>
    /// <returns>AppClassTypeInfo with the properly resolved qualified name</returns>
    private TypeInfo ResolveAppClassType(AppClassTypeNode appClass)
    {
        string qualifiedName;

        // Check if this is an unqualified name (empty package path)
        if (appClass.PackagePath.Count == 0 && _importMap.TryGetValue(appClass.ClassName, out var resolvedName))
        {
            // Resolve using import map
            qualifiedName = resolvedName;
        }
        else
        {
            // Use as-is (either fully qualified or unresolved unqualified)
            qualifiedName = string.Join(":", appClass.PackagePath.Append(appClass.ClassName));
        }

        return AppClassTypeInfo.CreateWithInheritanceChain(qualifiedName, _typeResolver, _typeCache, OwnClassMetadata);
    }

    /// <summary>
    /// Stamps every app class type reference (method/function returns, property types,
    /// extends, casts), so the checker reads the import-resolved name instead of the short
    /// name as written. Always overwrites: a re-run on a cached tree picks up resolver changes.
    /// Declaration visitors stamp their type nodes first with the same resolved value.
    /// </summary>
    public override void VisitAppClassType(AppClassTypeNode node)
    {
        base.VisitAppClassType(node);
        node.SetInferredType(ResolveAppClassType(node));
    }

    #endregion

    #region Array Dimensionality Helpers

    /// <summary>
    /// Reduce array dimensionality by 1 (for array indexing)
    /// </summary>
    private TypeInfo ReduceDimensionality(TypeInfo type)
    {
        if (type is ArrayTypeInfo arrayType)
        {
            if (arrayType.Dimensions == 1)
                return arrayType.ElementType ?? AnyTypeInfo.Instance;
            else
                return new ArrayTypeInfo(arrayType.Dimensions - 1, arrayType.ElementType);
        }

        // Non-array type indexed like array - treat as Any
        return AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Increase array dimensionality by 1
    /// </summary>
    private TypeInfo IncreaseDimensionality(TypeInfo type)
    {
        return new ArrayTypeInfo(1, type);
    }

    #endregion

    #region Resolution Methods

   

    /// <summary>
    /// Resolve return type for member access (property or method call)
    /// </summary>
    private TypeInfo ResolveMemberAccessReturnType(
        TypeInfo objectType,
        string memberName,
        bool isMethodCall,
        TypeInfo[]? parameterTypes)
    {
        // Members a named CI or a known item gets from the CI's definition
        if (isMethodCall)
        {
            if (FindCompIntfcCustomMethod(objectType, memberName) is { } customMethod)
                return ConvertFunctionInfoToTypeInfo(customMethod, objectType, parameterTypes);
        }
        else if (ResolveCompIntfcProperty(objectType, memberName) is { } ciMember)
        {
            return ciMember;
        }

        // Array properties (Len, Dimension); array methods are resolved in ResolveFunctionInfo
        if (objectType.Kind == TypeKind.Array && !isMethodCall)
        {
            return PeopleCodeTypeDatabase.GetProperty("Array", memberName) is { } arrayProperty
                ? ConvertPropertyInfoToTypeInfo(arrayProperty)
                : AnyTypeInfo.Instance;
        }

        // Builtin types (primitives and builtin objects)
        if (objectType.Kind == TypeKind.BuiltinObject || objectType.Kind == TypeKind.Primitive)
        {
            if (isMethodCall)
            {
                var method = PeopleCodeTypeDatabase.GetMethod(objectType.Name, memberName);
                if (method != null)
                    return ConvertFunctionInfoToTypeInfo(method, objectType, parameterTypes);
            }
            else
            {
                var prop = PeopleCodeTypeDatabase.GetProperty(objectType.Name, memberName);
                if (prop != null)
                {
                    return ConvertPropertyInfoToTypeInfo(prop);
                }
            }

            // Special handling for Row and Record types when member not found
            // These types have implicit property/method transformations
            if (objectType.PeopleCodeType.HasValue)
            {
                // Row property access acts as GetRecord()
                // Example: &row.PSADSRELATION acts as &row.GetRecord(Record.PSADSRELATION) → returns Record
                if (objectType.PeopleCodeType.Value == PeopleCodeType.Row && !isMethodCall)
                {
                    return TypeInfo.FromPeopleCodeType(PeopleCodeType.Record);
                }

                // Record property access acts as GetField()
                // Example: &rec.FIELDNAME acts as &rec.GetField(Field.FIELDNAME) → returns Field
                if (objectType.PeopleCodeType.Value == PeopleCodeType.Record && !isMethodCall)
                {
                    return TypeInfo.FromPeopleCodeType(PeopleCodeType.Field);
                }

                // Grid column access acts as GetColumn()
                // Example: &grid.CHECKLIST_ITEMCODE acts as &grid.GetColumn("CHECKLIST_ITEMCODE") → returns GridColumn
                if (objectType.PeopleCodeType.Value == PeopleCodeType.Grid && !isMethodCall)
                {
                    return TypeInfo.FromPeopleCodeType(PeopleCodeType.GridColumn);
                }

                // Row method call to non-existent method acts as GetRowset().GetRow()
                // Example: &row.TEST(3) acts as &row.GetRowset(Scroll.TEST).GetRow(3) → returns Row
                if (objectType.PeopleCodeType.Value == PeopleCodeType.Row && isMethodCall)
                {
                    return TypeInfo.FromPeopleCodeType(PeopleCodeType.Row);
                }
            }
        }

        // Custom types (AppClass/Interface)
        else if (objectType is AppClassTypeInfo appClassType)
        {
            if (isMethodCall)
            {
                // Walk up the inheritance chain to find the method
                var method = LookupMethodInInheritanceChain(appClassType, memberName);
                if (method != null)
                {
                    return InheritedReturnAnnotation(appClassType, method) is { } inherited
                        ? ConvertTypeWithDimensionalityToTypeInfo(inherited)
                        : ConvertFunctionInfoToTypeInfo(method, null, parameterTypes);
                }
            }
            else
            {
                // Walk up the inheritance chain to find the property
                var property = LookupPropertyInInheritanceChain(appClassType, memberName);
                if (property != null)
                    return ConvertPropertyInfoToTypeInfo(property);

                /* %This.foo can reference a private variable &foo */
                property = LookupPrivateInstanceVariable(appClassType, memberName);
                if (property != null)
                    return ConvertPropertyInfoToTypeInfo(property);
            }
        }

        return AnyTypeInfo.Instance;
    }

    /// <summary>
    /// A property or collection of a named CI or a known item, from its definition: a property is
    /// its field's primitive type (any when unknown), a collection a DataCollection one level
    /// deeper on the path. Null when the definition doesn't name it (today's typing applies).
    /// A collection's own members are DataCollection's (Count, Item, ...), so it has none here.
    /// </summary>
    private TypeInfo? ResolveCompIntfcProperty(TypeInfo objectType, string memberName)
    {
        if (CompIntfcTarget.Find(objectType, _typeResolver) is not { } ci)
            return null;

        if (ci.FindProperty(memberName) is { } property)
            return CompIntfcTarget.PropertyValueType(property).WithAssignable(true);

        return ci.Node.Collections.TryGetValue(memberName, out var collection)
            ? ci.CollectionType(collection)
            : null;
    }

    /// <summary>
    /// A user-defined method of a named CI (from its Methods PeopleCode), or null.
    /// </summary>
    private FunctionInfo? FindCompIntfcCustomMethod(TypeInfo objectType, string methodName) =>
        CompIntfcTarget.Find(objectType, _typeResolver)?.FindCustomMethod(methodName)?.Signature;


    #endregion

    #region Inheritance Chain Helpers

    /// <summary>
    /// Look up a method in an AppClass, walking up the inheritance chain if necessary.
    /// </summary>
    /// <param name="appClassType">The type to start searching from</param>
    /// <param name="methodName">The method name to find</param>
    /// <returns>The FunctionInfo for the method, or null if not found</returns>
    private FunctionInfo? LookupMethodInInheritanceChain(AppClassTypeInfo appClassType, string methodName)
    {
        var currentClassName = appClassType.QualifiedName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentClassName };

        while (!string.IsNullOrEmpty(currentClassName))
        {
            var metadata = GetClassMetadata(currentClassName);

            // If we have metadata, check for the method
            if (metadata != null && metadata.Methods.TryGetValue(methodName, out var method))
            {
                return method;
            }

            // Check if we've reached a builtin base class
            if (metadata != null && metadata.IsBaseClassBuiltin && metadata.BuiltinBaseType.HasValue)
            {
                // Look up method in the builtin type database
                var builtinTypeName = metadata.BuiltinBaseType.Value.GetTypeName();
                var builtinMethod = PeopleCodeTypeDatabase.GetMethod(builtinTypeName, methodName);
                if (builtinMethod != null)
                {
                    return builtinMethod;
                }
                break; // Builtin types don't have further inheritance to traverse
            }

            // Move to the next class in hierarchy - check both BaseClassName and InterfaceName
            // (PeopleCode allows either extends or implements, but not both, and they're interchangeable)
            string? nextClassName = null;
            if (metadata != null)
            {
                if (!string.IsNullOrEmpty(metadata.BaseClassName))
                {
                    nextClassName = metadata.BaseClassName;
                }
                else if (!string.IsNullOrEmpty(metadata.InterfaceName))
                {
                    nextClassName = metadata.InterfaceName;
                }
            }

            if (nextClassName == null)
            {
                break; // No more base classes/interfaces
            }

            // Circular inheritance detection
            if (!visited.Add(nextClassName))
            {
                break; // Detected circular inheritance
            }

            currentClassName = nextClassName;
        }

        return null; // Method not found in the hierarchy
    }

    /// <summary>
    /// The program's own metadata when it is a class or interface, for inheritance-chain building
    /// (so analyzing a class never fetches, and re-refines, the class itself), else null.
    /// </summary>
    private TypeMetadata? OwnClassMetadata =>
        _programMetadata.Kind is ProgramKind.AppClass or ProgramKind.Interface ? _programMetadata : null;

    /// <summary>
    /// Metadata for an app class or interface: the current program's own, else the type cache,
    /// else the resolver (caching what it returns). Null when the class can't be resolved.
    /// </summary>
    private TypeMetadata? GetClassMetadata(string className)
    {
        if (className.Equals(_programMetadata.QualifiedName, StringComparison.OrdinalIgnoreCase))
            return _programMetadata;

        var metadata = _typeCache.Get(className);
        if (metadata == null && _typeResolver != null)
        {
            metadata = _typeResolver.GetTypeMetadata(className);
            if (metadata != null)
                _typeCache.Set(className, metadata);
        }
        return metadata;
    }

    /// <summary>
    /// The base declaration's return annotation for an app-class method whose resolved
    /// declaration has none of its own (spec §7 "Inheritance"), or null. Walks the same chain as
    /// <see cref="LookupMethodInInheritanceChain"/>; the first declaration with an annotation,
    /// even an unknown or misfit one, ends the walk.
    /// </summary>
    private TypeWithDimensionality? InheritedReturnAnnotation(AppClassTypeInfo appClassType, FunctionInfo method)
    {
        if (method.IsBuiltin || method.RefinedReturnType != null || method.HasReturnAnnotation)
            return null;

        return AnnotatedReturnDeclaration(appClassType.QualifiedName, method.Name)?.RefinedReturnType;
    }

    /// <summary>
    /// The first declaration of <paramref name="methodName"/> with a return annotation (even an
    /// unknown or misfit one) walking up from <paramref name="className"/>, or null. Only
    /// annotations count: a RefinedReturnType without one was inferred from returns (spec §9)
    /// and is never inherited.
    /// </summary>
    private FunctionInfo? AnnotatedReturnDeclaration(string? className, string methodName)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrEmpty(className) && visited.Add(className))
        {
            var metadata = GetClassMetadata(className);
            if (metadata == null)
                return null;

            if (metadata.Methods.TryGetValue(methodName, out var declaration) && declaration.HasReturnAnnotation)
                return declaration;

            // Builtin bases carry no annotations
            if (metadata.IsBaseClassBuiltin)
                return null;

            className = !string.IsNullOrEmpty(metadata.BaseClassName) ? metadata.BaseClassName : metadata.InterfaceName;
        }

        return null;
    }

    /// <summary>
    /// For a parameter of a method in the current class's header that has no annotation of its
    /// own, the annotation on the same parameter of the nearest annotated base declaration of
    /// the method (spec §7 "Inheritance"), or null. Base metadata comes from the resolver. Like
    /// <see cref="InheritedReturnAnnotation"/>, the walk (<see cref="ParameterAnnotationInChain"/>) passes
    /// overrides that leave the parameter unannotated and ends at the first declaration with an
    /// annotation, even an unknown or misfit one.
    /// </summary>
    private TypeWithDimensionality? InheritedParameterAnnotation(ParameterNode parameter)
    {
        if (parameter.Parent is not MethodNode method || _programMetadata.IsBaseClassBuiltin)
            return null;

        var index = method.Parameters.ToList().IndexOf(parameter);
        var className = !string.IsNullOrEmpty(_programMetadata.BaseClassName)
            ? _programMetadata.BaseClassName
            : _programMetadata.InterfaceName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { _programMetadata.QualifiedName };

        return ParameterAnnotationInChain(className, method.Name, index, visited);
    }

    /// <summary>
    /// Stamps a call to an app-class method with each parameter's effective annotation, own or
    /// inherited, so the checker's annotated-parameter warning (spec §7) sees inherited ones too.
    /// Nothing is stamped when no parameter is annotated.
    /// </summary>
    private void StampParameterAnnotations(FunctionCallNode node, AppClassTypeInfo appClassType, FunctionInfo method)
    {
        if (method.IsBuiltin)
            return;

        var annotations = new TypeWithDimensionality?[method.Parameters.Count];
        for (int i = 0; i < annotations.Length; i++)
        {
            annotations[i] = ParameterAnnotationInChain(appClassType.QualifiedName, method.Name, i,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        if (annotations.Any(a => a != null))
            node.SetParameterAnnotations(annotations);
    }

    /// <summary>
    /// The annotation on parameter <paramref name="index"/> of the first declaration of
    /// <paramref name="methodName"/> with one, walking up from <paramref name="className"/>
    /// (inclusive). Overrides that leave the parameter unannotated are passed; the first
    /// declaration with an annotation, even an unknown or misfit one, ends the walk.
    /// </summary>
    private TypeWithDimensionality? ParameterAnnotationInChain(string? className, string methodName, int index, HashSet<string> visited)
    {
        while (!string.IsNullOrEmpty(className) && visited.Add(className))
        {
            var metadata = GetClassMetadata(className);
            if (metadata == null)
                return null;

            if (metadata.Methods.TryGetValue(methodName, out var declaration))
            {
                if (index < 0 || index >= declaration.Parameters.Count
                    || declaration.Parameters[index] is not SingleParameter single)
                    return null;

                if (single.RefinedParameterType != null || single.HasAnnotation)
                    return single.RefinedParameterType;
            }

            if (metadata.IsBaseClassBuiltin)
                return null;

            className = !string.IsNullOrEmpty(metadata.BaseClassName) ? metadata.BaseClassName : metadata.InterfaceName;
        }

        return null;
    }

    private PropertyInfo? LookupPrivateInstanceVariable(AppClassTypeInfo classType, string propertyName)
    {

        // If we have metadata, check for the property
        if (_programMetadata != null && _programMetadata.InstanceVariables.TryGetValue($"&{propertyName}", out var property))
        {
            return property;
        }
        return null;
    }

    /// <summary>
    /// Look up a property in an AppClass, walking up the inheritance chain if necessary.
    /// </summary>
    /// <param name="appClassType">The type to start searching from</param>
    /// <param name="propertyName">The property name to find</param>
    /// <returns>The PropertyInfo for the property, or null if not found</returns>
    private PropertyInfo? LookupPropertyInInheritanceChain(AppClassTypeInfo appClassType, string propertyName)
    {
        var currentClassName = appClassType.QualifiedName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentClassName };

        while (!string.IsNullOrEmpty(currentClassName))
        {
            var metadata = GetClassMetadata(currentClassName);

            // If we have metadata, check for the property
            if (metadata != null && metadata.Properties.TryGetValue(propertyName, out var property))
            {
                return property;
            }

            // Check if we've reached a builtin base class
            if (metadata != null && metadata.IsBaseClassBuiltin && metadata.BuiltinBaseType.HasValue)
            {
                // Look up property in the builtin type database
                var builtinTypeName = metadata.BuiltinBaseType.Value.GetTypeName();
                var builtinProperty = PeopleCodeTypeDatabase.GetProperty(builtinTypeName, propertyName);
                if (builtinProperty != null)
                {
                    return builtinProperty;
                }
                break; // Builtin types don't have further inheritance to traverse
            }

            // Move to the next class in hierarchy - check both BaseClassName and InterfaceName
            // (PeopleCode allows either extends or implements, but not both, and they're interchangeable)
            string? nextClassName = null;
            if (metadata != null)
            {
                if (!string.IsNullOrEmpty(metadata.BaseClassName))
                {
                    nextClassName = metadata.BaseClassName;
                }
                else if (!string.IsNullOrEmpty(metadata.InterfaceName))
                {
                    nextClassName = metadata.InterfaceName;
                }
            }

            if (nextClassName == null)
            {
                break; // No more base classes/interfaces
            }

            // Circular inheritance detection
            if (!visited.Add(nextClassName))
            {
                break; // Detected circular inheritance
            }

            currentClassName = nextClassName;
        }

        return null; // Property not found in the hierarchy
    }

    #endregion

    private FunctionInfo? ResolveFunctionInfo(FunctionCallNode node, TypeInfo[] parameterTypes)
    {
        if (node.Function is IdentifierNode identifier)
        {
            var func = _program.Functions.FirstOrDefault(f => f.Name.Equals(identifier.Name, StringComparison.OrdinalIgnoreCase));
            if (func != null)
            {
                if (func.IsImplementation)
                {
                    _programMetadata.Methods.TryGetValue(identifier.Name, out var method);
                    return method;
                }
                else if (func.IsDeclaration)
                {
                    // FunctionInfo should already be cached on the declaration node by ProcessDeclaredFunctions
                    var cachedFuncInfo = func.GetFunctionInfo();
                    if (cachedFuncInfo != null)
                        return cachedFuncInfo;

                    // Fallback: resolve on-demand (shouldn't normally happen after ProcessDeclaredFunctions)
                    string qualifiedName = $"{func.RecordName}.{func.FieldName}.{func.RecordEvent}";
                    var sourceMetadata = _typeCache.Get(qualifiedName) ?? _typeResolver?.GetTypeMetadata(qualifiedName);
                    if (sourceMetadata != null && sourceMetadata.Methods.TryGetValue(identifier.Name, out var importedFunc))
                    {
                        return importedFunc;
                    }
                }
            }

            var builtinFunc = PeopleCodeTypeDatabase.GetFunction(identifier.Name);
            if (builtinFunc != null) return builtinFunc;

            var identifierType = GetInferredType(identifier);
            if (identifierType != null && identifierType.Kind == TypeKind.BuiltinObject)
            {
                var obj = PeopleCodeTypeDatabase.GetObject(identifierType.Name);
                if (obj is not null && obj?.DefaultMethodHash != 0)
                {
                    return obj.LookupMethodByHash(obj.DefaultMethodHash);
                }
            }
        }
        else if (node.Function is MemberAccessNode memberAccess)
        {
            var objectType = GetInferredType(memberAccess.Target);
            if (objectType != null)
            {
                // A named CI's own methods come from its definition; its standard ones from typeinfo
                if (FindCompIntfcCustomMethod(objectType, memberAccess.MemberName) is { } customMethod)
                    return customMethod;

                if (objectType.Kind == TypeKind.BuiltinObject || objectType.Kind == TypeKind.Primitive)
                {
                    return PeopleCodeTypeDatabase.GetMethod(objectType.Name, memberAccess.MemberName);
                }
                else if (objectType is AppClassTypeInfo appClassType)
                {
                    // Walk up the inheritance chain to find the method
                    return LookupMethodInInheritanceChain(appClassType, memberAccess.MemberName);
                } else if (objectType.Kind == TypeKind.Array)
                {
                    return PeopleCodeTypeDatabase.GetMethod("Array", memberAccess.MemberName);
                }
            }
        }
        else
        {
            var targetType = GetInferredType(node.Function);
            if (targetType != null && targetType.Kind == TypeKind.BuiltinObject)
            {
                var obj = PeopleCodeTypeDatabase.GetObject(targetType.Name);
                // obj is not null guard: a null obj makes 'obj?.DefaultMethodHash != 0' true
                // (nullable null != 0), which would then null-deref on LookupMethodByHash.
                if (obj is not null && obj.DefaultMethodHash != 0)
                {
                    return obj.LookupMethodByHash(obj.DefaultMethodHash);
                }
            }
        }

        return null;
    }

    #region Declaration Visitors

    /// <summary>
    /// Visits local variable declaration, resolves types, then lets base class register variables
    /// </summary>
    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        // FIRST: Resolve and set type attribute on the TypeNode
        if (node.Type != null)
        {
            var resolvedType = ConvertTypeNodeToTypeInfo(node.Type);
            node.Type.SetInferredType(resolvedType);
        }

        // NOW: Let base class register variables (will read from attribute)
        base.VisitLocalVariableDeclaration(node);

        StampDeclaredNames(node, node.VariableNameInfos);
    }

    /// <summary>
    /// Records on the declaration the type each declared name holds right after it, so a hover on
    /// the declared name (not an expression node) shows what a use there would.
    /// </summary>
    private void StampDeclaredNames(AstNode declaration, IEnumerable<VariableNameInfo> names)
    {
        foreach (var name in names)
        {
            if (name.Token != null && FindVariable(name.Name) is { IsAutoDeclared: false } variable)
                StampDeclaredName(declaration, variable, name.SourceSpan);
        }
    }

    private void StampDeclaredName(AstNode declaration, VariableInfo variable, SourceSpan nameSpan)
    {
        var type = variable.InferredType ?? ConvertTypeNameToTypeInfo(variable.Type);
        var source = variable.RefinementSource;
        declaration.SetDeclaredVariableType(new DeclaredVariableType(
            variable.Name, nameSpan, type, source, source == null ? UndecidedCandidates(variable) : null));
    }

    /// <summary>
    /// The API types an undecided ApiObject variable's uses still allow, or null when narrowing
    /// settled it or has nothing to say.
    /// </summary>
    private IReadOnlyList<string>? UndecidedCandidates(VariableInfo variable) =>
        _narrowing?.For(variable.DeclarationNode, variable.Name) is { NarrowedType: null, Candidates.Count: > 0 } undecided
            ? undecided.Candidates
            : null;

    /// <summary>
    /// Visits local variable declaration with assignment, resolves types, then lets base class register
    /// </summary>
    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        // FIRST: Resolve and set type attribute
        if (node.Type != null)
        {
            var resolvedType = ConvertTypeNodeToTypeInfo(node.Type);
            node.Type.SetInferredType(resolvedType);
        }

        // NOW: Let base class register the variable and visit the initializer
        base.VisitLocalVariableDeclarationWithAssignment(node);

        // Refine Local Record &r = CreateRecord(Record.NAME) → RecordTypeInfo(NAME)
        TryRefineVariableFromExpression(node.VariableNameInfo.Name, node.InitialValue, node);

        StampDeclaredNames(node, new[] { node.VariableNameInfo });
    }

    /// <summary>
    /// Visits program variable (instance/global/component), resolves types first
    /// </summary>
    public override void VisitProgramVariable(ProgramVariableNode node)
    {
        // FIRST: Resolve and set type attribute
        if (node.Type != null)
        {
            var resolvedType = ConvertTypeNodeToTypeInfo(node.Type);
            node.Type.SetInferredType(resolvedType);
        }

        // NOW: Let base class register variables
        base.VisitProgramVariable(node);

        StampDeclaredNames(node, node.NameInfos);
    }

    /// <summary>
    /// Visits method, resolves parameter types first
    /// </summary>
    public override void VisitMethod(MethodNode node)
    {
        // FIRST: Resolve parameter types
        foreach (var parameter in node.Parameters)
        {
            if (parameter.Type != null)
            {
                var resolvedType = ConvertTypeNodeToTypeInfo(parameter.Type);
                parameter.Type.SetInferredType(resolvedType);
            }
        }

        // The MethodNode is the class-header declaration; implementations are linked, not separate
        CheckReturnAnnotation(node, node.Name, node.ReturnType);

        // NOW: Let base class create scope and register parameters
        var collector = node.Implementation != null
            && _programMetadata.Kind == ProgramKind.AppClass
            && _programMetadata.Methods.TryGetValue(node.Name, out var methodInfo)
            && !HasInheritedReturnAnnotation(methodInfo)
            ? ReturnCollectorFor(node.Name, methodInfo)
            : null;
        VisitRoutine(collector, () => base.VisitMethod(node));
    }

    /// <summary>
    /// Visits a property getter or setter; a getter's returns can refine the property's type.
    /// </summary>
    public override void VisitPropertyImpl(PropertyImplNode node)
    {
        ReturnCollector? collector = null;
        if (node.IsGetter
            && node.Parent is PropertyNode property
            && _programMetadata.Kind == ProgramKind.AppClass
            && _programMetadata.Properties.TryGetValue(property.Name, out var propertyInfo)
            && propertyInfo.RefinedType == null
            && _annotations.ForType(property.Type) == null
            && IsRefinableReturnType(propertyInfo.ToTypeWithDimensionality()))
        {
            collector = new ReturnCollector(property.Name, propertyInfo.Type, refined => propertyInfo.RefinedType = refined);
        }

        VisitRoutine(collector, () => base.VisitPropertyImpl(node));
    }

    /// <summary>
    /// Visits function, resolves parameter types first (implementations only)
    /// </summary>
    public override void VisitFunction(FunctionNode node)
    {
        // For function implementations, resolve parameter types
        if (node.IsImplementation)
        {
            foreach (var parameter in node.Parameters)
            {
                if (parameter.Type != null)
                {
                    var resolvedType = ConvertTypeNodeToTypeInfo(parameter.Type);
                    parameter.Type.SetInferredType(resolvedType);
                }
            }
        }

        // Annotations on Declare Function lines have no effect, so they don't warn either
        if (node.IsImplementation)
            CheckReturnAnnotation(node, node.Name, node.ReturnType);

        // NOW: Let base class handle scope creation and parameter registration
        if (!node.IsImplementation)
        {
            base.VisitFunction(node);
            return;
        }

        // Class programs' metadata holds methods, not functions, under the same names
        var collector = _programMetadata.Kind is not (ProgramKind.AppClass or ProgramKind.Interface)
            && _programMetadata.Methods.TryGetValue(node.Name, out var functionInfo)
            ? ReturnCollectorFor(node.Name, functionInfo)
            : null;
        VisitRoutine(collector, () => base.VisitFunction(node));
    }

    /// <summary>
    /// Methods and getters are visited callees first, so a call usually sees its callee's refined return.
    /// </summary>
    protected override IEnumerable<AstNode> RoutineVisitOrder(AppClassNode node) =>
        global::PeopleCodeParser.SelfHosted.Visitors.RoutineVisitOrder.CalleesFirst(node);

    #endregion

    #region Same-program return refinement

    /// <summary>
    /// The <c>Return</c> values of one routine body being visited, and where an agreed type goes.
    /// </summary>
    private sealed class ReturnCollector(string name, PeopleCodeType declaredType, Action<TypeWithDimensionality> apply)
    {
        public string Name { get; } = name;
        public PeopleCodeType DeclaredType { get; } = declaredType;
        public Action<TypeWithDimensionality> Apply { get; } = apply;
        public List<(ReturnStatementNode Node, TypeInfo? Type)> Returns { get; } = new();
    }

    // One entry per routine body being visited; null when the routine isn't refined
    private readonly Stack<ReturnCollector?> _returnCollectors = new();

    /// <summary>
    /// A collector for a function or method declared to return scalar ApiObject, Record or Field
    /// with no return annotation, or null.
    /// </summary>
    private static ReturnCollector? ReturnCollectorFor(string name, FunctionInfo info)
    {
        if (info.HasReturnAnnotation || info.RefinedReturnType != null || !IsRefinableReturnType(info.ReturnType))
            return null;

        return new ReturnCollector(name, info.ReturnType.Type, refined => info.RefinedReturnType = refined);
    }

    /// <summary>
    /// True when a base declaration of this class's method annotates its return (spec §7
    /// "Inheritance"): the annotation decides, so the body's returns don't refine it. Not
    /// <c>InheritedReturnAnnotation(...) != null</c>: a misfit or unknown base annotation has no
    /// RefinedReturnType but still decides (the declared type).
    /// </summary>
    private bool HasInheritedReturnAnnotation(FunctionInfo method) =>
        !method.HasReturnAnnotation && AnnotatedReturnDeclaration(_programMetadata.QualifiedName, method.Name) != null;

    private static bool IsRefinableReturnType(TypeWithDimensionality type) =>
        type.IsScalar && type.Type is PeopleCodeType.ApiObject or PeopleCodeType.Record or PeopleCodeType.Field;

    /// <summary>
    /// Visits a routine body with its collector current, then writes the agreed return type.
    /// </summary>
    private void VisitRoutine(ReturnCollector? collector, Action visit)
    {
        _returnCollectors.Push(collector);
        try
        {
            visit();
        }
        finally
        {
            _returnCollectors.Pop();
        }

        if (collector != null && AgreedReturnType(collector) is { } refined)
            collector.Apply(refined);
    }

    public override void VisitReturn(ReturnStatementNode node)
    {
        base.VisitReturn(node);

        // Return Null; says nothing about the type (e.g. a not-found path), so it doesn't disagree
        if (node.Value != null && !IsNullLiteral(node.Value)
            && _returnCollectors.Count > 0 && _returnCollectors.Peek() is { } collector)
            collector.Returns.Add((node, GetInferredType(node.Value)));
    }

    /// <summary>
    /// The type every <c>Return</c> value agrees on (spec §9), or null. Disagreeing API subtypes
    /// warn (R1) on the first return that differs; disagreeing records or fields stay silent.
    /// </summary>
    private static TypeWithDimensionality? AgreedReturnType(ReturnCollector collector)
    {
        var returns = collector.Returns;
        var refined = AgreedType(collector.DeclaredType, returns.Select(r => r.Type).ToList(), out int conflict);

        if (conflict >= 0)
        {
            var first = (BuiltinObjectTypeInfo)returns[0].Type!;
            var other = (BuiltinObjectTypeInfo)returns[conflict].Type!;
            var value = returns[conflict].Node.Value!;
            value.AddTypeWarning(new TypeError(
                $"{collector.Name} returns more than one API type " +
                $"({first.PeopleCodeType!.Value.GetTypeName()} on line {returns[0].Node.SourceSpan.Start.Line + 1}, " +
                $"{other.PeopleCodeType!.Value.GetTypeName()} on line {returns[conflict].Node.SourceSpan.Start.Line + 1}).",
                value));
        }

        return refined;
    }

    /// <summary>
    /// The type a set of values (returns, or assignments to a member) agrees on for a declared
    /// scalar ApiObject, Record or Field (spec §9), or null. API values must all be the same
    /// non-base API subtype; records the same record name; fields the same record and field.
    /// </summary>
    internal static TypeWithDimensionality? AgreedType(PeopleCodeType declaredType, IReadOnlyList<TypeInfo?> values) =>
        AgreedType(declaredType, values, out _);

    /// <param name="conflict">The index of the first API value whose subtype differs from the
    /// first one's (where R1 goes), or -1.</param>
    private static TypeWithDimensionality? AgreedType(PeopleCodeType declaredType, IReadOnlyList<TypeInfo?> values, out int conflict)
    {
        conflict = -1;
        if (values.Count == 0)
            return null;

        switch (declaredType)
        {
            case PeopleCodeType.Record:
            {
                if (values[0] is not RecordTypeInfo first || string.IsNullOrEmpty(first.RecordName))
                    return null;
                bool agree = values.All(v => v is RecordTypeInfo record
                    && first.RecordName.Equals(record.RecordName, StringComparison.OrdinalIgnoreCase));
                return agree
                    ? new TypeWithDimensionality(PeopleCodeType.Record) { Identity = new TypeIdentity(RecordName: first.RecordName) }
                    : null;
            }

            case PeopleCodeType.Field:
            {
                if (values[0] is not FieldTypeInfo first || string.IsNullOrEmpty(first.FieldName))
                    return null;
                bool agree = values.All(v => v is FieldTypeInfo field
                    && (first.RecordName ?? "").Equals(field.RecordName ?? "", StringComparison.OrdinalIgnoreCase)
                    && first.FieldName.Equals(field.FieldName, StringComparison.OrdinalIgnoreCase));
                if (!agree)
                    return null;
                var recordName = string.IsNullOrEmpty(first.RecordName) ? null : first.RecordName;
                return new TypeWithDimensionality(PeopleCodeType.Field) { Identity = new TypeIdentity(recordName, first.FieldName) };
            }

            case PeopleCodeType.ApiObject:
                return AgreedApiType(values, out conflict);
        }

        return null;
    }

    private static TypeWithDimensionality? AgreedApiType(IReadOnlyList<TypeInfo?> values, out int conflict)
    {
        conflict = -1;
        var types = new List<BuiltinObjectTypeInfo>(values.Count);
        foreach (var type in values)
        {
            // Any, Unknown, plain ApiObject or a non-API value: nothing to say
            if (type is not BuiltinObjectTypeInfo { PeopleCodeType: not null } api
                || api.IsApiObjectBase
                || !PeopleCodeTypeDatabase.IsApiObjectSubtype(api.Name))
                return null;
            types.Add(api);
        }

        var first = types[0];
        for (int i = 1; i < types.Count; i++)
        {
            if (types[i].PeopleCodeType == first.PeopleCodeType)
                continue;

            conflict = i;
            return null;
        }

        // A CI name survives only when every value carries the same one
        var identity = first.Identity is { IsEmpty: false } firstIdentity ? firstIdentity : null;
        foreach (var type in types.Skip(1))
        {
            if (identity == null)
                break;
            if (type.Identity is not { } other || !other.IsRefinementOf(identity) || !identity.IsRefinementOf(other))
                identity = null;
        }

        return new TypeWithDimensionality(first.PeopleCodeType!.Value) { Identity = identity };
    }

    #endregion

    #region Member assignments

    private static bool IsNullLiteral(ExpressionNode expression) =>
        UnwrapParentheses(expression) is LiteralNode { LiteralType: LiteralType.Null };

    // Values assigned to this class's own properties and instance variables, for ReturnTypeRefiner
    private readonly Dictionary<PropertyInfo, List<TypeInfo?>> _memberAssignments = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The inferred types of the values assigned to each of this class's own properties and
    /// instance variables, in visit order: <c>&amp;Name = …</c> where <c>&amp;Name</c> resolves to the
    /// instance variable or property (not a shadowing local or parameter), and <c>%This.Name = …</c>.
    /// Keyed by the metadata's PropertyInfo, which one multi-name instance declaration shares.
    /// </summary>
    internal IReadOnlyDictionary<PropertyInfo, List<TypeInfo?>> MemberAssignments => _memberAssignments;

    private void RecordMemberAssignment(AssignmentNode node)
    {
        if (node.Operator != AssignmentOperator.Assign || _programMetadata.Kind != ProgramKind.AppClass)
            return;

        PropertyInfo? member = null;
        if (node.Target is IdentifierNode identifier && identifier.Name.StartsWith('&'))
        {
            var variable = FindVariable(identifier.Name);
            if (variable?.Kind == VariableKind.Instance)
                _programMetadata.InstanceVariables.TryGetValue(variable.Name, out member);
            else if (variable?.Kind == VariableKind.Property)
                _programMetadata.Properties.TryGetValue(variable.Name, out member);
        }
        else if (node.Target is MemberAccessNode { Target: IdentifierNode target } memberAccess
            && target.Name.Equals("%This", StringComparison.OrdinalIgnoreCase))
        {
            _programMetadata.Properties.TryGetValue(memberAccess.MemberName, out member);
        }

        // &x = Null; (teardown) says nothing about the type, so it doesn't disagree
        if (member == null || IsNullLiteral(node.Value))
            return;

        if (!_memberAssignments.TryGetValue(member, out var values))
            _memberAssignments[member] = values = new List<TypeInfo?>();
        values.Add(GetInferredType(node.Value));
    }

    #endregion

    #region Statement Visitors

    /// <summary>
    /// Visit assignment and update auto-declared / specialized Record-Field variable types
    /// </summary>
    public override void VisitAssignment(AssignmentNode node)
    {
        // Let base class handle the assignment (reference tracking, etc.)
        base.VisitAssignment(node);

        RecordMemberAssignment(node);

        // Check if the assignment target is an identifier (simple variable assignment)
        if (node.Target is IdentifierNode identifier)
        {
            // Find the variable in scope
            var variable = FindVariable(identifier.Name);

            // If it's an auto-declared variable with type "any", update its type from the right-hand side
            if (_inferAutoDeclaredTypes && 
                variable != null && 
                variable.IsAutoDeclared && 
                variable.Type.Equals("any", StringComparison.OrdinalIgnoreCase)
                )
            {
                // Infer type from the right-hand side expression
                var rightHandType = GetInferredType(node.Value);
                if (rightHandType != null && rightHandType.Name != null && !rightHandType.Name.Equals("any", StringComparison.OrdinalIgnoreCase))
                {
                    // A variable holds the value, not the buffer field — drop the field-name marker
                    rightHandType = rightHandType.WithAutoDeclared(true).WithBufferField(null);
                    // Update the variable's type
                    variable.SetInferredType(rightHandType);
                }
            }

            // &r = CreateRecord(Record.NAME) / &f = &rec.GetField(Field.X) — keep named identity
            TryRefineVariableFromExpression(identifier.Name, node.Value, node);

            // The target keeps the type it held before the assignment (the checker compares the two);
            // the assignment records the type it holds now, for hovers on the target
            if (variable != null && identifier.SourceSpan.IsValid)
                StampDeclaredName(node, variable, identifier.SourceSpan);
        }
    }

    /// <summary>
    /// Stamps a variable use with the variable's current type and where a refined type came from
    /// (or, for an undecided ApiObject, the candidates its uses still allow).
    /// </summary>
    private void StampVariableUse(IdentifierNode node, VariableInfo variable)
    {
        var inferredType = variable.InferredType ?? ConvertTypeNameToTypeInfo(variable.Type);

        // Variables (identifiers starting with &) are assignable
        if (node.Name.StartsWith("&"))
        {
            inferredType = variable.IsAutoDeclared
                ? inferredType.WithAssignable(true).WithAutoDeclared(true)
                : inferredType.WithAssignable(true);
        }

        SetInferredType(node, inferredType);
        if (variable.RefinementSource is { } src)
            node.SetRefinementSource(src);
        else if (!variable.IsAutoDeclared && UndecidedCandidates(variable) is { } candidates)
            node.SetNarrowingCandidates(candidates);
    }

    #endregion

    #region Expression Visitors

    /// <summary>
    /// Visits class constant references like those used in %metadata:Key
    /// </summary>
    /// <param name="node"></param>
    public override void VisitClassConstant(ClassConstantNode node)
    {
        base.VisitClassConstant(node);
        SetInferredType(node, AnyTypeInfo.Instance);
    }

    /// <summary>
    /// Visit literal expression and infer type from literal value
    /// </summary>
    public override void VisitLiteral(LiteralNode node)
    {
        base.VisitLiteral(node);

        TypeInfo inferredType = node.LiteralType switch
        {
            LiteralType.String => PrimitiveTypeInfo.String,
            LiteralType.Integer => PrimitiveTypeInfo.Integer,
            LiteralType.Decimal => PrimitiveTypeInfo.Number,
            LiteralType.Boolean => PrimitiveTypeInfo.Boolean,
            LiteralType.Null => AnyTypeInfo.Instance,  // Null can be any type
            _ => UnknownTypeInfo.Instance
        };

        SetInferredType(node, inferredType);
    }

    public override void VisitInterpolatedString(InterpolatedStringNode node)
    {
        base.VisitInterpolatedString(node);
        SetInferredType(node, PrimitiveTypeInfo.String);
    }

    /// <summary>
    /// Visit identifier and resolve type from variable tracking
    /// </summary>
    public override void VisitIdentifier(IdentifierNode node)
    {
        base.VisitIdentifier(node);

        // Stylers re-run inference on the hash-cached tree: drop the previous pass's variable
        // stamps so a use whose variable is no longer refined doesn't keep a stale source
        node.Attributes.Remove(AstNode.RefinementSourceAttributeKey);
        node.Attributes.Remove(AstNode.NarrowingCandidatesAttributeKey);

        if (node.Name.Equals("^"))
        {
            if (_defaultRecordName != null && _defaultFieldName != null)
            {
                // AppRefiner shorthand for the current buffer field: behaves like a bare
                // FIELD in record PeopleCode, except ^.Member keeps the Field object.
                var fieldType = new FieldTypeInfo(_defaultRecordName, _defaultFieldName, _typeResolver);
                bool isMemberAccessTarget =
                    node.Parent is MemberAccessNode caretMa && ReferenceEquals(caretMa.Target, node);
                SetInferredType(node, isMemberAccessTarget ? fieldType : BufferFieldValue(fieldType));
                return;
            }
        }

        // Special handling for %This - resolve to current class type
        if (node.Name.Equals("%This", StringComparison.OrdinalIgnoreCase))
        {
            // %This refers to the current instance of the class being analyzed
           if (_programMetadata.Kind == ProgramKind.AppClass || _programMetadata.Kind == ProgramKind.Interface)
            {
                var thisType = AppClassTypeInfo.CreateWithInheritanceChain(_programMetadata.QualifiedName, _typeResolver, _typeCache, OwnClassMetadata);
                SetInferredType(node, thisType);
            }
            else
            {
                // %This only makes sense in class context
                SetInferredType(node, UnknownTypeInfo.Instance);
            }
        }
        // Special handling for %Super - resolve to base class type
        else if (node.Name.Equals("%Super", StringComparison.OrdinalIgnoreCase))
        {
            // %Super refers to the base class of the current class
            if ((_programMetadata.Kind == ProgramKind.AppClass || _programMetadata.Kind == ProgramKind.Interface)
                && !string.IsNullOrEmpty(_programMetadata.BaseClassName))
            {
                // Check if the base class is a builtin type
                if (_programMetadata.IsBaseClassBuiltin && _programMetadata.BuiltinBaseType.HasValue)
                {
                    // %Super refers to a builtin type - use the builtin type directly
                    var superType = TypeInfo.FromPeopleCodeType(_programMetadata.BuiltinBaseType.Value);
                    SetInferredType(node, superType);
                }
                else
                {
                    // %Super refers to another AppClass
                    var superType = AppClassTypeInfo.CreateWithInheritanceChain(_programMetadata.BaseClassName, _typeResolver, _typeCache, OwnClassMetadata);
                    SetInferredType(node, superType);
                }
            }
            else
            {
                // %Super only makes sense when there's a base class
                SetInferredType(node, UnknownTypeInfo.Instance);
            }
        }
        else
        {
            var variable = FindVariable(node.Name);

            // Try undefined/auto-declared variable handler (e.g., for language extension implicit parameters)
            // Auto-declared variables are treated as "undefined" to allow extensions to provide better types
            if ((variable == null || variable.IsAutoDeclared) && _onUndefinedVariable != null)
            {
                var delegateType = _onUndefinedVariable(node, GetCurrentScope(), VariableRegistry);
                if (delegateType != null)
                {
                    // Language extension provided a type - use it
                    SetInferredType(node, delegateType);
                    return;
                }
            }

            // A declared or auto-declared variable: use its type
            if (variable != null)
            {
                StampVariableUse(node, variable);
            }
            // Variable is null - try system variables and field types
            else
            {
                // Not a declared variable, check if it's a system variable.
                // System variables (like %UserId) are not prefixed with '&'.
                var systemVar = PeopleCodeTypeDatabase.GetSystemVariable(node.Name);
                if (systemVar != null)
                {
                    SetInferredType(node, ConvertPropertyInfoToTypeInfo(systemVar));
                }
                // Bare identifiers (no & / %) — PeopleCode rules:
                //
                //   NAME.something  → NAME is always a *record* (DirectRecordAccess).
                //   NAME alone + default record (Record PeopleCode)
                //                   → field on the default record.
                //   NAME alone, no default record
                //                   → invalid / Unknown (full record.field required).
                //
                // Special reference keywords (Field, Record, Scroll, Page, SQL, ...) are excluded:
                // they never stand alone as a field/record — they are always the left half of a
                // definition reference like Field.OPRID, which VisitMemberAccess types as a whole
                // (see the IsSpecialReferenceKeyword branch there).
                else if (!node.Name.StartsWith("&") && !node.Name.StartsWith("%")
                    && !ReferenceTypeInfo.IsSpecialReferenceKeyword(node.Name))
                {
                    bool isMemberAccessTarget =
                        node.Parent is MemberAccessNode ma && ReferenceEquals(ma.Target, node);

                    // NAME(...) → NAME is a function (builtin or declared), never a buffer field.
                    // The call's type comes from VisitFunctionCall; the callee itself has no value type.
                    bool isCallee =
                        node.Parent is FunctionCallNode call && ReferenceEquals(call.Function, node);

                    if (isCallee)
                    {
                        SetInferredType(node, UnknownTypeInfo.Instance);
                    }
                    else if (isMemberAccessTarget)
                    {
                        // NAME.x → NAME is a record name
                        var recordType = new RecordTypeInfo(node.Name, _typeResolver) { DirectRecordAccess = true };
                        SetInferredType(node, recordType);
                    }
                    else if (_defaultRecordName != null)
                    {
                        // Bare field reference in record PeopleCode (terminal buffer access).
                        // Compiler treats this like REC.FIELD with implicit .Value — the
                        // expression type is the field's data type, not Field.
                        var fieldType = new FieldTypeInfo(_defaultRecordName, node.Name, _typeResolver);
                        SetInferredType(node, BufferFieldValue(fieldType));
                    }
                    else
                    {
                        // Outside record context a bare name is not a valid field or record ref
                        SetInferredType(node, UnknownTypeInfo.Instance);
                    }
                }
                else
                {
                    SetInferredType(node, UnknownTypeInfo.Instance);
                }
            }
        }
    }

    public override void VisitArrayType(ArrayTypeNode node)
    {
        base.VisitArrayType(node);

        var resolvedType = ConvertTypeNodeToTypeInfo(node);
        node.SetInferredType(resolvedType);
        if (node.ElementType != null)
        {
            var elementInfo = ConvertTypeNodeToTypeInfo(node.ElementType);
            node.ElementType.SetInferredType(elementInfo);
        }
    }

    /// <summary>
    /// Visit array access and reduce dimensionality
    /// </summary>
    public override void VisitArrayAccess(ArrayAccessNode node)
    {
        base.VisitArrayAccess(node);

        // Get the array type and reduce dimensionality
        var arrayType = GetInferredType(node.Array);
        if (arrayType != null)
        {
            // Check if we're trying to index a non-array type
            // If invalid, record error on THIS node and propagate Unknown (not Invalid)
            if (arrayType is not ArrayTypeInfo && arrayType is not AnyTypeInfo && arrayType is not UnknownTypeInfo)
            {
                var invalid = new InvalidTypeInfo(
                    $"Cannot index type '{arrayType}' - indexing requires an array type");
                node.AddTypeError(new TypeError(invalid.Reason, node));
                SetInferredType(node, UnknownTypeInfo.Instance);
                return;
            }

            // Each index expression must be numeric (Number/Integer). Any/Unknown stay silent.
            foreach (var index in node.Indices)
            {
                var indexType = GetInferredType(index);
                if (indexType != null && !CanBeArrayIndex(indexType))
                {
                    var invalid = new InvalidTypeInfo(
                        $"Array index must be numeric; cannot use type '{indexType}'");
                    index.AddTypeError(new TypeError(invalid.Reason, index));
                }
            }

            // Each index reduces dimensionality by 1
            var elementType = arrayType;
            for (int i = 0; i < node.Indices.Count; i++)
            {
                elementType = ReduceDimensionality(elementType);
            }

            // Array access is assignable if the base array is assignable
            // e.g., &arr[1] is assignable if &arr is a variable
            if (arrayType.IsAssignable)
            {
                elementType = elementType.WithAssignable(true);
            }

            SetInferredType(node, elementType);
        }
        else
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
        }
    }

    /// <summary>
    /// Visit binary operation and apply type promotion rules
    /// </summary>
    public override void VisitBinaryOperation(BinaryOperationNode node)
    {
        base.VisitBinaryOperation(node);

        var leftType = GetInferredType(node.Left);
        var rightType = GetInferredType(node.Right);

        if (leftType == null || rightType == null)
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
            return;
        }

        // Validate operation compatibility before inferring result type
        // If invalid, record error on THIS node and propagate Unknown (not Invalid)
        if (IsArithmeticOperator(node.Operator))
        {
            if (!CanPerformArithmetic(leftType) || !CanPerformArithmetic(rightType))
            {
                var invalid = new InvalidTypeInfo(
                    $"Cannot perform arithmetic operation '{OperatorToString(node.Operator)}' on types '{leftType}' and '{rightType}'");
                node.AddTypeError(new TypeError(invalid.Reason, node));
                SetInferredType(node, UnknownTypeInfo.Instance);
                return;
            }
        }
        else if (IsLogicalOperator(node.Operator))
        {
            if (!CanPerformLogical(leftType) || !CanPerformLogical(rightType))
            {
                var invalid = new InvalidTypeInfo(
                    $"Cannot perform logical operation '{OperatorToString(node.Operator)}' on types '{leftType}' and '{rightType}'");
                node.AddTypeError(new TypeError(invalid.Reason, node));
                SetInferredType(node, UnknownTypeInfo.Instance);
                return;
            }
        }
        // Concatenation operator (|) accepts any type and coerces to string at runtime
        // No type checking needed for BinaryOperator.Concatenate

        // Special handling for date/time arithmetic (Add and Subtract only)
        TypeInfo resultType;
        if (node.Operator is BinaryOperator.Add or BinaryOperator.Subtract)
        {
            resultType = InferDateTimeArithmetic(node, leftType, rightType);
        }
        else
        {
            // Non-date/time operators use standard logic
            resultType = node.Operator switch
            {
                // Comparison operators always return boolean
                BinaryOperator.Equal or BinaryOperator.NotEqual or
                BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual or
                BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual
                    => PrimitiveTypeInfo.Boolean,

                // Logical operators return boolean
                BinaryOperator.And or BinaryOperator.Or
                    => PrimitiveTypeInfo.Boolean,

                // String concatenation (pipe operator) always returns string
                BinaryOperator.Concatenate
                    => PrimitiveTypeInfo.String,

                // Other arithmetic operators - apply type promotion
                BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Power
                    => leftType.GetCommonType(rightType),

                _ => leftType.GetCommonType(rightType)
            };
        }

        SetInferredType(node, resultType);
    }

    /// <summary>
    /// Visit unary operation and infer result type
    /// </summary>
    public override void VisitUnaryOperation(UnaryOperationNode node)
    {
        base.VisitUnaryOperation(node);

        var operandType = GetInferredType(node.Operand);
        if (operandType == null)
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
            return;
        }

        // Validate operand types (mirrors binary And/Or and arithmetic checks).
        // If invalid, record error on THIS node and propagate Unknown (not Invalid).
        if (node.Operator == UnaryOperator.Not)
        {
            if (!CanPerformLogical(operandType))
            {
                var invalid = new InvalidTypeInfo(
                    $"Cannot apply logical operator 'Not' to type '{operandType}'");
                node.AddTypeError(new TypeError(invalid.Reason, node));
                SetInferredType(node, UnknownTypeInfo.Instance);
                return;
            }
        }
        else if (node.Operator == UnaryOperator.Negate)
        {
            // Unary negate is Number/Integer only; date/time arithmetic uses binary ops.
            if (!CanPerformUnaryNegate(operandType))
            {
                var invalid = new InvalidTypeInfo(
                    $"Cannot apply unary negate to type '{operandType}'");
                node.AddTypeError(new TypeError(invalid.Reason, node));
                SetInferredType(node, UnknownTypeInfo.Instance);
                return;
            }
        }

        TypeInfo resultType = node.Operator switch
        {
            UnaryOperator.Not => PrimitiveTypeInfo.Boolean,
            UnaryOperator.Negate => operandType,  // Preserve numeric type
            UnaryOperator.Reference => ResolveReferenceOperatorType(node),
            _ => operandType
        };

        SetInferredType(node, resultType);
    }

    /// <summary>
    /// @expr — if expr is a pure string literal "Record.NAME" / "Field.X", produce a typed
    /// ReferenceTypeInfo. Concatenation and other dynamic forms stay @ANY (Dynamic Reference).
    /// </summary>
    private static TypeInfo ResolveReferenceOperatorType(UnaryOperationNode node)
    {
        if (TryGetStringLiteral(node.Operand, out var text)
            && TryParseDefinitionReferenceString(text, out var category, out var name))
        {
            var fullReference = $"{category.GetTypeName()}.{name}";
            return new ReferenceTypeInfo(category, name, fullReference);
        }

        return new ReferenceTypeInfo(PeopleCodeType.Any, "Dynamic Reference", "Dynamic Reference");
    }

    /// <summary>
    /// A narrowing conflict on this use replaces the generic invalid-member error (spec §8, §10),
    /// unless the target's type came from an annotation or assignment: then the normal error applies.
    /// </summary>
    private void ReportNarrowingConflict(MemberAccessNode node)
    {
        if (_narrowing?.ConflictAt(node) is not { } conflict
            || node.Target is not IdentifierNode target
            || target.GetRefinementSource() is { Kind: not RefinementKind.Narrowing })
            return;

        node.AddTypeWarning(new TypeError(conflict.Message, node));
        node.MarkNarrowingConflict();
    }

    /// <summary>
    /// Visit member access (property access, not method call)
    /// </summary>
    public override void VisitMemberAccess(MemberAccessNode node)
    {
        base.VisitMemberAccess(node);

        ReportNarrowingConflict(node);

        // Check for special reference keywords (Record.X, Field.Y, etc.)
        // These create ReferenceTypeInfo, not builtin types
        if (node.Target is IdentifierNode identifier &&
            !identifier.Name.StartsWith("&") &&
            !identifier.Name.StartsWith("%") &&
            ReferenceTypeInfo.IsSpecialReferenceKeyword(identifier.Name))
        {
            // Special keyword like Record.MY_RECORD, Field.MY_FIELD
            var referenceCategory = ReferenceTypeInfo.GetReferenceCategoryType(identifier.Name);
            var fullReference = $"{identifier.Name}.{node.MemberName}";
            var refType = new ReferenceTypeInfo(referenceCategory, node.MemberName, fullReference);
            SetInferredType(node, refType);
            return;
        }

        // Normal member access - get target type and resolve member
        var objectType = GetInferredType(node.Target);
        if (objectType != null)
        {
            // Further member access on a definition reference promotes it to an instance:
            //   Record.REC.<member> → record(REC) then property or field
            //   Field.FLD.<member>  → field(<defaultRecord>.FLD) then Field members / .Value
            // First hop alone (Record.REC / Field.FLD) stays ReferenceTypeInfo for APIs like CreateRecord.
            if (objectType is ReferenceTypeInfo refType)
            {
                if (refType.ReferenceCategory == PeopleCodeType.Record ||
                    refType.ReferenceCategory == PeopleCodeType.Scroll)
                {
                    var promotedRecord = new RecordTypeInfo(refType.ReferencedName, _typeResolver);
                    // Re-stamp target so hover on the middle of Record.REC.IsChanged shows record
                    SetInferredType(node.Target, promotedRecord);
                    objectType = promotedRecord;
                }
                else if (refType.ReferenceCategory == PeopleCodeType.Field)
                {
                    var promotedField = new FieldTypeInfo(
                        _defaultRecordName ?? string.Empty,
                        refType.ReferencedName,
                        _typeResolver).WithAssignable(true);
                    SetInferredType(node.Target, promotedField);
                    objectType = promotedField;
                }
            }

            // Safety net: if a bare identifier was still typed as Field (e.g. parent not wired yet)
            // but is used as NAME.member, reinterpret NAME as a record.
            if (objectType is FieldTypeInfo &&
                node.Target is IdentifierNode bareTarget &&
                !bareTarget.Name.StartsWith("&") &&
                !bareTarget.Name.StartsWith("%") &&
                !ReferenceTypeInfo.IsSpecialReferenceKeyword(bareTarget.Name))
            {
                // Only reinterpret when the member is NOT a known Field property (Value, Name, ...)
                // so standalone-field.Value after a mis-parent still works if it was correctly a field.
                // For NAME.member where NAME was wrongly a field, members like ISCRIPT1 are not Field props.
                var fieldProp = PeopleCodeTypeDatabase.GetProperty("field", node.MemberName);
                if (fieldProp == null)
                {
                    var asRecord = new RecordTypeInfo(bareTarget.Name, _typeResolver) { DirectRecordAccess = true };
                    SetInferredType(bareTarget, asRecord);
                    objectType = asRecord;
                }
            }

            if (objectType is FieldTypeInfo fieldType && node.MemberName == "Value")
            {
                // field.Value is assignable (can be used for out parameters)
                var memberType = fieldType.GetFieldDataType();
                memberType = memberType.WithAssignable(true);
                SetInferredType(node, memberType);
                return;
            }

            /* Literal / named record access:
             * DirectRecordAccess (bare REC.x): members are always fields — no REC.Delete().
             * Full record (e.g. Record.REC.x or &rec.x as RecordTypeInfo): try real Record
             * properties first (IsChanged), then fall back to field names.
             */

            TypeInfo? rightHandType = null;

            if (objectType is RecordTypeInfo recordType &&
                recordType.RecordName != null)
            {
                if (!recordType.DirectRecordAccess)
                {
                    // Full Record object (&rec / Record.REC / GetRecord) — prefer known
                    // properties over field names. Field members stay Field objects
                    // (no implicit .Value); scalar use requires explicit .Value.
                    var realProp = PeopleCodeTypeDatabase.GetProperty("record", node.MemberName);
                    if (realProp != null)
                    {
                        rightHandType = ConvertPropertyInfoToTypeInfo(realProp).WithAssignable(true);
                    }
                    else
                    {
                        rightHandType = new FieldTypeInfo(recordType.RecordName, node.MemberName, _typeResolver)
                            .WithAssignable(true);
                    }
                }
                else
                {
                    // Bare REC.FIELD (DirectRecordAccess):
                    //   REC.FIELD.Member  → intermediate Field (so .AriaAttributes / .Value work)
                    //   REC.FIELD         → terminal value (implicit .Value / field data type)
                    // Record.REC.FIELD is NOT DirectRecordAccess and stays Field (above).
                    var bufferField = (FieldTypeInfo)new FieldTypeInfo(recordType.RecordName, node.MemberName, _typeResolver)
                        .WithAssignable(true);
                    bool isIntermediateFieldAccess =
                        node.Parent is MemberAccessNode parentMa && ReferenceEquals(parentMa.Target, node);
                    rightHandType = isIntermediateFieldAccess
                        ? bufferField
                        : BufferFieldValue(bufferField);
                }
            }
            else
            {
                // Resolve member as property (not method call)
                rightHandType = ResolveMemberAccessReturnType(objectType, node.MemberName, isMethodCall: false, parameterTypes: null);
                rightHandType = rightHandType.WithAssignable(true);
            }

            // If result is bare Field type and target is a Record instance (not RecordTypeInfo path above),
            // create FieldTypeInfo for implicit .Value support when we have a record identifier name.
            if (rightHandType.PeopleCodeType == PeopleCodeType.Field &&
                rightHandType is not FieldTypeInfo &&
                objectType.PeopleCodeType == PeopleCodeType.Record &&
                node.Target is IdentifierNode recordIdentifier)
            {
                rightHandType = new FieldTypeInfo(recordIdentifier.Name, node.MemberName, _typeResolver).WithAssignable(true);
            }

            /* This handles the pattern &rowset.GetRow(1).RECORD_NAME) */
            if (objectType.PeopleCodeType == PeopleCodeType.Row && rightHandType.PeopleCodeType == PeopleCodeType.Record)
            {
                // Record/Row/Field can be used for out parameters
                rightHandType = new RecordTypeInfo(node.MemberName, _typeResolver);
            }

            SetInferredType(node, rightHandType);
        }
        else
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
        }
    }

    /// <summary>
    /// Visit function call and resolve return type
    /// </summary>
    public override void VisitFunctionCall(FunctionCallNode node)
    {
        base.VisitFunctionCall(node);

        var parameterTypes = new TypeInfo[node.Arguments.Count];
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            parameterTypes[i] = GetInferredType(node.Arguments[i]) ?? UnknownTypeInfo.Instance;
        }

        var functionInfo = ResolveFunctionInfo(node, parameterTypes);
        if (functionInfo != null)
        {
            node.SetFunctionInfo(functionInfo);

            TypeInfo? objectType = null;
            if (node.Function is MemberAccessNode memberAccess)
            {
                objectType = GetInferredType(memberAccess.Target);
            }
            else if (node.Function is IdentifierNode ident
                && ident.Name.Equals("createarray", StringComparison.OrdinalIgnoreCase)
                && parameterTypes.Length == 0)
            {
                /* Artificially put in an "any" as the parameter type */
                parameterTypes = [AnyTypeInfo.Instance];
            }

            if (objectType is AppClassTypeInfo receiverClass)
                StampParameterAnnotations(node, receiverClass, functionInfo);

            var returnType = objectType is AppClassTypeInfo appClassType
                && InheritedReturnAnnotation(appClassType, functionInfo) is { } inherited
                ? ConvertTypeWithDimensionalityToTypeInfo(inherited)
                : ConvertFunctionInfoToTypeInfo(functionInfo, objectType, parameterTypes, node.Arguments);
            SetInferredType(node, returnType);
        }
        else
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
        }
    }

    /// <summary>
    /// Visit object creation expression and infer type from the class being created
    /// </summary>
    public override void VisitObjectCreation(ObjectCreationNode node)
    {
        // Let the base visitor handle traversal and any other logic first.
        base.VisitObjectCreation(node);

        // The type of the expression is the type of the object being created.
        var inferredType = ConvertTypeNodeToTypeInfo(node.Type);
        SetInferredType(node, inferredType);

        //var qualifiedName = node.Type
        if (node.Type is AppClassTypeNode act)
        {
            var qualifiedName = inferredType.Name;

            var sourceMetadata = GetClassMetadata(qualifiedName);

            // Look up the constructor 
            if (sourceMetadata != null &&
                sourceMetadata.Constructor != null && sourceMetadata.Constructor.Name.Equals(act.ClassName, StringComparison.OrdinalIgnoreCase))
            {
                node.SetFunctionInfo(sourceMetadata.Constructor);
            }

        }

    }

    /// <summary>
    /// Visit parenthesized expression and propagate inner type
    /// </summary>
    public override void VisitParenthesized(ParenthesizedExpressionNode node)
    {
        base.VisitParenthesized(node);

        // Propagate the inner expression's type to the parenthesized node
        var innerType = GetInferredType(node.Expression);
        if (innerType != null)
        {
            SetInferredType(node, innerType);
        }
        else
        {
            SetInferredType(node, UnknownTypeInfo.Instance);
        }
    }

    /// <summary>
    /// Visit type cast (AS) expression and infer the target type
    /// </summary>
    public override void VisitTypeCast(TypeCastNode node)
    {
        base.VisitTypeCast(node);

        // The result type of a cast is always the target type
        var targetType = ConvertTypeNodeToTypeInfo(node.TargetType);
        SetInferredType(node, targetType);
    }

    #endregion

    #region Operation Validation Helpers

    /// <summary>
    /// Check if an operator is an arithmetic operator
    /// </summary>
    private bool IsArithmeticOperator(BinaryOperator op) =>
        op is BinaryOperator.Add or BinaryOperator.Subtract or
              BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Power;

    /// <summary>
    /// Check if an operator is a logical operator
    /// </summary>
    private bool IsLogicalOperator(BinaryOperator op) =>
        op is BinaryOperator.And or BinaryOperator.Or;

    /// <summary>
    /// Check if a type can be used in arithmetic operations
    /// Includes numeric types (Number, Integer) and date/time types (Date, Time, DateTime)
    /// </summary>
    // Field objects are not numeric operands (compiler: "invalid numeric operand").
    // Buffer REC.FIELD is already collapsed to its data type at inference time.
    private bool CanPerformArithmetic(TypeInfo type) =>
        type is PrimitiveTypeInfo { PeopleCodeType:
            PeopleCodeType.Number or
            PeopleCodeType.Integer or
            PeopleCodeType.Date or
            PeopleCodeType.Time or
            PeopleCodeType.DateTime } ||
        type is AnyTypeInfo ||
        type is UnknownTypeInfo ||
        type is ReferenceTypeInfo rti && rti.ReferenceCategory == PeopleCodeType.Any;

    /// <summary>
    /// Check if a type can be used in logical operations
    /// </summary>
    private bool CanPerformLogical(TypeInfo type) =>
        type is PrimitiveTypeInfo { PeopleCodeType: PeopleCodeType.Boolean } ||
        type is AnyTypeInfo ||
        type is UnknownTypeInfo;

    /// <summary>
    /// Check if a type can be used as an array index (Number/Integer, or Any/Unknown).
    /// Date/Time are not valid index types.
    /// </summary>
    private bool CanBeArrayIndex(TypeInfo type) =>
        type is PrimitiveTypeInfo { PeopleCodeType: PeopleCodeType.Number or PeopleCodeType.Integer } ||
        type is AnyTypeInfo ||
        type is UnknownTypeInfo ||
        type is ReferenceTypeInfo rti && rti.ReferenceCategory == PeopleCodeType.Any;

    /// <summary>
    /// Check if a type can be used with unary negate (Number/Integer only — not date/time).
    /// </summary>
    private bool CanPerformUnaryNegate(TypeInfo type) =>
        type is PrimitiveTypeInfo { PeopleCodeType: PeopleCodeType.Number or PeopleCodeType.Integer } ||
        type is AnyTypeInfo ||
        type is UnknownTypeInfo ||
        type is ReferenceTypeInfo rti && rti.ReferenceCategory == PeopleCodeType.Any;

    /// <summary>
    /// Infer the result type for date/time arithmetic operations
    /// Handles special rules for Add and Subtract with Date, Time, and DateTime types
    /// </summary>
    private TypeInfo InferDateTimeArithmetic(BinaryOperationNode node, TypeInfo leftType, TypeInfo rightType)
    {
        // Field objects do not participate in date/time arithmetic; buffer REC.FIELD
        // is already typed as the field data type when terminal.
        var actualLeftType = leftType;
        var actualRightType = rightType;

        // Early return for Any/Unknown/Ref-Any types - bypass date/time validation
        // If any operand is Unknown, return Unknown (Unknown takes precedence)
        if (actualLeftType is UnknownTypeInfo || actualRightType is UnknownTypeInfo)
        {
            return UnknownTypeInfo.Instance;
        }
        // If any operand is Any or Ref-Any, return Any
        if (actualLeftType is AnyTypeInfo || actualRightType is AnyTypeInfo ||
            (actualLeftType is ReferenceTypeInfo leftRefType && leftRefType.ReferenceCategory == PeopleCodeType.Any) ||
            (actualRightType is ReferenceTypeInfo rightRefType && rightRefType.ReferenceCategory == PeopleCodeType.Any))
        {
            return AnyTypeInfo.Instance;
        }

        var left = (actualLeftType as PrimitiveTypeInfo)?.PeopleCodeType;
        var right = (actualRightType as PrimitiveTypeInfo)?.PeopleCodeType;

        // Both must be primitive types for date/time arithmetic
        if (!left.HasValue || !right.HasValue)
        {
            return leftType.GetCommonType(rightType);
        }

        // Apply date/time arithmetic rules
        var isAdd = node.Operator == BinaryOperator.Add;
        var isSubtract = node.Operator == BinaryOperator.Subtract;

        // time + number => time (both orders)
        if (isAdd && ((left == PeopleCodeType.Time && right == PeopleCodeType.Number) ||
                      (left == PeopleCodeType.Number && right == PeopleCodeType.Time)))
        {
            return PrimitiveTypeInfo.Time;
        }

        // time - number => time (only left to right)
        if (isSubtract && left == PeopleCodeType.Time && right == PeopleCodeType.Number)
        {
            return PrimitiveTypeInfo.Time;
        }

        // date + number => date (both orders)
        if (isAdd && ((left == PeopleCodeType.Date && right == PeopleCodeType.Number) ||
                      (left == PeopleCodeType.Number && right == PeopleCodeType.Date)))
        {
            return PrimitiveTypeInfo.Date;
        }

        // date - number => date (only left to right)
        if (isSubtract && left == PeopleCodeType.Date && right == PeopleCodeType.Number)
        {
            return PrimitiveTypeInfo.Date;
        }

        // date - date => number
        if (isSubtract && left == PeopleCodeType.Date && right == PeopleCodeType.Date)
        {
            return PrimitiveTypeInfo.Number;
        }

        // time - time => number
        if (isSubtract && left == PeopleCodeType.Time && right == PeopleCodeType.Time)
        {
            return PrimitiveTypeInfo.Number;
        }

        // date + time => datetime (both orders)
        if (isAdd && ((left == PeopleCodeType.Date && right == PeopleCodeType.Time) ||
                      (left == PeopleCodeType.Time && right == PeopleCodeType.Date)))
        {
            return PrimitiveTypeInfo.DateTime;
        }

        // datetime - datetime => number
        if (isSubtract && left == PeopleCodeType.DateTime && right == PeopleCodeType.DateTime)
        {
            return PrimitiveTypeInfo.Number;
        }

        // datetime - number => datetime
        if (isSubtract && left == PeopleCodeType.DateTime && right == PeopleCodeType.Number)
        {
            return PrimitiveTypeInfo.DateTime;
        }

        // datetime + number => datetime
        if (isAdd && left == PeopleCodeType.DateTime && right == PeopleCodeType.Number)
        {
            return PrimitiveTypeInfo.DateTime;
        }


        // Invalid combinations - generate type errors

        // datetime - time is not allowed
        if (isSubtract && left == PeopleCodeType.DateTime && right == PeopleCodeType.Time)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract time from datetime - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // time - datetime is not allowed
        if (isSubtract && left == PeopleCodeType.Time && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract datetime from time - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // datetime + datetime is not allowed
        if (isAdd && left == PeopleCodeType.DateTime && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add datetime to datetime - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // datetime + time is not allowed (date + time is allowed, but datetime + time is not)
        if (isAdd && left == PeopleCodeType.DateTime && right == PeopleCodeType.Time)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add time to datetime - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // time + datetime is not allowed
        if (isAdd && left == PeopleCodeType.Time && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add datetime to time - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // datetime + date is not allowed
        if (isAdd && left == PeopleCodeType.DateTime && right == PeopleCodeType.Date)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add date to datetime - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // date + datetime is not allowed
        if (isAdd && left == PeopleCodeType.Date && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add datetime to date - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // datetime - date is not allowed
        if (isSubtract && left == PeopleCodeType.DateTime && right == PeopleCodeType.Date)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract date from datetime - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // date - datetime is not allowed
        if (isSubtract && left == PeopleCodeType.Date && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract datetime from date - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // date - time is not allowed
        if (isSubtract && left == PeopleCodeType.Date && right == PeopleCodeType.Time)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract time from date - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // time - date is not allowed
        if (isSubtract && left == PeopleCodeType.Time && right == PeopleCodeType.Date)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot subtract date from time - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // number + datetime is not allowed
        if (isAdd && left == PeopleCodeType.Number && right == PeopleCodeType.DateTime)
        {
            var invalid = new InvalidTypeInfo(
                "Cannot add datetime to number - this operation is not supported");
            node.AddTypeError(new TypeError(invalid.Reason, node));
            return UnknownTypeInfo.Instance;
        }

        // If we get here, it's a normal numeric operation
        return leftType.GetCommonType(rightType);
    }

    /// <summary>
    /// Convert a binary operator to its string representation
    /// </summary>
    private string OperatorToString(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Power => "**",
        BinaryOperator.Concatenate => "|",
        BinaryOperator.And => "And",
        BinaryOperator.Or => "Or",
        _ => op.ToString()
    };

    #endregion
}
