using AppRefiner.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
// Remove direct dependency on System.Windows.Forms if possible
// using System.Windows.Forms; 

namespace AppRefiner.Templates
{
    /// <summary>
    /// Represents the definition of a UI control needed for a template input.
    /// </summary>
    public class TemplateParameterUIDefinition
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Type { get; set; } // "string", "boolean", "number" etc.
        public string DefaultValue { get; set; }
        public string Description { get; set; }
        public bool IsVisible { get; set; } = true; // Determined by display conditions
        public string CurrentValue { get; set; } = ""; // Current value held by the manager
        
        public TemplateParameterUIDefinition(TemplateInput input, string currentValue = "")
        {
            Id = input.Id;
            Label = input.Label;
            Type = input.Type;
            DefaultValue = input.DefaultValue ?? "";
            Description = input.Description ?? "";
            CurrentValue = currentValue; // Initialize with current value if provided
        }
    }
    
    /// <summary>
    /// Manages the loading, UI generation, and application of code templates.
    /// </summary>
    public class TemplateManager
    {
        // --- Fields (Moved from MainForm) ---

        // These will likely be refactored to manage state differently, 
        // rather than holding direct control references.
        private Dictionary<string, Control> templateInputControls = new();
        private Dictionary<string, Control> templateInputLabels = new();
        private Dictionary<string, DisplayCondition> templateInputsDisplayConditions = new();

        // --- Properties ---
        
        /// <summary>
        /// Gets the list of currently loaded templates.
        /// </summary>
        public List<Template> LoadedTemplates { get; private set; } = new List<Template>();

        /// <summary>
        /// Gets or sets the currently selected template for parameter generation or application.
        /// </summary>
        private Template? _activeTemplate;
        public Template? ActiveTemplate 
        { 
            get => _activeTemplate;
            set 
            {
                _activeTemplate = value;
                InitializeParameterValues(); // Reset values when template changes
            }
        }
        
        // Stores the current values provided for the ActiveTemplate's inputs
        private Dictionary<string, string> currentParameterValues = new Dictionary<string, string>();


        // --- Constructor ---
        public TemplateManager()
        {
            // Initialization logic, if any
        }

        // --- Methods (To be moved/implemented) ---

        /// <summary>
        /// Loads all available templates from the designated directory.
        /// </summary>
        public void LoadTemplates()
        {
            // Logic from MainForm.LoadTemplates will go here
            LoadedTemplates = Template.GetAvailableTemplates();
            // Reset active template and values if templates are reloaded
            ActiveTemplate = null; 
            currentParameterValues.Clear();
        }
        
        /// <summary>
        /// Initializes or resets the current parameter values based on the ActiveTemplate's inputs and defaults.
        /// </summary>
        private void InitializeParameterValues()
        {
            currentParameterValues.Clear();
            if (ActiveTemplate?.Inputs != null)
            {
                foreach (var input in ActiveTemplate.Inputs)
                {
                    currentParameterValues[input.Id] = input.DefaultValue ?? "";
                }
            }
        }

        /// <summary>
        /// Updates the stored value for a specific parameter ID.
        /// </summary>
        /// <param name="inputId">The ID of the input to update.</param>
        /// <param name="value">The new value.</param>
        public void UpdateParameterValue(string inputId, string value)
        {
            if (ActiveTemplate != null && ActiveTemplate.Inputs != null && ActiveTemplate.Inputs.Any(i => i.Id == inputId))
            {
                 currentParameterValues[inputId] = value;
            }
        }

        /// <summary>
        /// Gets the definitions for UI controls needed for the ActiveTemplate's parameters,
        /// considering current values and display conditions.
        /// </summary>
        /// <returns>A list of definitions for UI generation.</returns>
        public List<TemplateParameterUIDefinition> GetParameterDefinitionsForActiveTemplate()
        {
            var definitions = new List<TemplateParameterUIDefinition>();
            if (ActiveTemplate?.Inputs == null) return definitions;

            foreach (var input in ActiveTemplate.Inputs)
            {
                string currentValue = currentParameterValues.TryGetValue(input.Id, out var val) ? val : input.DefaultValue ?? "";
                 var definition = new TemplateParameterUIDefinition(input, currentValue);
                 
                 // Check display condition
                 if (input.DisplayCondition != null)
                 {
                     definition.IsVisible = Template.IsDisplayConditionMet(input.DisplayCondition, currentParameterValues);
                 }
                 else
                 {
                     definition.IsVisible = true;
                 }
                 
                 definitions.Add(definition);
            }
            return definitions;
        }

        /// <summary>
        /// Validates if all required inputs for the ActiveTemplate have non-empty values.
        /// Considers display conditions.
        /// </summary>
        /// <returns>True if validation passes, false otherwise.</returns>
        public bool ValidateInputs()
        {
             if (ActiveTemplate?.Inputs == null) return true;

            foreach (var input in ActiveTemplate.Inputs.Where(i => i.Required))
            {
                // Check if the input should be visible based on current values
                bool isVisible = true;
                if (input.DisplayCondition != null)
                {
                    isVisible = Template.IsDisplayConditionMet(input.DisplayCondition, currentParameterValues);
                }

                // Only validate if the input is required AND visible
                if (isVisible && (!currentParameterValues.TryGetValue(input.Id, out string? value) || string.IsNullOrWhiteSpace(value)))
                {
                     return false; // Required and visible input is missing or empty
                }
            }
            return true;
        }

        public void PromptForInputs(IntPtr mainHandle, WindowWrapper handleWrapper)
        {
            if (ActiveTemplate == null) return;
            if (ActiveTemplate.Inputs != null && ActiveTemplate.Inputs.Count > 0)
            {
                using var parameterDialog = new TemplateParameterDialog(ActiveTemplate, mainHandle);
                if (parameterDialog.ShowDialog(handleWrapper) != DialogResult.OK)
                {
                    return;
                }

                currentParameterValues = parameterDialog.ParameterValues;
            }
        }

        /// <summary>
        /// Applies the ActiveTemplate using the current parameter values to the specified editor.
        /// </summary>
        /// <param name="editor">The ScintillaEditor to apply the template to.</param>
        public void ApplyActiveTemplateToEditor(ScintillaEditor editor)
        {
            if (editor == null || ActiveTemplate == null) return;


            // Apply values to get the final content string
            string generatedContent = ActiveTemplate.Apply(currentParameterValues);

            // --- Apply to Editor ---
            int originalCursorPos = ScintillaManager.GetCursorPosition(editor);

            if (ActiveTemplate.IsInsertMode)
            {
                ScintillaManager.InsertTextAtCursor(editor, generatedContent);
                int finalCursorPos = originalCursorPos + ActiveTemplate.CursorPosition;
                int selectionStart = originalCursorPos + ActiveTemplate.SelectionStart;
                int selectionEnd = originalCursorPos + ActiveTemplate.SelectionEnd;

                if (ActiveTemplate.CursorPosition >= 0)
                {
                     ScintillaManager.SetCursorPosition(editor, finalCursorPos);
                     WindowHelper.FocusWindow(editor.hWnd);
                }
                else if (ActiveTemplate.SelectionStart >= 0 && ActiveTemplate.SelectionEnd >= 0)
                {
                     ScintillaManager.SetSelection(editor, selectionStart, selectionEnd);
                     WindowHelper.FocusWindow(editor.hWnd);
                }
            }
            else // Replace mode
            {
                ScintillaManager.SetScintillaText(editor, generatedContent);
                if (ActiveTemplate.CursorPosition >= 0)
                {
                     ScintillaManager.SetCursorPosition(editor, ActiveTemplate.CursorPosition);
                     WindowHelper.FocusWindow(editor.hWnd);
                }
                else if (ActiveTemplate.SelectionStart >= 0 && ActiveTemplate.SelectionEnd >= 0)
                {
                     ScintillaManager.SetSelection(editor, ActiveTemplate.SelectionStart, ActiveTemplate.SelectionEnd);
                     WindowHelper.FocusWindow(editor.hWnd);
                }
            }
        }
    }
} 