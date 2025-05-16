using AppRefiner.Ast;
using AppRefiner.PeopleCode;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Linq;
using AppRefiner.Refactors;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.QuickFixes
{
    /// <summary>
    /// Refactoring operation to generate a constructor that properly calls the parent class constructor.
    /// </summary>
    public class GenerateBaseConstructorRefactor : BaseRefactor
    {
        public new static string RefactorName => "Generate Base Constructor";
        public new static string RefactorDescription => "Generates a constructor that calls the parent class constructor.";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => false;
        
        // Location tracking
        private int _publicHeaderEnd = -1;
        private int _classDeclarationEnd = -1;
        private int _classBodyStart = -1;
        private int _lastMethodImplementationEnd = -1;
        private int _publicHeaderStart = -1;
        private string _currentClassName = string.Empty;

        /// <summary>
        /// Creates a new instance of the GenerateBaseConstructorRefactor.
        /// </summary>
        /// <param name="editor">The ScintillaEditor instance.</param>
        public GenerateBaseConstructorRefactor(ScintillaEditor editor)
            : base(editor)
        {
        }

        // Location tracking methods
        public override void EnterAppClassProgram(AppClassProgramContext context)
        {
            var declCtx = context.classDeclaration();
            if (declCtx is ClassDeclarationExtensionContext extCtx)
                _currentClassName = extCtx.genericID().GetText();
            else if (declCtx is ClassDeclarationImplementationContext implCtx)
                _currentClassName = implCtx.genericID().GetText();
            else if (declCtx is ClassDeclarationPlainContext plainCtx)
                _currentClassName = plainCtx.genericID().GetText();
                
            _classDeclarationEnd = declCtx.Stop.StopIndex + 1;

            // Find start of class body
            var classBody = context.classBody();
            if (classBody != null && classBody.classMember().Length > 0)
            {
                _classBodyStart = classBody.classMember(0).Start.StartIndex;
            }
            else
            {
                // No class body, implementations would start after END-CLASS header
                _classBodyStart = _classDeclarationEnd + 1;
                _lastMethodImplementationEnd = _classDeclarationEnd;
            }

            base.EnterAppClassProgram(context);
        }

        public override void EnterPublicHeader(PublicHeaderContext context)
        {
            _publicHeaderStart = context.Start.StartIndex;
            base.EnterPublicHeader(context);
        }

        public override void ExitPublicHeader(PublicHeaderContext context)
        {
            _publicHeaderEnd = context.Stop.StopIndex;
            base.ExitPublicHeader(context);
        }

        public override void ExitMethodImplementation(MethodImplementationContext context)
        {
            _lastMethodImplementationEnd = context.Stop.StopIndex;
            base.ExitMethodImplementation(context);
        }

        public override void ExitProgram(ProgramContext context)
        {
            if (Editor.DataManager == null)
            {
                SetFailure("No data manager available.");
                base.ExitProgram(context);
                return;
            }

            // Reset locations if not an AppClass program to avoid stale data
            if (context.appClass() == null || context.appClass() is not AppClassProgramContext)
            {
                _publicHeaderEnd = -1;
                _classDeclarationEnd = -1;
                _classBodyStart = -1;
                _lastMethodImplementationEnd = -1;
                _currentClassName = string.Empty;
                _publicHeaderStart = -1;
                base.ExitProgram(context);
                return;
            }

            // Call base before our logic
            base.ExitProgram(context);

            // Parse the program into AST
            var appClass = Ast.Program.Parse(context, _currentClassName, Editor.DataManager);

            // Generate the constructor
            GenerateConstructor(context, appClass);
        }

        private void GenerateConstructor(ProgramContext programContext, Ast.Program program)
        {
            var appClass = program.ContainedAppClass;
            if (appClass == null)
            {
                SetFailure("No AppClass found in the program.");
                return;
            }
            
            // Check if the class extends another class
            var baseClass = appClass.ExtendedClass;
            if (baseClass == null)
            {
                SetFailure("Class does not extend another class.");
                return;
            }

            // Find the constructor of the base class
            var baseConstructor = baseClass.Methods.FirstOrDefault(m => m.IsConstructor);
            if (baseConstructor == null)
            {
                return;
            }

            // Get parameter strings for the constructor
            string declarationParams = string.Join(", ", baseConstructor.Parameters.Select(p => $"{p.Name} As {p.Type.FullText}"));
            string callParams = string.Join(", ", baseConstructor.Parameters.Select(p => p.Name));

            // Standard indentation for PeopleCode
            string indent = "   ";

            // Constructor Header String
            string constructorHeader = $"   method {_currentClassName}({declarationParams});";

            // Constructor Implementation String
            string constructorImpl = 
                $"method {_currentClassName}" + Environment.NewLine +
                $"{indent}%Super = create {baseClass.FullPath}({callParams});" + Environment.NewLine +
                "end-method;";
            string constructorImplWithNewlines = Environment.NewLine + Environment.NewLine + constructorImpl;

            // Add the constructor header to the public section
            InsertConstructorHeader(programContext, constructorHeader);
            
            // If we failed to insert the header, don't continue
            if (!GetResult().Success) return;

            // Add the constructor implementation after the class declaration
            InsertConstructorImplementation(constructorImplWithNewlines);
        }

        private void InsertConstructorHeader(ProgramContext? programContext, string constructorHeader)
        {
            // For header insertion, prefer the public section if it exists
            int insertPos = -1;
            string description = $"Insert constructor header for '{_currentClassName}'";
            string headerTextToInsert = constructorHeader;

            if (_publicHeaderEnd > 0)
            {
                // Insert at the end of the public section
                insertPos = _publicHeaderEnd;
                description += " into public section";
                headerTextToInsert = Environment.NewLine + headerTextToInsert;
            }
            else if (_publicHeaderStart > 0)
            {
                // If we have a public section start but no content yet
                insertPos = _publicHeaderStart;
                description += " into public section";
                headerTextToInsert += Environment.NewLine;
            }
            else
            {
                // No public section found, need to create one or insert before END-CLASS
                description += " (fallback before END-CLASS)";

                // Find the class declaration context
                ClassDeclarationContext? classDeclCtx = null;
                if (programContext != null)
                {
                    var appClassOrInterfaceCtx = programContext.appClass();
                    if (appClassOrInterfaceCtx is AppClassProgramContext appProgCtx)
                    {
                        classDeclCtx = appProgCtx.classDeclaration();
                    }
                }

                if (classDeclCtx != null)
                {
                    // Insert right before the END-CLASS token
                    insertPos = classDeclCtx.Stop.StartIndex;


                    headerTextToInsert = headerTextToInsert + Environment.NewLine;

                }
            }
            
            // Perform the insertion if a position was determined
            if (insertPos != -1)
            {
                InsertText(insertPos, headerTextToInsert, description);
            }
            else
            {
                SetFailure("Could not determine where to insert the constructor header.");
            }
        }

        private void InsertConstructorImplementation(string constructorImpl)
        {
            // For implementation insertion, find the appropriate location
            int insertPos = -1;
            string description = $"Insert constructor implementation for '{_currentClassName}'";

            if (_lastMethodImplementationEnd != -1 && _lastMethodImplementationEnd > _classDeclarationEnd)
            {
                // Insert after the last known method implementation
                insertPos = _lastMethodImplementationEnd + 1;
            }
            else if (_classBodyStart != -1 && _classBodyStart > _classDeclarationEnd)
            {
                // Insert at the start of where the body would be
                insertPos = _classBodyStart;
            }
            else if (_classDeclarationEnd != -1)
            {
                // Fallback: Insert immediately after END-CLASS token
                insertPos = _classDeclarationEnd + 1;
            }

            if (insertPos != -1)
            {
                InsertText(insertPos, constructorImpl, description);
            }
            else
            {
                SetFailure("Could not determine where to insert the constructor implementation.");
            }
        }
    }
} 