# Repository Guidelines

## Project Structure & Module Organization
`AppRefiner/` hosts the WinForms client: UI logic sits in `MainForm*.cs`, while feature folders such as `Linters/`, `Stylers/`, `Services/`, `Refactors/`, and `Templates/` keep editor behaviors isolated. `AppRefinerHook/` contains the native C++ injection layer that talks to PeopleSoft’s Scintilla control. Parser work lives under `PeopleCodeParser.SelfHosted/` (self-hosted parser, type system services) and `ParserComparison/` (CLI harness for large corpus runs). Explore `PluginSample/` for extension patterns and `docs/` for deeper architecture and API references.

## Build, Test, and Development Commands
- `pwsh .\build.ps1 [-SelfContained]` — orchestrates the full build, compiling the hook with MSBuild and publishing the .NET app to `publish/`.
- `dotnet build AppRefiner/AppRefiner.csproj -c Debug` — quick iteration on the client without packaging.
- `dotnet run --project ParserComparison -- -f SampleTest.pcode` — sanity-check parser changes before bulk runs.
- `dotnet run --project ParserComparison -- -d <folder> --failed-dir out/failed` — regression sweep across a directory of `.pcode` files.

## Coding Style & Naming Conventions
Use four-space indentation in both C# and C++. Keep classes, methods, and public properties PascalCase; locals and parameters stay camelCase; private fields follow the existing `mainForm` pattern (no leading underscore). Favor `var` only when the inferred type is obvious. Push heavy logic into `Services/` or `Managers/`, keeping WinForms partial classes for wiring. Guard native hooks with defensive checks, mirroring patterns in `AutoIndent.cpp`.

## Testing Guidelines
Unit tests sit inside `PeopleCodeParser.SelfHosted/TypeSystem/Tests` and run with xUnit: `dotnet test PeopleCodeParser.SelfHosted/PeopleCodeParser.SelfHosted.csproj`. When adding parser scenarios, drop canonical PeopleCode snippets under `statement_tests/` and cover them with targeted facts. For integration scenarios, script reproducible inputs and expected outputs in `ParserComparison/Tests` and verify via the CLI before shipping.

## Commit & Pull Request Guidelines
Write imperative, one-line commits (e.g., “Add stack trace parsing service”); avoid placeholders like “WIP” once ready for review. Reference issue IDs when applicable. Pull requests should explain the behavior change, list manual validation (e.g., parser corpus results, UI screenshots), and note any migration steps for the hook DLL or database settings. Flag dependencies on PeopleSoft environments so reviewers can reproduce locally or in a lab VM.

## Plugin & Integration Notes
New stylers, linters, or refactors should mirror the abstractions in `AppRefiner/Shared/` and register through `PluginManager`. Place third-party DLLs under `Plugins/` and document required configuration in the PR description. Never commit customer connection strings; rely on the encrypted per-user settings managed by the Database service.
