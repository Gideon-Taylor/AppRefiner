using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using AppRefiner.QuickFixes;
using AppRefiner.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class MissingConstructor : BaseStyler
    {
        private string extendedClassName = string.Empty;
        private string className = string.Empty;
        private MethodHeaderContext? constructorHeader;
        private GenericIDContext? classNameContext;
        public MissingConstructor()
        {
            Description = "Highlights classes missing a constructor required by their parent class.";
            Active = true;
        }


        public override void EnterClassDeclarationExtension([NotNull] ClassDeclarationExtensionContext context)
        {
            var superclassContext = context.superclass();
            if (superclassContext is AppClassSuperClassContext appClassSuperCtx)
            {
                extendedClassName = appClassSuperCtx.appClassPath().GetText();
            }
            classNameContext = context.genericID();
            className = classNameContext.GetText();
        }

        public override void EnterClassDeclarationImplementation([NotNull] ClassDeclarationImplementationContext context)
        {
            extendedClassName = context.appClassPath()?.GetText() ?? string.Empty;
            classNameContext = context.genericID();
            className = classNameContext.GetText();
        }

        public override void EnterMethodHeader([NotNull] MethodHeaderContext context)
        {
            var methodName = context.genericID().GetText();
            if (methodName.Equals(className))
            {
                constructorHeader = context;
            }
        }

        public override void ExitProgram([NotNull] ProgramContext context)
        {
            if (extendedClassName != string.Empty && constructorHeader == null)
            {
                Ast.Method? baseClassConstructor = null;

                var programAst = new AstService(DataManager).GetProgramAst(extendedClassName);

                if (programAst?.ContainedAppClass != null)
                {
                    var baseClass = programAst.ContainedAppClass;
                    var constructors = baseClass.Methods.Where(m => m.Name.Equals(baseClass.Name, StringComparison.OrdinalIgnoreCase));
                    if (constructors.Any())
                    {
                        baseClassConstructor = constructors.First();
                    }
                }
                else if (programAst?.ContainedInterface != null)
                {
                    var baseClass = programAst.ContainedInterface;
                    var constructors = baseClass.Methods.Where(m => m.Name.Equals(baseClass.Name, StringComparison.OrdinalIgnoreCase));
                    if (constructors.Any())
                    {
                        baseClassConstructor = constructors.First();
                    }
                }

                if (baseClassConstructor?.Parameters.Count > 0 && classNameContext != null)
                {

                    var indicator = new Indicator
                    {
                        Start = classNameContext.Start.StartIndex,
                        Length = classNameContext.Stop.StopIndex - classNameContext.Start.StartIndex,
                        Color = 0x0000FFFF, // Red color for missing constructor
                        Tooltip = $"Class '{className}' is missing a constructor required by '{extendedClassName}'.",
                        Type = IndicatorType.SQUIGGLE,
                        QuickFixes = [(typeof(GenerateBaseConstructorRefactor), "Add missing constructor.")]
                    };
                    Indicators?.Add(indicator);
                }
            }

        }

    }
}


