# Using Templates in AppRefiner

AppRefiner utilizes a template system to help you quickly generate code snippets, boilerplate, or complex structures based on user input. This promotes consistency and speeds up development.

## Overview

Templates allow you to define reusable code patterns with placeholders and conditional logic. When you invoke a template, AppRefiner prompts you for any required inputs and then inserts the generated code into your editor. By default, templates replace the entire content of the active editor, but they can also be configured to insert text at the current cursor position.

## Template Location and Discovery

-   AppRefiner automatically discovers template files (`*.template`) located in the `AppRefiner/Templates` directory within its installation path.
-   Any valid `.template` file placed in this folder will be available for use after restarting AppRefiner.

## How to Use Templates

1.  Open the **Command Palette** (`Ctrl+Shift+P`).
2.  Type `Template:` to filter the list of commands.
3.  Select the desired template by its name (e.g., `Template: Plain Class`).
4.  AppRefiner will display a dialog prompting you for the inputs defined in the template file. Fill in the required values.
5.  Click "OK" or confirm the dialog.
6.  The generated code will be inserted at your current cursor position.

## Creating Custom Templates

You can easily create your own templates:

1.  Create a new file with the `.template` extension (e.g., `MyLoop.template`) in the `AppRefiner/Templates` directory.
2.  Structure the file with two sections separated by `---`:
    *   **Top Section:** A JSON object defining the template's metadata and inputs.
    *   **Bottom Section:** The template text using a Handlebars-like syntax.

### Template File Structure (`*.template`)

```plaintext
{
  "templateName": "User-Friendly Name",
  "description": "What this template does.",
  "isInsertMode": true | false,
  "inputs": [
    {
      "id": "internal_id",
      "label": "Prompt Label",
      "type": "string | boolean | number",
      "required": true | false,
      "defaultValue": "Optional default",
      "description": "Optional help text for input.",
      "displayCondition": { // Optional: Show this input only if...
        "field": "other_input_id", // ...this other input's ID...
        "operator": "equals" | "notEquals", // ...matches/doesn't match...
        "value": "the target value" | true | false // ...this value.
      }
    }
    // ... more inputs ...
  ]
}
---
Template text goes here.
Use {{internal_id}} for substitutions.
Use {{#if boolean_or_truthy_id}} Conditional text {{/if}}.
Use [[select]]this text will be selected[[/select]] after insertion.
```

### JSON Header Fields

-   **`templateName` (string, required):** The name displayed in the Command Palette.
-   **`description` (string, required):** A brief explanation shown in the Command Palette.
-   **`isInsertMode` (boolean, optional):** If set to `true`, the template content will be inserted at the current cursor position instead of replacing the entire editor content. Defaults to `false` (replace mode).
-   **`inputs` (array, required):** A list of input objects needed by the template.
    -   **`id` (string, required):** The internal identifier used in the template text (e.g., `{{id}}`).
    -   **`label` (string, required):** The user-friendly text shown in the input prompt.
    -   **`type` (string, required):** The expected data type. Common types include `string`, `boolean`, `number`. This influences validation and potentially the UI control used.
    -   **`required` (boolean, required):** Whether the user *must* provide a value for this input.
    -   **`defaultValue` (string, optional):** A default value pre-filled in the input prompt.
    -   **`description` (string, optional):** Additional help text displayed for the input field.
    -   **`displayCondition` (object, optional):** Defines a condition for whether this input field is shown to the user.
        -   `field` (string): The `id` of another input field to check.
        -   `operator` (string): The comparison type, e.g., `equals`, `notEquals`.
        -   `value` (string | boolean | number): The value to compare against the `field`'s value.

### Template Body Syntax

-   **Substitution:** Use double curly braces `{{input_id}}` to insert the value provided by the user for the corresponding input `id`.
-   **Conditional Blocks:** Use `{{#if input_id}} ... {{/if}}` to include the enclosed text only if the value for `input_id` is considered "truthy" (e.g., boolean `true`, a non-empty string, a non-zero number).
-   **Post-Insertion Selection:** Use `[[select]] ... [[/select]]` to specify text that should be automatically selected in the editor after the template is inserted. This is useful for guiding the user to the next logical place to type. *Note: Only the first `[[select]]` block encountered is typically used.*
-   **Post-Insertion Cursor Position:** Use `[[cursor]]` to specify the exact location where the cursor should be placed after the template is inserted. If both `[[select]]` and `[[cursor]]` are present, `[[select]]` usually takes precedence. *Note: Only the first `[[cursor]]` marker is typically used.*

## Example: `PlainClass.template`

This built-in template generates a basic PeopleCode class structure.

```plaintext
{
  "templateName": "Plain Class",
  "description": "Basic PeopleCode class with constructor",
  "isInsertMode": false,
  "inputs": [
    {
      "id": "class_name",
      "label": "Class Name",
      "type": "string",
      "required": true,
      "defaultValue": "MyClass",
      "description": "The name of the class to generate"
    },
    {
      "id": "include_comments",
      "label": "Include Comments",
      "type": "boolean",
      "required": true,
      "defaultValue": "true",
      "description": "Whether to include additional comments in the generated code"
    }
  ]
}
---
{{#if include_comments}}
/* This is a basic class template */
{{/if}}
class {{class_name}}
{{#if include_comments}}
   /* Constructor */
{{/if}}
   method {{class_name}}();

end-class;

{{#if include_comments}}
/* Constructor implementation */
{{/if}}
method {{class_name}}
   [[select]]/* your code goes here */[[/select]]
end-method;
```

When invoked, this template asks for a "Class Name" and whether to "Include Comments". Based on the input, it generates the class definition, conditionally adding comments and selecting the placeholder text within the constructor method.

---

*This document requires details specific to AppRefiner's template/snippet implementation.* 