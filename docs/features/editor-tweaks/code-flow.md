## Code Flow Improvements

> **Security Note**: The following features require code execution within the Application Designer process context through DLL injection. While this enables powerful features, it also means the code runs with the same privileges as the Application Designer. Please ensure you trust the source of the AppRefiner installation.

### Auto Indentation
AppRefiner provides intelligent auto-indentation for PeopleCode editors that automatically adjusts indentation based on code blocks. The indentation rules understand common PeopleCode constructs including:

- If/End-If blocks
- For/End-For loops
- While/End-While loops
- Method/End-Method blocks
- Function/End-Function blocks
- Evaluate/End-Evaluate blocks
- Try/End-Try blocks
- Repeat/Until loops

The indentation is context-aware and handles:
- Proper indentation of nested blocks
- Special cases like `else` clauses that align with their parent `if` statement
- Automatic de-indentation of `end-*` statements to match their opening block
- Proper handling of method declarations in class headers

### Auto Pairing
If enabled AppRefiner will automatically pairs quotes and parentheses as you type, making code editing more efficient (note: This requires Auto Indentation to be enabled too):

- When you type an opening quote (`"`), a closing quote is automatically inserted
- When you type an opening parenthesis (`(`), a closing parenthesis is automatically inserted
- The cursor is positioned between the paired characters
- When typing over an auto-inserted closing character, it's skipped instead of duplicated
- Special handling for commas and semicolons ensures they're placed outside of quoted strings

### Hover Tooltips
When you enable Auto Indentation this feature begins working seamlessly. AppRefiner will enhance the editor with hover tooltips that provide additional information about styled code elements:

- Tooltips appear when hovering over styled text (after a 1s delay)
- Tooltips can display:
  - Information on styled text (ex: grayed out items can tell you why they are grayed out).
- Tooltips automatically hide when the mouse moves away from the text