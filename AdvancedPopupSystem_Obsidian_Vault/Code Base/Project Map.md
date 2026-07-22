---
type: code
status: active
description: Repository orientation — where code lives, the assemblies, define constraints, and the folder → namespace table. Read when placing new code or navigating.
code_paths:
  - Assets/advanced-popup-system/
---

# Project Map

- **Package root:** `Assets/advanced-popup-system/` — this folder **is** the published UPM package **and** its own git
  repo (`.git` here). Its `package.json`, `README.md`, `documentation.md`, `CHANGELOG.md`, `LICENSE.md` ship to users
  (see [[Shipped Docs]]). This vault + `.agents`/`.claude`/`CLAUDE.md` also live here (next to `.git`), versioned with
  the asset — internal tooling; exclude them from the published package if you don't want them imported by consumers. The package is built by the dev-only exporter `Editor/Build/APSPackageExporter` (self-excluded) — see [[Build & Packaging]].
- **Runtime code:** `Runtime/` (namespace root `AdvancedPS.Core`). **Editor code:** `Editor/` (`AdvancedPS.Editor`).
  **Built-in generated displays:** `Runtime/Generated/Displays/` (ship read-only). **Consumer-generated code:**
  `Assets/AdvancedPopupSystem/Generated/` in the *consumer* project (the enum + custom displays — see below), **not** in
  the package. **Samples:** `Samples/` (`AdvancedPS.Core.Examples`).
- **Settings JSON** is written to the **consumer project's** `Assets/Resources/AP_Settings.json` (via
  `Application.dataPath/Resources`), not into the package — see [[Settings & Logging]].
- **Layer store** is written to the consumer project's `ProjectSettings/APS_Layers.json` (outside `Assets`, editor-only,
  never shipped) — the update-safe source of truth for `PopupLayerEnum` ([[Layers]], [[Build & Packaging]]).
- **Package lookup:** `FileSearcher` resolves the package via `PackageInfo.FindForAssembly` (folder-name search as
  fallback), so image/built-in-display paths work whether APS is under `Packages/` (UPM) or `Assets/`. Generated code
  (enum + custom displays) it writes to the **consumer** project at `Assets/AdvancedPopupSystem/Generated/` — never into
  the package, so read-only UPM installs work and updates never clobber it ([[Editor & Codegen]], [[Build & Packaging]]).

## Assemblies (`.asmdef`) & define constraints

| Assembly | Folder | rootNamespace | Constraint | Notes |
|----------|--------|---------------|------------|-------|
| `dest-69.advanced-popup-system` | `Runtime/` | `AdvancedPS.Core` | — | Core runtime; references Newtonsoft.Json + `AdvancedPS.Generated.Layers` (the generated enum) |
| `dest-69.advanced-popup-system.newinput` | `Runtime/Core/Input/New/` | `AdvancedPS.Core.Input` | `HAS_NEWINPUT` | Input System path |
| `dest-69.advanced-popup-system.oldinput` | `Runtime/Core/Input/Old/` | `AdvancedPS.Core.Input` | `!HAS_NEWINPUT` | Legacy Input Manager path |
| `dest-69.advanced-popup-system.dotween` | `Runtime/Generated/Displays/DoTweenDisplay/` | — | `DOTWEEN` | Optional DoTween display |
| `dest-69.advanced-popup-system.editor` | `Editor/` | `AdvancedPS.Editor` | Editor platform | APS window, inspectors, menus |
| `dest-69.advanced-popup-system.addressables` | `Runtime/Addressables/` | `AdvancedPS.Core.System` | `APS_ADDRESSABLES` | Optional Addressables resolver ([[Addressables]]) |
| `dest-69.advanced-popup-system.addressables.editor` | `Editor/Addressables/` | `AdvancedPS.Editor` | Editor + `APS_ADDRESSABLES` | Optional index codegen + group sync |
| `AdvancedPS.Generated.Layers` | `Assets/AdvancedPopupSystem/Generated/Layers/` (**consumer**) | `AdvancedPS.Core` | — | Holds generated `PopupLayerEnum`; **no references** (so core can reference it without a cycle); `autoReferenced` |
| `AdvancedPS.Generated.Displays` | `Assets/AdvancedPopupSystem/Generated/Displays/` (**consumer**) | `AdvancedPS.Core` | — | User-authored custom displays; references core + `AdvancedPS.Generated.Layers` |
| `AdvancedPS.Bootstrap` | `Editor/Bootstrap/` | `AdvancedPS.Bootstrap` | Editor platform | Seeds `Generated.Layers` on fresh install; **zero core references** (compiles/runs while core is red) |
| `…examples.dotween` / `.easing` / `.performance` | `Samples/*/` | `AdvancedPS.Core.Examples` | (DOTWEEN for dotween) | Showcase scenes |

**Define constraints are load-bearing** ([[Invariants]]): `HAS_NEWINPUT` selects the New vs Old input assembly
(mutually exclusive — set by a `versionDefine` on `com.unity.inputsystem`); `DOTWEEN` gates the DoTween display + its
example; `APS_ADDRESSABLES` (a `versionDefine` on `com.unity.addressables`) gates the optional Addressables assemblies +
the inspector's Addressable box ([[Addressables]]). Never merge the input assemblies or reference the DoTween/Addressables
assemblies from core runtime.

## Folder → namespace (`Runtime/`)

| Folder | Namespace | Contents |
|--------|-----------|----------|
| `Core/APSystem/` | `AdvancedPS.Core` (+ `.System`) | `AdvancedPopupSystem`, `DisplayRegistry`; `.System`: `Operation`, `APSStats`, `DisplaySettingsFactory` |
| `Core/Abstract/` | `AdvancedPS.Core.System` | `IAdvancedPopup` (abstract base), `IDisplay`/`IDisplay<T>`/`DisplayBase<T>`, `IDisplaySettings`/`BaseSettings<T>` |
| `Core/Popups/` | `AdvancedPS.Core` | `AdvancedPopup`, `AdvancedPopupInstantiate` (stub) |
| `Core/Easing/` | `AdvancedPS.Core.System` | `EasingType`, `EasingFunctions` |
| `Core/Input/` | `AdvancedPS.Core` (bindings); `AdvancedPS.Core.Input` (New/Old) | `PopupKeyBinding`; `KeyEventSystemAPS` (per input backend) |
| `Generated/` | `AdvancedPS.Core` | `PopupLayerEnum.generated.cs`; `Displays/<Name>Display/` (Fade/Scale/Slide/DoTween), `SlideEnum` |
| `Settings/` | `AdvancedPS.Core.System` | `PopupSettings` + `InspectorEnum`, `SettingsManager` |
| `Utils/` | `AdvancedPS.Core.Utils` | `APLogger`, `FileSearcher`, `TaskUtils`, `TypeHelper` |
| `Editor/` | `AdvancedPS.Editor` (+ `.Styles`) | APS window (`PopupSystemEditor` + Layer/Displays/Settings panels), inspectors, `CreateAdvancedPopup` |

## Depends on

- [[Invariants]] (package & assembly rules)
