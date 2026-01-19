using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
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

        int SCI_SETINLAYHINT = 2900;
        int SCI_GETINLAYHINT = 2901;
        int SCI_INLAYHINTREMOVE = 2902;
        int SCI_INLAYHINTCLEARLINE = 2903;
        int SCI_INLAYHINTCLEARALL = 2904;
        int SCI_GETINLAYINFO = 2905;
        int SCI_INLAYHINTSSUPPORTED = 2906;
        int SCI_STYLESETFORE = 2051;
        int SCI_STYLESETBACK = 2052;
        int SCI_STYLESETBOLD = 2053;
        int SCI_STYLESETITALIC = 2054;
        int SCI_STYLESETSIZE = 2055;
        const int STYLE_INLAY_HINT = 40;

        [StructLayout(LayoutKind.Sequential)]
        private struct Sci_InlayInfo
        {
            public int handle;
            public IntPtr line;
            public IntPtr position;
            public int style;
            public IntPtr text;
            public bool paddingLeft;
            public bool paddingRight;
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
        Dictionary<int, string> handleToText = new();  // Maps hint handle -> text for change detection
        RemoteBuffer paramNameBuffer;
        int currentLineNumber = -1;
        List<DesiredInlayHint> desiredHints = new();
        public override void VisitProgram(ProgramNode node)
        {
            // Check if inlay hints are supported by this version of Scintilla
            var inlayHintsSupported = Editor?.SendMessage(SCI_INLAYHINTSSUPPORTED, 0, 0) ?? IntPtr.Zero;
            if (inlayHintsSupported == IntPtr.Zero || inlayHintsSupported.ToInt32() != 1)
            {
                // Inlay hints not supported - skip this styler
                return;
            }

            // Clear state for this run
            desiredHints.Clear();
            paramNameAddresses.Clear();

            // Configure inlay hint style
            Editor?.SendMessage(SCI_STYLESETFORE, STYLE_INLAY_HINT, 0x808080);  // Gray
            Editor?.SendMessage(SCI_STYLESETITALIC, STYLE_INLAY_HINT, 1);
            Editor?.SendMessage(SCI_STYLESETBOLD, STYLE_INLAY_HINT, 1);
            Editor?.SendMessage(SCI_STYLESETSIZE, STYLE_INLAY_HINT, 8);  // Smaller font to reduce horizontal space

            // Get or create buffer and reset for this run
            // Scintilla copies strings internally, so we don't need to maintain addresses
            paramNameBuffer = Editor.AppDesignerProcess.MemoryManager.GetOrCreateBuffer("parameterNames");
            paramNameBuffer.Reset();

            currentLineNumber = ScintillaManager.GetCurrentLineNumber(Editor);

            // Traverse AST to collect desired hints
            base.VisitProgram(node);

            // Synchronize desired hints with current Scintilla state
            SynchronizeInlayHints();
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            base.VisitFunctionCall(node);

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
                // No type resolver available - collect generic names
                CollectGenericParameterNames(node);
                return;
            }

            // Validate and get parameter mappings
            var validator = new FunctionCallValidator(typeResolver);
            var result = validator.Validate(funcInfo, arguments.ToArray());

            if (result.IsValid && result.ArgumentMappings != null)
            {
                // Collect actual parameter names from mappings
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

                    // Add to collection instead of immediately displaying
                    AddDesiredHint(arg.SourceSpan.Start.Line, arg.SourceSpan.Start.Column - 1, paramText);
                }
            }
            else
            {
                // Validation failed - collect generic names
                CollectGenericParameterNames(node);
            }
        }

        private void CollectGenericParameterNames(FunctionCallNode node)
        {
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                var arg = node.Arguments[i];
                var paramText = $"arg{i}:";

                // Add to collection instead of immediately displaying
                AddDesiredHint(arg.SourceSpan.Start.Line, arg.SourceSpan.Start.Column - 1, paramText);
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

            // Add to desired hints collection
            desiredHints.Add(new DesiredInlayHint
            {
                Line = line,
                Position = position,
                Text = paramText,
                TextAddress = paramNameAddr
            });
        }

        /// <summary>
        /// Queries Scintilla for all current inlay hints using two-phase SCI_GETINLAYINFO call.
        /// Phase 1: Query with null buffer to get required size
        /// Phase 2: Allocate buffer and query again to get actual data
        /// </summary>
        /// <returns>List of current inlay hints, or empty list if none exist</returns>
        private List<Sci_InlayInfo> GetCurrentInlayHints()
        {
            if (Editor == null)
                return new List<Sci_InlayInfo>();

            // Phase 1: Get required buffer size (in bytes)
            var requiredSize = Editor.SendMessage(SCI_GETINLAYINFO, 0, IntPtr.Zero);
            if (requiredSize == IntPtr.Zero || requiredSize.ToInt32() <= 0)
            {
                // No inlay hints exist
                return new List<Sci_InlayInfo>();
            }

            // Phase 2: Allocate buffer and get data
            int structSize = Marshal.SizeOf<Sci_InlayInfo>();
            int count = requiredSize.ToInt32() / structSize;

            // Allocate RemoteBuffer for receiving the array
            var infoBuffer = Editor.AppDesignerProcess.MemoryManager.GetOrCreateBuffer("inlayInfoQuery", (uint)requiredSize.ToInt32());
            infoBuffer.Reset(); // Ensure we start at offset 0

            // Call SCI_GETINLAYINFO with buffer address
            var bytesWritten = Editor.SendMessage(SCI_GETINLAYINFO, infoBuffer.Address, requiredSize);

            if (bytesWritten.ToInt32() != requiredSize.ToInt32())
            {
                Debug.Log($"FunctionParameterNames: Warning - SCI_GETINLAYINFO returned {bytesWritten} bytes, expected {requiredSize}");
                return new List<Sci_InlayInfo>();
            }

            // Read the array from remote buffer
            byte[] data = infoBuffer.Read(requiredSize.ToInt32());

            // Marshal byte array to struct array
            List<Sci_InlayInfo> results = new List<Sci_InlayInfo>(count);
            for (int i = 0; i < count; i++)
            {
                int offset = i * structSize;
                IntPtr structPtr = Marshal.AllocHGlobal(structSize);
                try
                {
                    Marshal.Copy(data, offset, structPtr, structSize);
                    var info = Marshal.PtrToStructure<Sci_InlayInfo>(structPtr);
                    results.Add(info);
                }
                finally
                {
                    Marshal.FreeHGlobal(structPtr);
                }
            }

            Debug.Log($"FunctionParameterNames: Retrieved {results.Count} current inlay hints from Scintilla");
            return results;
        }

        /// <summary>
        /// Synchronizes desired inlay hints with current Scintilla state.
        /// Uses handle -> text mapping to detect when hint text has changed.
        /// Uses the new unified SetInlayHint API (single call instead of add/set text/set style).
        /// </summary>
        private void SynchronizeInlayHints()
        {
            if (Editor == null)
                return;

            var currentHints = GetCurrentInlayHints();

            Debug.Log($"FunctionParameterNames: Synchronizing - Desired: {desiredHints.Count}, Current: {currentHints.Count}");

            // Build lookup dictionaries for efficient comparison
            // Key: "line:position" -> hint data
            var currentByLocation = new Dictionary<string, Sci_InlayInfo>();
            foreach (var hint in currentHints)
            {
                string key = $"{hint.line.ToInt32()}:{hint.position.ToInt32()}";
                currentByLocation[key] = hint;
            }

            var desiredByLocation = new Dictionary<string, DesiredInlayHint>();
            foreach (var hint in desiredHints)
            {
                string key = hint.GetLocationKey();
                desiredByLocation[key] = hint;
            }

            // Track statistics for logging
            int addedCount = 0;
            int updatedCount = 0;
            int removedCount = 0;
            int unchangedCount = 0;

            // Get or create buffer for InlayInfo struct (reuse for all hints)
            var infoBuffer = Editor.AppDesignerProcess.MemoryManager.GetOrCreateBuffer("inlayInfoSet", (uint)Marshal.SizeOf<Sci_InlayInfo>());

            // Process desired hints: add new, update changed, or skip unchanged
            foreach (var desired in desiredHints)
            {
                string locationKey = desired.GetLocationKey();

                if (currentByLocation.TryGetValue(locationKey, out var existing))
                {
                    // Hint exists at this location - check if text changed
                    if (handleToText.TryGetValue(existing.handle, out string storedText))
                    {
                        if (storedText == desired.Text)
                        {
                            // Text hasn't changed - skip
                            unchangedCount++;
                            continue;
                        }
                    }

                    // Text changed or not tracked - update the existing hint
                    var updateInfo = new Sci_InlayInfo
                    {
                        handle = existing.handle,
                        line = new IntPtr(desired.Line),
                        position = new IntPtr(desired.Position),
                        style = STYLE_INLAY_HINT,
                        text = desired.TextAddress,
                        paddingLeft = false,
                        paddingRight = false
                    };

                    infoBuffer.Reset();
                    if (infoBuffer.WriteStruct(updateInfo) == null)
                    {
                        Debug.Log($"FunctionParameterNames: Warning - Failed to write struct to buffer for update");
                        continue;
                    }

                    var result = Editor.SendMessage(SCI_SETINLAYHINT, infoBuffer.Address, IntPtr.Zero);

                    if (result.ToInt32() == -1)
                    {
                        Debug.Log($"FunctionParameterNames: Warning - Failed to update hint with handle {existing.handle}");
                    }
                    else
                    {
                        handleToText[existing.handle] = desired.Text;
                        updatedCount++;
                    }
                }
                else
                {
                    // New hint - create it (handle=0)
                    var createInfo = new Sci_InlayInfo
                    {
                        handle = 0,
                        line = new IntPtr(desired.Line),
                        position = new IntPtr(desired.Position),
                        style = STYLE_INLAY_HINT,
                        text = desired.TextAddress
                    };

                    infoBuffer.Reset();
                    if (infoBuffer.WriteStruct(createInfo) == null)
                    {
                        Debug.Log($"FunctionParameterNames: Warning - Failed to write struct to buffer for create");
                        continue;
                    }

                    var handle = Editor.SendMessage(SCI_SETINLAYHINT, infoBuffer.Address, IntPtr.Zero);

                    if (handle.ToInt32() > 0)
                    {
                        handleToText[handle.ToInt32()] = desired.Text;
                        addedCount++;
                    }
                    else
                    {
                        Debug.Log($"FunctionParameterNames: Warning - Failed to create hint at {locationKey}");
                    }
                }
            }

            // Remove hints that are no longer desired
            foreach (var current in currentHints)
            {
                string locationKey = $"{current.line.ToInt32()}:{current.position.ToInt32()}";

                if (!desiredByLocation.ContainsKey(locationKey))
                {
                    // This hint is no longer desired - remove it
                    Editor.SendMessage(SCI_INLAYHINTREMOVE, current.handle, 0);
                    handleToText.Remove(current.handle);
                    removedCount++;
                }
            }

            Debug.Log($"FunctionParameterNames: Sync complete - Added: {addedCount}, Updated: {updatedCount}, Removed: {removedCount}, Unchanged: {unchangedCount}");
        }

    }
}
