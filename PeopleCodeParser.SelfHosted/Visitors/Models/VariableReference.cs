using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Visitors.Models;

/// <summary>
/// Represents the type of variable reference
/// </summary>
public enum ReferenceType
{
    /// <summary>
    /// Variable declaration - where the variable is first defined
    /// </summary>
    Declaration,
    
    /// <summary>
    /// Variable is being read/used in an expression
    /// </summary>
    Read,
    
    /// <summary>
    /// Variable is being written to/assigned
    /// </summary>
    Write,
    
    /// <summary>
    /// Parameter annotation in method signature
    /// </summary>
    ParameterAnnotation
}

/// <summary>
/// Represents a single reference to a variable at a specific location in the source code
/// </summary>
public class VariableReference
{
    /// <summary>
    /// Name of the variable being referenced
    /// </summary>
    public string VariableName { get; }
    
    /// <summary>
    /// Type of reference (declaration, read, write, parameter annotation)
    /// </summary>
    public ReferenceType ReferenceType { get; }
    
    /// <summary>
    /// Source location of this reference
    /// </summary>
    public SourceSpan SourceSpan { get; }
    
    /// <summary>
    /// Scope context where this reference occurs
    /// </summary>
    public ScopeContext Scope { get; }
    
    /// <summary>
    /// Token that represents this reference (if available)
    /// </summary>
    public Token? Token { get; }
    
    /// <summary>
    /// Line number where this reference occurs
    /// </summary>
    public int Line => SourceSpan.Start.Line;
    
    /// <summary>
    /// Column number where this reference occurs
    /// </summary>
    public int Column => SourceSpan.Start.Column;
    
    /// <summary>
    /// Additional context about this reference (e.g., "assignment target", "function call parameter")
    /// </summary>
    public string? Context { get; set; }

    public VariableReference(
        string variableName, 
        ReferenceType referenceType, 
        SourceSpan sourceSpan, 
        ScopeContext scope, 
        Token? token = null,
        string? context = null)
    {
        VariableName = variableName ?? throw new ArgumentNullException(nameof(variableName));
        ReferenceType = referenceType;
        SourceSpan = sourceSpan;
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Token = token;
        Context = context;
    }
    
    /// <summary>
    /// Creates a variable reference from a VariableNameInfo object
    /// </summary>
    public static VariableReference FromVariableNameInfo(
        VariableNameInfo nameInfo, 
        ReferenceType referenceType, 
        ScopeContext scope,
        string? context = null)
    {
        return new VariableReference(
            nameInfo.Name, 
            referenceType, 
            nameInfo.SourceSpan, 
            scope, 
            nameInfo.Token,
            context);
    }
    
    /// <summary>
    /// Creates a declaration reference (where the variable is first defined)
    /// </summary>
    public static VariableReference CreateDeclaration(
        string variableName, 
        SourceSpan sourceSpan, 
        ScopeContext scope, 
        Token? token = null)
    {
        return new VariableReference(variableName, ReferenceType.Declaration, sourceSpan, scope, token, "variable declaration");
    }
    
    /// <summary>
    /// Creates a read reference (variable used in expression)
    /// </summary>
    public static VariableReference CreateRead(
        string variableName, 
        SourceSpan sourceSpan, 
        ScopeContext scope, 
        Token? token = null,
        string? context = null)
    {
        return new VariableReference(variableName, ReferenceType.Read, sourceSpan, scope, token, context ?? "variable read");
    }
    
    /// <summary>
    /// Creates a write reference (variable assignment)
    /// </summary>
    public static VariableReference CreateWrite(
        string variableName, 
        SourceSpan sourceSpan, 
        ScopeContext scope, 
        Token? token = null,
        string? context = null)
    {
        return new VariableReference(variableName, ReferenceType.Write, sourceSpan, scope, token, context ?? "variable write");
    }
    
    /// <summary>
    /// Creates a parameter annotation reference
    /// </summary>
    public static VariableReference CreateParameterAnnotation(
        string variableName, 
        SourceSpan sourceSpan, 
        ScopeContext scope, 
        Token? token = null)
    {
        return new VariableReference(variableName, ReferenceType.ParameterAnnotation, sourceSpan, scope, token, "parameter annotation");
    }
    
    /// <summary>
    /// Checks if this reference is in the same scope as another reference
    /// </summary>
    public bool IsInSameScope(VariableReference other)
    {
        return Scope.Id == other.Scope.Id;
    }
    
    /// <summary>
    /// Checks if this reference can access the scope where another reference occurs
    /// </summary>
    public bool CanAccessScope(VariableReference other)
    {
        return Scope.CanAccessScope(other.Scope);
    }
    
    /// <summary>
    /// Gets the distance between this reference and another reference (useful for sorting by proximity)
    /// </summary>
    public int GetDistanceFrom(VariableReference other)
    {
        // Simple line-based distance calculation
        return Math.Abs(Line - other.Line);
    }

    public override string ToString()
    {
        var refTypeStr = ReferenceType switch
        {
            ReferenceType.Declaration => "DECL",
            ReferenceType.Read => "READ",
            ReferenceType.Write => "WRITE", 
            ReferenceType.ParameterAnnotation => "PARAM",
            _ => "REF"
        };
        
        return $"{VariableName} [{refTypeStr}] at {Line}:{Column} in {Scope.Name}";
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is not VariableReference other)
            return false;
            
        return VariableName.Equals(other.VariableName, StringComparison.OrdinalIgnoreCase) &&
               ReferenceType == other.ReferenceType &&
               SourceSpan == other.SourceSpan &&
               Scope.Id == other.Scope.Id;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(
            VariableName.ToLowerInvariant(),
            ReferenceType,
            SourceSpan,
            Scope.Id);
    }
}