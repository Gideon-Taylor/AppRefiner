using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Refactors.QuickFixes
{
    public class ImplementMissingMethod : BaseRefactor
    {
        public new static string RefactorName => "Implement Missing Method";
        public new static string RefactorDescription => "Generates a basic implementation for a method declared in the header";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => false;

        private AppClassNode? targetClass;
        private MethodNode? targetMethod;
        private MethodNode? baseMethodToOverride;
        private string? baseClassPath;

        public ImplementMissingMethod(ScintillaEditor editor) : base(editor)
        {
        }

        public override void VisitProgram(ProgramNode node)
        {
            Reset();
            base.VisitProgram(node);
        }

        public override void VisitAppClass(AppClassNode node)
        {
            base.VisitAppClass(node);

            if (targetClass != null)
                return;

            targetClass = node;

            // Find method declarations that don't have implementations
            var methodsNeedingImplementation = FindMethodsNeedingImplementation(node);

            if (methodsNeedingImplementation.Count == 0)
            {
                SetFailure("No methods found that need implementation");
                return;
            }

            // Find the method that contains the current cursor position
            targetMethod = FindTargetMethodByCursorPosition(methodsNeedingImplementation);

            if (targetMethod == null)
            {
                // If no method found at cursor, take the first one
                targetMethod = methodsNeedingImplementation.First();
            }

            // Check if this method exists in base class/interface for override annotation
            if (node.BaseClass != null && Editor.DataManager != null)
            {
                baseClassPath = node.BaseClass.TypeName;
                AnalyzeBaseClassForOverride(baseClassPath, targetMethod.Name);
            }

            // Generate the implementation now that we have everything we need
            GenerateMethodImplementation();
        }

        private new void Reset()
        {
            targetClass = null;
            targetMethod = null;
            baseMethodToOverride = null;
            baseClassPath = null;
            base.Reset();
        }

        private MethodNode? FindTargetMethodByCursorPosition(List<MethodNode> methodsNeedingImplementation)
        {
            var currentPosition = CurrentPosition;

            foreach (var method in methodsNeedingImplementation)
            {
                if (method.SourceSpan.ContainsPosition(currentPosition))
                {
                    return method;
                }
            }

            return null;
        }

        private List<MethodNode> FindMethodsNeedingImplementation(AppClassNode classNode)
        {
            var methodsNeedingImpl = new List<MethodNode>();

            // Get all method declarations (methods without implementations)
            var declarations = classNode.Methods.Where(m => m.IsDeclaration).ToList();

            // Get all method implementations
            var implementations = classNode.Methods.Where(m => m.IsImplementation).ToList();

            foreach (var declaration in declarations)
            {
                // Check if there's already an implementation for this method
                bool hasImplementation = implementations.Any(impl =>
                    string.Equals(impl.Name, declaration.Name, StringComparison.OrdinalIgnoreCase));

                if (!hasImplementation)
                {
                    methodsNeedingImpl.Add(declaration);
                }
            }

            return methodsNeedingImpl;
        }

        private void AnalyzeBaseClassForOverride(string baseClassPath, string methodName)
        {
            if (Editor.DataManager == null)
                return;

            try
            {
                var baseClassSource = Editor.DataManager.GetAppClassSourceByPath(baseClassPath);
                if (string.IsNullOrEmpty(baseClassSource))
                    return;

                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(baseClassSource);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var baseProgram = parser.ParseProgram();

                if (baseProgram == null)
                    return;

                FindMethodInBaseClass(baseProgram, methodName);
            }
            catch (Exception)
            {
                // Silently handle errors - override annotation is optional
            }
        }

        private void FindMethodInBaseClass(ProgramNode baseProgram, string methodName)
        {
            MethodNode? baseMethod = null;

            if (baseProgram.AppClass != null)
            {
                baseMethod = baseProgram.AppClass.Methods.FirstOrDefault(m =>
                    string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase));
            }
            else if (baseProgram.Interface != null)
            {
                baseMethod = baseProgram.Interface.Methods.FirstOrDefault(m =>
                    string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase));
            }

            if (baseMethod != null)
            {
                baseMethodToOverride = baseMethod;
            }
        }


        private void GenerateMethodImplementation()
        {
            if (targetMethod == null || targetClass == null)
            {
                SetFailure("No target method or class identified");
                return;
            }

            var implementsComment = baseMethodToOverride != null && !string.IsNullOrEmpty(baseClassPath)
                ? $"{baseClassPath}.{baseMethodToOverride.Name}"
                : null;

            var options = new MethodImplementationOptions
            {
                Type = targetMethod.IsConstructor ? ImplementationType.Constructor : ImplementationType.Missing,
                ImplementsComment = implementsComment,
                BaseClassPath = baseClassPath,
                TargetClassName = targetClass.Name
            };

            var implementation = targetMethod.GenerateDefaultImplementation(options);

            var insertPosition = FindImplementationInsertionPosition(targetMethod.IsConstructor);
            if (insertPosition >= 0)
            {
                InsertText(insertPosition, implementation, $"Insert implementation for method '{targetMethod.Name}'");
            }
            else
            {
                SetFailure("Could not determine where to insert method implementation");
            }
        }


        private int FindImplementationInsertionPosition(bool isConstructor)
        {
            if (targetClass == null)
                return -1;



            if (isConstructor)
            {
                /* We want to target the first one... */
                var firstImplementation = targetClass.Methods
                .Where(m => m.IsImplementation)
                .OrderBy(m => m.SourceSpan.End.ByteIndex)
                .FirstOrDefault();

                if (firstImplementation != null && firstImplementation.Implementation != null)
                {
                    return firstImplementation.Implementation.SourceSpan.Start.ByteIndex;
                }
            }
            else
            {
                var lastImplementation = targetClass.Methods
                .Where(m => m.IsImplementation)
                .OrderBy(m => m.SourceSpan.End.ByteIndex)
                .LastOrDefault();

                if (lastImplementation != null && lastImplementation.Implementation != null)
                {
                    return lastImplementation.Implementation.SourceSpan.Start.ByteIndex;
                }
            }

            return targetClass.SourceSpan.End.ByteIndex + 1;
        }
    }
}