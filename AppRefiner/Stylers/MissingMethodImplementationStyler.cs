using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.Ast;
using AppRefiner.PeopleCode;
using AppRefiner.QuickFixes;
using AppRefiner.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class MissingMethodImplementationStyler : BaseStyler
    {
        private const uint WARNING_COLOR = 0xFF00A5FF; // Orange (BGRA)

        private class MethodInfo
        {
            public string Name { get; set; } = string.Empty;
            public ParserRuleContext HeaderContext { get; set; }
            public bool IsImplemented { get; set; } = false;

            public MethodInfo(ParserRuleContext headerContext)
            {
                HeaderContext = headerContext;
            }
        }

        private readonly List<MethodInfo> _declaredMethods = new List<MethodInfo>();
        private string _currentClassName = string.Empty;

        public MissingMethodImplementationStyler()
        {
            Description = "Highlights methods that are declared in the class header but not implemented.";
            Active = true;
        }

        public override void Reset()
        {
            _declaredMethods.Clear();
            _currentClassName = string.Empty;
            base.Reset();
        }

        public override void EnterClassDeclarationExtension([NotNull] ClassDeclarationExtensionContext context)
        {
            _currentClassName = context.genericID().GetText();
            base.EnterClassDeclarationExtension(context);
        }

        public override void EnterClassDeclarationImplementation([NotNull] ClassDeclarationImplementationContext context)
        {
            _currentClassName = context.genericID().GetText();
            base.EnterClassDeclarationImplementation(context);
        }

        public override void EnterClassDeclarationPlain([NotNull] ClassDeclarationPlainContext context)
        {
            _currentClassName = context.genericID().GetText();
            base.EnterClassDeclarationPlain(context);
        }

        public override void EnterMethodHeader([NotNull] MethodHeaderContext context)
        {
            var methodName = context.genericID().GetText();
            // Exclude constructors from this styler
            if (!methodName.Equals(_currentClassName, StringComparison.OrdinalIgnoreCase))
            {
                _declaredMethods.Add(new MethodInfo(context) { Name = methodName });
            }
            base.EnterMethodHeader(context);
        }

        public override void EnterMethodImplementation([NotNull] MethodImplementationContext context)
        {
            var methodName = context.method()?.genericID()?.GetText();
            if (methodName != null)
            {
                var declaredMethod = _declaredMethods.FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
                if (declaredMethod != null)
                {
                    declaredMethod.IsImplemented = true;
                }
            }
            base.EnterMethodImplementation(context);
        }

        public override void ExitProgram([NotNull] ProgramContext context)
        {
            foreach (var methodInfo in _declaredMethods)
            {
                if (!methodInfo.IsImplemented && methodInfo.HeaderContext != null)
                {
                    var startToken = methodInfo.HeaderContext.Start;
                    var stopToken = methodInfo.HeaderContext.Stop;

                    if (startToken != null && stopToken != null)
                    {
                        var indicator = new Indicator
                        {
                            Start = startToken.StartIndex,
                            Length = stopToken.StopIndex - startToken.StartIndex + 1,
                            Color = WARNING_COLOR,
                            Tooltip = $"Method '{methodInfo.Name}' is declared but not implemented.",
                            Type = IndicatorType.SQUIGGLE,
                            QuickFixes = [(typeof(ImplementMissingMethod), $"Implement method '{methodInfo.Name}'.")]
                        };
                        Indicators?.Add(indicator);
                    }
                }
            }
            base.ExitProgram(context);
        }
    }
} 