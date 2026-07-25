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
  **Generated code that ships:** `Runtime/Generated/` — the layer enum (`Layers/`, its own assembly) and the built-in
  displays (`Displays/`). **Consumer-generated code:** custom displays only, in the *consumer* project at
  `Assets/AdvancedPopupSystem/Generated/Displays/`. **Samples:** sources in `Samples~/` (hidden from Unity, opt-in — [[Samples]]); `Samples/Utils/` ships raw; assembly `AdvancedPS.Core.Examples`.
- **Settings JSON** is written to the **consumer project's** `Assets/Resources/AP_Settings.json` (via
  `Application.dataPath/Resources`), not into the package — see [[Settings & Logging]].
- **Layer store** is written to the consumer project's `ProjectSettings/APS_Layers.json` (outside `Assets`, editor-only,
  never shipped) — the update-safe source of truth for `PopupLayerEnum` ([[Layers]], [[Build & Packaging]]).
- **Package lookup:** `FileSearcher` resolves the package via `PackageInfo.FindForAssembly` (folder-name search as
  fallback), so image / enum / built-in-display paths work whether APS is under `Packages/` (UPM) or `Assets/`. It also
  exposes `IsPackageWritable`/`EmbedPackage`: the enum ships in the package, so editing layers needs a writable copy
  (embed a read-only UPM install). Custom displays it writes to the consumer project (no writable package needed)
  ([[Editor & Codegen]], [[Build & Packaging]]).

## Assemblies (`.asmdef`) & define constraints

| Assembly | Folder | rootNamespace | Constraint | Notes |
|----------|--------|---------------|------------|-------|
| `dest-69.advanced-popup-system` | `Runtime/` | `AdvancedPS.Core` | — | Core runtime; references Newtonsoft.Json + `AdvancedPS.Generated.Layers` (the generated enum) |
| `dest-69.advanced-popup-system.newinput` | `Runtime/Core/Input/New/` | `AdvancedPS.Core.Input` | `HAS_NEWINPUT` | Input System path |
| `dest-69.advanced-popup-system.oldinput` | `Runtime/Core/Input/Old/` | `AdvancedPS.Core.Input` | `!HAS_NEWINPUT` | Legacy Input Manager path |
| `dest-69.advanced-popup-system.dotween` | `Runtime/Generated/Displays/DoTweenDisplay/` | — | `DOTWEEN` | Optional DoTween display |
| `dest-69.advanced-popup-system.editor` | `Editor/` | `AdvancedPS.Editor` | Editor platform | APS window, inspectors, menus |
| `dest-69.advanced-popup-system.editor.build` | `Editor/Build/` | `AdvancedPS.Editor` | Editor + `APS_DEV` | Dev-only exporter tooling; `APS_DEV` is set only in the dev project's Player settings, so the UPM/git channel ships the folder **inert** (never compiles for consumers) |
| `dest-69.advanced-popup-system.addressables` | `Runtime/Addressables/` | `AdvancedPS.Core.System` | `APS_ADDRESSABLES` | Optional Addressables resolver ([[Addressables]]) |
| `dest-69.advanced-popup-system.addressables.editor` | `Editor/Addressables/` | `AdvancedPS.Editor` | Editor + `APS_ADDRESSABLES` | Optional index codegen + group sync |
| `AdvancedPS.Generated.Layers` | `Runtime/Generated/Layers/` (**ships in package**) | `AdvancedPS.Core` | — | Holds generated `PopupLayerEnum`; **no references** (so core references it without a cycle); `autoReferenced`; editing needs a writable package |
| `AdvancedPS.Generated.Displays` | `Assets/AdvancedPopupSystem/Generated/Displays/` (**consumer**) | `AdvancedPS.Core` | — | User-authored custom displays; references core + `AdvancedPS.Generated.Layers` |
| `…examples.addressables` / `.dotween` / `.easing` / `.performance` | `Samples~/*/` (**hidden, opt-in**) | `AdvancedPS.Core.Examples` | `APS_ADDRESSABLES` (+`DOTWEEN` for dotween) | Showcase scenes; sources hidden in `Samples~/` ([[Samples]]) |

**Define constraints are load-bearing** ([[Invariants]]): `HAS_NEWINPUT` selects the New vs Old input assembly
(mutually exclusive — set by a `versionDefine` on `com.unity.inputsystem`); `DOTWEEN` gates the DoTween display + its
example; `APS_ADDRESSABLES` (a `versionDefine` on `com.unity.addressables`) gates the optional Addressables assemblies +
the inspector's Addressable box ([[Addressables]]); `APS_DEV` gates the dev-only `Editor/Build/` assembly and lives
**only** in the dev project's scripting defines (Project Settings ▸ Player — outside the repo; re-add it on a fresh
dev-project checkout). Never merge the input assemblies or reference the DoTween/Addressables
assemblies from core runtime.

## Folder → namespace (`Runtime/`)

| Folder | Namespace | Contents |
|--------|-----------|----------|
| `Core/APSystem/` | `AdvancedPS.Core` (+ `.System`) | `AdvancedPopupSystem`, `EscapePolicyEnum`, `DisplayRegistry`; `.System`: `Operation`, `APSStats`, `DisplaySettingsFactory` |
| `Core/Abstract/` | `AdvancedPS.Core.System` | `IAdvancedPopup` (abstract base), `IDisplay`/`IDisplay<T>`/`DisplayBase<T>`, `IDisplaySettings`/`BaseSettings<T>` |
| `Core/Popups/` | `AdvancedPS.Core` | `AdvancedPopup`, `AdvancedPopupInstantiate` (stub) |
| `Core/Easing/` | `AdvancedPS.Core.System` | `EasingType`, `EasingFunctions` |
| `Core/Input/` | `AdvancedPS.Core.Input` (New/Old) | `PointerEventSystemAPS` (per input backend) — the only input APS polls |
| `Generated/` | `AdvancedPS.Core` | `PopupLayerEnum.generated.cs`; `Displays/<Name>Display/` (Fade/Scale/Slide/DoTween), `SlideEnum` |
| `Settings/` | `AdvancedPS.Core.System` (config SOs: `AdvancedPS.Core`) | `PopupSettings` + `InspectorEnum`, `SettingsManager`; the consumer-side config assets `LayerCanvasConfig` ([[Layers]]) and `PopupOrderConfig` ([[Hierarchy Order]]) |
| `Utils/` | `AdvancedPS.Core.Utils` | `APLogger`, `FileSearcher`, `TaskUtils`, `TypeHelper` |
| `Editor/` | `AdvancedPS.Editor` (+ `.Styles`) | APS window (`PopupSystemEditor` + Layer/Order/Displays/Settings panels), inspectors, `CreateAdvancedPopup` |

## Depends on

- [[Invariants]] (package & assembly rules)
