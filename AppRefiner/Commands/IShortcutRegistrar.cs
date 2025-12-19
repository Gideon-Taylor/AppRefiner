using AppRefiner.Services;

namespace AppRefiner.Commands
{
    /// <summary>
    /// Interface for registering and querying keyboard shortcuts.
    /// Implemented by ApplicationKeyboardService to provide plugin commands
    /// with the ability to register shortcuts and check availability.
    /// </summary>
    public interface IShortcutRegistrar
    {
        /// <summary>
        /// Check if a keyboard shortcut combination is available for registration.
        /// </summary>
        /// <param name="modifiers">The modifier keys (Ctrl, Alt, Shift)</param>
        /// <param name="key">The key to check</param>
        /// <returns>True if the shortcut is available, false if already registered</returns>
        bool IsShortcutAvailable(ModifierKeys modifiers, Keys key);

        /// <summary>
        /// Try to register a keyboard shortcut for a command.
        /// The command will be executed with a fresh CommandContext when the shortcut is pressed.
        /// </summary>
        /// <param name="commandId">Unique identifier for the command</param>
        /// <param name="modifiers">The modifier keys (Ctrl, Alt, Shift)</param>
        /// <param name="key">The key to register</param>
        /// <param name="command">The command instance to execute</param>
        /// <returns>True if registered successfully, false if combination already taken</returns>
        bool TryRegisterShortcut(string commandId, ModifierKeys modifiers, Keys key, BaseCommand command);

        /// <summary>
        /// Get a formatted string representation of a shortcut for display purposes.
        /// </summary>
        /// <param name="modifiers">The modifier keys</param>
        /// <param name="key">The key</param>
        /// <returns>A formatted string like "Ctrl+Alt+H"</returns>
        string GetShortcutDisplayText(ModifierKeys modifiers, Keys key);
    }
}
