using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using AppRefiner.PeopleCode;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips showing information about variables in the code.
    /// This is a sample implementation showing how to use ParseTreeTooltipProvider.
    /// </summary>
    public class VariableInfoTooltipProvider : ParseTreeTooltipProvider
    {
        private Dictionary<string, string> variableTypes = new Dictionary<string, string>();

        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public override string Name => "Variable Info";

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public override string Description => "Shows information about variables in the code";

        /// <summary>
        /// Medium priority
        /// </summary>
        public override int Priority => 50;

        /// <summary>
        /// Specifies which token types this provider is interested in.
        /// </summary>
        public override int[]? TokenTypes => new int[] 
        { 
            PeopleCodeLexer.USER_VARIABLE,
            PeopleCodeLexer.LOCAL,
            PeopleCodeLexer.CONSTANT,
            PeopleCodeLexer.INSTANCE
        };

        /// <summary>
        /// Resets the internal state of the tooltip provider.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            variableTypes.Clear();
        }

        /// <summary>
        /// Handles local variable definitions
        /// </summary>
        public override void EnterLocalVariableDefinition([NotNull] PeopleCodeParser.LocalVariableDefinitionContext context)
        {
            if (context.typeT() != null && context.USER_VARIABLE() != null)
            {
                var typeName = context.typeT().GetText();
                
                foreach (var userVariable in context.USER_VARIABLE())
                {
                    var variableName = userVariable.GetText();
                    variableTypes[variableName] = typeName;
                    
                    string tooltip = $"Local Variable: {variableName}\nType: {typeName}";
                    RegisterTooltip(userVariable.Symbol, tooltip);
                }
            }
        }
        
        /// <summary>
        /// Handles local variable declarations with assignment
        /// </summary>
        public override void EnterLocalVariableDeclAssignment([NotNull] PeopleCodeParser.LocalVariableDeclAssignmentContext context)
        {
            if (context.typeT() != null && context.USER_VARIABLE() != null && context.expression() != null)
            {
                var typeName = context.typeT().GetText();
                var variableName = context.USER_VARIABLE().GetText();
                var initialValue = context.expression().GetText();
                
                variableTypes[variableName] = typeName;
                
                string tooltip = $"Local Variable: {variableName}\nType: {typeName}\nInitial Value: {initialValue}";
                RegisterTooltip(context.USER_VARIABLE().Symbol, tooltip);
            }
        }
        
        /// <summary>
        /// Handles instance variable declarations
        /// </summary>
        public override void EnterInstanceDecl([NotNull] PeopleCodeParser.InstanceDeclContext context)
        {
            if (context.typeT() != null && context.USER_VARIABLE() != null)
            {
                var typeName = context.typeT().GetText();
                
                foreach (var userVariable in context.USER_VARIABLE())
                {
                    var variableName = userVariable.GetText();
                    variableTypes[variableName] = typeName;
                    
                    string tooltip = $"Instance Variable: {variableName}\nType: {typeName}";
                    RegisterTooltip(userVariable.Symbol, tooltip);
                }
            }
        }
        
        /// <summary>
        /// Handles constant declarations
        /// </summary>
        public override void EnterConstantDeclaration([NotNull] PeopleCodeParser.ConstantDeclarationContext context)
        {
            if (context.USER_VARIABLE() != null && context.literal() != null)
            {
                var variableName = context.USER_VARIABLE().GetText();
                var value = context.literal().GetText();
                
                string tooltip = $"Constant: {variableName}\nValue: {value}";
                RegisterTooltip(context.USER_VARIABLE().Symbol, tooltip);
            }
        }
        
        /// <summary>
        /// Handles method arguments for extracted type information
        /// </summary>
        public override void EnterMethodArgument([NotNull] PeopleCodeParser.MethodArgumentContext context)
        {
            if (context.USER_VARIABLE() != null && context.typeT() != null)
            {
                var variableName = context.USER_VARIABLE().GetText();
                var typeName = context.typeT().GetText();
                var direction = context.OUT() != null ? "out" : "in";
                
                variableTypes[variableName] = typeName;
                
                string tooltip = $"Method Parameter: {variableName}\nType: {typeName}\nDirection: {direction}";
                RegisterTooltip(context.USER_VARIABLE().Symbol, tooltip);
            }
        }
        
        /// <summary>
        /// Handles user variable references in expressions
        /// </summary>
        public override void EnterIdentUserVariable([NotNull] PeopleCodeParser.IdentUserVariableContext context)
        {
            var variableName = context.USER_VARIABLE().GetText();
            
            // Check if we know the type of this variable
            if (variableTypes.TryGetValue(variableName, out var typeName))
            {
                string tooltip = $"Variable: {variableName}\nType: {typeName}";
                RegisterTooltip(context.USER_VARIABLE().Symbol, tooltip);
            }
        }
    }
} 