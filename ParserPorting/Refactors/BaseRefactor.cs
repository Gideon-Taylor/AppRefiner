using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted;
using AppRefiner.Services;
using AppRefiner;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace ParserPorting.Refactors
{
    /// <summary>
    /// Represents the result of a refactoring operation
    /// </summary>
    public class RefactorResult
    {
        /// <summary>
        /// Whether the refactoring was successful
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Optional message providing details about the result
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// Creates a new refactoring result
        /// </summary>
        /// <param name="success">Whether the refactoring was successful</param>
        /// <param name="message">Optional message providing details</param>
        public RefactorResult(bool success, string? message = null)
        {
            Success = success;
            Message = message;
        }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static RefactorResult Successful => new(true);

        /// <summary>
        /// Creates a failed result with the specified error message
        /// </summary>
        public static RefactorResult Failed(string message) => new(false, message);
    }

    /// <summary>
    /// Represents a text edit to be applied to the source code
    /// </summary>
    public class TextEdit
    {
        /// <summary>
        /// The starting index in the source where the edit begins
        /// </summary>
        public int StartIndex { get; }

        /// <summary>
        /// The ending index in the source where the edit ends
        /// </summary>
        public int EndIndex { get; }

        /// <summary>
        /// The new text to replace the range with (empty for deletion)
        /// </summary>
        public string NewText { get; }

        /// <summary>
        /// A description of what this edit does
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Creates a new text edit
        /// </summary>
        public TextEdit(int startIndex, int endIndex, string newText, string description)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
            NewText = newText ?? string.Empty;
            Description = description;
        }

        /// <summary>
        /// The net change in length (positive if new text is longer, negative if shorter)
        /// </summary>
        public int LengthDelta => NewText.Length - (EndIndex - StartIndex);

        /// <summary>
        /// Applies this edit directly to the Scintilla editor using API calls
        /// </summary>
        public bool ApplyToScintilla(ScintillaEditor editor)
        {
            return ScintillaManager.ReplaceTextRange(editor, StartIndex, EndIndex, NewText);
        }

        /// <summary>
        /// Updates cursor position based on this edit
        /// </summary>
        public int UpdateCursorPosition(int cursorPosition)
        {
            if (cursorPosition < StartIndex)
            {
                // Cursor is before edit, no change needed
                return cursorPosition;
            }
            else if (cursorPosition <= EndIndex)
            {
                // Cursor is within edited text, move to end of new text
                return StartIndex + NewText.Length;
            }
            else
            {
                // Cursor is after edit, adjust by the change in length
                return cursorPosition + LengthDelta;
            }
        }
    }

    /// <summary>
    /// Base class for implementing PeopleCode refactoring operations using the self-hosted parser
    /// </summary>
    public abstract class BaseRefactor : AstVisitorBase
    {
        /// <summary>
        /// Gets the display name for this refactor
        /// </summary>
        public static string RefactorName => "Base Refactor";

        /// <summary>
        /// Gets the description for this refactor
        /// </summary>
        public static string RefactorDescription => "Base refactoring operation";

        /// <summary>
        /// Gets whether this refactor requires a user input dialog
        /// </summary>
        public virtual bool RequiresUserInputDialog => false;

        /// <summary>
        /// Gets whether this refactor should defer showing the dialog until after the visitor has run
        /// </summary>
        public virtual bool DeferDialogUntilAfterVisitor => false;

        /// <summary>
        /// Gets whether this refactor should have a keyboard shortcut registered
        /// </summary>
        public static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// Gets whether this refactor should be hidden from refactor lists and discovery
        /// </summary>
        public static bool IsHidden => false;

        /// <summary>
        /// Gets the keyboard shortcut modifier keys for this refactor
        /// </summary>
        public static ModifierKeys ShortcutModifiers => ModifierKeys.Control;

        /// <summary>
        /// Gets the keyboard shortcut key for this refactor
        /// </summary>
        public static Keys ShortcutKey => Keys.None;

        /// <summary>
        /// Gets the type of a refactor that should be run immediately after this one completes successfully.
        /// Returns null if no follow-up refactor is needed.
        /// </summary>
        public virtual Type? FollowUpRefactorType => null;

        /// <summary>
        /// Gets whether this refactor should run even when the parser has syntax errors.
        /// Defaults to true for backward compatibility, but refactors that modify imports or 
        /// other structure-sensitive elements should set this to false.
        /// </summary>
        public virtual bool RunOnIncompleteParse => true;

        protected ScintillaEditor Editor { get; }
        protected int CurrentPosition { get; }
        protected int LineNumber { get; }
        protected int CurrentCursorPosition => CurrentPosition;

        private string? source;
        private int cursorPosition = -1;
        private bool failed;
        private string? failureMessage;
        private readonly List<TextEdit> edits = new();

        protected BaseRefactor(ScintillaEditor editor)
        {
            Editor = editor;
            CurrentPosition = ScintillaManager.GetCursorPosition(editor);
            LineNumber = ScintillaManager.GetCurrentLineNumber(editor);
        }

        /// <summary>
        /// Gets the main window handle for the editor
        /// </summary>
        protected IntPtr GetEditorMainWindowHandle()
        {
            return Process.GetProcessById((int)Editor.ProcessId).MainWindowHandle;
        }

        /// <summary>
        /// Shows the dialog for this refactor
        /// </summary>
        public virtual bool ShowRefactorDialog()
        {
            // Base implementation just returns true (no dialog needed)
            return true;
        }

        /// <summary>
        /// Initializes the refactor with source code and cursor position
        /// </summary>
        public virtual void Initialize(string sourceText, int cursorPosition = -1)
        {
            source = sourceText;
            edits.Clear();
            failed = false;
            failureMessage = null;
            this.cursorPosition = cursorPosition;
        }

        /// <summary>
        /// Sets a failure status with an error message
        /// </summary>
        protected void SetFailure(string message)
        {
            failed = true;
            failureMessage = message;
        }

        /// <summary>
        /// Gets the result of the refactoring operation
        /// </summary>
        public RefactorResult GetResult() => failed ? RefactorResult.Failed(failureMessage ?? "Unknown error") : RefactorResult.Successful;

        /// <summary>
        /// Gets the updated cursor position after refactoring
        /// </summary>
        public int GetUpdatedCursorPosition()
        {
            return cursorPosition;
        }

        /// <summary>
        /// Gets the list of edits that will be applied
        /// </summary>
        public IReadOnlyList<TextEdit> GetEdits() => edits.AsReadOnly();

        /// <summary>
        /// Adds a text edit using AST node positioning
        /// </summary>
        protected void ReplaceNode(AstNode node, string newText, string description)
        {
            if (node.SourceSpan.IsValid)
            {
                EditText(node.SourceSpan.Start.Index, node.SourceSpan.End.Index, newText, description);
            }
        }

        /// <summary>
        /// Adds a text edit with explicit start and end positions
        /// </summary>
        protected void EditText(int startIndex, int endIndex, string newText, string description)
        {
            edits.Add(new TextEdit(startIndex, endIndex, newText, description));
        }

        /// <summary>
        /// Inserts text at a specific position
        /// </summary>
        protected void InsertText(int position, string textToInsert, string description)
        {
            edits.Add(new TextEdit(position, position, textToInsert, description));
        }

        /// <summary>
        /// Inserts text at a SourcePosition
        /// </summary>
        protected void InsertText(SourcePosition position, string textToInsert, string description)
        {
            InsertText(position.Index, textToInsert, description);
        }

        /// <summary>
        /// Creates a SourcePosition at index 0
        /// </summary>
        protected static SourcePosition Zero => new SourcePosition(0, 1, 1);

        /// <summary>
        /// Inserts text after an AST node
        /// </summary>
        protected void InsertAfter(AstNode node, string textToInsert, string description)
        {
            if (node.SourceSpan.IsValid)
            {
                InsertText(node.SourceSpan.End.Index + 1, textToInsert, description);
            }
        }

        /// <summary>
        /// Inserts text before an AST node
        /// </summary>
        protected void InsertBefore(AstNode node, string textToInsert, string description)
        {
            if (node.SourceSpan.IsValid)
            {
                InsertText(node.SourceSpan.Start.Index, textToInsert, description);
            }
        }

        /// <summary>
        /// Deletes text at the specified range
        /// </summary>
        protected void DeleteText(int startIndex, int endIndex, string description)
        {
            edits.Add(new TextEdit(startIndex, endIndex, string.Empty, description));
        }

        /// <summary>
        /// Deletes an AST node
        /// </summary>
        protected void DeleteNode(AstNode node, string description)
        {
            if (node.SourceSpan.IsValid)
            {
                DeleteText(node.SourceSpan.Start.Index, node.SourceSpan.End.Index, description);
            }
        }

        /// <summary>
        /// Gets the original text for an AST node
        /// </summary>
        protected string? GetOriginalText(AstNode node)
        {
            if (source == null || !node.SourceSpan.IsValid)
                return null;

            var start = node.SourceSpan.Start.Index;
            var end = node.SourceSpan.End.Index;
            
            if (start < 0 || end >= source.Length || start > end)
                return null;

            return source.Substring(start, end - start + 1);
        }

        /// <summary>
        /// Applies all collected edits to the Scintilla editor
        /// </summary>
        public virtual void ApplyEdits()
        {
            // Sort edits in reverse order to avoid position shifting
            var sortedEdits = edits.OrderByDescending(e => e.StartIndex).ToList();
            
            foreach (var edit in sortedEdits)
            {
                edit.ApplyToScintilla(Editor);
            }
        }

        /// <summary>
        /// Gets all configurable properties for a specific refactor type
        /// </summary>
        public static List<PropertyInfo> GetConfigurableProperties(Type refactorType)
        {
            var properties = refactorType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && 
                          p.GetCustomAttribute<JsonIgnoreAttribute>() == null &&
                          IsConfigurableProperty(p))
                .ToList();

            return properties;
        }

        /// <summary>
        /// Determines if a property should be configurable
        /// </summary>
        private static bool IsConfigurableProperty(PropertyInfo property)
        {
            // Exclude specific properties that shouldn't be configurable
            var excludedProperties = new HashSet<string>
            {
                nameof(Editor),
                nameof(CurrentPosition),
                nameof(LineNumber),
                nameof(RequiresUserInputDialog),
                nameof(DeferDialogUntilAfterVisitor),
                nameof(FollowUpRefactorType),
                nameof(RunOnIncompleteParse)
            };

            return !excludedProperties.Contains(property.Name);
        }

        /// <summary>
        /// Gets the default configuration for a specific refactor type
        /// </summary>
        public static string GetDefaultRefactorConfig(Type refactorType)
        {
            var configProperties = GetConfigurableProperties(refactorType);
            var config = new Dictionary<string, object?>();

            // Create a temporary instance to get default values
            try
            {
                // BaseRefactor requires an editor parameter, but we just need default values
                var tempInstance = Activator.CreateInstance(refactorType, new object[] { null! });
                if (tempInstance != null)
                {
                    foreach (var property in configProperties)
                    {
                        config[property.Name] = property.GetValue(tempInstance);
                    }
                }
            }
            catch
            {
                // If we can't create an instance, just use default values for property types
                foreach (var property in configProperties)
                {
                    config[property.Name] = GetDefaultValueForType(property.PropertyType);
                }
            }

            return JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <summary>
        /// Gets a default value for a given type
        /// </summary>
        private static object? GetDefaultValueForType(Type type)
        {
            if (type == typeof(bool)) return false;
            if (type == typeof(int)) return 0;
            if (type == typeof(string)) return string.Empty;
            if (type.IsEnum) return Enum.GetValues(type).GetValue(0);
            return null;
        }

        /// <summary>
        /// Applies configuration to this refactor instance
        /// </summary>
        public virtual void ApplyRefactorConfig(string jsonConfig)
        {
            if (string.IsNullOrEmpty(jsonConfig)) return;

            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonConfig);
                if (config == null) return;

                var configProperties = GetConfigurableProperties(GetType());

                foreach (var property in configProperties)
                {
                    if (config.TryGetValue(property.Name, out var value))
                    {
                        try
                        {
                            var typedValue = JsonSerializer.Deserialize(value.GetRawText(), property.PropertyType);
                            property.SetValue(this, typedValue);
                        }
                        catch
                        {
                            // Skip properties that can't be deserialized
                        }
                    }
                }
            }
            catch
            {
                // If configuration fails, continue with defaults
            }
        }

        /// <summary>
        /// Gets the current configuration of this refactor instance as JSON
        /// </summary>
        public virtual string GetCurrentRefactorConfig()
        {
            var configProperties = GetConfigurableProperties(GetType());
            var config = new Dictionary<string, object?>();

            foreach (var property in configProperties)
            {
                config[property.Name] = property.GetValue(this);
            }

            return JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <summary>
        /// Helper method to get static string properties from a type using reflection
        /// </summary>
        public static string? GetStaticStringProperty(Type type, string propertyName)
        {
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            return prop?.GetValue(null) as string;
        }
    }
}