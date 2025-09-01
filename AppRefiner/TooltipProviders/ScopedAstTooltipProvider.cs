using System;
using System.Collections.Generic;
using System.Linq;
using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Base class for tooltip providers that leverage the self-hosted AST parser with comprehensive scope tracking.
    /// This class provides advanced scope-aware tooltip functionality with variable analysis and tracking.
    /// For simple AST-based tooltips without scope analysis, use AstTooltipProvider.
    /// </summary>
    public abstract class ScopedAstTooltipProvider<T> : ScopedAstVisitor<T>, ITooltipProvider
    {
        /// <summary>
        /// Collection of tooltip definitions identified during AST traversal.
        /// Key is the position range (start, length), value is the tooltip text.
        /// </summary>
        protected Dictionary<(int Start, int Length), string> TooltipDefinitions { get; private set; } = new();

        /// <summary>
        /// The parsed AST program node
        /// </summary>
        protected ProgramNode? Program { get; private set; }

        /// <summary>
        /// Current cursor position in the document, provided by the editor
        /// </summary>
        protected int CurrentPosition { get; private set; } = -1;

        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Determines if this provider is active and should process tooltips.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Priority of this tooltip provider. Higher priority providers are called first.
        /// </summary>
        public virtual int Priority { get; } = 0;

        /// <summary>
        /// Specifies whether this provider requires a database connection
        /// </summary>
        public virtual DataManagerRequirement DatabaseRequirement { get; } = DataManagerRequirement.NotRequired;

        /// <summary>
        /// The database manager instance, if available
        /// </summary>
        public IDataManager? DataManager { get; set; }

        /// <summary>
        /// Attempts to get a tooltip for the current position in the editor.
        /// This method should be called after the AST has been processed with scope tracking.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        /// <param name="position">The position in the document where to show the tooltip.</param>
        /// <returns>Tooltip text if available, null otherwise.</returns>
        public virtual string? GetTooltip(ScintillaEditor editor, int position)
        {
            try
            {
                CurrentPosition = position;

                // Check if we have any tooltips for this position
                foreach (var entry in TooltipDefinitions)
                {
                    var (start, length) = entry.Key;
                    if (position >= start && position < start + length)
                    {
                        return entry.Value;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Called when the tooltip is hidden. Can be used for cleanup.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        public virtual void OnHideTooltip(ScintillaEditor editor)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Processes a parsed AST program with scope tracking for tooltip identification.
        /// This should be called by the tooltip system after parsing.
        /// </summary>
        /// <param name="program">The parsed program AST</param>
        public virtual void ProcessProgram(ProgramNode program)
        {
            try
            {
                Reset();
                Program = program;
                
                if (Program != null)
                {
                    Program.Accept(this);
                }
            }
            catch (Exception)
            {
                // Silently handle processing errors
            }
        }

        /// <summary>
        /// Parses an external class for database-integrated analysis
        /// </summary>
        protected ProgramNode? ParseExternalClass(string classPath)
        {
            if (DataManager == null) return null;

            try
            {
                string? sourceCode = DataManager.GetAppClassSourceByPath(classPath);
                if (string.IsNullOrEmpty(sourceCode)) return null;

                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(sourceCode);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                return parser.ParseProgram();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Resets the internal state of the tooltip provider.
        /// This calls the base scoped visitor reset and clears tooltip definitions.
        /// </summary>
        public new virtual void Reset()
        {
            base.Reset(); // Reset scope tracking
            TooltipDefinitions = new Dictionary<(int Start, int Length), string>();
            Program = null;
            CurrentPosition = -1;
        }

        /// <summary>
        /// Helper method to register a tooltip for a specific SourceSpan.
        /// This is the preferred method for AST node tooltip registration.
        /// </summary>
        /// <param name="span">The source span of the AST node</param>
        /// <param name="tooltipText">Tooltip text to display</param>
        protected void RegisterTooltip(SourceSpan span, string tooltipText)
        {
            if (!span.IsValid || string.IsNullOrEmpty(tooltipText))
                return;

            RegisterTooltip(span.Start.ByteIndex, span.Length, tooltipText);
        }

        /// <summary>
        /// Helper method to register a tooltip for a specific position range.
        /// </summary>
        /// <param name="start">Start byte index of the text range</param>
        /// <param name="length">Length of the text range</param>
        /// <param name="tooltipText">Tooltip text to display</param>
        protected void RegisterTooltip(int start, int length, string tooltipText)
        {
            if (length <= 0 || string.IsNullOrEmpty(tooltipText))
                return;

            TooltipDefinitions[(start, length)] = tooltipText;
        }

        /// <summary>
        /// Checks if the current cursor position is within or adjacent to the specified span
        /// </summary>
        protected bool ContainsPosition(SourceSpan span)
        {
            return span.IsValid && CurrentPosition >= span.Start.ByteIndex && 
                   CurrentPosition <= span.End.ByteIndex;
        }

        /// <summary>
        /// Checks if the current cursor position is within the specified range
        /// </summary>
        protected bool ContainsPosition(int start, int length)
        {
            return CurrentPosition >= start && CurrentPosition < start + length;
        }

        /// <summary>
        /// Gets variable information at the current cursor position.
        /// This leverages the scope tracking capabilities of the base class.
        /// </summary>
        protected VariableInfo? GetVariableAtPosition()
        {
            try
            {
                var currentScope = GetCurrentScope();
                return GetAccessibleVariables(currentScope)
                    .FirstOrDefault(v => v.VariableNameInfo.Token.SourceSpan.IsValid && 
                                        ContainsPosition(v.VariableNameInfo.Token.SourceSpan));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets all variables that are accessible at the current cursor position.
        /// This includes variables from the current scope and all parent scopes.
        /// </summary>
        protected IEnumerable<VariableInfo> GetVariablesAtPosition()
        {
            try
            {
                var currentScope = GetCurrentScope();
                return GetAccessibleVariables(currentScope);
            }
            catch
            {
                return Enumerable.Empty<VariableInfo>();
            }
        }

        /// <summary>
        /// Generates a tooltip for a variable based on its scope and usage information.
        /// This is a helper method for common variable tooltip scenarios.
        /// </summary>
        protected string GenerateVariableTooltip(VariableInfo variable)
        {
            var parts = new List<string>();

            // Basic variable information
            parts.Add($"**{variable.Name}** ({variable.Type})");

            // Variable kind information
            var kindDescription = variable.Kind switch
            {
                VariableKind.Parameter => "Method parameter",
                VariableKind.Local => "Local variable",
                VariableKind.Instance => "Instance variable",
                VariableKind.Property => "Property",
                VariableKind.Constant => "Constant",
                VariableKind.Global => "Global variable",
                VariableKind.Component => "Component variable",
                _ => "Variable"
            };
            parts.Add($"*{kindDescription}*");

            // Scope information
            if (variable.DeclarationScope != null)
            {
                parts.Add($"Declared in: {variable.DeclarationScope.Name}");
            }

            // Usage information
            if (variable.References.Any())
            {
                var referenceCount = variable.References.Count();
                parts.Add($"Referenced {referenceCount} time{(referenceCount != 1 ? "s" : "")}");
            }
            else
            {
                parts.Add("*Unused*");
            }

            return string.Join("\n", parts);
        }

        /// <summary>
        /// Finds all references to a variable name across all scopes.
        /// This is useful for generating comprehensive tooltips about variable usage.
        /// </summary>
        protected IEnumerable<VariableReference> FindAllReferencesToVariable(string variableName)
        {
            return GetAllVariables()
                .Where(v => string.Equals(v.Name, variableName, StringComparison.OrdinalIgnoreCase))
                .SelectMany(v => v.References);
        }
    }

    /// <summary>
    /// Non-generic version of ScopedAstTooltipProvider for simple usage without custom scope data.
    /// This is the most common usage pattern - inherit from this class unless you need custom scope data.
    /// </summary>
    public abstract class ScopedAstTooltipProvider : ScopedAstTooltipProvider<object>
    {
        // This class provides the same functionality as ScopedAstTooltipProvider<T>
        // but with object as the generic type parameter for simplicity
    }
}