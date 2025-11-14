using PeopleCodeParser.SelfHosted.Nodes;
using System.Text;

namespace AppRefiner.Refactors.QuickFixes
{
    public class GenerateBaseConstructor : BaseRefactor
    {
        public new static string RefactorName => "Generate Base Constructor";
        public new static string RefactorDescription => "Generates a constructor that calls the parent class constructor";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => false;

        private AppClassNode? targetClass;
        private MethodNode? baseConstructor;
        private HashSet<string> existingMemberNames = new(StringComparer.OrdinalIgnoreCase);

        public GenerateBaseConstructor(ScintillaEditor editor) : base(editor)
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

            var existingConstructor = node.Methods.FirstOrDefault(m =>
                string.Equals(m.Name, node.Name, StringComparison.OrdinalIgnoreCase));

            if (existingConstructor != null)
                return;

            if (node.BaseType == null)
            {
                SetFailure("Class does not extend another class");
                return;
            }

            targetClass = node;
            CollectExistingMemberNames(node);

            if (Editor.DataManager != null)
            {
                AnalyzeBaseClass(node.BaseType.TypeName);
            }
            else
            {
                SetFailure("No data manager available");
            }
        }

        private new void Reset()
        {
            targetClass = null;
            baseConstructor = null;
            existingMemberNames.Clear();
            base.Reset();
        }

        private void CollectExistingMemberNames(AppClassNode classNode)
        {
            foreach (var variable in classNode.InstanceVariables)
            {
                existingMemberNames.Add(variable.Name);
            }

            foreach (var property in classNode.Properties)
            {
                existingMemberNames.Add(property.Name);
            }

            foreach (var method in classNode.Methods)
            {
                existingMemberNames.Add(method.Name);
            }
        }

        private void AnalyzeBaseClass(string baseClassPath)
        {
            if (Editor.DataManager == null)
            {
                SetFailure("No data manager available");
                return;
            }

            try
            {
                var baseClassSource = Editor.DataManager.GetAppClassSourceByPath(baseClassPath);
                if (string.IsNullOrEmpty(baseClassSource))
                {
                    SetFailure($"Base class '{baseClassPath}' not found in database");
                    return;
                }

                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(baseClassSource);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var baseProgram = parser.ParseProgram();

                if (baseProgram == null)
                {
                    SetFailure($"Could not parse base class '{baseClassPath}'");
                    return;
                }

                FindBaseConstructor(baseProgram, baseClassPath);
            }
            catch (Exception ex)
            {
                SetFailure($"Error analyzing base class: {ex.Message}");
            }
        }

        private void FindBaseConstructor(ProgramNode baseProgram, string baseClassPath)
        {
            MethodNode? constructor = null;
            string? baseClassName = null;

            if (baseProgram.AppClass != null)
            {
                var baseClass = baseProgram.AppClass;
                baseClassName = baseClass.Name;
                constructor = baseClass.Methods.FirstOrDefault(m =>
                    string.Equals(m.Name, baseClass.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (constructor?.Parameters.Count > 0)
            {
                baseConstructor = constructor;
                GenerateConstructor();
            }
            else if (constructor != null)
            {
                SetFailure("Base class constructor has no parameters");
            }
            else
            {
                SetFailure($"No constructor found in base class '{baseClassPath}'");
            }
        }

        private void GenerateConstructor()
        {
            if (targetClass == null || baseConstructor == null)
                return;

            var safeParameters = GenerateSafeParameters();
            GenerateConstructorHeader(safeParameters);
            if (GetResult().Success)
            {
                GenerateConstructorImplementation(safeParameters);
            }
        }

        private void GenerateConstructorHeader(List<(string Name, string Type)> safeParameters)
        {
            var parameterList = string.Join(", ", safeParameters.Select(p => $"{p.Name} As {p.Type}"));
            var header = $"   method {targetClass!.Name}({parameterList});";
            
            var insertPosition = FindHeaderInsertionPosition();
            if (insertPosition >= 0)
            {
                var headerWithNewline = header + Environment.NewLine;
                InsertText(insertPosition, headerWithNewline, $"Insert constructor header for '{targetClass.Name}'");
            }
            else
            {
                SetFailure("Could not determine where to insert constructor header");
            }
        }

        private void GenerateConstructorImplementation(List<(string Name, string Type)> safeParameters)
        {
            var parameterList = string.Join(", ", safeParameters.Select(p => p.Name));
            var baseClassPath = targetClass!.BaseType!.TypeName;

            // Generate parameter annotations
            var annotations = new StringBuilder();
            for (int i = 0; i < safeParameters.Count; i++)
            {
                var param = safeParameters[i];
                var comma = (i < safeParameters.Count - 1) ? "," : "";
                annotations.AppendLine($"   /+ {param.Name} as {param.Type}{comma} +/");
            }

            var implementation = $"method {targetClass.Name}" + Environment.NewLine +
                                annotations.ToString() +
                                $"   %Super = create {baseClassPath}({parameterList});" + Environment.NewLine +
                                Environment.NewLine +
                                "end-method;" + Environment.NewLine;

            var insertPosition = FindImplementationInsertionPosition();
            if (insertPosition >= 0)
            {
                var fullImplementation = Environment.NewLine + Environment.NewLine + implementation;
                InsertText(insertPosition, fullImplementation, $"Insert constructor implementation for '{targetClass.Name}'");
            }
            else
            {
                SetFailure("Could not determine where to insert constructor implementation");
            }
        }

        private List<(string Name, string Type)> GenerateSafeParameters()
        {
            var safeParameters = new List<(string Name, string Type)>();

            foreach (var param in baseConstructor!.Parameters)
            {
                var paramType = param.Type?.ToString() ?? "any";
                var safeName = GenerateSafeParameterName(param.Name);
                safeParameters.Add((safeName, paramType));
            }

            return safeParameters;
        }

        private string GenerateSafeParameterName(string baseName)
        {
            string safeName = baseName;
            int counter = 1;

            while (existingMemberNames.Contains(safeName))
            {
                safeName = $"{baseName}{counter++}";
            }

            existingMemberNames.Add(safeName);
            return safeName;
        }




        private int FindHeaderInsertionPosition()
        {
            if (targetClass == null) return -1;

            if (targetClass.BaseType != null)
            {
                var insertLine = targetClass.BaseType.SourceSpan.Start.Line + 1; /* Line after the extends/implements... */
                return ScintillaManager.GetLineStartIndex(Editor, insertLine);
            }
            return -1;
        }

        private int FindImplementationInsertionPosition()
        {
            if (targetClass == null) return -1;

            var firstImplementation = targetClass.Methods
                .Where(m => m.IsImplementation)
                .OrderBy(m => m.SourceSpan.End.ByteIndex)
                .FirstOrDefault();

            if (firstImplementation != null && firstImplementation.Implementation != null)
            {
                return firstImplementation.Implementation.SourceSpan.End.ByteIndex + 1;
            }

            return targetClass.SourceSpan.End.ByteIndex + 1;
        }
    }
}