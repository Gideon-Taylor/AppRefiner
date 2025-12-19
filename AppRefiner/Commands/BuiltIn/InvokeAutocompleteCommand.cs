using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static AppRefiner.ScintillaEditor;

namespace AppRefiner.Commands.BuiltIn
{
    public class InvokeAutocompleteCommand : BaseCommand
    {
        // Message constants (must match MainForm.cs)
        private const int AR_APP_PACKAGE_SUGGEST = 2500;
        private const int AR_VARIABLE_SUGGEST = 2509;
        private const int AR_FUNCTION_CALL_TIP = 2511;
        private const int AR_OBJECT_MEMBERS = 2512;
        private const int AR_SYSTEM_VARIABLE_SUGGEST = 2513;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public override string CommandName => "Editor: Autocomplete";
        public override string CommandDescription => "Manually trigger autocomplete at cursor position";
        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            // Register Ctrl+Space
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control,
                Keys.Space,
                this))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control, Keys.Space));
            }
        }

        public override void Execute(CommandContext context)
        {
            var editor = context.ActiveEditor;
            var mainForm = context.MainForm;
            var appDesigner = context.ActiveAppDesigner;
            if (editor == null || mainForm == null || appDesigner == null) return;

            int position = ScintillaManager.GetCursorPosition(editor);
            if (position <= 0) return;

            // Get document text
            string? text = ScintillaManager.GetScintillaText(editor);
            if (string.IsNullOrEmpty(text)) return;

            // Detect context by scanning backward
            var detectedContext = DetectAutocompleteContext(text, position);

            int messageId;
            IntPtr lParam = IntPtr.Zero;

            if (!detectedContext.HasValue)
            {
                // No specific trigger found - show variables as default
                messageId = AR_VARIABLE_SUGGEST;
            }
            else
            {
                var (contextType, triggerPos, triggerChar) = detectedContext.Value;

                // Map context to message ID
                messageId = contextType switch
                {
                    AutoCompleteContext.AppPackage => AR_APP_PACKAGE_SUGGEST,
                    AutoCompleteContext.Variable => AR_VARIABLE_SUGGEST,
                    AutoCompleteContext.ObjectMembers => AR_OBJECT_MEMBERS,
                    AutoCompleteContext.SystemVariables => AR_SYSTEM_VARIABLE_SUGGEST,
                    AutoCompleteContext.FunctionCallTip => AR_FUNCTION_CALL_TIP,
                    _ => AR_VARIABLE_SUGGEST
                };

                // For function call tips, pass the trigger character in LParam
                if (contextType == AutoCompleteContext.FunctionCallTip)
                {
                    lParam = new IntPtr(triggerChar);
                }
            }

            // Send message to MainForm's WndProc with position in WParam
            // Let existing message handlers take care of AST parsing and type inference
            mainForm.ProcessMessage(new Message
            {
                HWnd = context.ActiveAppDesigner!.MainWindowHandle,
                Msg = messageId,
                WParam = new IntPtr(position),
                LParam = lParam
            });
            //SendMessage(context.ActiveAppDesigner!.MainWindowHandle, messageId, new IntPtr(position), IntPtr.Zero);
        }

        /// <summary>
        /// Scan backward from position to detect autocomplete context
        /// </summary>
        private (AutoCompleteContext context, int triggerPosition, char triggerChar)? DetectAutocompleteContext(
            string text, int position)
        {
            // Scan backward from cursor
            for (int i = position - 1; i >= 0; i--)
            {
                char ch = text[i];

                // Continue through identifier characters
                if (char.IsLetterOrDigit(ch) || ch == '_')
                    continue;

                // Found trigger character
                if (ch == '%') return (AutoCompleteContext.SystemVariables, i, ch);
                if (ch == '&') return (AutoCompleteContext.Variable, i, ch);
                if (ch == '.') return (AutoCompleteContext.ObjectMembers, i, ch);
                if (ch == ':') return (AutoCompleteContext.AppPackage, i, ch);
                if (ch == '(' || ch == ',') return (AutoCompleteContext.FunctionCallTip, i, ch);

                // Hit non-identifier, non-trigger character
                return null;
            }

            return null; // Reached start of document
        }
    }
}
