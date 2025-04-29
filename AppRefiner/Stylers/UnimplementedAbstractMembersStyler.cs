using AppRefiner.Database;
using AppRefiner.Ast;
using static AppRefiner.PeopleCode.PeopleCodeParser; // For context types
using Antlr4.Runtime.Misc; // For NotNull
using Antlr4.Runtime;
using Antlr4.Runtime.Tree; // For IParseTree, ITerminalNode
using System.Linq;
using System.Text; // For StringBuilder

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Styler that checks if an Application Class implements all abstract members
    /// from its base class or interfaces.
    /// </summary>
    public class UnimplementedAbstractMembersStyler : BaseStyler
    {
        private const uint WARNING_COLOR = 0xFF00A5FF; // Orange (BGRA) for unimplemented members warning

        // Member variables to store context during parse tree walk
        private ParserRuleContext? _extendsTargetCtx;
        private ParserRuleContext? _implementsTargetCtx;
        private ProgramContext? _currentProgramContext;

        public UnimplementedAbstractMembersStyler()
        {
            Description = "Highlights Application Classes that do not implement all abstract members from base classes or interfaces.";
            Active = true; // Assuming it should be active by default
        }

        /// <summary>
        /// Specifies that this styler requires a database connection to resolve class hierarchies.
        /// </summary>
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;

        public override void Reset()
        {
            // No specific cache to clear here beyond the base reset
            base.Reset();
            _extendsTargetCtx = null;
            _implementsTargetCtx = null;
            _currentProgramContext = null;
        }

        public override void EnterProgram([NotNull] ProgramContext context)
        {
            // Store the program context for later use in ExitProgram
            _currentProgramContext = context;
        }

        public override void EnterClassDeclarationExtension([NotNull] ClassDeclarationExtensionContext context)
        {
            // Store the context for the 'extends' clause path
            var superClassCtx = context.superclass();
            if (superClassCtx is AppClassSuperClassContext appClassSuperCtx)
            {
                _extendsTargetCtx = appClassSuperCtx.appClassPath();
            }
        }

        public override void EnterClassDeclarationImplementation([NotNull] ClassDeclarationImplementationContext context)
        {
            // Store the context for the 'implements' clause path
            _implementsTargetCtx = context.appClassPath();
        }

        public override void ExitProgram([NotNull] ProgramContext context)
        {
            // Ensure we are exiting the same context we entered and have necessary components
            if (context != _currentProgramContext || DataManager == null || _currentProgramContext == null)
            {
                 // Reset state if context mismatch or missing components, just in case
                Reset(); 
                return;
            } 

            if (_implementsTargetCtx == null && _extendsTargetCtx == null)
            {
                // No class extension or implementation found, nothing to do
                Reset();
                return;
            }

            try
            {
                // Parse the AST using the stored program context
                var programAst = AppRefiner.Ast.Program.Parse(_currentProgramContext, "", DataManager);

                // Check if this program contains an Application Class
                if (programAst.ContainedAppClass == null) return;

                var appClass = programAst.ContainedAppClass;

                // Get the list of unimplemented abstract methods and properties
                var (unimplementedMethods, unimplementedProperties) = appClass.GetAllUnimplementedAbstractMembers();

                // If there are no unimplemented members, we are done
                if (unimplementedMethods.Count == 0 && unimplementedProperties.Count == 0) return;

                // Determine the final target node to highlight
                IParseTree? finalTargetNode = null;
                if (appClass.ExtendedClass != null && _extendsTargetCtx != null)
                {
                    finalTargetNode = _extendsTargetCtx;
                }
                else if (appClass.ImplementedInterface != null && _implementsTargetCtx != null)
                {
                    finalTargetNode = _implementsTargetCtx;
                }

                // If we still don't have a target, we cannot add an indicator
                if (finalTargetNode == null) return;

                // Determine start and length
                int startIndex, stopIndex, length;
                if (finalTargetNode is ParserRuleContext ctxNode)
                {
                    startIndex = ctxNode.Start.StartIndex;
                    stopIndex = ctxNode.Stop.StopIndex;
                }
                else if (finalTargetNode is ITerminalNode termNode)
                {
                    startIndex = termNode.Symbol.StartIndex;
                    stopIndex = termNode.Symbol.StopIndex;
                }
                else
                {
                    return; // Cannot determine bounds
                }

                length = stopIndex - startIndex + 1;
                if (length <= 0) return;

                // Build the tooltip message
                var tooltipBuilder = new StringBuilder("Missing implementations:");
                foreach (var method in unimplementedMethods)
                {
                    tooltipBuilder.Append($"\n - Method: {method.Name}");
                }
                foreach (var prop in unimplementedProperties)
                {
                    tooltipBuilder.Append($"\n - Property: {prop.Name}");
                }
                tooltipBuilder.Append($"\n\nUse 'Implement Base Class Members' action to resolve.");
                // Add the indicator
                Indicators?.Add(new Indicator
                {
                    Start = startIndex,
                    Length = length,
                    Color = WARNING_COLOR,
                    Type = IndicatorType.SQUIGGLE,
                    Tooltip = tooltipBuilder.ToString()
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in UnimplementedAbstractMembersStyler: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                // Reset state after processing the program
                // This is crucial if the styler instance is reused
                Reset(); 
            }
        }
    }
} 