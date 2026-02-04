using AppRefiner.Plugins;

namespace AppRefiner.Commands
{
    /// <summary>
    /// Manages discovery, initialization, and execution of commands.
    /// Commands can be built-in (in Commands/BuiltIn/) or provided by plugins.
    /// </summary>
    public class CommandManager
    {
        private readonly List<(Type CommandType, BaseCommand Instance, string CommandId)> _discoveredCommands = [];

        /// <summary>
        /// Discovers and instantiates all commands from the main assembly and loaded plugins.
        /// This should be called during application initialization, after plugins have been loaded.
        /// </summary>
        public void DiscoverAndCacheCommands()
        {
            _discoveredCommands.Clear();

            // Discover from main assembly
            var coreTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(BaseCommand).IsAssignableFrom(p) && !p.IsAbstract);

            // Discover from plugins
            var pluginTypes = PluginManager.DiscoverCommandTypes();

            var allTypes = coreTypes.Concat(pluginTypes);

            foreach (var type in allTypes)
            {
                try
                {
                    // Instantiate the command
                    var instance = (BaseCommand)Activator.CreateInstance(type)!;

                    // Generate a unique command ID
                    string commandId = $"Command_{type.FullName}";

                    _discoveredCommands.Add((type, instance, commandId));

                    Debug.Log($"Discovered command: {instance.CommandName} ({type.FullName})");
                }
                catch (Exception ex)
                {
                    Debug.Log($"Failed to instantiate command {type.FullName}: {ex.Message}");
                }
            }

            Debug.Log($"Total commands discovered: {_discoveredCommands.Count}");
        }

        /// <summary>
        /// Initializes keyboard shortcuts for all discovered commands.
        /// This should be called after DiscoverAndCacheCommands and after the ApplicationKeyboardService is created.
        /// Commands can query shortcut availability and register their preferred shortcuts with fallback options.
        /// </summary>
        /// <param name="registrar">The shortcut registration interface (typically ApplicationKeyboardService)</param>
        public void InitializeCommandShortcuts(IShortcutRegistrar registrar)
        {
            foreach (var (commandType, instance, commandId) in _discoveredCommands)
            {
                try
                {
                    instance.InitializeShortcuts(registrar, commandId);

                    // Log successful registration
                    var shortcutInfo = instance.GetDisplayName();
                    if (shortcutInfo.Contains('('))
                    {
                        Debug.Log($"Registered shortcut for {instance.CommandName}: {shortcutInfo}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log($"Failed to initialize shortcuts for {commandId}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets all discovered commands with their command IDs.
        /// </summary>
        /// <returns>Enumerable of tuples containing command ID and command instance</returns>
        public IEnumerable<(string CommandId, BaseCommand Instance)> GetCommands()
        {
            return _discoveredCommands.Select(cmd => (cmd.CommandId, cmd.Instance));
        }

        public (string CommandId, BaseCommand Instance) GetCommandByType(Type commandType)
        {
            var cmd = _discoveredCommands.FirstOrDefault(c => c.CommandType == commandType);
            return (cmd.CommandId, cmd.Instance);
        }

        /// <summary>
        /// Executes a command with proper error handling.
        /// </summary>
        /// <param name="command">The command to execute</param>
        /// <param name="context">The command context with application services</param>
        public void ExecuteCommand(BaseCommand command, CommandContext context)
        {
            try
            {
                // Check if command requires active editor
                if (command.RequiresActiveEditor && context.ActiveEditor == null)
                {
                    Debug.Log($"Command {command.CommandName} requires an active editor but none is available");
                    return;
                }

                // Check dynamic enabled state
                if (command.DynamicEnabledCheck != null && !command.DynamicEnabledCheck())
                {
                    Debug.Log($"Command {command.CommandName} is currently disabled");
                    return;
                }

                Debug.Log($"Executing command: {command.CommandName}");
                command.Execute(context);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error executing command {command.CommandName}: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"Error executing command '{command.CommandName}':\n\n{ex.Message}",
                    "Command Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Gets the total number of discovered commands.
        /// </summary>
        public int CommandCount => _discoveredCommands.Count;
    }
}
