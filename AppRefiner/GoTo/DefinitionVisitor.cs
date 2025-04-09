using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Commands
{
    /// <summary>
    /// A visitor implementation that collects code definitions for navigation purposes
    /// </summary>
    public class DefinitionVisitor : PeopleCodeParserBaseListener
    {
        /// <summary>
        /// Collection of all definitions found in the code
        /// </summary>
        public List<CodeDefinition> Definitions { get; } = new List<CodeDefinition>();

        /// <summary>
        /// Tracks the current scope during traversal
        /// </summary>
        private DefinitionScope currentScope = DefinitionScope.Public;

        /// <summary>
        /// Visit Method implementations
        /// </summary>
        /// <param name="context">The method context</param>
        public override void EnterMethod(MethodContext context)
        {
            var methodName = context.genericID().GetText();
            var returnType = "void"; // Default to void

            // Try to find the corresponding method header to get return type
            // We don't need to worry about the return type for now
            // Just collect the method definition

            Definitions.Add(new CodeDefinition(
                methodName,
                returnType,
                DefinitionType.Method,
                currentScope,
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Property getter implementations
        /// </summary>
        /// <param name="context">The getter context</param>
        public override void EnterGetter(GetterContext context)
        {
            var propertyName = context.genericID().GetText();
            var returnType = "any"; // Default to any

            Definitions.Add(new CodeDefinition(
                propertyName,
                returnType,
                DefinitionType.Getter,
                currentScope,
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Property setter implementations
        /// </summary>
        /// <param name="context">The setter context</param>
        public override void EnterSetter(SetterContext context)
        {
            var propertyName = context.genericID().GetText();
            var type = "any"; // Default to any

            Definitions.Add(new CodeDefinition(
                propertyName,
                type,
                DefinitionType.Setter,
                currentScope,
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit Function definitions
        /// </summary>
        /// <param name="context">The function definition context</param>
        public override void EnterFunctionDefinition(FunctionDefinitionContext context)
        {
            var functionName = context.allowableFunctionName().GetText();
            var returnType = "void"; // Default to void

            // Check if there's a return type specified
            if (context.typeT() != null)
            {
                returnType = GetTypeString(context.typeT());
            }

            Definitions.Add(new CodeDefinition(
                functionName,
                returnType,
                DefinitionType.Function,
                DefinitionScope.Global, // Functions are always global scope
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit property declarations in the PropertyGetSet context
        /// </summary>
        public override void EnterPropertyGetSet(PropertyGetSetContext context)
        {
            var propertyName = context.genericID().GetText();
            var propertyType = GetTypeString(context.typeT());

            Definitions.Add(new CodeDefinition(
                propertyName,
                propertyType,
                DefinitionType.Property,
                currentScope,
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit property declarations in the PropertyDirect context
        /// </summary>
        public override void EnterPropertyDirect(PropertyDirectContext context)
        {
            var propertyName = context.genericID().GetText();
            var propertyType = GetTypeString(context.typeT());

            Definitions.Add(new CodeDefinition(
                propertyName,
                propertyType,
                DefinitionType.Property,
                currentScope,
                context.Start.StartIndex,
                context.Start.Line
            ));
        }

        /// <summary>
        /// Visit instance declarations (private variables)
        /// </summary>
        public override void EnterInstanceDecl(InstanceDeclContext context)
        {
            var propertyType = GetTypeString(context.typeT());

            // An instance declaration can define multiple variables
            foreach (var varNode in context.USER_VARIABLE())
            {
                var propertyName = varNode.GetText();

                Definitions.Add(new CodeDefinition(
                    propertyName,
                    propertyType,
                    DefinitionType.Property,
                    DefinitionScope.Private,
                    varNode.Symbol.StartIndex,
                    varNode.Symbol.Line
                ));
            }
        }

        /// <summary>
        /// Enter public header section
        /// </summary>
        public override void EnterPublicHeader(PublicHeaderContext context)
        {
            currentScope = DefinitionScope.Public;
        }

        /// <summary>
        /// Enter protected header section
        /// </summary>
        public override void EnterProtectedHeader(ProtectedHeaderContext context)
        {
            currentScope = DefinitionScope.Protected;
        }

        /// <summary>
        /// Enter private header section
        /// </summary>
        public override void EnterPrivateHeader(PrivateHeaderContext context)
        {
            currentScope = DefinitionScope.Private;
        }

        /// <summary>
        /// Helper method to extract type information from type context
        /// </summary>
        private string GetTypeString(TypeTContext typeContext)
        {
            if (typeContext == null)
                return "any";

            if (typeContext is ArrayTypeContext arrayType)
            {
                var baseType = arrayType.typeT() != null
                    ? GetTypeString(arrayType.typeT())
                    : "any";
                return $"Array of {baseType}";
            }
            else if (typeContext is BaseExceptionTypeContext)
            {
                return "Exception";
            }
            else if (typeContext is AppClassTypeContext appClass)
            {
                return appClass.appClassPath().GetText();
            }
            else if (typeContext is SimpleTypeTypeContext simpleType)
            {
                return simpleType.simpleType().GetText();
            }

            return typeContext.GetText();
        }
    }
} 