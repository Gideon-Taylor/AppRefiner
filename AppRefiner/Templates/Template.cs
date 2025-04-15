using System.Text.Json;
using System.Text.RegularExpressions;

namespace AppRefiner.Templates
{
    /// <summary>
    /// Represents a condition that determines whether an input field should be displayed
    /// </summary>
    public class DisplayCondition
    {
        /// <summary>
        /// The ID of the field this condition depends on
        /// </summary>
        public required string Field { get; set; }

        /// <summary>
        /// The comparison operator (equals, notEquals, etc.)
        /// </summary>
        public required string Operator { get; set; }

        /// <summary>
        /// The value to compare against
        /// </summary>
        public required object Value { get; set; }
    }

    /// <summary>
    /// Represents an input field in a template
    /// </summary>
    public class TemplateInput
    {
        /// <summary>
        /// Identifier used in template markers (e.g., {{id}})
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// User-friendly label for the input field
        /// </summary>
        public required string Label { get; set; }

        /// <summary>
        /// Data type of the input (string, number, boolean, etc.)
        /// </summary>
        public required string Type { get; set; }

        /// <summary>
        /// Whether this input is required
        /// </summary>
        public required bool Required { get; set; }

        /// <summary>
        /// Default value for this input
        /// </summary>
        public required string DefaultValue { get; set; }

        /// <summary>
        /// Additional description or help text
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// Display condition that determines whether this input should be shown
        /// </summary>
        public DisplayCondition? DisplayCondition { get; set; }
    }

    /// <summary>
    /// Represents a text template with replaceable markers
    /// </summary>
    public class Template
    {
        /// <summary>
        /// Display name of the template
        /// </summary>
        public required string TemplateName { get; set; }

        /// <summary>
        /// Description of what the template does
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// Collection of input fields required by the template
        /// </summary>
        public List<TemplateInput>? Inputs { get; set; }

        /// <summary>
        /// The template text with {{marker}} placeholders
        /// </summary>
        public string? TemplateText { get; set; }

        /// <summary>
        /// Position relative to the start of the inserted text to place the cursor after applying the template (-1 if not specified)
        /// </summary>
        public int CursorPosition { get; private set; } = -1;

        /// <summary>
        /// Starting position of the selection range relative to the start of the inserted text (-1 if not specified)
        /// </summary>
        public int SelectionStart { get; private set; } = -1;

        /// <summary>
        /// Ending position of the selection range relative to the start of the inserted text (-1 if not specified)
        /// </summary>
        public int SelectionEnd { get; private set; } = -1;

        /// <summary>
        /// Determines if the template should insert at the cursor or replace the entire content.
        /// Defaults to false (replace).
        /// </summary>
        public bool IsInsertMode { get; set; } = false;

        public override string ToString()
        {
            return TemplateName;
        }

        /// <summary>
        /// Loads a template from a file
        /// </summary>
        /// <param name="filePath">Path to the template file (.template or .json)</param>
        /// <returns>A populated Template object</returns>
        public static Template LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Template file not found: {filePath}");

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".template")
            {
                // Parse combined template file format
                string fileContent = File.ReadAllText(filePath);
                return LoadFromCombinedFormat(fileContent);
            }
            else if (extension == ".json")
            {
                // Handle legacy JSON format
                string json = File.ReadAllText(filePath);
                return LoadFromJson(json, filePath);
            }
            else
            {
                throw new FormatException($"Unsupported template file format: {extension}");
            }
        }

        /// <summary>
        /// Loads a template from a combined format (JSON + template text)
        /// </summary>
        /// <param name="content">The full content of a .template file</param>
        /// <returns>A populated Template object</returns>
        public static Template LoadFromCombinedFormat(string content)
        {
            try
            {
                // Split the content at the delimiter
                const string delimiter = "---";
                int delimiterIndex = content.IndexOf(delimiter);

                if (delimiterIndex < 0)
                    throw new FormatException("Template file is missing the '---' delimiter between JSON and template text");

                string json = content[..delimiterIndex].Trim();
                string templateText = content[(delimiterIndex + delimiter.Length)..].Trim();

                // Load the JSON part
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(json, options);

                // Create template with properties from JSON
                var template = new Template
                {
                    TemplateName = jsonDoc.GetProperty("templateName").GetString()!,
                    Description = jsonDoc.GetProperty("description").GetString()!,
                    Inputs = new List<TemplateInput>(),
                    TemplateText = templateText,
                    IsInsertMode = jsonDoc.TryGetProperty("isInsertMode", out var insertModeProp) && insertModeProp.GetBoolean()
                };

                var inputs = jsonDoc.GetProperty("inputs");
                foreach (var input in inputs.EnumerateArray())
                {
                    var templateInput = new TemplateInput
                    {
                        Id = input.GetProperty("id").GetString()!,
                        Label = input.GetProperty("label").GetString()!,
                        Type = input.GetProperty("type").GetString()!,
                        Required = input.GetProperty("required").GetBoolean(),
                        DefaultValue = input.TryGetProperty("defaultValue", out var defaultValue) ?
                            defaultValue.GetString()! : "",
                        Description = input.TryGetProperty("description", out var description) ?
                            description.GetString()! : ""
                    };

                    // Parse display condition if present
                    if (input.TryGetProperty("displayCondition", out var displayConditionProp))
                    {
                        templateInput.DisplayCondition = new DisplayCondition
                        {
                            Field = displayConditionProp.GetProperty("field").GetString()!,
                            Operator = displayConditionProp.GetProperty("operator").GetString()!,
                            Value = GetValueFromJsonElement(displayConditionProp.GetProperty("value"))!
                        };
                    }

                    template.Inputs.Add(templateInput);
                }

                return template;
            }
            catch (Exception ex)
            {
                throw new FormatException($"Error parsing template file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets all simple markers in the template (excluding conditional markers)
        /// </summary>
        /// <returns>A list of unique marker names without braces</returns>
        public List<string> GetMarkers()
        {
            if (TemplateText == null) return new List<string>();

            var markers = new HashSet<string>();
            // Match {{name}} but not {{#if name}} or {{/if}}
            var regex = new Regex(@"{{(?!\\#if\\s)(?!\\/if)([^{}]+)}}");
            var matches = regex.Matches(TemplateText);

            foreach (Match match in matches)
            {
                markers.Add(match.Groups[1].Value);
            }

            return markers.ToList();
        }

        /// <summary>
        /// Determines whether a display condition is met based on the provided values
        /// </summary>
        public static bool IsDisplayConditionMet(DisplayCondition condition, Dictionary<string, string> values)
        {
            if (condition == null || string.IsNullOrEmpty(condition.Field))
                return true;

            if (!values.TryGetValue(condition.Field, out string? fieldValue))
                return false;

            switch (condition.Operator.ToLower())
            {
                case "equals":
                    if (condition.Value is bool boolValue)
                        return fieldValue.Equals("true", StringComparison.CurrentCultureIgnoreCase) == boolValue;
                    return fieldValue.Equals(condition.Value?.ToString(), StringComparison.OrdinalIgnoreCase);

                case "notequals":
                    if (condition.Value is bool boolNotValue)
                        return fieldValue.Equals("true", StringComparison.CurrentCultureIgnoreCase) != boolNotValue;
                    return !fieldValue.Equals(condition.Value?.ToString(), StringComparison.OrdinalIgnoreCase);

                // Add more operators as needed

                default:
                    return true;
            }
        }

        /// <summary>
        /// Validates that all values required by the template are provided
        /// </summary>
        /// <param name="values">Dictionary of values to apply to the template</param>
        /// <returns>True if all required values are provided, false otherwise</returns>
        public bool ValidateInputs(Dictionary<string, string> values)
        {
            if (Inputs == null)
                return true;

            foreach (var input in Inputs.Where(i => i.Required))
            {
                // Skip validation if the field has a display condition that isn't met
                if (input.DisplayCondition != null && !IsDisplayConditionMet(input.DisplayCondition, values))
                    continue;

                if (!values.TryGetValue(input.Id, out string? value) || string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Applies the provided values to the template, replacing all markers and determining final cursor/selection positions.
        /// </summary>
        /// <param name="values">Dictionary of values to apply to the template</param>
        /// <returns>The processed template text with markers replaced by values</returns>
        public string Apply(Dictionary<string, string> values)
        {
            if (TemplateText == null) return string.Empty;
            
            // Store original cursor/selection state before modification
            CursorPosition = -1;
            SelectionStart = -1;
            SelectionEnd = -1;

            // Fill in default values for any missing inputs
            var allValues = new Dictionary<string, string>(values);
            if (Inputs != null)
            {
                foreach (var input in Inputs)
                {
                    if (!allValues.ContainsKey(input.Id) && !string.IsNullOrEmpty(input.DefaultValue))
                    {
                        allValues[input.Id] = input.DefaultValue;
                    }
                }
            }

            // Process conditional blocks first
            string processedTemplate = ProcessConditionalBlocks(TemplateText, allValues);

            // Replace all simple markers with their values
            foreach (var marker in GetMarkers())
            {
                string replacementValue = allValues.ContainsKey(marker) ? allValues[marker] : string.Empty;
                processedTemplate = processedTemplate.Replace("{{" + marker + "}}", replacementValue);
            }

            // Process cursor position marker BEFORE selection markers
            int cursorMarkerIndex = processedTemplate.IndexOf("[[cursor]]");
            if (cursorMarkerIndex >= 0)
            {
                CursorPosition = cursorMarkerIndex;
                processedTemplate = processedTemplate.Replace("[[cursor]]", string.Empty);
                // Remove selection markers if cursor is also present
                processedTemplate = processedTemplate.Replace("[[select]]", string.Empty);
                processedTemplate = processedTemplate.Replace("[[/select]]", string.Empty);
            }
            else
            {
                // Process selection range markers ONLY if no cursor marker was found
                int selectStartIndex = processedTemplate.IndexOf("[[select]]");
                int selectEndIndex = processedTemplate.IndexOf("[[/select]]");

                if (selectStartIndex >= 0 && selectEndIndex >= 0 && selectEndIndex > selectStartIndex)
                {
                    // Store the positions relative to the start of the processed template text
                    SelectionStart = selectStartIndex;
                    SelectionEnd = selectEndIndex - "[[select]]".Length; // Adjust for marker length

                    // Remove the markers
                    processedTemplate = processedTemplate.Replace("[[select]]", string.Empty);
                    processedTemplate = processedTemplate.Replace("[[/select]]", string.Empty);
                }
                else
                {
                    // Reset selection markers if not properly defined
                    SelectionStart = -1;
                    SelectionEnd = -1;
                }
            }

            return processedTemplate;
        }

        /// <summary>
        /// Processes conditional blocks in the template ({{#if condition}}...{{/if}})
        /// </summary>
        /// <param name="template">The template text to process</param>
        /// <param name="values">Dictionary of values for conditional evaluation</param>
        /// <returns>The processed template with conditional blocks evaluated</returns>
        private string ProcessConditionalBlocks(string template, Dictionary<string, string> values)
        {
            var ifBlockRegex = new Regex(@"{{#if\s+([^}]+)}}\r?\n?(.*?){{/if}}\r?\n?", RegexOptions.Singleline);
            var result = template;

            while (ifBlockRegex.IsMatch(result))
            {
                result = ifBlockRegex.Replace(result, match =>
                {
                    string condition = match.Groups[1].Value.Trim();
                    string content = match.Groups[2].Value;

                    bool conditionValue = false;
                    if (values.TryGetValue(condition, out string? value))
                    {
                        // Evaluate the condition as a boolean
                        conditionValue = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    }

                    return conditionValue ? content : "";
                });
            }

            return result;
        }

        /// <summary>
        /// Extracts a strongly typed value from a JsonElement based on its kind
        /// </summary>
        private static object? GetValueFromJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    return element.GetDouble();
                case JsonValueKind.String:
                    return element.GetString()!;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Loads a template from a JSON string (legacy format)
        /// </summary>
        /// <param name="json">JSON string containing template definition</param>
        /// <param name="filePath">Optional path to the JSON file (needed to find associated template text file)</param>
        /// <returns>A populated Template object</returns>
        public static Template LoadFromJson(string json, string? filePath = null)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(json, options);

                // Create template with properties from JSON
                var template = new Template
                {
                    TemplateName = jsonDoc.GetProperty("templateName").GetString()!,
                    Description = jsonDoc.GetProperty("description").GetString()!,
                    Inputs = new List<TemplateInput>(),
                    IsInsertMode = jsonDoc.TryGetProperty("isInsertMode", out var insertModeProp) && insertModeProp.GetBoolean()
                };

                // Load template text from associated .txt file
                string jsonFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
                string templateFilePath = Path.ChangeExtension(jsonFilePath, ".txt");

                if (File.Exists(templateFilePath))
                {
                    template.TemplateText = File.ReadAllText(templateFilePath);
                }
                else
                {
                    // Fallback to template property in JSON if it exists
                    template.TemplateText = jsonDoc.TryGetProperty("template", out var templateProp)
                        ? templateProp.GetString()!
                        : throw new FileNotFoundException($"Template text file not found: {templateFilePath}");
                }

                var inputs = jsonDoc.GetProperty("inputs");
                foreach (var input in inputs.EnumerateArray())
                {
                    var templateInput = new TemplateInput
                    {
                        Id = input.GetProperty("id").GetString()!,
                        Label = input.GetProperty("label").GetString()!,
                        Type = input.GetProperty("type").GetString()!,
                        Required = input.GetProperty("required").GetBoolean(),
                        DefaultValue = input.TryGetProperty("defaultValue", out var defaultValue) ?
                            defaultValue.GetString()! : "",
                        Description = input.TryGetProperty("description", out var description) ?
                            description.GetString()! : ""
                    };

                    // Parse display condition if present
                    if (input.TryGetProperty("displayCondition", out var displayConditionProp))
                    {
                        templateInput.DisplayCondition = new DisplayCondition
                        {
                            Field = displayConditionProp.GetProperty("field").GetString()!,
                            Operator = displayConditionProp.GetProperty("operator").GetString()!,
                            Value = GetValueFromJsonElement(displayConditionProp.GetProperty("value"))!
                        };
                    }

                    template.Inputs.Add(templateInput);
                }

                return template;
            }
            catch (Exception ex)
            {
                throw new FormatException($"Error parsing template JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets all available templates from the Templates directory
        /// </summary>
        /// <returns>A list of all available templates</returns>
        public static List<Template> GetAvailableTemplates()
        {
            var templates = new List<Template>();
            string templatesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");

            if (Directory.Exists(templatesDirectory))
            {
                // Look for .template files first (new format)
                foreach (var file in Directory.GetFiles(templatesDirectory, "*.template"))
                {
                    try
                    {
                        templates.Add(LoadFromFile(file));
                    }
                    catch (Exception ex)
                    {
                        // Log or handle the error, but continue processing other templates
                        Console.WriteLine($"Error loading template {file}: {ex.Message}");
                    }
                }

                // Then look for .json files (legacy format) that don't have a corresponding .template file
                foreach (var file in Directory.GetFiles(templatesDirectory, "*.json"))
                {
                    try
                    {
                        string templateFile = Path.ChangeExtension(file, ".template");

                        // Skip if we already loaded a .template version
                        if (File.Exists(templateFile))
                            continue;

                        templates.Add(LoadFromFile(file));
                    }
                    catch (Exception ex)
                    {
                        // Log or handle the error, but continue processing other templates
                        Console.WriteLine($"Error loading template {file}: {ex.Message}");
                    }
                }
            }

            return templates;
        }
    }
}
