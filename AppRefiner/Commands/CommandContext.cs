using AppRefiner.Refactors;
using AppRefiner.Services;
using AppRefiner.Snapshots;
using AppRefiner.Stylers;

namespace AppRefiner.Commands
{
    /// <summary>
    /// Context passed to commands containing references to AppRefiner's core services and managers.
    /// This allows commands to interact with the application without tight coupling.
    /// </summary>
    public readonly struct CommandContext
    {
        /// <summary>
        /// The currently active ScintillaEditor instance, or null if no editor is active.
        /// Provides access to the editor's content, AST, database connection, and more.
        /// </summary>
        public ScintillaEditor? ActiveEditor { get; init; }

        /// <summary>
        /// The LinterManager instance for running code analysis rules.
        /// </summary>
        public LinterManager? LinterManager { get; init; }

        /// <summary>
        /// The StylerManager instance for applying visual indicators to code.
        /// </summary>
        public StylerManager? StylerManager { get; init; }

        /// <summary>
        /// The AutoCompleteService instance for code completion functionality.
        /// </summary>
        public AutoCompleteService? AutoCompleteService { get; init; }

        /// <summary>
        /// The RefactorManager instance for executing code refactoring operations.
        /// </summary>
        public RefactorManager? RefactorManager { get; init; }

        /// <summary>
        /// The SettingsService instance for accessing and modifying application settings.
        /// </summary>
        public SettingsService? SettingsService { get; init; }

        /// <summary>
        /// The FunctionCacheManager instance for accessing cached PeopleCode function metadata.
        /// </summary>
        public FunctionCacheManager? FunctionCacheManager { get; init; }

        /// <summary>
        /// The current auto-suggest configuration settings.
        /// </summary>
        public AutoSuggestSettings AutoSuggestSettings { get; init; }

        /// <summary>
        /// The main form instance. Useful for Invoke() calls to run code on the UI thread.
        /// </summary>
        public MainForm? MainForm { get; init; }

        /// <summary>
        /// The main window handle of the active Application Designer process.
        /// Used for dialog parenting to ensure dialogs appear correctly.
        /// </summary>
        public IntPtr MainWindowHandle { get; init; }

        /// <summary>
        /// The active AppDesignerProcess instance, or null if no Application Designer process is active.
        /// Provides access to the Application Designer state and operations.
        /// </summary>
        public AppDesignerProcess? ActiveAppDesigner { get; init; }

        /// <summary>
        /// The SnapshotManager instance for managing code snapshots and restores.
        /// </summary>
        public SnapshotManager? SnapshotManager { get; init; }
    }
}
