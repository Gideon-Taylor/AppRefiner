using AppRefiner;
using AppRefiner.Commands;
using AppRefiner.Services;
using System.Windows.Forms;

namespace PluginSample
{
    /// <summary>
    /// Simple example command that demonstrates basic plugin command functionality.
    /// This command shows a message box and attempts to register a single keyboard shortcut.
    /// </summary>
    public class SimpleExampleCommand : BaseCommand
    {
        public override string CommandName => "Plugin: Simple Hello World";

        public override string CommandDescription => "Simple plugin command that shows a greeting message";

        public override bool RequiresActiveEditor => false;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            // Try to register Ctrl+Alt+H
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Alt,
                Keys.H,
                () =>
                {
                    // Create empty context for shortcut execution
                    Execute(new CommandContext());
                }))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Alt, Keys.H));
                Debug.Log($"{CommandName}: Successfully registered shortcut Ctrl+Alt+H");
            }
            else
            {
                Debug.Log($"{CommandName}: Shortcut Ctrl+Alt+H already in use, no shortcut registered");
            }
        }

        public override void Execute(CommandContext context)
        {
            var editorInfo = context.ActiveEditor != null
                ? $"Active Editor: {context.ActiveEditor.Caption}"
                : "No active editor";

            MessageBox.Show(
                $"Hello from a plugin command!\n\n{editorInfo}",
                "Simple Plugin Command",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    /// <summary>
    /// Advanced example command that demonstrates fallback shortcuts and conditional enablement.
    /// This command shows how to:
    /// - Try multiple shortcut combinations with fallback options
    /// - Use dynamic enabled state based on database connection
    /// - Access services from the CommandContext
    /// </summary>
    public class AdvancedExampleCommand : BaseCommand
    {
        public override string CommandName => "Plugin: Advanced Database Info";

        public override string CommandDescription => "Advanced command that shows database information (requires DB connection)";

        public override bool RequiresActiveEditor => true;

        /// <summary>
        /// This command is only enabled when there's an active editor with a database connection
        /// </summary>
        public override Func<bool>? DynamicEnabledCheck => () =>
        {
            // This will be called when the command palette is opened
            // to determine if the command should be enabled
            // Note: We can't access context here, but we can access global state if needed
            return true; // For demo purposes, always enabled (real check happens in Execute)
        };

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            // Try multiple shortcut combinations with fallback
            var preferredShortcuts = new[]
            {
                (ModifierKeys.Control | ModifierKeys.Alt, Keys.D),           // Ctrl+Alt+D
                (ModifierKeys.Control | ModifierKeys.Shift, Keys.D),         // Ctrl+Shift+D
                (ModifierKeys.Alt | ModifierKeys.Shift, Keys.D),             // Alt+Shift+D
                (ModifierKeys.Control | ModifierKeys.Alt, Keys.I)            // Ctrl+Alt+I
            };

            foreach (var (modifiers, key) in preferredShortcuts)
            {
                if (registrar.IsShortcutAvailable(modifiers, key))
                {
                    if (registrar.TryRegisterShortcut(commandId, modifiers, key,
                        () => Execute(new CommandContext())))
                    {
                        SetRegisteredShortcut(registrar.GetShortcutDisplayText(modifiers, key));
                        Debug.Log($"{CommandName}: Registered shortcut {RegisteredShortcutText}");
                        return; // Successfully registered
                    }
                }
            }

            Debug.Log($"{CommandName}: Could not register any preferred shortcuts - all were taken");
        }

        public override void Execute(CommandContext context)
        {
            // Check if we have an active editor
            if (context.ActiveEditor == null)
            {
                MessageBox.Show(
                    "This command requires an active editor.",
                    "No Active Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Build information message
            var message = $"Advanced Plugin Command Example\n\n";
            message += $"Editor: {context.ActiveEditor.Caption}\n";
            message += $"Type: {context.ActiveEditor.Type}\n\n";

            // Show database connection info
            if (context.ActiveEditor.DataManager != null)
            {
                message += "Database: Connected\n";
                message += $"Connection Info: {context.ActiveEditor.DataManager.GetType().Name}\n\n";
            }
            else
            {
                message += "Database: Not connected\n\n";
            }

            // Show available services
            message += "Available Services:\n";
            message += $"- LinterManager: {(context.LinterManager != null ? "Available" : "Not available")}\n";
            message += $"- StylerManager: {(context.StylerManager != null ? "Available" : "Not available")}\n";
            message += $"- RefactorManager: {(context.RefactorManager != null ? "Available" : "Not available")}\n";
            message += $"- SettingsService: {(context.SettingsService != null ? "Available" : "Not available")}\n";
            message += $"- AutoCompleteService: {(context.AutoCompleteService != null ? "Available" : "Not available")}\n";
            message += $"- FunctionCacheManager: {(context.FunctionCacheManager != null ? "Available" : "Not available")}\n";

            MessageBox.Show(
                message,
                "Advanced Plugin Command",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    /// <summary>
    /// Example command that demonstrates interaction with AppRefiner services.
    /// This command triggers a lint operation on the active editor.
    /// </summary>
    public class TriggerLintCommand : BaseCommand
    {
        public override string CommandName => "Plugin: Trigger Linting";

        public override string CommandDescription => "Runs all active linters on the current editor";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            // Try to register Ctrl+Shift+L
            if (registrar.TryRegisterShortcut(commandId,
                ModifierKeys.Control | ModifierKeys.Shift,
                Keys.L,
                () => Execute(new CommandContext())))
            {
                SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                    ModifierKeys.Control | ModifierKeys.Shift, Keys.L));
            }
        }

        public override void Execute(CommandContext context)
        {
            if (context.ActiveEditor == null)
            {
                MessageBox.Show("No active editor", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (context.LinterManager == null)
            {
                MessageBox.Show("LinterManager not available", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Trigger linting using the LinterManager
            context.LinterManager.ProcessLintersForActiveEditor(context.ActiveEditor, context.ActiveEditor.DataManager);

            MessageBox.Show(
                $"Linting completed for {context.ActiveEditor.Caption}",
                "Lint Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
