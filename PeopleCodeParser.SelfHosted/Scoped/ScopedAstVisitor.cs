using System.Collections;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Scoped.Models;
using PeopleCodeParser.SelfHosted.Scoped.Utilities;

namespace PeopleCodeParser.SelfHosted.Scoped;

public abstract class ScopedAstVisitor<T> : AstVisitorBase
{
    protected readonly Stack<Dictionary<string, T>> scopeStack = new();
    protected readonly Stack<Dictionary<string, VariableInfo>> variableScopeStack = new();
    protected readonly Stack<ScopeInfo> scopeInfoStack = new();

    protected ScopedAstVisitor()
    {
        var globalScopeInfo = new ScopeInfo(ScopeType.Global, "Global");
        scopeInfoStack.Push(globalScopeInfo);
        scopeStack.Push(new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase));
        variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.InvariantCultureIgnoreCase));
    }

    protected Dictionary<string, T> GetCurrentScope() => scopeStack.Peek();

    protected Dictionary<string, VariableInfo> GetCurrentVariableScope() => variableScopeStack.Peek();

    protected ScopeInfo GetCurrentScopeInfo() => scopeInfoStack.Peek();

    protected void AddToCurrentScope(string key, T value)
    {
        var currentScope = GetCurrentScope();
        if (!currentScope.ContainsKey(key))
        {
            currentScope[key] = value;
        }
    }

    protected void ReplaceInFoundScope(string key, T newValue)
    {
        foreach (var scope in scopeStack)
        {
            if (scope.ContainsKey(key))
            {
                scope[key] = newValue;
                return;
            }
        }
    }

    protected bool TryFindInScopes(string key, out T? value)
    {
        value = default;
        foreach (var scope in scopeStack)
        {
            if (scope.TryGetValue(key, out value))
            {
                return true;
            }
        }
        return false;
    }

    protected void AddLocalVariable(string name, string type, int line, (int Start, int Stop) span)
    {
        var currentScope = variableScopeStack.Peek();
        if (!currentScope.ContainsKey(name))
        {
            var variableInfo = new VariableInfo(name, type, line, span);
            currentScope[name] = variableInfo;
            OnVariableDeclared(variableInfo);
        }
    }
    
    protected void AddLocalVariable(string name, string type, SourceSpan sourceSpan)
    {
        var currentScope = variableScopeStack.Peek();
        if (!currentScope.ContainsKey(name))
        {
            var variableInfo = new VariableInfo(name, type, sourceSpan);
            currentScope[name] = variableInfo;
            OnVariableDeclared(variableInfo);
        }
    }

    protected bool TryGetVariableInfo(string name, out VariableInfo? info)
    {
        info = null;
        foreach (var scope in variableScopeStack)
        {
            if (scope.TryGetValue(name, out info))
            {
                return true;
            }
        }
        return false;
    }

    protected IEnumerable<VariableInfo> GetVariablesInCurrentScope()
    {
        if (variableScopeStack.Count == 0) return Enumerable.Empty<VariableInfo>();
        return variableScopeStack.Peek().Values;
    }

    protected IEnumerable<VariableInfo> GetAllVariablesInScope()
    {
        var allVariables = new List<VariableInfo>();
        foreach (var scope in variableScopeStack.Reverse())
        {
            allVariables.AddRange(scope.Values);
        }
        return allVariables;
    }

    protected void MarkVariableAsUsed(string name)
    {
        foreach (var scope in variableScopeStack)
        {
            if (scope.TryGetValue(name, out var info))
            {
                info.Used = true;
                OnVariableUsed(info);
                break;
            }
        }
    }

    private void EnterScope(ScopeType scopeType, string scopeName)
    {
        var parentScope = GetCurrentScopeInfo();
        var newScopeInfo = new ScopeInfo(scopeType, scopeName, parentScope);
        
        scopeInfoStack.Push(newScopeInfo);
        scopeStack.Push(new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase));
        variableScopeStack.Push(new Dictionary<string, VariableInfo>(StringComparer.InvariantCultureIgnoreCase));
        
        OnEnterScope();
    }

    private void ExitScope()
    {
        if (scopeStack.Count > 1 && variableScopeStack.Count > 1 && scopeInfoStack.Count > 1)
        {
            var scope = scopeStack.Pop();
            var varScope = variableScopeStack.Pop();
            var scopeInfo = scopeInfoStack.Pop();
            
            OnExitScope(scope, varScope);
        }
    }

    public override void VisitMethod(MethodNode node)
    {
        EnterScope(ScopeType.Method, node.Name);
        
        AddMethodParameters(node);
        
        base.VisitMethod(node);
        
        ExitScope();
    }

    public override void VisitFunction(FunctionNode node)
    {
        EnterScope(ScopeType.Function, node.Name);
        
        AddFunctionParameters(node);
        
        base.VisitFunction(node);
        
        ExitScope();
    }

    public override void VisitProperty(PropertyNode node)
    {
        EnterScope(ScopeType.Property, node.Name);
        
        base.VisitProperty(node);
        
        ExitScope();
    }

    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        var typeName = AstTypeExtractor.GetTypeFromNode(node.Type);
        var sourceSpan = (node.SourceSpan.Start.Index, node.SourceSpan.End.Index);
        
        foreach (var variableName in node.VariableNames)
        {
            AddLocalVariable(variableName, typeName, node.SourceSpan.Start.Line, sourceSpan);
        }
        
        base.VisitLocalVariableDeclaration(node);
    }

    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        var typeName = AstTypeExtractor.GetTypeFromNode(node.Type);
        var sourceSpan = (node.SourceSpan.Start.Index, node.SourceSpan.End.Index);
        
        AddLocalVariable(node.VariableName, typeName, node.SourceSpan.Start.Line, sourceSpan);
        
        base.VisitLocalVariableDeclarationWithAssignment(node);
    }

    public override void VisitConstant(ConstantNode node)
    {
        var typeName = AstTypeExtractor.GetDefaultTypeForExpression(node.Value);
        var sourceSpan = (node.SourceSpan.Start.Index, node.SourceSpan.End.Index);
        
        AddLocalVariable(node.Name, $"Constant({typeName})", node.SourceSpan.Start.Line, sourceSpan);
        
        base.VisitConstant(node);
    }

    public override void VisitIdentifier(IdentifierNode node)
    {
        MarkVariableAsUsed(node.Name);
        
        base.VisitIdentifier(node);
    }

    private void AddMethodParameters(MethodNode method)
    {
        foreach (var parameter in method.Parameters)
        {
            var typeName = AstTypeExtractor.GetTypeFromNode(parameter.Type);
            var sourceSpan = (parameter.SourceSpan.Start.Index, parameter.SourceSpan.End.Index);
            
            AddLocalVariable(parameter.Name, $"Parameter({typeName})", parameter.SourceSpan.Start.Line, sourceSpan);
        }
    }

    private void AddFunctionParameters(FunctionNode function)
    {
        foreach (var parameter in function.Parameters)
        {
            var typeName = AstTypeExtractor.GetTypeFromNode(parameter.Type);
            var sourceSpan = (parameter.SourceSpan.Start.Index, parameter.SourceSpan.End.Index);
            
            AddLocalVariable(parameter.Name, $"Parameter({typeName})", parameter.SourceSpan.Start.Line, sourceSpan);
        }
    }

    public void Reset()
    {
        while (scopeStack.Count > 1)
        {
            var dict = scopeStack.Pop();
            dict.Clear();
        }

        while (variableScopeStack.Count > 1)
        {
            var dict = variableScopeStack.Pop();
            dict.Clear();
        }

        while (scopeInfoStack.Count > 1)
        {
            scopeInfoStack.Pop();
        }

        if (scopeStack.Count > 0) scopeStack.Peek().Clear();
        if (variableScopeStack.Count > 0) variableScopeStack.Peek().Clear();
    }

    protected virtual void OnVariableDeclared(VariableInfo varInfo) { }
    
    protected virtual void OnVariableUsed(VariableInfo varInfo) { }
    
    protected virtual void OnEnterScope() { }
    
    protected virtual void OnExitScope(Dictionary<string, T> scope, Dictionary<string, VariableInfo> variableScope) { }
}