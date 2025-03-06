using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AppRefiner.Templates
{
    /// <summary>
    /// Represents an input field in a template
    /// </summary>
    public class TemplateInput
    {
        /// <summary>
        /// Identifier used in template markers (e.g., {{id}})
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// User-friendly label for the input field
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Data type of the input (string, number, boolean, etc.)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Whether this input is required
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Default value for this input
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// Additional description or help text
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Represents a text template with replaceable markers
    /// </summary>
    public class Template
    {
        /// <summary>
        /// Display name of the template
        /// </summary>
        public string TemplateName { get; set; }

        /// <summary>
        /// Description of what the template does
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Collection of input fields required by the template
        /// </summary>
        public List<TemplateInput> Inputs { get; set; }

        /// <summary>
        /// The template text with {{marker}} placeholders
        /// </summary>
        public string TemplateText { get; set; }

        /// <summary>
        /// Loads a template from a JSON file
        /// </summary>
        /// <param name="filePath">Path to the template JSON file</param>
        /// <returns>A populated Template object</returns>
        public static Template LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Template file not found: {filePath}");

            string json = File.ReadAllText(filePath);
            return LoadFromJson(json);
        }

        /// <summary>
        /// Loads a template from a JSON string
        /// </summary>
        /// <param name="json">JSON string containing template definition</param>
        /// <returns>A populated Template object</returns>
        public static Template LoadFromJson(string json)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(json, options);
                
                var template = new Template
                {
                    TemplateName = jsonDoc.GetProperty("templateName").GetString(),
                    Description = jsonDoc.GetProperty("description").GetString(),
                    TemplateText = jsonDoc.GetProperty("template").GetString(),
                    Inputs = new List<TemplateInput>()
                };

                var inputs = jsonDoc.GetProperty("inputs");
                foreach (var input in inputs.EnumerateArray())
                {
                    template.Inputs.Add(new TemplateInput
                    {
                        Id = input.GetProperty("id").GetString(),
                        Label = input.GetProperty("label").GetString(),
                        Type = input.GetProperty("type").GetString(),
                        Required = input.GetProperty("required").GetBoolean(),
                        DefaultValue = input.TryGetProperty("defaultValue", out var defaultValue) ? 
                            defaultValue.GetString() : null,
                        Description = input.TryGetProperty("description", out var description) ? 
                            description.GetString() : null
                    });
                }

                return template;
            }
            catch (Exception ex)
            {
                throw new FormatException($"Error parsing template JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets all markers in the template
        /// </summary>
        /// <returns>A list of unique marker names without braces</returns>
        public List<string> GetMarkers()
        {
            var markers = new HashSet<string>();
            var regex = new Regex(@"{{([^{}]+)}}");
            var matches = regex.Matches(TemplateText);

            foreach (Match match in matches)
            {
                markers.Add(match.Groups[1].Value);
            }

            return markers.ToList();
        }

        /// <summary>
        /// Validates that all values required by the template are provided
        /// </summary>
        /// <param name="values">Dictionary of values to apply to the template</param>
        /// <returns>True if all required values are provided, false otherwise</returns>
        public bool ValidateInputs(Dictionary<string, string> values)
        {
            foreach (var input in Inputs.Where(i => i.Required))
            {
                if (!values.ContainsKey(input.Id) || string.IsNullOrWhiteSpace(values[input.Id]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Applies the provided values to the template, replacing all markers
        /// </summary>
        /// <param name="values">Dictionary of values to apply to the template</param>
        /// <returns>The processed template text with markers replaced by values</returns>
        public string Apply(Dictionary<string, string> values)
        {
            string result = TemplateText;

            // Fill in default values for any missing inputs
            var allValues = new Dictionary<string, string>(values);
            foreach (var input in Inputs)
            {
                if (!allValues.ContainsKey(input.Id) && !string.IsNullOrEmpty(input.DefaultValue))
                {
                    allValues[input.Id] = input.DefaultValue;
                }
            }

            // Replace all markers with their values
            foreach (var marker in GetMarkers())
            {
                string replacementValue = allValues.ContainsKey(marker) ? allValues[marker] : string.Empty;
                result = result.Replace("{{" + marker + "}}", replacementValue);
            }

            return result;
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
                foreach (var file in Directory.GetFiles(templatesDirectory, "*.json"))
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
            }
            
            return templates;
        }
    }
}
