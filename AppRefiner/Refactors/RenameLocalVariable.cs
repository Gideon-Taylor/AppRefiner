using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
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
        private Dictionary<string, List<(int, int)>>? targetScope;
        public RenameLocalVariable(int cursorPosition, string newVariableName)
        {
            this.cursorPosition = cursorPosition;
            this.newVariableName = newVariableName;
        }
        
        // Called when a variable is declared
        protected override void OnVariableDeclared(VariableInfo varInfo)
        {
            // Add this declaration to our tracking
            AddOccurrence(varInfo.Name, varInfo.Span);
            
            // Check if cursor is within this variable declaration
            if (varInfo.Span.Item1 <= cursorPosition && cursorPosition <= varInfo.Span.Item2 + 1)
            {
                variableToRename = varInfo.Name;
                targetScope = GetCurrentScope();
            }
        }
        
        // Override the base method for tracking variable usage
        public override void EnterIdentUserVariable(IdentUserVariableContext context)
        {
            base.EnterIdentUserVariable(context);
            
            string varName = context.GetText();
            var span = (context.Start.StartIndex, context.Stop.StopIndex);
            
            AddOccurrence(varName, span);
            
            // Check if cursor is within this variable reference
            if (span.Item1 <= cursorPosition && cursorPosition <= span.Item2 + 1)
            {
                variableToRename = varName;
                targetScope = GetCurrentScope();
            }
        }
        
        // Helper method to add an occurrence to the appropriate scope
        private void AddOccurrence(string varName, (int, int) span)
        {
            
            // If not found, add to current scope
            var currentScope = GetCurrentScope();
            if (!currentScope.ContainsKey(varName))
            {
                currentScope[varName] = new List<(int, int)>();
            }
            currentScope[varName].Add(span);
        }

        public override void ExitProgram([NotNull] ProgramContext context)
        {
            GenerateChanges();
        }
        // Generate the refactoring changes
        public void GenerateChanges()
        {
            if (variableToRename == null || targetScope == null)
            {
                // No variable found at cursor position
                SetFailure("No variable found at cursor position. Please place cursor on a variable name.");
                return;
            }

            targetScope.TryGetValue(variableToRename, out var allOccurrences);

            if (allOccurrences == null || allOccurrences.Count == 0)
            {
                // No occurrences found
                SetFailure($"Unable to find any occurrences of variable '{variableToRename}'");
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
