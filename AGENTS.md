# Repository Guidelines

## Project Structure & Module Organization

This repository is the `com.volumebox.toolbox` Unity package. Runtime code is grouped by feature in `Audio System/`, `Message System/`, `Object Pool/`, `Travel System/`, and `Update System/`. Shared helpers, attributes, settings, and wrappers live in `Utils/`. Keep Unity editor-only code under `Editor/`; it compiles through `VolumeBox.Toolbox.Editor.asmdef` and must not be referenced by runtime assemblies. Package images and GUI assets are in `Data/`, the default prefab is in `Resources/`, and sample/test scenes are in `Scenes/` and `Tests/`.

Every Unity asset must retain its matching `.meta` file. Move or rename assets through Unity when practical so GUID references remain valid.

## Build, Test, and Development Commands

This package has no standalone build script. Open a Unity project that includes this directory as a local package, then let Unity compile `VolumeBox.Toolbox.asmdef` and the editor assembly.

- Unity: `Window > General > Test Runner` runs Edit Mode and Play Mode tests.
- Batch mode: `Unity.exe -batchmode -quit -projectPath <project> -runTests -testPlatform EditMode -testResults results.xml` executes tests in CI.
- `git diff --check` detects whitespace errors before committing.

Do not use `npm test`; `package.json` contains Unity Package Manager metadata and release versioning, not Node scripts.

## Coding Style & Naming Conventions

Use C# with four-space indentation and braces on new lines. Follow existing conventions: PascalCase for types, methods, properties, and public members; camelCase for parameters and private fields; `I` prefixes for interfaces (for example, `IPooled`). Keep code in the `VolumeBox.Toolbox` namespace, with test code in `VolumeBox.Toolbox.Tests`. Prefer one primary type per file and match its filename. Avoid runtime dependencies on `UnityEditor` APIs.

## Testing Guidelines

Tests use NUnit and Unity Test Framework in `Tests/Tests.asmdef`, enabled by `UNITY_INCLUDE_TESTS`. Name fixtures and files `*Tests.cs`; use `[Test]` for synchronous behavior and `[UnityTest]` for coroutine or frame-dependent behavior. Add regression coverage for changes to pooling, scene travel, messaging, or update scheduling. Run the relevant fixture plus the complete test suite before submitting.

## Commit & Pull Request Guidelines

Recent commits use short, imperative summaries such as `Fix scene unloading synchronization` and `Categorize scene pools`. Keep each commit focused; semantic-release also recognizes Angular/Conventional Commit forms such as `fix: ...` and `feat: ...`. Pull requests should explain behavior changes, identify tested Unity versions and test modes, link related issues, and include screenshots or recordings for inspector, editor-window, or GUI changes. Call out package-version or asset-GUID changes explicitly.
