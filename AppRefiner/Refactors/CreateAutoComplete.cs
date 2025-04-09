using Antlr4.Runtime;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that provides auto-completion for create() statements based on variable types
    /// </summary>
    public class CreateAutoComplete : ScopedRefactor<string>
    {
        public new static string RefactorName => "Create Auto Complete";
        public new static string RefactorDescription => "Auto-completes create() statements with appropriate class types";

        /// <summary>
        /// This refactor should not have a keyboard shortcut
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from refactoring lists
        /// </summary>
        public new static bool IsHidden => true;

        private bool isAppropriateContext = false;
        private string? detectedClassType = null;
        private int createStartPos = -1;
        private int createEndPos = -1;
        private bool autoPairingEnabled;

        // Track instance variables and their types
        private readonly Dictionary<string, string> instanceVariables = new();

        // Track parent class information for %Super usage
        private string? parentClassName = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateAutoComplete"/> class
        /// </summary>
        /// <param name="editor">The Scintilla editor instance to use for this refactor</param>
        /// <param name="autoPairingEnabled">Whether auto-pairing is enabled, determines if closing parenthesis should be added</param>
        public CreateAutoComplete(ScintillaEditor editor, bool autoPairingEnabled = true)
            : base(editor)
        {
            this.autoPairingEnabled = autoPairingEnabled;
            Debug.Log($"CreateAutoComplete initialized with auto-pairing: {autoPairingEnabled}");
        }

        /// <summary>
        /// Track class extension information
        /// </summary>
        public override void EnterClassDeclarationExtension(ClassDeclarationExtensionContext context)
        {
            base.EnterClassDeclarationExtension(context);

            // Get the superclass information
            var superclass = context.superclass();
            if (superclass is AppClassSuperClassContext appClassSuper)
            {
                // Extract the parent class name from the app class path
                parentClassName = appClassSuper.appClassPath()?.GetText();
                Debug.Log($"Class extends {parentClassName}");
            }
            else if (superclass is SimpleTypeSuperclassContext simpleTypeSuper)
            {
                // Handle built-in types like Exception
                var simpleType = simpleTypeSuper.simpleType();
                if (simpleType != null)
                {
                    parentClassName = simpleType.GetText();
                    Debug.Log($"Class extends simple type {parentClassName}");
                }
            }
            else if (superclass is ExceptionSuperClassContext)
            {
                // Handle the built-in Exception class
                parentClassName = "Exception";
                Debug.Log("Class extends Exception");
            }
        }

        /// <summary>
        /// Track class implementation information
        /// </summary>
        public override void EnterClassDeclarationImplementation(ClassDeclarationImplementationContext context)
        {
            base.EnterClassDeclarationImplementation(context);

            // Get the interface class path
            var interfacePath = context.appClassPath();
            if (interfacePath != null)
            {
                // For implementations, we don't set parentClassName 
                // because %Super wouldn't be needed for interfaces
                Debug.Log($"Class implements {interfacePath.GetText()}");
            }
        }

        /// <summary>
        /// Process local variable declaration assignments to capture type information
        /// </summary>
        public override void EnterLocalVariableDeclAssignment(LocalVariableDeclAssignmentContext context)
        {
            base.EnterLocalVariableDeclAssignment(context);

            // Check if this is a create() expression in a local variable declaration assignment
            var expr = context.expression();
            if (expr != null && IsCreateExpressionAtCursor(expr))
            {
                // We're in a local variable declaration, so get the type info directly
                var typeContext = context.typeT();
                if (typeContext is AppClassTypeContext appClass)
                {
                    detectedClassType = appClass.appClassPath()?.GetText();
                    isAppropriateContext = true;
                }
            }
        }

        /// <summary>
        /// Process variable assignments to handle the second case
        /// </summary>
        public override void EnterExpressionStmt(ExpressionStmtContext context)
        {
            var expr = context.expression();
            if (expr != null && expr is EqualityExprContext equalityExpr)
            {
                // Check if this might be an assignment statement (lhs = rhs)
                var lhsExpr = equalityExpr.expression(0);
                var rhsExpr = equalityExpr.expression(1);

                if (rhsExpr != null && IsCreateExpressionAtCursor(rhsExpr))
                {
                    // Try to get variable type info from the left-hand side
                    if (lhsExpr is IdentifierExprContext identExpr)
                    {
                        var ident = identExpr.ident();
                        if (ident is IdentUserVariableContext userVarContext)
                        {
                            string varName = userVarContext.USER_VARIABLE().GetText();
                            if (TryGetVariableInfo(varName, out var varInfo) && varInfo != null)
                            {
                                string varType = varInfo.Type;

                                // Check if it's an app class type
                                if (varType.Contains(":") && !varType.StartsWith("Array of "))
                                {
                                    detectedClassType = varType;
                                    isAppropriateContext = true;
                                }
                                else
                                {
                                    // Variable exists but type is not an app class
                                    isAppropriateContext = true;
                                }
                            }
                            else
                            {
                                /* Maybe its a private instance or property */
                                // Check if the variable is a property
                                if (instanceVariables.TryGetValue(varName, out var varType) &&
                                    varType.Contains(":") && !varType.StartsWith("Array of "))
                                {
                                    detectedClassType = varType;
                                    isAppropriateContext = true;
                                }
                                else
                                {
                                    // Variable doesn't exist or type is not an app class
                                    isAppropriateContext = true;
                                }
                            }
                        }
                        else if (ident is IdentSuperContext)
                        {
                            // Handle %Super = create() case
                            if (parentClassName != null)
                            {
                                detectedClassType = parentClassName;
                                isAppropriateContext = true;
                            }
                            else
                            {
                                // Parent class exists but we couldn't determine its type
                                isAppropriateContext = true;
                            }
                        }
                    }
                    // Or a property access with %This
                    else if (lhsExpr is DotAccessExprContext dotAccessExpr)
                    {
                        // Check if the expression is %THIS
                        var baseExpr = dotAccessExpr.expression();
                        if (baseExpr != null && baseExpr.GetText().Equals("%THIS", StringComparison.OrdinalIgnoreCase))
                        {
                            // Get the property name
                            var dotAccesses = dotAccessExpr.dotAccess();
                            if (dotAccesses != null && dotAccesses.Length > 0)
                            {
                                var propertyName = dotAccesses[0].genericID()?.GetText();

                                if (propertyName != null)
                                {
                                    // Convert property name to instance variable format for lookup
                                    string instanceVarName = $"&{propertyName}";

                                    // Look up the instance variable type
                                    if (instanceVariables.TryGetValue(instanceVarName, out var varType) &&
                                        varType.Contains(":") && !varType.StartsWith("Array of "))
                                    {
                                        detectedClassType = varType;
                                        isAppropriateContext = true;
                                    }
                                    else
                                    {
                                        // Property exists but type is not an app class or unknown
                                        isAppropriateContext = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // This is a property assignment, we can't easily determine the property type
                            // Just use the basic create auto-completion
                            isAppropriateContext = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Process super assignments
        /// </summary>
        public override void EnterSuperAssignmentStmt(SuperAssignmentStmtContext context)
        {
            base.EnterSuperAssignmentStmt(context);

            // Check if this is a %Super = create() case
            var expr = context.expression();
            if (expr != null && IsCreateExpressionAtCursor(expr))
            {
                // Use parent class type if available
                if (parentClassName != null)
                {
                    detectedClassType = parentClassName;
                    isAppropriateContext = true;
                }
                else
                {
                    // Parent class exists but we couldn't determine its type
                    isAppropriateContext = true;
                }
            }
        }

        /// <summary>
        /// Track instance variables to capture their types
        /// </summary>
        public override void EnterPrivateProperty(PrivatePropertyContext context)
        {
            base.EnterPrivateProperty(context);

            var instanceDeclContext = context.instanceDeclaration();
            if (instanceDeclContext is InstanceDeclContext instanceDecl)
            {
                // Get the type information
                var typeContext = instanceDecl.typeT();
                string typeStr = "Any"; // Default type if not specified

                if (typeContext is AppClassTypeContext appClass)
                {
                    typeStr = appClass.appClassPath()?.GetText() ?? "Any";
                }
                else if (typeContext != null)
                {
                    typeStr = ScopedRefactor<string>.GetTypeFromContext(typeContext);
                }

                // Process each variable in the instance declaration
                foreach (var varNode in instanceDecl.USER_VARIABLE())
                {
                    string varName = varNode.GetText();
                    instanceVariables[varName] = typeStr;
                }
            }
        }

        /// <summary>
        /// Track property declarations with Get/Set to capture their types
        /// </summary>
        public override void EnterPropertyGetSet(PropertyGetSetContext context)
        {
            base.EnterPropertyGetSet(context);

            // Get the property type information
            var typeContext = context.typeT();
            string typeStr = "Any"; // Default type if not specified

            if (typeContext is AppClassTypeContext appClass)
            {
                typeStr = appClass.appClassPath()?.GetText() ?? "Any";
            }
            else if (typeContext != null)
            {
                typeStr = ScopedRefactor<string>.GetTypeFromContext(typeContext);
            }

            // Get the property name
            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                string propName = genericIdNode.GetText();

                // Store the property in our dictionary with the instance variable prefix
                // since properties are accessed via the instance variable syntax
                string instanceVarName = $"&{propName}";
                instanceVariables[instanceVarName] = typeStr;
            }
        }

        /// <summary>
        /// Track direct property declarations to capture their types
        /// </summary>
        public override void EnterPropertyDirect(PropertyDirectContext context)
        {
            base.EnterPropertyDirect(context);

            // Get the property type information
            var typeContext = context.typeT();
            string typeStr = "Any"; // Default type if not specified

            if (typeContext is AppClassTypeContext appClass)
            {
                typeStr = appClass.appClassPath()?.GetText() ?? "Any";
            }
            else if (typeContext != null)
            {
                typeStr = ScopedRefactor<string>.GetTypeFromContext(typeContext);
            }

            // Get the property name
            var genericIdNode = context.genericID();
            if (genericIdNode != null)
            {
                string propName = genericIdNode.GetText();

                // Store the property in our dictionary with the instance variable prefix
                // since properties are accessed via the instance variable syntax
                string instanceVarName = $"&{propName}";
                instanceVariables[instanceVarName] = typeStr;
            }
        }

        /// <summary>
        /// Check if the expression is a create() call and cursor is between the parentheses
        /// </summary>
        private bool IsCreateExpressionAtCursor(ExpressionContext expr)
        {
            if (expr is FunctionCallExprContext functionCallExpr)
            {
                var simpleFunc = functionCallExpr.simpleFunctionCall();
                if (simpleFunc != null && simpleFunc.genericID()?.GetText().ToLower() == "create")
                {
                    var args = simpleFunc.functionCallArguments();
                    if (simpleFunc.LPAREN() != null && simpleFunc.RPAREN() != null)
                    {
                        createStartPos = simpleFunc.genericID().Stop.StopIndex + 1;  // Position after "create"
                        createEndPos = simpleFunc.RPAREN().Symbol.StartIndex;        // Position of the closing parenthesis

                        // Check if cursor is between the parentheses
                        return CurrentPosition > createStartPos && CurrentPosition <= createEndPos;
                    }
                }
            }
            else if (expr is ObjectCreateExprContext)
            {
                // Handle ObjectCreateExpr if needed
                // This would be for "create <classname>()" syntax which we don't need to handle
            }
            return false;
        }

        /// <summary>
        /// Complete the traversal and generate changes
        /// </summary>
        public override void ExitProgram(ProgramContext context)
        {
            // Debug log the instance variables and properties we've tracked
            var varCount = instanceVariables.Count;
            Debug.Log($"CreateAutoComplete tracked {varCount} instance variables and properties");
            foreach (var pair in instanceVariables)
            {
                Debug.Log($"  {pair.Key}: {pair.Value}");
            }

            if (parentClassName != null)
            {
                Debug.Log($"Parent class detected: {parentClassName}");
            }

            if (!isAppropriateContext)
            {
                SetFailure("Not in a valid create() statement context");
                return;
            }

            if (createStartPos < 0 || createEndPos < 0)
            {
                SetFailure("Could not locate create() statement bounds");
                return;
            }

            // Determine what to insert based on class type and auto-pairing status
            if (detectedClassType != null)
            {
                string insertText;

                // If auto-pairing is disabled, we need to add both opening and closing parentheses
                insertText = " " + detectedClassType + "(";

                // Replace from after "create" to the opening parenthesis and add both parentheses
                ReplaceText(
                    createStartPos,
                    autoPairingEnabled ? createStartPos : createStartPos + 1, // +1 to include the original opening parenthesis
                    insertText,
                    "Auto-complete create statement with type information (auto-pairing disabled)"
                );

            }
            else
            {
                // Just add a space after "create" without changing anything else
                string insertText = " ";

                // Insert just a space after "create" without affecting the parenthesis
                ReplaceText(
                    createStartPos,
                    createStartPos,
                    insertText,
                    "Auto-complete create statement with space"
                );
            }
        }
    }
}