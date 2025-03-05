using Antlr4.Runtime;
using AppRefiner.Linters.Models;
using AppRefiner.Refactors.CodeChanges;
using System.Collections.Generic;
using System.Linq;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Refactors
{
    public class RenameLocalVariable : ScopedRefactor<List<(int, int)>>
    {
        private readonly int cursorPosition;
        private readonly string newVariableName;
        private string? variableToRename;
        
        public RenameLocalVariable(int cursorPosition, string newVariableName)
        {
            this.cursorPosition = cursorPosition;
            this.newVariableName = newVariableName;
        }
        
        // Initialize the refactoring operation with source code and token stream
        public new void Initialize(string sourceText, ITokenStream tokenStream)
        {
            base.Initialize(sourceText, tokenStream);
            Reset();
            variableToRename = null;
        }
        
        // Called when a variable is declared
        protected override void OnVariableDeclared(VariableInfo varInfo)
        {
            // Add this declaration to our tracking
            AddOccurrence(varInfo.Name, varInfo.Span);
            
            // Check if cursor is within this variable declaration
            if (varInfo.Span.Item1 <= cursorPosition && cursorPosition <= varInfo.Span.Item2)
            {
                variableToRename = varInfo.Name;
            }
        }
        
        // Override the base method for tracking variable usage
        public override void EnterIdentUserVariable(IdentUserVariableContext context)
        {
            base.EnterIdentUserVariable(context);
            
            string varName = context.GetText();
            var span = (context.Start.StartIndex, context.Stop.StopIndex);
            
            // Check if variable exists in any scope before adding
            if (TryFindInScopes(varName, out _))
            {
                AddOccurrence(varName, span);
            }
            
            // Check if cursor is within this variable reference
            if (span.Item1 <= cursorPosition && cursorPosition <= span.Item2)
            {
                variableToRename = varName;
            }
        }
        
        // Helper method to add an occurrence to the appropriate scope
        private void AddOccurrence(string varName, (int, int) span)
        {
            // Try to find the variable in an existing scope
            foreach (var scope in scopeStack)
            {
                if (scope.ContainsKey(varName))
                {
                    scope[varName].Add(span);
                    return;
                }
            }
            
            // If not found, add to current scope
            var currentScope = GetCurrentScope();
            if (!currentScope.ContainsKey(varName))
            {
                currentScope[varName] = new List<(int, int)>();
            }
            currentScope[varName].Add(span);
        }
        
        // Generate the refactoring changes
        public void GenerateChanges()
        {
            if (variableToRename == null)
            {
                // No variable found at cursor position
                return;
            }

            // Collect all occurrences of the variable
            var allOccurrences = new List<(int, int)>();
            
            foreach (var scope in scopeStack)
            {
                if (scope.TryGetValue(variableToRename, out var occurrences))
                {
                    allOccurrences.AddRange(occurrences);
                }
            }
            
            if (allOccurrences.Count == 0)
            {
                return;
            }
            
            // Sort occurrences in reverse order to avoid position shifting
            allOccurrences.Sort((a, b) => b.Item1.CompareTo(a.Item1));
            
            // Generate replacement changes for each occurrence
            foreach (var (start, end) in allOccurrences)
            {
                Changes.Add(new ReplaceChange(end + 1, newVariableName)
                {
                    StartIndex = start,
                    Description = $"Rename variable '{variableToRename}' to '{newVariableName}'"
                });
            }
        }
    }
}
