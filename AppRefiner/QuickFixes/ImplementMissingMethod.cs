using AppRefiner.Ast;
using AppRefiner.PeopleCode;
using AppRefiner.Services;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using AppRefiner.Refactors;
using Antlr4.Runtime.Misc;

namespace AppRefiner.QuickFixes
{
    public class ImplementMissingMethod : BaseRefactor
    {
        public new static string RefactorName => "Implement Missing Method";
        public new static string RefactorDescription => "Generates a basic implementation for a method declared in the header.";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => false;

        private int _lastMethodImplementationEnd = -1;
        private int _classDeclarationEnd = -1;
        private int _classBodyStart = -1;
        private MethodHeaderContext? _targetMethodHeaderContext;
        private string _currentClassName = string.Empty;

        public ImplementMissingMethod(ScintillaEditor editor)
            : base(editor)
        {
        }

        public override void EnterAppClassProgram(AppClassProgramContext context)
        {
            var declCtx = context.classDeclaration();
            if (declCtx is ClassDeclarationExtensionContext extCtx)
                _currentClassName = extCtx.genericID().GetText();
            else if (declCtx is ClassDeclarationImplementationContext implCtx)
                _currentClassName = implCtx.genericID().GetText();
            else if (declCtx is ClassDeclarationPlainContext plainCtx)
                _currentClassName = plainCtx.genericID().GetText();

            _classDeclarationEnd = declCtx.Stop.ByteStopIndex();

            var classBody = context.classBody();
            if (classBody != null && classBody.classMember().Length > 0)
            {
                _classBodyStart = classBody.classMember(0).Start.ByteStartIndex();
            }
            else
            {
                _classBodyStart = _classDeclarationEnd + 1;
                _lastMethodImplementationEnd = _classDeclarationEnd;
            }

            base.EnterAppClassProgram(context);
        }

        public override void ExitMethodImplementation(MethodImplementationContext context)
        {
            // Find the actual end including any trailing semicolons
            _lastMethodImplementationEnd = FindActualEndWithSemicolons(context);
            base.ExitMethodImplementation(context);
        }
        
        public override void ExitGetterImplementation(GetterImplementationContext context)
        {
            // Track getter implementations as well
            _lastMethodImplementationEnd = FindActualEndWithSemicolons(context);
            base.ExitGetterImplementation(context);
        }
        
        public override void ExitSetterImplementation(SetterImplementationContext context)
        {
            // Track setter implementations as well
            _lastMethodImplementationEnd = FindActualEndWithSemicolons(context);
            base.ExitSetterImplementation(context);
        }

        public override void EnterMethodHeader([NotNull] MethodHeaderContext context)
        {
            // We need to find the specific method header that triggered the quick fix.
            // This might require passing information from the styler to the refactor,
            // or matching based on the cursor position when the quick fix is invoked.
            // For now, let's assume we can identify the target header.
            // This logic will need refinement based on how the quick fix is triggered and context is passed.

            // A simple way to identify the target is by the line number of the indicator.
            // The styler knows the line number of the header.
            var currentLineNumber = ScintillaManager.GetCurrentLineNumber(Editor);
            if (currentLineNumber + 1 >= context.Start.Line && currentLineNumber + 1 <= context.Stop.Line)
            {
                 var methodName = context.genericID().GetText();
                 // Ensure it's not a constructor
                if (!methodName.Equals(_currentClassName, StringComparison.OrdinalIgnoreCase))
                {
                    _targetMethodHeaderContext = context;
                }
            }
            base.EnterMethodHeader(context);
        }


        public override void ExitProgram(ProgramContext context)
        {
            if (_targetMethodHeaderContext == null)
            {
                SetFailure("Target method header not found or method is a constructor.");
                base.ExitProgram(context); 
                return;
            }

            // Reset locations if not an AppClass program to avoid stale data
            if (context.appClass() == null || context.appClass() is not AppClassProgramContext)
            {
                 _classDeclarationEnd = -1;
                 _classBodyStart = -1;
                 _lastMethodImplementationEnd = -1;
                 _targetMethodHeaderContext = null;
                 _currentClassName = string.Empty;
                 base.ExitProgram(context);
                 return;
            }

            GenerateMethodImplementation(context, _targetMethodHeaderContext);
            base.ExitProgram(context);
        }

        private void GenerateMethodImplementation(ProgramContext programContext, MethodHeaderContext headerContext)
        {
            var methodName = headerContext.genericID().GetText();
            
            // Extract parameters from the method header
            var methodArguments = headerContext.methodArguments()?.methodArgument() ?? new MethodArgumentContext[0];
            string paramString = string.Join(", ", methodArguments.Select(p => GetParameterString(p)));
            
            // Extract return type from the method header
            var hasReturnType = headerContext.children?.Any(c => c.GetText().Equals("RETURNS", StringComparison.OrdinalIgnoreCase)) == true;
            TypeTContext? returnTypeContext = null;
            
            if (hasReturnType)
            {
                // Find the typeT that comes after RETURNS
                for (int i = 0; i < headerContext.ChildCount - 1; i++)
                {
                    if (headerContext.GetChild(i).GetText().Equals("RETURNS", StringComparison.OrdinalIgnoreCase))
                    {
                        if (headerContext.GetChild(i + 1) is TypeTContext typeContext)
                        {
                            returnTypeContext = typeContext;
                            break;
                        }
                    }
                }
            }
            
            // Generate method implementation
            string indent = "   ";
            string methodImpl = $"method {methodName}" + Environment.NewLine +
                              $"{indent}throw CreateException(0, 0, \"Method '{methodName}' not implemented.\");" + Environment.NewLine;
            
            // Add return statement if method has a return type
            if (returnTypeContext != null)
            {
                string defaultValue = GetDefaultValueForType(returnTypeContext.GetText());
                methodImpl += $"{indent}Return {defaultValue};" + Environment.NewLine;
            }
            
            methodImpl += "end-method;";
            
            // Determine insertion position
            int insertPos = -1;
            if (_lastMethodImplementationEnd != -1 && _lastMethodImplementationEnd > _classDeclarationEnd)
            {
                insertPos = _lastMethodImplementationEnd + 1;
            }
            else if (_classBodyStart != -1 && _classBodyStart > _classDeclarationEnd)
            {
                insertPos = _classBodyStart;
            }
            else if (_classDeclarationEnd != -1)
            {
                insertPos = _classDeclarationEnd + 1;
            }
            
            if (insertPos != -1)
            {
                string methodImplWithNewlines = Environment.NewLine + Environment.NewLine + methodImpl;
                InsertText(insertPos, methodImplWithNewlines, $"Insert implementation for method '{methodName}'");
            }
            else
            {
                SetFailure("Could not determine where to insert the method implementation.");
            }
        }
        
        private string GetParameterString(MethodArgumentContext paramContext)
        {
            var name = paramContext.USER_VARIABLE().GetText();
            var type = paramContext.typeT()?.GetText() ?? "Any";
            var isOut = paramContext.OUT() != null;
            return $"{name} As {type}{(isOut ? " out" : "")}";
        }
        
        private string GetDefaultValueForType(string typeName)
        {
            switch (typeName.ToLower())
            {
                case "boolean":
                    return "False";
                case "integer":
                case "number":
                case "float":
                    return "0";
                case "string":
                    return "\"\"";
                case "date":
                case "time":
                case "datetime":
                    return "Null";
                default:
                    return "Null";
            }
        }
        
        private int FindActualEndWithSemicolons(ParserRuleContext context)
        {
            // Start with the context's stop position
            int endPos = context.Stop.ByteStopIndex();
            
            // Find the end of the current line to handle comments and other content after semicolons
            if (Editor.ContentString != null)
            {
                var source = Editor.ContentString;
                int searchPos = endPos + 1;
                
                // Find the end of the current line
                while (searchPos < source.Length)
                {
                    char c = source[searchPos];
                    if (c == '\r' || c == '\n')
                    {
                        // Found end of line, but don't include the newline character itself
                        endPos = searchPos - 1;
                        break;
                    }
                    searchPos++;
                }
                
                // If we reached the end of the file without finding a newline
                if (searchPos >= source.Length)
                {
                    endPos = source.Length - 1;
                }
            }
            
            return endPos;
        }
    }
} 