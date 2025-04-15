## Code Flow Improvements

> **Security Note**: AppRefiner integrates deeply with Application Designer using techniques like DLL injection to provide enhanced features. This code runs with the same privileges as Application Designer. Ensure you trust the source of your AppRefiner installation.

AppRefiner provides several features that streamline the process of writing and understanding code within the Application Designer editor:

### Auto Pairing

Controlled via the "Auto-Pairing" setting (toggled using the `Editor: Toggle Auto Pairing` command), this feature automatically pairs quotes and parentheses as you type:

- When you type an opening quote (`"`), a closing quote is automatically inserted.
- When you type an opening parenthesis (`(`), a closing parenthesis is automatically inserted.
- The cursor is positioned between the paired characters for immediate typing.
- Typing over an auto-inserted closing character skips it, preventing duplication.
- Special handling exists for commas and semicolons to ensure correct placement relative to quotes.

### Hover Tooltips

AppRefiner enhances the editor with hover tooltips that provide contextual information about code elements:

- Tooltips appear when hovering the mouse cursor over certain code elements (after a brief delay).
- The specific information shown depends on which **Tooltip Providers** are enabled and configured in the **Tooltips Tab** of the main AppRefiner window. Examples include:
    - Details about PeopleSoft objects (requires DB connection).
    - Parameter information for function/method calls.
    - Declaration information for variables.
    - Explanations for items marked by Stylers (e.g., why a variable is grayed out).
- Tooltips automatically hide when the mouse moves away.

*Refer to the UI Overview and the Tooltips Tab in AppRefiner for managing specific tooltip providers.*