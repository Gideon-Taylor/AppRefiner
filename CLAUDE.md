# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AppRefiner is a Windows Forms application that enhances PeopleSoft's Application Designer with modern development features including code folding, linting, syntax highlighting, and refactoring tools. It integrates with Application Designer through Win32 API hooks and provides a plugin architecture for extensibility.

## Building

```powershell
# Framework-dependent build (requires .NET 8 runtime on target machine)
.\build.ps1

# Self-contained build (includes .NET runtime)
.\build.ps1 -SelfContained
```

Prerequisites: Windows with Visual Studio 2022 (C++ development tools), .NET 8 SDK, PowerShell 5.1+.

**IMPORTANT FOR CLAUDE CODE USERS**: Do not attempt to build the project directly when working in WSL environments. AppRefiner is a Windows-specific application that relies on Windows Forms, Win32 APIs, and Visual Studio C++ build tools. Building should only be done on Windows with proper development tools installed.

The PeopleCode parser is self-hosted in `PeopleCodeParser.SelfHosted` — pure C#, no grammar file and no code generation step.

## Conventions

### MessageBox dialogs

**Never use `MessageBox.Show()`.** Always use this pattern instead, so the dialog is correctly owned by the Application Designer window it belongs to:

```csharp
Task.Delay(100).ContinueWith(_ =>
{
    var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;
    var handleWrapper = new WindowWrapper(mainHandle);
    new MessageBoxDialog("Your message here", "Dialog Title", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
});
```

The `Task.Delay(100).ContinueWith()` wrapper is required for correct threading, and the main window handle must be passed to *both* the `MessageBoxDialog` constructor and `ShowDialog()`.

### Debug logging

AppRefiner has its **own** `Debug` class — not `System.Diagnostics.Debug`. Use `Debug.Log()`, never `Debug.WriteLine()`.

### Database access

Components requiring database access must declare `DataManagerRequirement` and check `HasDatabaseConnection()` before executing any database-dependent logic.

### Threading

Use `Invoke()` for UI thread operations. AST parsing runs on background threads, and services use concurrent collections for thread safety.

## Writing extensions

To add a linter, styler, refactor, command, language extension, or plugin, see the
`authoring-extensions` skill in `.claude/skills/authoring-extensions/`.
