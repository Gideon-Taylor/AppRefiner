using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class MeaninglessVariableNameStyler : BaseStyler
    {
        private const uint HIGHLIGHT_COLOR = 0xD9D6A560;
        private HashSet<string> meaninglessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "b", "c", "d", "e", "f", "g", "h", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w",
            "aa", "bb", "cc", "dd", "ee", "ff", "gg", "hh", "ii", "jj", "kk", "ll", "mm", "nn", "oo", "pp", "qq", "rr", "ss", "tt", "uu", "vv", "ww", "xx", "yy", "zz",
            "var", "var1", "var2", "var3", "temp", "tmp", "temp1", "tmp1", "temp2", "tmp2", "foo", "bar", "baz",
            "obj", "object", "str", "string", "num", "number", "int", "integer", "flt", "float", "bool", "boolean",
            "arr", "array", "lst", "list", "val", "value", "res", "result", "ret", "return"
        };

        public MeaninglessVariableNameStyler()
        {
            Description = "Highlights meaningless variable names.";
            Active = true;
        }

        public override void Reset()
        {
            // Reset any state when starting a new analysis
        }

        public override void EnterLocalVariableDefinition(LocalVariableDefinitionContext context)
        {
            CheckVariableNames(context);
        }

        public override void EnterLocalVariableDeclAssignment(LocalVariableDeclAssignmentContext context)
        {
            CheckVariableName(context.USER_VARIABLE().GetText(), context.USER_VARIABLE().Symbol);
        }

        public override void EnterInstanceDecl(InstanceDeclContext context)
        {
            if (context.USER_VARIABLE() != null)
            {
                foreach (var varToken in context.USER_VARIABLE())
                {
                    CheckVariableName(varToken.GetText(), varToken.Symbol);
                }
            }
        }

        public override void EnterMethodArgument(MethodArgumentContext context)
        {
            CheckVariableName(context.USER_VARIABLE().GetText(), context.USER_VARIABLE().Symbol);
        }

        public override void EnterFunctionArgument([NotNull] FunctionArgumentContext context)
        {
            CheckVariableName(context.USER_VARIABLE().GetText(), context.USER_VARIABLE().Symbol);
        }

        private void CheckVariableNames(LocalVariableDefinitionContext context)
        {
            foreach (var varToken in context.USER_VARIABLE())
            {
                CheckVariableName(varToken.GetText(), varToken.Symbol);
            }
        }

        private void CheckVariableName(string variableName, IToken token)
        {
            // Remove & if it's at the start of the variable name
            string cleanName = variableName.StartsWith("&") ? variableName.Substring(1) : variableName;

            // Check if the variable name is in our list of meaningless names
            if (meaninglessNames.Contains(cleanName))
            {
                // Add an indicator for this meaningless variable name
                Indicators?.Add(new Indicator
                {
                    Start = token.StartIndex,
                    Length = token.StopIndex - token.StartIndex + 1,
                    Color = HIGHLIGHT_COLOR,
                    Tooltip = "Meaningless variable name",
                    Type = IndicatorType.HIGHLIGHTER
                });
            }
        }
    }
}
