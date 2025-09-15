using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Validation logic for reference type expressions
/// </summary>
public static class ReferenceTypeValidation
{
    /// <summary>
    /// Validates a member access expression as a reference type (e.g., HTML.FOO, SQL.BAR)
    /// </summary>
    /// <param name="memberAccess">The member access node to validate</param>
    /// <returns>Validation result with details</returns>
    public static ReferenceValidationResult ValidateStaticReference(MemberAccessNode memberAccess)
    {
        if (memberAccess == null)
            return ReferenceValidationResult.Invalid("Member access node is null");

        // Check if the target is an identifier (static reference)
        if (memberAccess.Target is not IdentifierNode identifier)
            return ReferenceValidationResult.Invalid("Reference target must be an identifier for static references");

        // Check if the identifier is a valid reference type
        if (!ReferenceTypeIdentifierUtils.IsValidReferenceIdentifier(identifier.Name))
        {
            return ReferenceValidationResult.Invalid(
                $"'{identifier.Name}' is not a valid reference type identifier. " +
                $"Valid identifiers are: {string.Join(", ", ReferenceTypeIdentifierUtils.GetAllValidIdentifiers())}");
        }

        // Parse the identifier
        if (!ReferenceTypeIdentifierUtils.TryParseIdentifier(identifier.Name, out var referenceType))
            return ReferenceValidationResult.Invalid($"Failed to parse reference identifier '{identifier.Name}'");

        // Validate member name is not empty
        if (string.IsNullOrWhiteSpace(memberAccess.MemberName))
            return ReferenceValidationResult.Invalid("Reference member name cannot be empty");

        return ReferenceValidationResult.Valid(referenceType, memberAccess.MemberName);
    }

    /// <summary>
    /// Validates a unary operation expression as a dynamic reference (e.g., @("HTML.FOO"))
    /// </summary>
    /// <param name="unaryOp">The unary operation node to validate</param>
    /// <returns>Validation result with details</returns>
    public static ReferenceValidationResult ValidateDynamicReference(UnaryOperationNode unaryOp)
    {
        if (unaryOp == null)
            return ReferenceValidationResult.Invalid("Unary operation node is null");

        // Check if it's a reference operator (@)
        if (unaryOp.Operator != UnaryOperator.Reference)
            return ReferenceValidationResult.Invalid("Unary operation must use the reference operator (@)");

        // The operand should evaluate to a string expression
        // We can't validate the actual string content at compile time for dynamic references,
        // but we can ensure the structure is correct
        if (unaryOp.Operand == null)
            return ReferenceValidationResult.Invalid("Dynamic reference operand cannot be null");

        return ReferenceValidationResult.ValidDynamic("Dynamic reference");
    }

    /// <summary>
    /// Determines if an expression node represents a reference type
    /// </summary>
    /// <param name="expression">The expression to check</param>
    /// <returns>True if it's a reference expression, false otherwise</returns>
    public static bool IsReferenceExpression(ExpressionNode expression)
    {
        return expression switch
        {
            // Static reference: HTML.FOO, SQL.BAR, etc.
            MemberAccessNode memberAccess when memberAccess.Target is IdentifierNode identifier
                && ReferenceTypeIdentifierUtils.IsValidReferenceIdentifier(identifier.Name) => true,

            // Dynamic reference: @("HTML.FOO"), @(&var), etc.
            UnaryOperationNode unaryOp when unaryOp.Operator == UnaryOperator.Reference => true,

            _ => false
        };
    }

    /// <summary>
    /// Gets the reference type identifier from a static reference expression
    /// </summary>
    /// <param name="memberAccess">The member access node</param>
    /// <returns>The reference type identifier, or null if not a valid static reference</returns>
    public static ReferenceTypeIdentifier? GetReferenceTypeIdentifier(MemberAccessNode memberAccess)
    {
        if (memberAccess?.Target is IdentifierNode identifier &&
            ReferenceTypeIdentifierUtils.TryParseIdentifier(identifier.Name, out var referenceType))
        {
            return referenceType;
        }

        return null;
    }

    /// <summary>
    /// Gets the member name from a static reference expression
    /// </summary>
    /// <param name="memberAccess">The member access node</param>
    /// <returns>The member name, or null if not a valid static reference</returns>
    public static string? GetReferenceMemberName(MemberAccessNode memberAccess)
    {
        if (memberAccess?.Target is IdentifierNode identifier &&
            ReferenceTypeIdentifierUtils.IsValidReferenceIdentifier(identifier.Name))
        {
            return memberAccess.MemberName;
        }

        return null;
    }
}

/// <summary>
/// Result of reference type validation
/// </summary>
public class ReferenceValidationResult
{
    /// <summary>
    /// Whether the reference is valid
    /// </summary>
    public bool IsValid { get; private set; }

    /// <summary>
    /// Whether this is a dynamic reference
    /// </summary>
    public bool IsDynamic { get; private set; }

    /// <summary>
    /// The reference type identifier (for static references)
    /// </summary>
    public ReferenceTypeIdentifier? ReferenceType { get; private set; }

    /// <summary>
    /// The member name (for static references)
    /// </summary>
    public string? MemberName { get; private set; }

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Additional context or description
    /// </summary>
    public string? Description { get; private set; }

    private ReferenceValidationResult() { }

    /// <summary>
    /// Creates a valid static reference result
    /// </summary>
    public static ReferenceValidationResult Valid(ReferenceTypeIdentifier referenceType, string memberName)
    {
        return new ReferenceValidationResult
        {
            IsValid = true,
            IsDynamic = false,
            ReferenceType = referenceType,
            MemberName = memberName,
            Description = $"{referenceType}.{memberName}"
        };
    }

    /// <summary>
    /// Creates a valid dynamic reference result
    /// </summary>
    public static ReferenceValidationResult ValidDynamic(string description)
    {
        return new ReferenceValidationResult
        {
            IsValid = true,
            IsDynamic = true,
            Description = description
        };
    }

    /// <summary>
    /// Creates an invalid reference result
    /// </summary>
    public static ReferenceValidationResult Invalid(string errorMessage)
    {
        return new ReferenceValidationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }

    public override string ToString()
    {
        if (!IsValid)
            return $"Invalid: {ErrorMessage}";

        if (IsDynamic)
            return $"Valid Dynamic: {Description}";

        return $"Valid Static: {ReferenceType}.{MemberName}";
    }
}