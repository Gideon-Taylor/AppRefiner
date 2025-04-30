using AppRefiner.Ast;
using AppRefiner.PeopleCode;
using AppRefiner.Services;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms; // For ModifierKeys, Keys if needed later
using static AppRefiner.PeopleCode.PeopleCodeParser; // Add static import for context types

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation to implement required methods and properties from a base class or interface.
    /// Specifically starting with implementing the base class constructor.
    /// </summary>
    public class ImplementAbstractMembersRefactor : BaseRefactor
    {
        public new static string RefactorName => "Implement Base Class Members";
        public new static string RefactorDescription => "Generates method/property stubs required by the parent class or interface, starting with the constructor.";
        public new static bool RegisterKeyboardShortcut => false; // TODO: Maybe enable later?
        public new static bool IsHidden => false;

        /// <summary>
        /// Specifies that the ResolveImports refactor should run after this one.
        /// </summary>
        public override System.Type? FollowUpRefactorType => typeof(ResolveImports); // Override

        // --- Location Tracking --- 
        private int _lastMethodImplementationEnd = -1;
        private int _publicHeaderEnd = -1;
        private int _protectedHeaderEnd = -1;
        private int _privateHeaderEnd = -1; // Add tracker for private header end
        private int _publicHeaderStart = -1; // Add tracker for public header start
        private int _protectedHeaderStart = -1; // Add tracker for protected header start
        private int _privateHeaderStart = -1; // Add tracker for private header start
        private int _classDeclarationEnd = -1; // End of CLASS ... END-CLASS/INTERFACE header
        private int _classBodyStart = -1; // Start of first method implementation

        private ParserRuleContext? _constructorHeaderContext = null;
        private MethodImplementationContext? _constructorImplContext = null;
        private string _currentClassName = string.Empty;

        /// <summary>
        /// Creates a new instance of the ImplementBaseClassRefactor.
        /// </summary>
        /// <param name="editor">The ScintillaEditor instance.</param>
        /// <param name="astService">The AST service for parsing.</param>
        public ImplementAbstractMembersRefactor(ScintillaEditor editor)
            : base(editor) 
        {

        }

        // --- Listener Overrides for Location Tracking --- 

        public override void EnterAppClassProgram(AppClassProgramContext context)
        {
            var declCtx = context.classDeclaration();
            if (declCtx is ClassDeclarationExtensionContext extCtx)
                _currentClassName = extCtx.genericID().GetText();
            else if (declCtx is ClassDeclarationImplementationContext implCtx)
                _currentClassName = implCtx.genericID().GetText();
            else if (declCtx is ClassDeclarationPlainContext plainCtx)
                _currentClassName = plainCtx.genericID().GetText();
                
            _classDeclarationEnd = declCtx.Stop.StopIndex; // End of END-CLASS

            // Find start of class body (first implementation) more reliably
            var classBody = context.classBody();
            if(classBody != null && classBody.classMember().Length > 0)
            {
                _classBodyStart = classBody.classMember(0).Start.StartIndex;
            }
            else
            {
                // No class body, implementations would start after END-CLASS header
                 _classBodyStart = _classDeclarationEnd + 1; // Position after END-CLASS
                 _lastMethodImplementationEnd = _classDeclarationEnd; // No methods yet
            }

            base.EnterAppClassProgram(context);
        }

        // --- Track Start Indices --- 
        public override void EnterPublicHeader(PublicHeaderContext context)
        {
            _publicHeaderStart = context.Start.StartIndex;
            base.EnterPublicHeader(context);
        }
        public override void EnterProtectedHeader(ProtectedHeaderContext context)
        {
             _protectedHeaderStart = context.Start.StartIndex;
            base.EnterProtectedHeader(context);
        }
         public override void EnterPrivateHeader(PrivateHeaderContext context)
        {
             _privateHeaderStart = context.Start.StartIndex;
            base.EnterPrivateHeader(context);
        }

        // --- Track End Indices --- 
        public override void ExitPublicHeader(PublicHeaderContext context)
        {
            _publicHeaderEnd = context.Stop.StopIndex;
            base.ExitPublicHeader(context);
        }
        public override void ExitProtectedHeader(ProtectedHeaderContext context)
        {
            _protectedHeaderEnd = context.Stop.StopIndex;
            base.ExitProtectedHeader(context);
        }
        public override void ExitPrivateHeader(PrivateHeaderContext context)
        {
            _privateHeaderEnd = context.Stop.StopIndex;
            base.ExitPrivateHeader(context);
        }

        public override void EnterMethodImplementation(MethodImplementationContext context)
        {
            // Track the start of the first method implementation (handled in EnterAppClassProgram now)
            // Check if this method is the constructor
            var methodName = context.method()?.genericID()?.GetText();
            if (methodName != null && methodName.Equals(_currentClassName, StringComparison.OrdinalIgnoreCase))
            {
                _constructorImplContext = context; // Store the whole implementation context
            }

            base.EnterMethodImplementation(context);
        }

        public override void ExitMethodImplementation(MethodImplementationContext context)
        {
            _lastMethodImplementationEnd = context.Stop.StopIndex;
            base.ExitMethodImplementation(context);
        }
        
        // Need to find the constructor header declaration as well
        public override void EnterNonPrivateMethodHeader(NonPrivateMethodHeaderContext context)
        {
            var methodName = context.methodHeader()?.genericID()?.GetText();
            if (methodName != null && methodName.Equals(_currentClassName, StringComparison.OrdinalIgnoreCase))
            {
                _constructorHeaderContext = context.methodHeader(); // Store the METHOD header context
            }
             base.EnterNonPrivateMethodHeader(context);
        }
         public override void EnterPrivateMethodHeader(PrivateMethodHeaderContext context)
        {
            var methodName = context.methodHeader()?.genericID()?.GetText();
            if (methodName != null && methodName.Equals(_currentClassName, StringComparison.OrdinalIgnoreCase))
            {
                _constructorHeaderContext = context.methodHeader(); // Store the METHOD header context
            }
             base.EnterPrivateMethodHeader(context);
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
                 _protectedHeaderEnd = -1;
                 _classDeclarationEnd = -1;
                 _classBodyStart = -1;
                 _lastMethodImplementationEnd = -1;
                 _constructorHeaderContext = null;
                 _constructorImplContext = null;
                 _currentClassName = string.Empty;
                 // Reset new fields
                 _privateHeaderEnd = -1; 
                 _publicHeaderStart = -1;
                 _protectedHeaderStart = -1;
                 _privateHeaderStart = -1;
                 // Don't call GenerateConstructorImplementation if it's not an AppClass
                 base.ExitProgram(context);
                 return;
            }

            // Ensure base is called before our logic if needed
            base.ExitProgram(context);

            // Pass the context to the generator method
            var appClass = AppRefiner.Ast.Program.Parse(context, _currentClassName, Editor.DataManager);

            if (GetResult().Success)
            {
                GenerateConstructorImplementation(context, appClass);
                // Only proceed if constructor generation was successful
                if (GetResult().Success)
                {
                    GenerateAbstractMemberStubs(context, appClass);
                }
            }
        }

        private void GenerateConstructorImplementation(ProgramContext programContext, AppRefiner.Ast.Program program)
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
                // No base class, nothing to implement. 
                // We could potentially set a message, but for now, just return silently.
                // SetFailure("Class does not extend another class."); 
                return;
            }

            // Find the constructor of the base class
            // PeopleCode allows omitting super() call if base has no *explicit* constructor.
            // So, only proceed if the base class HAS an explicit constructor defined.
            var baseConstructor = baseClass.Methods.FirstOrDefault(m => m.IsConstructor);
            if (baseConstructor == null)
            {
                // Base class exists but has no explicit constructor. No need to generate a super() call.
                // If there's an existing constructor in the current class that *tries* to call super(), 
                // maybe we should remove that call? For now, let's just do nothing if base has no constructor.
                return; 
            }

            // --- Generate the new constructor code ---

            // 1. Parameter strings
            // Use Type.FullText for the declaration string
            string declarationParams = string.Join(", ", baseConstructor.Parameters.Select(p => $"{p.Name} As {p.Type.FullText}"));
            string callParams = string.Join(", ", baseConstructor.Parameters.Select(p => p.Name));

            // 2. Indentation (assuming standard 3 spaces for PeopleCode)
            // TODO: Get indentation from editor settings or detect from existing code?
            string indent = "   "; 

            // 3. Constructor Header String
            // Needs newline before it if inserting
            string constructorHeader = $"method {_currentClassName}({declarationParams});";
            string constructorHeaderWithNewline = Environment.NewLine + constructorHeader; 

            // 4. Constructor Implementation String
            // Needs surrounding newlines if inserting
             string constructorImpl = 
                $"method {_currentClassName}" + Environment.NewLine +
                $"{indent}%Super = create {baseClass.Name}({callParams});" + Environment.NewLine +
                 "end-method;";
            string constructorImplWithNewlines = Environment.NewLine + Environment.NewLine + constructorImpl;


            // --- Add Changes (Replace or Insert) ---

            // A. Handle Constructor Header Declaration
            if (_constructorHeaderContext != null) 
            {
                 // Replace existing header
                 ReplaceNode(_constructorHeaderContext, constructorHeader, $"Replace existing constructor header for '{_currentClassName}'", true);
            }
            else
            {
                // Insert new header
                int insertPos = -1;
                string description = $"Insert constructor header for '{_currentClassName}'";
                string headerTextToInsert = constructorHeader; // Start with just the header

                // Determine insert position based on base constructor scope
                switch (baseConstructor.Scope)
                {
                    case Scope.Public:
                        if (_publicHeaderStart > 0)
                        {
                            insertPos = _publicHeaderStart;
                            description += " into public section";
                            // Need newline *after* when inserting at start of section content
                            headerTextToInsert += Environment.NewLine;
                        }
                        break;
                    case Scope.Protected:
                         if (_protectedHeaderStart > 0)
                        {
                            insertPos = _protectedHeaderStart;
                            description += " into protected section";
                            headerTextToInsert += Environment.NewLine;
                        }
                        break;
                    case Scope.Private:
                         if (_privateHeaderStart > 0)
                        {
                            insertPos = _privateHeaderStart;
                            description += " into private section";
                            headerTextToInsert += Environment.NewLine;
                        }
                        break;
                }

                // Fallback if scope section doesn't exist or wasn't found
                if (insertPos == -1)
                {
                    description += " (fallback before END-CLASS)";
                    // Use the passed programContext to find the appClass/interface context
                    var appClassOrInterfaceCtx = programContext.appClass(); 
                    ClassDeclarationContext? classDeclCtx = null;
                    if (appClassOrInterfaceCtx is AppClassProgramContext appProgCtx) {
                        classDeclCtx = appProgCtx.classDeclaration();
                    }
                    
                    if (classDeclCtx != null) {
                        // Insert right *before* the END-CLASS token starts
                        insertPos = classDeclCtx.Stop.StartIndex; 
                        // Need newline *before* when inserting before END-CLASS
                        headerTextToInsert = Environment.NewLine + headerTextToInsert + Environment.NewLine; 
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
                     return; // Stop if we can't place the header
                }
            }

            // B. Handle Constructor Implementation
            if (_constructorImplContext != null)
            {
                // Replace existing implementation
                ReplaceNode(_constructorImplContext, constructorImpl, $"Replace existing constructor implementation for '{_currentClassName}'");
            }
            else
            {
                // Insert new implementation
                 // Should be inserted *after* the END-CLASS token.
                int insertPos = -1;
                string description = $"Insert constructor implementation for '{_currentClassName}'";

                if (_lastMethodImplementationEnd != -1 && _lastMethodImplementationEnd > _classDeclarationEnd)
                {
                     // Insert after the last known method implementation
                     insertPos = _lastMethodImplementationEnd + 1; 
                }
                else if (_classBodyStart != -1 && _classBodyStart > _classDeclarationEnd)
                {
                    // Insert at the start of where the body would be (first position after END-CLASS)
                    insertPos = _classBodyStart;
                }
                else if (_classDeclarationEnd != -1) 
                {
                    // Fallback: Insert immediately after END-CLASS token
                    insertPos = _classDeclarationEnd + 1;
                }


                if (insertPos != -1) 
                {
                     // Ensure we add appropriate newlines before the implementation
                     InsertText(insertPos, constructorImplWithNewlines, description);
                }
                else
                {
                     SetFailure("Could not determine where to insert the constructor implementation.");
                     // Don't necessarily return, the header change might still be valid? Or should we rollback?
                     // For now, let's proceed but the result might be incomplete.
                }

            }
        }

        /// <summary>
        /// Generates implementation stubs for inherited abstract methods and properties.
        /// </summary>
        private void GenerateAbstractMemberStubs(ProgramContext programContext, AppRefiner.Ast.Program program)
        {
            var appClass = program.ContainedAppClass;
            if (appClass == null) return; // Should have failed earlier if null

            var (unimplementedMethods, unimplementedProperties) = appClass.GetAllUnimplementedAbstractMembers();

            if (!unimplementedMethods.Any() && !unimplementedProperties.Any())
            {
                return; // Nothing to implement
            }

            // Use existing indentation helper
            string indent = "   "; 
            
            // --- Insertion Point for Implementations (common for methods and properties) ---
            int implementationInsertPos = -1;
            if (_lastMethodImplementationEnd != -1 && _lastMethodImplementationEnd > _classDeclarationEnd)
            {
                /* + 2 so we skip the ending semicolon */
                 implementationInsertPos = _lastMethodImplementationEnd + 2; 
            }
            else if (_classBodyStart != -1 && _classBodyStart > _classDeclarationEnd)
            {
                // Insert at the start of where the body would be (first position after END-CLASS)
                 implementationInsertPos = _classBodyStart; 
            }
            else if (_classDeclarationEnd != -1) 
            {
                /* + 2 so we skip the ending semicolon */
                implementationInsertPos = _classDeclarationEnd + 2;
            }

            if (implementationInsertPos == -1)
            {
                SetFailure("Could not determine where to insert method/property implementations.");
                return; // Cannot proceed without implementation location
            }

            // --- Generate Method Stubs ---
            foreach (var method in unimplementedMethods)
            {
                // 1. Header String
                string paramString = string.Join(", ", method.Parameters.Select(p => $"{p.Name} As {p.Type.FullText}{(p.IsOut ? " out" : "")}"));
                string returnString = method.ReturnType != null ? $" returns {method.ReturnType.FullText}" : "";
                // Use standard indent for header lines
                string methodHeader = $"{indent}method {method.Name}({paramString}){returnString};";

                // 2. Implementation String
                string returnStmt = "";
                if (method.ReturnType != null)
                {
                    returnStmt = Environment.NewLine + indent + $"Return {method.ReturnType.GetDefaultValue()};";
                }
                // Use the new DeclaringTypeFullName property
                string originComment = !string.IsNullOrEmpty(method.DeclaringTypeFullName) ? $"{method.DeclaringTypeFullName}.{method.Name} not implemented for {appClass.Name}" : "Method not implemented";
                string methodImpl = 
                    $"method {method.Name}" + Environment.NewLine + 
                    // Add standard indent to throw line
                    $"{indent}throw CreateException(0, 0, \"{originComment.Replace("\"", "\"\"")}\");" + // Escape quotes
                    returnStmt + Environment.NewLine + 
                    "end-method;";
                // Needs surrounding newlines when inserting individually
                string methodImplWithNewlines = Environment.NewLine + Environment.NewLine + methodImpl;

                // 3. Add Header Change (Insert)
                InsertMemberHeader(programContext, methodHeader, method.Scope, $"Insert abstract method header for '{method.Name}'");
                if (!GetResult().Success) return; // Stop if header insertion failed

                // 4. Add Implementation Change (Insert)
                // Insert implementation *after* the determined point
                InsertText(implementationInsertPos, methodImplWithNewlines, $"Insert abstract method implementation for '{method.Name}'");
                 if (!GetResult().Success) return; // Stop if impl insertion failed
            }

            // --- Generate Property Stubs ---
            foreach (var prop in unimplementedProperties)
            {
                // Abstract properties only need their header declared.
                
                // 1. Header String
                string readonlyKeyword = prop.IsReadonly ? " readonly" : "";
                // Add comment with declaring type name
                string declaringTypeComment = !string.IsNullOrEmpty(prop.DeclaringTypeFullName) ? $" /* Implements {prop.DeclaringTypeFullName}.{prop.Name} */" : ""; 
                 // Use standard indent for header lines
                string propHeader = $"{indent}property {prop.Type.FullText} {prop.Name}{readonlyKeyword};{declaringTypeComment}"; 

                // 2. Add Header Change (Insert)
                InsertMemberHeader(programContext, propHeader, prop.Scope, $"Insert abstract property header for '{prop.Name}'");
                 if (!GetResult().Success) return; // Stop if header insertion failed
            }
        }

        /// <summary>
        /// Helper to insert a class member header into the correct scope section.
        /// </summary>
        private void InsertMemberHeader(ProgramContext programContext, string headerText, Scope scope, string description)
        {
            int insertPos = -1;
            string headerTextToInsert = headerText;

             // Determine insert position based on scope
             switch (scope)
            {
                case Scope.Public:
                    if (_publicHeaderStart > 0)
                    {
                        insertPos = _publicHeaderEnd + 1; // Insert at the end of the public section
                        description += " into public section";
                         headerTextToInsert = Environment.NewLine + headerTextToInsert; // Need newline before
                    }
                    break;
                case Scope.Protected:
                        if (_protectedHeaderStart > 0)
                    {
                        insertPos = _protectedHeaderEnd + 1; // Insert at the end of the protected section
                        description += " into protected section";
                        headerTextToInsert = Environment.NewLine + headerTextToInsert;
                    }
                    break;
                case Scope.Private:
                        if (_privateHeaderStart > 0)
                    {
                        insertPos = _privateHeaderEnd + 1; // Insert at the end of the private section
                        description += " into private section";
                         headerTextToInsert = Environment.NewLine + headerTextToInsert;
                    }
                    break;
            }

            // Fallback if scope section doesn't exist or wasn't found
            if (insertPos == -1)
            {
                description += " (fallback before END-CLASS)";
                var appClassOrInterfaceCtx = programContext.appClass(); 
                ClassDeclarationContext? classDeclCtx = null;
                if (appClassOrInterfaceCtx is AppClassProgramContext appProgCtx) {
                    classDeclCtx = appProgCtx.classDeclaration();
                }
                
                if (classDeclCtx != null) {
                    insertPos = classDeclCtx.Stop.StartIndex; 
                    headerTextToInsert = headerTextToInsert + Environment.NewLine; 
                }
            }
            
            if (insertPos != -1)
            {
                    InsertText(insertPos, headerTextToInsert, description); 
            }
            else
            {
                    SetFailure($"Could not determine where to insert header: {headerText}");
            }
        }

    }
} 