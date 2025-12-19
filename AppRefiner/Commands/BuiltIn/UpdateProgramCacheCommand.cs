using AppRefiner;
using AppRefiner.Commands;
using AppRefiner.Services;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Linq;
using System.Windows.Forms;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Plugin command that updates the function cache for the current Record Field PeopleCode.
    /// This is a faster alternative to full cache refresh that doesn't require database access.
    ///
    /// Record Field PeopleCode is the only program type where functions can be called from
    /// other programs, making it important to keep these functions cached for autocomplete
    /// and function signature lookups.
    ///
    /// How it works:
    /// 1. Verifies the current editor is a Record Field program (RECORD.FIELD.EVENT)
    /// 2. Removes all cached functions for that specific Record Field
    /// 3. Extracts function implementations from the current editor's AST
    /// 4. Adds the updated functions to the cache
    ///
    /// Benefits:
    /// - No database connection required
    /// - Fast updates as you edit Record Field code
    /// - keeps function cache in sync with your work
    /// - Only updates the specific Record Field, not the entire database
    /// </summary>
    public class UpdateProgramCacheCommand : BaseCommand
    {
        public override string CommandName => "Plugin: Update Function Cache for Record Field";

        public override string CommandDescription =>
            "Updates the function cache with functions from the current Record Field PeopleCode (no database access required)";

        public override bool RequiresActiveEditor => true;

        public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
        {
            // Try to register Ctrl+Alt+U for "Update cache"
            var preferredShortcuts = new[]
            {
                (ModifierKeys.Control | ModifierKeys.Alt, Keys.U),
                (ModifierKeys.Control | ModifierKeys.Shift, Keys.U),
                (ModifierKeys.Alt | ModifierKeys.Shift, Keys.U)
            };

            foreach (var (modifiers, key) in preferredShortcuts)
            {
                if (registrar.IsShortcutAvailable(modifiers, key))
                {
                    if (registrar.TryRegisterShortcut(commandId, modifiers, key,
                        this))
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
            // Validation
            if (context.ActiveEditor == null)
            {
                MessageBox.Show("No active editor.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (context.FunctionCacheManager == null)
            {
                MessageBox.Show("Function cache manager not available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var editor = context.ActiveEditor;
            var dbName = editor.AppDesignerProcess?.DBName;

            if (string.IsNullOrEmpty(dbName))
            {
                MessageBox.Show("Cannot determine database name from the current editor.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Get the program path
            var programPath = GetCurrentProgramPath(editor);
            if (string.IsNullOrEmpty(programPath))
            {
                MessageBox.Show(
                    "This command only works with Record Field PeopleCode programs.\n\n" +
                    "Record Field PeopleCode is the only program type where functions can be used across programs.",
                    "Not a Record Field Program",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Get parsed program
            var program = editor.GetParsedProgram(forceReparse: false);
            if (program == null)
            {
                MessageBox.Show("Cannot parse the current program. Check for syntax errors.", "Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Extract functions from the current program
            var functions = ExtractFunctionsFromProgram(program, dbName, programPath);

            // Update cache: Remove old + Add new
            int removedCount = context.FunctionCacheManager.RemoveFunctionsByPath(dbName, programPath);

            int addedCount = 0;
            foreach (var function in functions)
            {
                if (context.FunctionCacheManager.AddFunction(function))
                {
                    addedCount++;
                }
            }

            // Show success message
            var message = $"Function cache updated for Record Field:\n{programPath}\n\n" +
                         $"Removed: {removedCount} function(s)\n" +
                         $"Added: {addedCount} function(s)";

            if (addedCount == 0 && removedCount == 0)
            {
                message += "\n\nNote: No functions found. Only function implementations " +
                          "(not declarations) are cached.";
            }

            MessageBox.Show(message, "Cache Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Debug.Log($"Updated function cache for {programPath}: Removed {removedCount}, Added {addedCount}");
        }

        /// <summary>
        /// Gets the current program's path identifier for Record Field PeopleCode.
        /// This must match the format used by PeopleCodeItem.BuildPath() which populates the cache.
        ///
        /// For Record Field programs, the format is: "RECORD.FIELD.EVENT" (dot-separated)
        /// </summary>
        private string? GetCurrentProgramPath(ScintillaEditor editor)
        {
            if (editor.Caption.Contains("Record PeopleCode"))
            {
                var parts = editor.Caption.Split('.');
                return $"{parts[0]}.{parts[1]}.{parts[2]}";
            }

            // Not a Record Field program
            return null;
        }

        /// <summary>
        /// Extracts function definitions from the Record Field PeopleCode AST.
        /// Only functions with IsImplementation=true are cached (not declarations).
        ///
        /// These functions can be called from other PeopleCode programs, which is why
        /// they need to be cached for autocomplete and signature lookups.
        /// </summary>
        private List<FunctionCacheItem> ExtractFunctionsFromProgram(ProgramNode program, string dbName, string programPath)
        {
            var functions = new List<FunctionCacheItem>();

            // Extract functions (only implementations, not declarations)
            // Function declarations are just forward references and don't go in the cache
            foreach (var function in program.Functions.Where(f => f.IsImplementation))
            {
                var newFunc = new FunctionCacheItem
                {
                    DBName = dbName,
                    FunctionName = function.Name,
                    FunctionPath = programPath, // RECORD.FIELD.EVENT format
                    ReturnType = function.ReturnType?.TypeName ?? "",
                    ParameterNames = [.. function.Parameters.Select(p => p.Name)],
                    ParameterTypes = [.. function.Parameters.Select(p => p.Type.TypeName)],
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                functions.Add(newFunc);
            }

            return functions;
        }
    }
}
