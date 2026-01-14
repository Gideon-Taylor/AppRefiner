using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;
using PeopleCodeTypeInfo.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.Stylers
{
    public class FunctionParameterNames : BaseStyler
    {
        public override string Description => "Parameter names";

        int SCI_INLAYHINTADD = 2900;
        int SCI_INLAYHINTSETTEXT = 2901;
        int SCI_INLAYHINTSETSTYLE = 2902;
        int SCI_INLAYHINTREMOVE = 2903;
        int SCI_INLAYHINTCLEARLINE = 2904;
        int SCI_INLAYHINTCLEARALL = 2905;
        int SCI_INLAYHINTLINEFROMHANDLE = 2906;
        int SCI_INLAYHINTPOSITIONFROMHANDLE = 2907;
        int SCI_INLAYHINTHANDLEFROMLINE = 2908;
        int SCI_INLAYHINTCOUNT = 2909;
        int SCI_STYLESETFORE = 2051;
        int SCI_STYLESETBACK = 2052;
        int SCI_STYLESETBOLD = 2053;
        int SCI_STYLESETITALIC = 2054;
        int SCI_STYLESETSIZE = 2055;
        const int STYLE_INLAY_HINT = 40;
        Dictionary<string, nint> paramNameAddresses = new();
        RemoteBuffer paramNameBuffer;
        int currentLineNumber = -1;
        public override void VisitProgram(ProgramNode node)
        {
            paramNameAddresses.Clear();
            Editor.SendMessage(SCI_INLAYHINTCLEARALL, 0, 0);

            Editor?.SendMessage(SCI_STYLESETFORE, STYLE_INLAY_HINT, 0x808080);  // Gray
            Editor?.SendMessage(SCI_STYLESETITALIC, STYLE_INLAY_HINT, 1);
            Editor?.SendMessage(SCI_STYLESETSIZE, STYLE_INLAY_HINT, 9);

            paramNameBuffer = Editor.AppDesignerProcess.MemoryManager.GetOrCreateBuffer("parameterNames");
            paramNameBuffer.Reset();

            currentLineNumber = ScintillaManager.GetCurrentLineNumber(Editor);

            base.VisitProgram(node);
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            base.VisitFunctionCall(node);

            if (node.SourceSpan.Start.Line != currentLineNumber)
                return;

            // Get function info attached to the node (populated by type inference)
            var funcInfo = node.GetFunctionInfo();
            if (funcInfo == null || node.Arguments.Count == 0)
                return;

            // Get argument types from inferred types
            List<TypeInfo> arguments = new();
            foreach (var arg in node.Arguments)
            {
                var inferredType = arg.GetInferredType();
                if (inferredType == null)
                {
                    inferredType = UnknownTypeInfo.Instance;
                }
                arguments.Add(inferredType);
            }

            // Get TypeResolver from editor
            var typeResolver = Editor?.AppDesignerProcess?.TypeResolver;
            if (typeResolver == null)
            {
                // No type resolver available - show generic names
                DisplayGenericParameterNames(node);
                return;
            }

            // Validate and get parameter mappings
            var validator = new FunctionCallValidator(typeResolver);
            var result = validator.Validate(funcInfo, arguments.ToArray());

            if (result.IsValid && result.ArgumentMappings != null)
            {
                // Display actual parameter names from mappings
                foreach (var mapping in result.ArgumentMappings)
                {
                    if (mapping.ArgumentIndex >= node.Arguments.Count)
                        continue;

                    var arg = node.Arguments[mapping.ArgumentIndex];

                    // Use actual parameter name or fallback to generic
                    var displayName = string.IsNullOrEmpty(mapping.ParameterName)
                        ? $"arg{mapping.ArgumentIndex}"
                        : mapping.ParameterName;

                    var paramText = $"{displayName}:";

                    // Get or create buffer address for this parameter name
                    nint paramNameAddr;
                    if (!paramNameAddresses.TryGetValue(paramText, out paramNameAddr))
                    {
                        paramNameAddr = (nint)paramNameBuffer.WriteString(paramText, Encoding.UTF8);
                        paramNameAddresses.Add(paramText, paramNameAddr);
                    }

                    // Add inlay hint
                    int hint = (int)Editor?.SendMessage(SCI_INLAYHINTADD, arg.SourceSpan.Start.Line, arg.SourceSpan.Start.Column - 1);
                    Editor?.SendMessage(SCI_INLAYHINTSETTEXT, hint, paramNameAddr);
                    Editor?.SendMessage(SCI_INLAYHINTSETSTYLE, hint, STYLE_INLAY_HINT);
                }
            }
            else
            {
                // Validation failed - show generic names
                DisplayGenericParameterNames(node);
            }
        }

        private void DisplayGenericParameterNames(FunctionCallNode node)
        {
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                var arg = node.Arguments[i];
                var paramText = $"arg{i}:";

                nint paramNameAddr;
                if (!paramNameAddresses.TryGetValue(paramText, out paramNameAddr))
                {
                    paramNameAddr = (nint)paramNameBuffer.WriteString(paramText, Encoding.UTF8);
                    paramNameAddresses.Add(paramText, paramNameAddr);
                }

                int hint = (int)Editor?.SendMessage(SCI_INLAYHINTADD, arg.SourceSpan.Start.Line, arg.SourceSpan.Start.Column - 1);
                Editor?.SendMessage(SCI_INLAYHINTSETTEXT, hint, paramNameAddr);
                Editor?.SendMessage(SCI_INLAYHINTSETSTYLE, hint, STYLE_INLAY_HINT);
            }
        }

    }
}
