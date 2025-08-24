using System;
using System.Collections.Generic;
using System.Linq;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Scoped.Models;
using PeopleCodeParser.SelfHosted.Scoped.Utilities;

namespace PeopleCodeParser.SelfHosted.Scoped;

/// <summary>
/// Visitor that identifies unused variables, parameters, and instance variables in PeopleCode.
/// This is a self-hosted equivalent to the AppRefiner's UnusedLocalVariableStyler.
/// </summary>
public class UnusedVariablesVisitor : ScopedAstVisitor<object>
{
    private const uint HIGHLIGHT_COLOR = 0x73737380; // Light gray text (no alpha)
    private readonly Dictionary<string, VariableInfo> instanceVariables = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly List<Indicator> indicators = new();
    private string? currentClassName;
    private string? currentMethodName;

    public List<Indicator> Indicators => indicators;

    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        
        // Process the program
        base.VisitProgram(node);
        
        // Check for unused variables in the global scope
        CheckForUnusedVariables(GetCurrentVariableScope(), "Unused global variable");
        
        // Check for unused instance variables
        foreach (var variable in instanceVariables.Values)
        {
            if (!variable.Used)
            {
                AddIndicator(variable, Indicator.IndicatorType.TEXTCOLOR, HIGHLIGHT_COLOR, "Unused instance variable");
            }
        }
    }

    public override void VisitAppClass(AppClassNode node)
    {
        currentClassName = node.Name;
        base.VisitAppClass(node);
        currentClassName = null;
    }

    public override void VisitVariable(VariableNode node)
    {
        var variableName = node.Name;
        
        // Handle different variable scopes
        if (node.Scope == VariableScope.Instance)
        {
            // This is an instance variable
            if (!instanceVariables.ContainsKey(variableName))
            {
                // Get the precise token for the variable name if available
                var nameInfo = node.NameInfos.FirstOrDefault(ni => ni.Name == variableName);
                var sourceSpan = nameInfo?.SourceSpan ?? node.SourceSpan;
                
                instanceVariables[variableName] = new VariableInfo(
                    variableName,
                    "Instance",
                    sourceSpan
                );
            }
            
            // Also handle additional names in multi-variable declarations
            foreach (var additionalName in node.AdditionalNames)
            {
                if (!instanceVariables.ContainsKey(additionalName))
                {
                    // Get the precise token for the additional variable name if available
                    var nameInfo = node.NameInfos.FirstOrDefault(ni => ni.Name == additionalName);
                    var sourceSpan = nameInfo?.SourceSpan ?? node.SourceSpan;
                    
                    instanceVariables[additionalName] = new VariableInfo(
                        additionalName,
                        "Instance",
                        sourceSpan
                    );
                }
            }
        }
        else if (node.Scope == VariableScope.Local || node.Scope == VariableScope.Global)
        {
            // Local or global variables are handled by the base ScopedAstVisitor
            // through LocalVariableDeclaration and LocalVariableDeclarationWithAssignment
            // but we need to handle the case when there are multiple variables declared
            foreach (var additionalName in node.AdditionalNames)
            {
                var typeName = node.Type.ToString();
                
                // Get the precise token for the additional variable name if available
                var nameInfo = node.NameInfos.FirstOrDefault(ni => ni.Name == additionalName);
                var sourceSpan = nameInfo?.SourceSpan ?? node.SourceSpan;
                
                AddLocalVariable(additionalName, typeName, sourceSpan);
            }
        }
        
        base.VisitVariable(node);
    }

    public override void VisitProperty(PropertyNode node)
    {
        // Track property as a special kind of instance variable (with & prefix)
        var propAsVar = $"&{node.Name}";
        
        if (!instanceVariables.ContainsKey(propAsVar))
        {
            instanceVariables[propAsVar] = new VariableInfo(
                propAsVar,
                "Property",
                node.SourceSpan
            );
        }
        
        base.VisitProperty(node);
    }

    public override void VisitMemberAccess(MemberAccessNode node)
    {
        // Check for %This dot access to instance variables (without the & prefix)
        var target = node.Target;
        if (target is IdentifierNode identNode && identNode.Name.Equals("%THIS", StringComparison.OrdinalIgnoreCase))
        {
            var memberName = node.MemberName;
            
            // In member access after %This, the variable name will be without & prefix
            // We need to find the matching instance variable with &
            string varNameWithPrefix = $"&{memberName}";
            
            // Check if this variable exists as an instance variable and mark as used
            if (instanceVariables.TryGetValue(varNameWithPrefix, out var instanceVar))
            {
                instanceVar.Used = true;
            }
        }
        
        base.VisitMemberAccess(node);
    }

    public override void VisitFor(ForStatementNode node)
    {
        // Mark the iterator variable as used if it's an instance variable
        string iteratorName = node.Variable;
        if (instanceVariables.TryGetValue(iteratorName, out var instanceVar))
        {
            instanceVar.Used = true;
        }
        
        base.VisitFor(node);
    }

    public override void VisitMethod(MethodNode node)
    {
        currentMethodName = node.Name;
        base.VisitMethod(node);
        currentMethodName = null;
    }

    protected override void OnExitScope(Dictionary<string, object> scope, Dictionary<string, VariableInfo> variableScope)
    {
        var scopeInfo = GetCurrentScopeInfo();
        string tooltipPrefix;
        
        switch (scopeInfo.Type)
        {
            case ScopeType.Method:
                tooltipPrefix = "Unused method parameter/variable";
                break;
            case ScopeType.Function:
                tooltipPrefix = "Unused function parameter/variable";
                break;
            case ScopeType.Property:
            case ScopeType.Getter:
            case ScopeType.Setter:
                tooltipPrefix = "Unused property variable";
                break;
            default:
                tooltipPrefix = "Unused local variable";
                break;
        }
        
        // Check for unused variables in the current scope
        CheckForUnusedVariables(variableScope, tooltipPrefix);
    }

    private void CheckForUnusedVariables(Dictionary<string, VariableInfo> variableScope, string tooltipPrefix)
    {
        foreach (var variable in variableScope.Values)
        {
            if (!variable.Used)
            {
                string tooltip = variable.Type.StartsWith("Parameter") 
                    ? $"Unused parameter: {variable.Name}" 
                    : $"{tooltipPrefix}: {variable.Name}";
                    
                AddIndicator(variable, Indicator.IndicatorType.TEXTCOLOR, HIGHLIGHT_COLOR, tooltip);
            }
        }
    }
    
    private void AddIndicator(VariableInfo varInfo, Indicator.IndicatorType type, uint color, string? tooltip = null)
    {
        // If we have a SourceSpan, use it directly for more accurate positioning
        if (varInfo.SourceSpan.HasValue)
        {
            indicators.Add(new Indicator
            {
                Start = varInfo.SourceSpan.Value.Start.ByteIndex,
                Length = varInfo.SourceSpan.Value.ByteLength,
                Type = type,
                Color = color,
                Tooltip = tooltip
            });
        }
        else
        {
            // Fall back to the stored byte span
            indicators.Add(new Indicator
            {
                Start = varInfo.Span.Start,
                Length = varInfo.Span.Stop - varInfo.Span.Start + 1,
                Type = type,
                Color = color,
                Tooltip = tooltip
            });
        }
    }

    public override void VisitIdentifier(IdentifierNode node)
    {
        // Mark the variable as used in the current scope
        MarkVariableAsUsed(node.Name);
        
        // Also check if this is an instance variable and mark it as used
        if (instanceVariables.TryGetValue(node.Name, out var instanceVar))
        {
            instanceVar.Used = true;
        }
        
        base.VisitIdentifier(node);
    }

    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        var typeName = AstTypeExtractor.GetTypeFromNode(node.Type);
        
        for (int i = 0; i < node.VariableNames.Count; i++)
        {
            var variableName = node.VariableNames[i];
            
            // Get the precise source span for this variable name if available
            var sourceSpan = node.SourceSpan;
            if (i < node.VariableNameInfos.Count)
            {
                var nameInfo = node.VariableNameInfos[i];
                if (nameInfo.SourceSpan.HasValue)
                {
                    sourceSpan = nameInfo.SourceSpan.Value;
                }
            }
            
            AddLocalVariable(variableName, typeName, sourceSpan);
        }
        
        base.VisitLocalVariableDeclaration(node);
    }

    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        var typeName = AstTypeExtractor.GetTypeFromNode(node.Type);
        
        // Get the precise source span for this variable name if available
        var sourceSpan = node.SourceSpan;
        if (node.VariableNameInfo?.SourceSpan.HasValue == true)
        {
            sourceSpan = node.VariableNameInfo.SourceSpan.Value;
        }
        
        AddLocalVariable(node.VariableName, typeName, sourceSpan);
        
        base.VisitLocalVariableDeclarationWithAssignment(node);
    }

    public override void VisitConstant(ConstantNode node)
    {
        var typeName = AstTypeExtractor.GetDefaultTypeForExpression(node.Value);
        
        AddLocalVariable(node.Name, $"Constant({typeName})", node.SourceSpan);
        
        base.VisitConstant(node);
    }
    
    public new void Reset()
    {
        base.Reset();
        instanceVariables.Clear();
        indicators.Clear();
        currentClassName = null;
    }

    // Override these methods to provide custom behavior for variable tracking events
    protected override void OnVariableDeclared(VariableInfo varInfo)
    {
        // Could add custom logic here if needed
    }
    
    protected override void OnVariableUsed(VariableInfo varInfo)
    {
        // Could add custom logic here if needed
    }
}
