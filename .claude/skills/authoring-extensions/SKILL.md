---
name: authoring-extensions
description: Use when adding a new linter, styler, refactor, command, language extension, or plugin to AppRefiner - covers the base class to inherit, the required members, and the discovery/registration mechanics for each extension type.
---

# Authoring AppRefiner Extensions

All extension types are discovered by reflection from the main assembly and from DLLs in
AppRefiner's `Plugins` directory. There is no registration step — create the class and it is found.

For every type below, the fastest path is to copy an existing implementation from the
corresponding `BuiltIn/` folder and adapt it. This file covers only what you cannot get by reading
that code.

## Adding New Linters

1. Inherit from `BaseLintRule` (extends `ScopedAstVisitor<object>`).
2. Implement `LINTER_ID`, `Description`, `Type`.
3. Override visitor methods for the AST nodes you care about (e.g. `VisitMethod(MethodNode node)`).
4. Use `AddReport()` to emit findings with line numbers and source spans.
5. Scope/variable info is available from the base class: `GetCurrentScope()`, `FindVariable(name)`,
   `GetAllVariables()`, `GetUnusedVariables()`.

## Adding New Stylers

1. Inherit from `BaseStyler` (extends `ScopedAstVisitor<object>`).
2. Override visitor methods for the nodes of interest.
3. Use `AddIndicator()` for visual feedback with source spans.
4. Consider offering Quick Fixes via `GetQuickFixes()`.

`TypeInferenceVisitor` runs **before** all stylers (StylerManager orchestrates this), so type
information is already attached to every node when a styler executes.

## Adding New Refactors

1. Inherit from `ScopedRefactor` — the unified base class, with automatic scope tracking.
2. Override the static metadata properties (`RefactorName`, `RefactorDescription`, ...).
3. Use `EditText()`, `InsertText()`, `DeleteText()` to track modifications.
4. Collect user input via `ShowRefactorDialog()` if needed.

## Adding New Commands

Built-in commands live in `Commands/BuiltIn/`; plugins may also supply them. All inherit
`BaseCommand` (namespace `AppRefiner.Commands`) and appear in the command palette.

**Required:** `CommandName`, `CommandDescription`.
**Optional:** `RequiresActiveEditor` (default `true`), `DynamicEnabledCheck` (a `Func<bool>`).

### Keyboard shortcuts

Override `InitializeShortcuts(IShortcutRegistrar registrar, string commandId)`. The registrar
offers `IsShortcutAvailable`, `TryRegisterShortcut`, and `GetShortcutDisplayText`.

The non-obvious part: **pass `this` to `TryRegisterShortcut`, not a lambda or an `Action`.** When the
shortcut fires, `ApplicationKeyboardService` builds a *fresh* `CommandContext` reflecting current
application state and calls your `Execute(context)` with it. Capturing state in a closure instead
would hand the command a stale editor.

Check availability before registering, provide fallback combinations if your preferred one is taken,
and call `SetRegisteredShortcut()` on success so the palette shows the shortcut.

### CommandContext

`Execute(CommandContext context)` receives: `ActiveEditor` (may be null), `LinterManager`,
`StylerManager`, `RefactorManager`, `SettingsService`, `AutoCompleteService`, `FunctionCacheManager`,
`AutoSuggestSettings`. The database is reached through `context.ActiveEditor.DataManager`.

## Adding New Language Extensions

Language extensions add properties and methods to existing PeopleCode types via code
transformations, similar to C# extension methods. Inherit `BaseLanguageExtension`
(namespace `AppRefiner.LanguageExtensions`).

Define two collections:
- `TargetTypes` — the types the extension applies to.
- `Transforms` — the methods/properties it provides.

**Every transform applies to every target type.** A single extension class can define many
transforms; they share active state and configuration but are shown as individual rows in the
extension grid UI.

### Simple pattern-based transforms

`ExtensionTransform.CreateSimple()` handles plain string-replacement cases. Pattern placeholders:

- `%1` — the target expression (the object before the dot)
- `%2`, `%3`, `%4`, ... — the 1st, 2nd, 3rd function argument

Unreplaced placeholders for omitted optional arguments are removed automatically — so
`"Find(%2, %1, %3)"` yields `Find("foo", &string)` when the optional third argument is absent.

### Custom transform logic

For anything more complex, use the full `ExtensionTransform` constructor with a `TransformAction`
delegate, which receives `(editor, node, matchedType, variableRegistry)`:

- `node` — a `MemberAccessNode` for properties, a `FunctionCallNode` for methods
- `matchedType` — the `TypeInfo` that actually matched, which matters for multi-type extensions
- `variableRegistry` — may be null

Modify code through `ScintillaManager.ReplaceTextRange()`.

See `LanguageExtensions/BuiltIn/` (StringExtensions, ArrayExtensions) for working examples.

## Plugin Development

1. Create a .NET 8 class library.
2. Reference the AppRefiner project.
3. Implement whichever base classes you need (any of the above).
4. Copy the compiled DLL into AppRefiner's `Plugins` directory.
5. Restart AppRefiner — plugins are only scanned at startup.

Check the Debug Console for registration messages if an extension doesn't appear.
