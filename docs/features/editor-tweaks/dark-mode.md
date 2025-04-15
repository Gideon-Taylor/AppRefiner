# Dark Mode

Dark Mode in AppRefiner provides a low-light interface that reduces eye strain and is ideal for working in dimly lit environments.

## Overview

AppRefiner's Dark Mode transforms the code editor into a dark-themed color scheme. This feature is designed to reduce eye strain during extended coding sessions and provide a modern, sleek appearance.

## Benefits

- **Reduced eye strain**: Lower brightness and contrast are easier on the eyes, especially during long coding sessions
- **Reduced blue light**: Dark backgrounds emit less blue light, which can help with sleep patterns if you code at night
- **Improved focus**: The dark background can help highlight syntax coloring and make code stand out more clearly

## Enabling and Controlling Dark Mode

There are two ways to control Dark Mode:

1.  **Apply to Current Editor**: Use the Command Palette (`Ctrl+Shift+P`) and run the `Editor: Dark Mode` command. This applies dark styling immediately to the currently active Application Designer editor window.
2.  **Apply Automatically**: Use the Command Palette (`Ctrl+Shift+P`) and run the `Editor: Toggle Auto Dark Mode` command. This toggles the setting (found on the Settings tab in AppRefiner) that controls whether dark mode is applied automatically whenever AppRefiner enhances an editor window.

**Note on Reverting**: Applying dark mode via the `Editor: Dark Mode` command is currently a one-way transformation for that specific editor instance. There is no direct "undo" command for it. To revert the appearance of an editor window that had dark mode applied manually, you must close and reopen that specific PeopleCode/HTML/SQL definition in Application Designer. If you used the *Auto Dark Mode* setting, simply toggle it off (`Editor: Toggle Auto Dark Mode`) and reopen the definition.

## Related Features

- [Code Folding](code-folding.md)
- [Annotations](annotations.md)
