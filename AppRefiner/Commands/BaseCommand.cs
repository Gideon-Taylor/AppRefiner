namespace AppRefiner.Commands
{
    /// <summary>
    /// Base class for commands that can be registered in the command palette.
    /// Commands can be built-in (in Commands/BuiltIn/) or provided by plugins.
    /// Commands can optionally register keyboard shortcuts and control their enabled state dynamically.
    /// </summary>
    public abstract class BaseCommand
    {
        /// <summary>
        /// The display name of the command as it appears in the command palette.
        /// </summary>
        public abstract string CommandName { get; }

        /// <summary>
        /// A description of what the command does, shown in the command palette.
        /// </summary>
        public abstract string CommandDescription { get; }

        /// <summary>
        /// Whether this command requires an active editor to be available.
        /// If true, the command will be disabled when no editor is active.
        /// Default is true.
        /// </summary>
        public virtual bool RequiresActiveEditor => true;

        /// <summary>
        /// Optional function to dynamically determine if the command should be enabled.
        /// This is called when the command palette is opened or when determining if a shortcut should execute.
        /// Return null to always enable, or provide a function that returns true when enabled.
        /// </summary>
        public virtual Func<bool>? DynamicEnabledCheck => null;

        /// <summary>
        /// The shortcut text that was successfully registered, or null if no shortcut is registered.
        /// This is set during initialization by the SetRegisteredShortcut method.
        /// </summary>
        protected string? RegisteredShortcutText { get; private set; }

        /// <summary>
        /// Called during initialization to allow the command to register keyboard shortcuts.
        /// Override this method to register shortcuts and query availability.
        /// Use the registrar to check if shortcuts are available and register them.
        /// </summary>
        /// <param name="registrar">The shortcut registration interface for checking availability and registering shortcuts</param>
        /// <param name="commandId">The unique identifier for this command instance</param>
        /// <example>
        /// <code>
        /// public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        /// {
        ///     // Try to register Ctrl+Alt+H
        ///     if (registrar.TryRegisterShortcut(commandId,
        ///         ModifierKeys.Control | ModifierKeys.Alt,
        ///         Keys.H,
        ///         () => ExecuteFromShortcut()))
        ///     {
        ///         SetRegisteredShortcut(registrar.GetShortcutDisplayText(
        ///             ModifierKeys.Control | ModifierKeys.Alt, Keys.H));
        ///     }
        ///     else
        ///     {
        ///         // Try alternate shortcut
        ///         if (registrar.TryRegisterShortcut(commandId,
        ///             ModifierKeys.Control | ModifierKeys.Shift,
        ///             Keys.H,
        ///             () => ExecuteFromShortcut()))
        ///         {
        ///             SetRegisteredShortcut(registrar.GetShortcutDisplayText(
        ///                 ModifierKeys.Control | ModifierKeys.Shift, Keys.H));
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        public virtual void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            // Base implementation does nothing - override to register shortcuts
        }

        /// <summary>
        /// Gets the display name for the command including shortcut if registered.
        /// This is used in the command palette to show both the name and keyboard shortcut.
        /// </summary>
        public string GetDisplayName()
        {
            return string.IsNullOrEmpty(RegisteredShortcutText)
                ? CommandName
                : $"{CommandName} ({RegisteredShortcutText})";
        }

        /// <summary>
        /// Execute the command with the provided context.
        /// This is the main entry point for command execution, called from both the command palette
        /// and keyboard shortcuts.
        /// </summary>
        /// <param name="context">The command context containing references to AppRefiner services and the active editor</param>
        public abstract void Execute(CommandContext context);

        /// <summary>
        /// Helper method for subclasses to store the registered shortcut text.
        /// Call this from InitializeShortcuts after successfully registering a shortcut.
        /// </summary>
        /// <param name="shortcutText">The formatted shortcut text (e.g., "Ctrl+Alt+H")</param>
        protected void SetRegisteredShortcut(string shortcutText)
        {
            RegisteredShortcutText = shortcutText;
        }
    }
}
