using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using PeopleCodeTypeInfo.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.Stylers
{
    public class FunctionParameterNames : BaseStyler
    {
        public override string Description => "Parameter names";

        int SCI_INLAYHINTCLEARALL = 2904;
        int SCI_SETINLAYINFO = 2905;
        int SCI_INLAYHINTSSUPPORTED = 2906;
        int SCI_STYLESETFORE = 2051;
        int SCI_STYLESETITALIC = 2054;
        int SCI_STYLESETBOLD = 2053;
        int SCI_STYLESETSIZE = 2055;
        const int STYLE_INLAY_HINT = 40;

        [StructLayout(LayoutKind.Sequential)]
        private struct Sci_InlayHintInfo
        {
            public IntPtr line;
            public IntPtr position;
            public IntPtr text;
            public int style;
            [MarshalAs(UnmanagedType.I1)]
            public bool paddingLeft;
            [MarshalAs(UnmanagedType.I1)]
            public bool paddingRight;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Sci_InlayHintSet
        {
            public nuint count;
            public IntPtr hints;
        }

        private struct DesiredInlayHint
        {
            public int Line { get; set; }
            public int Position { get; set; }
            public string Text { get; set; }
            public IntPtr TextAddress { get; set; }

            public string GetLocationKey() => $"{Line}:{Position}";
        }

        Dictionary<string, nint> paramNameAddresses = new();
        RemoteBuffer paramNameBuffer;
        int currentLineNumber = -1;
        List<DesiredInlayHint> desiredHints = new();
        ScintillaEditor? lastEditor;
        bool stylesConfigured;
        int previousHintHash;
        HashCode runningHash;
        public override void VisitProgram(ProgramNode node)
        {
            // Check if inlay hints are supported by this version of Scintilla
            var inlayHintsSupported = Editor?.SendMessage(SCI_INLAYHINTSSUPPORTED, 0, 0) ?? IntPtr.Zero;
            if (inlayHintsSupported == IntPtr.Zero || inlayHintsSupported.ToInt32() != 1)
            {
                // Inlay hints not supported - skip this styler
                return;
            }

            // If the editor changed, invalidate cached remote addresses and styles
            if (Editor != lastEditor)
            {
                paramNameAddresses.Clear();
                stylesConfigured = false;
                previousHintHash = 0;
                lastEditor = Editor;
            }

            // Clear per-run state (string buffer and addresses persist across runs)
            desiredHints.Clear();
            runningHash = new HashCode();

            // Configure inlay hint style (only once per editor)
            if (!stylesConfigured)
            {
                Editor?.SendMessage(SCI_STYLESETFORE, STYLE_INLAY_HINT, 0x808080);  // Gray
                Editor?.SendMessage(SCI_STYLESETITALIC, STYLE_INLAY_HINT, 1);
                Editor?.SendMessage(SCI_STYLESETBOLD, STYLE_INLAY_HINT, 1);
                Editor?.SendMessage(SCI_STYLESETSIZE, STYLE_INLAY_HINT, 8);  // Smaller font to reduce horizontal space
                stylesConfigured = true;
            }

            // Get or create string buffer (persists across runs — only new unique strings get written)
            paramNameBuffer = Editor.AppDesignerProcess.MemoryManager.GetOrCreateBuffer("parameterNames");

            currentLineNumber = ScintillaManager.GetCurrentLineNumber(Editor);

            // Traverse AST to collect desired hints
            base.VisitProgram(node);

            // Skip synchronization if the hint set is identical to the previous run
            var currentHash = runningHash.ToHashCode();
            if (currentHash == previousHintHash && desiredHints.Count > 0)
            {
                return;
            }
            previousHintHash = currentHash;

            // Synchronize desired hints with current Scintilla state
            SynchronizeInlayHints();
        }

        public override void VisitObjectCreation(ObjectCreationNode node)
        {
            base.VisitObjectCreation(node);

            var functionInfo = node.GetFunctionInfo();
            if (functionInfo == null || node.Arguments.Count == 0)
                return;

            CollectHintsForArguments(functionInfo, node.Arguments);
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            base.VisitFunctionCall(node);

            var funcInfo = node.GetFunctionInfo();
            if (funcInfo == null || node.Arguments.Count == 0)
                return;

            CollectHintsForArguments(funcInfo, node.Arguments);
        }

        private void CollectHintsForArguments(FunctionInfo functionInfo, List<ExpressionNode> arguments)
        {
            var argTypes = new TypeInfo[arguments.Count];
            for (int i = 0; i < arguments.Count; i++)
                argTypes[i] = arguments[i].GetInferredType() ?? UnknownTypeInfo.Instance;

            var typeResolver = Editor?.AppDesignerProcess?.TypeResolver;
            if (typeResolver == null)
            {
                CollectGenericParameterNames(arguments);
                return;
            }

            var validator = new FunctionCallValidator(typeResolver);
            var result = validator.Validate(functionInfo, argTypes);

            if (result.IsValid && result.ArgumentMappings != null)
            {
                foreach (var mapping in result.ArgumentMappings)
                {
                    if (mapping.ArgumentIndex >= arguments.Count)
                        continue;

                    var arg = arguments[mapping.ArgumentIndex];
                    var displayName = string.IsNullOrEmpty(mapping.ParameterName)
                        ? $"arg{mapping.ArgumentIndex}"
                        : mapping.ParameterName;

                    AddDesiredHint(arg.SourceSpan.Start.Line, arg.SourceSpan.Start.Column - 1, $"{displayName}:");
                }
            }
            else
            {
                CollectGenericParameterNames(arguments);
            }
        }

        private void CollectGenericParameterNames(List<ExpressionNode> arguments)
        {
            for (int i = 0; i < arguments.Count; i++)
            {
                var arg = arguments[i];
                AddDesiredHint(arg.SourceSpan.Start.Line, arg.SourceSpan.Start.Column - 1, $"arg{i}:");
            }
        }

        /// <summary>
        /// Adds a desired inlay hint to the collection.
        /// Ensures the text is written to RemoteBuffer and address is cached.
        /// </summary>
        private void AddDesiredHint(int line, int position, string paramText)
        {
            // Get or create buffer address for this parameter name
            IntPtr paramNameAddr;
            if (!paramNameAddresses.TryGetValue(paramText, out paramNameAddr))
            {
                var writeResult = paramNameBuffer.WriteString(paramText, Encoding.UTF8);
                if (writeResult == null)
                {
                    // Buffer full - need to handle this
                    Debug.Log($"FunctionParameterNames: Warning - Buffer full, cannot add hint '{paramText}'");
                    return;
                }
                paramNameAddr = writeResult.Value;
                paramNameAddresses.Add(paramText, paramNameAddr);
            }

            // Add to desired hints collection and update running hash
            desiredHints.Add(new DesiredInlayHint
            {
                Line = line,
                Position = position,
                Text = paramText,
                TextAddress = paramNameAddr
            });
            runningHash.Add(line);
            runningHash.Add(position);
            runningHash.Add(paramText);
        }

        /// <summary>
        /// Sends all desired inlay hints to Scintilla as a single atomic operation.
        /// Allocates a buffer of Sci_InlayHintInfo structs and calls SCI_SETINLAYINFO
        /// with lParam=1 to clear existing hints and apply the new set atomically.
        /// </summary>
        private void SynchronizeInlayHints()
        {
            if (Editor == null)
                return;

            if (desiredHints.Count == 0)
            {
                Editor.SendMessage(SCI_INLAYHINTCLEARALL, 0, 0);
                Debug.Log("FunctionParameterNames: Cleared all inlay hints (no desired hints)");
                return;
            }

            int hintSize = Marshal.SizeOf<Sci_InlayHintInfo>();
            int setSize = Marshal.SizeOf<Sci_InlayHintSet>();
            uint arraySize = (uint)(hintSize * desiredHints.Count);
            uint totalSize = (uint)setSize + arraySize;

            // Get or create buffer sized for the set struct + the array of hint structs
            var infoBuffer = Editor.AppDesignerProcess.MemoryManager.GetOrCreateBuffer("inlayInfoSet", totalSize);
            infoBuffer.Reset();

            // Reserve space for the Sci_InlayHintSet struct at the start (we'll write it after the array)
            var setAddress = infoBuffer.Address;
            infoBuffer.Write(new byte[setSize]);

            // Write each hint struct sequentially into the buffer after the set struct
            var arrayAddress = IntPtr.Add(infoBuffer.Address, setSize);
            foreach (var desired in desiredHints)
            {
                var hintInfo = new Sci_InlayHintInfo
                {
                    line = new IntPtr(desired.Line),
                    position = new IntPtr(desired.Position),
                    text = desired.TextAddress,
                    style = STYLE_INLAY_HINT,
                    paddingLeft = false,
                    paddingRight = false
                };

                if (infoBuffer.WriteStruct(hintInfo) == null)
                {
                    Debug.Log($"FunctionParameterNames: Warning - Buffer full writing hint at {desired.Line}:{desired.Position}");
                    return;
                }
            }

            // Now write the set struct at the beginning, pointing to the array
            var hintSet = new Sci_InlayHintSet
            {
                count = (nuint)desiredHints.Count,
                hints = arrayAddress
            };
            infoBuffer.WriteStruct(hintSet, offset: 0);

            // Send atomically - wParam points to Sci_InlayHintSet, lParam=1 means clear-and-replace
            Editor.SendMessage(SCI_SETINLAYINFO, setAddress, new IntPtr(1));

            Debug.Log($"FunctionParameterNames: Sent {desiredHints.Count} inlay hints atomically");
        }

    }
}
