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
        int previousHintHash;
        int lastSyncedDocumentReset = -1;
        HashCode runningHash;
        /// <summary>
        /// Forgets the last-sent hint state so the next pass re-sends unconditionally.
        /// Call whenever the displayed hints are cleared outside this styler's knowledge
        /// (e.g. the param-names toggle clearing via SCI_INLAYHINTCLEARALL) — otherwise
        /// the skip-if-unchanged hash suppresses the re-send and the hints stay gone.
        /// </summary>
        public void InvalidateDisplayedHints()
        {
            previousHintHash = 0;
            lastSyncedDocumentReset = -1;
        }

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
            bool editorChanged = Editor != lastEditor;
            if (editorChanged)
            {
                paramNameAddresses.Clear();
                previousHintHash = 0;
                lastSyncedDocumentReset = -1;
                lastEditor = Editor;
            }

            // Clear per-run state (string buffer and addresses persist across runs)
            desiredHints.Clear();
            runningHash = new HashCode();

            // Configure inlay hint style on EVERY pass: App Designer re-initializes its
            // styling when it rewrites the editor on save (and dark-mode application does
            // SCI_STYLECLEARALL), either of which resets STYLE_INLAY_HINT to defaults.
            // Four SendMessages is cheap; caching this once per editor left hints
            // rendering unstyled after a save.
            Editor?.SendMessage(SCI_STYLESETFORE, STYLE_INLAY_HINT, 0x808080);  // Gray
            Editor?.SendMessage(SCI_STYLESETITALIC, STYLE_INLAY_HINT, 1);
            Editor?.SendMessage(SCI_STYLESETBOLD, STYLE_INLAY_HINT, 1);
            Editor?.SendMessage(SCI_STYLESETSIZE, STYLE_INLAY_HINT, 8);  // Smaller font to reduce horizontal space

            // Get or create string buffer (persists across runs — only new unique strings get written).
            // No MemoryManager.SyncRoot lock is held across this styler's buffer use: the
            // "parameterNames"/"inlayInfoSet" buffers are only ever touched by the StylerManager's
            // single serialized background consumer, so there is no concurrent access to guard.
            paramNameBuffer = Editor.AppDesignerProcess.MemoryManager.GetOrCreateBuffer("parameterNames");

            // On editor change the address cache was cleared, so nothing references the
            // previously written strings — rewind the write offset. Without this the buffer
            // fills monotonically across editor switches until hints silently stop.
            // (Assumes the enhanced Scintilla copies hint text during SCI_SETINLAYINFO rather
            // than retaining our buffer pointers for paint time — verify in the test pass.)
            if (editorChanged)
            {
                paramNameBuffer.Reset();
            }

            currentLineNumber = ScintillaManager.GetCurrentLineNumber(Editor);

            // Traverse AST to collect desired hints
            base.VisitProgram(node);

            // Skip synchronization only if the hint set is identical AND the document has
            // not been wholesale-replaced since the last send. The hash alone is not
            // enough: App Designer saves by replacing the entire editor text, which
            // destroys the displayed hints while the re-computed desired set stays
            // identical — the hash matched and the re-send was skipped forever. Normal
            // edits don't bump DocumentResetCount, so the skip still avoids redundant
            // sends (and any repaint they cause) while typing.
            var currentHash = runningHash.ToHashCode();
            var documentReset = Editor!.DocumentResetCount;
            if (currentHash == previousHintHash &&
                desiredHints.Count > 0 &&
                documentReset == lastSyncedDocumentReset)
            {
                return;
            }
            previousHintHash = currentHash;
            lastSyncedDocumentReset = documentReset;

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
