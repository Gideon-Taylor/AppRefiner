using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Text;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips showing the containing scope hierarchy for a line of code.
    /// Shows function/method, if/else, for/while, and evaluate blocks that contain the current line.
    /// This is the self-hosted equivalent of the ANTLR-based ScopeTooltipProvider.
    /// </summary>
    public class ScopeTooltipProvider : BaseTooltipProvider
    {
        private List<ScopeInfo> containingScopes = new();

        // Map from line number to first token on that line
        private Dictionary<int, Token> firstTokensOnLine = new();

        // Map from method name to parameter signature
        private Dictionary<string, string> methodParameterMap = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> methodReturnsMap = new(StringComparer.OrdinalIgnoreCase);

        // Store program for lazy evaluation
        private ProgramNode? storedProgram = null;
        private int storedPosition = -1;
        private int storedLineNumber = -1;
        private bool hasProcessedAst = false;

        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public override string Name => "Scope Context";

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public override string Description => "Shows containing scope hierarchy when hovering at the beginning of a line";

        /// <summary>
        /// Medium-high priority
        /// </summary>
        public override int Priority => 80;

        /// <summary>
        /// Resets the internal state of the tooltip provider.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            containingScopes.Clear();
            firstTokensOnLine.Clear();
            methodParameterMap.Clear();
            methodReturnsMap.Clear();
        }

        /// <summary>
        /// Processes the AST to collect scope information
        /// </summary>
        public override void ProcessProgram(ProgramNode program, int position, int lineNumber)
        {
            // Reset state
            containingScopes.Clear();
            firstTokensOnLine.Clear();
            methodParameterMap.Clear();
            methodReturnsMap.Clear();

            base.ProcessProgram(program, position, lineNumber);
        }



        /// <summary>
        /// Override to process method declarations
        /// </summary>
        public override void VisitMethodImpl(MethodImplNode node)
        {
            // Skip if this scope starts after our target line
            if (node.SourceSpan.Start.Line > CurrentLine)
                return;
            if (node.Declaration is null) return;

            // Capture method signature information
            string methodName = node.Name;

            // Build parameter signature
            var parameters = new List<string>();
            foreach (var param in node.Parameters)
            {
                string paramType = param.Type?.TypeName ?? "any";
                string direction = param.IsOut ? "out" : "in";
                parameters.Add($"{param.Name} as {paramType}");
            }
            methodParameterMap[methodName] = string.Join(", ", parameters);

            // Capture return type if present
            if (node.ReturnType != null)
            {
                methodReturnsMap[methodName] = node.ReturnType.TypeName;
            }

            // Process as a scope if it contains our target line
            ProcessScope(node, "method");

            base.VisitMethodImpl(node);
        }

        /// <summary>
        /// Override to process function declarations
        /// </summary>
        public override void VisitFunction(FunctionNode node)
        {
            // Skip if this scope starts after our target line
            if (node.SourceSpan.Start.Line > CurrentLine)
                return;

            // Build parameter signature
            var parameters = new List<string>();
            foreach (var param in node.Parameters)
            {
                string paramType = param.Type?.TypeName ?? "any";
                parameters.Add($"{param.Name} as {paramType}");
            }

            // Process as a scope if it contains our target line
            ProcessScope(node, "function");

            base.VisitFunction(node);
        }

        /// <summary>
        /// Override to process app class declarations
        /// </summary>
        public override void VisitAppClass(AppClassNode node)
        {
            // Process property getters and setters from the class
            foreach (var property in node.PropertyGetters)
            {
                if (property.GetterBody != null)
                {
                    ProcessScope(property, "get");
                }
            }

            foreach (var property in node.PropertySetters)
            {
                if (property.SetterBody != null)
                {
                    ProcessScope(property, "set");
                }
            }

            base.VisitAppClass(node);
        }

        /// <summary>
        /// Override to process if statements
        /// </summary>
        public override void VisitIf(IfStatementNode node)
        {
            // Skip if this scope starts after our target line
            if (node.SourceSpan.Start.Line > CurrentLine)
                return;

            ProcessScope(node, "If");

            // Handle else block if it exists and contains our target line
            if (node.ElseBlock != null && node.ElseBlock.SourceSpan.Start.Line <= CurrentLine &&
                CurrentLine <= node.ElseBlock.SourceSpan.End.Line)
            {
                var elseScope = new ScopeInfo
                {
                    ScopeType = "Else",
                    StartLine = node.ElseBlock.SourceSpan.Start.Line,
                    EndLine = node.ElseBlock.SourceSpan.End.Line,
                    Context = node.ElseBlock,
                    HeaderText = "Else",
                    ParentScopeStartLine = node.SourceSpan.Start.Line,
                    IsElseBlock = true
                };

                containingScopes.Add(elseScope);
            }

            base.VisitIf(node);
        }

        /// <summary>
        /// Override to process for statements
        /// </summary>
        public override void VisitFor(ForStatementNode node)
        {
            // Skip if this scope starts after our target line
            if (node.SourceSpan.Start.Line > CurrentLine)
                return;

            ProcessScope(node, "For");
            base.VisitFor(node);
        }

        public override void VisitRepeat(RepeatStatementNode node)
        {
            // Skip if this scope starts after our target line
            if (node.SourceSpan.Start.Line > CurrentLine)
                return;

            ProcessScope(node, "Repeat");
            base.VisitRepeat(node);
        }

        public override void VisitCatch(CatchStatementNode node)
        {
            // Skip if this scope starts after our target line
            if (node.SourceSpan.Start.Line > CurrentLine)
                return;

            ProcessScope(node, "Catch");
            base.VisitCatch(node);
        }

        /// <summary>
        /// Override to process while statements
        /// </summary>
        public override void VisitWhile(WhileStatementNode node)
        {
            // Skip if this scope starts after our target line
            if (node.SourceSpan.Start.Line > CurrentLine)
                return;

            ProcessScope(node, "While");
            base.VisitWhile(node);
        }

        /// <summary>
        /// Override to process evaluate statements
        /// </summary>
        public override void VisitEvaluate(EvaluateStatementNode node)
        {
            // Skip if this scope starts after our target line
            if (node.SourceSpan.Start.Line > CurrentLine)
                return;

            ProcessScope(node, "Evaluate");

            // Note: WhenClause doesn't have SourceSpan since it doesn't inherit from AstNode
            // For now, we'll skip processing individual when clauses
            // This is a limitation of the current AST structure

            base.VisitEvaluate(node);
        }

        /// <summary>
        /// Common handler for scope contexts
        /// </summary>
        private void ProcessScope(AstNode node, string scopeType)
        {
            if (node == null || !node.SourceSpan.IsValid)
                return;

            // Skip if this scope starts after our target line
            if (node.SourceSpan.Start.Line > CurrentLine)
                return;

            // Skip if this scope ends before our target line
            if (node.SourceSpan.End.Line < CurrentLine)
                return;

            // Create a scope info object only if our target line is within this scope
            var scopeInfo = new ScopeInfo
            {
                ScopeType = scopeType,
                StartLine = node.SourceSpan.Start.Line,
                EndLine = node.SourceSpan.End.Line,
                Context = node
            };

            // Get the text for this scope's header line
            string headerLine = GetNodeHeaderLine(node, scopeType);
            if (!string.IsNullOrEmpty(headerLine))
            {
                scopeInfo.HeaderText = headerLine.Trim();
            }

            // Add this scope to our list
            containingScopes.Add(scopeInfo);
        }

        /// <summary>
        /// Gets the header line text for a node
        /// </summary>
        private string GetNodeHeaderLine(AstNode node, string scopeType)
        {
            if (node == null || !node.SourceSpan.IsValid)
                return string.Empty;

            // Note: PropertyGetterNode and PropertySetterNode don't exist in self-hosted parser
            // Property getters and setters are handled in VisitAppClass method instead
            else if (node is IfStatementNode ifNode)
            {
                return $"If {ifNode.Condition?.ToString() ?? ""}";
            }
            else if (node is ForStatementNode forNode)
            {
                return $"For {forNode.IteratorName} = {forNode.FromValue} To {forNode.ToValue}";
            }
            else if (node is WhileStatementNode whileNode)
            {
                return $"While {whileNode.Condition?.ToString() ?? ""}";
            }
            else if (node is RepeatStatementNode repeatNode)
            {
                return $"Repeat {repeatNode.Condition?.ToString() ?? ""}";
            }
            else if (node is EvaluateStatementNode evalNode)
            {
                return $"Evaluate {evalNode.Expression?.ToString() ?? ""}";
            }
            else if (node is MethodImplNode methodImplNode)
            {
                if (methodImplNode.Declaration is null) return string.Empty;

                var methodNode = methodImplNode.Declaration;
                var methodName = methodNode.Name;
                var methodLine = $"Method {methodName}";

                // Check if we have parameter information for this method
                if (!string.IsNullOrEmpty(methodName) && methodParameterMap.TryGetValue(methodName, out var parameters))
                {
                    methodLine += $"({parameters})";
                }
                else
                {
                    methodLine += "()";
                }

                // Check if we have a return type
                if (methodReturnsMap.TryGetValue(methodName, out var returnType))
                {
                    methodLine += $" returns {returnType}";
                }

                return methodLine;
            }
            else if (node is FunctionNode funcNode)
            {
                var functionLine = $"Function {funcNode.Name}";

                // Add parameters
                var parameters = new List<string>();
                foreach (var param in funcNode.Parameters)
                {
                    string paramType = param.Type?.TypeName ?? "any";
                    parameters.Add($"{param.Name} as {paramType}");
                }
                functionLine += $"({string.Join(", ", parameters)})";

                // Add return type if present
                if (funcNode.ReturnType != null)
                {
                    functionLine += $" returns {funcNode.ReturnType.TypeName}";
                }

                return functionLine;
            }
            else
            {
                return $"{scopeType} statement";
            }
        }

        /// <summary>
        /// Checks if the given position is over leading whitespace on the current line
        /// </summary>
        private bool IsPositionOverLeadingWhitespace(ScintillaEditor editor, int position, int lineNumber)
        {
            if (editor == null || !editor.IsValid() || lineNumber < 0)
                return false;

            try
            {
                // Get the text of the current line (CurrentLine is 1-based, ScintillaManager methods expect 0-based)
                string lineText = ScintillaManager.GetLineText(editor, lineNumber - 1);
                if (string.IsNullOrEmpty(lineText))
                    return true; // Empty line counts as all whitespace
                
                // Get the start position of the current line
                int lineStartPos = ScintillaManager.GetLineStartIndex(editor, lineNumber - 1);
                if (lineStartPos == -1)
                    return false;
                
                // Calculate the column position within the line
                int columnPos = position - lineStartPos;
                
                // Check if we're within the line bounds
                if (columnPos < 0 || columnPos >= lineText.Length)
                    return false;
                
                // Check if all characters from start of line to current position are whitespace
                for (int i = 0; i <= columnPos; i++)
                {
                    if (!char.IsWhiteSpace(lineText[i]))
                        return false; // Found non-whitespace character, so position is not over leading whitespace
                }
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool CanProvideTooltipAt(ScintillaEditor editor, ProgramNode program, List<Token> tokens, int cursorPosition, int lineNumber)
        {
            return IsPositionOverLeadingWhitespace (editor, cursorPosition, lineNumber);
        }


        /// <summary>
        /// Attempts to get a tooltip for the current position in the editor.
        /// </summary>
        public override string? GetTooltip(ScintillaEditor editor, int position)
        {
            if (editor == null || !editor.IsValid())
                return null;

            // Always use the provided line number from the base class
            if (CurrentLine <= 0)
                return null;

            // Only do the expensive AST processing if we're over leading whitespace
            if (Program != null && containingScopes.Count == 0)
            {
                // Do the AST processing now that we know the position is valid
                Program.Accept(this);
            }

            // Check if we have any scopes containing our target line
            if (containingScopes.Count == 0)
                return null;

            // Skip scopes that start on the current line
            var relevantScopes = containingScopes
                .Where(s => s.StartLine != CurrentLine)
                .ToList();

            // No scopes to display
            if (relevantScopes.Count == 0)
                return null;

            // First sort by scope type (methods/functions first), then by nesting level
            var methodScopes = relevantScopes
                .Where(s => s.ScopeType.Equals("method", StringComparison.OrdinalIgnoreCase) ||
                            s.ScopeType.Equals("function", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.StartLine)
                .ToList();

            // Then get all control flow scopes
            var controlScopes = relevantScopes
                .Where(s => !s.ScopeType.Equals("method", StringComparison.OrdinalIgnoreCase) &&
                            !s.ScopeType.Equals("function", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Process Else blocks to reorganize them after their parent If blocks
            var processedControlScopes = ReorganizeControlScopes(controlScopes);

            if (methodScopes.Count > 0)
            {
                /* Indent all control scopes by 1 */
                foreach(var scope in containingScopes)
                {
                    scope.IndentLevel++;
                }
            }

            // Create the tooltip with indentation
            var tooltipBuilder = new StringBuilder();

            // First add method/function scopes which are at the outermost level
            foreach (var scope in methodScopes)
            {
                tooltipBuilder.AppendLine(scope.HeaderText);
            }

            // Then add control flow scopes with proper indentation
            foreach (var scope in processedControlScopes)
            {
                string indentation = new(' ', scope.IndentLevel * 2);
                tooltipBuilder.AppendLine(indentation + scope.HeaderText);
            }

            return tooltipBuilder.ToString().TrimEnd();
        }

        /// <summary>
        /// Reorganizes control flow scopes to handle nested If/Else structures correctly
        /// </summary>
        private List<ScopeInfo> ReorganizeControlScopes(List<ScopeInfo> controlScopes)
        {
            // First sort all scopes by their starting line
            var sortedScopes = controlScopes.OrderBy(s => s.StartLine).ToList();

            // Initialize indent levels based on nesting
            int currentIndent = 0;
            int latestEndLine = 0;

            // Process each scope to set its indent level
            for (int i = 0; i < sortedScopes.Count; i++)
            {
                var scope = sortedScopes[i];

                // When we see an Else, we don't increase the indent level from its parent If
                if (scope.IsElseBlock)
                {
                    // Find the parent If scope to match its indent level
                    var parentIf = sortedScopes.FirstOrDefault(s =>
                        s.StartLine == scope.ParentScopeStartLine && s.ScopeType == "If");

                    if (parentIf != null)
                    {
                        scope.IndentLevel = parentIf.IndentLevel;
                    }
                    else
                    {
                        // If parent not found, just use current indent
                        scope.IndentLevel = currentIndent;
                    }
                }
                else
                {
                    // Normal flow - adjust indent based on nesting
                    if (scope.StartLine > latestEndLine)
                    {
                        // This scope starts after the previous one ends, so we need to reduce indent
                        while (currentIndent > 0)
                        {
                            bool foundParent = false;

                            // Look for a parent scope whose end line is >= this scope's start line
                            for (int j = i - 1; j >= 0; j--)
                            {
                                if (sortedScopes[j].EndLine >= scope.StartLine)
                                {
                                    foundParent = true;
                                    break;
                                }
                            }

                            if (foundParent)
                                break;

                            currentIndent--;
                        }
                    }

                    scope.IndentLevel = currentIndent;

                    // If this is an If, increase the indent for child scopes
                    if (scope.ScopeType == "If" || scope.ScopeType == "For" ||
                        scope.ScopeType == "While" || scope.ScopeType == "Evaluate")
                    {
                        currentIndent++;
                    }
                }

                // Keep track of the latest end line
                latestEndLine = Math.Max(latestEndLine, scope.EndLine);
            }

            return sortedScopes;
        }

        /// <summary>
        /// Stores information about a scope for generating tooltips
        /// </summary>
        private class ScopeInfo
        {
            public string ScopeType { get; set; } = "";
            public int StartLine { get; set; }
            public int EndLine { get; set; }
            public string HeaderText { get; set; } = "";
            public AstNode? Context { get; set; }

            /// <summary>
            /// For Else blocks, stores the start line of the parent If statement
            /// This helps us link Else blocks to their parent If statements
            /// </summary>
            public int ParentScopeStartLine { get; set; } = -1;

            /// <summary>
            /// Flag to indicate if this is an Else block
            /// </summary>
            public bool IsElseBlock { get; set; } = false;

            /// <summary>
            /// Indentation level for displaying in the tooltip
            /// </summary>
            public int IndentLevel { get; set; } = 0;
        }
    }
}