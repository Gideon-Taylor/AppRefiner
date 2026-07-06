using AppRefiner.Dialogs;
using AppRefiner.Services;

namespace AppRefiner.Commands.BuiltIn
{
    public class BrowseMessageCatalogCommand : BaseCommand
    {
        public override string CommandName => "Browse Message Catalog";
        public override string CommandDescription => "Browse and search the PeopleSoft Message Catalog";
        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            var shortcuts = new[]
            {
                (ModifierKeys.Control | ModifierKeys.Alt, Keys.M),
                (ModifierKeys.Control | ModifierKeys.Shift, Keys.M),
            };

            foreach (var (modifiers, key) in shortcuts)
            {
                if (registrar.IsShortcutAvailable(modifiers, key)
                    && registrar.TryRegisterShortcut(commandId, modifiers, key, this))
                {
                    SetRegisteredShortcut(registrar.GetShortcutDisplayText(modifiers, key));
                    return;
                }
            }

            Debug.Log($"{CommandName}: could not register a shortcut");
        }

        public override void Execute(CommandContext context)
        {
            var editor = context.ActiveEditor;
            if (editor == null) return;

            var dataManager = editor.DataManager;
            var mainHandle = editor.AppDesignerProcess.MainWindowHandle;

            if (dataManager == null)
            {
                Task.Delay(100).ContinueWith(_ =>
                {
                    var handleWrapper = new WindowWrapper(mainHandle);
                    new MessageBoxDialog("Connect to a database to browse the Message Catalog.",
                        "Message Catalog", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                });
                return;
            }

            context.MainForm?.Invoke(() =>
            {
                using var dialog = new MessageCatalogDialog(dataManager, mainHandle);
                if (dialog.ShowDialog(new WindowWrapper(mainHandle)) == DialogResult.OK
                    && !string.IsNullOrEmpty(dialog.TextToInsert))
                {
                    ScintillaManager.InsertTextAtCursor(editor, dialog.TextToInsert);
                }
            });
        }
    }
}
